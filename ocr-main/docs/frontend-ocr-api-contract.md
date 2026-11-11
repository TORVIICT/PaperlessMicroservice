# Contrato API entre Frontend y OCR

## Objetivo

Definir el contrato REST inicial para que el frontend en React y el microservicio OCR puedan desarrollarse en paralelo.

Para el Midterm, el frontend consumirá directamente la API del OCR. En el Capstone, las mismas rutas se consumirán mediante Kong sin cambiar la lógica del frontend.

```text
Midterm:  Frontend React -> API OCR
Capstone: Frontend React -> Kong -> API OCR
```

## URL base

```text
/api/v1
```

La dirección del servidor se configurará mediante una variable de entorno en el frontend.

## Endpoints

| Método y ruta | Función | Respuesta esperada |
| :--- | :--- | :--- |
| `POST /documents` | Subir un documento | `202 Accepted` con el identificador y estado inicial |
| `GET /documents` | Listar documentos | Lista paginada de documentos |
| `GET /documents/{id}` | Consultar los datos y el estado de un documento | Detalle del documento |
| `GET /documents/{id}/text` | Obtener el texto extraído | Texto y metadatos asociados |
| `GET /documents/{id}/original` | Descargar el documento original | Archivo binario |
| `GET /documents/{id}/archive` | Descargar el PDF buscable, cuando exista | Archivo binario |
| `DELETE /documents/{id}` | Eliminar un documento y sus resultados | `204 No Content` |

## Carga de documentos

La carga se realizará mediante `multipart/form-data`. No se enviarán archivos codificados en Base64.

Campo requerido:

```text
file: archivo que será procesado
```

Ejemplo de respuesta de `POST /documents`:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "fileName": "factura.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 245800,
  "status": "queued",
  "createdAt": "2026-10-28T14:30:00Z",
  "completedAt": null,
  "pageCount": null,
  "error": null
}
```

## Modelo del documento

| Campo | Tipo | Descripción |
| :--- | :--- | :--- |
| `id` | UUID | Identificador generado por el OCR |
| `fileName` | string | Nombre original del archivo |
| `contentType` | string | Tipo MIME del archivo |
| `sizeBytes` | number | Tamaño del archivo en bytes |
| `status` | string | Estado actual del procesamiento |
| `createdAt` | datetime UTC | Fecha de recepción |
| `completedAt` | datetime UTC o `null` | Fecha de finalización |
| `pageCount` | number o `null` | Cantidad de páginas detectadas |
| `error` | object o `null` | Información del error, si existe |

La base de datos del OCR almacenará el estado, los metadatos y el texto extraído. Los archivos podrán almacenarse fuera de la base de datos y sus referencias se guardarán en ella.

## Estados del procesamiento

```text
queued -> processing -> completed
                   \-> failed
```

| Estado | Significado |
| :--- | :--- |
| `queued` | El documento fue recibido y está pendiente de procesamiento |
| `processing` | El worker está ejecutando el OCR |
| `completed` | El procesamiento terminó y el resultado está disponible |
| `failed` | Ocurrió un error definitivo |

## Formatos admitidos

El alcance esperado conserva los formatos soportados por el legado:

- PDF
- JPEG/JPG
- PNG
- TIFF
- GIF
- BMP
- WebP
- HEIC

La primera validación técnica se realizará con PDF, JPG y PNG. Los demás formatos permanecen dentro del alcance, pero deben comprobarse con el motor OCR.

Tamaño máximo inicial propuesto: **50 MB**, configurable en el backend y en el despliegue.

## Formato de errores

Todas las respuestas de error tendrán una estructura común:

```json
{
  "code": "UNSUPPORTED_FILE_TYPE",
  "message": "El formato del archivo no está soportado.",
  "documentId": null,
  "details": {}
}
```

Códigos iniciales:

- `INVALID_FILE`
- `INVALID_STATUS`
- `UNSUPPORTED_FILE_TYPE`
- `FILE_TOO_LARGE`
- `DOCUMENT_NOT_FOUND`
- `DOCUMENT_NOT_READY`
- `OCR_PROCESSING_FAILED`
- `SERVICE_UNAVAILABLE`

## Actualización del estado en el frontend

Para el Midterm, el frontend consultará `GET /documents/{id}` cada tres segundos mientras el estado sea `queued` o `processing`. Las consultas se detendrán cuando el documento pase a `completed` o `failed`.

SSE podrá incorporarse posteriormente como mejora sin eliminar el endpoint de consulta de estado.

## Estado de Implementación en el Microservicio OCR

> **Estado:** Implementado y Verificado (Noviembre 2026)  
> **Servicio:** Microservicio OCR (.NET 9 LTS — `DocumentsController.cs`)  
> **Compatibilidad CORS:** Habilitado para consumo directo desde SPA React con cabecera `Location` expuesta.

### Validación de Puntos Técnicos:

- [x] **Rutas y nombres de los campos:** Confirmados y 100% alineados con el modelo (`id`, `fileName`, `contentType`, `sizeBytes`, `status`, `createdAt`, `completedAt`, `pageCount`, `error`).
- [x] **Límite máximo:** 50 MB validado en el backend (retorna error `FILE_TOO_LARGE` y código HTTP 413).
- [x] **Formatos admitidos:** Validación de extensiones (`.pdf`, `.jpg`, `.jpeg`, `.png`, `.tiff`, `.tif`, `.gif`, `.bmp`, `.webp`, `.heic`, `.heif`) con error `UNSUPPORTED_FILE_TYPE` (HTTP 415).
- [x] **Generación de PDF buscable:** Disponible mediante `GET /api/v1/documents/{id}/archive` (PDF/A con OCR incrustado generado por OCRmyPDF).
- [x] **Eliminación:** `DELETE /api/v1/documents/{id}` elimina tanto el registro en base de datos como los archivos físicos (original, archivo PDF/A y miniatura) y retorna `204 No Content`.
- [x] **Paginación:** `GET /api/v1/documents?page={page}&pageSize={pageSize}&status={estado}` con estructura de respuesta `{ items: [...], total: N, page: N, pageSize: N }`. El filtro `status` acepta `queued`, `processing`, `completed` o `failed`; cualquier otro valor retorna `400` con código `INVALID_STATUS`.
- [x] **Texto extraído:** `GET /api/v1/documents/{id}/text` retorna `{ documentId, text, pageCount, durationMs, engineVersion }` (si no está listo, retorna `409 Conflict` con código `DOCUMENT_NOT_READY`).


