using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Infrastructure.SocialListening.Apify;

public sealed class DemoSocialCommentProvider : ISocialCommentProvider
{
    private static readonly SocialCommentDraft[] Comments =
    [
        new(
            "ig-1",
            "instagram",
            "María Zambrano",
            "@maria.zambrano",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "¡Qué orgullo ver cómo sigue creciendo nuestra universidad! 💙",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Admisiones ULEAM 2026",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            new DateTimeOffset(2026, 8, 21, 14, 32, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "ig-2",
            "instagram",
            "Carlos Mera",
            "@carlosmera.ec",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "¿Dónde puedo revisar los requisitos para la matrícula?",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Proceso de matriculación",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            new DateTimeOffset(2026, 8, 21, 11, 8, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "ig-3",
            "instagram",
            "Andrea Ponce",
            "@andreaponce",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Llevo días intentando entrar al sistema y todavía no funciona.",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Servicios digitales ULEAM",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            new DateTimeOffset(2026, 8, 20, 18, 45, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "fb-1",
            "facebook",
            "José Luis Cedeño",
            "José Luis Cedeño",
            "https://www.facebook.com/UleamEc",
            "Excelente iniciativa para apoyar a los estudiantes de Manabí.",
            "https://www.facebook.com/UleamEc",
            "Becas y ayudas estudiantiles",
            "https://www.facebook.com/UleamEc",
            new DateTimeOffset(2026, 8, 21, 9, 20, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "fb-2",
            "facebook",
            "Rosa Delgado",
            "Rosa Delgado",
            "https://www.facebook.com/UleamEc",
            "La información está clara, gracias por compartir.",
            "https://www.facebook.com/UleamEc",
            "Calendario académico",
            "https://www.facebook.com/UleamEc",
            new DateTimeOffset(2026, 8, 20, 16, 5, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "fb-3",
            "facebook",
            "Miguel Alcívar",
            "Miguel Alcívar",
            "https://www.facebook.com/UleamEc",
            "No responden los mensajes y necesito resolver mi trámite urgente.",
            "https://www.facebook.com/UleamEc",
            "Atención al estudiante",
            "https://www.facebook.com/UleamEc",
            new DateTimeOffset(2026, 8, 19, 13, 41, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "tt-1",
            "tiktok",
            "Vale C.",
            "@valec",
            "https://www.tiktok.com/@uleamecuador",
            "La mejor universidad de Manabí, qué lindo video 😍",
            "https://www.tiktok.com/@uleamecuador",
            "Así se vive la ULEAM",
            "https://www.tiktok.com/@uleamecuador",
            new DateTimeOffset(2026, 8, 21, 15, 2, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "tt-2",
            "tiktok",
            "Mateo",
            "@mateo.ec",
            "https://www.tiktok.com/@uleamecuador",
            "¿La carrera también está disponible en la extensión Chone?",
            "https://www.tiktok.com/@uleamecuador",
            "Conoce nuestra oferta académica",
            "https://www.tiktok.com/@uleamecuador",
            new DateTimeOffset(2026, 8, 20, 20, 17, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "tt-3",
            "tiktok",
            "Emi",
            "@emi_05",
            "https://www.tiktok.com/@uleamecuador",
            "No explican cuándo inicia el proceso, así es imposible organizarse.",
            "https://www.tiktok.com/@uleamecuador",
            "Conoce nuestra oferta académica",
            "https://www.tiktok.com/@uleamecuador",
            new DateTimeOffset(2026, 8, 19, 19, 33, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "x-1",
            "x",
            "Eduardo Vera",
            "@eduveram",
            "https://x.com/UleamEcuador",
            "Gran noticia para la investigación y la comunidad universitaria.",
            "https://x.com/UleamEcuador",
            "Nuevo convenio de investigación",
            "https://x.com/UleamEcuador",
            new DateTimeOffset(2026, 8, 21, 12, 14, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "x-2",
            "x",
            "Ana Sofía",
            "@anasofiag",
            "https://x.com/UleamEcuador",
            "¿Hay transmisión en vivo del evento?",
            "https://x.com/UleamEcuador",
            "Encuentro académico 2026",
            "https://x.com/UleamEcuador",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(-5))
        ),
        new(
            "x-3",
            "x",
            "Diego Macías",
            "@diegomacias",
            "https://x.com/UleamEcuador",
            "Otra vez cambiaron el horario sin avisar. Muy mala organización.",
            "https://x.com/UleamEcuador",
            "Encuentro académico 2026",
            "https://x.com/UleamEcuador",
            new DateTimeOffset(2026, 8, 19, 8, 52, 0, TimeSpan.FromHours(-5))
        ),
    ];

    public Task<SocialCommentFetchResult> FetchAsync(
        string? platform,
        bool refresh,
        CancellationToken cancellationToken,
        string? topic = null,
        IReadOnlyDictionary<string, string>? sourceUrls = null
    )
    {
        var selected = Comments
            .Where(comment =>
                string.IsNullOrWhiteSpace(platform)
                || comment.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)
            )
            .Where(comment => MatchesTopic(comment, topic))
            .ToArray();

        return Task.FromResult(SocialCommentFetchResult.Demo(selected));
    }

    private static bool MatchesTopic(SocialCommentDraft comment, string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return true;
        }

        var source = Normalize($"{comment.Text} {comment.PostTitle}");
        return topic.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(term => Normalize(term).TrimEnd('s'))
            .Where(term => term.Length > 2)
            .All(source.Contains);
    }

    private static string Normalize(string value) =>
        value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Aggregate(new System.Text.StringBuilder(), (builder, character) => builder.Append(char.ToLowerInvariant(character)))
            .ToString();
}
