using System.Reflection;
using AnalisisSentimiento.Web.Infrastructure;
using ArchUnitNET.NUnit;
using MediatR;
using NUnit.Framework;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace ArchitectureTests;

public class WebAndSlicePoliciesTests : BaseTest
{
    private static readonly HashSet<string> EndpointsExentosDeUsarISenderPorUsarIdentityDirecto =
    [
        "AnalisisSentimiento.Web.Endpoints.Users",
    ];

    [Test]
    public void Endpoints_DebenDependerDe_ISender()
    {
        var endpointTypes = Web.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(IEndpointGroup).IsAssignableFrom(type))
            .ToArray();

        var invalidTypes = endpointTypes
            .Where(type =>
                !EndpointsExentosDeUsarISenderPorUsarIdentityDirecto.Contains(
                    type.FullName ?? string.Empty
                )
            )
            .Where(type => !TypeDependsOn(type, typeof(ISender)))
            .Select(type => type.FullName)
            .ToArray();

        invalidTypes.ShouldBeEmpty(
            $"Los siguientes endpoints no dependen de ISender: {string.Join(", ", invalidTypes)}"
        );
    }

    [Test]
    public void Web_NoDebeDepender_DeEFCore_DbContext_DbSet()
    {
        Types()
            .That()
            .Are(CapaWeb)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore"))
            .Check(Arquitectura);

        var webTypes = Web.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        var invalidTypes = webTypes
            .Where(TypeDependsOnEfCoreOrDbTypes)
            .Select(type => type.FullName)
            .ToArray();

        invalidTypes.ShouldBeEmpty(
            $"Los siguientes tipos de Web dependen de EF Core/DbContext/DbSet: {string.Join(", ", invalidTypes)}"
        );
    }

    private static bool TypeDependsOn(Type type, Type targetType)
    {
        return GetAllReferencedTypes(type)
            .Any(referencedType =>
                referencedType == targetType || referencedType.FullName == targetType.FullName
            );
    }

    private static bool TypeDependsOnEfCoreOrDbTypes(Type type)
    {
        return GetAllReferencedTypes(type)
            .Any(referencedType =>
                (
                    referencedType.Namespace is not null
                    && referencedType.Namespace.StartsWith(
                        "Microsoft.EntityFrameworkCore",
                        StringComparison.Ordinal
                    )
                )
                || referencedType.Name == "DbContext"
                || referencedType.Name == "DbSet`1"
            );
    }

    private static IEnumerable<Type> GetAllReferencedTypes(Type type)
    {
        var bindingFlags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static;

        if (type.BaseType is not null)
        {
            yield return NormalizeType(type.BaseType);
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            yield return NormalizeType(interfaceType);
        }

        foreach (var field in type.GetFields(bindingFlags))
        {
            yield return NormalizeType(field.FieldType);
        }

        foreach (var property in type.GetProperties(bindingFlags))
        {
            yield return NormalizeType(property.PropertyType);
        }

        foreach (var constructor in type.GetConstructors(bindingFlags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                yield return NormalizeType(parameter.ParameterType);
            }
        }

        foreach (var method in type.GetMethods(bindingFlags))
        {
            yield return NormalizeType(method.ReturnType);

            foreach (var parameter in method.GetParameters())
            {
                yield return NormalizeType(parameter.ParameterType);
            }
        }
    }

    private static Type NormalizeType(Type type)
    {
        if (type.IsGenericType)
        {
            return type.GetGenericTypeDefinition();
        }

        return type;
    }
}
