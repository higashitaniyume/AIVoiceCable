using AIVoiceCable.Models;

namespace AIVoiceCable.Interfaces;

public interface ISecretService
{
    SecretConfig Secrets { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
