using ArchUnitNET.NUnit;
using MediatR;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArchitectureTests;

public class VisibilityTests : BaseTest
{
    [Test]
    public void HandlersParametrizados_NoDebeSer_Publico()
    {
        Classes()
            .That()
            .Are(CapaAplicacion)
            .And()
            .ImplementInterface(typeof(IRequestHandler<,>))
            .Should()
            .BeInternal()
            .Check(Arquitectura);
    }

    [Test]
    public void HandlersNoParametrizados_NoDebeSer_Publico()
    {
        Classes()
            .That()
            .Are(CapaAplicacion)
            .And()
            .ImplementInterface(typeof(IRequestHandler<>))
            .Should()
            .BeInternal()
            .WithoutRequiringPositiveResults()
            .Check(Arquitectura);
    }
}
