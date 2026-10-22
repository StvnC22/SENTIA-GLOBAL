using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.SocialListening;

public class TextEnrichmentTests
{
    [Test]
    public void Extract_AsignaSentimientoPorAspecto()
    {
        var result = TextEnrichment.Extract(
            "Excelente la carrera de medicina en ULEAM, pero la plataforma no funciona otra vez",
            "negative"
        );

        result.Topics.ShouldContain("educación");
        result.Topics.ShouldContain("servicio");
        result.Aspects.ShouldContain(new AspectSentiment("educación", "positive"));
        result.Aspects.ShouldContain(new AspectSentiment("servicio", "negative"));
        result.Entities.ShouldContain("ULEAM");
    }

    [Test]
    public void Extract_UsaSentimientoGlobalCuandoHayUnSoloAspectoNeutral()
    {
        var result = TextEnrichment.Extract("Consulta sobre la beca de este semestre", "positive");

        result.Aspects.ShouldHaveSingleItem();
        result.Aspects[0].ShouldBe(new AspectSentiment("beca", "positive"));
    }
}
