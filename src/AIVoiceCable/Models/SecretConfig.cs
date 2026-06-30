namespace AIVoiceCable.Models;

public sealed class SecretConfig
{
    public string? FishAudioApiKey { get; set; }
    public string? AssemblyAiApiKey { get; set; }
    public Dictionary<string, string> LlmApiKeys { get; set; } = new();
}
