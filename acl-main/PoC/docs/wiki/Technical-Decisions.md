# Technical Decisions

This page lists every decision taken in the PoC. The source of truth is `PoC/docs/technical-decisions.md`; this page does not add or invent any decision. The contract itself is in `PoC/docs/contract-analysis.md`.

## SDK, runtime and target framework

[VERIFIED] The values below were verified on this machine on 2026-10-19 with `dotnet --version` (→ `10.0.301`) and `dotnet --list-sdks` (→ `10.0.301 [/home/steven/dotnet/sdk]`).

| Item | Value |
|------|-------|
| .NET SDK | `10.0.301` (`dotnet --version`) |
| Installed SDKs | `10.0.301 [/home/steven/dotnet/sdk]` (`dotnet --list-sdks`) |
| Target framework | `net10.0` |
| `PoC/global.json` | pins `10.0.301`, `"allowPrerelease": false` |
| Language | C# (latest supported by the SDK), nullable reference types enabled |
| API style | Controllers with `[ApiController]` and attribute routing |

## Package versions

[VERIFIED] The versions below are the resolved versions read from `PoC/tests/OcrAcl.Tests/OcrAcl.Tests.csproj` on 2026-10-19.

| Project | Package | Purpose | Resolved version |
|---------|---------|---------|------------------|
| Tests | `Microsoft.NET.Test.Sdk` | test host | `17.14.1` |
| Tests | `xunit` | test framework (v2) | `2.9.3` |
| Tests | `xunit.runner.visualstudio` | test runner | `3.1.4` |
| Tests | `Microsoft.AspNetCore.Mvc.Testing` | in-process HTTP pipeline tests | `10.0.13` |
| Tests | `coverlet.collector` | optional code coverage (kept because the template adds it and it is allowed) | `6.0.4` |
| Source | — | no source package is needed | — |

## Decisions

### Project and API style

- `[DECISION]` **D-01 Controllers, not Minimal APIs.** Controllers are the default; the contract has two simple endpoints and the PoC needs the `[ApiController]` conventions plus a controller class per endpoint for readability. Minimal APIs would not remove any complexity here. Impact: `OcrController` (Api).
- `[DECISION]` **D-02 No OpenAPI/Swagger package and no `Microsoft.AspNetCore.OpenApi` reference.** A Swagger UI is forbidden and the contract document is the description of the API. Impact: no OpenAPI package in either `.csproj`.
- `[DECISION]` **D-03 No mocking or assertion library.** All fakes are hand-written (`PoC/tests/OcrAcl.Tests/Fixtures/Fakes.cs`) and assertions use xUnit only. Impact: no extra package.
- `[DECISION]` **D-04 No `TimeProvider` is registered and no `FixedTimeProvider` fixture is created.** The contract has no timestamp, no expiry and no scheduling, and the clock is forbidden in the response path, so a clock fixture would be dead code. Impact: `Program.cs` registers no clock.

### Error handling

- `[DECISION]` **D-05** Missing required field, `null`, wrong type, out-of-range, bad format or invalid Base64 → `400` with `error.code = "invalid_request"` and one `details` entry per invalid field. Replaces the `[ApiController]` automatic `400 ProblemDetails`. Evidence: TC-VAL-001…TC-VAL-026.
- `[DECISION]` **D-06** Malformed JSON → `400` with `"invalid_request"` and `details = []`, unless the JSON error carries a field path, in which case one `details` entry with that field. Replaces the MVC JSON binding error. **D-17** defines how that field is derived. Evidence: TC-VAL-024.
- `[DECISION]` **D-07** Empty body (content length 0, or no JSON tokens) → `400` with `"invalid_request"` and `details = []`. Replaces the MVC JSON binding error. Evidence: TC-VAL-025.
- `[DECISION]` **D-08** Request `Content-Type` is not `application/json`, or is missing → `415` with `"unsupported_media_type"` and `details = []`. Replaces the MVC `UnsupportedContentType` response. Evidence: TC-INT-005, TC-INT-006.
- `[DECISION]` **D-09** Unknown route → `404` with `"not_found"` and `details = []`. Replaces the framework 404. Evidence: TC-INT-003.
- `[DECISION]` **D-10** Method not allowed on a known route → `405` with `"method_not_allowed"` and `details = []`. Replaces the framework 405. Evidence: TC-INT-004.
- `[DECISION]` **D-11** Any unhandled exception → `500` with `"internal_error"`, `details = []` and a fixed message (`"An unexpected error occurred."`), identical in every environment. Replaces the developer exception page and the default 500 body. Evidence: TC-ERR-001…TC-ERR-005.
- `[DECISION]` **D-12 One error-handling component.** `Api/Middleware/ContractErrorHandlerMiddleware` is registered first: it catches unhandled exceptions (D-11), rewrites the empty framework responses for `400`/`404`/`405`/`415` (D-06…D-10), and never touches a response that already has a body. It does not inspect the environment, so its behaviour is identical in `Development` and `Production`. Impact: `Program.cs`, `ContractErrorHandlerMiddleware`. Evidence: TC-ERR-002, TC-INT-003…TC-INT-006.
- `[DECISION]` **D-13 Status-code pages are disabled** inside the error middleware (`IStatusCodePagesFeature.Enabled = false`) so the framework cannot overwrite the contract body with its default `text/plain` text. Impact: `ContractErrorHandlerMiddleware`.
- `[DECISION]` **D-14 The body is read and validated in `Api`**, not by MVC model binding. The action reads the body with `JsonDocument.ParseAsync` inside a `try/catch (JsonException)` and validates the DTO with a hand-written validator, so the exact contract error shape is produced. `[ApiController]` stays, but with no action parameters, so the automatic 400 never fires. Impact: `OcrController`, `OcrRequestTranslator`. Evidence: TC-VAL-001…TC-VAL-026.
- `[DECISION]` **D-15 JSON naming is the ASP.NET Core default (camelCase, case-insensitive on read)**, matching CT-20/A-01. No custom serializer options. Evidence: TC-CON-003, TC-CON-005.
- `[DECISION]` **D-16 Unknown JSON properties in the request body are ignored** (System.Text.Json default; resolves finding F-4). The contract enumerates the accepted fields and never says an extra property is an error. Impact: a monolith sending a typo'd field would not notice; recorded as a recommendation. Evidence: TC-VAL-027.
- `[DECISION]` **D-17 `details[].field` for a JSON type error** is taken from the `JsonException.Path` and normalized to the bare property name (`documentId`, not `$.documentId`). If the path is empty or unknown, `details` is empty (CT-13, A-04). Evidence: TC-VAL-010…TC-VAL-014.
- `[DECISION]` **D-18 `error.message` and `details[].message` wording** is fixed English text defined once in `Api/Contracts/ContractErrorCodes.cs` (A-02, A-03). Tests assert shape, `code`, `field` and absence of internal details, never the wording. Evidence: TC-ERR-004, TC-ERR-005.
- `[DECISION]` **D-19 No application-level result type is needed in `Application`.** Validation is done in `Api` (D-14), so the only failure that can reach `Application` is an unexpected one, which crosses the boundary as an exception and becomes a `500` in `Api`.

### Simulation

- `[DECISION]` **D-20 The simulated OCR is a pure function of the canonical request content.** Canonical input = `documentId`, `fileName`, `mimeType`, `language` (after defaulting, D-22) and `contentBase64`, joined with `"\n"`. The digest is SHA-256 over the UTF-8 bytes of that string, as lowercase hex. `GetHashCode` and friends are forbidden. Impact: `SimulatedOcrEngine`. Evidence: TC-UNIT-007, TC-UNIT-008, TC-DET-001…TC-DET-004.
- `[DECISION]` **D-21 Derived values** (deterministic, invariant culture, no clock): `pageCount = 1 + decodedByteLength / 1024`; `confidence = Math.Round(0.50 + (hash[0] / 255.0) * 0.49, 2)`; `text` = eight words from a fixed 16-word list, the index of position *i* being the *i*-th digest byte modulo 16. Evidence: TC-UNIT-009, TC-UNIT-010, TC-DET-004.
- `[DECISION]` **D-22 `language` defaults to `eng` when the property is absent** (CT-07), applied in the `Api` translator. Evidence: TC-UNIT-002.
- `[DECISION]` **D-23 The internal model is visibly marked as simulated**: `OcrResult` carries `EngineDescriptor = "SIMULATED-OCR"`. The external response is built field by field and never contains it (M-04, CT-18). Evidence: TC-UNIT-006, TC-UNIT-011, TC-ISO-001…TC-ISO-005.
- `[DECISION]` **D-24 No `Random`, no `Guid.NewGuid()`, no `DateTime.Now`/`UtcNow`, no `Task.Delay`, no `Thread.Sleep`** anywhere in `PoC/src`. Impact: determinism scan of the Phase 3 checklist prints nothing. Evidence: TC-DET-001…TC-DET-004.
- `[DECISION]` **D-25 The decoded document bytes are not stored**; the engine decodes them only to compute `pageCount`. No persistence of any kind.

### Asynchrony, concurrency and cancellation

- `[DECISION]` **D-26 The contract is synchronous** (CT-01), so no job store, state machine or in-memory queue exists. `ASY` cases cover cancellation and concurrency only; checklist item 16 is "not applicable". Evidence: TC-ASY-001…TC-ASY-003.
- `[DECISION]` **D-27 Every asynchronous method has the `Async` suffix, accepts a `CancellationToken` and propagates it to the port.** The controller passes `HttpContext.RequestAborted`. Evidence: TC-UNIT-012, TC-UNIT-014, TC-ASY-001, TC-ASY-002.
- `[DECISION]` **D-28 The HTTP pipeline is safe for concurrent requests**: the engine is stateless and pure, so `Task.WhenAll` over different documents returns each document's own result. Evidence: TC-ASY-003, TC-DET-003.

### Test design

- `[DECISION]` **D-29 `Fixtures/OcrAclFactory` accepts an optional `Action<IServiceCollection>` and an optional environment name.** Tests that override a port create their own factory instance so every application starts empty. Impact: `PoC/tests/OcrAcl.Tests/Fixtures/OcrAclFactory.cs`.
- `[DECISION]` **D-30 `Fixtures/ContractSamples.cs` holds the literal samples of the contract** as constant strings, each with a comment citing its clause and line numbers. Impact: contract tests.
- `[DECISION]` **D-31 Fakes (`Fixtures/Fakes.cs`)**: `RecordingOcrEngine`, `FaultyOcrEngine` (throws `InvalidOperationException("INTERNAL-MARKER-7f3a")`) and `BlockingOcrEngine` (signals start, then waits on the token). No mocking library (D-03).
- `[DECISION]` **D-32 JSON is compared by parsing** (`JsonDocument`) and asserting the exact set of property names, the `JsonValueKind` and the literal values. `Assert.Contains` is never used to prove a contract shape. Evidence: TC-CON-001…TC-CON-007, TC-ISO-001.
- `[DECISION]` **D-33 Expected values never come from the implementation**: contract-fixed values are literals with their clause; simulated values are asserted through the properties the contract defines and through equality between repeated calls. Impact: all categories.
- `[DECISION]` **D-34 The architecture tests (`ARC`) implement the four checks of T-27**: reflection on the compiled assembly, source scan of the layer folders, namespace/folder agreement and a non-vacuous guard, plus a self-test of the helper against a deliberately violating synthetic string. Evidence: TC-ARC-001…TC-ARC-005.

### Project and process

- `[DECISION]` **D-35 The solution is `PoC/OcrAcl.slnx` with exactly two projects**: `src/OcrAcl.Api` (root namespace `OcrAcl`) and `tests/OcrAcl.Tests` (root namespace `OcrAcl.Tests`). `<RootNamespace>` is set explicitly so the folders become `OcrAcl.Application` / `OcrAcl.Infrastructure` / `OcrAcl.Api`.
- `[DECISION]` **D-36 `Program.cs` ends with `public partial class Program { }`** and registers only controllers, `IOcrEngine` with `SimulatedOcrEngine`, and `OcrDocumentService`. No `TimeProvider` (D-04), no HTTP client, no health-check package (the health endpoint is the contract's own, CT-11).
- `[DECISION]` **D-37 Logging uses the built-in `ILogger` only.** The error middleware logs the exception; it never logs request content, the Base64 document or the recognized text.
- `[DECISION]` **D-38 Template leftovers removed in Phase 3**: `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs`, the sample `.http` file and the sample test `UnitTest1.cs` were deleted; `UseHttpsRedirection`, HSTS, CORS, authentication and Swagger UI were never added; `launchSettings.json` was changed to listen on `http://127.0.0.1:5149`; `appsettings.json` files were kept unchanged (no secrets).
- `[DECISION]` **D-39 The solution file emitted by the installed SDK is `OcrAcl.slnx`** (the XML solution format). Every rule that mentions `OcrAcl.sln` is satisfied by `OcrAcl.slnx`, and the architecture test looks for either name.
