using AnalisisSentimiento.Application.Common.Exceptions;
using AnalisisSentimiento.Application.SocialListening.Commands.CollectSocialComments;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.SocialListening.Commands;

public class CollectSocialCommentsTests : TestBase
{
    private const string InstagramUrl = "https://www.instagram.com/uleam_ecuador_oficial/";

    [Test]
    public async Task ShouldStoreLiveOpinionsWithoutDuplicates()
    {
        await TestApp.RunAsUserAsync("analista@test", "Analista1234!", [Roles.Analyst]);

        var first = await TestApp.SendAsync(
            new CollectSocialCommentsCommand("instagram", InstagramUrl)
        );
        first.IsSuccess.ShouldBeTrue();
        first.Value.DataSource.ShouldBe("live");
        var stored = await TestApp.CountAsync<CollectedOpinion>();
        stored.ShouldBe(first.Value.Comments.Count);
        stored.ShouldBeGreaterThan(0);

        await TestApp.SendAsync(new CollectSocialCommentsCommand("instagram", InstagramUrl));

        (await TestApp.CountAsync<CollectedOpinion>()).ShouldBe(stored);
    }

    [Test]
    public async Task ShouldRejectConsultaRole()
    {
        await TestApp.RunAsUserAsync("consulta@test", "Consulta1234!", [Roles.Consulta]);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            TestApp.SendAsync(new CollectSocialCommentsCommand("instagram", InstagramUrl))
        );
        (await TestApp.CountAsync<CollectedOpinion>()).ShouldBe(0);
    }
}
