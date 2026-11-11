# Technical Decisions — OCR ACL PoC

> Phase 2 output (first version, completed during Phase 3). Every decision `D-xx` that the contract does not dictate is recorded here (M-03, T-09).
> The contract itself is `PoC/docs/contract-analysis.md` (`CT-xx`, `A-xx`).

## 1. SDK, runtime and target framework

| Item | Value | Source |
|------|-------|--------|
| .NET SDK | `10.0.301` (`dotnet --version`, 2026-10-19) | verified on this machine |
| Installed SDKs | `10.0.301 [/home/steven/dotnet/sdk]` (`dotnet --list-sdks`) | verified; only stable SDKs, no preview |
| Target framework | `net10.0` | T-02: highest stable framework supported by the installed SDK |
| `PoC/global.json` | pins `10.0.301`, `"allowPrerelease": false` | T-02 |
| Language | C# (latest supported by the SDK), nullable reference types enabled | T-01, T-04 |
| API style | Controllers with `[ApiController]` and attribute routing | T-03, D-01 |

- **D-01** **Controllers, not Minimal APIs.** T-03 makes controllers the default; the contract has two simple endpoints and the PoC needs the `[ApiController]` conventions plus a controller class per endpoint for readability (M-03). Minimal APIs would not remove any complexity here.
- **D-02** **No OpenAPI/Swagger package and no `Microsoft.AspNetCore.OpenApi` reference.** The package is optional in T-11 and a Swagger UI is forbidden by T-30; the contract document is the description of the API (M-03).

## 2. Packages

Only packages from the T-11 allowed list are used (C-07). Resolved versions are recorded in Phase 3 (T-13).

| Project | Package | Purpose | Resolved version |
|---------|---------|---------|------------------|
| Tests | `Microsoft.NET.Test.Sdk` | test host | `17.14.1` |
| Tests | `xunit` | test framework (v2) | `2.9.3` |
| Tests | `xunit.runner.visualstudio` | test runner | `3.1.4` |
| Tests | `Microsoft.AspNetCore.Mvc.Testing` | in-process HTTP pipeline tests | `10.0.13` |
| Tests | `coverlet.collector` | optional code coverage (kept because the template adds it and it is allowed) | `6.0.4` |
| Source | — | no source package is needed | — |

Resolved versions verified on 2026-10-19 from `tests/OcrAcl.Tests/OcrAcl.Tests.csproj` (T-13). The test project is the `dotnet new xunit` template (xUnit v2, T-12); no package outside the T-11 list was added, and no package was added to the source project.

- **D-03** **No mocking or assertion library** (T-12): all fakes are hand-written (`Fixtures/Fakes.cs`) and assertions use xUnit only.
- **D-04** **No `TimeProvider` is registered and no `FixedTimeProvider` fixture is created.** T-29 allows registering `TimeProvider.System` "only if the code uses a clock"; the contract has no timestamp, no expiry and no scheduling, and T-18 forbids the clock in the response path. A `FixedTimeProvider` fixture would therefore be dead code (M-03). If a later feature needs a timestamp, the fixture is created together with the first use.

## 3. Error handling decisions (T-26)

The contract fixes the error body completely (CT-12…CT-18). The framework's own responses (automatic 400 from `[ApiController]`, malformed JSON, wrong content type, unknown route, wrong method, `ProblemDetails`, developer exception page) are **not** the contract, so each one is replaced by an explicit decision. Every decision below is exercised by at least one `VAL`/`ERR`/`INT` test case.

| ID | Situation | Behaviour | Replaces |
|----|-----------|-----------|----------|
| D-05 | Missing required field, `null`, wrong type, out-of-range, bad format, invalid Base64 | `400` + `error.code = "invalid_request"` + one `details` entry per invalid field | `[ApiController]` automatic 400 `ProblemDetails` |
| D-06 | Malformed JSON (for example `{`) | `400` + `"invalid_request"` + `details = []`, unless the JSON error carries a field path, in which case one `details` entry with that field | MVC JSON binding error (400 `ProblemDetails` with .NET type names) |
| D-07 | Empty body (content length 0, or no JSON tokens) | `400` + `"invalid_request"` + `details = []` | MVC JSON binding error |
| D-08 | Request `Content-Type` is not `application/json`, or is missing | `415` + `"unsupported_media_type"` + `details = []` | MVC `UnsupportedContentType` / 415 with an empty or non-contract body |
| D-09 | Unknown route | `404` + `"not_found"` + `details = []` | framework 404 (empty body, or the default status-code-pages `text/plain` body) |
| D-10 | Method not allowed on a known route | `405` + `"method_not_allowed"` + `details = []` | framework 405 (empty body) |
| D-11 | Any unhandled exception | `500` + `"internal_error"` + `details = []` + a **fixed** message (`"An unexpected error occurred."`), identical in every environment | developer exception page (`Development`), default 500 body |

- **D-12** **One error-handling component.** `Api/Middleware/ContractErrorHandlerMiddleware` is registered **first** in the pipeline (T-26): it (a) catches every unhandled exception → D-11, (b) rewrites the empty framework responses for `400`/`404`/`405`/`415` → D-06…D-10, (c) never touches a response that already has a body (the controller wrote the contract error itself). It does not inspect the environment, so its behaviour is identical in `Development` and in `Production` (T-26).
- **D-13** **Status-code pages are disabled** inside the error middleware (`IStatusCodePagesFeature.Enabled = false`) so the framework cannot overwrite the contract body with its default `text/plain` `404 Not Found` text. Without this, the default `WebApplication` status-code-pages middleware would corrupt the 404/405 responses.
- **D-14** **The body is read and validated in the `Api` layer**, not by MVC model binding: the action calls `Request.ReadFromJsonAsync<T>` inside a `try/catch (JsonException)` and validates the DTO with a small hand-written validator. Reason: this is the only way to guarantee the exact error shape of the contract (field-level `details`, no .NET type names in the message) without fighting the framework's binding messages. `[ApiController]` stays (T-03) but with no action parameters, so the automatic 400 never fires. Validation lives in `Api` because T-05 puts "validation of the external request" there; `Application` stays HTTP-free.
- **D-15** **JSON naming is the ASP.NET Core default (`camelCase`, case-insensitive on read)**; the contract requires camelCase (CT-20, A-01). No custom `JsonSerializerOptions` are configured.
- **D-16** **Unknown JSON properties in the request body are ignored** (System.Text.Json default; resolves finding F-4). The contract enumerates the accepted fields and never says that an extra property is an error, so rejecting it would invent a rule (M-02). Risk: a monolith sending a typo'd field would not notice; recorded as a recommendation for the real ACL.
- **D-17** **`details[].field` for a JSON type error** is taken from the `JsonException.Path` of System.Text.Json (the property that failed), normalized to the bare property name (`documentId`, not `$.documentId`). If the path is empty or refers to no known property, `details` is an empty array, as required by CT-13/A-04.
- **D-18** **`error.message` and `details[].message` wording** is fixed English text defined once in `Api/Contracts/ContractError.cs` (see A-02, A-03). Tests assert the shape, the `code`, the `field` and the absence of internal details — never the wording as a contract requirement.
- **D-19** **No application-level result type is needed in `Application`.** Validation is done in `Api` (D-14), so the only failure that can reach `Application` is an unexpected one, which crosses the boundary as an exception and becomes a `500` in `Api` (D-11, T-26 first bullet).

## 4. Simulation decisions (T-18, M-05)

- **D-20** **The simulated OCR is a pure function of the canonical request content.** Canonical input = `documentId`, `fileName`, `mimeType`, `language` (after defaulting, D-22) and `contentBase64`, joined with `"\n"` (never `Environment.NewLine`). The digest is **SHA-256 over the UTF-8 bytes of the canonical string**, rendered as lowercase hex. `string.GetHashCode()`, `HashCode.Combine` and `object.GetHashCode()` are forbidden (T-18).
- **D-21** **Derived values** (all deterministic, `CultureInfo.InvariantCulture`, no clock):
  - `pageCount = 1 + decodedByteLength / 1024` → always ≥ 1 (CT-09).
  - `confidence = Math.Round(0.50 + (hash[0] / 255.0) * 0.49, 2)` → always inside [0.0, 1.0] (CT-09).
  - `text` = eight words taken from a **fixed** list of 16 lowercase words, the word index of position *i* being the *i*-th byte of the digest modulo 16, joined with single spaces. The text depends only on the content, never on the clock, the machine, the culture or the call order.
- **D-22** **`language` defaults to `eng` when the property is absent** (CT-07). The default is applied in the `Api` translator, so `Application` and `Infrastructure` always receive an explicit language.
- **D-23** **The internal model is visibly marked as simulated** (T-18): the internal `OcrResult` carries `EngineDescriptor = "SIMULATED-OCR"` (a constant of `Infrastructure`). The external response is built field by field from the contract (CT-09) and never contains `EngineDescriptor`, the constant `SIMULATED-OCR`, the word `Simulated`, the namespace `OcrAcl` or any internal property name (M-04, CT-18). `ISO` test cases prove it.
- **D-24** **No `Random`, no `Guid.NewGuid()`, no `DateTime.Now`/`UtcNow`, no `Task.Delay`, no `Thread.Sleep`** anywhere in `PoC/src` (the determinism scan of the Phase 3 exit checklist must print nothing).
- **D-25** **The decoded document bytes are not stored**: the engine decodes `contentBase64` only to compute the length used for `pageCount` and validates it. There is no persistence of any kind (T-22).

## 5. Asynchrony, concurrency and cancellation (T-17, T-23)

- **D-26** The contract is synchronous (CT-01), so **no job store, no state machine and no in-memory queue** exist. `ASY` cases cover cancellation and concurrency only (checklist item 15); checklist item 16 is "not applicable" in the matrix with the reason taken from the contract.
- **D-27** Every asynchronous method has the `Async` suffix, accepts a `CancellationToken` and propagates it to the port (T-17). The controller passes `HttpContext.RequestAborted`; the service passes it to `IOcrEngine.RecognizeAsync`; the simulated engine honours it before doing the work.
- **D-28** The HTTP pipeline is safe for concurrent requests: the engine is stateless and pure (no shared mutable state), so `Task.WhenAll` over different documents returns each document's own result (TS-08, ASY case).

## 6. Test design decisions

- **D-29** `Fixtures/OcrAclFactory` accepts an optional `Action<IServiceCollection>` (runs in `ConfigureTestServices`) and an optional environment name (TS-23). Tests that override a port create their own factory instance (`using var factory = ...`) so every application starts empty (TS-08).
- **D-30** `Fixtures/ContractSamples.cs` holds the literal samples of `CONTRATO_OCR.md` (valid request, valid responses, error body samples) as constant strings, each with a comment citing its `CT-xx` and line numbers. Synthetic data only.
- **D-31** Fakes (`Fixtures/Fakes.cs`): `RecordingOcrEngine` (captures the internal request so translators can be asserted against literals), `FaultyOcrEngine` (throws `InvalidOperationException("INTERNAL-MARKER-7f3a")`), `BlockingOcrEngine` (signals start with a `TaskCompletionSource`, then waits on the `CancellationToken`). No mocking library (D-03).
- **D-32** JSON is compared by parsing (`JsonDocument`) and asserting the exact set of property names, the `JsonValueKind` and the literal values (TS-24). `Assert.Contains` is never used to prove a contract shape.
- **D-33** Expected values never come from the implementation (TS-02): contract-fixed values (`documentId` echo, `status`, `language`, error `code`s, field names) are written as literals with their `CT-xx`; simulated values (`text`, `pageCount`, `confidence`) are asserted through their contract-guaranteed properties (presence, type, range) and through equality between repeated calls (D-20…D-22), never as constants copied from the code.
- **D-34** The architecture tests (`ARC`) implement the four checks of T-27: reflection on the compiled assembly, source scan of the layer folders, namespace/folder agreement and a non-vacuous guard, plus a self-test of the source-scan helper against a deliberately violating synthetic string.

## 7. Project and process decisions

- **D-35** The solution is `PoC/OcrAcl.sln` with exactly two projects (T-10): `src/OcrAcl.Api` (root namespace `OcrAcl`) and `tests/OcrAcl.Tests` (root namespace `OcrAcl.Tests`). `<RootNamespace>` is set explicitly so the folders become `OcrAcl.Application` / `OcrAcl.Infrastructure` / `OcrAcl.Api` (T-10, T-27).
- **D-36** `Program.cs` ends with `public partial class Program { }` (T-29) and registers only the services the PoC uses: controllers, the `IOcrEngine` port with the simulated engine, and `ILogger` (built in). No `TimeProvider` (D-04), no HTTP client, no health-check package (the health endpoint is the contract's own `GET /api/ocr/health`, CT-11).
- **D-37** Logging uses the built-in `ILogger` only (T-21). The error middleware logs the exception (identifier and outcome); it never logs request content, the Base64 document or the recognized text.
- **D-38** Template leftovers removed in Phase 3 (T-30): `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs`, the sample `.http` file and the sample test `UnitTest1.cs` were deleted; `UseHttpsRedirection`, HSTS, CORS, authentication and Swagger UI were never added (`--no-https --no-openapi`); `launchSettings.json` was kept and its `applicationUrl` changed from `http://localhost:5149` to `http://127.0.0.1:5149` (T-30, no secrets). `appsettings.json` and `appsettings.Development.json` were kept unchanged (no secrets, T-20).
- **D-39** The solution file emitted by the installed SDK is `OcrAcl.slnx` (the XML solution format) because `dotnet new sln` on SDK 10.0.301 produces `.slnx` by default; every rule that mentions `OcrAcl.sln` (T-10, T-27, workflow structure check) is satisfied by `OcrAcl.slnx`, and the architecture test looks for either name..
