using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface ITtsService
{
    Task<string> GenerateSpeechAsync(string text, VoiceProfile voiceProfile, string model, CancellationToken cancellationToken);
}
