using AnalisisSentimiento.Application.Monitoring.Queries.GetScheduledMonitors;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.Monitoring.Queries;

public class GetScheduledMonitorsTests : TestBase
{
    [Test]
    public async Task ShouldReturnCreatedMonitors()
    {
        await TestApp.RunAsUserAsync("consulta@test", "Consulta1234!", [Roles.Consulta]);

        await TestApp.AddAsync(
            ScheduledMonitor.Create("Monitor A", "ULEAM", 60, province: "Manabí")
        );

        var list = await TestApp.SendAsync(new GetScheduledMonitorsQuery());
        list.ShouldContain(item => item.Name == "Monitor A");
    }
}
