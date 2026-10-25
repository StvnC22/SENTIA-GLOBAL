using AnalisisSentimiento.Application.SocialListening.Queries.SearchOpinions;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.SocialListening.Queries;

public class SearchOpinionsTests : TestBase
{
    [Test]
    public async Task ShouldFallBackToDemoWhenCorpusIsEmpty()
    {
        await TestApp.RunAsUserAsync("consulta@test", "Consulta1234!", [Roles.Consulta]);

        var result = await TestApp.SendAsync(new SearchOpinionsQuery("ULEAM"));

        result.DataSource.ShouldBe("demo");
        result.Message.ShouldNotBeNullOrWhiteSpace();
        result.Comments.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldSearchCollectedOpinionsWithOperatorsAndTerritory()
    {
        await TestApp.RunAsUserAsync("consulta@test", "Consulta1234!", [Roles.Consulta]);
        await TestApp.AddAsync(
            Opinion("ig-1", "instagram", "Orgullo ULEAM en Portoviejo", "positive", "Portoviejo")
        );
        await TestApp.AddAsync(
            Opinion(
                "ig-2",
                "instagram",
                "ULEAM: el sistema no funciona en Chone",
                "negative",
                "Chone"
            )
        );
        await TestApp.AddAsync(
            Opinion("tt-1", "tiktok", "Lindo video de la universidad", "positive", null)
        );

        var all = await TestApp.SendAsync(new SearchOpinionsQuery());
        all.DataSource.ShouldBe("live");
        all.Comments.Count.ShouldBe(3);
        all.Insights!.NetworkBreakdown.ShouldNotBeEmpty();

        var onlyTikTok = await TestApp.SendAsync(new SearchOpinionsQuery(Platform: "tiktok"));
        onlyTikTok.Comments.ShouldHaveSingleItem().Id.ShouldBe("tt-1");

        var excluded = await TestApp.SendAsync(new SearchOpinionsQuery("ULEAM NO Chone"));
        excluded.Comments.ShouldHaveSingleItem().Id.ShouldBe("ig-1");

        var implicitAnd = await TestApp.SendAsync(new SearchOpinionsQuery("ULEAM sistema"));
        implicitAnd.Comments.ShouldHaveSingleItem().Id.ShouldBe("ig-2");

        var portoviejo = await TestApp.SendAsync(
            new SearchOpinionsQuery(Province: "Manabí", Canton: "Portoviejo")
        );
        portoviejo.Comments.ShouldHaveSingleItem().Id.ShouldBe("ig-1");
        portoviejo.Comments.Single().CommentUrl.ShouldBe("https://example.org/ig-1");
    }

    [Test]
    public async Task ShouldExplainWhenNothingMatches()
    {
        await TestApp.RunAsUserAsync("consulta@test", "Consulta1234!", [Roles.Consulta]);
        await TestApp.AddAsync(
            Opinion("ig-1", "instagram", "Orgullo ULEAM", "positive", "Portoviejo")
        );

        var result = await TestApp.SendAsync(new SearchOpinionsQuery("Quito"));

        result.Comments.ShouldBeEmpty();
        result.DataSource.ShouldBe("live");
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    private static CollectedOpinion Opinion(
        string id,
        string platform,
        string text,
        string sentiment,
        string? canton
    )
    {
        var opinion = CollectedOpinion.Create(
            platform,
            id,
            "https://example.org/perfil",
            DateTimeOffset.UtcNow
        );
        opinion.Update(
            "Autor",
            "@autor",
            "https://example.org/autor",
            text,
            $"https://example.org/{id}",
            "Publicación",
            "https://example.org/post",
            DateTimeOffset.UtcNow.AddDays(-1),
            canton is null ? null : "Manabí",
            canton,
            canton,
            3,
            sentiment,
            sentiment == "positive" ? "Positivo" : "Negativo",
            85,
            DateTimeOffset.UtcNow
        );
        return opinion;
    }
}
