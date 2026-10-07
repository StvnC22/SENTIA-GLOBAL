using AnalisisSentimiento.Application.Common.Messaging;
using MediatR;
using NUnit.Framework;
using Shouldly;

namespace ArchitectureTests;

public class MessagingPoliciesTests : BaseTest
{
    // Users opera directo sobre UserManager/SignInManager, sin pasar por MediatR.
    private const string UsersNamespaceSegmentExemptFromCommandQueryTyping = ".Users.";

    [Test]
    public void RequestsEnAplicacion_DebenSerCommandOQuery_Tipado()
    {
        var invalidTypes = Aplicacion
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => !IsUsersRequest(type))
            .Where(ImplementsRawMediatRRequest)
            .Where(type => !ImplementsCommandOrQueryMarker(type))
            .Select(type => type.FullName)
            .ToArray();

        invalidTypes.ShouldBeEmpty(
            "Los siguientes tipos implementan IRequest/IRequest<T> de MediatR directamente en vez "
                + $"de ICommand/ICommand<T>/IQuery<T>: {string.Join(", ", invalidTypes)}"
        );
    }

    [Test]
    public void Commands_NoDebenImplementar_IQuery()
    {
        var invalidTypes = Aplicacion
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => ImplementsGenericInterface(type, typeof(IQuery<>)))
            .Where(type =>
                ImplementsInterface(type, typeof(ICommand))
                || ImplementsGenericInterface(type, typeof(ICommand<>))
            )
            .Select(type => type.FullName)
            .ToArray();

        invalidTypes.ShouldBeEmpty(
            $"Los siguientes tipos implementan tanto ICommand/ICommand<T> como IQuery<T>, rompiendo la separación CQRS: {string.Join(", ", invalidTypes)}"
        );
    }

    [Test]
    public void CommandHandlers_DebenEstarEn_Namespace_Commands()
    {
        var invalidHandlers = GetHandlersByRequestMarker(requestPredicate: request =>
                ImplementsInterface(request, typeof(ICommand))
                || ImplementsGenericInterface(request, typeof(ICommand<>))
            )
            .Where(handler =>
                handler.Namespace is null
                || !handler.Namespace.Contains(".Commands.", StringComparison.Ordinal)
            )
            .Select(handler => handler.FullName)
            .ToArray();

        invalidHandlers.ShouldBeEmpty(
            "Los siguientes handlers de un ICommand/ICommand<T> no están bajo un namespace con "
                + $"'.Commands.': {string.Join(", ", invalidHandlers)}"
        );
    }

    [Test]
    public void QueryHandlers_DebenEstarEn_Namespace_Queries()
    {
        var invalidHandlers = GetHandlersByRequestMarker(requestPredicate: request =>
                ImplementsGenericInterface(request, typeof(IQuery<>))
            )
            .Where(handler =>
                handler.Namespace is null
                || !handler.Namespace.Contains(".Queries.", StringComparison.Ordinal)
            )
            .Select(handler => handler.FullName)
            .ToArray();

        invalidHandlers.ShouldBeEmpty(
            "Los siguientes handlers de un IQuery<T> no están bajo un namespace con '.Queries.': "
                + string.Join(", ", invalidHandlers)
        );
    }

    private IEnumerable<Type> GetHandlersByRequestMarker(Func<Type, bool> requestPredicate)
    {
        return Aplicacion
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(handlerType =>
                handlerType
                    .GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && (
                            i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                            || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                        )
                        && requestPredicate(i.GetGenericArguments()[0])
                    )
            );
    }

    private static bool IsUsersRequest(Type type)
    {
        return type.Namespace?.Contains(
                UsersNamespaceSegmentExemptFromCommandQueryTyping,
                StringComparison.Ordinal
            ) ?? false;
    }

    private static bool ImplementsCommandOrQueryMarker(Type type)
    {
        return ImplementsInterface(type, typeof(ICommand))
            || ImplementsGenericInterface(type, typeof(ICommand<>))
            || ImplementsGenericInterface(type, typeof(IQuery<>));
    }

    private static bool ImplementsRawMediatRRequest(Type type)
    {
        return ImplementsInterface(type, typeof(IRequest))
            || ImplementsGenericInterface(type, typeof(IRequest<>));
    }

    private static bool ImplementsInterface(Type type, Type targetInterface)
    {
        return type.GetInterfaces().Contains(targetInterface);
    }

    private static bool ImplementsGenericInterface(Type type, Type openGenericInterface)
    {
        return type.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface);
    }
}
