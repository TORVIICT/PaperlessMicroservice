# OCR Anti-Corruption Layer (ACL)

Anti-Corruption Layer used to integrate the independent OCR service with
Paperless-ngx. In the active flow, the frontend communicates directly with OCR
through Kong. After processing finishes, OCR notifies the ACL; the ACL retrieves
the result, translates it to the legacy contract, and delivers it through the
public Paperless API.

## Architecture

```
Frontend → Kong → OCR API → PostgreSQL / OCR storage
                    ↓
                 OCR Worker → ACL → Paperless-ngx
```

The active synchronization flow implements:

- **Inbound webhook:** `POST /api/internal/v1/ocr/completed` with `documentId`
  and `correlationId`.
- **Outbound to OCR:** retrieve document details, text, original, and searchable
  PDF through `/api/v1/documents/{id}`.
- **Outbound to Paperless:** upload the processed file, poll the ingestion task,
  and patch the extracted text.

The older frontend-to-ACL document routes remain available for legacy or
coupled scenarios, but they are not used by the main frontend flow.

## Tech Stack

- **.NET 10** / ASP.NET Core Web API
- **xUnit v2** + `Microsoft.AspNetCore.Mvc.Testing` for integration tests
- **Layered architecture** inside one source project: `Api` → `Application` ← `Infrastructure`

## Prerequisites

- .NET 10 SDK (latest stable)
- For the complete flow: OCR and Paperless available with a valid Paperless API
  token. The integrated Docker environment configures their internal addresses.

## Quick Start

```bash
# Build
dotnet build OcrAcl.slnx --nologo

# Run all tests (with evidence TRX)
dotnet test OcrAcl.slnx --nologo --logger "trx;LogFileName=run-XX.trx" --results-directory docs/testing/results

# Run unit tests only
dotnet test OcrAcl.slnx --nologo --filter "Category=UNIT"

# Run integration tests only (mock OCR)
dotnet test OcrAcl.slnx --nologo --filter "Category=INTEGRATION"

# Run architecture tests
dotnet test OcrAcl.slnx --nologo --filter "Category=ARC"

# Run E2E test against real Paperless-ngx (requires running instance)
export MONOLITH_BASE_URL=http://localhost:8000
export MONOLITH_API_TOKEN=<paperless-api-token>
dotnet test OcrAcl.slnx --nologo --filter "FullyQualifiedName~MonolithHopEndToEndTests"
```

## Configuration

The ACL is configured through environment variables. Docker Compose provides
the internal service addresses automatically:

| Setting | Description | Default |
|---------|-------------|---------|
| `ASPNETCORE_URLS` | ACL listening address | Platform default; `http://+:8080` in Docker |
| `OCR_SERVICE_URL` | OCR API base URL | `http://localhost:5000` outside Docker |
| `OCR_SERVICE_TOKEN` | Token sent to OCR when required | `test-token` for local development |
| `MONOLITH_BASE_URL` | Paperless API base URL | `http://localhost:8000` |
| `MONOLITH_API_TOKEN` | Paperless service-user token | Empty; required for real delivery |
| `INTERNAL_SERVICE_TOKEN` | Protects the OCR completion webhook | Empty locally; required in Docker |

## API Endpoints

### Active completion webhook

```http
POST /api/internal/v1/ocr/completed
X-Internal-Token: <shared-internal-token>
Content-Type: application/json

{
  "documentId": "550e8400-e29b-41d4-a716-446655440000",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000"
}
```

The ACL returns `200 OK` only after retrieving the OCR result, uploading the
document to Paperless, waiting for ingestion, and patching the extracted text.

### Legacy document routes

The routes below are retained for legacy or coupled scenarios. The React
frontend does not use them in the active integration; it calls OCR through Kong.

### Submit Document
```http
POST /api/v1/documents
Content-Type: multipart/form-data

file: <binary>
```

**Response (202 Accepted):**
```json
{
  "id": "uuid",
  "fileName": "document.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 102400,
  "status": "queued",
  "createdAt": "2026-11-01T10:00:00Z",
  "completedAt": null,
  "pageCount": null,
  "error": null
}
```

### Get Document Status
```http
GET /api/v1/documents/{id}
```

### Get Extracted Text
```http
GET /api/v1/documents/{id}/text
```

**Response (200 OK):**
```json
{
  "id": "uuid",
  "text": "Extracted text content...",
  "pageCount": 5
}
```

**Response (409 Conflict):** Document not completed.

### Download Archive (PDF/A)
```http
GET /api/v1/documents/{id}/archive
```

**Response (200 OK):** `application/pdf` stream  
**Response (404/409):** Not found or not ready.

## Accepted File Types

This validation applies only to the legacy frontend-to-ACL upload route. The
active webhook does not receive an uploaded file; it retrieves the already
processed result from OCR. The public OCR API owns the current eight-format
contract.

| MIME Type | Extension |
|-----------|-----------|
| `application/pdf` | `.pdf` |
| `image/png` | `.png` |
| `image/jpeg` | `.jpg`, `.jpeg` |

**Rejected** (D-35): TIFF, GIF, BMP, WebP, HEIC → `415 UNSUPPORTED_FILE_TYPE`

**Size limit**: 50 MB → `413 FILE_TOO_LARGE`

## Error Envelope

All errors follow the FE contract envelope:
```json
{
  "code": "ERROR_CODE",
  "message": "Human-readable message",
  "documentId": "uuid",
  "details": { "ocrCode": "ORIGINAL_OCR_CODE" }
}
```

| FE Code | HTTP Status | When |
|---------|-------------|------|
| `INVALID_FILE` | 400 | OCR returns 400 |
| `UNSUPPORTED_FILE_TYPE` | 415 | MIME not in allowed list |
| `FILE_TOO_LARGE` | 413 | File > 50 MB |
| `DOCUMENT_NOT_FOUND` | 404 | Unknown document ID |
| `DOCUMENT_NOT_READY` | 409 | Text/archive requested before completion |
| `OCR_PROCESSING_FAILED` | 422 | OCR job failed (original code in `details.ocrCode`) |
| `SERVICE_UNAVAILABLE` | 503 | OCR unreachable / max retries exceeded |

## Monolith Delivery (T-C Flow)

1. **Upload**: `POST /api/documents/post_document/` with `document` (PDF/A with text layer if available, else original), optional `title`, `created`
2. **Poll**: `GET /api/tasks/?task_id=<uuid>` until `status == "success"`
3. **Patch**: `PATCH /api/documents/{documentId}/` with `{"content": "<OCR text>"}`

Authentication: `Authorization: Token <service-user-token>` (configured via
`MONOLITH_API_TOKEN`).

## Project Structure

```
src/OcrAcl.Api/
├── Api/                    # Controllers, translators, validation, error handling
├── Application/            # Use cases, internal models, ports
│   ├── Models/             # OcrJob, OcrResult, OcrStatus, DocumentRecord, etc.
│   ├── Ports/              # IOcrClient, IMonolithClient, IDocumentStore, IClock, IIdGenerator, ICorrelationIdAccessor
│   └── Services/           # SubmitDocumentHandler, GetDocumentStatusHandler, GetDocumentTextHandler, GetArchiveHandler, DeliverResultToMonolithHandler
└── Infrastructure/         # HTTP clients, polling, in-memory store
    ├── Ocr/                # HttpOcrClient, PollingPolicy, OcrPollingBackgroundService
    ├── Monolith/           # HttpMonolithClient, MonolithOutboundHeadersHandler
    └── Store/              # InMemoryDocumentStore
tests/OcrAcl.Tests/
├── Unit/                   # Unit tests (92)
├── Integration/            # Integration tests with fake OCR (32)
├── Architecture/           # Architecture guard tests (5)
└── Fixtures/               # Test doubles
docs/
├── architecture.md         # T-100
├── contract-mapping.md     # T-101
├── decisions.md            # T-102
├── test-cases.md           # T-103
├── test-results.md         # T-104
├── limitations.md          # T-106
└── monolith-ingestion-contract.md  # MON-API contract
```

## Documentation

| Document | Task | Description |
|----------|------|-------------|
| `docs/architecture.md` | T-100 | Layers, components, flows, boundaries |
| `docs/contract-mapping.md` | T-101 | Endpoint, field, status, error mappings |
| `docs/decisions.md` | T-102 | All technical decisions (D-33 through D-46) |
| `docs/test-cases.md` | T-103 | All 133 test cases with traceability |
| `docs/test-results.md` | T-104 | Final run evidence (129 passed) |
| `docs/limitations.md` | T-106 | Mock vs real validation, all blockers |
| `docs/monolith-ingestion-contract.md` | — | MON-API contract (label MON-API) |

## Development

### Running the API independently

```bash
dotnet run --project src/OcrAcl.Api/OcrAcl.Api.csproj
```

The listening address depends on `ASPNETCORE_URLS`. In the integrated Docker
environment the ACL listens on `acl-api:8080` and is optionally published on
`127.0.0.1:5058` for diagnostics.

### Running the complete integration

From the sibling `infrastructure` repository:

```powershell
Set-Location ..\infrastructure
Copy-Item .env.example .env
# Complete POSTGRES_PASSWORD, INTERNAL_TOKEN and PAPERLESS_API_TOKEN.
.\scripts\start.ps1
```

The ACL is not a public frontend entry point in this flow. Its completion
webhook is reachable only from the Docker network and is protected with
`X-Internal-Token`.

### Adding Tests

- Unit tests: `tests/OcrAcl.Tests/Unit/`
- Integration tests: `tests/OcrAcl.Tests/Integration/`
- Architecture tests: `tests/OcrAcl.Tests/Architecture/`
- Use `[Trait("TestCaseId", "TC-XXX-###")]` and `[Trait("Category", "UNIT|INTEGRATION|ARC")]` attributes

## Known Limitations

See `docs/limitations.md` for complete list. Key points:

- The active completion handler is stateless and does not persist a delivery
  ledger; repeated notifications depend on controlled use by OCR and Paperless.
- OCR retries the webhook a limited number of times. Exhausted synchronization
  attempts do not yet have a persistent outbox or automatic replay.
- Authentication currently uses an internal shared token. Full user identity
  and authorization through Kong remain part of the final architecture.
- The legacy frontend-to-ACL polling routes still use in-memory state and should
  not be confused with the active webhook flow.

## License

Internal PoC — not for production use.
