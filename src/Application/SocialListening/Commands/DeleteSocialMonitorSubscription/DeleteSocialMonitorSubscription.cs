using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Commands.DeleteSocialMonitorSubscription;

public sealed record DeleteSocialMonitorSubscriptionCommand(string Id, string OwnerId) : ICommand;

internal sealed class DeleteSocialMonitorSubscriptionCommandHandler(ISocialMonitorStore store)
    : IRequestHandler<DeleteSocialMonitorSubscriptionCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSocialMonitorSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        await store.DeleteAsync(request.Id, request.OwnerId, cancellationToken);
        return Result.Ok();
    }
}
