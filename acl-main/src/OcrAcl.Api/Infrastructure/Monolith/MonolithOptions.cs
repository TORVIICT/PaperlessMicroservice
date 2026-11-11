namespace OcrAcl.Infrastructure.Monolith;

public sealed class MonolithOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan InitialWait { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxPollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxPollAttempts { get; set; } = 360;
}