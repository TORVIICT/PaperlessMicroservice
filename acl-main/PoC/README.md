# OCR Anti-Corruption Layer (ACL) — PoC

A proof of concept of an Anti-Corruption Layer (ACL) for migrating the OCR capability out of the Paperless-ngx monolith. The ACL receives the monolith's requests, simulates OCR **deterministically** (no real OCR) and returns responses in the original contract format. It never leaks internal models or errors.

The contract these endpoints implement is `../CONTRATO_OCR.md`. The analysis, decisions and test evidence are in `docs/`.

## Prerequisites

- **.NET SDK `10.0.301`** (stable). The SDK is pinned by `global.json` (`"allowPrerelease": false`).
- No database, broker, container or network access is needed: everything runs locally and in-process.

## Layout

```
PoC/
├── OcrAcl.slnx               # solution (SDK 10 emits the XML .slnx format)
├── global.json               # pins the SDK
├── .gitignore                # bin/ and obj/ only
├── src/OcrAcl.Api/           # the only source project
│   ├── Program.cs            # composition root (exposes Program for tests)
│   ├── Api/                  # Contracts/, Controllers/, Translation/, Middleware/
│   ├── Application/          # Models/, Ports/, Services/
│   └── Infrastructure/       # SimulatedOcrEngine
├── tests/OcrAcl.Tests/       # Unit/, Integration/, Contract/, Validation/, Errors/,
│                             # Isolation/, Determinism/, Async/, Architecture/, Fixtures/
└── docs/                     # contract-analysis.md, technical-decisions.md, testing/
```

## Build

Run from `PoC/`:

```bash
dotnet build --nologo
```

Expected: `0 Warning(s)` and `0 Error(s)`.

## Run the tests

Run from `PoC/`:

```bash
dotnet test --nologo --logger "trx;LogFileName=run-01.trx" --results-directory docs/testing/results
```

Expected: `Passed!  - Failed: 0, Passed: 84, Skipped: 0, Total: 84`. The `.trx` evidence is written to `docs/testing/results/`.

Run a subset while working:

```bash
dotnet test --nologo --filter "Category=VAL"
```

## Run the service on localhost

Run from `PoC/` (the PoC listens on `http://127.0.0.1` only):

```bash
dotnet run --project src/OcrAcl.Api --urls http://127.0.0.1:5099
```

The command above (`dotnet run ...`) was executed on 2026-10-19 and the process was stopped afterwards.

### POST /api/ocr/documents

Request (`Content-Type: application/json`):

```json
{
  "documentId": "doc-001",
  "fileName": "invoice.pdf",
  "mimeType": "application/pdf",
  "contentBase64": "SGVsbG8gd29ybGQ=",
  "language": "eng"
}
```

Fields: `documentId` (1–64 chars, required), `fileName` (1–255 chars, required), `mimeType` (`application/pdf`, `image/png` or `image/jpeg`, required), `contentBase64` (valid Base64, required), `language` (`eng` or `spa`, optional, default `eng`).

Response `200 OK` (`Content-Type: application/json`). The real response observed during the localhost smoke test:

```json
{
  "documentId": "doc-001",
  "status": "completed",
  "text": "oscar foxtrot papa november alpha foxtrot charlie foxtrot",
  "pageCount": 1,
  "language": "eng",
  "confidence": 0.9
}
```

`text`, `pageCount` and `confidence` come from the simulation; only their names, types and ranges are part of the contract. The simulation is a pure function of the request content (SHA-256 based), so the same input always produces the same bytes.

### GET /api/ocr/health

Response `200 OK`:

```json
{ "status": "ok" }
```

### Errors

Every error uses the same body (`Content-Type: application/json`):

```json
{
  "error": {
    "code": "invalid_request",
    "message": "The request is invalid.",
    "details": [ { "field": "mimeType", "message": "The value is invalid." } ]
  }
}
```

Statuses: `400 invalid_request`, `415 unsupported_media_type`, `404 not_found`, `405 method_not_allowed`, `500 internal_error`. No error body contains a stack trace, exception name, namespace or internal field name, in any hosting environment.

## Documentation

- `docs/contract-analysis.md` — contract clauses `CT-01…CT-20`, assumptions `A-01…A-10`, unknowns (Phase 1).
- `docs/technical-decisions.md` — SDK, packages with resolved versions, decisions `D-01…D-39` (Phase 2/3).
- `docs/testing/test-cases.md` — the test-case matrix (78 cases) with sources and results.
- `docs/testing/test-execution-summary.md` — execution totals, defects, gaps (Phase 4).
- `PROGRESS.md` — the checkpoint log of the work (all six phases; Phase 5 = Done, wiki pages not published).

### Wiki-ready pages

The eight pages under `docs/wiki/` are written to be copied into the project wiki by a person. They are **not published**: the agent has no access to the project wiki.

- `docs/wiki/Home.md`
- `docs/wiki/Architecture-and-Workflow.md`
- `docs/wiki/Technical-Decisions.md`
- `docs/wiki/Architectural-Adaptations.md`
- `docs/wiki/PoC-Findings-and-Results.md`
- `docs/wiki/Limitations-and-Next-Steps.md`
- `docs/wiki/Risks-and-Mitigation.md`
- `docs/wiki/Testing-Documentation.md`

The optional `docs/wiki/_Sidebar.md` lists links to the eight pages.
