namespace AIVoiceCable.Models;

public sealed class FishAudioConfig
{
    public const string PreferredModel = "s2.1-pro-free";
    public const string FallbackModel = "s2.1-pro";

    public string BaseUrl { get; set; } = "https://api.fish.audio/";
    public string DefaultModel { get; set; } = PreferredModel;
    public string FallbackModelName { get; set; } = FallbackModel;
    public string DefaultVoiceProfileId { get; set; } = "preset-yongchutafei";
    public int RetryCount { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 120;
}
