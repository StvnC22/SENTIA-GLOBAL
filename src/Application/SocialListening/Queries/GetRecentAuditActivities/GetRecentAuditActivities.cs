using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.SocialListening.Queries.GetRecentAuditActivities;

public sealed record GetRecentAuditActivitiesQuery(int Count)
    : IQuery<IReadOnlyList<AuditActivity>>;

internal sealed class GetRecentAuditActivitiesQueryHandler(IAuditActivityStore store)
    : IRequestHandler<GetRecentAuditActivitiesQuery, IReadOnlyList<AuditActivity>>
{
    public Task<IReadOnlyList<AuditActivity>> Handle(
        GetRecentAuditActivitiesQuery request,
        CancellationToken cancellationToken
    ) => store.GetRecentAsync(request.Count, cancellationToken);
}
