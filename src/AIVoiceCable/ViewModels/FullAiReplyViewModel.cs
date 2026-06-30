using System.Collections.ObjectModel;
using System.Windows;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using AIVoiceCable.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIVoiceCable.ViewModels;

public sealed partial class FullAiReplyViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IAudioCaptureService _captureService;
    private readonly IRealtimeTranscriptionService _transcriptionService;
    private readonly ILlmService _llmService;
    private readonly ITtsService _ttsService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly AudioDeviceService _audioDeviceService;
    private readonly ILoggingService _logger;
    private CancellationTokenSource? _runCts;
    private bool _suppressFinalTranscript;
    private bool _isProcessingFinal;

    [ObservableProperty]
    private string state = "未启动";

    [ObservableProperty]
    private string partialTranscript = "";

    [ObservableProperty]
    private string finalTranscript = "";

    [ObservableProperty]
    private string aiReply = "";

    [ObservableProperty]
    private string statusMessage = "准备就绪。";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private bool isPaused;

    [ObservableProperty]
    private AudioDeviceInfo? selectedCaptureRenderDevice;

    public ObservableCollection<AudioDeviceInfo> RenderDevices { get; } = [];

    public FullAiReplyViewModel(
        IConfigService configService,
        IAudioCaptureService captureService,
        IRealtimeTranscriptionService transcriptionService,
        ILlmService llmService,
        ITtsService ttsService,
        IAudioPlaybackService playbackService,
        AudioDeviceService audioDeviceService,
        ILoggingService logger)
    {
        _configService = configService;
        _captureService = captureService;
        _transcriptionService = transcriptionService;
        _llmService = llmService;
        _ttsService = ttsService;
        _playbackService = playbackService;
        _audioDeviceService = audioDeviceService;
        _logger = logger;

        _transcriptionService.PartialTranscriptReceived += OnPartialTranscriptReceived;
        _transcriptionService.FinalTranscriptReceived += OnFinalTranscriptReceived;
        _transcriptionService.ErrorOccurred += OnTranscriptionError;
        RefreshDevices();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        _runCts = new CancellationTokenSource();
        try
        {
            State = "正在监听";
            IsRunning = true;
            IsPaused = false;
            await _captureService.StartLoopbackAsync(SelectedCaptureRenderDevice?.Id ?? _configService.Config.Audio.CaptureRenderDeviceId, _runCts.Token);
            await _transcriptionService.StartAsync(_runCts.Token);
            StatusMessage = "完全 AI 回复已启动。";
        }
        catch (Exception ex)
        {
            _logger.Error("完全 AI 回复启动失败", ex);
            State = "出错";
            StatusMessage = ex.Message;
            IsRunning = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        _runCts?.Cancel();
        await _transcriptionService.StopAsync();
        await _captureService.StopAsync();
        await _playbackService.StopAsync();
        IsRunning = false;
        IsPaused = false;
        State = "已停止";
        StatusMessage = "完全 AI 回复已停止。";
    }

    [RelayCommand]
    private void PauseListening()
    {
        IsPaused = !IsPaused;
        State = IsPaused ? "暂停监听" : "正在监听";
    }

    [RelayCommand]
    private void ClearContext()
    {
        _llmService.ClearContext();
        PartialTranscript = "";
        FinalTranscript = "";
        AiReply = "";
        StatusMessage = "上下文和当前转录已清空。";
    }

    [RelayCommand]
    private async Task TestAsrAsync()
    {
        await StartAsync();
        StatusMessage = "ASR 测试已启动，请播放一段声音并观察 partial/final transcript。";
    }

    [RelayCommand]
    private async Task TestLlmAsync()
    {
        try
        {
            State = "正在请求 LLM";
            AiReply = await _llmService.TestAsync(CancellationToken.None);
            State = IsRunning ? "正在监听" : "未启动";
            StatusMessage = "LLM 测试成功。";
        }
        catch (Exception ex)
        {
            _logger.Error("LLM 测试失败", ex);
            State = "出错";
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task TestTtsAsync()
    {
        try
        {
            var voice = _configService.VoiceProfiles.FirstOrDefault(v => v.Id == _configService.Config.FishAudio.DefaultVoiceProfileId)
                ?? _configService.VoiceProfiles.First();
            State = "正在生成语音";
            var audio = await _ttsService.GenerateSpeechAsync("TTS 测试成功。", voice, _configService.Config.FishAudio.DefaultModel, CancellationToken.None);
            StatusMessage = $"TTS 测试成功：{audio}";
            State = IsRunning ? "正在监听" : "未启动";
        }
        catch (Exception ex)
        {
            _logger.Error("TTS 测试失败", ex);
            State = "出错";
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task TestPlaybackAsync()
    {
        try
        {
            var voice = _configService.VoiceProfiles.FirstOrDefault(v => v.Id == _configService.Config.FishAudio.DefaultVoiceProfileId)
                ?? _configService.VoiceProfiles.First();
            State = "正在生成语音";
            var audio = await _ttsService.GenerateSpeechAsync("播放到 VB-CABLE 测试成功。", voice, _configService.Config.FishAudio.DefaultModel, CancellationToken.None);
            State = "正在播放到 VB-CABLE";
            await _playbackService.PlayAsync(audio, _configService.Config.Audio.TtsOutputDeviceId, _configService.Config.Audio.MonitorDeviceId, _configService.Config.Audio.MonitorLocally, CancellationToken.None);
            State = IsRunning ? "正在监听" : "未启动";
            StatusMessage = "播放到 VB-CABLE 测试完成。";
        }
        catch (Exception ex)
        {
            _logger.Error("播放到 VB-CABLE 测试失败", ex);
            State = "出错";
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        RenderDevices.Clear();
        foreach (var device in _audioDeviceService.GetRenderDevices())
        {
            RenderDevices.Add(device);
        }

        SelectedCaptureRenderDevice = RenderDevices.FirstOrDefault(d => d.Id == _configService.Config.Audio.CaptureRenderDeviceId)
            ?? RenderDevices.FirstOrDefault(d => d.IsDefault);
    }

    private void OnPartialTranscriptReceived(object? sender, PartialTranscriptEventArgs e)
    {
        if (IsPaused || _suppressFinalTranscript)
        {
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            State = "正在识别";
            PartialTranscript = e.Text;
        });
    }

    private async void OnFinalTranscriptReceived(object? sender, FinalTranscriptEventArgs e)
    {
        if (IsPaused || _suppressFinalTranscript || _isProcessingFinal || string.IsNullOrWhiteSpace(e.Text))
        {
            return;
        }

        _isProcessingFinal = true;
        try
        {
            await ProcessFinalTranscriptAsync(e.Text);
        }
        finally
        {
            _isProcessingFinal = false;
        }
    }

    private void OnTranscriptionError(object? sender, TranscriptionErrorEventArgs e)
    {
        _logger.Error(e.Message, e.Exception);
        Application.Current.Dispatcher.Invoke(() =>
        {
            State = "出错";
            StatusMessage = e.Message;
        });
    }

    private async Task ProcessFinalTranscriptAsync(string text)
    {
        var token = _runCts?.Token ?? CancellationToken.None;
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            FinalTranscript = text;
            State = "正在请求 LLM";
            StatusMessage = "收到最终转录，正在生成 AI 回复。";
        });

        var reply = await _llmService.GenerateReplyAsync(text, token);
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            AiReply = reply;
            State = "正在生成语音";
        });

        var voice = _configService.VoiceProfiles.FirstOrDefault(v => v.Id == _configService.Config.FishAudio.DefaultVoiceProfileId)
            ?? _configService.VoiceProfiles.First();
        var audio = await _ttsService.GenerateSpeechAsync(reply, voice, _configService.Config.FishAudio.DefaultModel, token);

        _suppressFinalTranscript = _configService.Config.FullAiReply.PauseAsrWhilePlaying;
        await Application.Current.Dispatcher.InvokeAsync(() => State = "正在播放到 VB-CABLE");
        try
        {
            await _playbackService.PlayAsync(audio, _configService.Config.Audio.TtsOutputDeviceId, _configService.Config.Audio.MonitorDeviceId, _configService.Config.Audio.MonitorLocally, token);
        }
        finally
        {
            _suppressFinalTranscript = false;
        }

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            State = IsRunning ? "正在监听" : "已停止";
            StatusMessage = "一轮完整 AI 回复已完成。";
        });
    }
}
