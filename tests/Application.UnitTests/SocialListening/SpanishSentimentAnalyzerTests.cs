using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.SocialListening;

public class SpanishSentimentAnalyzerTests
{
    [TestCase("Excelente iniciativa, gracias", "positive")]
    [TestCase("¿Dónde puedo revisar los requisitos?", "neutral")]
    [TestCase("El sistema no funciona, muy mala organización", "negative")]
    [TestCase("Mis sentidas condolencias a la familia", "negative")]
    [TestCase("Qué orgullo ver a nuestra universidad", "positive")]
    public void Analyze_ClasificaElTextoEsperado(string text, string expectedLabel)
    {
        SpanishSentimentAnalyzer.Analyze(text).Label.ShouldBe(expectedLabel);
    }

    [Test]
    public void AnalyzeInsights_IdentificaEmocionAspectoEntidadYTemas()
    {
        var insights = SocialTextInsightsAnalyzer.Analyze(
            "Estoy orgulloso de la docencia en ULEAM #Educación"
        );

        insights.Emotion.ShouldBe("alegría");
        insights.Aspects.ShouldContain("docencia");
        insights.Entities.ShouldContain("ULEAM");
        insights.Entities.ShouldContain("#Educación");
        insights.Topics.ShouldContain("educación");
    }

    [TestCase("¡Gana un premio gratis! https://spam.invalid", true)]
    [TestCase("¿Cómo va la matrícula?", false)]
    public void IsLikelySpam_ClassifiesPromotionalContent(string text, bool expected)
    {
        SocialTextInsightsAnalyzer.IsLikelySpam(text).ShouldBe(expected);
    }
}
