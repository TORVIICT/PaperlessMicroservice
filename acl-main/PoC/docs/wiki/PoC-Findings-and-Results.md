# PoC Findings and Results

This page states what the PoC proved, with evidence, and what it could not verify. Results come from the final test run `run-02.trx`.

## Verdict per goal

[DECISION] The mission defines three goals for the ACL (`PoC/docs/../../.opencode/sdd/constitution/mission.md`).

- `[VERIFIED]` **Goal 1 — receive requests coming from the monolith.** The `POST /api/ocr/documents` endpoint routes and accepts a valid request and the health endpoint answers. Evidence: TC-INT-001, TC-INT-002, TC-CON-001…TC-CON-006, all green in `run-02.trx`. The Api layer reads and validates the external request in `OcrController` and `OcrRequestTranslator`.
- `[VERIFIED]` **Goal 2 — simulate OCR deterministically (no real OCR).** The `SimulatedOcrEngine` returns the same result for the same input, a different text for different content, and respects the contract ranges. Evidence: TC-UNIT-007…TC-UNIT-011, TC-DET-001…TC-DET-004, all green in `run-02.trx`.
- `[VERIFIED]` **Goal 3 — return responses in the format the monolith expects.** The success and error responses match the contract shape exactly, with only contract fields and no internal markers. Evidence: TC-CON-001…TC-CON-007, TC-ISO-001…TC-ISO-005, TC-ERR-001…TC-ERR-005, all green in `run-02.trx`.

`[FINDING]` The PoC validates the ACL against *a* stated contract, not against the monolith's real one, because the contract source is a team proposal (F-1, `PoC/docs/contract-analysis.md`, `CONTRATO_OCR.md` lines 1-4).

## Results per category

[VERIFIED] From the final run `run-02.trx` (84 results, 84 passed, 0 failed, 0 notExecuted). The cases level counts are in `PoC/docs/testing/test-execution-summary.md`.

| Category | Total cases | Passed | Failed | Blocked |
|----------|-------------|--------|--------|---------|
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

[VERIFIED] Final run `run-02.trx` console line: `Passed!  - Failed: 0, Passed: 84, Skipped: 0, Total: 84`. The 84 results map to the 78 cases; the 6 extra results come from multi-row theories.

## Assumptions used

[DECISION] The tests relied on these assumptions from `PoC/docs/contract-analysis.md`:

- `[VERIFIED]` **A-01** (camelCase response, `application/json` accepted): TC-CON-003, TC-CON-005.
- `[FINDING]` **A-02, A-03** (the wording of `error.message` and `details[].message` is chosen by the PoC): tests assert shape, `code`, `field` and absence of internal detail, never the wording (TC-ERR-004, TC-ERR-005).
- `[VERIFIED]` **A-04** (`details = []` when no field can be identified): TC-VAL-024, TC-VAL-025.
- `[VERIFIED]` **A-06** (absent `Content-Type` treated as wrong): TC-INT-006.
- `[VERIFIED]` **A-08** (whitespace handling for `documentId`/`fileName`): TC-UNIT-003, TC-VAL-016.
- `[VERIFIED]` **A-09** (health route rejects other methods with `405`): covered by the `405` path in TC-INT-004.
- `[VERIFIED]` **A-10** (no size limit enforced): implied by the absence of a size check in `OcrRequestTranslator`; TC-VAL-022 and TC-VAL-023 cover Base64 validation but no test exercises a large payload. Final run `run-02.trx`.

## Defects found and fixed

[DECISION] Four defects were found and fixed during Phase 4/validation, each re-checked by a later full run (details in `PoC/docs/testing/test-execution-summary.md`):

- `[VERIFIED]` TC-VAL-010…TC-VAL-014: wrong-type fields returned an empty `details`; fixed in `OcrController`.
- `[VERIFIED]` TC-VAL-009: a `null` `language` was treated as absent; fixed with `LanguageSpecified`.
- `[VERIFIED]` TC-INT-006: a missing `Content-Type` returned `200`; fixed with an explicit check.
- `[VERIFIED]` TC-ARC-004: a test-expectation defect in the architecture helper self-test; fixed.

## Sensitivity checks

`[LIMITATION]` Not performed. The suite was green on the first full run; sensitivity checks are optional (TS-28) and none were executed.

## What could not be verified

- `[UNKNOWN / NEEDS VERIFICATION]` The real monolith contract (U-01) and any extra headers or fields it may send (U-02).
- `[UNKNOWN / NEEDS VERIFICATION]` Whether the future OCR engine keeps the same contract (U-03) and the real payload size limits (U-04).
- `[LIMITATION]` No blocked test cases exist; nothing was blocked.
