using System.Collections.ObjectModel;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIVoiceCable.ViewModels;

public sealed partial class VoiceProfilesViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ILoggingService _logger;

    [ObservableProperty]
    private VoiceProfile? selectedProfile;

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string referenceId = "";

    [ObservableProperty]
    private string note = "";

    [ObservableProperty]
    private string statusMessage = "";

    public ObservableCollection<VoiceProfile> VoiceProfiles { get; } = [];

    public VoiceProfilesViewModel(IConfigService configService, ILoggingService logger)
    {
        _configService = configService;
        _logger = logger;
        Refresh();
        SelectedProfile = VoiceProfiles.FirstOrDefault(v => v.IsDefault) ?? VoiceProfiles.FirstOrDefault();
    }

    partial void OnSelectedProfileChanged(VoiceProfile? value)
    {
        Name = value?.Name ?? "";
        ReferenceId = value?.ReferenceId ?? "";
        Note = value?.Note ?? "";
    }

    [RelayCommand]
    private void New()
    {
        SelectedProfile = null;
        Name = "";
        ReferenceId = "";
        Note = "";
        StatusMessage = "正在新建声色。";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ReferenceId))
        {
            StatusMessage = "声色名称和 reference_id 不能为空。";
            return;
        }

        if (SelectedProfile is null)
        {
            SelectedProfile = new VoiceProfile { Name = Name.Trim(), ReferenceId = ReferenceId.Trim(), Note = Note.Trim() };
            _configService.VoiceProfiles.Add(SelectedProfile);
            VoiceProfiles.Add(SelectedProfile);
        }
        else
        {
            SelectedProfile.Name = Name.Trim();
            SelectedProfile.ReferenceId = ReferenceId.Trim();
            SelectedProfile.Note = Note.Trim();
        }

        await _configService.SaveVoiceProfilesAsync();
        _logger.Info($"声色已保存：{Name}");
        StatusMessage = "声色已保存。";
        Refresh(SelectedProfile.Id);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        if (SelectedProfile.Id == "preset-yongchutafei")
        {
            StatusMessage = "预置声色不能删除。";
            return;
        }

        _configService.VoiceProfiles.Remove(SelectedProfile);
        await _configService.SaveVoiceProfilesAsync();
        _logger.Info($"声色已删除：{SelectedProfile.Name}");
        StatusMessage = "声色已删除。";
        Refresh();
    }

    [RelayCommand]
    private async Task SetDefaultAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        foreach (var voice in _configService.VoiceProfiles)
        {
            voice.IsDefault = voice.Id == SelectedProfile.Id;
        }

        _configService.Config.FishAudio.DefaultVoiceProfileId = SelectedProfile.Id;
        await _configService.SaveVoiceProfilesAsync();
        await _configService.SaveConfigAsync();
        StatusMessage = $"默认声色已设置为：{SelectedProfile.Name}";
        Refresh(SelectedProfile.Id);
    }

    private void Refresh(string? selectedId = null)
    {
        VoiceProfiles.Clear();
        foreach (var profile in _configService.VoiceProfiles)
        {
            VoiceProfiles.Add(profile);
        }

        SelectedProfile = VoiceProfiles.FirstOrDefault(v => v.Id == selectedId)
            ?? VoiceProfiles.FirstOrDefault(v => v.IsDefault)
            ?? VoiceProfiles.FirstOrDefault();
    }
}
