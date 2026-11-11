# Limitations and Known Gaps

> Feature: `002-ocr-acl-integration`
> Documents what is verified only with mocks vs. what needs real-service validation, and all open blockers.

## 1. Mock-Verified Only (Not Validated Against Real Services)

| Area | Verified With Mocks | Real Validation Needed |
|------|---------------------|------------------------|
| **OCR contract compliance** | `HttpOcrClientTests` (20 tests) — submit, get job, download archive/thumbnail, error mapping, headers, cancellation | Real OCR service at `Ocr:BaseUrl` |
| **OCR polling & back-off** | `OrchestrationIntegrationTests` (5 tests) — full submit→poll→complete/failed, retries, timeout | Real OCR service with actual latency |
| **OCR error codes** | `SubmitDocumentHandlerTests`, `HttpOcrClientTests` — `INVALID_FILE`, `UNSUPPORTED_FILE_TYPE`, 4 OCR-only codes mapped to `OCR_PROCESSING_FAILED` | Real OCR error responses |
| **Gateway auth forwarding** | `HttpOcrClientTests.SubmitAsync_IncludesAuthorizationHeader` — `Authorization: Bearer` forwarded | Real Kong Gateway with JWT validation |
| **Correlation-ID propagation** | `HttpOcrClientTests.SubmitAsync_IncludesCorrelationIdHeader` — `X-Correlation-ID` propagated | Real Gateway correlation-ID behavior |
| **Monolith T-C flow** | `MonolithTranslatorTests` (9 tests) — upload request, content patch, file selection logic | Real Paperless-ngx (partially done in T-116) |

## 2. Partially Validated Against Real Services

| Area | Validation Status |
|------|-------------------|
| **Monolith T-C flow (T-116)** | **Verified once** against Paperless-ngx v3.3.4 at `http://localhost:8010` with token `0558926a83002fd97187e6cda172c8e8a5dfddb3`. Test uploads file, polls task, patches content, verifies content persisted. **Not part of CI** — requires live instance and env vars `MONOLITH_BASE_URL`, `MONOLITH_API_TOKEN`. |
| **PDF/A with text layer upload (D-45)** | Tested manually: ocrmypdf-generated PDF/A uploaded, monolith skipped its own OCR, content held layer's text. **Not automated**. |
| **Large file handling** | 50 MB limit enforced in ACL (D-35). **Not tested** with real OCR service. |
| **Monolith archive generation** | Manual test: image-only PDF → monolith generated archive; PDF/A with text layer → no archive generated. **Not automated**. |

## 3. All Open Blockers (From tasks.md)

| Blocker | Tasks Affected | Status | Notes |
|---------|----------------|--------|-------|
| **OQ-03** | T-056 | **Open** | Kong prefix stripping; FE-E2 (list), FE-E5 (original), FE-E7 (delete) scope unknown |
| **OQ-07** | T-065, T-066, T-067, T-085, T-119 | **Open** | Idempotency policy: key, duplicates, retry after `failed` |
| **OQ-08** | T-048, T-063, T-083 | **Open** | Timeout outcome (what happens after 120s); restart behavior (in-memory store lost) |
| **OQ-10** | T-053, T-086 | **Open** | Correlation-ID policy: propagate inbound or generate; OCR credential if separate from Gateway token |

## 4. Deferred Stages (Per D-46)

| Stage | Tasks | Status | Notes |
|-------|-------|--------|-------|
| **Stage 6** (Errors, Observability, Security) | T-080–T-089 | **Deferred by user** | No structured logging, no metrics, no security hardening, no error handling completeness |
| **Stage 7** (Architecture & Cross-Cutting Tests) | T-090–T-097 | **Deferred by user** | Only Application layer guarded (TC-ARC-010–014); no Api/Infrastructure layer guards, no full cross-cutting verification |

## 5. Partially Covered Areas

| Area | What's Covered | What's Missing |
|------|----------------|----------------|
| **Architecture tests (T-028)** | Application layer only: no forbidden refs, no forbidden usings, namespace matches folder, at least one type/file | Api layer guards, Infrastructure layer guards, full dependency direction verification (AC-11) |
| **Error handling** | FE envelope with HTTP status per code (D-42), OCR→FE mapping (D-36), 409 for not-ready, 404 for not found | No global exception handler testing, no sanitization verification, no logging of errors without PII |
| **Idempotency** | None (OQ-07 open) | No duplicate detection, no idempotency key in submit, no retry-after-failed policy |
| **Timeout/restart behavior** | Global timeout token created (TC-UNIT-POLL-009/010), background service cancels after 120s (TC-T064-005) | No test for: job still running at timeout → what state; ACL restart → in-flight jobs lost (OQ-08) |
| **Correlation-ID** | Header forwarded if present (TC-UNIT-OCR-004) | No policy for: generate if missing; propagate to monolith; log correlation |
| **Security** | No secrets in repo (C-03), token from config/env | No TLS config, no rate limiting, no input sanitization beyond MIME/size, no auth on ACL endpoints (assumes Gateway) |

## 6. Contract Assumptions Not Verified

| Assumption | Source | Risk |
|------------|--------|------|
| OCR service has `spa` (Spanish) Tesseract data installed | D-37 | OCR may fail or return empty text for Spanish documents |
| OCR contract `01` is stable and matches `OCR-SRC` | All OCR tests | If OCR changes contract, ACL breaks |
| Monolith `post_document` accepts `document` as multipart file | MON-API §4 | If monolith changes, T-C flow breaks |
| Monolith task polling returns `result_data.document_id` on success | MON-API §5 | If monolith changes task result shape, polling breaks |
| Monolith `PATCH /api/documents/{id}/` accepts `content` field | MON-API §5 R3 | If monolith changes, content patch fails |

## 7. Test Coverage Gaps

| Gap | Reason |
|-----|--------|
| No load/concurrency tests | NFR-02/NFR-03 not measured (Stage 7 deferred) |
| No chaos/fault injection tests | Stage 6 deferred |
| No security penetration tests | Stage 6 deferred |
| No real OCR service integration tests | No OCR service available in test environment |
| No real Kong Gateway integration tests | No Gateway available in test environment |
| E2E test not in CI | Requires live Paperless-ngx instance |

## 8. Honesty Summary

| Claim | Truth |
|-------|-------|
| "All tests pass" | ✅ True — 129/129 in run-39.trx |
| "Build has 0 warnings/errors" | ✅ True |
| "OCR contract fully implemented" | ✅ True — per `OCR-SRC` and mock tests |
| "Monolith delivery works" | ⚠️ **Partially** — works against one Paperless v3.3.4 instance; not tested against other versions or configurations |
| "Idempotent" | ❌ False — OQ-07 open, no implementation |
| "Observable" | ❌ False — Stage 6 deferred, no metrics/logging |
| "Secure" | ❌ False — Stage 6 deferred, no security hardening |
| "Architecture fully verified" | ❌ False — Only Application layer guarded (Stage 7 deferred) |
| "Production ready" | ❌ False — PoC only; in-memory store, no persistence, open blockers |

## 9. Recommendations for Next Phase

1. **Resolve OQ-03, OQ-07, OQ-08, OQ-10** with stakeholders
2. **Implement Stage 6**: Structured logging, metrics, health checks, error handling hardening
3. **Implement Stage 7**: Full architecture guards, cross-cutting tests, load tests
4. **Replace in-memory store** with persistent storage (Redis/PostgreSQL) for production
5. **Add idempotency key** to submit endpoint (client-generated or ACL-generated)
6. **Validate against real OCR service** when available
7. **Validate against real Kong Gateway** when available
8. **Automate E2E test** in CI with testcontainers or dedicated test environment

---

*Document generated as part of Stage 8 (T-106). Last updated: 2026-11-02*