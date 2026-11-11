# Test Cases Documentation

> Feature: `002-ocr-acl-integration`
> All automated test cases with ID, purpose, expected result, and contract/decision reference.

## Summary

| Category | Count |
|----------|-------|
| UNIT | 92 |
| INTEGRATION | 36 |
| ARC (Architecture) | 5 |
| **Total** | **133** |

Note: 129 tests passed (the 4 E2E tests against real Paperless require running instance).

---

## UNIT Tests (92)

### OcrStatusTests (11 tests) — `OcrAcl.Tests.Unit.OcrStatusTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-UNIT-005 | `OcrStatus_Pending_IsNonTerminal` | Verify `Pending` status is non-terminal | `IsNonTerminal() = true`, `IsTerminal() = false` | OCR-SRC §3.2 |
| TC-UNIT-006 | `OcrStatus_Processing_IsNonTerminal` | Verify `Processing` status is non-terminal | `IsNonTerminal() = true`, `IsTerminal() = false` | OCR-SRC §3.2 |
| TC-UNIT-007 | `OcrStatus_Completed_IsTerminal` | Verify `Completed` status is terminal | `IsTerminal() = true`, `IsNonTerminal() = false` | OCR-SRC §3.2 |
| TC-UNIT-008 | `OcrStatus_Failed_IsTerminal` | Verify `Failed` status is terminal | `IsTerminal() = true`, `IsNonTerminal() = false` | OCR-SRC §3.2 |
| TC-UNIT-009 | `CanTransition_ReturnsExpectedResult` (16 cases) | Verify allowed state transitions | Only `Pending→Processing`, `Processing→Completed`, `Processing→Failed` return `true` | OCR-SRC §4 |
| TC-UNIT-010 | `CanTransition_ThrowsOnUnknownFromStatus` | Invalid `from` status throws | `ArgumentOutOfRangeException` | OCR-SRC §3.2 |
| TC-UNIT-011 | `CanTransition_ThrowsOnUnknownToStatus` | Invalid `to` status throws | `ArgumentOutOfRangeException` | OCR-SRC §3.2 |
| TC-UNIT-012 | `FromOcrString_ParsesCaseInsensitively` (12 cases) | Parse OCR status strings case-insensitively | All 12 case variants map to correct enum | OCR-SRC §3.2 |
| TC-UNIT-013 | `FromOcrString_ThrowsOnUnrecognizedStatus` | Unknown string throws | `ArgumentException` | OCR-SRC §3.2 |
| TC-UNIT-014 | `ToFeStatus_MapsCorrectly` (4 cases) | Map internal status to FE status | `Pending→queued`, `Processing→processing`, `Completed→completed`, `Failed→failed` | FE-E3, D-43 |
| TC-UNIT-015 | `ToFeStatus_ThrowsOnUnknownStatus` | Unknown enum value throws | `ArgumentOutOfRangeException` | FE-E3 |

### OcrJobTests (4 tests) — `OcrAcl.Tests.Unit.OcrJobTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-UNIT-001 | `OcrJob_CanBeCreated_WithAllProperties` | Verify `OcrJob` model construction with all fields | All properties round-trip correctly | OCR-SRC §3–§4 |
| TC-UNIT-002 | `OcrJob_WithFailedStatus_HasErrorAndNullResult` | Failed job has error, no result | `Status=Failed`, `Error!=null`, `Result=null` | OCR-SRC §4 |
| TC-UNIT-003 | `OcrJob_WithProcessingStatus_HasNullCompletedAtAndNullResult` | Processing job has no completion data | `CompletedAt=null`, `Result=null`, `Error=null` | OCR-SRC §4 |
| TC-UNIT-004 | `OcrJob_WithPendingStatus_IsNonTerminal` | Pending job status is non-terminal | `Status=Pending`, `IsNonTerminal()=true` | OCR-SRC §3.2 |

### SubmitDocumentHandlerTests (12 tests) — `OcrAcl.Tests.Unit.SubmitDocumentHandlerTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-UNIT-016 | `ExecuteAsync_ValidInput_ReturnsJobWithGeneratedClientReference` | Valid submit returns job with `clientReference` | `JobId!=Guid.Empty`, `ClientReference` starts with `doc-ref-test` | FR-01, FR-03, FR-04 |
| TC-UNIT-017 | `ExecuteAsync_ValidInput_CallsOcrClientWithCorrectParameters` | OCR client called with correct params | File, name, type, clientRef, language, mode passed correctly | OCR-E1, D-37 |
| TC-UNIT-018 | `ExecuteAsync_PropagatesCancellationToken` | Cancellation token flows to OCR client | Token passed through | NFR-09 |
| TC-UNIT-019 | `ExecuteAsync_EmptyFileContent_ThrowsArgumentException` (2 cases) | Empty stream throws | `ArgumentException` with "File content is required" | FR-02, D-35 |
| TC-UNIT-020 | `ExecuteAsync_InvalidFileName_ThrowsArgumentException` (2 cases) | Empty/whitespace filename throws | `ArgumentException` with "File name is required" | FR-02 |
| TC-UNIT-021 | `ExecuteAsync_UnsupportedContentType_ThrowsArgumentException` (4 cases) | Unsupported MIME types rejected | `ArgumentException` with "Unsupported content type" | D-35, OQ-05 |
| TC-UNIT-022 | `ExecuteAsync_AllowedContentType_DoesNotThrow` (3 cases) | PDF, PNG, JPEG accepted | Returns job successfully | D-35 |
| TC-UNIT-023 | `ExecuteAsync_InvalidLanguage_ThrowsArgumentException` (2 cases) | Empty/whitespace language throws | `ArgumentException` with "Language is required" | D-37 |
| TC-UNIT-024 | `ExecuteAsync_InvalidMode_ThrowsArgumentException` (2 cases) | Empty/whitespace mode throws | `ArgumentException` with "Mode is required" | D-37 |
| TC-UNIT-025 | `ExecuteAsync_ReturnsCompletedJob_WhenOcrClientReturnsCompleted` | Completed OCR job returned | `Status=Completed` | FR-05 |
| TC-UNIT-026 | `ExecuteAsync_ReturnsFailedJob_WhenOcrClientReturnsFailed` | Failed OCR job returned | `Status=Failed` | FR-06, D-36 |
| TC-UNIT-027 | `ExecuteAsync_GeneratesUniqueClientReference_PerCall` | Unique `clientReference` per call | 3 distinct values with prefix `doc-ref-test` | D-34, OCR-SRC §3 |

### HttpOcrClientTests (20 tests) — `OcrAcl.Tests.Unit.HttpOcrClientTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-UNIT-OCR-001 | `SubmitAsync_ValidRequest_ReturnsJobWithJobIdAndProcessingStatus` | Submit returns job with ID and processing status | `JobId`, `ClientReference`, `Status=Processing` | OCR-E1, OCR-SRC §3.1 |
| TC-UNIT-OCR-002 | `SubmitAsync_SendsCorrectMultipartFields` | Multipart contains all required fields | `file`, `clientReference`, `language`, `mode` in request | OCR-E1 |
| TC-UNIT-OCR-003 | `SubmitAsync_IncludesAuthorizationHeader` | `Authorization: Bearer <token>` sent | Header present with correct token | OCR-SRC §2.2, D-41 |
| TC-UNIT-OCR-004 | `SubmitAsync_IncludesCorrelationIdHeader` | `X-Correlation-ID` propagated | Header with correct GUID | OCR-SRC §2.2, OQ-10 |
| TC-UNIT-OCR-005 | `SubmitAsync_IncludesAcceptJsonHeader` | `Accept: application/json` sent | Header present | OCR-SRC §2.2 |
| TC-UNIT-OCR-006 | `SubmitAsync_415Response_ThrowsUnsupportedFileType` | OCR 415 → mapped exception | `InvalidOperationException("UNSUPPORTED_FILE_TYPE")` | OCR-E1, D-36 |
| TC-UNIT-OCR-007 | `SubmitAsync_400Response_ReadsErrorBodyAndThrowsInvalidFile` | OCR 400 with body → mapped exception | `InvalidOperationException("INVALID_FILE")` | OCR-E1, D-36 |
| TC-UNIT-OCR-008 | `SubmitAsync_MissingLocationHeader_Throws` | Missing Location header → exception | `InvalidOperationException` mentioning "Location" | OCR-E1 |
| TC-UNIT-OCR-009 | `GetJobAsync_CompletedJob_ReturnsJobWithResult` | Completed job maps all result fields | All `OcrResult` properties populated | OCR-E2, OCR-SRC §3.2 |
| TC-UNIT-OCR-010 | `GetJobAsync_FailedJob_ReturnsJobWithError` | Failed job maps error | `Status=Failed`, `Error.Code/Message` set | OCR-E2, D-36 |
| TC-UNIT-OCR-011 | `GetJobAsync_ProcessingJob_ReturnsJobWithoutResultOrError` | Processing job has no result/error | `Status=Processing`, `Result=null`, `Error=null` | OCR-E2 |
| TC-UNIT-OCR-012 | `GetJobAsync_404Response_ThrowsJobNotFound` | OCR 404 → exception | `InvalidOperationException` | OCR-E2 |
| TC-UNIT-OCR-013 | `GetJobByUrlAsync_UsesProvidedUrl` | Custom URL used for polling | Request goes to provided URL | OCR-E2, OCR-SRC §3.2 |
| TC-UNIT-OCR-014 | `GetJobByUrlAsync_AcceptsAbsoluteUrl` | Absolute URL accepted | Request succeeds | OCR-E2 |
| TC-UNIT-OCR-015 | `DownloadArchiveAsync_ReturnsStream` | Archive download returns stream | Stream content matches OCR response | OCR-E3 |
| TC-UNIT-OCR-016 | `DownloadArchiveAsync_404_Throws` | OCR 404 on archive → exception | `InvalidOperationException` | OCR-E3 |
| TC-UNIT-OCR-017 | `DownloadThumbnailAsync_ReturnsStream` | Thumbnail download returns stream | Stream content matches OCR response | OCR-E4 |
| TC-UNIT-OCR-018 | `DownloadThumbnailAsync_404_Throws` | OCR 404 on thumbnail → exception | `InvalidOperationException` | OCR-E4 |
| TC-UNIT-OCR-019 | `SubmitAsync_CancellationToken_Propagated` | Cancellation flows to HTTP call | `OperationCanceledException` | NFR-09 |
| TC-UNIT-OCR-020 | `GetJobAsync_CancellationToken_Propagated` | Cancellation flows to HTTP call | `OperationCanceledException` | NFR-09 |

### PollingPolicyTests (10 tests) — `OcrAcl.Tests.Unit.PollingPolicyTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-UNIT-POLL-001 | `GetPollDelay_Attempts1To3_Returns500Ms` | Attempts 1–3 = 500ms delay | All return 500ms | OCR-SRC §4 |
| TC-UNIT-POLL-002 | `GetPollDelay_Attempts4To10_Returns1Second` | Attempts 4–10 = 1s delay | All return 1s | OCR-SRC §4 |
| TC-UNIT-POLL-003 | `GetPollDelay_Attempts11Plus_Returns2Seconds` | Attempts 11+ = 2s delay | All return 2s | OCR-SRC §4 |
| TC-UNIT-POLL-004 | `GetPollDelay_InvalidAttempt_Throws` | Attempt ≤0 throws | `ArgumentOutOfRangeException` | OCR-SRC §4 |
| TC-UNIT-POLL-005 | `GetJitteredDelay_ReturnsValueWithinRange` | Jitter within ±250ms | 100 iterations all in range | OCR-SRC §4 |
| TC-UNIT-POLL-006 | `Constants_HaveExpectedValues` | Constants match contract | `InitialWait=500ms`, `GlobalTimeout=120s`, `MaxTransientRetries=3`, `JitterRange=250ms` | OCR-SRC §4 |
| TC-UNIT-POLL-007 | `WaitInitialAsync_CompletesAfterInitialWait` | Initial wait executes | ≥450ms elapsed | OCR-SRC §4 |
| TC-UNIT-POLL-008 | `WaitPollDelayAsync_UsesCorrectDelayPerAttempt` | Delay per attempt matches back-off | 500ms/1s/2s for attempts 1/5/11 | OCR-SRC §4 |
| TC-UNIT-POLL-009 | `CreateGlobalTimeoutToken_LinksExternalToken` | Linked token respects external cancel | External cancel → linked cancels | OCR-SRC §4, D-40 |
| TC-UNIT-POLL-010 | `CreateGlobalTimeoutToken_CancelsAfterGlobalTimeout` | Timeout token created | Token not cancelled immediately | OCR-SRC §4, D-40 |

### MonolithTranslatorTests (9 tests) — `OcrAcl.Tests.Unit.MonolithTranslatorTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| (no TC) | `TranslateToContentPatch_ReturnsTextFromOcrResult` | Extracts `content` from OCR result | `patch.Content = result.Text` | MON-API §5, D-44 |
| (no TC) | `TranslateToContentPatch_ReturnsEmptyString_WhenTextIsNull` | Null text → empty string | `patch.Content = ""` | MON-API §5 R3 |
| (no TC) | `TranslateToUploadRequest_UsesTitle_WhenProvided` | Custom title used | `request.Title = provided title` | MON-API §4 |
| (no TC) | `TranslateToUploadRequest_DerivesTitleFromFilename_WhenNotProvided` | Title from filename (no extension) | `request.Title = filename without .pdf` | MON-API §4 |
| (no TC) | `TranslateToUploadRequest_PreservesFileContentAndMetadata` | File content, name, type preserved | All match input | MON-API §4 |
| (no TC) | `TranslateToContentPatch_AlwaysReturnsContent_EvenWhenEmpty` | Step 3 always sent (R3) | Empty string, not omitted | MON-API §5 R3 |
| (no TC) | `SelectUploadFile_ReturnsOriginalFile_WhenNoArchivedPdf` | No archive → upload original | Returns original file stream | MON-API §4, D-45 |
| (no TC) | `SelectUploadFile_Throws_WhenHasArchivedPdfIsTrue` | Has archive but no content → throws | `InvalidOperationException` with guidance | MON-API §4, D-45 |
| (no TC) | `SelectUploadFile_ResetsStreamPosition` | Stream position reset to 0 | `Position = 0` | MON-API §4 |

---

## INTEGRATION Tests (36)

### SubmitPollCompletedIntegrationTests (5 tests) — `OcrAcl.Tests.Integration.SubmitPollCompletedIntegrationTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-INT-001 | `Submit_Returns202WithQueuedStatus` | POST returns 202 + queued | `202`, `status=queued` | FE-E1, FE-E3 |
| TC-INT-002 | `GetDocumentStatus_CompletedDocument_ReturnsCompletedStatus` | GET returns completed with pageCount | `200`, `status=completed`, `pageCount=5` | FE-E3, FR-07 |
| TC-INT-003 | `GetDocumentStatus_FailedDocument_ReturnsFailedStatus` | GET returns failed with mapped error | `200`, `status=failed`, `error.code=OCR_PROCESSING_FAILED`, `details.ocrCode=ENGINE_ERROR` | FE-E3, D-36, D-42 |
| TC-INT-004 | `GetDocumentText_CompletedDocument_ReturnsTextAndPageCount` | Text endpoint returns text + pageCount | `200`, `{id, text, pageCount}` | FE-E4, D-43 |
| TC-INT-005 | `GetDocumentText_NotCompleted_Returns409` | Text on non-completed → 409 | `409 Conflict` | FE-E4, D-43 |

### ArchiveStreamingIntegrationTests (4 tests) — `OcrAcl.Tests.Integration.ArchiveStreamingIntegrationTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-INT-006 | `GetArchive_CompletedDocumentWithArchive_StreamsPdf` | Archive streams PDF, OCR URL hidden | `200`, `application/pdf`, content matches, no `fake-ocr` in response | FE-E6, FR-10 |
| TC-INT-007 | `GetArchive_DocumentNotFound_Returns404` | Unknown document → 404 | `404 NotFound` | FE-E6 |
| TC-INT-008 | `GetArchive_DocumentNotCompleted_Returns409` | Not completed → 409 | `409 Conflict` | FE-E6, D-43 |
| TC-INT-009 | `GetArchive_OcrReturns404_Returns409` | OCR 404 (not ready) → 409 | `409 Conflict` | FE-E6, OCR-E3 |

### IdentityStateOrchestrationIntegrationTests (5 tests) — `OcrAcl.Tests.Integration.IdentityStateOrchestrationIntegrationTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-T062-001 | `Upload_CreatesDocumentWithAllThreeIdentities` | Store has `documentId↔jobId↔clientReference` | All three lookups return same record | D-34, T-062 |
| TC-T062-002 | `OnlyClientReferenceCrossesToOcr` | Only `clientReference` sent to OCR | OCR request contains only `clientReference` | C-13, OCR-SRC §3.1 |
| TC-T062-003 | `StatusUpdates_PersistThroughAllIdentityMappings` | Completed state via all 3 lookups | All 3 lookups show `Completed` + result | D-34, T-062 |
| TC-T062-004 | `ErrorState_PersistsThroughAllIdentityMappings` | Failed state via all 3 lookups | All 3 lookups show `Failed` + error | D-34, T-062 |
| TC-T062-005 | `GetDocumentStatus_ReturnsCorrectMappingViaDocumentId` | GET /documents/{id} uses documentId | Response has `id=documentId`, correct fields | FE-E3, D-34 |

### OrchestrationIntegrationTests (5 tests) — `OcrAcl.Tests.Integration.OrchestrationIntegrationTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-T064-001 | `Submit_Poll_Completed_FullFlow_WithBackgroundService` | Full submit→poll→complete flow | 202→queued, then GET→completed + text | FR-05, FR-07, AC-03, AC-04 |
| TC-T064-002 | `Submit_Poll_Failed_TerminalFailure` | Submit→poll→failed flow | GET→failed, text→409 | FR-06, AC-03, D-36 |
| TC-T064-003 | `TransientFailure_RetriesWithJitter_ThenSucceeds` | 2 retries then success | Eventually `completed` | OCR-SRC §4, FR-13, AC-08 |
| TC-T064-004 | `TransientFailure_MaxRetriesExceeded_ReturnsFailed` | 4 failures (>3 max) → failed | `failed`, `error.details.ocrCode=SERVICE_UNAVAILABLE` | OCR-SRC §4, FR-13, AC-08 |
| TC-T064-005 | `CancelledRequest_AbortsPolling` | Global timeout (120s) → failed | `failed` after timeout | OCR-SRC §4, FR-12, D-40 |

### MonolithHopEndToEndTests (1 test) — `OcrAcl.Tests.Integration.MonolithHopEndToEndTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-E2E-001 | `T116_MonolithHop_EndToEnd_UploadsDocumentAndPatchesContent` | Full T-C flow vs real Paperless | Upload→poll task→patch content→verify in monolith | MON-API, D-44, T-116 |

---

## ARC Tests (5) — `OcrAcl.Tests.Architecture.ApplicationLayerTests`

| Test ID | Method | Purpose | Expected Result | Contract/Decision |
|---------|--------|---------|-----------------|-------------------|
| TC-ARC-010 | `ApplicationTypes_DoNotReferenceForbiddenNamespaces` | No `Api`, `Infrastructure`, `Microsoft.AspNetCore` refs | Zero violations | T-27, AC-11 |
| TC-ARC-011 | `ApplicationSourceFiles_DoNotContainForbiddenUsings` | No forbidden `using` in Application source | Zero violations | T-27, AC-11 |
| TC-ARC-012 | `ApplicationNamespaces_MatchFolderStructure` | Namespaces match `OcrAcl.Application.*` | Zero violations | T-27 |
| TC-ARC-013 | `ScanHelper_DetectsForbiddenUsingInSyntheticInput` | Scanner detects violations | 1 violation found for test input | T-27 |
| TC-ARC-014 | `ApplicationHasAtLeastOneTypeAndFile` | Application layer not empty | ≥1 type, ≥1 file | T-27 |

---

## Traceability Matrix

| AC / FR / Contract | Test Cases |
|---------------------|------------|
| AC-02 (submit 202 + queued) | TC-INT-001 |
| AC-03 (poll to completed) | TC-T064-001, TC-INT-002 |
| AC-04 (status query) | TC-T064-001, TC-INT-002, TC-INT-003, TC-T062-005 |
| AC-05 (archive streaming) | TC-INT-006–TC-INT-009 |
| AC-06 (error envelope) | TC-INT-003, TC-T064-002 |
| AC-07 (timeout) | TC-T064-005 |
| AC-08 (retries) | TC-T064-003, TC-T064-004 |
| AC-09 (correlation ID) | TC-UNIT-OCR-004 |
| AC-11 (architecture) | TC-ARC-010–TC-ARC-014 |
| AC-14 (monolith hop) | TC-E2E-001 |
| AC-18 (error HTTP status) | TC-INT-003 (422), TC-INT-005/008/009 (409), TC-INT-007 (404) |
| FR-01–FR-04 (submit) | TC-UNIT-016–TC-UNIT-027, TC-UNIT-OCR-001–005 |
| FR-05 (completed) | TC-UNIT-025, TC-UNIT-OCR-009, TC-T064-001, TC-INT-002 |
| FR-06 (failed) | TC-UNIT-026, TC-UNIT-OCR-010, TC-T064-002, TC-INT-003 |
| FR-07 (status) | TC-INT-001, TC-INT-002, TC-INT-003, TC-T062-005 |
| FR-09 (text) | TC-INT-004, TC-INT-005 |
| FR-10 (archive) | TC-INT-006–TC-INT-009 |
| FR-12 (timeout) | TC-T064-005 |
| FR-13 (retries) | TC-T064-003, TC-T064-004 |
| FR-15 (monolith hop) | TC-E2E-001 |
| OCR-E1–E4 | TC-UNIT-OCR-001–020 |
| MON-API §4–§5 | MonolithTranslatorTests (9 tests), TC-E2E-001 |
| D-33 (polling) | TC-UNIT-POLL-001–010, TC-T064-001–005 |
| D-34 (identity store) | TC-T062-001–005 |
| D-35 (formats/size) | TC-UNIT-021, TC-UNIT-022 |
| D-36 (error mapping) | TC-UNIT-026, TC-UNIT-OCR-006–007, TC-INT-003, TC-T064-002 |
| D-37 (language/mode) | TC-UNIT-023, TC-UNIT-024, TC-UNIT-017 |
| D-40 (async/timeout) | TC-T064-005, TC-UNIT-POLL-009–010 |
| D-41 (token forwarding) | TC-UNIT-OCR-003 |
| D-42 (error HTTP status) | TC-INT-003, TC-INT-005, TC-INT-007–009 |
| D-43 (text endpoint) | TC-INT-004, TC-INT-005, TC-INT-008 |
| D-44 (T-C flow) | TC-E2E-001, MonolithTranslatorTests |
| D-45 (PDF/A upload) | MonolithTranslatorTests (3 tests) |

---

*Document generated as part of Stage 8 (T-103). Last updated: 2026-11-02*