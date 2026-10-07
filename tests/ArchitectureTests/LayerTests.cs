using ArchUnitNET.NUnit;
using NUnit.Framework;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArchitectureTests;

// Sin proyecto Contracts separado: ver MessagingPoliciesTests para la separación Command/Query.
public class LayerTests : BaseTest
{
    [Test]
    public void Dominio_NoDebeDependerDe_Aplicacion()
    {
        Types().That().Are(CapaDominio).Should().NotDependOnAny(CapaAplicacion).Check(Arquitectura);
    }

    [Test]
    public void Dominio_NoDebeDependerDe_Infraestructura()
    {
        Types()
            .That()
            .Are(CapaDominio)
            .Should()
            .NotDependOnAny(CapaInfraestructura)
            .Check(Arquitectura);
    }

    [Test]
    public void Dominio_NoDebeDependerDe_Web()
    {
        Types().That().Are(CapaDominio).Should().NotDependOnAny(CapaWeb).Check(Arquitectura);
    }

    [Test]
    public void Aplicacion_NoDebeDependerDe_Infraestructura()
    {
        Types()
            .That()
            .Are(CapaAplicacion)
            .Should()
            .NotDependOnAny(CapaInfraestructura)
            .Check(Arquitectura);
    }

    [Test]
    public void Aplicacion_NoDebeDependerDe_Web()
    {
        Types().That().Are(CapaAplicacion).Should().NotDependOnAny(CapaWeb).Check(Arquitectura);
    }

    [Test]
    public void Infraestructura_NoDebeDependerDe_Web()
    {
        Types()
            .That()
            .Are(CapaInfraestructura)
            .Should()
            .NotDependOnAny(CapaWeb)
            .Check(Arquitectura);
    }

    [Test]
    public void Web_NoDebeDependerDe_ApplicationDbContext()
    {
        Types()
            .That()
            .Are(CapaWeb)
            .Should()
            .NotDependOnAny(DbContextInfraestructura)
            .Check(Arquitectura);
    }

    [Test]
    public void Web_NoDebeDependerDe_IApplicationDbContext()
    {
        Types()
            .That()
            .Are(CapaWeb)
            .Should()
            .NotDependOnAny(DbContextContratoAplicacion)
            .Check(Arquitectura);
    }
}
