# Test Execution Summary

- **Date:** 2026-10-19
- **OS:** Linux
- **.NET SDK:** 10.0.301 (stable)
- **Target framework:** net10.0
- **Command:** `dotnet test --nologo --logger "trx;LogFileName=run-02.trx" --results-directory docs/testing/results` (run from `PoC/`)
- **Results file:** `docs/testing/results/run-02.trx`

## Totals — results level (what the `.trx` counts)

| Total | Passed | Failed | NotExecuted (skipped) |
|-------|--------|--------|-----------------------|
| 84 | 84 | 0 | 0 |

Console summary line: `Passed!  - Failed: 0, Passed: 84, Skipped: 0, Total: 84`.
`.trx` counters: `total="84" executed="84" passed="84" failed="0" notExecuted="0"`.

## Totals — cases level (matrix rows)

| Category | Total | Passed | Failed | Blocked |
|----------|-------|--------|--------|---------|
| UNIT | 14 | 14 | 0 | 0 |
| INT | 6 | 6 | 0 | 0 |
| CON | 7 | 7 | 0 | 0 |
| VAL | 29 | 29 | 0 | 0 |
| ERR | 5 | 5 | 0 | 0 |
| ISO | 5 | 5 | 0 | 0 |
| DET | 4 | 4 | 0 | 0 |
| ASY | 3 | 3 | 0 | 0 |
| ARC | 5 | 5 | 0 | 0 |
| **Total** | **78** | **78** | **0** | **0** |

The 84 results map to the 78 cases; the 6 extra results come from multi-row theories (TC-CON-006 = 3 rows; TC-INT-004, TC-INT-005, TC-ERR-003, TC-ERR-005 = 2 rows each). Every result belongs to exactly one matrix row and every `Passed` row has at least one result (TS-20). Skipped = 0.

## Defects found and fixed

No failures were observed in the final suite. The defects below were found and fixed while writing the tests (Phase 3/validation), each re-checked by a later full run.

| Test case | Symptom | Root cause | Classification | Fix | Re-run result |
|-----------|---------|------------|----------------|-----|---------------|
| TC-VAL-010…014 | `details` was empty for wrong-type fields | `ReadFromJsonAsync` throws `JsonException`; the controller returned an empty `details` instead of the failing field (D-17) | Implementation defect | Controller now parses with `JsonDocument` and derives the field from `JsonException.Path` (`BuildDeserializationError`/`NormalizeJsonPath`) | Passed |
| TC-VAL-009 | null `language` returned `200` (treated as absent) | A nullable `string` cannot distinguish an absent key from an explicit `null` (CT-07) | Implementation defect | `OcrDocumentRequest.LanguageSpecified` (from `JsonIgnore`) set from the parsed JSON; the translator rejects an explicit null (A-08 semantics) | Passed |
| TC-INT-006 | request without `Content-Type` returned `200` | `[ApiController]` only rejects a *wrong* content type, not a missing one | Implementation defect | The action checks `Request.ContentType` first and returns `415` (D-08, A-06) | Passed |
| TC-ARC-004 | the helper self-test asserted the wrong substring | The violation string carries the forbidden token (`Microsoft.AspNetCore`), not the full `using` target | Test defect (expectation still follows its source) | Assertion aligned with the actual helper output token | Passed |

No test was skipped, disabled or weakened at any point (M-07, TS-10); no expected value was changed to match behaviour (TS-11).

## Blocked cases and reasons

None.

## Known gaps (scenarios not covered and why)

- **Checklist item 16 (asynchronous behaviour)** is not applicable: the contract is synchronous (CT-01, `CONTRATO_OCR.md` "Flow"). It is listed under "Not applicable scenarios" in `test-cases.md` with that reason. Cancellation and concurrency (item 15) are covered by the `ASY` cases.
- The exact wording of `error.message` and `details[].message` is not asserted as a contract value (A-02, A-03); the tests assert presence, type, code and the absence of internal detail instead.
- Real OCR quality, extra languages and payload size limits are out of contract (`CONTRATO_OCR.md` lines 90-94).

## Sensitivity checks performed (TS-28)

Not performed. The suite was fully green on the first full run; sensitivity checks are optional and none were executed.

## Implemented vs. verified vs. finding vs. recommendation (M-09)

- **Implemented:** the ACL service (`Api`/`Application`/`Infrastructure`) that accepts the `POST /api/ocr/documents` and `GET /api/ocr/health` contract, validates the request, simulates OCR deterministically and returns the contract response/error shapes.
- **Verified by tests:** all 78 cases / 84 results green in `run-02.trx`, covering valid requests, mapping, contract compatibility, validation per field, HTTP edge cases, error shape, isolation, determinism, cancellation/concurrency and architecture rules.
- **Findings:** the contract source is a team proposal, not the monolith's real contract (`CONTRATO_OCR.md` lines 1-4); the error contract is complete and required replacing the framework defaults (T-26); unknown JSON properties are ignored (D-16).
- **Recommendations:** before the real ACL, implement the same error normalisation for every deployment environment and confirm the monolith's real field/error wording (A-02, A-03); keep the determinism hash (D-20) documented for the engine team.
