namespace OcrAcl.Api.Contracts;

public sealed record OcrCompletionRequest(Guid DocumentId, string CorrelationId);
