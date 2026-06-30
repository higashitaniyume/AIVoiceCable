using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AIVoiceCable.Services;

public sealed class SystemAudioCaptureService(AudioDeviceService audioDeviceService, ILoggingService logger) : IAudioCaptureService, IDisposable
{
    private readonly object _gate = new();
    private WasapiLoopbackCapture? _capture;

    public bool IsRunning { get; private set; }
    public event EventHandler<AudioCapturedEventArgs>? AudioCaptured;

    public Task StartLoopbackAsync(string? renderDeviceId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            StopInternal();
            var device = audioDeviceService.GetRenderDeviceByIdOrDefault(renderDeviceId);
            if (device is null)
            {
                throw new InvalidOperationException("没有可用的系统音频监听来源。");
            }

            _capture = new WasapiLoopbackCapture(device);
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            IsRunning = true;
            logger.Info($"WASAPI loopback 已启动：{device.FriendlyName}");

            cancellationToken.Register(() => _ = StopAsync());
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            StopInternal();
        }

        return Task.CompletedTask;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!IsRunning || _capture is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
        AudioCaptured?.Invoke(this, new AudioCapturedEventArgs(buffer, e.BytesRecorded, _capture.WaveFormat));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsRunning = false;
        if (e.Exception is not null)
        {
            logger.Error("WASAPI loopback 已停止并出现异常", e.Exception);
        }
        else
        {
            logger.Info("WASAPI loopback 已停止");
        }
    }

    private void StopInternal()
    {
        if (_capture is null)
        {
            IsRunning = false;
            return;
        }

        try
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.StopRecording();
        }
        catch
        {
            // StopRecording can throw if the device disappears.
        }
        finally
        {
            _capture.Dispose();
            _capture = null;
            IsRunning = false;
        }
    }

    public void Dispose()
    {
        StopInternal();
    }
}
