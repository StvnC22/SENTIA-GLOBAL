using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Application.FunctionalTests.SocialListening.Queries;

public class GetSocialCommentsTests : TestBase
{
    [Test]
    public async Task ShouldFilterCommentsByPlatformAndCalculateSummary()
    {
        var result = await TestApp.SendAsync(new GetSocialCommentsQuery("instagram"));

        result.Comments.ShouldNotBeEmpty();
        result.Comments.ShouldAllBe(comment => comment.Platform == "instagram");
        result.Summary.Total.ShouldBe(result.Comments.Count);
        result.Summary.Positive.ShouldBeGreaterThan(0);
        result.Summary.Negative.ShouldBeGreaterThan(0);
        result.Insights.ShouldNotBeNull();
        result.Insights!.ExecutiveSummary.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task ShouldFilterByBooleanTopicAndTerritory()
    {
        var result = await TestApp.SendAsync(
            new GetSocialCommentsQuery(
                Topic: "ULEAM O Manabí",
                Province: "Manabí",
                Canton: "Portoviejo"
            )
        );

        result.Comments.ShouldNotBeEmpty();
        result.Comments.ShouldAllBe(comment =>
            string.Equals(comment.Canton, "Portoviejo", StringComparison.OrdinalIgnoreCase)
        );
        result.Insights!.GeoRanking.ShouldNotBeEmpty();
    }
}
