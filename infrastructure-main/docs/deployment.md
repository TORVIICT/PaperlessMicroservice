# Despliegue local

## Preparación

```powershell
Copy-Item .env.example .env
docker compose config
```

`docker compose config` valida la sintaxis y la interpolación de variables sin
necesidad de iniciar contenedores. No valida `kong/kong.yml`: ese archivo se
genera antes de levantar el stack.

Si el puerto `8000` está ocupado, cambiá `KONG_PROXY_PORT` en `.env`. El mismo
puerto debe usarse en `VITE_API_BASE_URL` y en las pruebas de humo.

Cuando Paperless ya se ejecuta en el host sobre `8000`, la configuración local
validada es:

```env
KONG_PROXY_PORT=8080
PAPERLESS_BASE_URL=http://host.docker.internal:8000
```

## Inicio

```powershell
.\scripts\start.ps1
```

El script:

1. Crea `.env` desde `.env.example` si no existe.
2. Renderiza `kong/kong.yml` desde `kong/kong.yml.template` usando
   `FRONTEND_ORIGIN`. Falla si quedan marcadores sin resolver.
3. Levanta `kong`, `postgres`, `ocr-api`, `ocr-worker` y `acl-api`.
4. Espera hasta 180 segundos a que los servicios estén listos y luego valida
   la Admin API de Kong.

En el modo real se usa `--build` para evitar reutilizar imágenes locales
anteriores. El modo `-MockOcr` conserva la imagen simulada y no construye los
servicios internos.

El orden de arranque lo garantizan los health checks: `ocr-api` espera a
PostgreSQL, el ACL espera a la API y `ocr-worker` espera a ambos. La API corre en modo
`Worker:Mode=external`, por lo que solo persiste el documento como `Pending` y
el worker externo lo procesa.

Al arrancar, la API aplica las migraciones EF pendientes sobre PostgreSQL. El
worker también verifica el esquema, pero la API es la que migra primero porque
el worker depende de su health check. Un despliegue nunca crea el esquema con
`EnsureCreated`.

Sin el OCR real, se puede usar el upstream simulado:

```powershell
.\scripts\start.ps1 -MockOcr
```

## Verificación

Estado de Kong:

```powershell
curl.exe http://127.0.0.1:8001/status
```

Ruta pública:

```powershell
curl.exe http://localhost:8000/api/ocr/v1/documents
```

Estado de los servicios y volúmenes:

```powershell
docker compose ps
docker volume ls | Select-String paperless-ocr
```

El estado `healthy` de `ocr-worker` refleja su latido
(`/tmp/ocr-worker-heartbeat`), que se actualiza cada 10 segundos incluso durante
un OCR largo. Si el worker deja de escribir el archivo, el health check falla y
Compose lo reinicia.

Trabajos atascados:

- Un trabajo en `Processing` más antiguo que `Worker:StaleJobTimeoutMinutes`
  vuelve a `Pending` automáticamente.
- Al reiniciar el worker se ejecuta una recuperación inmediata de trabajos
  atascados y queda registrada en los logs como `Recovered N OCR jobs`.

Prueba automatizada con el upstream simulado:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\gateway-smoke.ps1
```

## Logs

```powershell
docker compose logs -f kong
docker compose logs -f ocr-api
docker compose logs -f ocr-worker
docker compose logs -f acl-api
```

## Detención

```powershell
.\scripts\stop.ps1
```

Para eliminar también los datos locales:

```powershell
docker compose down --remove-orphans --volumes
```

## Frontend

```env
VITE_API_BASE_URL=http://localhost:8000/api/ocr/v1
VITE_ENABLE_MOCKS=false
```

El valor debe cambiarse en el archivo de entorno del frontend, no en los
componentes React.
