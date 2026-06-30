using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIVoiceCable.Interfaces;
using AIVoiceCable.Models;

namespace AIVoiceCable.Services;

public sealed class SecretService : ISecretService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SecretConfig Secrets { get; private set; } = new();

    private static string AppDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIVoiceCable");
    private static string SecretPath => Path.Combine(AppDataDirectory, "secrets.json");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataDirectory);
        if (!File.Exists(SecretPath))
        {
            Secrets = new SecretConfig();
            await SaveAsync(cancellationToken);
            return;
        }

        try
        {
            var encryptedJson = await File.ReadAllTextAsync(SecretPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(encryptedJson))
            {
                Secrets = new SecretConfig();
                return;
            }

            var protectedPayload = JsonSerializer.Deserialize<ProtectedSecretFile>(encryptedJson, _jsonOptions);
            if (protectedPayload?.Payload is null)
            {
                Secrets = new SecretConfig();
                return;
            }

            var bytes = Convert.FromBase64String(protectedPayload.Payload);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plain);
            Secrets = JsonSerializer.Deserialize<SecretConfig>(json, _jsonOptions) ?? new SecretConfig();
        }
        catch
        {
            var backupPath = $"{SecretPath}.bad.{DateTimeOffset.Now:yyyyMMddHHmmss}";
            File.Copy(SecretPath, backupPath, overwrite: true);
            Secrets = new SecretConfig();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDataDirectory);
        var json = JsonSerializer.Serialize(Secrets, _jsonOptions);
        var plain = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        var envelope = new ProtectedSecretFile(Convert.ToBase64String(protectedBytes));
        await File.WriteAllTextAsync(SecretPath, JsonSerializer.Serialize(envelope, _jsonOptions), cancellationToken);
    }

    private sealed record ProtectedSecretFile(string Payload);
}
