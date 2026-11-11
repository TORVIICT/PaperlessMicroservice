# Infrastructure

Configuración de despliegue e integración para el proyecto de modernización de
Paperless-ngx.

## Alcance actual

El stack local levanta Kong, PostgreSQL, la API del OCR, un worker externo y el ACL:

```text
Frontend -> Kong :8000 -> ocr-api:8080 -> PostgreSQL + volumen de archivos
                                          ^
                                          |
                                     ocr-worker -> ACL -> Paperless-ngx
```

Kong es la única entrada pública para el frontend. Las rutas internas del OCR y
los nombres de contenedores no se publican y el frontend no los conoce.

El webhook OCR → ACL permanece únicamente en la red Docker. Paperless-ngx se
conecta a la misma red, pero conserva su despliegue y ciclo de vida propios.

## Servicios

| Servicio | Imagen / build | Función |
| :--- | :--- | :--- |
| `kong` | `kong:3.9.4` | Gateway DB-less, CORS, correlación y límite de tamaño |
| `postgres` | `postgres:17-alpine` | Metadatos y estado de los documentos OCR |
| `ocr-api` | `../ocr` (`ocr-api`) | API HTTP; solo persiste el documento como `Pending` |
| `ocr-worker` | `../ocr` (`ocr-worker`) | Procesa los trabajos pendientes desde PostgreSQL |
| `acl-api` | `../acl` | Traduce y entrega los resultados terminados a Paperless |

### Procesamiento y recuperación

- La API arranca en `Worker:Mode=external`: crea el trabajo en `Pending` y no
  ejecuta OCR dentro del proceso HTTP.
- `ocr-worker` reclama trabajos con `UPDATE ... FOR UPDATE SKIP LOCKED`, por lo
  que varios workers pueden escalar sin procesar el mismo documento.
- Un trabajo que quede en `Processing` más de `Worker:StaleJobTimeoutMinutes`
  (30 por defecto) se devuelve a `Pending` y se vuelve a procesar.
- El worker publica un latido cada 10 segundos en `/tmp/ocr-worker-heartbeat`.
  Su `HEALTHCHECK` verifica que el archivo se haya actualizado en el último
  minuto, sin depender de un puerto HTTP.
- El esquema se aplica con migraciones EF al arrancar la API. No se usa
  `EnsureCreated` en despliegues reales.

Los datos se guardan en volúmenes nombrados:

- `paperless-ocr-postgres`: base de datos del OCR.
- `paperless-ocr-storage`: archivos originales y PDFs procesados.

## Requisitos

- Docker con Docker Compose v2 o superior.
- Una red Docker compartida llamada `paperless-modernization`.
- PowerShell 5.1 o superior y `curl.exe` para ejecutar las pruebas de humo.

Kong se fija en `kong:3.9.4`, la imagen OSS publicada al preparar esta
configuración. La etiqueta `kong:3.16` no existe en la imagen OSS; para usar Kong
Gateway Enterprise 3.16 hay que cambiar `KONG_IMAGE` a
`kong/kong-gateway:3.16` y proporcionar una licencia válida.

## Configuración

1. Copiar el archivo de variables:

   ```powershell
   Copy-Item .env.example .env
   ```

2. Revisar como mínimo `POSTGRES_PASSWORD`, `INTERNAL_TOKEN`,
   `FRONTEND_ORIGIN`, `PAPERLESS_BASE_URL` y `PAPERLESS_API_TOKEN`.

`kong/kong.yml` no se versiona: se genera a partir de
`kong/kong.yml.template` porque Kong DB-less no interpola variables de entorno
dentro del archivo declarativo. El renderizado lo hace `scripts/start.ps1` y
falla si queda algún marcador `{{...}}` sin resolver.

## Inicio rápido

```powershell
Copy-Item .env.example .env
# Completar POSTGRES_PASSWORD, INTERNAL_TOKEN y PAPERLESS_API_TOKEN.
.\scripts\start.ps1
```

El script renderiza `kong/kong.yml` desde `.env`, levanta el stack y espera a
que la Admin API de Kong responda.

Para validar solo el enrutamiento, sin construir el OCR real:

```powershell
.\scripts\start.ps1 -MockOcr
```

Comprobaciones manuales usando el puerto configurado en `.env`:

```powershell
curl.exe http://127.0.0.1:8001/status
curl.exe http://localhost:8080/api/ocr/v1/documents
docker compose config
```

> Si el puerto `8000` ya está ocupado, cambiá `KONG_PROXY_PORT` en `.env` y usá
> ese mismo puerto en `VITE_API_BASE_URL`, en las pruebas de humo y en los
> ejemplos de esta documentación.

En el entorno local actual, Paperless publica `8000`; por eso el archivo `.env`
usa `KONG_PROXY_PORT=8080` y
`PAPERLESS_BASE_URL=http://host.docker.internal:8000`. Si Paperless se levanta
mediante `paperless.yml` en el puerto `8002`, Kong puede conservar el valor
predeterminado `8000`. El frontend siempre debe usar el puerto elegido para
Kong.

## Prueba con un OCR simulado

El overlay `tests/docker-compose.mock.yml` reemplaza `ocr-api` por
`traefik/whoami` en el puerto `8080`. Solo valida el enrutamiento de Kong; no
valida el contrato del OCR.

```powershell
.\scripts\start.ps1 -MockOcr
powershell -ExecutionPolicy Bypass -File .\tests\gateway-smoke.ps1
.\scripts\stop.ps1
```

La prueba lee `KONG_PROXY_PORT` y `KONG_ADMIN_PORT` desde `.env`; también acepta
`-GatewayUrl` y `-AdminUrl` para apuntar a otro entorno.

## Configuración del frontend

Con Kong activo, el frontend debe usar:

```env
VITE_API_BASE_URL=http://localhost:8080/api/ocr/v1
VITE_ENABLE_MOCKS=false
```

El ejemplo corresponde a la distribución local actual. Si `KONG_PROXY_PORT`
cambia, debe actualizarse `VITE_API_BASE_URL`; no se requieren cambios en los
componentes React.

## Estructura

```text
infrastructure/
├── docker-compose.yml
├── kong/
│   ├── kong.yml.template   # versionado
│   └── kong.yml            # generado, ignorado por git
├── docs/
├── scripts/
└── tests/
```

- `docker-compose.yml`: Kong, PostgreSQL, API y worker del OCR.
- `kong/kong.yml.template`: servicios, rutas, CORS, correlación y límites.
- `docs/`: decisiones de enrutamiento, red, despliegue y seguridad.
- `scripts/`: inicio, detención y renderizado de la configuración.
- `tests/`: upstream simulado y pruebas de humo del gateway.

## Rutas publicadas

| Ruta pública | Destino | Estado |
| :--- | :--- | :--- |
| `/api/ocr/v1/*` | OCR | Activa |
| Webhook interno OCR → ACL | ACL | Activo solo dentro de Docker; no se publica en Kong |
| `/api/search/v1/*` | Search | Fase futura |

## Seguridad

- La Admin API de Kong se publica únicamente en `127.0.0.1`.
- No se versionan secretos ni tokens; `.env` está ignorado.
- Kong y el OCR aplican un límite de 55 MB en transporte. El límite de negocio
  de 50 MB se valida en el OCR y devuelve un error estructurado.
- CORS se limita al origen exacto configurado en `FRONTEND_ORIGIN`.
- `X-Correlation-ID` se genera o propaga en Kong y viaja hasta el OCR.
- La autenticación con Supabase corresponde a la fase Capstone.

## Próximas fases

1. Ejecutar la prueba completa con una instancia real de Paperless.
2. Incorporar autenticación, Search y observabilidad en Capstone.
