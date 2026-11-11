# OCR Frontend

Aplicación web independiente para cargar documentos, consultar su procesamiento OCR y acceder a sus resultados.

## Tecnologías

- React
- TypeScript
- Vite
- React Router
- TanStack Query

## Requisitos

- Node.js 20 o superior
- npm 10 o superior

## Configuración

1. Copiar `.env.example` como `.env.local`.
2. Configurar `VITE_API_BASE_URL` con el puerto publicado por Kong. En el
   entorno integrado actual, Paperless ocupa `8000` y Kong utiliza `8080`:

   ```env
   VITE_API_BASE_URL=http://localhost:8080/api/ocr/v1
   VITE_ENABLE_MOCKS=false
   ```

El frontend no conoce ni llama directamente al OCR: todas las peticiones reales
pasan por Kong. Si se cambia `KONG_PROXY_PORT` en infraestructura, esta URL debe
usar el mismo puerto.

`VITE_ENABLE_MOCKS` controla la API simulada de MSW. En desarrollo se activa
por defecto y puede deshabilitarse con `VITE_ENABLE_MOCKS=false` para conectar
el backend real. En producción permanece desactivada si no se configura.

### API simulada

Con MSW activo, los documentos cargados recorren `queued`, `processing` y
terminan en `completed`. Si el nombre del archivo contiene `fail`, el
procesamiento termina en `failed` para poder probar el flujo de error.

## Comandos

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
npm run lint
npm test
npm run build
```

La aplicación queda disponible en `http://localhost:5173`.

## Pruebas

Las pruebas viven en `test/` y se dividen en unitarias, hooks y contrato de la
API. Usan Vitest, Testing Library y MSW en Node. `npm run test:watch` ejecuta
Vitest en modo interactivo.

## Organización

```text
src/
├── app/          # Composición global, rutas y proveedores
├── features/     # Funcionalidades organizadas por dominio
├── mocks/        # Simulación local del contrato HTTP con MSW
└── shared/       # Código reutilizable sin reglas de un dominio específico
```

Cada funcionalidad mantiene separados sus componentes, páginas, servicios y modelos para evitar mezclar presentación, acceso a datos y reglas de negocio.
