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
}
