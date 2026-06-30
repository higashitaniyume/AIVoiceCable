namespace AIVoiceCable.Models;

public sealed class LlmProviderConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "OpenAI-compatible";
    public string BaseUrl { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-v4-flash";
    public string SystemPrompt { get; set; } = "你是一个简洁、自然的语音助手。";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 600;
    public double TopP { get; set; } = 0.95;
    public int RetryCount { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 60;
    public bool IsDefault { get; set; }

    public override string ToString() => Name;

    public static LlmProviderConfig CreateDeepSeekDefault()
    {
        return new LlmProviderConfig
        {
            Id = "deepseek",
            Name = "DeepSeek",
            BaseUrl = "https://api.deepseek.com/",
            Model = "deepseek-v4-flash",
            SystemPrompt = "你是一个简洁、自然的语音助手。回复要适合直接朗读。",
            Temperature = 0.7,
            MaxTokens = 600,
            TopP = 0.95,
            RetryCount = 5,
            TimeoutSeconds = 60,
            IsDefault = true
        };
    }
}
