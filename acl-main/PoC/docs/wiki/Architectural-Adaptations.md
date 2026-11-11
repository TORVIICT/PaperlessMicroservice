# Architectural Adaptations

This page separates two things: adaptations that were actually made while building and validating the PoC, and adaptations proposed for the real ACL. Nothing in the second part is implemented.

## Part 1 — Adaptations made

[DECISION] These are the changes to the design that happened between the initial design (Phase 2) and the final code, taken from the "Defects found and fixed" table of `PoC/docs/testing/test-execution-summary.md`, the changed decisions and `PoC/PROGRESS.md`.

- `[IMPLEMENTED]` **The request body is parsed and validated inside `OcrController`, not by MVC model binding** (decision D-14, adapted while fixing TC-VAL-010…TC-VAL-014). What changed: the controller reads the body with `JsonDocument.ParseAsync` and derives the failing field from `JsonException.Path` where before the automatic binding returned an empty `details` for wrong-type fields. Why: the contract requires one `details` entry per known invalid field (CT-13). Which defect triggered it: TC-VAL-010…TC-VAL-014 in the defects table.
- `[IMPLEMENTED]` **`OcrDocumentRequest` gained a `LanguageSpecified` flag set from the parsed JSON** (defect TC-VAL-009). What changed: the translator can tell an absent `language` (default `eng`, CT-07) apart from an explicit `null` (invalid). Why: a nullable `string` alone cannot distinguish the two cases. Which defect triggered it: TC-VAL-009 in the defects table.
- `[IMPLEMENTED]` **The controller checks `Request.ContentType` before reading the body** (decision D-08, adapted while fixing TC-INT-006). What changed: an absent `Content-Type` now returns `415`, where `[ApiController]` alone rejected only a wrong content type. Why: the contract requires `415` for a request that is not `application/json` (CT-14, A-06). Which defect triggered it: TC-INT-006 in the defects table.
- `[IMPLEMENTED]` **The solution file is `OcrAcl.slnx`, not `OcrAcl.sln`** (decision D-39, recorded in `PoC/PROGRESS.md` as finding F-6). What changed: the second project had to be added to the XML solution format emitted by SDK 10.0.301. Why: `dotnet new sln` on this SDK produces `.slnx`. Which defect triggered it: none; it was an environment finding, and the architecture test now accepts either name.

[FINDING] No other architectural adaptation was needed. The layer split, the port and the one error-handling component from Phase 2 remained valid through implementation and validation. The fourth row of the defects table (TC-ARC-004) was a test-expectation defect, not an architectural change.

## Part 2 — Adaptations proposed for the real ACL

[DECISION] Nothing in this part is implemented. Each item says what would change, in which layer, and why the PoC suggests it, citing a finding or a limitation.

- `[RECOMMENDATION]` **Replace the simulated engine behind the existing port.** What would change: a new implementation of `IOcrEngine` in `Infrastructure` that calls the real OCR service. Which layer: `Infrastructure` only. Why: the PoC shows the port isolates the engine (finding in `PoC/docs/technical-decisions.md` D-20, and the `[LIMITATION]` that the engine is simulated). Nothing else would move.
- `[RECOMMENDATION]` **Add authentication at the edge.** What would change: a new `Api`-layer component (for example middleware or an authentication handler) before the controller. Which layer: `Api`. Why: the contract has no authentication (CT-19), so it was left out; the real ACL would decide it. This addresses the limitation about the absence of authentication.
- `[RECOMMENDATION]` **Handle large payloads with a size limit or streaming.** What would change: a limit in `Api` validation and possibly a streaming path to the engine. Which layer: `Api` and `Application`. Why: file-size limits are out of contract (`CONTRATO_OCR.md` lines 90-94; A-10) and the PoC decodes the whole document in memory. This addresses the memory-use limitation.
- `[RECOMMENDATION]` **Add monitoring and correlation ids.** What would change: structured logging and a correlation-id propagation component. Which layer: `Api` (and a port if the engine must forward it). Why: the PoC logs with `ILogger` only (D-37) and the contract does not define headers (U-02). This addresses the observability limitation.
- `[RECOMMENDATION]` **Confirm the real contract wording and shape with the monolith.** What would change: `Api` contracts and error text once the real contract is known. Which layer: `Api`. Why: the contract used is a team proposal and not the monolith's real one (finding F-1, assumptions A-02, A-03, unknown U-01). This addresses the risk of matching a contract that differs from the monolith's.
