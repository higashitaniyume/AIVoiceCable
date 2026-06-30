using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class LlmService(
    IConfigService configService,
    ISecretService secretService,
    RetryPolicyService retryPolicy,
    ILoggingService logger) : ILlmService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly List<ChatMessage> _context = [];

    public Task<string> TestAsync(CancellationToken cancellationToken)
    {
        return GenerateReplyAsync("请回复：LLM 连接测试成功。", cancellationToken);
    }

    public void ClearContext()
    {
        _context.Clear();
        logger.Info("LLM 上下文已清空");
    }

    public async Task<string> GenerateReplyAsync(string userText, CancellationToken cancellationToken)
    {
        var provider = GetDefaultProvider();
        if (!secretService.Secrets.LlmApiKeys.TryGetValue(provider.Id, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"请先填写 {provider.Name} 的 API Key。");
        }

        logger.Info($"LLM 请求开始：provider={provider.Name}, model={provider.Model}");
        var messages = BuildMessages(provider, userText);
        var options = new RetryOptions { MaxAttempts = provider.RetryCount };

        var result = await retryPolicy.ExecuteAsync(async token =>
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds) };
            var endpoint = new Uri(new Uri(provider.BaseUrl.TrimEnd('/') + "/"), "chat/completions");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = provider.Model,
                messages,
                temperature = provider.Temperature,
                max_tokens = provider.MaxTokens,
                top_p = provider.TopP
            }, _jsonOptions), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                var summary = body.Length > 500 ? body[..500] : body;
                throw CreateHttpError(response.StatusCode, $"LLM 请求失败：HTTP {(int)response.StatusCode} {summary.ReplaceLineEndings(" ")}");
            }

            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return string.IsNullOrWhiteSpace(content) ? "我暂时没有生成有效回复。" : content.Trim();
        }, options, cancellationToken);

        if (configService.Config.FullAiReply.SaveContext)
        {
            _context.Add(new ChatMessage("user", userText));
            _context.Add(new ChatMessage("assistant", result));
            TrimContext(configService.Config.FullAiReply.ContextTurns);
        }

        logger.Info("LLM 请求成功");
        return result;
    }

    private LlmProviderConfig GetDefaultProvider()
    {
        return configService.Config.LlmProviders.FirstOrDefault(p => p.Id == configService.Config.DefaultLlmProviderId)
            ?? configService.Config.LlmProviders.First();
    }

    private List<ChatMessage> BuildMessages(LlmProviderConfig provider, string userText)
    {
        var system = string.IsNullOrWhiteSpace(provider.SystemPrompt)
            ? configService.Config.FullAiReply.SystemPrompt
            : provider.SystemPrompt;

        var messages = new List<ChatMessage> { new("system", system) };
        if (configService.Config.FullAiReply.SaveContext)
        {
            messages.AddRange(_context);
        }

        messages.Add(new ChatMessage("user", userText));
        return messages;
    }

    private void TrimContext(int turns)
    {
        var maxMessages = Math.Max(1, turns) * 2;
        if (_context.Count <= maxMessages)
        {
            return;
        }

        _context.RemoveRange(0, _context.Count - maxMessages);
    }

    private static HttpRequestException CreateHttpError(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new HttpRequestException("LLM API Key 或权限错误。", null, statusCode),
            HttpStatusCode.PaymentRequired => new HttpRequestException("LLM 服务额度不足。", null, statusCode),
            HttpStatusCode.TooManyRequests => new HttpRequestException("LLM 请求被限流。", null, statusCode),
            _ => new HttpRequestException(message, null, statusCode)
        };
    }

    private sealed record ChatMessage(string role, string content);
}
