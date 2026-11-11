# Seguridad

## Límites actuales

- Kong funciona en modo DB-less: su configuración se genera desde
  `kong/kong.yml.template` hacia `kong/kong.yml` (ignorado por git).
- La Admin API se publica solo en `127.0.0.1:8001`.
- No se almacenan secretos, tokens ni credenciales en el repositorio. `.env`
  está ignorado; `.env.example` solo contiene valores de ejemplo.
- El frontend no accede a servicios internos ni a la Admin API.
- El webhook OCR → ACL exige `X-Internal-Token`; el valor se inyecta mediante
  variables de entorno y no se publica en Kong.
- CORS se limita al origen exacto configurado en `FRONTEND_ORIGIN`.
- `X-Correlation-ID` se genera o propaga en Kong y se devuelve al cliente.
- La autenticación con Supabase pertenece a la fase Capstone.

## Tamaño y formato de archivos

El límite se aplica en dos capas con propósitos distintos:

| Capa | Límite | Resultado |
| :--- | :--- | :--- |
| Nginx de Kong | 55 MB | Corta la conexión antes de consumir más recursos |
| Plugin `request-size-limiting` | 55 MB | `413` en el gateway |
| Transporte del OCR (Kestrel/FormOptions) | 55 MB | Permite que el controller devuelva un error estructurado |
| Regla de negocio del OCR | 50 MB | `413 FILE_TOO_LARGE` |

El formato se valida en el OCR por extensión y `Content-Type`. Kong OSS no
puede inspeccionar el contenido del archivo, solo su tamaño; por eso la
validación de formato no se duplica en el gateway.

## Datos sensibles

- PostgreSQL guarda solo metadatos, estado y rutas de archivo.
- Los archivos viven en el volumen `paperless-ocr-storage`.
- No se registran tokens, texto extraído ni contenido de documentos en los logs.
- Los errores de upstream deben exponerse truncados y sin cuerpos internos.

## Pendientes antes de exponer el entorno

1. Definir autenticación y autorización antes de publicar documentos reales.
2. Validar en un entorno real que los errores del upstream no filtren detalles
   internos.
3. Revisar los límites y timeouts con mediciones de cargas reales.

La configuración actual es de desarrollo local. No debe usarse como despliegue
de producción sin completar los puntos anteriores.
