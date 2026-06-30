using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface IAudioCaptureService
{
    bool IsRunning { get; }
    Task StartLoopbackAsync(string? renderDeviceId, CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler<AudioCapturedEventArgs>? AudioCaptured;
}
