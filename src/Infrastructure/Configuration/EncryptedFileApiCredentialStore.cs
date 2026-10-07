using System.Security.Cryptography;
using System.Text.Json;
using AnalisisSentimiento.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace AnalisisSentimiento.Infrastructure.Configuration;

public sealed class EncryptedFileApiCredentialStore(
    IDataProtectionProvider dataProtectionProvider,
    IConfiguration configuration
) : IApiCredentialStore
{
    private const string ProtectorPurpose = "EmocionesULEAM.ApiCredentials.v1";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        ProtectorPurpose
    );
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath = ResolveFilePath(configuration);

    public async Task<ApiCredentials> GetAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
            {
                return ApiCredentials.Empty;
            }

            try
            {
                var protectedPayload = await File.ReadAllTextAsync(_filePath, cancellationToken);
                var json = _protector.Unprotect(protectedPayload);
                return JsonSerializer.Deserialize<ApiCredentials>(json) ?? ApiCredentials.Empty;
            }
            catch (CryptographicException)
            {
                return ApiCredentials.Empty;
            }
            catch (JsonException)
            {
                return ApiCredentials.Empty;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ApiCredentials credentials, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(credentials);
            var protectedPayload = _protector.Protect(json);
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, protectedPayload, cancellationToken);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "No se pudo escribir el archivo de credenciales cifradas en el equipo.",
                exception
            );
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string ResolveFilePath(IConfiguration configuration)
    {
        var configuredPath = configuration["ApiCredentials:Path"];
        return string.IsNullOrWhiteSpace(configuredPath)
            ? LocalAppStorage.GetCredentialsFilePath()
            : Path.GetFullPath(configuredPath);
    }
}
