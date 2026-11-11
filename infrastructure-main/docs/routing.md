# Enrutamiento

## Ruta activa

| Ruta pública | Upstream | Reescritura |
| :--- | :--- | :--- |
| `/api/ocr/v1/*` | `http://ocr-api:8080/api/v1/*` | `strip_path: true` |

Ejemplos:

| Solicitud al gateway | Solicitud al OCR |
| :--- | :--- |
| `GET /api/ocr/v1/documents` | `GET /api/v1/documents` |
| `POST /api/ocr/v1/documents` | `POST /api/v1/documents` |
| `GET /api/ocr/v1/documents?status=queued` | `GET /api/v1/documents?status=queued` |
| `GET /api/ocr/v1/documents/{id}` | `GET /api/v1/documents/{id}` |
| `GET /api/ocr/v1/documents/{id}/text` | `GET /api/v1/documents/{id}/text` |
| `GET /api/ocr/v1/documents/{id}/original` | `GET /api/v1/documents/{id}/original` |
| `GET /api/ocr/v1/documents/{id}/archive` | `GET /api/v1/documents/{id}/archive` |
| `DELETE /api/ocr/v1/documents/{id}` | `DELETE /api/v1/documents/{id}` |

El contrato público del OCR es `/api/v1/documents`. La ruta heredada
`/api/v1/ocr/jobs` es interna y no se publica a través de Kong.

## Decisiones

- El frontend solo conoce `/api/ocr/v1`. No conoce `ocr-api`, el puerto `8080`
  ni la ruta interna `/api/v1`.
- Se usa `strip_path: true` porque la ruta pública y la interna tienen prefijos
  distintos.
- La ruta acepta únicamente los métodos que necesita el flujo actual:
  `GET`, `POST`, `DELETE` y `OPTIONS`.
- `retries: 0` evita reintentos automáticos en cargas y eliminaciones, que no
  son idempotentes.
- Los timeouts son `3s` para conexión, `10s` para lectura y `60s` para escritura.
  La escritura es más larga porque una carga de hasta 55 MB puede tardar en
  transmitirse.

## Plugins

La configuración declarativa se renderiza desde `kong/kong.yml.template`:

| Plugin | Alcance | Configuración |
| :--- | :--- | :--- |
| `correlation-id` | Global | Cabecera `X-Correlation-ID`, generada con `uuid#counter` y devuelta al cliente |
| `cors` | Ruta `ocr-api` | Origen exacto de `FRONTEND_ORIGIN`, métodos `GET/POST/DELETE/OPTIONS`, cabeceras `Content-Type` y `X-Correlation-ID` |
| `request-size-limiting` | Ruta `ocr-api` | 55 MB, sin exigir `Content-Length` para no romper `GET` ni `DELETE` |

Además, `KONG_NGINX_HTTP_CLIENT_MAX_BODY_SIZE=55m` alinea el límite de Nginx con
el plugin y con el límite de transporte del OCR. El límite de negocio de 50 MB
se valida en el OCR, que responde con `FILE_TOO_LARGE`.

## Tráfico interno

`POST /api/internal/v1/ocr/completed` pertenece al ACL y solo lo invoca el
worker OCR mediante `http://acl-api:8080`. No se publica en Kong porque no es
una operación del frontend. Tampoco se publican rutas de Paperless ni Search
hasta que exista un caso de uso público confirmado.
