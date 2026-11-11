# Informe OQ-01 (Post-Document): Análisis de la subida de documentos en Paperless-ngx

> **Objetivo:** Documentar cómo funciona `PostDocumentView` / `post_document` para evaluar si el ACL puede entregar al monolito el PDF/A que ya produjo el servicio OCR, sin que Paperless repita el OCR (OQ-01).
> **Repositorio inspeccionado:** `/home/steven/Documents/GitLab/Paperless-ngx/paperless-ngx` (solo lectura)
> **Fecha:** 2026-10-31

---

## Tabla Resumen

| Punto | Hallazgo en una línea | Estado |
|-------|----------------------|--------|
| 1. Endpoint y auth | `POST /api/documents/post_document/`, `MultiPartParser`, `IsAuthenticated` + `documents.add_document` | **Encontrado** |
| 2. Campos y validación | `document` (FileField requerido), `title`, `correspondent`, `document_type`, `storage_path`, `tags`, `created`, `archive_serial_number`, `custom_fields`; MIME validado con `magic` | **Encontrado** |
| 3. Respuesta y consulta | Retorna `task_id` (Celery); consulta vía `GET /api/tasks/?task_id=...` → `result_data.document_id` | **Encontrado** |
| 4. Duplicados (checksum) | SHA256 del archivo; si existe y `CONSUMER_DELETE_DUPLICATES=true` → error `DOCUMENT_ALREADY_EXISTS` | **Encontrado** |
| 5. Parser PDF y OCR_MODE | `RasterisedDocumentParser` (Tesseract/ocrmypdf); `auto`/`force`/`redo`/`off` controlan `skip_text`, `force_ocr`, `redo_ocr`; `Document.content` = texto extraído | **Encontrado** |
| 6. PDF/A y ARCHIVE_FILE_GENERATION | Original se guarda en `ORIGINALS_DIR`; `archive_path` generado por parser (ocrmypdf/Ghostscript); `ARCHIVE_FILE_GENERATION` ∈ {auto, always, never}; miniatura **sí** generada por monolito (WebP 500px) | **Encontrado** |
| 7. Fecha y título por defecto | Fecha: `created` param → parser date → `st_mtime`; Título: `title` param → `filename.stem` | **Encontrado** |
| 8. OCR_MODE por documento | **Global** (env `PAPERLESS_OCR_MODE` + DB `ApplicationConfiguration.mode`); no por request | **Encontrado** |
| 9. Límites tiempo/concurrencia | Celery `CELERY_TASK_TIME_LIMIT` = 1800s (30 min) por env `PAPERLESS_WORKER_TIMEOUT`; worker concurrency configurable | **Encontrado** |
| 10. Lo no determinable | Requiere ejecución real: límites de tamaño archivo, comportamiento exacto con PDF/A pre-existente, miniatura WebP exacta, límite concurrencia real | **NO ENCONTRADO** |

---

## HECHOS (CON EVIDENCIA)

### 1. Ruta, método, tipo de contenido y clases de autenticación/permisos

**Archivo:** `src/paperless/urls.py:149-153`
```python
re_path(
    "^post_document/",
    PostDocumentView.as_view(),
    name="post_document",
),
```

**Archivo:** `src/documents/views.py:3495-3503`
```python
class PostDocumentView(GenericAPIView[Any]):
    permission_classes = (IsAuthenticated,)
    serializer_class = PostDocumentSerializer
    parser_classes = (parsers.MultiPartParser,)

    def post(self, request, *args, **kwargs):
        if not request.user.has_perm("documents.add_document"):
            return HttpResponseForbidden("Insufficient permissions")
```

- **Método:** `POST`
- **Content-Type:** `multipart/form-data` (`MultiPartParser`)
- **Autenticación:** DRF `IsAuthenticated` (JWT/Session/Token según configuración DRF)
- **Permiso:** `documents.add_document` (permission class `PaperlessObjectPermissions` + `IsAuthenticated`)
- **Credencial externa:** Un servicio externo (ACL) necesitaría un token JWT válido de un usuario con permiso `documents.add_document`, o un token de API (`PaperlessObtainAuthTokenView` en `/api/token/`). No existe autenticación servicio-a-servicio nativa (API key / mTLS) para este endpoint.

### 2. Campos aceptados y su validación

**Archivo:** `src/documents/serialisers.py:2282-2449` (`PostDocumentSerializer`)

| Campo | Tipo | Requerido | Validación |
|-------|------|-----------|------------|
| `document` | `FileField` | **Sí** | MIME detectado con `magic.from_buffer()`; debe estar en `is_mime_type_supported()` (PDF, PNG, JPEG, TIFF, GIF, BMP, WebP, HEIC, etc.) |
| `title` | `CharField` | No | Máx 127 chars (truncado en `_store`) |
| `correspondent` | `PrimaryKeyRelatedField` | No | FK a `Correspondent` |
| `document_type` | `PrimaryKeyRelatedField` | No | FK a `DocumentType` |
| `storage_path` | `PrimaryKeyRelatedField` | No | FK a `StoragePath` |
| `tags` | `PrimaryKeyRelatedField` (many) | No | FKs a `Tag` |
| `created` | `DateTimeField` | No | Acepta datetime; se usa solo la fecha |
| `archive_serial_number` | `IntegerField` | No | Rango `[0, 0xFFFFFFFF]` |
| `custom_fields` | `JSONField` | No | Dict `id→value` o lista de IDs |
| `from_webui` | `BooleanField` | No | Solo para logging/trigger_source |

**Validación MIME** (`serialisers.py:2354-2372`): usa `magic.from_buffer()`; rechaza si `is_mime_type_supported(mime_type)` es False. PDFs con MIME recuperable (ej. `application/octet-stream` pero extensión `.pdf`) se aceptan y se intenta recuperar con `qpdf`.

**Límite de tamaño:** No hay límite explícito en serializer ni settings (`DATA_UPLOAD_MAX_MEMORY_SIZE` no configurado → usa default Django 2.5MB). **Requiere prueba real** para confirmar límite efectivo.

### 3. Qué responde el endpoint y cómo se consulta el resultado

**Respuesta exitosa** (`views.py:3563`):
```python
return Response(async_task.id)  # 200 OK, body = "celery-task-uuid"
```

**Consulta de resultado:**
- Endpoint: `GET /api/tasks/?task_id=<celery-task-id>` (o `GET /api/tasks/<pk>/`)
- Serializer: `TaskSerializerV10` (`serialisers.py:2636-2678`)
- Campos relevantes en respuesta:
  - `status`: `pending` | `started` | `success` | `failure` | `revoked`
  - `result_data`: JSON con `document_id` (y opcional `duplicate_of`, `duplicate_in_trash`)
  - `related_document_ids`: lista de IDs de documento creados
  - `date_done`, `duration_seconds`, `status_display`

**Ejemplo `result_data` éxito** (`data_models.py:214-220`):
```json
{
  "document_id": 123
}
```
**Ejemplo `result_data` duplicado** (`data_models.py:207-220`):
```json
{
  "duplicate_of": 456,
  "duplicate_in_trash": false
}
```

### 4. Archivo duplicado (checksum)

**Archivo:** `src/documents/consumer.py:1042-1093` (`ConsumerPreflightPlugin.pre_check_duplicate`)

- **Checksum:** SHA256 del archivo original (`compute_checksum`)
- **Búsqueda:** `Document.global_objects.filter(Q(checksum=checksum) | Q(archive_checksum=checksum))`
- **Si existe y `CONSUMER_DELETE_DUPLICATES=true`** (default: env `PAPERLESS_CONSUMER_DELETE_DUPLICATES` no seteado → `False`):
  - Elimina archivo temporal
  - Lanza `ConsumeFileDuplicateError` → tarea termina con `status=failure`, `result_data = {"duplicate_of": <id>, "duplicate_in_trash": bool}`
  - Respuesta HTTP del endpoint original: **200 OK** con `task_id`; el error está en `result_data` al consultar la tarea
- **Si `CONSUMER_DELETE_DUPLICATES=false` (default):** Solo log warning; continua procesamiento normal (crea documento duplicado en BD con mismo checksum)

**No hay opción en el request para forzar/permitir duplicados.** El comportamiento es global vía setting.

### 5. Parser para `application/pdf` y comportamiento por `OCR_MODE`

**Parser seleccionado:** `RasterisedDocumentParser` (Tesseract/ocrmypdf) — registrado en `paperless/parsers/registry.py:202-210` con score 10.

**Archivo:** `src/paperless/parsers/tesseract.py:497-663` (`parse()`)

**Detección de texto existente** (`tesseract.py:514-520`):
```python
text_original = self.extract_text(None, document_path)
original_has_text = is_born_digital_text(text_original, document_path, log=self.log)
```

**Comportamiento por `OCR_MODE`** (`tesseract.py:533-663`):

| Modo | `produce_archive=False` | `produce_archive=True` | `Document.content` resultante |
|------|------------------------|------------------------|------------------------------|
| **`off`** | Solo `pdftotext` → `self.text = text_original` | PDF/A via Ghostscript **sin OCR**; `self.text = text_original` | Texto original del PDF (si existe) o vacío |
| **`auto`** | Si `original_has_text` → salta ocrmypdf; `self.text = text_original` | Si `original_has_text` → `skip_text=True` (solo PDF/A, no OCR); si no → OCR completo | Texto original (si existía) o texto OCR |
| **`force`** | OCR completo siempre (`force_ocr=True`) | OCR completo + PDF/A | Texto OCR (re-generado) |
| **`redo`** | OCR completo siempre (`redo_ocr=True`) | OCR completo + PDF/A | Texto OCR (re-generado, descarta texto previo) |

**Notas clave:**
- `OCR_MODE` se lee de `OcrConfig().mode` → `app_config.mode` (DB) o `settings.OCR_MODE` (env `PAPERLESS_OCR_MODE`, default `auto`)
- **No hay forma de pasar `OCR_MODE` por request** — es global (ver punto 8)
- Si `produce_archive=False` (ej. `ARCHIVE_FILE_GENERATION=never`), el parser puede saltar ocrmypdf entero en modo `auto`/`off`
- `Document.content` = `self.text` extraído por parser (`consumer.py:558` → `_store()` → `Document.content=text`)

### 6. Qué se guarda con PDF/A y `ARCHIVE_FILE_GENERATION`

**Archivo original:** Siempre se guarda en `ORIGINALS_DIR` con nombre único (`generate_unique_filename`, `consumer.py:698-722`).

**Archivo archive (PDF/A):**
- **Generación:** Decidida en `should_produce_archive()` (`consumer.py:127-185`) según:
  - `parser.requires_pdf_rendition` (formatos no visualizables en browser → siempre)
  - `parser.can_produce_archive` (parser soporta archive)
  - `OcrConfig().archive_file_generation` ∈ `{auto, always, never}` (env `PAPERLESS_ARCHIVE_FILE_GENERATION`, default `auto`)
  - Lógica `auto`: imágenes → sí; PDF born-digital con texto → no; PDF escaneado/sin texto → sí
- **Generación:** El parser (`RasterisedDocumentParser`) produce `archive_path` via:
  - `ocrmypdf` (si OCR) → PDF/A con capa de texto
  - `_convert_pdf_to_pdfa()` via Ghostscript (si `mode=off` o `skip_text=True`) — **sin OCR**
  - `_convert_image_to_pdfa()` via img2pdf + pikepdf (imágenes)
- **Archivo original:** **Siempre se conserva** tal cual en `ORIGINALS_DIR` (`consumer.py:717-722`). No se sobrescribe.
- **Archivo archive:** Se guarda en `ARCHIVE_DIR` con nombre único (`consumer.py:729-755`). `Document.archive_filename` y `archive_checksum` poblados.
- **Miniatura:** **Sí, la genera el monolito** (`consumer.py:553-556` → `document_parser.get_thumbnail()` → WebP 500px). El parser devuelve `Path` a WebP en temp dir; consumer copia a `THUMBNAIL_DIR` (`consumer.py:724-727`). Formato **WebP obligatorio** (500px ancho).

**Implicación para ACL:** Si ACL entrega PDF/A ya procesado, el monolito **aún intentará generar su propio archive** salvo que `ARCHIVE_FILE_GENERATION=never` o el parser detecte que no es necesario (`should_produce_archive=False`). No hay mecanismo nativo para "inyectar" un archive externo y saltar la generación.

### 7. Fecha y título si no se envían

**Archivo:** `src/documents/consumer.py:851-890` (`_store()`)

**Fecha (`created`):**
1. `metadata.created` (parámetro `created` del request) — **prioridad 1**
2. `date` devuelto por `parser.get_date()` (metadatos del PDF) — **prioridad 2**
3. `st_mtime` del archivo original — **prioridad 3** (fallback)

**Título (`title`):**
1. `metadata.title` (parámetro `title` del request) — **prioridad 1**
2. `metadata.filename` (parámetro `filename` implícito del upload) → `Path(filename).stem` — **prioridad 2**
3. `original_file.stem` — **prioridad 3** (fallback)

### 8. `OCR_MODE` por documento o global

**Archivo:** `src/paperless/config.py:77` (`OcrConfig.__post_init__`)
```python
self.mode = app_config.mode or ModeChoices(settings.OCR_MODE)
```

- **Global:** Se lee de `ApplicationConfiguration.mode` (tabla `paperless_applicationconfiguration` en BD) o fallback a `settings.OCR_MODE` (env `PAPERLESS_OCR_MODE`, default `auto`).
- **No hay campo en `PostDocumentSerializer` ni en `DocumentMetadataOverrides`** para especificar `OCR_MODE` por request.
- **Remote OCR** (`RemoteDocumentParser`) usa `RemoteOCRConfig().remote_ocr_mode` (similar: global `PAPERLESS_REMOTE_OCR_MODE`).
- **No hay mecanismo** para pasar `OCR_MODE` por documento vía API `post_document`.

### 9. Límites de tiempo y concurrencia para entrega desde ACL

**Tiempo de tarea Celery:**
- `CELERY_TASK_TIME_LIMIT = get_int_from_env("PAPERLESS_WORKER_TIMEOUT", 1800)` → **30 minutos** por defecto (`settings/__init__.py:709`)
- Aplicable a `consume_file` task (`tasks.py:186`)

**Concurrencia:**
- Worker concurrency: configurable vía `celery worker --concurrency` o `PAPERLESS_WORKER_CONCURRENCY` (no hardcodeado en settings)
- `THREADS_PER_WORKER` para ocrmypdf (`settings.THREADS_PER_WORKER`, default CPU count)

**Implicaciones para ACL:**
- ACL debe completar polling OCR externo y entregar resultado **antes de 30 min** (o ajustar `PAPERLESS_WORKER_TIMEOUT`)
- Si ACL usa `POST /api/documents/post_document/`, la tarea `consume_file` se encola y el monolito procesa en background; ACL no controla el worker
- Si ACL quiere **entregar resultado directamente** (evitando OCR monolito), necesitaría endpoint alternativo que no pase por `consume_file` — **no existe hoy**

### 10. Lo NO determinable leyendo código (requiere ejecución real)

| Aspecto | Por qué no se sabe |
|---------|-------------------|
| Límite real de tamaño de archivo upload | Django `DATA_UPLOAD_MAX_MEMORY_SIZE` no configurado (default 2.5MB); nginx/proxy pueden imponer límite adicional |
| Comportamiento exacto al subir PDF/A ya procesado | ¿El parser detecta PDF/A y salta OCR? ¿`original_has_text` detecta texto en capa invisible? |
| Miniatura exacta (WebP 500px) — calidad, compresión | `make_thumbnail_from_pdf` usa `convert` con `scale="500x5000>"` pero calidad JPEG/WEBP no visible en código |
| Concurrencia real de workers | Depende de deployment (systemd, docker, k8s); no en código |
| Límite real `DATA_UPLOAD_MAX_MEMORY_SIZE` | Django default 2.5MB; si nginx/proxy tienen `client_max_body_size` distinto, gana el menor |
| Comportamiento `CONSUMER_DELETE_DUPLICATES` con archivos en papelera | Código maneja `duplicates_in_trash` pero no probado |
| Qué pasa si ACL entrega PDF/A y `ARCHIVE_FILE_GENERATION=auto` | ¿`should_produce_archive` detecta PDF/A y retorna False? No hay lógica explícita para detectar PDF/A |
| Tiempo real de procesamiento PDF grande | Depende de CPU, `THREADS_PER_WORKER`, `OCR_MODE`, páginas |

**Pruebas manuales sugeridas:**
1. Subir PDF de 10MB, 50MB, 100MB → verificar límite real
2. Subir PDF/A-2b generado externamente → verificar `original_has_text`, `original_has_text`, `should_produce_archive`
3. Subir PDF born-digital con texto → verificar `original_has_text=True`, `skip_text=True` en `auto`
4. Subir PDF escaneado sin texto → verificar OCR completo, `Document.content` poblado
5. Subir duplicado exacto → verificar `CONSUMER_DELETE_DUPLICATES=true/false`
6. Medir tiempo real `consume_file` para PDF 50 páginas en `auto`/`force`/`off`
7. Verificar miniatura WebP generada (dimensiones, calidad, formato)
8. Probar `ARCHIVE_FILE_GENERATION=never` con PDF → ¿se guarda `archive_filename`?

---

## RECOMENDACIONES (OPINIÓN)

### 1. Para entregar PDF/A desde ACL sin repetir OCR: **Opción T-B (Parser Remoto)**
- Implementar `AclDocumentParser` en Paperless que implemente `ParserProtocol` y delegue al ACL vía HTTP
- Ventaja: Reutiliza pipeline existente (`consume_file`, `should_produce_archive`, thumbnails, BD); cero cambios en monolito salvo instalar parser
- Requiere: ACL exponga endpoint `POST /ocr/jobs` + polling compatible con `ParserProtocol` (`parse()`, `get_text()`, `get_archive_path()`, `get_thumbnail()`, `get_page_count()`)

### 2. Si se prefiere T-A (ACL empuja a monolito): **Nuevo endpoint interno**
- Crear `POST /api/internal/ocr/result` (auth: service-to-service API key) que acepte:
  - `clientReference` (idempotency key) → mapea a checksum o `documentId`
  - `text`, `pageCount`, `date`, `archive` (file, application/pdf), `thumbnail` (file, image/webp)
  - `mimeType`, `originalFilename`
- Monolito: valida, deduplica por `clientReference`/checksum, escribe BD + archivos, retorna `documentId`
- Requiere desarrollo en monolito (~2-3 semanas)

### 3. Evitar duplicados desde ACL
- ACL debe calcular SHA256 del archivo **antes** de subir y consultar si ya existe (vía endpoint búsqueda por checksum — **no existe hoy**, habría que añadirlo)
- O usar `clientReference` como clave de idempotencia en endpoint T-A

### 4. Para evitar OCR monolito con PDF/A pre-procesado
- Setear `PAPERLESS_ARCHIVE_FILE_GENERATION=never` en monolito (pero afecta globalmente)
- O implementar detección de PDF/A en `should_produce_archive()` (requiere cambio en monolito)
- O usar `OCR_MODE=off` + `ARCHIVE_FILE_GENERATION=never` (pero pierde OCR en docs sin texto)

### 5. Miniatura WebP
- Si ACL genera miniatura, debe ser **WebP 500px ancho** para coincidir con formato monolito
- Monolito **siempre genera la suya** salvo que se desactive generación de archive (y aún así `get_thumbnail()` se llama siempre en `consumer.py:553`)

---

## PREGUNTAS ABIERTAS PARA EL EQUIPO DEL MONOLITO

1. **¿Aceptan añadir detección de PDF/A en `should_produce_archive()`?** (para que PDF/A externo no regenere archive)
2. **¿Existe o planean endpoint "internal OCR result" (T-A)?** Si no, ¿aceptarían PR para `AclDocumentParser` (T-B)?
3. **¿Puede `OCR_MODE` ser pasado por documento en `post_document`?** Hoy es solo global.
4. **¿Cuál es el límite real de upload en producción?** (nginx `client_max_body_size`, Django `DATA_UPLOAD_MAX_MEMORY_SIZE`)
5. **¿Cómo manejan miniatura WebP si ACL la provee?** ¿Aceptan `thumbnail` como campo opcional en ingesta T-A?
6. **¿Qué ocurre si `CONSUMER_DELETE_DUPLICATES=true` y el duplicado está en papelera?** ¿Se restaura o error?
7. **¿`PAPERLESS_WORKER_TIMEOUT=1800` es suficiente para PDFs grandes en `force`?** ¿Han medido timeouts reales?
8. **¿Soportan autenticación servicio-a-servicio (mTLS, API key) para endpoints internos?** Hoy solo JWT/Session user auth.
9. **¿Puede `OCR_MODE` ser pasado por request en el futuro?** ¿O solo global?
10. **¿Qué validación hacen de PDF/A entrante?** ¿Verifican conformidad PDF/A-2b o confían en el origen?

---

*Fin del informe. Todos los hechos citan archivo y línea. No se inventaron contratos ni payloads.*