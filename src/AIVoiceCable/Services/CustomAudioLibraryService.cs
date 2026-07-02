using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class CustomAudioLibraryService(IConfigService configService, ILoggingService logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private string LibraryPath => Path.Combine(configService.AppDataDirectory, "custom-audios.json");
    private string AudioDirectory => Path.Combine(configService.AppDataDirectory, "custom-audios");

    public ObservableCollection<CustomAudioItem> Items { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AudioDirectory);
        if (!File.Exists(LibraryPath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(LibraryPath);
            var items = await JsonSerializer.DeserializeAsync<List<CustomAudioItem>>(stream, _jsonOptions, cancellationToken) ?? [];
            Items.Clear();
            foreach (var item in items.OrderByDescending(i => i.CreatedAt))
            {
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            logger.Error("自定义音频库加载失败", ex);
        }
    }

    public async Task<CustomAudioItem> AddFromFileAsync(string sourcePath, string? name, string? note, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("选择的音频文件不存在。", sourcePath);
        }

        Directory.CreateDirectory(AudioDirectory);
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".audio";
        }

        var targetPath = Path.Combine(AudioDirectory, $"custom-{DateTimeOffset.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, targetPath, overwrite: false);

        var item = new CustomAudioItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(sourcePath) : name.Trim(),
            FilePath = targetPath,
            Note = note?.Trim() ?? ""
        };

        Items.Insert(0, item);
        await SaveAsync(cancellationToken);
        logger.Info($"自定义音频已添加：{item.Name}");
        return item;
    }

    public async Task DeleteAsync(CustomAudioItem item, bool deleteFile, CancellationToken cancellationToken = default)
    {
        Items.Remove(item);
        if (deleteFile && File.Exists(item.FilePath))
        {
            File.Delete(item.FilePath);
        }

        await SaveAsync(cancellationToken);
        logger.Info($"自定义音频已删除：{item.Name}");
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(configService.AppDataDirectory);
        await using var stream = File.Create(LibraryPath);
        await JsonSerializer.SerializeAsync(stream, Items.ToList(), _jsonOptions, cancellationToken);
    }
}
