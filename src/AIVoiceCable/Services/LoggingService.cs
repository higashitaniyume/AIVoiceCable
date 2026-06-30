using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly object _fileGate = new();
    private readonly string _logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIVoiceCable", "logs");

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public void Info(string message) => Write("INFO", message, null);
    public void Warn(string message) => Write("WARN", message, null);
    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public string RedactSecrets(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            message,
            @"(?i)(api[-_ ]?key|authorization|bearer)\s*[:= ]\s*([A-Za-z0-9._\-]{8,})",
            "$1=***");
    }

    private void Write(string level, string message, Exception? exception)
    {
        Directory.CreateDirectory(_logsDirectory);
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Message = RedactSecrets(message),
            Exception = exception?.ToString()
        };

        void AddEntry()
        {
            Entries.Add(entry);
            while (Entries.Count > 1000)
            {
                Entries.RemoveAt(0);
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AddEntry();
        }
        else
        {
            dispatcher.Invoke(AddEntry);
        }

        var line = entry.Exception is null ? entry.Display : $"{entry.Display}{Environment.NewLine}{entry.Exception}";
        var path = Path.Combine(_logsDirectory, $"{DateTimeOffset.Now:yyyy-MM-dd}.log");
        lock (_fileGate)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
