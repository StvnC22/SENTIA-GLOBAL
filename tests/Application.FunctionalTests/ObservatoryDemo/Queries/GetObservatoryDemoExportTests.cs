using System.Text;
using AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemoExport;

namespace AnalisisSentimiento.Application.FunctionalTests.ObservatoryDemo.Queries;

public class GetObservatoryDemoExportTests : TestBase
{
    [Test]
    public async Task ShouldExportCsvWithTerritoryHeaders()
    {
        var result = await TestApp.SendAsync(new GetObservatoryDemoExportQuery("csv"));

        result.FileName.ShouldEndWith(".csv");
        result.ContentType.ShouldContain("csv");
        var text = Encoding.UTF8.GetString(result.Content);
        text.ShouldContain("provincia,canton,parroquia");
        text.ShouldContain("Manabí");
    }
}
