# Technical Decisions Log

> Feature: `002-ocr-acl-integration`. Records every choice the contracts do not dictate.

## D-33 — ACL → OCR transport: HTTP polling — 2026-10-31
**Answers:** OQ-02
**User said:** "pero pasame el .opencode con eso resuelto" (confirming the proposal: "HTTP polling … Con mock, polling es lo más simple").
**Decision:** The ACL talks to the OCR service over **HTTP polling** (`01 §3–§4`: `POST /api/v1/ocr/jobs`, then `GET` with the documented back-off). No broker and no shared volume. MassTransit is **not** implemented; the transport stays behind `IOcrClient` so it can be added later without touching the use cases.
**Affects:** spec §5.3, §5.4, §8, §9, FR-05, FR-20, AC-17; plan §7, §14; `T-041`, `T-060`.
**Task updates:** `T-041` → Todo. `T-060` → Not applicable (D-33).
**Spec/plan drift:** spec §5.4 and plan §7 still describe MassTransit as an open alternative and HTTP polling as "proposed". The user decides whether to edit them (M-01).

---

### D-34 — ACL-owned in-memory document store; identity model — 2026-10-31
**Answers:** OQ-04 (and UC-05)
**User said:** "pero pasame el .opencode con eso resuelto" (confirming the proposal: "El ACL guarda su propio documentId ↔ jobId ↔ clientReference detrás de un puerto"; in-memory is the assistant's recommendation for the store kind).
**Decision:** The ACL **may keep its own state** behind the `IDocumentStore` port. The FE `id` is an **ACL-generated opaque `documentId`** (UUID), not the OCR `jobId`. The store keeps `documentId ↔ jobId ↔ clientReference` plus `fileName`, `contentType`, `sizeBytes`, status and timestamps (the fields the OCR never returns, INC-07/INC-08). The adapter is **in-memory** for now: state is **lost on restart**, and this limitation must be documented. No relational adapter. The ACL never reads or writes another service's database (C-11). This does **not** answer OQ-08 (what happens to in-flight jobs after a restart).
**Affects:** spec §7.3, §12, FR-19, AC-16; plan §6, §14; `T-020`, `T-022`, `T-023`, `T-024`, `T-031`, `T-032`, `T-036`, `T-057`, `T-058`, `T-062`.
**Task updates:** `T-020`, `T-022`, `T-024`, `T-031`, `T-032`, `T-057`, `T-062` → Todo. `T-058` → Not applicable (D-34). `T-023` and `T-036` are not unblocked: they now wait on BLK-02 (shape of the text response), which was never part of OQ-04.
**Spec/plan drift:** plan §6.2 says "Proposed design (not decided)" and §4 marks `IDocumentStore` and `GetDocumentStatusHandler` as blocked. The user decides whether to edit them (M-01).

---

### D-35 — Accepted formats and size limit — 2026-10-31
**Answers:** OQ-05
**User said:** "pero pasame el .opencode con eso resuelto" (confirming the proposal: "Aceptar solo PDF, PNG y JPEG, y rechazar los otros 5 formatos del contrato del frontend; límite de 50 MB").
**Decision:** The ACL accepts only **PDF (`application/pdf`), PNG (`image/png`) and JPEG (`image/jpeg`)**. TIFF, GIF, BMP, WebP and HEIC are rejected with `UNSUPPORTED_FILE_TYPE`. The **50 MB** limit is enforced by the ACL before sending bytes to the OCR; larger files are rejected with `FILE_TOO_LARGE`. The size is configurable. The 5-format limitation is recorded as a known limitation until the OCR team extends its contract.
**Affects:** spec §7.4, §11.1, FR-02; plan §5.2; `T-030`.
**Task updates:** `T-030` → Todo (OQ-09 also answered, D-37).
**Spec/plan drift:** spec §7.4 and plan §5.2 present this as "proposed default". The user decides whether to edit them (M-01).

---

### D-36 — Error envelope and mapping of the 4 OCR-only codes — 2026-10-31
**Answers:** OQ-06 (**in part**)
**User said:** "pero pasame el .opencode con eso resuelto" (confirming the proposal: "Usar el formato de error del frontend; los 4 códigos que solo existen en el OCR se mapean a OCR_PROCESSING_FAILED con el código original en details").
**Decision:** The ACL returns the **FE error envelope** `{code, message, documentId, details}` (not RFC 9457). The four OCR-only codes `PASSWORD_PROTECTED_PDF`, `CORRUPT_DOCUMENT`, `PROCESSING_TIMEOUT` and `ENGINE_ERROR` are mapped to `OCR_PROCESSING_FAILED`, with the **original OCR code kept in `details`** (for example `details.ocrCode`). No stack trace, exception type, internal namespace, OCR host path or document content ever appears in an error body.
**Not decided here:** (1) the **HTTP status for each FE code** and (2) the **inner shape of the `error` object** inside the document model. Both were part of OQ-06 in `07` but not part of the proposal that was confirmed; they are registered as **BLK-01** with a proposed default. The assistant also suggested, as a possible change, keeping `PASSWORD_PROTECTED_PDF` as its own code because the user can act on it; the user has not asked for it, so it is **not** applied.
**Affects:** spec §7.5, §13, FR-11, AC-06, AC-18; plan §10; `T-033`, `T-036`, `T-081`, `T-082`.
**Task updates:** `T-081` → Todo. `T-033` and `T-082` → Blocked (BLK-01). `T-036` → Blocked (BLK-02).
**Spec/plan drift:** plan §10 and spec §13 say the envelope is a "proposed default". The user decides whether to edit them (M-01).

---

### D-37 — Default language and mode — 2026-10-31
**Answers:** OQ-09
**User said:** "pero pasame el .opencode con eso resuelto" (the proposal was `eng`/`skip` and noted "Si tus documentos son en español, quizá quieras spa"; **`spa` is the assistant's recommendation**, the user's documents being Spanish per the project brief).
**Decision:** `language` defaults to **`spa`** and `mode` to **`skip`**. Both are **configuration values** (not hard-coded) sent to the OCR on every submit. The FE upload has no field for them, so the FE cannot override them. Only the single-code form (`spa`) is used; the contract does not document combined codes such as `spa+eng`. Whether the OCR service has the `spa` Tesseract data installed is **not verified** and is to be confirmed with the OCR team.
**Affects:** spec §11.1; plan §8.1; `T-030`.
**Task updates:** `T-030` → Todo (OQ-05 also answered, D-35).
**Spec/plan drift:** spec §11.1 says the default is `eng`/`skip` "unless the user says otherwise". The user decides whether to edit it (M-01).

---

### D-38 — ACL project root confirmed for feature 002 — 2026-10-31
**Answers:** C-05 / T-005 (write scope confirmation before first write outside `.opencode/`)
**User said:** "si"
**Decision:** The ACL project root for feature `002-ocr-acl-integration` is the **repository root** `/home/steven/Documents/GitLab/Paperless-ngx/acl` (where `.git/`, `.opencode/`, `README.md`, `AGENTS.md` live). The historical `PoC/` directory (feature 001, superseded by D-06) is **not** the project root. A new solution, source project, and test project will be created at the repository root following the structure in `tech-stack.md` T-10.
**Affects:** All implementation tasks (T-005 onwards), `tech-stack.md` T-10, C-05, M-01
**Task updates:** `T-005` → Todo (unblocked for execution)
**Spec/plan drift:** None (this decision operationalizes existing rules).

---

### D-39 — External frontend; monolith used only as storage — 2026-10-31
**Answers:** none (context). It narrows OQ-01 but does **not** answer it.
**User said:** "el front de paperless no lo vamos a usar, vamos a usar un frontned externo, ese frontend llama al ai gateway y ese llama al acl el acl llama al servicio ocr y cuando termina y genera el texto metemos ese texto en la db de ocr y en el monolito nunca usamos el front del monolito solo metemos la info al monolito"; "nose si el pdf,tambien pero el caso es que si desconectamos el monolito en el front externo podamos: descargar el pdf y el texto extraido"; "un compañero externo hara el front yo le pase tambien los contratos del front y el contrato del ocr".
**Decision:** (1) The frontend is **external**, built by a teammate outside this project from the FE contract (`02`) and the OCR contract (`01`); Paperless-ngx's own UI is **not** used. (2) Flow: Frontend → AI Gateway → ACL → OCR service. (3) The monolith is used **only to store** information. (4) The external frontend must be able to **download the PDF and the extracted text even if the monolith is disconnected**. (5) Whether the monolith keeps only the text or also the PDF is **not decided**.
**Not decided here:** the **mechanism** by which the result reaches the monolith. OQ-01 stays **Open**.
**Affects:** `mission.md` (Context); `04` §1 and §4; spec §1.3 and §3; BLOCKERS OQ-01; register `07` UC-06…UC-08.
**Task updates:** none. `T-016`, `T-026`, `T-043`, `T-055`, `T-070`…`T-073` and `T-116` stay Blocked (OQ-01).
**Spec/plan drift:** spec §1.3 and §3 were edited to match (UC-06…UC-08). The plan was not touched; the user decides whether to edit it (M-01).

---

### D-40 — Asynchronous processing model — 2026-10-31
**Answers:** OQ-08 (**in part**)
**User said:** "si retorna 202 de una pero sigue en el background"
**Decision:** `POST /api/v1/documents` returns `202` immediately and the ACL follows up the OCR job **in the background**. The document state is kept in the ACL store (D-34).
**Not decided here:** (1) what happens when the ~120 s timeout expires while the OCR job is still running; (2) what happens after an ACL restart (the in-memory store of D-34 loses the jobs in progress). The assistant suggested, as an option, marking the document `failed` with `OCR_PROCESSING_FAILED` and `PROCESSING_TIMEOUT` in `details`, and documenting the restart loss as a limitation; the user has not confirmed it, so it is **not** applied. Both points stay open under OQ-08.
**Affects:** spec §9, §13.3, FR-12, AC-07; plan §7.3; `T-061`.
**Task updates:** `T-061` → Todo. `T-048`, `T-063` and `T-083` stay Blocked (OQ-08).
**Spec/plan drift:** spec §9 and plan §7.3 present the model as a "proposed default". The user decides whether to edit them (M-01).

---

### D-41 — Token handled by the API Gateway — 2026-10-31
**Answers:** OQ-10 (**in part**)
**User said:** "el token lo maneja el api gateway" (in answer to the assistant's question: if Kong already validates the user the usual choice is to forward the user's token; if the OCR needs its own credential, a service token).
**Decision:** The API Gateway handles the token. **(inference)** The ACL does not issue or validate tokens: it forwards the inbound `Authorization` header to the OCR service, which expects it on every call (OCR-SRC §2.2). No secret is written to the repository (C-03) and no token is logged.
**Not decided here:** the **correlation-ID policy** (propagate an inbound `X-Correlation-ID` or generate one). The assistant's suggestion (propagate if present, UUID v4 if absent) is not confirmed. It is also not known whether the OCR requires a credential of its own.
**Affects:** spec §15, FR-04, FR-14, AC-09; plan §12; `T-087`.
**Task updates:** `T-087` → Todo. `T-053` and `T-086` stay Blocked (OQ-10).
**Spec/plan drift:** plan §12 says the token strategy depends on the Gateway's auth model. The user decides whether to edit it (M-01).

---

### D-42 — HTTP status per FE code and error object shape — 2026-11-01
**Answers:** BLK-01
**User said:** "aprobado el default de ambos tal como está en BLOCKERS.md"
**Decision:** The ACL returns the **FE error envelope** `{code, message, documentId, details}` with the following **HTTP status per code**:
- `INVALID_FILE` → 400
- `UNSUPPORTED_FILE_TYPE` → 415
- `FILE_TOO_LARGE` → 413
- `DOCUMENT_NOT_FOUND` → 404
- `DOCUMENT_NOT_READY` → 409
- `OCR_PROCESSING_FAILED` → 422 (when an endpoint is asked for the result of a failed document)
- `SERVICE_UNAVAILABLE` → 503

The **inner `error` object** inside the document model (returned by `GET /documents/{id}`) has the same shape as the envelope but **without `documentId`**: `{code, message, details}`.

No stack trace, exception type, internal namespace, OCR host path, or document content ever appears in an error body.
**Affects:** spec §7.5, §13, FR-11, AC-06, AC-18; plan §10; `T-033`, `T-081`, `T-082`.
**Task updates:** `T-033` → Todo, `T-082` → Todo. `T-035`, `T-081` (wait on T-033) → Todo.
**Spec/plan drift:** spec §7.5 and plan §10 present HTTP status and error shape as "proposed default". The user decides whether to edit them (M-01).

---

### D-43 — Response body of GET /documents/{id}/text — 2026-11-01
**Answers:** BLK-02
**User said:** "aprobado el default de ambos tal como está en BLOCKERS.md"
**Decision:** The response body for `GET /api/v1/documents/{id}/text` is:
```json
{ "id": "<documentId>", "text": "<result.text>", "pageCount": <result.pageCount> }
```
No invented fields (no confidence, no language). Returns `409 DOCUMENT_NOT_READY` while the document status is not `completed`.
**Affects:** spec §7.2 (FE-E4), FR-09; plan §5; `T-023`, `T-036`.
**Task updates:** `T-023` → Todo, `T-036` → Todo.
**Spec/plan drift:** spec §7.2 marks FE-E4 shape as `[NOT DEFINED]`. The user decides whether to edit it (M-01).

---

### D-44 — OQ-01 resolved: ingestion through the monolith's public REST API (T-C) — 2026-11-01
**Answers:** OQ-01 (and, in part, point 5 of D-39)
**User said:** "pero ya lo acabamos de definir" / "ya podemos hacer la fase 5 no?" (after validating the mechanism by hand against Paperless-ngx v3.3.4: upload, task polling, content overwrite, and a PDF/A upload test).
**Decision:** The ACL delivers results to the monolith using only its **public REST API**. No parser piece (T-B), no new endpoint (T-A), no change to the monolith.
1. `POST /api/documents/post_document/` (multipart: `document`, optional `title`, `created`) → task UUID.
2. `GET /api/tasks/?task_id=<uuid>` until `status == "success"` → `results[0].result_data.document_id` (`failure` → mapped error; empty `results` → retry with back-off).
3. `PATCH /api/documents/{id}/` with `{"content": "<OCR text>"}`, only after the task succeeded.
Auth: `Authorization: Token <token>` of a dedicated service user with `documents.add_document`; the token comes from configuration, never from the repository or logs (C-03).
Contract: `docs/monolith-ingestion-contract.md` (label MON-API). `03` §10 summarises it.
Alternatives rejected: T-A (new internal endpoint) and T-B (remote parser `AclDocumentParser`): both require changing or extending the monolith.
Evidence: validated manually on a local Paperless-ngx v3.3.4 (2026-11-01): upload → task `success` with `result_data.document_id`; `PATCH content` persisted; an image-only PDF took 3.57 s and the monolith generated an archive file; a PDF/A with text layer (made with ocrmypdf) took 0.86 s, no archive file was generated, and `content` held the layer's text. The monolith's logs were not checked.
Consequences accepted:
(a) the monolith **stores the uploaded file**, because `document` is required in `post_document`; this answers part of D-39 point 5 (the monolith keeps the file, not only the text);
(b) the monolith runs its own consume task (thumbnail, archive, and its own OCR if the file has no text layer; `OCR_MODE` is global, not per request);
(c) there is no native idempotency key: the ACL keeps the monolith `document_id` per job (D-34), and since that store is in-memory a restart can cause a duplicate upload (tied to OQ-08, still open);
(d) a PDF/A with text layer made with ocrmypdf was tested and the monolith skipped its own OCR; the PDF/A produced by the project's own OCR service and large files are still untested.
**Not decided here** (proposed defaults in the contract, pending the user's confirmation):
- which file the ACL uploads: the OCR service's PDF/A when it delivers one with a text layer, otherwise the original file;
- whether the `PATCH` of `content` is always sent (proposed: yes, so that `content` equals the OCR service's text) or only when the uploaded file has no text layer.
**Affects:** BLOCKERS OQ-01; spec §1.3 and §3; `04` §1 and §4; Stage 5 of the plan; `T-016`, `T-026`, `T-043`, `T-055`, `T-070`…`T-073`, `T-116`.
**Task updates:** `T-073` → Todo (first). `T-016`, `T-026`, `T-043`, `T-055`, `T-070`, `T-071`, `T-072`, `T-116` → Todo (re-checked against contract). `T-055` → Not applicable (D-44).
**Spec/plan drift:** spec §1.3/§3 and `04` §1/§4 were edited to match (UC-06…UC-08). The plan was not touched; the user decides whether to edit it (M-01).

---

### D-45 — File uploaded to the monolith — 2026-11-01
**Answers:** OQ-01 (follow-up to D-44, first pending default)
**User said:** "si pal 70 podiamos dejar esto: Qué archivo sube el ACL: el PDF/A del servicio OCR si trae capa de texto, y si no, el original".
**Decision:** In Step 1 of MON-API (`post_document`), the ACL uploads as `document`:
- the **PDF/A with text layer** when the OCR service delivers one;
- otherwise the **original file**.
The `ARCHIVE_FILE_GENERATION` setting controls whether the monolith generates an archive file.
**Affects:** `docs/monolith-ingestion-contract.md` §4 and §5; `T-070`, `T-071`.
**Task updates:** none.
**Spec/plan drift:** none.

---

### D-46 — Skip Stage 6 and 7; proceed to Stage 8 (documentation) — 2026-11-02
**Answers:** Stage 6 (errors, observability, security), Stage 7 (architecture and cross-cutting tests)
**User said:** "Decisión del usuario: saltar Stage 6 y Stage 7 por ahora y hacer el Stage 8 (documentación, T-100 a T-107)."
**Decision:** Stage 6 (errors, observability, security) and Stage 7 (architecture and cross-cutting tests) are **deferred by user request**. The agent will proceed directly to Stage 8 (documentation: T-100 through T-107). The dependency on T-097 (Stage 7 gate) for T-100–T-103 and T-106 is ignored per user instruction; the dependency remains in tasks.md but is not enforced. The tasks in Stage 6 and 7 remain in tasks.md with their Blocked/Waiting states and are not marked Done.
**Affects:** PROGRESS.md (Stage 6, 7 state = "Deferred by user"); workflow.md (Stage 6, 7 state = "Deferred by user"); roadmap.md (Stage 6, 7 remain in "In Review" or move to "Backlog"); T-097 remains Blocked (OQ-08) in tasks.md but is ignored for T-100–T-103/T-106 per D-46.
**Task updates:** Stage 6 tasks (T-080–T-089) and Stage 7 tasks (T-090–T-097) remain in their current Blocked/Waiting states; no tasks are moved to Todo or Done.
**Spec/plan drift:** Stage 6 and 7 in spec.md/plan.md still describe work that is now deferred. The user decides whether to edit them (M-01).