namespace AIVoiceCable.Models;

public sealed class ReplyHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Text { get; set; } = "";
    public string VoiceProfileId { get; set; } = "";
    public string VoiceProfileName { get; set; } = "";
    public string Model { get; set; } = "";
    public string? AudioPath { get; set; }

    public string Summary => Text.Length <= 80 ? Text : Text[..80] + "...";
}
