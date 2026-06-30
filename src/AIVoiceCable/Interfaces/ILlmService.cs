namespace AIVoiceCable.Interfaces;

public interface ILlmService
{
    Task<string> GenerateReplyAsync(string userText, CancellationToken cancellationToken);
    Task<string> TestAsync(CancellationToken cancellationToken);
    void ClearContext();
}
