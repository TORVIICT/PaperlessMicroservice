# Limitations and Next Steps

This page lists what the PoC does not do or does not prove, then the ordered next steps. Every next step is a `[RECOMMENDATION]` and names the limitation or risk it addresses.

## Limitations

- `[LIMITATION]` **The OCR is simulated, not real.** `SimulatedOcrEngine` produces a deterministic text from a SHA-256 hash; it does not read images or PDFs.
- `[LIMITATION]` **The contract is not confirmed with the monolith.** It is a team proposal (`CONTRATO_OCR.md` lines 1-4; finding F-1). Every assumption below that is not confirmed carries this limitation.
- `[LIMITATION]` **A-01** (camelCase response, `application/json` accepted) is an inference from the samples, not a stated rule.
- `[LIMITATION]` **A-02 and A-03** (the wording of `error.message` and `details[].message`) are chosen by the PoC; the contract fixes only the shape, the code and the field name.
- `[LIMITATION]` **A-04** (`details = []` when no field can be identified) follows the contract literally but the exact cases are not enumerated.
- `[LIMITATION]` **A-05** (route evaluated before method) is not stated by the contract; it matches standard HTTP semantics.
- `[LIMITATION]` **A-06** (absent `Content-Type` treated as wrong) is not stated by the contract.
- `[LIMITATION]` **A-07** (the simulated values are chosen by the PoC) is allowed because the contract excludes how the text is produced.
- `[LIMITATION]` **A-08** (whitespace trimming) is not stated by the contract.
- `[LIMITATION]` **A-09** (health route rejects other methods with `405`) is derived from CT-16, not stated for the health route itself.
- `[LIMITATION]` **A-10** (no size limit) means a large payload is decoded entirely in memory; the PoC did not measure this.
- `[LIMITATION]` **There is no authentication** (CT-19) and the PoC did not add any.
- `[LIMITATION]` **No persistence, no monitoring stack and no deployment** are in scope; the PoC logs with `ILogger` only.
- `[LIMITATION]` **No performance, load or security testing** was done (out of scope).
- `[LIMITATION]` **Checklist item 16 (asynchronous behaviour) is not applicable**: the contract is synchronous (CT-01); no job submission or polling was built or tested.
- `[LIMITATION]` **Sensitivity checks were not performed** (TS-28, optional). The suite was green on the first full run.
- `[LIMITATION]` **Unknowns U-01…U-04** (the real monolith contract, extra headers/fields, the future engine contract, real payload limits) remain `[UNKNOWN / NEEDS VERIFICATION]`.

## Next steps

1. `[RECOMMENDATION]` **Confirm the assumptions A-01…A-10 and the unknowns U-01…U-04 with the monolith owners.** Addresses the limitation that the contract is a team proposal. This is not implemented.
2. `[RECOMMENDATION]` **Replace the simulated engine with the real OCR engine behind the same `IOcrEngine` port.** Addresses the simulated-OCR limitation and the risk of coupling. This is not implemented.
3. `[RECOMMENDATION]` **Decide and add authentication at the edge.** Addresses the no-authentication limitation. This is not implemented.
4. `[RECOMMENDATION]` **Decide how large payloads are handled (size limit or streaming).** Addresses the A-10 memory limitation. This is not implemented.
5. `[RECOMMENDATION]` **Add monitoring and correlation-id propagation.** Addresses the observability limitation and unknown U-02. This is not implemented.
6. `[RECOMMENDATION]` **Decide where the ACL is deployed and how it is configured.** Addresses the no-deployment limitation. This is not implemented.
7. `[RECOMMENDATION]` **Add performance, load and security testing before production.** Addresses the no-measurement limitation. This is not implemented.
