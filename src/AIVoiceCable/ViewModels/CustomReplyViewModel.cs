using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using AIVoiceCable.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AIVoiceCable.ViewModels;

public sealed partial class CustomReplyViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ITtsService _ttsService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ReplyHistoryService _historyService;
    private readonly ILoggingService _logger;
    private CancellationTokenSource? _operationCts;
    private ReplyHistoryItem? _activeHistoryItem;

    [ObservableProperty]
    private string text = "";

    [ObservableProperty]
    private VoiceProfile? selectedVoice;

    [ObservableProperty]
    private string selectedModel = FishAudioConfig.PreferredModel;

    [ObservableProperty]
    private ReplyHistoryItem? selectedHistoryItem;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "准备就绪。";

    [ObservableProperty]
    private string? lastAudioPath;

    [ObservableProperty]
    private string? selectedHistoryAudioPath;

    public ObservableCollection<VoiceProfile> VoiceProfiles { get; } = [];
    public ObservableCollection<string> FishAudioModels { get; } = [FishAudioConfig.PreferredModel, FishAudioConfig.FallbackModel];
    public ObservableCollection<ReplyHistoryItem> History => _historyService.Items;
    public ObservableCollection<string> ActiveHistoryAudioPaths { get; } = [];

    public CustomReplyViewModel(
        IConfigService configService,
        ITtsService ttsService,
        IAudioPlaybackService playbackService,
        ReplyHistoryService historyService,
        ILoggingService logger)
    {
        _configService = configService;
        _ttsService = ttsService;
        _playbackService = playbackService;
        _historyService = historyService;
        _logger = logger;
        _configService.VoiceProfilesChanged += OnVoiceProfilesChanged;
        RefreshVoices();
        _ = _historyService.LoadAsync();
    }

    partial void OnTextChanged(string value)
    {
        ClearActiveHistoryIfEdited();
    }

    partial void OnSelectedVoiceChanged(VoiceProfile? value)
    {
        ClearActiveHistoryIfEdited();
    }

    partial void OnSelectedModelChanged(string value)
    {
        ClearActiveHistoryIfEdited();
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        var audio = await ResolveAudioForPlaybackAsync();
        if (audio is null)
        {
            return;
        }

        await PlayAsync(audio, _configService.Config.Audio.MonitorDeviceId, null, false);
    }

    [RelayCommand]
    private async Task SendToCableAsync()
    {
        var audio = await ResolveAudioForPlaybackAsync();
        if (audio is null)
        {
            return;
        }

        await PlayAsync(
            audio,
            _configService.Config.Audio.TtsOutputDeviceId,
            _configService.Config.Audio.MonitorDeviceId,
            _configService.Config.Audio.MonitorLocally);
    }

    [RelayCommand]
    private async Task SaveAudioFileAsync()
    {
        var audio = await ResolveAudioForPlaybackAsync();
        if (string.IsNullOrWhiteSpace(audio) || !File.Exists(audio))
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "保存语音文件",
            Filter = "MP3 文件 (*.mp3)|*.mp3|所有文件 (*.*)|*.*",
            FileName = $"AIVoiceCable-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.mp3"
        };

        if (dialog.ShowDialog() == true)
        {
            File.Copy(audio, dialog.FileName, overwrite: true);
            StatusMessage = $"音频已保存：{dialog.FileName}";
            _logger.Info(StatusMessage);
        }
    }

    [RelayCommand]
    private async Task StopPlaybackAsync()
    {
        _operationCts?.Cancel();
        await _playbackService.StopAsync();
        IsBusy = false;
        StatusMessage = "已停止。";
    }

    [RelayCommand]
    private void ClearText()
    {
        Text = "";
        LastAudioPath = null;
        SelectedHistoryAudioPath = null;
        ActiveHistoryAudioPaths.Clear();
        _activeHistoryItem = null;
        StatusMessage = "文本已清空。";
    }

    [RelayCommand]
    private void ReuseHistory()
    {
        if (SelectedHistoryItem is null)
        {
            return;
        }

        Text = SelectedHistoryItem.Text;
        SelectedModel = SelectedHistoryItem.Model;
        SelectedVoice = VoiceProfiles.FirstOrDefault(v => v.Id == SelectedHistoryItem.VoiceProfileId) ?? SelectedVoice;
        _activeHistoryItem = SelectedHistoryItem;
        RefreshActiveHistoryAudioPaths(SelectedHistoryItem);
        LastAudioPath = SelectedHistoryAudioPath;

        if (!string.IsNullOrWhiteSpace(LastAudioPath) && File.Exists(LastAudioPath))
        {
            StatusMessage = $"已从历史记录恢复，可复用 {ActiveHistoryAudioPaths.Count} 个音频文件。";
        }
        else
        {
            StatusMessage = "已恢复历史文本，但关联语音文件已被清理或不存在。点击试听或发送到 VB-CABLE 会重新生成。";
            _logger.Warn("历史记录关联语音文件不存在或已被清理");
        }
    }

    private async Task<string?> ResolveAudioForPlaybackAsync()
    {
        if (_activeHistoryItem is not null && IsActiveHistoryStillSelected())
        {
            var audioPath = SelectedHistoryAudioPath ?? _activeHistoryItem.GetAudioPaths().FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(audioPath) && File.Exists(audioPath))
            {
                LastAudioPath = audioPath;
                StatusMessage = "正在使用历史记录中已生成的语音文件...";
                return audioPath;
            }

            StatusMessage = "历史记录关联的语音文件已被清理，正在重新生成。";
            _logger.Warn("历史记录关联语音文件已被清理，准备重新生成");
        }

        return await GenerateAsync();
    }

    private async Task<string?> GenerateAsync()
    {
        if (SelectedVoice is null)
        {
            StatusMessage = "请先选择声色。";
            return null;
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            StatusMessage = "请输入要生成的文本。";
            return null;
        }

        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "正在生成语音...";

        try
        {
            var audio = await _ttsService.GenerateSpeechAsync(Text, SelectedVoice, SelectedModel, _operationCts.Token);
            LastAudioPath = audio;
            var historyItem = await GetOrCreateActiveHistoryItemAsync(audio, _operationCts.Token);
            RefreshActiveHistoryAudioPaths(historyItem, audio);
            StatusMessage = $"语音生成完成：{audio}";
            return audio;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("自定义回复生成语音失败", ex);
            StatusMessage = ex.Message;
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PlayAsync(string audio, string? outputDeviceId, string? monitorDeviceId, bool monitorLocally)
    {
        _operationCts?.Cancel();
        _operationCts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "正在播放...";
        try
        {
            await _playbackService.PlayAsync(audio, outputDeviceId, monitorDeviceId, monitorLocally, _operationCts.Token);
            StatusMessage = "播放完成。";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("播放失败", ex);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshVoices()
    {
        var selectedId = SelectedVoice?.Id;
        var selectedModel = SelectedModel;
        VoiceProfiles.Clear();
        foreach (var voice in _configService.VoiceProfiles)
        {
            VoiceProfiles.Add(voice);
        }

        SelectedVoice = VoiceProfiles.FirstOrDefault(v => v.Id == selectedId)
            ?? VoiceProfiles.FirstOrDefault(v => v.Id == _configService.Config.FishAudio.DefaultVoiceProfileId)
            ?? VoiceProfiles.FirstOrDefault(v => v.IsDefault)
            ?? VoiceProfiles.FirstOrDefault();
        SelectedModel = string.IsNullOrWhiteSpace(selectedModel) ? _configService.Config.FishAudio.DefaultModel : selectedModel;
    }

    private bool IsActiveHistoryStillSelected()
    {
        return _activeHistoryItem is not null
            && string.Equals(Text, _activeHistoryItem.Text, StringComparison.Ordinal)
            && string.Equals(SelectedModel, _activeHistoryItem.Model, StringComparison.Ordinal)
            && string.Equals(SelectedVoice?.Id, _activeHistoryItem.VoiceProfileId, StringComparison.Ordinal);
    }

    private void ClearActiveHistoryIfEdited()
    {
        if (_activeHistoryItem is not null && !IsActiveHistoryStillSelected())
        {
            _activeHistoryItem = null;
            SelectedHistoryAudioPath = null;
            ActiveHistoryAudioPaths.Clear();
        }
    }

    private void OnVoiceProfilesChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RefreshVoices();
        }
        else
        {
            dispatcher.Invoke(RefreshVoices);
        }
    }

    private async Task<ReplyHistoryItem> GetOrCreateActiveHistoryItemAsync(string audioPath, CancellationToken cancellationToken)
    {
        if (_activeHistoryItem is not null && IsActiveHistoryStillSelected())
        {
            _activeHistoryItem.AddAudioPath(audioPath);
            await _historyService.SaveItemAsync(_activeHistoryItem, cancellationToken);
            return _activeHistoryItem;
        }

        var voice = SelectedVoice ?? VoiceProfiles.FirstOrDefault();
        var item = new ReplyHistoryItem
        {
            Text = string.IsNullOrWhiteSpace(Text) ? Path.GetFileNameWithoutExtension(audioPath) : Text,
            VoiceProfileId = voice?.Id ?? "",
            VoiceProfileName = voice?.Name ?? "",
            Model = string.IsNullOrWhiteSpace(SelectedModel) ? _configService.Config.FishAudio.DefaultModel : SelectedModel
        };
        item.AddAudioPath(audioPath);
        await _historyService.AddAsync(item, cancellationToken);
        _activeHistoryItem = item;
        if (string.IsNullOrWhiteSpace(Text))
        {
            Text = item.Text;
        }

        return item;
    }

    private void RefreshActiveHistoryAudioPaths(ReplyHistoryItem item, string? preferredPath = null)
    {
        item.NormalizeAudioPaths();
        ActiveHistoryAudioPaths.Clear();
        foreach (var path in item.GetAudioPaths())
        {
            ActiveHistoryAudioPaths.Add(path);
        }

        SelectedHistoryAudioPath = !string.IsNullOrWhiteSpace(preferredPath)
            ? preferredPath
            : ActiveHistoryAudioPaths.FirstOrDefault(File.Exists) ?? ActiveHistoryAudioPaths.FirstOrDefault();
        LastAudioPath = SelectedHistoryAudioPath;
    }
}
