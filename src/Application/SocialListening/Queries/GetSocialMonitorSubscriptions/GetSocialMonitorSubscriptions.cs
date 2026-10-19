using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetSocialMonitorSubscriptions;

public sealed record GetSocialMonitorSubscriptionsQuery(string OwnerId)
    : IQuery<IReadOnlyList<SocialMonitorSubscription>>;

internal sealed class GetSocialMonitorSubscriptionsQueryHandler(ISocialMonitorStore store)
    : IRequestHandler<GetSocialMonitorSubscriptionsQuery, IReadOnlyList<SocialMonitorSubscription>>
{
    public Task<IReadOnlyList<SocialMonitorSubscription>> Handle(
        GetSocialMonitorSubscriptionsQuery request,
        CancellationToken cancellationToken
    ) => store.GetForOwnerAsync(request.OwnerId, cancellationToken);
}
