using System.Collections.ObjectModel;
using System.IO;
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

    public ObservableCollection<VoiceProfile> VoiceProfiles { get; } = [];
    public ObservableCollection<string> FishAudioModels { get; } = [FishAudioConfig.PreferredModel, FishAudioConfig.FallbackModel];
    public ObservableCollection<ReplyHistoryItem> History => _historyService.Items;

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
        RefreshVoices();
        _ = _historyService.LoadAsync();
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        var audio = await GenerateAsync();
        if (audio is null)
        {
            return;
        }

        await PlayAsync(audio, _configService.Config.Audio.MonitorDeviceId, null, false);
    }

    [RelayCommand]
    private async Task SendToCableAsync()
    {
        var audio = await GenerateAsync();
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
        var audio = LastAudioPath;
        if (string.IsNullOrWhiteSpace(audio) || !File.Exists(audio))
        {
            audio = await GenerateAsync();
        }

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
        LastAudioPath = SelectedHistoryItem.AudioPath;
        StatusMessage = "已从历史记录恢复。";
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
            await _historyService.AddAsync(new ReplyHistoryItem
            {
                Text = Text,
                VoiceProfileId = SelectedVoice.Id,
                VoiceProfileName = SelectedVoice.Name,
                Model = SelectedModel,
                AudioPath = audio
            }, _operationCts.Token);
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
        VoiceProfiles.Clear();
        foreach (var voice in _configService.VoiceProfiles)
        {
            VoiceProfiles.Add(voice);
        }

        SelectedVoice = VoiceProfiles.FirstOrDefault(v => v.Id == _configService.Config.FishAudio.DefaultVoiceProfileId)
            ?? VoiceProfiles.FirstOrDefault(v => v.IsDefault)
            ?? VoiceProfiles.FirstOrDefault();
        SelectedModel = _configService.Config.FishAudio.DefaultModel;
    }
}
