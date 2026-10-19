using AnalisisSentimiento.Application.ObservatoryDemo;

namespace AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemo;

public record GetObservatoryDemoQuery : IQuery<ObservatoryDemoDashboard>;

internal class GetObservatoryDemoQueryHandler(TimeProvider timeProvider)
    : IRequestHandler<GetObservatoryDemoQuery, ObservatoryDemoDashboard>
{
    public Task<ObservatoryDemoDashboard> Handle(
        GetObservatoryDemoQuery request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(ObservatoryDemoSnapshot.Create(timeProvider.GetUtcNow()));
    }
}
