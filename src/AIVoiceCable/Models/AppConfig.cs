namespace AIVoiceCable.Models;

public sealed class AppConfig
{
    public string AppName { get; set; } = "AIVoiceCable";
    public FishAudioConfig FishAudio { get; set; } = new();
    public AssemblyAiConfig AssemblyAi { get; set; } = new();
    public List<LlmProviderConfig> LlmProviders { get; set; } = [LlmProviderConfig.CreateDeepSeekDefault()];
    public string DefaultLlmProviderId { get; set; } = "deepseek";
    public AudioSettingsConfig Audio { get; set; } = new();
    public FullAiReplyConfig FullAiReply { get; set; } = new();
}

public sealed class AudioSettingsConfig
{
    public string? TtsOutputDeviceId { get; set; }
    public string? MonitorDeviceId { get; set; }
    public string? CaptureRenderDeviceId { get; set; }
    public bool MonitorLocally { get; set; } = true;
}

public sealed class FullAiReplyConfig
{
    public string SystemPrompt { get; set; } = "你是一个简洁、自然的语音助手。回复要适合直接朗读，避免冗长。";
    public bool SaveContext { get; set; } = true;
    public int ContextTurns { get; set; } = 8;
    public bool PauseAsrWhilePlaying { get; set; } = true;
}
