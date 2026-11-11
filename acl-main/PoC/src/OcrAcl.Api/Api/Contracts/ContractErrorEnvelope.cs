namespace OcrAcl.Api.Contracts;

public sealed class ContractErrorEnvelope
{
    public ContractError Error { get; init; } = new();
}

public sealed class ContractError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<ContractErrorDetail> Details { get; init; } = Array.Empty<ContractErrorDetail>();
}

public sealed class ContractErrorDetail
{
    public string Field { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
