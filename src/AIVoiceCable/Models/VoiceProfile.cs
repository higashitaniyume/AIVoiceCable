namespace AIVoiceCable.Models;

public sealed class VoiceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string Note { get; set; } = "";
    public bool IsDefault { get; set; }

    public override string ToString() => Name;

    public static VoiceProfile CreateDefault()
    {
        return new VoiceProfile
        {
            Id = "preset-yongchutafei",
            Name = "永雏塔菲",
            ReferenceId = "55b28b196e1c4fff9a55cd32a46eff25",
            Note = "预置声色",
            IsDefault = true
        };
    }
}
