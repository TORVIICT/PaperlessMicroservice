# Contract Analysis — OCR ACL PoC

> Phase 1 output. Every statement about the contract cites a file that was read (M-02).
> Rule IDs referenced here: `CT-xx` clauses of this document, `A-xx` assumptions, `T-xx` from `constitution/tech-stack.md`, `M-xx`/`C-xx` from `constitution/mission.md`.

## Sources read

| # | Path | What it is | Contract content? |
|---|------|------------|-------------------|
| S-1 | `CONTRATO_OCR.md` (repository root, 94 lines) | OCR contract defined by the development team for this PoC | **Yes — this is the contract source** |
| S-2 | `README.md` (repository root, 93 lines) | GitLab project template boilerplate ("Getting started", "Editing this README") | No |
| S-3 | `AGENTS.md` (repository root, 37 lines) | Instructions for the agents working in this repository | No (project rules only) |
| S-4 | `opencode.json` (repository root, 4 lines) | OpenCode configuration, points to `AGENTS.md` | No |

Commands run from the repository root to enumerate the sources (2026-10-19):

```bash
find . -type f -not -path './.opencode/*' -not -path './PoC/*' -not -path './.git/*' | sort
# ./AGENTS.md
# ./CONTRATO_OCR.md
# ./opencode.json
# ./README.md
```

No PDF, `.http`, `.yaml` or `.yml` file exists in the repository. No file outside `CONTRATO_OCR.md` describes the OCR integration.

`CONTRATO_OCR.md` states about itself (lines 1-4): "Status: proposal defined by the development team for the ACL PoC. It is **not** discovered from the monolith or from Paperless-ngx documentation. The PoC must treat it as the contract the monolith expects, and must not add anything to it." See Finding F-1.

## Identified contract

Source of every item below: `CONTRATO_OCR.md`, cited per clause.

### Flow

- **Synchronous**: one request, one response. No job, no polling (S-1, "Flow", line 8).
  Consequence: there is no asynchronous flow in the contract, so the checklist item 16 and the `ASY` flow cases are not applicable (see the matrix); `ASY` still covers cancellation and concurrency (checklist item 15).

### Endpoint 1 — `POST /api/ocr/documents` (S-1, "Endpoints", line 12)

**Request headers**

| Header | Value | Required |
|--------|-------|----------|
| `Content-Type` | `application/json` | yes (line 14) |

**Request body** — JSON, camelCase (S-1, line 16):

| Field | Type | Required | Rules |
|-------|------|----------|-------|
| `documentId` | string | yes | 1 to 64 characters, not empty or whitespace (line 20) |
| `fileName` | string | yes | 1 to 255 characters, not empty or whitespace (line 21) |
| `mimeType` | string | yes | one of `application/pdf`, `image/png`, `image/jpeg` (line 22) |
| `contentBase64` | string | yes | valid Base64, not empty (line 23) |
| `language` | string | no | one of `eng`, `spa`; default `eng` (line 24) |

**Success response** — `200 OK`, `Content-Type: application/json` (S-1, line 26):

| Field | Type | Notes |
|-------|------|-------|
| `documentId` | string | same value as in the request (line 30) |
| `status` | string | always `completed` (line 31) |
| `text` | string | the recognized text (line 32) |
| `pageCount` | integer | 1 or more (line 33) |
| `language` | string | the language used (`eng` or `spa`) (line 34) |
| `confidence` | number | between 0.0 and 1.0 (line 35) |

**Sample request** (S-1, lines 39-47, literal):

```json
{
  "documentId": "doc-001",
  "fileName": "invoice.pdf",
  "mimeType": "application/pdf",
  "contentBase64": "SGVsbG8gd29ybGQ=",
  "language": "eng"
}
```

**Sample success response** (S-1, lines 51-60, literal). The file states: "the values of `text`, `pageCount` and `confidence` come from the simulation; only their names, types and ranges are part of the contract" (line 49).

```json
{
  "documentId": "doc-001",
  "status": "completed",
  "text": "string",
  "pageCount": 1,
  "language": "eng",
  "confidence": 0.95
}
```

### Endpoint 2 — `GET /api/ocr/health` (S-1, line 62)

- `200 OK`, `Content-Type: application/json`, body `{ "status": "ok" }` (S-1, lines 64).

### Errors (S-1, "Errors", lines 66-88)

Every error uses the same body with `Content-Type: application/json` (literal sample, lines 71-78):

```json
{
  "error": {
    "code": "invalid_request",
    "message": "Human readable text.",
    "details": [ { "field": "mimeType", "message": "Unsupported value." } ]
  }
}
```

| Case | Status | `error.code` | `details` |
|------|--------|--------------|-----------|
| Missing field, null, wrong type, out-of-range value, invalid Base64, malformed JSON, empty body | 400 | `invalid_request` | one entry per invalid field when the field is known; empty array otherwise (line 82) |
| Request `Content-Type` is not `application/json` | 415 | `unsupported_media_type` | empty array (line 83) |
| Unknown route | 404 | `not_found` | empty array (line 84) |
| Method not allowed on a known route | 405 | `method_not_allowed` | empty array (line 85) |
| Unexpected internal failure | 500 | `internal_error` | empty array; the message is fixed and generic (line 86) |

- "The error body never contains stack traces, exception names, namespaces or internal field names." (line 88).

### Explicitly not part of the contract (S-1, lines 90-94)

- Authentication (none).
- Real OCR quality, languages other than `eng` and `spa`, file size limits.
- Anything about how the text is produced.

## Contract clauses

| ID | Clause | Source |
|----|--------|--------|
| CT-01 | The flow is synchronous: one request, one response; there is no job submission and no polling endpoint. | `CONTRATO_OCR.md`, "Flow", line 8 |
| CT-02 | `POST /api/ocr/documents` is an exposed endpoint and requires the request header `Content-Type: application/json`. | `CONTRATO_OCR.md`, "Endpoints", lines 12-14 |
| CT-03 | `documentId`: JSON string, required, 1 to 64 characters, not empty and not whitespace only. | `CONTRATO_OCR.md`, line 20 |
| CT-04 | `fileName`: JSON string, required, 1 to 255 characters, not empty and not whitespace only. | `CONTRATO_OCR.md`, line 21 |
| CT-05 | `mimeType`: JSON string, required, only `application/pdf`, `image/png` or `image/jpeg`. | `CONTRATO_OCR.md`, line 22 |
| CT-06 | `contentBase64`: JSON string, required, valid Base64 and not empty. | `CONTRATO_OCR.md`, line 23 |
| CT-07 | `language`: JSON string, optional, only `eng` or `spa`; when absent the language used is `eng`. | `CONTRATO_OCR.md`, line 24 |
| CT-08 | A valid `POST /api/ocr/documents` request answers `200 OK` with `Content-Type: application/json`. | `CONTRATO_OCR.md`, line 26 |
| CT-09 | The success body contains exactly the fields `documentId` (echo of the request), `status` (always `completed`), `text` (string), `pageCount` (integer ≥ 1), `language` (`eng` or `spa`), `confidence` (number 0.0–1.0). | `CONTRATO_OCR.md`, lines 28-35 |
| CT-10 | The literal sample request and sample success response of the contract are accepted and produced (field names, casing and structure). | `CONTRATO_OCR.md`, lines 37-60 |
| CT-11 | `GET /api/ocr/health` answers `200 OK` with `Content-Type: application/json` and body `{ "status": "ok" }`. | `CONTRATO_OCR.md`, lines 62-64 |
| CT-12 | Every error response has `Content-Type: application/json` and the body shape `{ "error": { "code", "message", "details": [ { "field", "message" } ] } }`. | `CONTRATO_OCR.md`, lines 66-78 |
| CT-13 | Missing field, `null`, wrong type, out-of-range value, invalid Base64, malformed JSON and empty body → `400` with `error.code = "invalid_request"`; `details` has one entry per invalid field when the field is known, otherwise an empty array. | `CONTRATO_OCR.md`, line 82 |
| CT-14 | Request `Content-Type` other than `application/json` → `415` with `error.code = "unsupported_media_type"` and `details = []`. | `CONTRATO_OCR.md`, line 83 |
| CT-15 | Unknown route → `404` with `error.code = "not_found"` and `details = []`. | `CONTRATO_OCR.md`, line 84 |
| CT-16 | Method not allowed on a known route → `405` with `error.code = "method_not_allowed"` and `details = []`. | `CONTRATO_OCR.md`, line 85 |
| CT-17 | Unexpected internal failure → `500` with `error.code = "internal_error"`, `details = []` and a fixed, generic `message`. | `CONTRATO_OCR.md`, line 86 |
| CT-18 | No error body ever contains a stack trace, an exception name, a namespace or an internal field name. | `CONTRATO_OCR.md`, line 88 |
| CT-19 | There is no authentication of any kind. | `CONTRATO_OCR.md`, line 92 |
| CT-20 | The JSON field names are camelCase (`documentId`, `contentBase64`, `pageCount`, …). | `CONTRATO_OCR.md`, line 16 (request) and the samples of lines 39-60 and 71-78 |

## Assumptions

Each assumption is needed because the contract is silent on that point; the risk is stated (M-02, TS-03).

| ID | Assumption | Why it is needed | Risk if wrong |
|----|------------|------------------|---------------|
| A-01 | Response bodies are serialized as camelCase JSON with no BOM and `Content-Type: application/json; charset=utf-8` accepted as `application/json`. | The contract says "camelCase" only for the request body (line 16); the response samples are camelCase but the rule is not stated as a rule. **(inference)** from the samples. | The monolith would fail to bind the response; low risk because every sample is camelCase. |
| A-02 | The exact wording of `error.message` for each case is chosen by the PoC (English, generic, no internal detail); only its presence, type and the absence of internal detail are contract-guaranteed. | The contract fixes `code`, status and `details`, but the message text is only illustrated by "Human readable text." (line 75). | None for the shape; a real monolith that matched on the text would break. Tests assert shape and code, never the wording (TS-01 source `A-02`). |
| A-03 | The wording of `details[].message` is chosen by the PoC; the contract only guarantees one entry per invalid field with a `field` name and a message string. | The sample shows `"Unsupported value."` (line 77) but no list of texts. | Same as A-02. |
| A-04 | When the body is malformed JSON or empty, no field can be identified, so `details` is an empty array and `field` entries are not invented. | CT-13 says "empty array otherwise" without naming the cases. | Low; it follows the contract literally. |
| A-05 | Unknown route (`404`) is evaluated before method (`405`), and a known route with a wrong method answers `405` even if the body is invalid. | The contract defines both cases (lines 84-85) but not their precedence. | Low; matches the standard HTTP semantics of ASP.NET Core. |
| A-06 | A request whose `Content-Type` is absent is treated like a wrong `Content-Type` → `415`. | The contract only says "is not `application/json`" (line 83). | Low; a missing header is not `application/json`. |
| A-07 | The simulated values of `text`, `pageCount` and `confidence` are chosen by the PoC as long as they respect the contract types, ranges and determinism; the contract explicitly excludes "anything about how the text is produced" (line 94). | The contract does not define the simulation. | None for the contract; the exact values are a `D-xx` decision of Phase 2, not an assumption baked into a test expectation. |
| A-08 | Whitespace-only values for `documentId` and `fileName` are invalid ("not empty or whitespace", lines 20-21); a leading/trailing whitespace around an otherwise valid value is trimmed before the length rule is applied. | The contract does not say whether whitespace is trimmed. | Low; the length rule (1-64 / 1-255) is applied to the trimmed value. |
| A-09 | `GET /api/ocr/health` rejects other methods with `405` `method_not_allowed` (CT-16 applies to known routes) and has no request body rules. | The contract defines only `GET` for the health route (line 62). | Low. |
| A-10 | `contentBase64` is decoded by the ACL only to validate it and to feed the simulation; no size limit is enforced, because file size limits are explicitly out of contract (line 93). | The contract forbids a size limit but the service must not be exposed to unbounded memory in a PoC. | Memory use on a huge payload; acceptable in a local PoC. |

## Unknowns

| ID | Unknown | Status |
|----|---------|--------|
| U-01 | The real contract of Paperless-ngx's OCR (endpoints, payloads, statuses) — `CONTRATO_OCR.md` states it was **not** discovered from the monolith or from Paperless-ngx documentation. | `[UNKNOWN / NEEDS VERIFICATION]` — out of scope for this PoC (the file is the authoritative input). |
| U-02 | Whether the monolith sends additional headers (trace ids, correlation ids) or extra fields not listed in the contract. | `[UNKNOWN / NEEDS VERIFICATION]` — the PoC accepts the listed fields only; unknown fields are not specified by the contract (see F-4). |
| U-03 | Whether the OCR engine behind the ACL will keep the same contract. | `[UNKNOWN / NEEDS VERIFICATION]` — the engine does not exist yet (S-1, line 4). |
| U-04 | Request payload size limits accepted by the deployed monolith. | `[UNKNOWN / NEEDS VERIFICATION]` — explicitly not part of the contract (S-1, line 93). |

## Findings

- **F-1** The contract source is a team proposal, explicitly not reverse-engineered from Paperless-ngx (`CONTRATO_OCR.md`, lines 1-4). The PoC therefore validates *an* ACL against *a* stated contract, not against the monolith's real one. **(inference)** for the conclusion about the PoC's value.
- **F-2** The contract is synchronous (CT-01), so no in-memory job store, no state transitions and no `BackgroundService` are needed (T-23).
- **F-3** The contract already fixes the error handling completely (CT-12…CT-18), which removes the need for an assumption about the error body shape; the framework's default 400/404/405/415 bodies are **not** the contract and must be replaced (T-26).
- **F-4** The contract does not say what happens with unknown JSON properties in the request body (for example a sixth field). The behaviour is not decided here: it is registered as a decision `D-xx` in `PoC/docs/technical-decisions.md` in Phase 2, and the matrix cites that decision.

## Phase 1 exit check

- Every `CT-xx` cites `CONTRATO_OCR.md` with a line or section reference, and that file was read in full. ✔
- Every gap is an `A-xx` or an `[UNKNOWN / NEEDS VERIFICATION]`. ✔
- The contract is not contradictory and not missing in a way that changes what is built → the phase can close without stopping (C-10). ✔
