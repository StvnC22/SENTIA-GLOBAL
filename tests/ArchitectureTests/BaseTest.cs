using AnalisisSentimiento.Application.Common.Interfaces;
using AnalisisSentimiento.Domain.Common;
using AnalisisSentimiento.Infrastructure.Data;
using AnalisisSentimiento.Web.Infrastructure;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly Dominio = typeof(BaseEntity).Assembly;

    protected static readonly Assembly Aplicacion = typeof(IApplicationDbContext).Assembly;

    protected static readonly Assembly Infraestructura = typeof(ApplicationDbContext).Assembly;

    // Program (top-level statements) es inaccesible fuera de su assembly; IEndpointGroup es público.
    protected static readonly Assembly Web = typeof(IEndpointGroup).Assembly;

    protected static readonly Architecture Arquitectura = new ArchLoader()
        .LoadAssemblies(Dominio, Aplicacion, Infraestructura, Web)
        .Build();

    protected static readonly IObjectProvider<IType> CapaDominio = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Domain")
        .As("Capa Dominio");

    protected static readonly IObjectProvider<IType> CapaAplicacion = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Application")
        .As("Capa Aplicacion");

    protected static readonly IObjectProvider<IType> CapaInfraestructura = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Infrastructure")
        .As("Capa Infraestructura");

    protected static readonly IObjectProvider<IType> CapaWeb = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Web")
        .As("Capa Web");

    protected static readonly IObjectProvider<IType> DbContextInfraestructura = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Infrastructure\.Data")
        .And()
        .HaveName("ApplicationDbContext")
        .As("ApplicationDbContext");

    protected static readonly IObjectProvider<IType> DbContextContratoAplicacion = Types()
        .That()
        .ResideInNamespaceMatching(@"AnalisisSentimiento\.Application\.Common\.Interfaces")
        .And()
        .HaveName("IApplicationDbContext")
        .As("IApplicationDbContext");
}
