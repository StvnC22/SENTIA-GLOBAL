using AnalisisSentimiento.Application.ObservatoryDemo;

namespace AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemoExport;

public record GetObservatoryDemoExportQuery(string Format = "csv") : IQuery<ObservatoryDemoExportFile>;

internal class GetObservatoryDemoExportQueryHandler(TimeProvider timeProvider)
    : IRequestHandler<GetObservatoryDemoExportQuery, ObservatoryDemoExportFile>
{
    public Task<ObservatoryDemoExportFile> Handle(
        GetObservatoryDemoExportQuery request,
        CancellationToken cancellationToken
    )
    {
        var content = ObservatoryDemoSnapshot.CreateCsv(timeProvider.GetUtcNow());
        return Task.FromResult(
            new ObservatoryDemoExportFile(
                "observatorio-territorial-demo.csv",
                "text/csv; charset=utf-8",
                content
            )
        );
    }
}

public class GetObservatoryDemoExportQueryValidator : AbstractValidator<GetObservatoryDemoExportQuery>
{
    public GetObservatoryDemoExportQueryValidator()
    {
        RuleFor(query => query.Format)
            .NotEmpty()
            .Must(format => format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            .WithMessage("La demo solo exporta CSV.");
    }
}
