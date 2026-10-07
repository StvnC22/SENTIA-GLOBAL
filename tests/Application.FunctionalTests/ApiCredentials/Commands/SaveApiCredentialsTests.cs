using AnalisisSentimiento.Application.ApiCredentials.Commands.SaveApiCredentials;

namespace AnalisisSentimiento.Application.FunctionalTests.ApiCredentials.Commands;

public class SaveApiCredentialsTests : TestBase
{
    [Test]
    public async Task ShouldSaveKeysAndReturnOnlyConfigurationStatus()
    {
        var command = new SaveApiCredentialsCommand(
            "instagram-secret",
            null,
            "tiktok-secret",
            null,
            "ai-secret"
        );
        var result = await TestApp.SendAsync(command);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Instagram.ShouldBeTrue();
        result.Value.Facebook.ShouldBeFalse();
        result.Value.TikTok.ShouldBeTrue();
        result.Value.ArtificialIntelligence.ShouldBeTrue();
        command.ToString().ShouldNotContain("instagram-secret");
        command.ToString().ShouldContain("[REDACTED]");
    }

    [Test]
    public async Task ShouldIgnoreMaskedPlaceholdersWhenSaving()
    {
        await TestApp.SendAsync(
            new SaveApiCredentialsCommand("instagram-secret", null, null, "apify-secret", null)
        );

        var result = await TestApp.SendAsync(
            new SaveApiCredentialsCommand(
                "••••••••••••••••",
                null,
                null,
                "apif••••••••2Qge",
                "AIza••••••••"
            )
        );

        result.IsSuccess.ShouldBeTrue();
        result.Value.Instagram.ShouldBeTrue();
        result.Value.X.ShouldBeTrue();
        result.Value.ArtificialIntelligence.ShouldBeFalse();
    }
}
