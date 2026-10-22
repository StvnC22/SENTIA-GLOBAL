using AnalisisSentimiento.Application.Monitoring.Commands.RunScheduledMonitorNow;
using AnalisisSentimiento.Domain.Constants;
using AnalisisSentimiento.Domain.Entities;

namespace AnalisisSentimiento.Application.FunctionalTests.Monitoring.Commands;

public class RunScheduledMonitorNowTests : TestBase
{
    [Test]
    public async Task ShouldExecuteMonitorAndStoreResults()
    {
        await TestApp.RunAsUserAsync("analista@test", "Analista1234!", [Roles.Analyst]);
        var monitor = ScheduledMonitor.Create("ULEAM Manabí", "ULEAM", 60, province: "Manabí");
        await TestApp.AddAsync(monitor);

        var result = await TestApp.SendAsync(new RunScheduledMonitorNowCommand(monitor.Id));

        result.IsSuccess.ShouldBeTrue();
        result.Value.LastRunAt.ShouldNotBeNull();
        result.Value.LastTotal.ShouldNotBeNull();
        result.Value.LastTotal!.Value.ShouldBeGreaterThan(0);

        var stored = await TestApp.FindAsync<ScheduledMonitor>(monitor.Id);
        stored.ShouldNotBeNull();
        stored!.LastExecutiveSummary.ShouldNotBeNullOrWhiteSpace();
        stored.NextRunAt.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldFailWhenMonitorDoesNotExist()
    {
        await TestApp.RunAsUserAsync("analista@test", "Analista1234!", [Roles.Analyst]);

        var result = await TestApp.SendAsync(new RunScheduledMonitorNowCommand(999_999));

        result.IsFailed.ShouldBeTrue();
    }
}
