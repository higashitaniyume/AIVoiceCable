using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface IConfigService
{
    AppConfig Config { get; }
    IList<VoiceProfile> VoiceProfiles { get; }
    string AppDataDirectory { get; }
    string CacheDirectory { get; }
    string LogsDirectory { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveConfigAsync(CancellationToken cancellationToken = default);
    Task SaveVoiceProfilesAsync(CancellationToken cancellationToken = default);
}
