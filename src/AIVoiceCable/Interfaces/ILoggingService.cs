using System.Collections.ObjectModel;
using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface ILoggingService
{
    ObservableCollection<LogEntry> Entries { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
    string RedactSecrets(string message);
}
