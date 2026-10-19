using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.SocialListening;

public class FileSocialMonitorStoreTests
{
    [Test]
    public async Task SaveAsync_PersistsSubscriptionAndScopesItToOwner()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sentia-monitors-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileSocialMonitorStore(
                Options.Create(new SocialMonitorStoreOptions { FilePath = path })
            );
            var subscription = new SocialMonitorSubscription(
                "monitor-1",
                "user-1",
                true,
                60,
                10,
                "x",
                "educación",
                "ULEAM AND Manta",
                new Dictionary<string, string> { ["x"] = "https://x.com/uleam" },
                DateTimeOffset.UtcNow.AddMinutes(60)
            );

            await store.SaveAsync(subscription, CancellationToken.None);

            var loaded = await store.GetForOwnerAsync("user-1", CancellationToken.None);
            loaded.ShouldHaveSingleItem();
            loaded[0].Topic.ShouldBe(subscription.Topic);
            loaded[0].Search.ShouldBe(subscription.Search);
            (await store.GetForOwnerAsync("user-2", CancellationToken.None)).ShouldBeEmpty();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
