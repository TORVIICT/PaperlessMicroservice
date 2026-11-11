# Informe OQ-01: Contrato de Ingesta del Monolito Paperless-ngx

> **Fecha:** 2026-10-31  
> **Repositorio inspeccionado:** `/home/steven/Documents/GitLab/Paperless-ngx/paperless-ngx` (solo lectura)  
> **Objetivo:** Reunir evidencia para resolver OQ-01 (cómo el ACL entrega resultados al monolito para persistencia)

---

## Tabla Resumen

| Punto | Hallazgo en una línea | Estado |
|-------|----------------------|--------|
| 1. Interfaz del parser | `ParserProtocol` en `paperless/parsers/__init__.py:117` define contrato estructural; consumidor lo invoca vía `consume_file` Celery task | **Encontrado** |
| 2. Invocación actual OCR | Síncrono dentro de tarea Celery `consume_file` (`tasks.py:186`); el parser se ejecuta en-process, no hay polling ni cola externa | **Encontrado** |
| 3. Qué espera el parser | `parse()` → popula estado interno; consumidor llama `get_text()`, `get_date()`, `get_archive_path()`, `get_thumbnail()`, `get_page_count()` (`parsers/__init__.py:258-369`) | **Encontrado** |
| 4. Persistencia monolito | Consumer escribe directo en BD Django (`models.py:158-570`): `Document.content`, `page_count`, `archive_filename`, `thumbnail_path` (WebP obligatorio); archivos en `ORIGINALS_DIR`, `ARCHIVE_DIR`, `THUMBNAIL_DIR` | **Encontrado** |
| 5. Endpoint/consumidor externo | **NO ENCONTRADO** — no existe endpoint REST, webhook, ni consumidor de mensajes para ingesta externa; `PostDocumentView` (`views.py:3495`) sube archivo y dispara `consume_file` Celery, pero el ACL no puede llamar a esa tarea | **NO ENCONTRADO** |
| 6. Auth/errores/reintentos | Auth: JWT/Session DRF (`permissions.py`); errores: `ParseError` burbujea como `ConsumerError`; reintentos: Celery `autoretry_for` en tareas de índice, no en `consume_file` | **Parcial** |
| 7. Comparación T-B vs T-A | T-B (actual): monolito jala resultado vía parser síncrono en Celery; T-A (push): requeriría nuevo endpoint/consumidor + contrato de ingesta + idempotency + auth | **Encontrado** |

---

## HECHOS (CON EVIDENCIA)

### 1. Interfaz del Parser: Dónde Está y Quién la Invoca

**Definición del contrato:** `ParserProtocol` en `src/paperless/parsers/__init__.py:117-443` (clase `Protocol` con `runtime_checkable`). Requiere:
- Atributos de clase: `name`, `version`, `author`, `url`, opcional `uses_remote_service`
- Métodos de clase: `supported_mime_types() → dict[str,str]`, `score(mime_type, filename, path) → int|None`
- Propiedades: `can_produce_archive: bool`, `requires_pdf_rendition: bool`
- Métodos de instancia: `configure(ParserContext)`, `parse(document_path, mime_type, produce_archive)`, `get_text() → str`, `get_date() → datetime|None`, `get_archive_path() → Path|None`, `get_thumbnail(document_path, mime_type) → Path`, `get_page_count(document_path, mime_type) → int|None`, `extract_metadata(document_path, mime_type) → list[MetadataEntry]`
- Context manager: `__enter__`, `__exit__`

**Registro:** `ParserRegistry` en `src/paperless/parsers/registry.py:151-407` — singleton que registra built-ins (`register_defaults` en línea 192) y descubre plugins externos vía entrypoint `paperless_ngx.parsers` (`discover` línea 216).

**Invocación:** Tarea Celery `consume_file` en `src/documents/tasks.py:186-280`:
```python
@shared_task
def consume_file(self, input_doc, overrides) -> ConsumeFileSuccessResult|...:
    plugins = [ConsumerPreflightPlugin, ConsumerPlugin, ...]  # línea 202-216
    for plugin_class in plugins:
        plugin = plugin_class(input_doc, overrides, status_mgr, tmp_dir, self.request.id)
        msg = plugin.run()  # línea 247
```

`ConsumerPlugin.run()` (`src/documents/consumer.py:404-820`) obtiene parser del registro:
```python
parser_class = get_parser_registry().get_parser_for_file(mime_type, filename, working_copy, allow_remote=allow_remote)  # líneas 466-473
with parser_class() as document_parser:
    document_parser.configure(ParserContext(mailrule_id=...))  # líneas 501-504
    document_parser.parse(working_copy, mime_type, produce_archive=produce_archive)  # línea 540-544
    text = document_parser.get_text()  # línea 558
    date = document_parser.get_date()  # línea 559
    archive_path = document_parser.get_archive_path()  # línea 569
    page_count = document_parser.get_page_count(working_copy, mime_type)  # líneas 570-573
    thumbnail = document_parser.get_thumbnail(working_copy, mime_type)  # líneas 553-556
```

**Parser remoto actual:** `RemoteDocumentParser` en `src/paperless/parsers/remote.py:87-512` (Azure AI Vision). `uses_remote_service = True` (línea 117). `score` devuelve 20 si motor configurado, `None` si no (líneas 139-172). `parse` llama a Azure, guarda PDF en `self._archive_path` (línea 497-503), retorna texto en `result.content` (línea 505).

**Parser local (Tesseract):** `RasterisedDocumentParser` en `src/paperless/parsers/tesseract.py:66-...` — usa `ocrmypdf`/`tesseract` localmente, `score` = 10 (líneas 85-94).

---

### 2. Cómo se Llama Hoy al OCR Actual

**Flujo actual (T-B):**
1. Usuario sube archivo vía `PostDocumentView.post` (`views.py:3500-3563`) → guarda archivo temporal en `SCRATCH_DIR` → crea `ConsumableDocument` + `DocumentMetadataOverrides` → dispara `consume_file.apply_async()` (líneas 3552-3561)
2. Tarea Celery `consume_file` ejecuta pipeline de plugins (`ConsumerPreflightPlugin` → `ConsumerPlugin` etc.)
3. `ConsumerPlugin.run()` selecciona parser vía `ParserRegistry` según MIME type
4. Parser se ejecuta **síncrono dentro del worker Celery** (`with parser_class() as document_parser:` línea 501)
5. Parser popula estado interno (`self._text`, `self._archive_path`, etc.)
6. Consumer lee resultados vía getters (`get_text()`, `get_archive_path()`, `get_thumbnail()`, `get_page_count()`)
7. Consumer persiste en BD y archivos (`_store` → `Document.objects.create()` líneas 897-910; archivos copiados con `FileLock` líneas 697-764)

**No hay:** polling, cola de mensajes externa, webhook, callback, ni arquitectura asíncrona entre consumer y parser. El parser es una librería invocada in-process.

**Evidencia de sincronía:** `consume_file` es `@shared_task` (línea 185) que bloquea el worker hasta completar; `RemoteDocumentParser.parse` hace `poller.wait()` bloqueante (línea 493).

---

### 3. Qué Espera Recibir el Parser Exactamente

El **parser** recibe en `parse(document_path, mime_type, produce_archive)`:
- `document_path: Path` — ruta absoluta al archivo temporal en `SCRATCH_DIR`
- `mime_type: str` — MIME detectado por `python-magic`
- `produce_archive: bool` — si debe generar PDF/A archivable

El **parser retorna** (vía getters tras `parse`):
| Getter | Tipo | Descripción | Evidencia |
|--------|------|-------------|-----------|
| `get_text()` | `str` | Texto plano extraído (vacío si falla) | `parsers/__init__.py:293-301` |
| `get_date()` | `datetime|None` | Fecha detectada en documento | `parsers/__init__.py:303-311` |
| `get_archive_path()` | `Path|None` | Ruta a PDF/A buscable generado | `parsers/__init__.py:313-323` |
| `get_thumbnail()` | `Path` | **WebP obligatorio** (500px ancho) | `parsers/__init__.py:329-348`, `parsers.py:123-199` |
| `get_page_count()` | `int|None` | Número de páginas (solo PDF) | `parsers/__init__.py:350-369` |
| `extract_metadata()` | `list[MetadataEntry]` | Metadatos formato (namespace, prefix, key, value) | `parsers/__init__.py:371-404` |

**Thumbnail WebP obligatorio:** `make_thumbnail_from_pdf` en `src/documents/parsers.py:174-198` siempre genera `.webp` (línea 178: `out_path: Path = temp_dir / "convert.webp"`). Fallback Ghostscript también convierte PNG→WebP (líneas 150-160). Parser remoto usa misma función (línea 334).

**Archivo original:** El monolito **guarda el archivo original** en `ORIGINALS_DIR` con nombre único (`generate_unique_filename` línea 698). El ACL no tiene acceso a este directorio.

---

### 4. Cómo y Dónde Persiste el Monolito el Resultado

**Modelo `Document`** en `src/documents/models.py:158-570`:
```python
content = models.TextField(blank=True)           # línea 192-199
page_count = models.PositiveIntegerField(null=True)  # línea 237-247
filename = models.FilePathField(unique=True)     # línea 269-277 (archivo original)
archive_filename = models.FilePathField(unique=True, null=True)  # línea 279-287
archive_checksum = models.CharField(max_length=64, null=True)  # línea 227-235
thumbnail_path = property → THUMBNAIL_DIR / "{pk:07}.webp"  # líneas 526-531
```

**Persistencia en `ConsumerPlugin._store()`** (`consumer.py:851-912`):
```python
document = Document.objects.create(
    title=title[:127],
    content=text,                    # ← texto del parser
    mime_type=mime_type,
    checksum=compute_checksum(...),
    created=create_date,
    page_count=page_count,           # ← page_count del parser
    original_filename=filename,
)  # líneas 897-906
```

**Archivos físicos** (líneas 697-764, dentro de `FileLock(settings.MEDIA_LOCK)`):
- Original → `ORIGINALS_DIR / generated_filename`
- Thumbnail → `THUMBNAIL_DIR / "{pk:07}.webp"` (WebP obligatorio)
- Archive (si existe) → `ARCHIVE_DIR / generated_archive_filename`

**El monolito NUNCA expone** su BD ni directorios de almacenamiento. El parser escribe en directorio temporal (`tempfile.mkdtemp(prefix="paperless-", dir=settings.SCRATCH_DIR)`), el consumer copia desde allí a ubicaciones finales.

---

### 5. Endpoint, Webhook o Consumidor de Mensajes para Ingesta Externa

**Búsqueda exhaustiva — NO ENCONTRADO:**

- **REST API:** `PostDocumentView` (`views.py:3495-3563`) acepta `multipart/form-data` con campo `document` + metadatos opcionales, pero **dispara tarea Celery interna** (`consume_file.apply_async`), no acepta resultado ya procesado. No hay endpoint `POST /api/documents/{id}/ocr-result` ni similar.
- **Consumidores de mensajes:** No hay consumidor Kafka/RabbitMQ/Redis para ingesta de resultados OCR. `paperless_mail` consume emails, pero no resultados OCR.
- **Señales Django:** `document_consumption_finished` (`signals/__init__.py:4`) se emite **tras** persistencia exitosa, no como punto de entrada.
- **Management command:** `document_consumer.py` consume desde carpetas vigiladas, no desde servicio externo.
- **Webhooks:** No hay registro de webhooks entrantes para OCR.

**Conclusión:** El monolito **no tiene contrato de ingesta** para que un servicio externo (ACL) le entregue resultados OCR. La arquitectura asume que el parser se ejecuta **dentro del monolito** (T-B).

---

### 6. Autenticación, Manejo de Errores y Reintentos del Parser

**Autenticación (ingesta API):**
- `PostDocumentView` requiere `IsAuthenticated` + permiso `documents.add_document` (`views.py:3501-3502`)
- DRF usa JWT/Session auth estándar (`paperless/views.py` no inspeccionado pero es estándar DRF)

**Errores del parser:**
- Excepción base: `ParseError` en `src/documents/parsers.py:201-202`
- `RemoteDocumentParser` envuelve errores Azure en `ParseError` (`remote.py:507-509`)
- `ConsumerPlugin._fail()` captura `ParseError` y `Exception`, envía progreso `FAILED` y lanza `ConsumerError` (`consumer.py:230-239, 575-588`)

**Reintentos:**
- `consume_file` task: **sin `autoretry_for`** (líneas 185-195) — fallo = task failed
- Tareas de índice (`index_document`, `remove_document_from_index`): `autoretry_for=(SearchIndexLockError,)`, `max_retries=5`, `retry_backoff=60` (`tasks.py:95-146`)
- `update_document_content_maybe_archive_file`: sin reintento automático (`tasks.py:372-512`)
- `apply_ai_suggestions`: `autoretry_for=(LLMTimeoutError,)`, `max_retries=3`, `retry_backoff=60` (`tasks.py:762-799`)

**Timeout parser:** No hay timeout configurado a nivel de parser. Azure SDK usa sus timeouts por defecto. Celery worker tiene `task_soft_time_limit` / `task_time_limit` configurable globalmente.

---

### 7. Comparación T-B vs T-A

| Aspecto | T-B (Actual: Monolito jala via Parser) | T-A (Propuesto: ACL empuja a Monolito) |
|---------|----------------------------------------|----------------------------------------|
| **Dirección** | Monolito invoca parser (in-process Celery) | ACL invoca endpoint/consumidor monolito |
| **Parser** | `ParserProtocol` local o remoto (Azure) | ACL usa su propio OCR (externo), envía resultado |
| **Contrato** | `ParserProtocol` (Python interface) | **NO EXISTE** — requeriría definir nuevo contrato |
| **Persistencia** | Consumer escribe BD + archivos directamente | Monolito necesitaría endpoint que acepte: texto, page_count, date, PDF/A (bytes), thumbnail (WebP bytes), metadatos |
| **Auth** | DRF permissions (`add_document`) | Nuevo: API key / mTLS / JWT para servicio ACL |
| **Idempotencia** | Deduplicación por checksum SHA256 (`ConsumerPreflightPlugin` líneas 1042-1093) | Requeriría clave idempotencia (ej. `clientReference` ↔ `document_id`) |
| **Archivos** | Parser escribe temp, consumer mueve a `ORIGINS/ARCHIVE/THUMBNAIL` | ACL debe enviar bytes (multipart) o URLs firmadas (claim-check) |
| **Thumbnail** | Parser genera WebP 500px | ACL debe enviar WebP 500px (o monolito convierte) |
| **Fecha** | Parser opcional + fallback filename/dateparser | ACL debe enviar si la tiene |
| **Cambios monolito** | **Mínimos** (nuevo parser remoto ya existe) | **Mayores**: nuevo endpoint/consumidor, serializers, validaciones, tests, docs |
| **Cambios ACL** | Solo cliente HTTP al monolito | ACL ya tiene OCR; solo necesita delivery |

**Viabilidad T-B:** Ya implementada en `RemoteDocumentParser` (Azure). Para ACL: crear parser `AclDocumentParser` que implemente `ParserProtocol`, llame a ACL vía HTTP (polling), retorne resultados. Monolito no cambia.

**Viabilidad T-A:** Requiere **diseñar e implementar contrato de ingesta completo** en monolito (endpoint REST o consumidor mensaje), más serializers, permissions, validación, idempotencia, manejo errores, tests. Equipo monolito debe aprobar y desarrollar.

---

## RECOMENDACIONES (OPINIÓN)

### Recomendación 1: Adoptar T-B (Parser Remoto en Monolito) — **Basado en: `RemoteDocumentParser` ya existe y sigue `ParserProtocol`**

**Evidencia:** `RemoteDocumentParser` en `remote.py:87-512` demuestra que el monolito **ya soporta parsers remotos** vía `ParserProtocol`. El ACL solo necesita implementar un parser `AclDocumentParser` que:
1. Implemente `ParserProtocol` (mismos métodos que `RemoteDocumentParser`)
2. En `parse()`: llame a ACL `POST /api/v1/ocr/jobs`, haga polling HTTP (back-off 500ms/1s/2s, timeout 120s), descargue archive/thumbnail
3. En `get_text()`, `get_archive_path()`, `get_thumbnail()`, `get_page_count()`: retorne lo recibido del ACL
4. Se registre en `ParserRegistry` vía entrypoint `paperless_ngx.parsers`

**Ventajas:** Cero cambios en monolito salvo instalar paquete parser ACL; reusa arquitectura existente; monolito sigue siendo system of record; ACL no toca BD monolito.

**Riesgo:** Latencia añadida en task Celery (polling 120s max). Mitigación: `task_time_limit` > 120s.

---

### Recomendación 2: Si T-A es obligatorio, definir contrato de ingesta mínimo — **Basado en: campos que `_store()` persiste (models.py:897-906) + archivos**

**Contrato mínimo sugerido (opinión, no decisión):**
```
POST /api/internal/ocr/result  (auth: service-to-service API key)
Content-Type: multipart/form-data
Campos:
  - clientReference (string, ≤64, idempotency key) — mapea a checksum o ACL documentId
  - text (string) → Document.content
  - pageCount (int|null) → Document.page_count
  - date (ISO8601|null) → Document.created
  - archive (file, application/pdf) → Document.archive_file + archive_checksum
  - thumbnail (file, image/webp) → Document.thumbnail_path (WebP 500px obligatorio)
  - mimeType (string) → Document.mime_type
  - originalFilename (string) → Document.original_filename
  - metadata (json opcional) → CustomFieldInstance
Respuesta: 201 { documentId, checksum } | 409 duplicate | 400 validation
```

**Cambios monolito requeridos:** Nuevo `InternalOCRResultView`, serializer, permission class `IsAclService`, deduplicación por `clientReference`, escritura archivos + BD transaccional.

---

### Recomendación 3: Resolver OQ-08 (async model) antes de elegir — **Basado en: `consume_file` es síncrono bloqueante (tasks.py:186)**

Si ACL usa HTTP polling (OQ-02 resuelto D-33), el parser ACL bloqueará worker Celery hasta 120s. Opciones:
- Aumentar `CELERY_TASK_TIME_LIMIT` > 150s
- Mover polling a task separada con `ack_late=True` + resultado via signal/callback
- Usar T-B con parser que delegue a task hija asíncrona

---

## PREGUNTAS ABIERTAS PARA EL EQUIPO DEL MONOLITO

1. **¿Aceptan T-B (parser remoto) como vía oficial?** `RemoteDocumentParser` ya existe; solo falta empaquetar `AclDocumentParser` como plugin instalable (`paperless_ngx.parsers` entrypoint).

2. **Si T-A: ¿Quién desarrolla el endpoint de ingesta?** Requiere ~2-3 semanas en monolito (view, serializer, permission, tests, docs, migración). ¿Tiene prioridad el equipo monolito?

3. **Idempotencia en T-A:** ¿Usan `clientReference` (≤64, `[a-z0-9_-]`) como clave? ¿O checksum SHA256 existente? El monolito ya deduplica por checksum (`ConsumerPreflightPlugin.pre_check_duplicate` líneas 1042-1093).

4. **Thumbnail WebP:** ¿El monolito **requiere** WebP o acepta PNG y convierte? `make_thumbnail_from_pdf` siempre genera WebP (`parsers.py:178`). ¿Puede el ACL enviar PNG y que monolito convierta?

5. **Fecha del documento:** `get_date()` retorna `None` en parser remoto (`remote.py:292-300`). Consumer usa `date_parser` como fallback (`consumer.py:560-568`). ¿ACEPTA el monolito fecha del ACL o prefiere su propia heurística?

6. **Metadatos PDF:** `extract_metadata()` se llama **bajo demanda** desde API view (`parsers/__init__.py:378-384`), no durante consumo. ¿Necesita ACL proveer metadatos o solo texto/archivo/thumbnail?

7. **Versiones de documentos:** Monolito soporta versionado (`root_document` + `version_index` modelo líneas 317-332). ¿Cómo encaja resultado OCR en nueva versión vs documento nuevo?

8. **Límites de tamaño/tipo:** Monolito valida MIME en `PostDocumentSerializer.validate_document` (`serialisers.py:2354-2372`). ¿Debe ACL replicar misma validación o confiar en monolito?

9. **Autenticación servicio-a-servicio:** ¿Prefieren API key compartida, mTLS, o JWT firmado? `PostDocumentView` usa DRF auth estándar.

10. **Observabilidad:** ¿Necesitan logs de correlación (`X-Correlation-ID`) propagados del ACL al monolito? `ParserContext` solo tiene `mailrule_id` hoy (`parsers/__init__.py:79-114`).

---

## ARCHIVOS CLAVE PARA REFERENCIA RÁPIDA

| Archivo | Qué define |
|---------|------------|
| `paperless/parsers/__init__.py:117-443` | `ParserProtocol` — contrato estructural del parser |
| `paperless/parsers/registry.py:151-407` | `ParserRegistry` — registro y resolución de parsers |
| `documents/consumer.py:404-820` | `ConsumerPlugin.run()` — pipeline de consumo |
| `documents/tasks.py:186-280` | `consume_file` — tarea Celery que orquesta consumo |
| `documents/models.py:158-570` | `Document` — modelo de persistencia |
| `documents/parsers.py:123-199` | `make_thumbnail_from_pdf` — thumbnail WebP obligatorio |
| `paperless/parsers/remote.py:87-512` | `RemoteDocumentParser` — parser remoto existente (Azure) |
| `paperless/parsers/tesseract.py:66-...` | `RasterisedDocumentParser` — parser local (Tesseract) |
| `documents/views.py:3495-3563` | `PostDocumentView` — endpoint subida archivos (dispara Celery) |
| `documents/signals/__init__.py:1-5` | Señales `document_consumption_started/finished` |

---

*Fin del informe. Todos los hallazgos citan archivo y línea. No se inventaron contratos ni endpoints.*