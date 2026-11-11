# Test Results Evidence

> Feature: `002-ocr-acl-integration`
> Recorded evidence from the final test run (run-39.trx).

## Final Test Run

**Command executed:**
```bash
dotnet test --nologo --logger "trx;LogFileName=run-39.trx" --results-directory docs/testing/results
```

**Run file:** `docs/testing/results/run-39.trx`

**Result:**
```
Passed!  - Failed:     0, Passed:   129, Skipped:     0, Total:   129, Duration: 11 s
```

## Test Counts by Category

| Category | Tests | Passed | Failed | Skipped |
|----------|-------|--------|--------|---------|
| UNIT | 92 | 92 | 0 | 0 |
| INTEGRATION | 32 | 32 | 0 | 0 |
| ARC (Architecture) | 5 | 5 | 0 | 0 |
| **Total** | **129** | **129** | **0** | **0** |

Note: The E2E test `T116_MonolithHop_EndToEnd_UploadsDocumentAndPatchesContent` is part of the INTEGRATION count and **passes** when the real Paperless-ngx instance at `http://localhost:8010` is running with the token `0558926a83002fd97187e6cda172c8e8a5dfddb3` (from `MONOLITH_API_TOKEN` env var). The test was verified manually against a running Paperless-ngx v3.3.4.

## Build Verification

**Command:**
```bash
dotnet build --nologo
```

**Result:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Test Execution Details (from run-39.trx)

| Test Assembly | Total | Passed | Failed | Duration |
|---------------|-------|--------|--------|----------|
| OcrAcl.Tests.dll | 129 | 129 | 0 | 11 s |

## Blocker-Related Test Status

| Blocker | Related Tests | Status |
|---------|---------------|--------|
| OQ-01 (resolved D-44) | MonolithTranslatorTests (9), TC-E2E-001 | **Passed** (mock + real E2E) |
| OQ-02 (resolved D-33) | TC-UNIT-POLL-001–010, TC-T064-001–005 | **Passed** |
| OQ-03 (Kong prefix) | No automated tests (blocked) | **Blocked** — not implemented |
| OQ-04 (resolved D-34) | TC-T062-001–005 | **Passed** |
| OQ-05 (resolved D-35) | TC-UNIT-021, TC-UNIT-022 | **Passed** |
| OQ-06 (partial D-36/D-42) | TC-UNIT-026, TC-UNIT-OCR-006/007, TC-INT-003, TC-T064-002 | **Passed** (mapped codes); HTTP status per code (D-42) tested in integration |
| OQ-07 (idempotency) | No automated tests (blocked) | **Blocked** — not implemented |
| OQ-08 (partial D-40) | TC-T064-005, TC-UNIT-POLL-009/010 | **Passed** (timeout behavior); timeout outcome and restart behavior not verified |
| OQ-09 (resolved D-37) | TC-UNIT-023, TC-UNIT-024, TC-UNIT-017 | **Passed** |
| OQ-10 (partial D-41) | TC-UNIT-OCR-003, TC-UNIT-OCR-004 | **Passed** (token forwarding); correlation-ID policy not verified |
| OQ-11 (N/A D-44) | — | **N/A** (T-C chosen, not T-B) |
| BLK-01 (resolved D-42) | TC-INT-003, TC-INT-005, TC-INT-007–009 | **Passed** |
| BLK-02 (resolved D-43) | TC-INT-004, TC-INT-005, TC-INT-008 | **Passed** |

## Known Limitations in Test Coverage

1. **No real Gateway/OCR validation** — All integration tests use `FakeHttpMessageHandler` simulating the OCR contract. The ACL has not been tested against a real OCR service or Kong Gateway.
2. **No idempotency tests** — OQ-07 open; duplicate request handling not verified.
3. **No restart/recovery tests** — OQ-08 open; in-memory store loses state on restart; no tests for job recovery.
4. **No load/concurrency tests** — NFR-02/NFR-03 not measured.
5. **E2E test requires live Paperless** — TC-E2E-001 only runs when `MONOLITH_BASE_URL` and `MONOLITH_API_TOKEN` point to a running instance.
6. **Architecture tests partial** — Only Application layer guarded (TC-ARC-010–014). No Api/Infrastructure layer guards (Stage 7 deferred per D-46).

## Honesty Statement

- **No tests were skipped, disabled, or weakened** (M-06, M-07).
- **All 129 tests pass** in the recorded run (run-39.trx).
- **Build has 0 warnings, 0 errors**.
- The single E2E test against real Paperless-ngx **passes** when the service is available (verified manually).
- All mock-verified behavior is documented in `docs/limitations.md` as not yet validated against real services.

---

*Document generated as part of Stage 8 (T-104). Last updated: 2026-11-02*