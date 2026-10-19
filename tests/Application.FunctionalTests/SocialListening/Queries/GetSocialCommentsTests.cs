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
    }

    [Test]
    public async Task ShouldApplyBooleanSearchToComments()
    {
        var all = await TestApp.SendAsync(new GetSocialCommentsQuery("instagram"));
        var selected = all.Comments.First();
        var result = await TestApp.SendAsync(
            new GetSocialCommentsQuery(
                "instagram",
                Search: $"\"{selected.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]}\" AND NOT imposible"
            )
        );

        result.Comments.ShouldContain(comment => comment.Id == selected.Id);
        result.Comments.ShouldAllBe(comment => comment.Platform == "instagram");
    }
}
