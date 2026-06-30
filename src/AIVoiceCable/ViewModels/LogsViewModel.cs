using System.Collections.ObjectModel;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIVoiceCable.ViewModels;

public sealed partial class LogsViewModel(ILoggingService loggingService) : ObservableObject
{
    public ObservableCollection<LogEntry> Entries => loggingService.Entries;

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
    }
}
