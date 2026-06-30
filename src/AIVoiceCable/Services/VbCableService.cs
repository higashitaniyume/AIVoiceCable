using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class VbCableService(AudioDeviceService audioDeviceService)
{
    public bool IsInstalled => audioDeviceService.IsVbCableInstalled();

    public AudioDeviceInfo? FindCableInput()
    {
        return audioDeviceService.GetRenderDevices()
            .FirstOrDefault(d => d.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? audioDeviceService.GetRenderDevices().FirstOrDefault(d => d.IsVbCable);
    }

    public AudioDeviceInfo? FindCableOutput()
    {
        return audioDeviceService.GetCaptureDevices()
            .FirstOrDefault(d => d.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
            ?? audioDeviceService.GetCaptureDevices().FirstOrDefault(d => d.IsVbCable);
    }
}
