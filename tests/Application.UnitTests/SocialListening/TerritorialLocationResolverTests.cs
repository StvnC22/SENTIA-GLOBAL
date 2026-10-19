using AnalisisSentimiento.Application.SocialListening.Geography;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Application.UnitTests.SocialListening;

public class TerritorialLocationResolverTests
{
    [Test]
    public void Resolve_UsesMetadataPlaceBeforeCommentText()
    {
        var location = TerritorialLocationResolver.Resolve(
            "Manta, Ecuador",
            "Estoy en Quito ahora"
        );

        location.Province.ShouldBe("Manabí");
        location.Canton.ShouldBe("Manta");
        location.Source.ShouldBe("metadata");
        location.DisplayName.ShouldContain("Manta");
    }

    [Test]
    public void Resolve_DetectsParishFromCommentText()
    {
        var location = TerritorialLocationResolver.Resolve(
            null,
            "Hay problemas de agua en Tarqui"
        );

        location.Parish.ShouldBe("Tarqui");
        location.Canton.ShouldBe("Manta");
        location.Province.ShouldBe("Manabí");
        location.Source.ShouldBe("text");
    }

    [Test]
    public void Resolve_ReturnsUnknownWhenThereIsNoEcuadorPlace()
    {
        var location = TerritorialLocationResolver.Resolve(null, "Hola, ¿cuál es el horario?");

        location.ShouldBe(TerritorialLocation.Unknown);
        location.IsLocated.ShouldBeFalse();
    }
}
