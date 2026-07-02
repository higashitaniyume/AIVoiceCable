using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class ReplyHistoryService(IConfigService configService, ILoggingService logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private string HistoryPath => Path.Combine(configService.AppDataDirectory, "history.json");
    public ObservableCollection<ReplyHistoryItem> Items { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(HistoryPath))
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(HistoryPath);
            var items = await JsonSerializer.DeserializeAsync<List<ReplyHistoryItem>>(stream, _jsonOptions, cancellationToken) ?? [];
            Items.Clear();
            foreach (var item in items.OrderByDescending(i => i.CreatedAt).Take(200))
            {
                item.NormalizeAudioPaths();
                Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            logger.Error("历史记录加载失败", ex);
        }
    }

    public async Task AddAsync(ReplyHistoryItem item, CancellationToken cancellationToken = default)
    {
        item.NormalizeAudioPaths();
        Items.Insert(0, item);
        while (Items.Count > 200)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        await SaveAsync(cancellationToken);
    }

    public async Task SaveItemAsync(ReplyHistoryItem item, CancellationToken cancellationToken = default)
    {
        item.NormalizeAudioPaths();
        var index = Items.IndexOf(item);
        if (index >= 0)
        {
            Items.RemoveAt(index);
            Items.Insert(index, item);
        }

        await SaveAsync(cancellationToken);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(configService.AppDataDirectory);
        await using var stream = File.Create(HistoryPath);
        await JsonSerializer.SerializeAsync(stream, Items.ToList(), _jsonOptions, cancellationToken);
    }
}
