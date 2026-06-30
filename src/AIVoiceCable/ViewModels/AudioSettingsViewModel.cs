using System.Collections.ObjectModel;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using AIVoiceCable.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIVoiceCable.ViewModels;

public sealed partial class AudioSettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly AudioDeviceService _audioDeviceService;
    private readonly VbCableService _vbCableService;
    private readonly ILoggingService _logger;

    [ObservableProperty]
    private AudioDeviceInfo? selectedTtsOutputDevice;

    [ObservableProperty]
    private AudioDeviceInfo? selectedMonitorDevice;

    [ObservableProperty]
    private AudioDeviceInfo? selectedCaptureRenderDevice;

    [ObservableProperty]
    private bool monitorLocally;

    [ObservableProperty]
    private bool isVbCableInstalled;

    [ObservableProperty]
    private string statusMessage = "";

    public ObservableCollection<AudioDeviceInfo> RenderDevices { get; } = [];
    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = [];

    public AudioSettingsViewModel(IConfigService configService, AudioDeviceService audioDeviceService, VbCableService vbCableService, ILoggingService logger)
    {
        _configService = configService;
        _audioDeviceService = audioDeviceService;
        _vbCableService = vbCableService;
        _logger = logger;
        MonitorLocally = _configService.Config.Audio.MonitorLocally;
        Refresh();
    }

    [RelayCommand]
    public void Refresh()
    {
        RenderDevices.Clear();
        foreach (var device in _audioDeviceService.GetRenderDevices())
        {
            RenderDevices.Add(device);
        }

        CaptureDevices.Clear();
        foreach (var device in _audioDeviceService.GetCaptureDevices())
        {
            CaptureDevices.Add(device);
        }

        SelectedTtsOutputDevice = RenderDevices.FirstOrDefault(d => d.Id == _configService.Config.Audio.TtsOutputDeviceId)
            ?? _vbCableService.FindCableInput()
            ?? RenderDevices.FirstOrDefault(d => d.IsDefault);
        SelectedMonitorDevice = RenderDevices.FirstOrDefault(d => d.Id == _configService.Config.Audio.MonitorDeviceId)
            ?? RenderDevices.FirstOrDefault(d => d.IsDefault && d.Id != SelectedTtsOutputDevice?.Id);
        SelectedCaptureRenderDevice = RenderDevices.FirstOrDefault(d => d.Id == _configService.Config.Audio.CaptureRenderDeviceId)
            ?? RenderDevices.FirstOrDefault(d => d.IsDefault);

        IsVbCableInstalled = _vbCableService.IsInstalled;
        StatusMessage = IsVbCableInstalled
            ? "已检测到 VB-CABLE。推荐 TTS 输出设备选择 CABLE Input。"
            : "未检测到 VB-CABLE。请安装 VB-Audio Virtual Cable，安装后重启应用或刷新设备列表。";
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        _configService.Config.Audio.TtsOutputDeviceId = SelectedTtsOutputDevice?.Id;
        _configService.Config.Audio.MonitorDeviceId = SelectedMonitorDevice?.Id;
        _configService.Config.Audio.CaptureRenderDeviceId = SelectedCaptureRenderDevice?.Id;
        _configService.Config.Audio.MonitorLocally = MonitorLocally;
        await _configService.SaveConfigAsync();
        _logger.Info("音频设备配置已保存");
        StatusMessage = "音频设备配置已保存。";
    }
}
