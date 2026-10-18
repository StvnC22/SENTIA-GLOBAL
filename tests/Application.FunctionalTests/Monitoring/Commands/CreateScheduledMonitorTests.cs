using AnalisisSentimiento.Application.Monitoring.Commands.CreateScheduledMonitor;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.Monitoring.Commands;

public class CreateScheduledMonitorTests : TestBase
{
    [Test]
    public async Task ShouldCreateScheduledMonitor()
    {
        await TestApp.RunAsUserAsync("analista@test", "Analista1234!", [Roles.Analyst]);

        var created = await TestApp.SendAsync(
            new CreateScheduledMonitorCommand(
                "ULEAM Portoviejo",
                "ULEAM Y Portoviejo",
                30,
                null,
                "Manabí",
                "Portoviejo",
                null
            )
        );

        created.IsSuccess.ShouldBeTrue();
        created.Value.Name.ShouldBe("ULEAM Portoviejo");
        created.Value.IntervalMinutes.ShouldBe(30);

        var entity = await TestApp.FindAsync<ScheduledMonitor>(created.Value.Id);
        entity.ShouldNotBeNull();
        entity!.Topic.ShouldBe("ULEAM Y Portoviejo");
        entity.Canton.ShouldBe("Portoviejo");
    }
}
