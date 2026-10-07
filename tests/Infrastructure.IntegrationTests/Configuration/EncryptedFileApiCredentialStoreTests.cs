using AnalisisSentimiento.Infrastructure.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.Configuration;

public class EncryptedFileApiCredentialStoreTests
{
    private string _testDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"emociones-uleam-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Test]
    public async Task ShouldEncryptCredentialsAtRestAndRestoreThem()
    {
        var credentialPath = Path.Combine(_testDirectory, "credentials.dat");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ApiCredentials:Path"] = credentialPath }
            )
            .Build();
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(_testDirectory, "protection-keys"))
        );
        var store = new EncryptedFileApiCredentialStore(provider, configuration);
        var credentials = new ApiCredentials(
            "instagram-secret-value",
            "facebook-secret-value",
            null,
            null,
            "ai-secret-value",
            DateTimeOffset.UtcNow
        );

        await store.SaveAsync(credentials, CancellationToken.None);
        var payload = await File.ReadAllTextAsync(credentialPath);
        var restored = await store.GetAsync(CancellationToken.None);

        Assert.That(payload, Does.Not.Contain("instagram-secret-value"));
        Assert.That(payload, Does.Not.Contain("ai-secret-value"));
        Assert.That(restored, Is.EqualTo(credentials));
    }

    [Test]
    public async Task ShouldReturnEmptyCredentialsWhenStoredPayloadCannotBeRead()
    {
        var credentialPath = Path.Combine(_testDirectory, "credentials.dat");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ApiCredentials:Path"] = credentialPath }
            )
            .Build();
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(Path.Combine(_testDirectory, "protection-keys"))
        );
        var store = new EncryptedFileApiCredentialStore(provider, configuration);

        await File.WriteAllTextAsync(credentialPath, "not-a-valid-protected-payload");
        var restored = await store.GetAsync(CancellationToken.None);

        Assert.That(restored, Is.EqualTo(ApiCredentials.Empty));
    }
}
