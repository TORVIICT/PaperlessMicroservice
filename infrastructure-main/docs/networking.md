# Red

## Red compartida

Kong y el OCR deben estar conectados a la misma red Docker:

```text
paperless-modernization
```

Este repositorio define la red con ese nombre. Otros repositorios pueden
usarla como red externa:

```yaml
networks:
  modernization:
    external: true
    name: paperless-modernization
```

La red permite que Paperless-ngx y el ACL se unan al stack sin publicar sus
puertos internos al host.

## Nombres internos

| Servicio | Nombre interno | Puerto |
| :--- | :--- | :--- |
| Kong | `kong` | `8000` HTTP |
| Kong Admin API | `kong` | `8001` |
| OCR API | `ocr-api` | `8080` |
| OCR Worker | `ocr-worker` | No expone HTTP |
| ACL | `acl-api` | `8080` |
| PostgreSQL | `postgres` | `5432` |
| Paperless (externo) | `paperless-webserver` | `8000` |

Los nombres `ocr-api`, `ocr-worker`, `acl-api` y `postgres` son contratos de red, no URLs
públicas. Si el servicio real usa otro nombre o puerto, debe cambiarse en
`docker-compose.yml` y `kong/kong.yml.template`, sin modificar el frontend.

## Volúmenes

| Volumen | Contenido |
| :--- | :--- |
| `paperless-ocr-postgres` | Base de datos del OCR |
| `paperless-ocr-storage` | Archivos originales y PDFs procesados |

## Puertos publicados en el host

| Puerto | Uso | Exposición |
| :--- | :--- | :--- |
| `8000` | Proxy HTTP de Kong | Pública para el frontend |
| `8001` | Admin API de Kong | Solo `127.0.0.1` |
| `5058` | Diagnóstico local del ACL | Solo `127.0.0.1` |

La Admin API y el ACL no deben exponerse fuera de la máquina de desarrollo.
PostgreSQL y los servicios del OCR no publican puertos al host.

## Verificación

```powershell
docker network inspect paperless-modernization
docker compose ps
```
