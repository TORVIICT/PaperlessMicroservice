# Testing Documentation

This page summarises how the PoC tests are organised, how to run them and what they proved. It is a summary; the sources of truth are `PoC/docs/testing/test-cases.md` (the 78-case matrix) and `PoC/docs/testing/test-execution-summary.md` (the execution totals). The raw evidence lives in `PoC/docs/testing/results/*.trx`.

## Test strategy

[DECISION] The tests are grouped in the categories of `PoC/docs/../../.opencode/sdd/constitution/testing-standard.md`, each chosen because it proves a different property:

- `UNIT` — the service, the simulated engine and the translators in isolation (plain xUnit with hand-written fakes).
- `INT` — the real HTTP pipeline in memory (`WebApplicationFactory<Program>`): routing, binding, middleware, status codes.
- `CON` — compatibility with the contract using its literal JSON samples.
- `VAL` — invalid requests per field (missing, null, wrong type, out of range, bad format).
- `ERR` — the error body shape and status mapping, including unexpected exceptions via a faulty fake.
- `ISO` — internal models and ACL details never leak into the external response.
- `DET` — determinism of the simulation across repeats and fresh instances.
- `ASY` — cancellation and concurrency (the contract is synchronous, so there is no async flow to test).
- `ARC` — the layer dependency rules by reflection and source inspection.

## Project layout

[IMPLEMENTED] The tests live in `PoC/tests/OcrAcl.Tests/`, one folder per category plus a shared `Fixtures/` folder:

- `Fixtures/` — `OcrAclFactory.cs`, `ContractSamples.cs`, `Fakes.cs`, `HttpTestHelpers.cs`.
- `Unit/`, `Integration/`, `Contract/`, `Validation/`, `Errors/`, `Isolation/`, `Determinism/`, `Async/`, `Architecture/`.

## How to run

[VERIFIED] Run from `PoC/` (this is the command that produced the final run `run-02.trx`):

```bash
dotnet test --nologo --logger "trx;LogFileName=run-02.trx" --results-directory docs/testing/results
```

[VERIFIED] Run a subset while working (this command was run during Phase 4; it selects the `VAL` category):

```bash
dotnet test --nologo --filter "Category=VAL"
```

## Where the evidence lives

- `PoC/docs/testing/results/run-02.trx` — the final run (raw results).
- `PoC/docs/testing/test-cases.md` — the 78-case matrix with sources and results.
- `PoC/docs/testing/test-execution-summary.md` — the execution totals, defects and gaps.

## Final run

[VERIFIED] Final run file `run-02.trx`, dated 2026-10-19, on .NET SDK `10.0.301`, target framework `net10.0`.

- Results level (what the `.trx` counts): total 84, passed 84, failed 0, notExecuted 0.
- Cases level (matrix rows): total 78, passed 78, failed 0, blocked 0.
- Console line: `Passed!  - Failed: 0, Passed: 84, Skipped: 0, Total: 84`.

[VERIFIED] Per category (cases level, from `PoC/docs/testing/test-execution-summary.md`); the cases map to the results of the final run `run-02.trx`:

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

## Test integrity rules and their result

- `[VERIFIED]` **TS-10** (no test skipped, disabled, ignored or weakened): the scan `grep -rnE 'Skip\s*=|\[Ignore|#if false' PoC/tests` printed nothing. Final run `run-02.trx`.
- `[VERIFIED]` **TS-11** (expected values never changed to match behaviour): no expected value was changed to fit the code; the four defects were fixed in the source, not in the expectations (see `PoC/docs/testing/test-execution-summary.md`). Final run `run-02.trx`.
- `[VERIFIED]` **TS-15** checks: the scans printed nothing and the counts agreed (78 `[Fact]/[Theory]` methods match 78 `TestCaseId` traits; code and matrix ID sets equal). Final run `run-02.trx`.
- `[VERIFIED]` **TS-26** reconciliation: the check printed nothing; every `.trx` result maps to exactly one matrix row and every passed row has at least one result. Final run `run-02.trx`.

## Sensitivity checks

`[LIMITATION]` Not performed. The suite was green on the first full run; sensitivity checks are optional (TS-28) and none were executed.

## Blocked cases

`[VERIFIED]` None. No test case is `Blocked` in the matrix and `run-02.trx` reports `notExecuted 0`.

## Known gaps

- `[LIMITATION]` Checklist item 16 (asynchronous behaviour) is not applicable: the contract is synchronous (CT-01); it is listed under "Not applicable scenarios" in `PoC/docs/testing/test-cases.md`.
- `[LIMITATION]` The exact wording of `error.message` and `details[].message` is not asserted as a contract value (A-02, A-03); tests assert presence, type, code and absence of internal detail.
- `[LIMITATION]` Real OCR quality, extra languages and payload size limits are out of contract (`CONTRATO_OCR.md` lines 90-94).
