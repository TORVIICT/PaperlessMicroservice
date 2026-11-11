# Home

This is the wiki-ready documentation of the OCR Anti-Corruption Layer (ACL) Proof of Concept (PoC). The PoC is a small ASP.NET Core service that receives the requests the monolith would send, simulates OCR deterministically and answers in the format the monolith expects. It exists to show how an ACL keeps the original contract and avoids coupling, not to run real OCR.

[IMPLEMENTED] The PoC code lives under `PoC/src/OcrAcl.Api` and its tests under `PoC/tests/OcrAcl.Tests`. `PoC/docs/contract-analysis.md` and `PoC/docs/technical-decisions.md` hold the identified contract and the decisions.

## Status legend

Every statement in these pages is labelled with exactly one of these levels of truth:

- `[VERIFIED]` — proven by a test or a command that was run; it cites the test case IDs and the final run file (`run-02.trx`) or the command and its result.
- `[IMPLEMENTED]` — exists in the code; it cites the file path. Not necessarily tested.
- `[DECISION]` — a choice that was adopted; it cites a decision ID (`D-xx`).
- `[FINDING]` — something observed or deduced; deductions also carry **(inference)**. It cites its source.
- `[LIMITATION]` — something the PoC does not do or does not prove.
- `[RECOMMENDATION]` — a proposal for a future implementation; it is not implemented and says so.
- `[UNKNOWN / NEEDS VERIFICATION]` — not backed by a file that was read or a command that was run.

## Page index

| Page | Summary |
|------|---------|
| [Architecture and Workflow](Architecture-and-Workflow.md) | The three layers and the dependency rule, how a request flows, and how the original contract is kept. |
| [Technical Decisions](Technical-Decisions.md) | Every decision `D-01` to `D-39`, plus the SDK, framework and package versions. |
| [Architectural Adaptations](Architectural-Adaptations.md) | Adaptations made during implementation and validation, and adaptations proposed for the real ACL. |
| [PoC Findings and Results](PoC-Findings-and-Results.md) | The three goals of the mission with a verdict, the results per category and what could not be verified. |
| [Limitations and Next Steps](Limitations-and-Next-Steps.md) | What the PoC does not cover and the ordered next steps. |
| [Risks and Mitigation](Risks-and-Mitigation.md) | Risks found in this PoC with origin, likelihood, impact and mitigation strategy. |
| [Testing Documentation](Testing-Documentation.md) | How the tests are organised, how to run them and what they proved, with the final run totals. |

## Publishing status

**Not published. The agent has no access to the project wiki.**

[FINDING] The pages in `PoC/docs/wiki/` are written to be copied into the project wiki by a person. The wiki platform in use is `[UNKNOWN / NEEDS VERIFICATION]`.

### Moving the pages to the wiki

1. Copy the 8 pages of this index (and the optional `_Sidebar.md`) into the project wiki.
2. Keep each file name as the wiki page name (`Home`, `Architecture-and-Workflow`, and so on).
3. If the wiki platform needs it, remove the `.md` extension from the links.
4. Check that the Mermaid diagrams render. If they do not, the plain-text descriptions next to each diagram remain.
