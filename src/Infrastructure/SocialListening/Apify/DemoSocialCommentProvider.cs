using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Application.SocialListening.Search;

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
            "¡Qué orgullo ver cómo sigue creciendo nuestra universidad en Portoviejo! 💙 #ULEAM #Manabi",
            "https://www.instagram.com/p/demo-ig-1/",
            "Admisiones ULEAM 2026",
            "https://www.instagram.com/p/demo-ig-1/",
            new DateTimeOffset(2026, 8, 21, 14, 32, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Portoviejo",
            "Portoviejo",
            42
        ),
        new(
            "ig-2",
            "instagram",
            "Carlos Mera",
            "@carlosmera.ec",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "¿Dónde puedo revisar los requisitos para la matrícula en Manta?",
            "https://www.instagram.com/p/demo-ig-2/",
            "Proceso de matriculación",
            "https://www.instagram.com/p/demo-ig-2/",
            new DateTimeOffset(2026, 8, 21, 11, 8, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Manta",
            "Manta",
            11
        ),
        new(
            "ig-3",
            "instagram",
            "Andrea Ponce",
            "@andreaponce",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Llevo días intentando entrar al sistema y todavía no funciona. Estoy en Chone.",
            "https://www.instagram.com/p/demo-ig-3/",
            "Servicios digitales ULEAM",
            "https://www.instagram.com/p/demo-ig-3/",
            new DateTimeOffset(2026, 8, 20, 18, 45, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Chone",
            "Chone",
            27
        ),
        new(
            "ig-spam",
            "instagram",
            "Spam Bot",
            "@user99887766",
            "https://www.instagram.com/uleam_ecuador_oficial/",
            "Gana dinero YA!!!! haz clic aqui http://bit.ly/spam",
            "https://www.instagram.com/p/demo-spam/",
            "Promoción falsa",
            "https://www.instagram.com/p/demo-spam/",
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.FromHours(-5)),
            IsLikelyBot: true
        ),
        new(
            "fb-1",
            "facebook",
            "José Luis Cedeño",
            "José Luis Cedeño",
            "https://www.facebook.com/UleamEc",
            "Excelente iniciativa para apoyar a los estudiantes de Manabí con becas.",
            "https://www.facebook.com/UleamEc/posts/demo-fb-1",
            "Becas y ayudas estudiantiles",
            "https://www.facebook.com/UleamEc/posts/demo-fb-1",
            new DateTimeOffset(2026, 8, 21, 9, 20, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Portoviejo",
            "Portoviejo",
            88
        ),
        new(
            "fb-2",
            "facebook",
            "Rosa Delgado",
            "Rosa Delgado",
            "https://www.facebook.com/UleamEc",
            "La información está clara, gracias por compartir desde Jipijapa.",
            "https://www.facebook.com/UleamEc/posts/demo-fb-2",
            "Calendario académico",
            "https://www.facebook.com/UleamEc/posts/demo-fb-2",
            new DateTimeOffset(2026, 8, 20, 16, 5, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Jipijapa",
            "Jipijapa",
            19
        ),
        new(
            "fb-3",
            "facebook",
            "Miguel Alcívar",
            "Miguel Alcívar",
            "https://www.facebook.com/UleamEc",
            "No responden los mensajes y necesito resolver mi trámite urgente en El Carmen.",
            "https://www.facebook.com/UleamEc/posts/demo-fb-3",
            "Atención al estudiante",
            "https://www.facebook.com/UleamEc/posts/demo-fb-3",
            new DateTimeOffset(2026, 8, 19, 13, 41, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "El Carmen",
            "El Carmen",
            34
        ),
        new(
            "tt-1",
            "tiktok",
            "Vale C.",
            "@valec",
            "https://www.tiktok.com/@uleamecuador",
            "La mejor universidad de Manabí, qué lindo video desde Montecristi 😍 #ULEAM",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-1",
            "Así se vive la ULEAM",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-1",
            new DateTimeOffset(2026, 8, 21, 15, 2, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Montecristi",
            "Montecristi",
            210
        ),
        new(
            "tt-2",
            "tiktok",
            "Mateo",
            "@mateo.ec",
            "https://www.tiktok.com/@uleamecuador",
            "¿La carrera también está disponible en la extensión Chone?",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-2",
            "Conoce nuestra oferta académica",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-2",
            new DateTimeOffset(2026, 8, 20, 20, 17, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Chone",
            "Chone",
            45
        ),
        new(
            "tt-3",
            "tiktok",
            "Emi",
            "@emi_05",
            "https://www.tiktok.com/@uleamecuador",
            "No explican cuándo inicia el proceso, así es imposible organizarse en Pedernales.",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-3",
            "Conoce nuestra oferta académica",
            "https://www.tiktok.com/@uleamecuador/video/demo-tt-3",
            new DateTimeOffset(2026, 8, 19, 19, 33, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Pedernales",
            "Pedernales",
            67
        ),
        new(
            "x-1",
            "x",
            "Eduardo Vera",
            "@eduveram",
            "https://x.com/UleamEcuador",
            "Gran noticia para la investigación y la comunidad universitaria de Bahía de Caráquez.",
            "https://x.com/UleamEcuador/status/demo-x-1",
            "Nuevo convenio de investigación",
            "https://x.com/UleamEcuador/status/demo-x-1",
            new DateTimeOffset(2026, 8, 21, 12, 14, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Bahía de Caráquez",
            "Bahía de Caráquez",
            56
        ),
        new(
            "x-2",
            "x",
            "Ana Sofía",
            "@anasofiag",
            "https://x.com/UleamEcuador",
            "¿Hay transmisión en vivo del evento? Estoy en Puerto López.",
            "https://x.com/UleamEcuador/status/demo-x-2",
            "Encuentro académico 2026",
            "https://x.com/UleamEcuador/status/demo-x-2",
            new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Puerto López",
            "Puerto López",
            14
        ),
        new(
            "x-3",
            "x",
            "Diego Macías",
            "@diegomacias",
            "https://x.com/UleamEcuador",
            "Otra vez cambiaron el horario sin avisar en Tosagua. Muy mala organización.",
            "https://x.com/UleamEcuador/status/demo-x-3",
            "Encuentro académico 2026",
            "https://x.com/UleamEcuador/status/demo-x-3",
            new DateTimeOffset(2026, 8, 19, 8, 52, 0, TimeSpan.FromHours(-5)),
            "Manabí",
            "Tosagua",
            "Tosagua",
            39
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
            .Where(comment =>
                BooleanQueryMatcher.Matches(topic, $"{comment.Text} {comment.PostTitle}")
            )
            .ToArray();

        return Task.FromResult(SocialCommentFetchResult.Demo(selected));
    }
}
