namespace AIVoiceCable.Models;

public sealed class AssemblyAiConfig
{
    public string WebSocketEndpoint { get; set; } = "wss://streaming.assemblyai.com/v3/ws";
    public string SpeechModel { get; set; } = "universal-3-5-pro";
    public bool AutoLanguageDetection { get; set; } = true;
    public bool EnableVad { get; set; } = true;
    public int SampleRate { get; set; } = 16000;
    public int RetryCount { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 60;
    public int MinimumSpeechMs { get; set; } = 300;
    public int SilenceEndThresholdMs { get; set; } = 900;
}
