using System.Windows;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Services;
using AIVoiceCable.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AIVoiceCable;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var config = _serviceProvider.GetRequiredService<IConfigService>();
        await config.LoadAsync();

        var logger = _serviceProvider.GetRequiredService<ILoggingService>();
        logger.Info("应用启动");

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISecretService, SecretService>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<RetryPolicyService>();
        services.AddSingleton<AudioDeviceService>();
        services.AddSingleton<VbCableService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IAudioCaptureService, SystemAudioCaptureService>();
        services.AddSingleton<ITtsService, FishAudioService>();
        services.AddSingleton<ILlmService, LlmService>();
        services.AddSingleton<IRealtimeTranscriptionService, AssemblyAiRealtimeTranscriptionService>();
        services.AddSingleton<ReplyHistoryService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<CustomReplyViewModel>();
        services.AddSingleton<FullAiReplyViewModel>();
        services.AddSingleton<VoiceProfilesViewModel>();
        services.AddSingleton<ApiSettingsViewModel>();
        services.AddSingleton<AudioSettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
