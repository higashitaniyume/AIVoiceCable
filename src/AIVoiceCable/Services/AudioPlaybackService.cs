using System.IO;
using AIVoiceCable.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AIVoiceCable.Services;

public sealed class AudioPlaybackService(AudioDeviceService audioDeviceService, ILoggingService logger) : IAudioPlaybackService, IDisposable
{
    private readonly object _gate = new();
    private readonly List<IDisposable> _activePlayers = [];

    public event EventHandler? PlaybackStarted;
    public event EventHandler? PlaybackCompleted;

    public async Task PlayAsync(string audioPath, string? outputDeviceId, string? monitorDeviceId, bool monitorLocally, CancellationToken cancellationToken)
    {
        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException("音频文件不存在", audioPath);
        }

        await StopAsync();
        logger.Info($"播放开始：{audioPath}");
        PlaybackStarted?.Invoke(this, EventArgs.Empty);

        var tasks = new List<Task>
        {
            PlayOneAsync(audioPath, outputDeviceId, cancellationToken)
        };

        if (monitorLocally && !string.IsNullOrWhiteSpace(monitorDeviceId) && monitorDeviceId != outputDeviceId)
        {
            tasks.Add(PlayOneAsync(audioPath, monitorDeviceId, cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks);
            logger.Info("播放结束");
        }
        catch (OperationCanceledException)
        {
            logger.Info("播放已取消");
            throw;
        }
        catch (Exception ex)
        {
            logger.Error("播放失败", ex);
            throw;
        }
        finally
        {
            await StopAsync();
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            foreach (var player in _activePlayers.ToList())
            {
                try
                {
                    if (player is IWavePlayer wavePlayer)
                    {
                        wavePlayer.Stop();
                    }

                    player.Dispose();
                }
                catch
                {
                    // Best effort cleanup.
                }
            }

            _activePlayers.Clear();
        }

        return Task.CompletedTask;
    }

    private Task PlayOneAsync(string audioPath, string? deviceId, CancellationToken cancellationToken)
    {
        var device = audioDeviceService.GetRenderDeviceByIdOrDefault(deviceId);
        if (device is null)
        {
            throw new InvalidOperationException("没有可用的播放设备。");
        }

        var reader = new AudioFileReader(audioPath);
        var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 120);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        output.PlaybackStopped += (_, args) =>
        {
            reader.Dispose();
            output.Dispose();
            lock (_gate)
            {
                _activePlayers.Remove(reader);
                _activePlayers.Remove(output);
            }

            if (args.Exception is not null)
            {
                completion.TrySetException(args.Exception);
            }
            else
            {
                completion.TrySetResult();
            }
        };

        lock (_gate)
        {
            _activePlayers.Add(reader);
            _activePlayers.Add(output);
        }

        using var registration = cancellationToken.Register(() =>
        {
            output.Stop();
            completion.TrySetCanceled(cancellationToken);
        });

        output.Init(reader);
        output.Play();
        return completion.Task;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
