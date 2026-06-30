using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using NAudio.Wave;

namespace AIVoiceCable.Services;

public sealed class AssemblyAiRealtimeTranscriptionService(
    IConfigService configService,
    ISecretService secretService,
    IAudioCaptureService captureService,
    ILoggingService logger) : IRealtimeTranscriptionService, IDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private volatile bool _acceptAudio;

    public event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptReceived;
    public event EventHandler<FinalTranscriptEventArgs>? FinalTranscriptReceived;
    public event EventHandler<TranscriptionErrorEventArgs>? ErrorOccurred;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secretService.Secrets.AssemblyAiApiKey))
        {
            throw new InvalidOperationException("请先在 API 设置中填写 AssemblyAI API Key。");
        }

        await StopAsync();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _webSocket = new ClientWebSocket();
        var uri = BuildUri();
        logger.Info("AssemblyAI WebSocket 连接中");
        await _webSocket.ConnectAsync(uri, _cts.Token);

        captureService.AudioCaptured += OnAudioCaptured;
        _acceptAudio = true;
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);
        logger.Info("AssemblyAI WebSocket 已连接");
    }

    public async Task StopAsync()
    {
        _acceptAudio = false;
        captureService.AudioCaptured -= OnAudioCaptured;
        _cts?.Cancel();

        if (_webSocket is { State: WebSocketState.Open })
        {
            try
            {
                var terminate = Encoding.UTF8.GetBytes("{\"type\":\"Terminate\"}");
                await _webSocket.SendAsync(terminate, WebSocketMessageType.Text, true, CancellationToken.None);
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None);
            }
            catch
            {
                // Best effort shutdown.
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        _cts?.Dispose();
        _cts = null;
        logger.Info("AssemblyAI WebSocket 已断开");
    }

    private Uri BuildUri()
    {
        var config = configService.Config.AssemblyAi;
        var separator = config.WebSocketEndpoint.Contains('?') ? "&" : "?";
        var query = $"speech_model={Uri.EscapeDataString(config.SpeechModel)}&encoding=pcm_s16le&sample_rate={config.SampleRate}&min_turn_silence={config.SilenceEndThresholdMs}&token={Uri.EscapeDataString(secretService.Secrets.AssemblyAiApiKey!)}";
        return new Uri(config.WebSocketEndpoint + separator + query);
    }

    private async void OnAudioCaptured(object? sender, AudioCapturedEventArgs e)
    {
        if (!_acceptAudio || _webSocket is not { State: WebSocketState.Open } socket)
        {
            return;
        }

        try
        {
            var pcm = AudioConverter.ToPcm16Mono(e.Buffer, e.BytesRecorded, e.WaveFormat, configService.Config.AssemblyAi.SampleRate);
            if (pcm.Length == 0)
            {
                return;
            }

            await _sendGate.WaitAsync();
            try
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(pcm, WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
            finally
            {
                _sendGate.Release();
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new TranscriptionErrorEventArgs("发送音频到 AssemblyAI 失败", ex));
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket is { State: WebSocketState.Open } socket)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                HandleMessage(json);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.Error("AssemblyAI 接收转录失败", ex);
            ErrorOccurred?.Invoke(this, new TranscriptionErrorEventArgs("AssemblyAI 接收转录失败", ex));
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeProperty)
                ? typeProperty.GetString()
                : root.TryGetProperty("message_type", out var messageType) ? messageType.GetString() : null;

            if (string.Equals(type, "Turn", StringComparison.OrdinalIgnoreCase))
            {
                var transcript = root.TryGetProperty("transcript", out var text) ? text.GetString() ?? "" : "";
                var endOfTurn = root.TryGetProperty("end_of_turn", out var final) && final.GetBoolean();
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    return;
                }

                if (endOfTurn)
                {
                    logger.Info($"ASR final transcript：{transcript}");
                    FinalTranscriptReceived?.Invoke(this, new FinalTranscriptEventArgs(transcript));
                }
                else
                {
                    logger.Info($"ASR partial transcript：{transcript}");
                    PartialTranscriptReceived?.Invoke(this, new PartialTranscriptEventArgs(transcript));
                }
            }
            else if (string.Equals(type, "PartialTranscript", StringComparison.OrdinalIgnoreCase))
            {
                var transcript = root.GetProperty("text").GetString() ?? "";
                PartialTranscriptReceived?.Invoke(this, new PartialTranscriptEventArgs(transcript));
            }
            else if (string.Equals(type, "FinalTranscript", StringComparison.OrdinalIgnoreCase))
            {
                var transcript = root.GetProperty("text").GetString() ?? "";
                logger.Info($"ASR final transcript：{transcript}");
                FinalTranscriptReceived?.Invoke(this, new FinalTranscriptEventArgs(transcript));
            }
        }
        catch (Exception ex)
        {
            logger.Error("AssemblyAI 消息解析失败", ex);
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _sendGate.Dispose();
    }

    private static class AudioConverter
    {
        public static byte[] ToPcm16Mono(byte[] source, int bytesRecorded, WaveFormat format, int targetSampleRate)
        {
            var channels = Math.Max(1, format.Channels);
            var sourceRate = format.SampleRate;
            var frameCount = bytesRecorded / format.BlockAlign;
            if (frameCount == 0)
            {
                return [];
            }

            var mono = new float[frameCount];
            for (var frame = 0; frame < frameCount; frame++)
            {
                var sum = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    var offset = frame * format.BlockAlign + channel * (format.BitsPerSample / 8);
                    sum += ReadSample(source, offset, format);
                }

                mono[frame] = sum / channels;
            }

            var targetFrames = Math.Max(1, (int)Math.Round(frameCount * (double)targetSampleRate / sourceRate));
            var output = new byte[targetFrames * 2];
            for (var i = 0; i < targetFrames; i++)
            {
                var sourcePosition = i * (sourceRate / (double)targetSampleRate);
                var index = Math.Min(frameCount - 1, (int)sourcePosition);
                var next = Math.Min(frameCount - 1, index + 1);
                var fraction = sourcePosition - index;
                var sample = mono[index] + (mono[next] - mono[index]) * fraction;
                var pcm = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
                output[i * 2] = (byte)(pcm & 0xff);
                output[i * 2 + 1] = (byte)((pcm >> 8) & 0xff);
            }

            return output;
        }

        private static float ReadSample(byte[] source, int offset, WaveFormat format)
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                return BitConverter.ToSingle(source, offset);
            }

            if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)
            {
                return BitConverter.ToInt16(source, offset) / 32768f;
            }

            if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 32)
            {
                return BitConverter.ToInt32(source, offset) / (float)int.MaxValue;
            }

            return 0f;
        }
    }
}
