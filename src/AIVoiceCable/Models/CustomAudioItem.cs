using System.IO;
using System.Text.Json.Serialization;

namespace AIVoiceCable.Models;

public sealed class CustomAudioItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Note { get; set; } = "";

    [JsonIgnore]
    public string FileName => Path.GetFileName(FilePath);

    [JsonIgnore]
    public bool Exists => File.Exists(FilePath);

    [JsonIgnore]
    public string DisplayName => $"{Name}{(Exists ? "" : " (文件缺失)")}";
}
