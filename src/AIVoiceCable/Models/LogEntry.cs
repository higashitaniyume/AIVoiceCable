namespace AIVoiceCable.Models;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }

    public string Display => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}
