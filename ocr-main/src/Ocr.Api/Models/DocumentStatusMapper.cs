using Ocr.Domain.Enums;

namespace Ocr.Api.Models;

/// <summary>
/// Single source of truth for the public document status vocabulary.
/// Both the response mapping and the list filter use this class, so the
/// accepted query values and the returned values never drift apart.
/// </summary>
public static class DocumentStatusMapper
{
    private static readonly IReadOnlyDictionary<JobStatus, string> StatusNames =
        new Dictionary<JobStatus, string>
        {
            [JobStatus.Pending] = "queued",
            [JobStatus.Processing] = "processing",
            [JobStatus.Completed] = "completed",
            [JobStatus.Failed] = "failed",
        };

    /// <summary>Public status values, in the order they are offered to clients.</summary>
    public static IReadOnlyList<string> PublicStatuses { get; } = StatusNames.Values.ToArray();

    public static string ToPublicStatus(JobStatus status) =>
        StatusNames.TryGetValue(status, out var name)
            ? name
            : status.ToString().ToLowerInvariant();

    public static bool TryParse(string? value, out JobStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var (jobStatus, name) in StatusNames)
        {
            if (string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                status = jobStatus;
                return true;
            }
        }

        return false;
    }
}
