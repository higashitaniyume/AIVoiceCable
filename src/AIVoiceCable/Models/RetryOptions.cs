namespace AIVoiceCable.Models;

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(700);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(8);
}
