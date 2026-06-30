using System.Collections.ObjectModel;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIVoiceCable.ViewModels;

public sealed partial class ApiSettingsViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly ISecretService _secretService;
    private readonly ILlmService _llmService;
    private readonly ILoggingService _logger;

    [ObservableProperty]
    private string fishAudioApiKey = "";

    [ObservableProperty]
    private string fishAudioBaseUrl = "";

    [ObservableProperty]
    private string fishAudioDefaultModel = "";

    [ObservableProperty]
    private string fishAudioFallbackModel = "";

    [ObservableProperty]
    private string assemblyAiApiKey = "";

    [ObservableProperty]
    private string assemblyAiEndpoint = "";

    [ObservableProperty]
    private LlmProviderConfig? selectedProvider;

    [ObservableProperty]
    private string selectedProviderApiKey = "";

    [ObservableProperty]
    private string statusMessage = "";

    public ObservableCollection<LlmProviderConfig> LlmProviders { get; } = [];

    public ApiSettingsViewModel(IConfigService configService, ISecretService secretService, ILlmService llmService, ILoggingService logger)
    {
        _configService = configService;
        _secretService = secretService;
        _llmService = llmService;
        _logger = logger;

        FishAudioApiKey = _secretService.Secrets.FishAudioApiKey ?? "";
        FishAudioBaseUrl = _configService.Config.FishAudio.BaseUrl;
        FishAudioDefaultModel = _configService.Config.FishAudio.DefaultModel;
        FishAudioFallbackModel = _configService.Config.FishAudio.FallbackModelName;
        AssemblyAiApiKey = _secretService.Secrets.AssemblyAiApiKey ?? "";
        AssemblyAiEndpoint = _configService.Config.AssemblyAi.WebSocketEndpoint;
        RefreshProviders();
    }

    partial void OnSelectedProviderChanged(LlmProviderConfig? value)
    {
        SelectedProviderApiKey = value is not null && _secretService.Secrets.LlmApiKeys.TryGetValue(value.Id, out var key) ? key : "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _configService.Config.FishAudio.BaseUrl = FishAudioBaseUrl.Trim();
        _configService.Config.FishAudio.DefaultModel = FishAudioDefaultModel.Trim();
        _configService.Config.FishAudio.FallbackModelName = FishAudioFallbackModel.Trim();
        _configService.Config.AssemblyAi.WebSocketEndpoint = AssemblyAiEndpoint.Trim();

        _secretService.Secrets.FishAudioApiKey = FishAudioApiKey.Trim();
        _secretService.Secrets.AssemblyAiApiKey = AssemblyAiApiKey.Trim();
        if (SelectedProvider is not null)
        {
            _secretService.Secrets.LlmApiKeys[SelectedProvider.Id] = SelectedProviderApiKey.Trim();
        }

        if (SelectedProvider is not null && SelectedProvider.IsDefault)
        {
            _configService.Config.DefaultLlmProviderId = SelectedProvider.Id;
        }

        await _configService.SaveConfigAsync();
        await _secretService.SaveAsync();
        _logger.Info("API 设置已保存");
        StatusMessage = "API 设置已保存。API Key 已通过 Windows DPAPI 加密保存。";
    }

    [RelayCommand]
    private void AddProvider()
    {
        var provider = new LlmProviderConfig { Name = "新的 OpenAI-compatible 服务", BaseUrl = "https://api.example.com/v1/" };
        _configService.Config.LlmProviders.Add(provider);
        LlmProviders.Add(provider);
        SelectedProvider = provider;
        StatusMessage = "已新增 LLM 服务商，请填写 Base URL、模型和 API Key。";
    }

    [RelayCommand]
    private async Task DeleteProviderAsync()
    {
        if (SelectedProvider is null || _configService.Config.LlmProviders.Count <= 1)
        {
            StatusMessage = "至少保留一个 LLM 服务商。";
            return;
        }

        _secretService.Secrets.LlmApiKeys.Remove(SelectedProvider.Id);
        _configService.Config.LlmProviders.Remove(SelectedProvider);
        if (_configService.Config.DefaultLlmProviderId == SelectedProvider.Id)
        {
            _configService.Config.DefaultLlmProviderId = _configService.Config.LlmProviders[0].Id;
            _configService.Config.LlmProviders[0].IsDefault = true;
        }

        await _configService.SaveConfigAsync();
        await _secretService.SaveAsync();
        RefreshProviders();
        StatusMessage = "LLM 服务商已删除。";
    }

    [RelayCommand]
    private async Task SetDefaultProviderAsync()
    {
        if (SelectedProvider is null)
        {
            return;
        }

        foreach (var provider in _configService.Config.LlmProviders)
        {
            provider.IsDefault = provider.Id == SelectedProvider.Id;
        }

        _configService.Config.DefaultLlmProviderId = SelectedProvider.Id;
        await _configService.SaveConfigAsync();
        RefreshProviders(SelectedProvider.Id);
        StatusMessage = $"默认 LLM 服务商已设置为：{SelectedProvider.Name}";
    }

    [RelayCommand]
    private async Task TestLlmAsync()
    {
        try
        {
            await SaveAsync();
            var result = await _llmService.TestAsync(CancellationToken.None);
            StatusMessage = $"LLM 测试成功：{result}";
        }
        catch (Exception ex)
        {
            _logger.Error("LLM 测试失败", ex);
            StatusMessage = ex.Message;
        }
    }

    private void RefreshProviders(string? selectedId = null)
    {
        LlmProviders.Clear();
        foreach (var provider in _configService.Config.LlmProviders)
        {
            provider.IsDefault = provider.Id == _configService.Config.DefaultLlmProviderId;
            LlmProviders.Add(provider);
        }

        SelectedProvider = LlmProviders.FirstOrDefault(p => p.Id == selectedId)
            ?? LlmProviders.FirstOrDefault(p => p.Id == _configService.Config.DefaultLlmProviderId)
            ?? LlmProviders.FirstOrDefault();
    }
}
