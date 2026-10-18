using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Preprocessing;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.SocialListening;

public class CommentPreprocessorTests
{
    [Test]
    public void Clean_EliminaSpamDuplicadosYBots()
    {
        var drafts = new[]
        {
            new SocialCommentDraft(
                "1",
                "instagram",
                "Ana",
                "@ana",
                "https://instagram.com/a",
                "Excelente universidad en Manabí",
                "https://instagram.com/p/1",
                "Post",
                "https://instagram.com/p/1",
                DateTimeOffset.UtcNow
            ),
            new SocialCommentDraft(
                "1",
                "instagram",
                "Ana",
                "@ana",
                "https://instagram.com/a",
                "Excelente universidad en Manabí",
                "https://instagram.com/p/1",
                "Post",
                "https://instagram.com/p/1",
                DateTimeOffset.UtcNow
            ),
            new SocialCommentDraft(
                "2",
                "instagram",
                "Bot",
                "@user99887766",
                "https://instagram.com/b",
                "Gana dinero YA!!!! haz clic aqui",
                "https://instagram.com/p/2",
                "Spam",
                "https://instagram.com/p/2",
                DateTimeOffset.UtcNow,
                IsLikelyBot: true
            ),
        };

        var cleaned = CommentPreprocessor.Clean(drafts);
        cleaned.Count.ShouldBe(1);
        cleaned[0].Id.ShouldBe("1");
    }
}
