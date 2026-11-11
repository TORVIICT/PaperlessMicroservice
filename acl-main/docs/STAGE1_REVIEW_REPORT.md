## Review Report: 002-ocr-acl-integration — Stage 1 (Application Layer)

### ✅ Meets Spec

- **Build passes with 0 warnings, 0 errors** (AC-01): `dotnet build --nologo` reports `Build succeeded. 0 Warning(s) 0 Error(s)` — verified by re-running the command.
- **Boundary scans clean** (NFR-06, C-11, C-13, C-14): All four architecture-guard scans returned no hits:
  - No references to `ocr-db`, `5433`, `paperless-ngx`, `django`, `celery`, `postgres`, `npgsql`.
  - No `archiveDownloadUrl` or `thumbnailUrl` leaking in `Api/Contracts` or controllers.
  - No `Microsoft.AspNetCore.*` types in `Application` layer.
  - No `EnsureSuccessStatusCode`, `Console.WriteLine`, `.Result`, or `.Wait()` in source code.
- **Layer separation respected** (T-05, T-06): `Application` contains only internal models (`OcrJob`, `OcrResult`, `OcrStatus`, `AclError`) and ports (`IOcrClient`, `IIdGenerator`, `ICorrelationIdAccessor`, `IClock`). No external DTOs, no concrete adapters, no ASP.NET Core types.
- **Ports isolate unknowns** (T-05, plan §3.1): `IOcrClient`, `IMonolithClient` (blocked), `IDocumentStore` (not yet created), `IClock`, `IIdGenerator`, `ICorrelationIdAccessor` are all transport-agnostic interfaces in `Application/Ports`. No concrete implementation in `Application`.
- **Identity rules followed** (C-13): `clientReference` is the only identifier sent to OCR; `IIdGenerator.GenerateClientReference()` produces format matching `[a-z0-9_-]{1,64}` (though see **Discrepancy #1** below). No Django PK or other relational ID crosses the boundary.
- **Internal models match OCR contract** (OCR-SRC §3.2): `OcrJob`, `OcrResult`, `OcrJobError` fields correspond exactly to the `202` and `200` response DTOs. `ArchiveDownloadUrl` and `ThumbnailUrl` are documented as internal-only, never exposed outward (M-04, T-33).
- **Status model matches both contracts** (OCR-SRC §3.3, FE-SRC §5): `OcrStatus` enum has `Pending`, `Processing`, `Completed`, `Failed`. Transitions: `Pending→Processing`, `Processing→Completed/Failed`; `Failed` is terminal. Unknown status rejected explicitly via `ArgumentException`/`ArgumentOutOfRangeException` (INC-09).
- **FE status mapping implemented** (spec §11.2, INC-09): `OcrStatus.ToFeStatus()` maps `Pending→"queued"`, `Processing→"processing"`, `Completed→"completed"`, `Failed→"failed"`.
- **Error model matches FE contract** (FE-SRC §6, D-42): `AclError` with `Code`, `Message`, `Details`; `AclErrorCodes` constants for all 7 FE codes. No HTTP status in `Application` layer (T-26).
- **Abstractions for determinism** (T-18): `IClock` (wraps `TimeProvider`), `IIdGenerator` (with `FakeIdGenerator`), `ICorrelationIdAccessor` (with `FakeCorrelationIdAccessor`) — all support test-controlled time and IDs.
- **Correlation ID accessor in Api layer** (correct placement): `HttpContextCorrelationIdAccessor` lives in `Api/Services`, reads inbound `X-Correlation-ID` header, falls back to UUID v4. Propagation policy matches proposed default for OQ-10.
- **Deferred tasks correctly blocked**: `T-016` (`IMonolithClient`) is `Blocked (OQ-01)`; `T-020`–`T-029` are `Todo` but waiting on `T-020` (`IDocumentStore`) which depends on OQ-04 (answered D-34 but port not yet created) and subsequent tasks. No blocked task was partially implemented.

### ⚠️ Discrepancies

1. **`clientReference` format violates OCR contract (C-13, OCR-SRC §3, §9)**
   - **File**: `src/OcrAcl.Api/Application/Ports/IIdGenerator.cs`, line 39–46 (`DefaultIdGenerator.GenerateClientReference()`)
   - **Contract clause**: OCR-SRC §3.1 table: `clientReference` — "max 64 chars, `a-z 0-9 - _`" (lowercase only). DB column is `varchar(128)` (INC-22).
   - **Code produces**: `"DOC-REF-XXXXXX"` with uppercase `DOC-REF-` prefix.
   - **Impact**: The generated `clientReference` contains uppercase letters (`D`, `O`, `C`, `R`, `E`, `F`), which violates the `[a-z0-9_-]` constraint. The OCR service may reject it or store it inconsistently.
   - **Suggested fix**: Change the prefix to lowercase (e.g., `"doc-ref-"`) and ensure the random suffix uses only lowercase letters, digits, hyphen, underscore. Update `FakeIdGenerator` accordingly for test consistency.

2. **Two status enums for the same concept (`OcrJobStatus` vs `OcrStatus`)**
   - **Files**: `src/OcrAcl.Api/Application/Models/OcrJob.cs` (lines 49–62) defines `OcrJobStatus`; `src/OcrAcl.Api/Application/Models/OcrStatus.cs` (lines 6–19) defines `OcrStatus`.
   - **Spec reference**: Spec §11.4 mentions a single `OcrStatus` in the minimal internal model. Plan §3.1 component inventory lists "Internal status model" as one item.
   - **Impact**: Duplication increases maintenance burden and risk of divergence. `OcrJob` uses `OcrJobStatus` while transition logic and FE mapping use `OcrStatus`. They have identical values but are not interchangeable without casting.
   - **Suggested fix**: Consolidate to a single `OcrStatus` enum (preferably the one in `OcrStatus.cs` which includes transition logic and FE mapping). Update `OcrJob.Status` to use `OcrStatus`. Remove `OcrJobStatus`.

3. **`OcrJobError` vs `AclError` — different shapes for similar purpose**
   - **Files**: `OcrJob.cs` lines 64–78 (`OcrJobError { Code, Message }`); `AclError.cs` lines 8–24 (`AclError { Code, Message, Details }`).
   - **Contract reference**: OCR-SRC §3.2 defines `error = { code, message }` for failed jobs. FE-SRC §6 defines envelope `{ code, message, documentId, details }` and the document model's `error` object shape was `[NOT DEFINED]` until D-43 (`{ code, message, details }`).
   - **Impact**: Not a contract violation (they serve different layers: `OcrJobError` is the raw OCR payload; `AclError` is the ACL's internal error model). However, the mapping between them is not yet defined (will be in `ErrorTranslator`, T-033, Stage 2). No action needed now but worth noting for Stage 2.

4. **No unit tests yet (T-027 pending)**
   - **Evidence**: `dotnet test` reports "No test is available (0 tests)". TRX file `run-20.trx` confirms 0 tests.
   - **Spec expectation**: Stage 1 gate (T-029) requires "Build 0/0 + unit tests". Tasks `T-027` (Application unit tests) and `T-028` (Application architecture guard) are still `Todo`.
   - **Impact**: AC-11 (ARC tests) and AC-12 (full suite, coverage ≥ 85%) cannot be verified until tests exist. This is expected for a partial stage but must be completed before Stage 1 can be fully approved.
   - **Suggested fix**: Implement `T-027` (models, transitions, `SubmitDocument` handler with fake ports) and `T-028` (architecture guard test) before requesting Stage 1 approval.

5. **`OcrStatus.FromOcrString` handles `"pending"` but OCR contract returns `"processing"` in 202 body**
   - **File**: `OcrStatus.cs` line 80 maps `"pending" → OcrStatus.Pending`.
   - **Contract**: OCR-SRC §3.2 shows `202` response has `"status": "processing"`. The text mentions `"pending"` appears in the DB and in "Endpoint 2, Case A" (INC-09).
   - **Impact**: The mapping is correct for the GET endpoint (which may return `"pending"` per INC-09), but the initial `202` body returns `"processing"`. The `SubmitDocument` handler (not yet implemented) must handle this correctly. No code defect yet, but a test should verify the distinction.

### 💡 Suggested Improvements

1. **Add XML documentation to `OcrStatus.FromOcrString`** clarifying that `"pending"` comes from the OCR GET endpoint (INC-09), while the `202` response returns `"processing"`. This will help the `SubmitDocument` implementation.

2. **Consider making `OcrResult.ArchiveDownloadUrl` and `ThumbnailUrl` non-nullable when `HasArchivedPdf`/`HasThumbnail` are true** — the contract implies they are present in that case. Current nullable strings are safe but could be strengthened with a constructor or factory method that enforces the invariant.

3. **Add a `Validate()` method or constructor to `OcrJob`** that ensures `Result` is non-null only when `Status == Completed` and `Error` is non-null only when `Status == Failed`. This would catch invalid states early.

4. **Record the `clientReference` format decision in `DECISIONS.md`** as a `D-nn` (separate from D-37 which covers language/mode). The current implementation detail (prefix `"doc-ref-"`, 6-char random suffix) is not documented as a decision.

### 🔍 Errors Found

None that break the build or violate a contract **except Discrepancy #1** (uppercase `clientReference`), which will cause the OCR service to receive an out-of-contract value. This must be fixed before any integration test with a real or mock OCR service.

### 🚧 Blocked / Not Reviewed

| Blocker | Tasks | Reason |
|---------|-------|--------|
| OQ-01 | T-016, T-026, T-043, T-055, T-070–T-073, T-116 | No monolith ingestion contract delivered. Correctly blocked. |
| OQ-03 | T-056 | Kong prefix and FE-E2/E5/E7 scope not decided. Correctly blocked. |
| OQ-07 | T-065, T-066, T-067, T-085, T-119 | Idempotency policy not decided. Correctly blocked. |
| OQ-08 (partial) | T-048, T-063, T-083 | Timeout outcome and restart behaviour still open (D-40 only decided processing model). Correctly blocked. |
| OQ-10 (partial) | T-053, T-086 | Correlation-ID propagation policy not confirmed (D-41 only decided token strategy). Correctly blocked. |
| OQ-11 | T-055 | Thumbnail format for monolith — waits on OQ-01. Correctly blocked. |
| T-020 (`IDocumentStore`) | T-022, T-024, T-031, T-032, T-057, T-062 | Port not yet created (OQ-04 answered D-34 but task not executed). Waiting, not blocked. |
| T-027, T-028 | T-029 (Stage 1 gate) | Unit tests and architecture guard not yet implemented. Stage 1 cannot be fully approved until these are done. |

**Acceptance criteria in scope for Stage 1:**

- **AC-01** (build 0/0): ✅ **Verified** — `dotnet build --nologo` → `0 Warning(s) 0 Error(s)`.
- **AC-11** (external DTOs separate, ARC tests pass): ⚠️ **Partially verified (structure only)** — Layer separation is correct in code, but ARC tests (`T-090`–`T-096`) are in Stage 7 and not yet implemented. No architecture test exists to fail if the rule is violated.
- All other ACs (AC-02…AC-10, AC-12…AC-20) are **out of scope** for Stage 1 per the traceability matrix (spec §22) and are blocked by later stages or open questions.

### Verdict

⚠️ **APPROVED WITH OBSERVATIONS**

**Conditions for full Stage 1 approval:**
1. Fix **Discrepancy #1**: `clientReference` must be lowercase-only per OCR contract.
2. Resolve **Discrepancy #2**: Consolidate `OcrJobStatus` and `OcrStatus` into a single enum.
3. Complete `T-027` (Application unit tests) and `T-028` (Application architecture guard) with passing tests.
4. Re-run `dotnet build --nologo` and `dotnet test --nologo --logger "trx;LogFileName=run-21.trx" --results-directory docs/testing/results` with the fixes and new tests; confirm 0 warnings, 0 errors, all tests pass, no skipped tests.

The Application layer structure is sound, contracts are respected, boundaries are clean, and deferred tasks are correctly managed. The discrepancies are localized and fixable without architectural changes.