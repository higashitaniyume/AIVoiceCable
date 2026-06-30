namespace AIVoiceCable.Models;

public sealed class AudioDeviceInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Flow { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool IsVbCable { get; set; }

    public string DisplayName => $"{Name}{(IsDefault ? " (默认)" : "")}{(IsVbCable ? " [VB-CABLE]" : "")}";
    public override string ToString() => DisplayName;
}
