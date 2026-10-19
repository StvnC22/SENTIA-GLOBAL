using AnalisisSentimiento.Application.ObservatoryDemo.Queries.GetObservatoryDemo;

namespace AnalisisSentimiento.Application.FunctionalTests.ObservatoryDemo.Queries;

public class GetObservatoryDemoTests : TestBase
{
    [Test]
    public async Task ShouldReturnTerritorialDemoWithoutRequiringLiveSocialData()
    {
        var result = await TestApp.SendAsync(new GetObservatoryDemoQuery());

        result.Mode.ShouldBe("demo");
        result.Territories.ShouldNotBeEmpty();
        result.Territories.ShouldContain(item => item.Province == "Manabí");
        result.Alerts.Count.ShouldBe(result.Kpis.OpenAlerts);
        result.Tools.ShouldContain(tool => tool.Need == "Georreferenciación");
        result.Roles.Select(role => role.Role)
            .ShouldBe(["Administrador", "Analista", "Consultor"]);
    }
}
