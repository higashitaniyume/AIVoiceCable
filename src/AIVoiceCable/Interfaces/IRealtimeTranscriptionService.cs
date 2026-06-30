using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface IRealtimeTranscriptionService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    event EventHandler<PartialTranscriptEventArgs>? PartialTranscriptReceived;
    event EventHandler<FinalTranscriptEventArgs>? FinalTranscriptReceived;
    event EventHandler<TranscriptionErrorEventArgs>? ErrorOccurred;
}
