using NAudio.Wave;

namespace AIVoiceCable.Models;

public sealed class AudioCapturedEventArgs(byte[] buffer, int bytesRecorded, WaveFormat waveFormat) : EventArgs
{
    public byte[] Buffer { get; } = buffer;
    public int BytesRecorded { get; } = bytesRecorded;
    public WaveFormat WaveFormat { get; } = waveFormat;
}

public sealed class PartialTranscriptEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public sealed class FinalTranscriptEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public sealed class TranscriptionErrorEventArgs(string message, Exception? exception = null) : EventArgs
{
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}
