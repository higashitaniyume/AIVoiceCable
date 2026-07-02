using System.Collections.ObjectModel;
using System.IO;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using AIVoiceCable.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AIVoiceCable.ViewModels;

public sealed partial class CustomAudioReplyViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly CustomAudioLibraryService _audioLibraryService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ILoggingService _logger;
    private CancellationTokenSource? _playbackCts;

    [ObservableProperty]
    private CustomAudioItem? selectedAudioItem;

    [ObservableProperty]
    private string selectedSourceFilePath = "";

    [ObservableProperty]
    private string newAudioName = "";

    [ObservableProperty]
    private string newAudioNote = "";

    [ObservableProperty]
    private string statusMessage = "准备就绪。";

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<CustomAudioItem> AudioItems => _audioLibraryService.Items;

    public CustomAudioReplyViewModel(
        IConfigService configService,
        CustomAudioLibraryService audioLibraryService,
        IAudioPlaybackService playbackService,
        ILoggingService logger)
    {
        _configService = configService;
        _audioLibraryService = audioLibraryService;
        _playbackService = playbackService;
        _logger = logger;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await _audioLibraryService.LoadAsync();
        SelectedAudioItem ??= AudioItems.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectSourceFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择自定义音频文件",
            Filter = "音频文件 (*.mp3;*.wav;*.aac;*.m4a;*.flac;*.wma)|*.mp3;*.wav;*.aac;*.m4a;*.flac;*.wma|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedSourceFilePath = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NewAudioName))
            {
                NewAudioName = Path.GetFileNameWithoutExtension(dialog.FileName);
            }

            StatusMessage = $"已选择音频文件：{dialog.FileName}";
        }
    }

    [RelayCommand]
    private async Task AddAudioAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedSourceFilePath))
        {
            StatusMessage = "请先选择一个音频文件。";
            return;
        }

        try
        {
            var item = await _audioLibraryService.AddFromFileAsync(SelectedSourceFilePath, NewAudioName, NewAudioNote);
            SelectedAudioItem = item;
            SelectedSourceFilePath = "";
            NewAudioName = "";
            NewAudioNote = "";
            StatusMessage = $"已添加自定义音频：{item.Name}";
        }
        catch (Exception ex)
        {
            _logger.Error("添加自定义音频失败", ex);
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteAudioAsync()
    {
        if (SelectedAudioItem is null)
        {
            StatusMessage = "请先选择一个自定义音频。";
            return;
        }

        var item = SelectedAudioItem;
        await _audioLibraryService.DeleteAsync(item, deleteFile: true);
        SelectedAudioItem = AudioItems.FirstOrDefault();
        StatusMessage = $"已删除自定义音频：{item.Name}";
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        var audio = ResolveSelectedAudio();
        if (audio is null)
        {
            return;
        }

        await PlayAsync(audio, _configService.Config.Audio.MonitorDeviceId, null, false);
    }

    [RelayCommand]
    private async Task SendToCableAsync()
    {
        var audio = ResolveSelectedAudio();
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
    private async Task StopPlaybackAsync()
    {
        _playbackCts?.Cancel();
        await _playbackService.StopAsync();
        IsBusy = false;
        StatusMessage = "已停止。";
    }

    private string? ResolveSelectedAudio()
    {
        if (SelectedAudioItem is null)
        {
            StatusMessage = "请先从列表中选择一个自定义音频。";
            return null;
        }

        if (!File.Exists(SelectedAudioItem.FilePath))
        {
            StatusMessage = "该自定义音频文件已被清理或移动，请删除后重新添加。";
            _logger.Warn($"自定义音频文件不存在：{SelectedAudioItem.FilePath}");
            return null;
        }

        return SelectedAudioItem.FilePath;
    }

    private async Task PlayAsync(string audioPath, string? outputDeviceId, string? monitorDeviceId, bool monitorLocally)
    {
        _playbackCts?.Cancel();
        _playbackCts = new CancellationTokenSource();
        IsBusy = true;
        StatusMessage = "正在播放自定义音频...";

        try
        {
            await _playbackService.PlayAsync(audioPath, outputDeviceId, monitorDeviceId, monitorLocally, _playbackCts.Token);
            StatusMessage = "播放完成。";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("自定义音频播放失败", ex);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
