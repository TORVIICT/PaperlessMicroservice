# Architecture Documentation

> Feature: `002-ocr-acl-integration`
> This document describes the implemented architecture, components, flows, and boundaries.

## 1. Overview

The OCR Anti-Corruption Layer (ACL) is a .NET 10 ASP.NET Core Web API service that sits between the API Gateway and the OCR service. It receives document-processing requests from the API Gateway, translates them to the OCR service contract, manages the asynchronous OCR processing, retrieves the result, and delivers it to the monolith via the monolith's public REST API (T-C flow).

## 2. Layered Architecture

```
Api ───► Application ◄─── Infrastructure
(HTTP + external contracts)   (use cases, internal models, ports)   (HTTP clients, store adapters)
```

| Layer | Contains | Must NOT contain |
|-------|----------|------------------|
| `Api` | Controllers, external DTOs (FE contracts), translators, validation, error middleware | Business logic, OCR DTOs, concrete HTTP clients |
| `Application` | Use cases, internal models, ports (`IOcrClient`, `IMonolithClient`, `IDocumentStore`, `IClock`, `IIdGenerator`, `ICorrelationIdAccessor`) | `Microsoft.AspNetCore.*`, external DTOs, concrete adapters |
| `Infrastructure` | OCR HTTP client, polling policy, in-memory store adapter, correlation/clock adapters | Controllers, external DTOs |

`Program.cs` is the composition root (dependency injection). The only place allowed to reference `Infrastructure` from `Api` is `Program.cs`.

## 3. Component Inventory

| Component | Layer | Responsibility | Contract / Rule |
|-----------|-------|----------------|-----------------|
| `DocumentsController` | Api | `POST /api/v1/documents`, `GET /api/v1/documents/{id}`, `GET /api/v1/documents/{id}/text`, `GET /api/v1/documents/{id}/archive` | FE-E1, FE-E3, FE-E4, FE-E6 |
| `DocumentRequestTranslator` | Api | FE multipart → `SubmitDocument` command | T-06, `05 §2` |
| `DocumentResponseTranslator` | Api | `OcrJob`/`DocumentRecord` → FE document model | T-06, `05 §3` |
| `ErrorTranslator` | Api | `AclError` → FE error envelope | T-06, `06 §1–§2` |
| `ApiExceptionHandler` | Api | Catch unexpected exceptions, sanitize framework responses | T-26 |
| `SubmitDocumentHandler` | Application | Validate → generate `clientReference` → submit → enqueue for background polling | FR-01…FR-06 |
| `GetDocumentStatusHandler` | Application | Read state, map to internal document | FR-07, FR-08 |
| `GetDocumentTextHandler` | Application | Return `result.text` + metadata | FR-09 |
| `GetArchiveHandler` | Application | Request archive stream through OCR port | FR-10 |
| `DeliverResultToMonolithHandler` | Application | Orchestrate monolith delivery: upload → poll → patch content | FR-15, MON-API |
| `IOcrClient` | Application | Transport-agnostic OCR port | OCR-SRC §3–§4 |
| `HttpOcrClient` | Infrastructure | Submit, get job, download archive/thumbnail; headers; error parsing | OCR-SRC §2–§4, T-33 |
| `PollingPolicy` | Infrastructure | Back-off 500ms/1s/2s, timeout, transient retries | OCR-SRC §4 |
| `InMemoryDocumentStore` | Infrastructure | ACL-owned state: `documentId ↔ jobId ↔ clientReference` + metadata | D-34, OQ-04 |
| `IMonolithClient` | Application | Monolith REST API port | MON-API |
| `HttpMonolithClient` | Infrastructure | `post_document` → poll → `PATCH content` | MON-API |
| `InMemoryDocumentStore` | Infrastructure | In-memory `IDocumentStore` implementation | D-34 |
| `MonolithOutboundHeadersHandler` | Infrastructure | `Authorization: Token` + `X-Correlation-ID` on every call | MON-API §2, OCR-SRC §2.2 |

## 4. Data Flows

### 4.1 Submit (T-C flow via public REST API)

```
Frontend → Gateway → ACL DocumentsController
  → validate multipart (MIME, size, presence)
  → DocumentRequestTranslator builds SubmitDocument
  → generate clientReference (format: doc-ref-XXXXXX)
  → IOcrClient.SubmitAsync(file, clientReference, language, mode)
  → OCR 202 + Location + {jobId, status:processing, createdAt}
  → persist ACL state (documentId ↔ jobId ↔ clientReference, fileName, MIME, size)
  → respond 202 + FE document model (initial status)
  → enqueue job for background polling (OcrPollingBackgroundService)
```

### 4.2 Background Polling and Result

```
OcrPollingBackgroundService dequeues job
  → wait 500ms (PollingPolicy)
  → GET polling URL (from Location header)
  → 200 {status:processing} → wait 500/1000/2000ms by attempt
  → 200 {status:completed, result:{...}} → translate → update store
  → 200 {status:failed, error:{code,message}} → translate error → update store
  → 503/socket → up to 3 transient retries with jitter
  → global timeout ~120s → cancel → mark failed
  → optional: download archive/thumbnail when flags true
```

### 4.3 Monolith Delivery (T-C flow)

```
Completed OCR job → DeliverResultToMonolithHandler
  → Select upload file: PDF/A with text layer (if HasArchivedPdf) else original
  → TranslateToUploadRequest: file, title, created
  → UploadDocumentAsync → POST /api/documents/post_document/
  → PollTaskAsync → GET /api/tasks/?task_id=...
  → On success: PatchContentAsync → PATCH /api/documents/{id}/ with {content: OCR text}
  → Update ACL store with monolith documentId for idempotency
```

### 4.4 Read Paths

- `GET /documents/{id}` reads ACL store → FE model
- `GET /documents/{id}/text` returns `{id, text, pageCount}` (409 if not completed)
- `GET /documents/{id}/archive` streams OCR archive through ACL (no OCR host URL exposed)

## 5. Boundaries and Responsibilities

| Component | Responsibility | The ACL agent may |
|-----------|----------------|-------------------|
| Frontend | UI, polls Gateway every 3s | Nothing |
| API Gateway (Kong) | JWT auth, routing, rate limiting, TLS | Nothing (mock in tests) |
| **ACL** | Validate, translate between FE and OCR contracts, async orchestration, error mapping, traceability, idempotency | **Implement — only code written** |
| OCR service | Heavy OCR work, PDF/A, thumbnails, own DB | Call via contract only; never touch DB/files |
| Monolith | System of record; persists text in own DB | Nothing until ingestion contract exists (OQ-01) |
| Search | Called by Gateway; own DB | Out of scope (UC-04) |

## 5. Blocked / Deferred Parts

| Blocker | Tasks | Since |
|---------|-------|-------|
| OQ-01 (resolved D-44) | T-016, T-026, T-043, T-070, T-071, T-072, T-116 | Adaptation |
| OQ-03 | T-056 | Adaptation |
| OQ-07 | T-065, T-066, T-067, T-085, T-119 | Adaptation |
| OQ-08 | T-048, T-063, T-083 | Adaptation |
| OQ-10 | T-053, T-086 | Adaptation |
| OQ-11 | T-055 | Adaptation |
| OQ-11 (N/A D-44) | T-055 | 2026-11-02 |
| BLK-01 (resolved D-42) | T-033, T-082 | 2026-11-01 |
| BLK-02 (resolved D-43) | T-023, T-036 | 2026-11-01 |

## 6. Key Design Decisions

| ID | Decision | Rationale |
|----|----------|-----------|
| D-33 | HTTP polling over MassTransit | Simpler, no broker/volume needed; contract defines back-off |
| D-34 | In-memory document store | Simpler; state lost on restart (documented limitation) |
| D-35 | Accept only PDF/PNG/JPEG; reject 5 extra FE formats | Matches OCR contract; 50 MB limit |
| D-36 | FE error envelope; OCR-only codes → OCR_PROCESSING_FAILED | Simpler; original code in `details.ocrCode` |
| D-37 | `language=spa`, `mode=skip` (configurable) | Project documents are Spanish |
| D-38 | External frontend; monolith as storage only | Per user clarification |
| D-39 | T-C (public REST API) for monolith delivery | No monolith changes; uses existing `post_document` + `PATCH content` |
| D-40 | Background processing; 202 immediate; 120s timeout | Matches OCR contract; in-memory store lost on restart |
| D-41 | Gateway handles token; ACL forwards `Authorization` | Gateway validates user; ACL forwards |
| D-42 | FE envelope with HTTP status per code | D-42; inner `error` = `{code,message,details}` |
| D-43 | `GET /documents/{id}/text` = `{id, text, pageCount}` | 409 if not completed |
| D-44 | T-C (public REST API) for monolith delivery | No monolith changes; reuses existing `post_document` |
| D-45 | Upload OCR's PDF/A when available; else original | Avoids monolith re-OCR |
| D-46 | Skip Stage 6 & 7; proceed to Stage 8 | User decision |

---

*Document generated as part of Stage 8 (T-100). Last updated: 2026-11-02*