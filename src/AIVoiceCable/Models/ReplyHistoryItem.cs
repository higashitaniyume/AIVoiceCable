using System.Text.Json.Serialization;

namespace AIVoiceCable.Models;

public sealed class ReplyHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Text { get; set; } = "";
    public string VoiceProfileId { get; set; } = "";
    public string VoiceProfileName { get; set; } = "";
    public string Model { get; set; } = "";
    public string? AudioPath { get; set; }
    public List<string> AudioPaths { get; set; } = [];

    [JsonIgnore]
    public string Summary => Text.Length <= 80 ? Text : Text[..80] + "...";

    [JsonIgnore]
    public int AudioCount => GetAudioPaths().Count;

    public IReadOnlyList<string> GetAudioPaths()
    {
        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(AudioPath))
        {
            paths.Add(AudioPath);
        }

        foreach (var path in AudioPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    public void AddAudioPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AudioPaths.RemoveAll(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase));
        AudioPaths.Insert(0, path);
        AudioPath = path;
    }

    public void NormalizeAudioPaths()
    {
        var paths = GetAudioPaths().ToList();
        AudioPaths = paths;
        AudioPath = paths.FirstOrDefault();
    }
}
