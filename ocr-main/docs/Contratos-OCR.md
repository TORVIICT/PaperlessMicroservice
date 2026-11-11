Documentación de Contratos del Microservicio OCR

Este documento detalla los contratos de comunicación (HTTP/REST y Eventos Asíncronos) del microservicio OCR, la trazabilidad directa de cada campo con el código monolítico de Paperless-ngx, el catálogo estandarizado de errores y ejemplos de integración en cURL, Python y C#.

---

## 1. Justificación y Origen de los Contratos

El monolito Paperless-ngx procesa el OCR de manera local sincrónica/asincrónica mediante Celery y la clase `RasterisedDocumentParser` (en `src/paperless/parsers/tesseract.py`). 

Al desacoplar el OCR hacia un microservicio independiente en C# (.NET), dividimos la interacción en dos niveles:

1. **Contrato Externo (Paperless-ngx ↔ Microservicio OCR vía HTTP REST):**
   - **Por qué REST asíncrono (`202 Accepted`):** El procesamiento de OCR (especialmente en PDFs extensos de alta resolución) toma de varios segundos a minutos. Mantener una conexión HTTP bloqueada saturaría los sockets y causaría *timeouts* de pasarela (Gateway timeouts). Por ello, se implementa el patrón **Asynchronous Request-Reply** (solicitud `POST` que devuelve `202 Accepted` y URL de consulta en el encabezado `Location`).
   - **Aislamiento de dominio:** Paperless-ngx maneja modelos de Django (`Document`, `Task`, etc.). El microservicio OCR **no conoce ni debe conocer** la base de datos de Django. Utiliza `clientReference` para desacoplar ambos dominios.

2. **Contratos de Eventos Asíncronos (RabbitMQ / Bus de Mensajería):**
   - **Cola interna de trabajo (`ocr.job.process`):** La API desacopla la recepción del archivo de su procesamiento intensivo en CPU, delegándolo a `Ocr.Worker`.
   - **Eventos de dominio (`ocr.completed`, `ocr.failed`):** El microservicio de OCR emite un evento cuando termina un documento. El **Microservicio de Búsqueda** (desarrollado por otro miembro del equipo) consume directamente `ocr.completed` para indexar el texto en el motor de búsqueda sin necesidad de consultar al monolito Paperless ni a la base de datos de Django.

---

## 2. Trazabilidad: Del Monolito al Microservicio

A continuación se muestra el mapeo exacto entre el código fuente legado de Paperless-ngx (versión 3.2.1) y los campos definidos en los contratos:

> **Contrato interno heredado.** La API pública y versionada que consume el
> frontend es `/api/v1/documents` y está documentada en
> `contracts/ocr-api.openapi.yaml`. Las rutas `/api/v1/ocr/jobs` descritas en
> este documento se conservan solo como contrato interno de compatibilidad y
> no deben usarse desde el frontend ni exponerse a través de Kong.

### 2.1. Solicitud de Entrada (`POST /api/v1/ocr/jobs`)

| Parámetro del Contrato | Tipo | Origen en Paperless-ngx | Explicación Técnica y Razón de Diseño |
|---|---|---|---|
| `file` | Binario (`multipart/form-data`) | `DocumentParser.parse(document_path, ...)` | El archivo original que Paperless extrae del consumo (`consume/`) o correo. |
| `clientReference` | `string` (opaco) | `Document.id` o Celery `task_id` | Identificador de Paperless. El OCR no debe tener FK hacia Django; permite correlacionar el resultado. |
| `language` | `string` (ej. `spa+eng`) | `PAPERLESS_OCR_LANGUAGE` / `OcrConfig.language` | Códigos ISO 639-2 reconocidos por Tesseract separados por `+`. |
| `mode` | `enum` (`skip`, `redo`, `force`) | `PAPERLESS_OCR_MODE` / `ModeChoices` | Determina la estrategia de OCRmyPDF (`--skip-text`, `--redo-ocr`, `--force-ocr`). |
| `deskew` | `boolean` | `PAPERLESS_OCR_DESKEW` / `OcrConfig.deskew` | Endereza páginas inclinadas. Incompatible en OCRmyPDF con `mode=redo`. |
| `rotatePages` | `boolean` | `PAPERLESS_OCR_ROTATE_PAGES` / `OcrConfig.rotate` | Activa `--rotate-pages` para orientar automáticamente hojas apaisadas. |

### 2.2. Respuesta de Consulta (`GET /api/v1/ocr/jobs/{jobId}`)

| Campo del Contrato | Tipo | Origen en Paperless-ngx | Destino / Consumo |
|---|---|---|---|
| `jobId` | `UUID` | Generado por el OCR | Identificador único del trabajo para idempotencia y polling. |
| `status` | `enum` (`pending`, `processing`, `completed`, `failed`) | Ciclo de vida interno del OCR | Estado del procesamiento. |
| `result.text` | `string` | `RasterisedDocumentParser.get_text()` | Texto extraído para almacenamiento en Paperless y para el servicio de Búsqueda. |
| `result.pageCount` | `integer` | `get_page_count_for_pdf()` | Número de páginas para metadatos del documento. |
| `result.durationMs` | `integer` | Medido durante la invocación del motor | Observabilidad y métricas de rendimiento (SLA). |
| `result.artifacts.pdfa` | Referencia URI | `RasterisedDocumentParser.get_archive_path()` | Documento archivado con texto OCR incrustado (PDF/A-2b). |
| `result.artifacts.thumbnail` | Referencia URI | `RasterisedDocumentParser.get_thumbnail()` | Imagen miniatura generada (WebP) para previsualización en la UI. |

---

## 3. Catálogo Estandarizado de Errores

En el monolito, las excepciones de OCRmyPDF y Ghostscript eran capturadas como `ParseError` o advertencias de log. En el microservicio, se estandarizan bajo **RFC 9457 (Problem Details)** para errores HTTP y un catálogo tipificado para fallos del job (`error.code`):

| Código de Error (`code`) | Causa en el Monolito (`ocrmypdf`) | ¿Reintentable? | Acción Recomendada |
|---|---|---|---|
| `ENCRYPTED_PDF` | `EncryptedPdfError`, `DigitalSignatureError` | `false` | El documento tiene clave o firma protegida; requiere descifrado previo. |
| `CORRUPT_FILE` | `InputFileError`, PDF corrupto o truncated | `false` | Archivo dañado en origen; no reprocesar. |
| `UNSUPPORTED_FORMAT` | MIME type fuera de los soportados | `false` | Solo se admiten PDF, JPEG, PNG, TIFF, GIF, BMP, WEBP, HEIC. |
| `TIMEOUT` | El subproceso superó el límite de tiempo | `true` | Reintentar con mayor asignación de tiempo o menor resolución. |
| `ENGINE_ERROR` | `SubprocessOutputError` (Ghostscript / Tesseract) | Depende | Fallo interno en la rasterización o renderizado de fuentes. |
| `INTERNAL_ERROR` | Excepción no controlada en el worker | `true` | Fallo de infraestructura (reintentar con backoff). |

---

## 4. Ejemplos de Uso Práctico

### 4.1. Flujo completo con `curl`

#### Paso 1: Enviar el documento para OCR
```bash
curl -i -X POST http://localhost:5000/api/v1/ocr/jobs \
  -H "X-Correlation-Id: c3f1a2b0-7d8e-4f1a-9b1c-0123456789ab" \
  -H "Idempotency-Key: paperless-doc-4821-attempt-1" \
  -F "file=@/ruta/a/documento.pdf;type=application/pdf" \
  -F "clientReference=doc-4821" \
  -F "language=spa+eng" \
  -F "mode=skip" \
  -F "deskew=true" \
  -F "rotatePages=true"
```
**Respuesta esperada (`202 Accepted`):**
```http
HTTP/1.1 202 Accepted
Location: /api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001
X-Correlation-Id: c3f1a2b0-7d8e-4f1a-9b1c-0123456789ab
Content-Type: application/json

{
  "jobId": "b6f1c0de-0000-4000-8000-000000000001",
  "clientReference": "doc-4821",
  "status": "pending",
  "createdAt": "2026-10-12T14:30:00Z"
}
```

#### Paso 2: Consultar estado del trabajo (Polling)
```bash
curl -X GET http://localhost:5000/api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001 \
  -H "X-Correlation-Id: c3f1a2b0-7d8e-4f1a-9b1c-0123456789ab"
```
**Respuesta cuando finaliza (`200 OK`):**
```json
{
  "jobId": "b6f1c0de-0000-4000-8000-000000000001",
  "clientReference": "doc-4821",
  "status": "completed",
  "createdAt": "2026-10-12T14:30:00Z",
  "startedAt": "2026-10-12T14:30:01Z",
  "completedAt": "2026-10-12T14:30:06Z",
  "result": {
    "text": "UNIVERSIDAD TÉCNICA NACIONAL\nFacultad de Ingeniería...",
    "pageCount": 2,
    "durationMs": 4820,
    "engineVersion": "ocrmypdf 16.x / tesseract 5.x",
    "artifacts": {
      "pdfa": {
        "downloadUrl": "/api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001/artifacts/pdfa",
        "mimeType": "application/pdf",
        "sizeBytes": 204850
      },
      "thumbnail": {
        "downloadUrl": "/api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001/artifacts/thumbnail",
        "mimeType": "image/webp",
        "sizeBytes": 15420
      }
    }
  }
}
```

#### Paso 3: Descargar artefactos
```bash
# Descargar PDF/A procesado
curl -O -J http://localhost:5000/api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001/artifacts/pdfa

# Descargar Miniatura
curl -O -J http://localhost:5000/api/v1/ocr/jobs/b6f1c0de-0000-4000-8000-000000000001/artifacts/thumbnail
```

---

### 4.2. Integración en Python (Adaptador de Paperless-ngx)

Así es como el paquete `paperless_remote_ocr` utiliza el contrato:

```python
import time
import requests
from pathlib import Path

class RemoteOcrClient:
    def __init__(self, base_url: str = "http://ocr-api:8080"):
        self.base_url = base_url.rstrip("/")

    def process_document(
        self,
        file_path: Path,
        client_ref: str,
        language: str = "spa+eng",
        mode: str = "skip",
        timeout_seconds: int = 120,
    ) -> dict:
        # 1. Enviar el trabajo
        with open(file_path, "rb") as f:
            files = {"file": (file_path.name, f, "application/pdf")}
            data = {
                "clientReference": client_ref,
                "language": language,
                "mode": mode,
                "deskew": "true",
                "rotatePages": "true",
            }
            res = requests.post(f"{self.base_url}/api/v1/ocr/jobs", files=files, data=data)
            res.raise_for_status()
            job_info = res.json()
            job_id = job_info["jobId"]

        # 2. Polling con backoff lineal
        start_time = time.time()
        while time.time() - start_time < timeout_seconds:
            status_res = requests.get(f"{self.base_url}/api/v1/ocr/jobs/{job_id}")
            status_res.raise_for_status()
            job = status_res.json()

            if job["status"] == "completed":
                return job["result"]
            elif job["status"] == "failed":
                error_info = job.get("error", {})
                raise RuntimeError(f"OCR falló: {error_info.get('code')} - {error_info.get('message')}")

            time.sleep(2)

        raise TimeoutError(f"Tiempo de espera agotado ({timeout_seconds}s) para el trabajo {job_id}")
```

---

### 4.3. Consumo de Eventos en el Microservicio de Búsqueda (RabbitMQ)

Cuando el trabajo se completa, el Microservicio OCR publica el evento `ocr.completed`. El Microservicio de Búsqueda (C#) lo consume para indexar el texto extraído:

```json
{
  "schemaVersion": 1,
  "messageId": "b6f1c0de-0000-4000-8000-000000000003",
  "correlationId": "d3a2a3b4-0000-4000-8000-000000000002",
  "clientReference": "doc-4821",
  "text": "UNIVERSIDAD TÉCNICA NACIONAL\nFacultad de Ingeniería...",
  "pageCount": 2,
  "durationMs": 4820,
  "engineVersion": "ocrmypdf 16.x / tesseract 5.x",
  "completedAt": "2026-10-12T14:30:06Z"
}
```
