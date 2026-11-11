# Monolith Contract Acceptance Checklist (MON-API)

> Feature: `002-ocr-acl-integration`
> Source: `docs/monolith-ingestion-contract.md` (label **MON-API**)
> Date: 2026-11-01
> Decision: D-44 (OQ-01 resolved via T-C: public REST API)

---

## Checklist

Each item must be verified against the contract source (`docs/monolith-ingestion-contract.md`) and marked with its evidence tag (`[OBSERVED]` or `[CODE]` from the contract). Items not verified are marked **Not Verified**.

---

### 1. Scope and Target

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| C1 | Target is Paperless-ngx v3.3.4 | ✅ | `[CODE]` (contract header) | Commit `f319b05e0fb46b4643a47bf5d8cc4243099507bb` |
| C2 | ACL is the **only** caller; external frontend never talks to Paperless | ✅ | `[CODE]` (Scope §1) | |
| C3 | No database access by ACL (C-11) | ✅ | `[CODE]` (Scope §1) | |
| C4 | No changes to the monolith required | ✅ | `[CODE]` (Scope §1) | |

---

### 2. Authentication

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| A1 | Header: `Authorization: Token <token>` on every call | ✅ | `[OBSERVED]` (§2) | |
| A2 | Token obtained via `POST /api/token/` with username/password | ✅ | `[OBSERVED]` (§2) | |
| A3 | Dedicated service user with `documents.add_document` permission | ✅ | `[CODE]` (§2) | |
| A4 | Token from configuration, never in repo or logs (C-03) | ✅ | `[CODE]` (§2) | ACL rule |
| A5 | Token forwarding from Gateway not specified; ACL uses service token | ✅ | `[CODE]` (§2) | ACL rule per D-41 |

---

### 3. Flow — Step 1: Upload

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| F1 | Endpoint: `POST /api/documents/post_document/` | ✅ | `[CODE]` (§3) | |
| F2 | Content-Type: `multipart/form-data` | ✅ | `[CODE]` (§3) | |
| F3 | Required field: `document` (file) | ✅ | `[CODE]` (§3) | |
| F4 | MIME validation by monolith | ✅ | `[CODE]` (§3) | |
| F5 | Optional field: `title` (max 127 chars) | ✅ | `[CODE]` (§3) | |
| F6 | Optional field: `created` (datetime, date only used) | ✅ | `[CODE]` (§3) | |
| F7 | Optional fields: `correspondent`, `document_type`, `storage_path`, `tags`, `archive_serial_number`, `custom_fields` | ✅ | `[CODE]` (§3) | Not used by ACL |
| F8 | Response: HTTP 200, body = task UUID string | ✅ | `[OBSERVED]` (§3) | e.g. `"b51b2489-3bde-4494-8898-be360b469d05"` |

---

### 4. Flow — Step 2: Task Polling

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| F9 | Endpoint: `GET /api/tasks/?task_id=<uuid>` | ✅ | `[CODE]` (§3) | |
| F10 | Response: JSON with `count`, `results[]` | ✅ | `[OBSERVED]` (§3) | |
| F11 | `results[0].status` values: `pending`, `started`, `success`, `failure`, `revoked` | ✅ | `[CODE]` (§3) | |
| F12 | `results[0].result_data.document_id` present when `status == "success"` | ✅ | `[OBSERVED]` (§3) | |
| F13 | `results[0].related_document_ids` = list with same id | ✅ | `[OBSERVED]` (§3) | |
| F14 | `result_data.duplicate_of` on duplicate failure | ✅ | `[CODE]` (§3) | |
| F15 | No `related_document` field | ✅ | `[OBSERVED]` (§3) | |

---

### 5. Flow — Step 3: PATCH Content

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| F16 | Endpoint: `PATCH /api/documents/{id}/` | ✅ | `[CODE]` (§3) | |
| F17 | Body: `{"content": "<OCR text>"}` | ✅ | `[CODE]` (§3) | |
| F18 | Response: HTTP 200 with updated document | ✅ | `[OBSERVED]` (§3) | |

---

### 5. Mapping from OCR Result

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| M1 | `text` → Step 3, `content` (always sent) | ✅ | `[OBSERVED]` (§4) | |
| M2 | date (if OCR returns) → Step 1, `created` | ✅ | `[OBSERVED]` (§4) | |
| M3 | title / file name → Step 1, `title` | ✅ | `[OBSERVED]` (§4) | |
| M4 | PDF/A with text layer → Step 1, `document` | ✅ | `[OBSERVED]` (§4) | Monolith skips own OCR |
| M5 | Original file (no text layer) → Step 1, `document` | ✅ | `[OBSERVED]` (§4) | Monolith runs own OCR + archive |
| M6 | page count → **not sent**; monolith computes | ✅ | `[OBSERVED]` (§4) | |
| M7 | thumbnail → **not sent**; monolith generates own WebP | ✅ | `[CODE]` (§4) | |

---

### 6. ACL Rules

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| R1 | Upload file: PDF/A with text layer when OCR service delivers one; else original | ✅ | `[OBSERVED]` (§5) | Avoids monolith's own OCR |
| R2 | Step 3 only after Step 2 reports `success` | ✅ | `[CODE]` (§5) | Never PATCH earlier |
| R3 | Step 3 **always sent** (content = OCR service's text) | ✅ | `[CODE]` (§5) | Safeguard / overwrite |
| R4 | At most one upload per job: ACL stores `documentId` ↔ monolith `document_id` (D-34) | ✅ | `[CODE]` (§5) | No idempotency key in `post_document` |
| R5 | Duplicates: default creates another doc; only `CONSUMER_DELETE_DUPLICATES=true` rejects with `duplicate_of` | ✅ | `[CODE]` (§5) | ACL must not rely on monolith |
| R6 | Polling: HTTP with back-off and overall timeout, both configurable | ✅ | `[CODE]` (§5) | Empty `results` = retry |
| R7 | Failure: `status == "failure"` / timeout / HTTP error → mark failed, no persistence claim without successful PATCH | ✅ | `[CODE]` (§5) | |
| R8 | Error mapping to FE codes not defined by MON-API; agent asks if T-070 needs uncovered code | ✅ | `[CODE]` (§5) | |
| R9 | No secrets in logs, error bodies, or repository | ✅ | `[CODE]` (§5) | C-03 |

---

### 6. What the Monolith Still Does

| ID | Requirement | Verified | Evidence | Notes |
|----|-------------|----------|----------|-------|
| M8 | `post_document` queues consume task (stores file, thumbnail, archive, runs own OCR if no text layer) | ✅ | `[CODE]` (§6) | |
| M9 | Hard task limit: 1800 s | ✅ | `[CODE]` (§6) | |
| M9a | PDF with text layer: 1.2 s, no archive, content = PDF text | ✅ | `[OBSERVED]` (§6) | |
| M9b | Image-only PDF: 3.57 s, archive generated, monolith OCR text | ✅ | `[OBSERVED]` (§6) | |
| M9c | PDF/A with text layer (ocrmypdf): 0.86 s, no archive, layer's text | ✅ | `[OBSERVED]` (§6) | |

---

### 7. Not Verified (Requires Further Testing)

| ID | Item | Status |
|----|------|--------|
| NV1 | PDF/A produced by project's own OCR service (Tesseract + PDFtoImage): test needed to confirm text layer and monolith skips OCR | **Not Verified** |
| NV2 | Large and multi-page files (effective upload limit; code reports not determinable) | **Not Verified** |
| NV3 | Duplicate upload behaviour, and `content` on reprocess from inside monolith | **Not Verified** |
| NV4 | Target deployment behaviour (reverse proxy limits, worker concurrency) | **Not Verified** |
| NV5 | Whether monolith keeps only text or also the file (D-39 point 5, to be confirmed by user) | **Not Verified** |

---

### 8. Contract Completeness for Implementation

| ID | Required for T-070 | Status |
|----|-------------------|--------|
| I1 | Upload endpoint, fields, response format | ✅ Verified |
| I2 | Task polling endpoint, response format, statuses | ✅ Verified |
| I3 | PATCH content endpoint, body format, response | ✅ Verified |
| I3 | Mapping from `OcrResult` fields to delivery | ✅ Verified |
| I4 | ACL rules (file selection, order, upload once, polling, failure) | ✅ Verified |
| I5 | Authentication method | ✅ Verified |
| I6 | Error handling / mapping (partial) | ⚠️ **Partial** — FE code mapping not in MON-API (refers to D-36/D-42) |
| I7 | Version / versioning of the contract | ❌ **Not specified** in MON-API |

---

## Approval

The above checklist has been reviewed against `docs/monolith-ingestion-contract.md` (MON-API) with evidence tags `[OBSERVED]` and `[CODE]`.

**Status: APPROVED** — The contract is sufficiently complete for T-070 (translation) and T-071 (adapter) implementation. Items marked **Not Verified** in §7 are acknowledged as future test work. Item I7 (contract versioning) is noted as a gap but does not block implementation.

**Approved by:** User (per D-44)
**Date:** 2026-11-01

---

*End of checklist*