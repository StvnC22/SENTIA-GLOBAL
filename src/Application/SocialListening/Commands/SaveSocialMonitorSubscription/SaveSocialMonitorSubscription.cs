using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Commands.SaveSocialMonitorSubscription;

public sealed record SaveSocialMonitorSubscriptionCommand(SocialMonitorSubscription Subscription)
    : ICommand<SocialMonitorSubscription>;

internal sealed class SaveSocialMonitorSubscriptionCommandHandler(ISocialMonitorStore store)
    : IRequestHandler<SaveSocialMonitorSubscriptionCommand, Result<SocialMonitorSubscription>>
{
    public async Task<Result<SocialMonitorSubscription>> Handle(
        SaveSocialMonitorSubscriptionCommand request,
        CancellationToken cancellationToken
    )
    {
        await store.SaveAsync(request.Subscription, cancellationToken);
        return Result.Ok(request.Subscription);
    }
}
