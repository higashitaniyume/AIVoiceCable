namespace AIVoiceCable.Interfaces;

public interface IAudioPlaybackService
{
    Task PlayAsync(string audioPath, string? outputDeviceId, string? monitorDeviceId, bool monitorLocally, CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler? PlaybackStarted;
    event EventHandler? PlaybackCompleted;
}
