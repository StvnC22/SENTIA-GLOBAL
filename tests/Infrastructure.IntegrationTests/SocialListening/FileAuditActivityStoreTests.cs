using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Infrastructure.SocialListening;
using Microsoft.Extensions.Options;

namespace AnalisisSentimiento.Infrastructure.IntegrationTests.SocialListening;

public class FileAuditActivityStoreTests
{
    [Test]
    public async Task AppendAsync_StoresRecentActivityWithoutRequestContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sentia-audit-{Guid.NewGuid():N}.jsonl");
        try
        {
            var store = new FileAuditActivityStore(
                Options.Create(new AuditActivityStoreOptions { FilePath = path })
            );
            var activity = new AuditActivity(
                DateTimeOffset.UtcNow,
                "user-1",
                "GET",
                "/api/social-comments",
                200
            );

            await store.AppendAsync(activity, CancellationToken.None);

            (await store.GetRecentAsync(10, CancellationToken.None)).ShouldBe([activity]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
