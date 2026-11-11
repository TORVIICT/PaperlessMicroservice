namespace OcrAcl.Infrastructure.Monolith.Dtos;

using System.Text.Json.Serialization;

public sealed class MonolithTasksResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("results")]
    public List<MonolithTaskResultDto>? Results { get; init; }
}

public sealed class MonolithTaskResultDto
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; init; } = string.Empty;

    [JsonPropertyName("task_type")]
    public string TaskType { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("date_created")]
    public DateTimeOffset DateCreated { get; init; }

    [JsonPropertyName("date_started")]
    public DateTimeOffset? DateStarted { get; init; }

    [JsonPropertyName("date_done")]
    public DateTimeOffset? DateDone { get; init; }

    [JsonPropertyName("duration_seconds")]
    public double? DurationSeconds { get; init; }

    [JsonPropertyName("result_data")]
    public MonolithTaskResultData? ResultData { get; init; }

    [JsonPropertyName("related_document_ids")]
    public int[]? RelatedDocumentIds { get; init; }
}

public sealed class MonolithTaskResultData
{
    [JsonPropertyName("document_id")]
    public int? DocumentId { get; init; }

    [JsonPropertyName("duplicate_of")]
    public int? DuplicateOf { get; init; }

    [JsonPropertyName("duplicate_in_trash")]
    public bool DuplicateInTrash { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
