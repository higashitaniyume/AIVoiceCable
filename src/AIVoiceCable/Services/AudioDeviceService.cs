using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using NAudio.CoreAudioApi;

namespace AIVoiceCable.Services;

public sealed class AudioDeviceService(ILoggingService logger)
{
    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        return Enumerate(DataFlow.Render);
    }

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        return Enumerate(DataFlow.Capture);
    }

    public MMDevice? GetRenderDeviceByIdOrDefault(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            try
            {
                return enumerator.GetDevice(deviceId);
            }
            catch (Exception ex)
            {
                logger.Warn($"找不到已保存的播放设备：{deviceId}，将使用默认设备。{ex.Message}");
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    public bool IsVbCableInstalled()
    {
        return GetRenderDevices().Concat(GetCaptureDevices()).Any(d => d.IsVbCable);
    }

    private IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = "";
        try
        {
            defaultId = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia).ID;
        }
        catch
        {
            // Some systems can have no active endpoint for a flow.
        }

        var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(device => new AudioDeviceInfo
            {
                Id = device.ID,
                Name = device.FriendlyName,
                Flow = flow.ToString(),
                IsDefault = device.ID == defaultId,
                IsVbCable = IsVbCableName(device.FriendlyName)
            })
            .OrderByDescending(d => d.IsDefault)
            .ThenByDescending(d => d.IsVbCable)
            .ThenBy(d => d.Name)
            .ToList();

        logger.Info($"已刷新{(flow == DataFlow.Render ? "播放" : "录音")}设备：{devices.Count} 个");
        return devices;
    }

    private static bool IsVbCableName(string name)
    {
        return name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
            || name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase)
            || name.Contains("VB-CABLE", StringComparison.OrdinalIgnoreCase);
    }
}
