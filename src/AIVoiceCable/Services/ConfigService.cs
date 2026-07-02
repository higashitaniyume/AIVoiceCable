using System.IO;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class ConfigService(ISecretService secretService, ILoggingService logger) : IConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();

    public AppConfig Config { get; private set; } = new();
    public IList<VoiceProfile> VoiceProfiles { get; private set; } = new List<VoiceProfile>();
    public string AppDataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIVoiceCable");
    public string CacheDirectory => Path.Combine(AppDataDirectory, "cache");
    public string LogsDirectory => Path.Combine(AppDataDirectory, "logs");
    public event EventHandler? VoiceProfilesChanged;

    private string ConfigPath => Path.Combine(AppDataDirectory, "config.json");
    private string VoicesPath => Path.Combine(AppDataDirectory, "voices.json");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(LogsDirectory);

        Config = await LoadJsonAsync(ConfigPath, new AppConfig(), cancellationToken);
        VoiceProfiles = await LoadJsonAsync<IList<VoiceProfile>>(VoicesPath, new List<VoiceProfile> { VoiceProfile.CreateDefault() }, cancellationToken);

        if (VoiceProfiles.All(v => v.Id != "preset-yongchutafei"))
        {
            VoiceProfiles.Insert(0, VoiceProfile.CreateDefault());
        }

        if (!VoiceProfiles.Any(v => v.IsDefault) && VoiceProfiles.Count > 0)
        {
            VoiceProfiles[0].IsDefault = true;
        }

        Config.FishAudio.DefaultVoiceProfileId = VoiceProfiles.FirstOrDefault(v => v.IsDefault)?.Id ?? Config.FishAudio.DefaultVoiceProfileId;
        if (Config.LlmProviders.Count == 0)
        {
            Config.LlmProviders.Add(LlmProviderConfig.CreateDeepSeekDefault());
        }

        await secretService.LoadAsync(cancellationToken);
        await SaveConfigAsync(cancellationToken);
        await SaveVoiceProfilesAsync(cancellationToken);
        logger.Info($"配置已加载：{AppDataDirectory}");
    }

    public Task SaveConfigAsync(CancellationToken cancellationToken = default)
    {
        return SaveJsonAsync(ConfigPath, Config, cancellationToken);
    }

    public async Task SaveVoiceProfilesAsync(CancellationToken cancellationToken = default)
    {
        await SaveJsonAsync(VoicesPath, VoiceProfiles, cancellationToken);
        VoiceProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<T> LoadJsonAsync<T>(string path, T fallback, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken) ?? fallback;
        }
        catch (Exception ex)
        {
            var backupPath = $"{path}.bad.{DateTimeOffset.Now:yyyyMMddHHmmss}";
            File.Copy(path, backupPath, overwrite: true);
            logger.Error($"配置文件损坏，已备份到 {backupPath}", ex);
            return fallback;
        }
    }

    private async Task SaveJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, _jsonOptions, cancellationToken);
        }

        lock (_gate)
        {
            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
        }
    }
}
