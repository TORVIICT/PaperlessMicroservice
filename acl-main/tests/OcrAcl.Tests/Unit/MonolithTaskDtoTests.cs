using System.Text.Json;
using OcrAcl.Infrastructure.Monolith.Dtos;

namespace OcrAcl.Tests.Unit;

public sealed class MonolithTaskDtoTests
{
    [Fact]
    public void PendingTask_AllowsNullDuration()
    {
        const string json = """
            {
              "count": 1,
              "results": [
                {
                  "task_id": "task-1",
                  "status": "pending",
                  "date_created": "2026-11-01T00:00:00Z",
                  "duration_seconds": null,
                  "result_data": null
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<MonolithTasksResponse>(json);

        Assert.NotNull(response);
        Assert.Null(response.Results![0].DurationSeconds);
    }
}
