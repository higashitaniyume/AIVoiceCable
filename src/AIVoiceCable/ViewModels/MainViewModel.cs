using CommunityToolkit.Mvvm.ComponentModel;

namespace AIVoiceCable.ViewModels;

public sealed partial class MainViewModel(
    CustomReplyViewModel customReply,
    CustomAudioReplyViewModel customAudioReply,
    FullAiReplyViewModel fullAiReply,
    VoiceProfilesViewModel voiceProfiles,
    ApiSettingsViewModel apiSettings,
    AudioSettingsViewModel audioSettings,
    LogsViewModel logs) : ObservableObject
{
    [ObservableProperty]
    private int selectedTabIndex;

    public CustomReplyViewModel CustomReply { get; } = customReply;
    public CustomAudioReplyViewModel CustomAudioReply { get; } = customAudioReply;
    public FullAiReplyViewModel FullAiReply { get; } = fullAiReply;
    public VoiceProfilesViewModel VoiceProfiles { get; } = voiceProfiles;
    public ApiSettingsViewModel ApiSettings { get; } = apiSettings;
    public AudioSettingsViewModel AudioSettings { get; } = audioSettings;
    public LogsViewModel Logs { get; } = logs;
}
