using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class FishAudioService(
    IConfigService configService,
    ISecretService secretService,
    RetryPolicyService retryPolicy,
    ILoggingService logger) : ITtsService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> GenerateSpeechAsync(string text, VoiceProfile voiceProfile, string model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secretService.Secrets.FishAudioApiKey))
        {
            throw new InvalidOperationException("请先在 API 设置中填写 Fish Audio API Key。");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("请输入要生成语音的文本。");
        }

        var models = new[] { model, configService.Config.FishAudio.FallbackModelName }
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Exception? lastError = null;
        foreach (var candidate in models)
        {
            try
            {
                return await GenerateWithModelAsync(text, voiceProfile, candidate, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.UnprocessableEntity && candidate != models.Last())
            {
                lastError = ex;
                logger.Warn($"Fish Audio 模型 {candidate} 失败，尝试回退模型。");
            }
        }

        throw lastError ?? new InvalidOperationException("Fish Audio 生成失败。");
    }

    private async Task<string> GenerateWithModelAsync(string text, VoiceProfile voiceProfile, string model, CancellationToken cancellationToken)
    {
        logger.Info($"Fish Audio 请求开始：model={model}, voice={voiceProfile.Name}");
        var options = new RetryOptions { MaxAttempts = configService.Config.FishAudio.RetryCount };

        var bytes = await retryPolicy.ExecuteAsync(async token =>
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(configService.Config.FishAudio.TimeoutSeconds) };
            var baseUri = new Uri(configService.Config.FishAudio.BaseUrl.TrimEnd('/') + "/");
            var requestUri = new Uri(baseUri, "v1/tts");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretService.Secrets.FishAudioApiKey);
            request.Headers.TryAddWithoutValidation("model", model);

            var payload = new
            {
                text,
                reference_id = voiceProfile.ReferenceId,
                format = "mp3",
                sample_rate = 44100,
                mp3_bitrate = 128,
                normalize = true,
                latency = "normal",
                chunk_length = 300,
                temperature = 0.7,
                top_p = 0.7,
                prosody = new { speed = 1.0, volume = 0, normalize_loudness = true }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                var summary = await ReadSummaryAsync(response, token);
                throw CreateHttpError(response.StatusCode, $"Fish Audio 请求失败：HTTP {(int)response.StatusCode} {summary}");
            }

            return await response.Content.ReadAsByteArrayAsync(token);
        }, options, cancellationToken);

        var path = Path.Combine(configService.CacheDirectory, $"tts-{DateTimeOffset.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        logger.Info($"Fish Audio 请求成功，音频已保存：{path}");
        return path;
    }

    private static async Task<string> ReadSummaryAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 500)
        {
            body = body[..500];
        }

        return body.ReplaceLineEndings(" ");
    }

    private static HttpRequestException CreateHttpError(HttpStatusCode statusCode, string message)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new HttpRequestException("Fish Audio API Key 或权限错误。", null, statusCode),
            HttpStatusCode.PaymentRequired => new HttpRequestException("Fish Audio API Credit 不足。", null, statusCode),
            HttpStatusCode.TooManyRequests => new HttpRequestException("Fish Audio 请求被限流。", null, statusCode),
            _ => new HttpRequestException(message, null, statusCode)
        };
    }
}
