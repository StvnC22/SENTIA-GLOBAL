using AnalisisSentimiento.Application.SocialListening.Search;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.SocialListening;

public class BooleanQueryMatcherTests
{
    [Test]
    public void Matches_OperadoresYON_EnEspanol()
    {
        BooleanQueryMatcher.Matches("ULEAM Y Portoviejo", "Orgullo ULEAM en Portoviejo").ShouldBeTrue();
        BooleanQueryMatcher.Matches("ULEAM Y Quito", "Orgullo ULEAM en Portoviejo").ShouldBeFalse();
        BooleanQueryMatcher.Matches("beca O ayuda", "Necesito ayuda estudiantil").ShouldBeTrue();
        BooleanQueryMatcher.Matches("ULEAM NO spam", "ULEAM spam gratis").ShouldBeFalse();
        BooleanQueryMatcher.Matches("ULEAM NO spam", "ULEAM matrícula").ShouldBeTrue();
    }

    [Test]
    public void Matches_HashtagsYSinonimos()
    {
        BooleanQueryMatcher.Matches("#Manabi", "Viva #Manabi 2026").ShouldBeTrue();
        BooleanQueryMatcher.Matches("beca~ayuda,beneficio", "Solicité un beneficio universitario").ShouldBeTrue();
        BooleanQueryMatcher.Matches("uleam|universidad", "La universidad sigue creciendo").ShouldBeTrue();
    }
}
