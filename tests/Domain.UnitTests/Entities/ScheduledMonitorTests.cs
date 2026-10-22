using AnalisisSentimiento.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace AnalisisSentimiento.Domain.UnitTests.Entities;

public class ScheduledMonitorTests
{
    [Test]
    public void RecordRun_NoGeneraAlertaEnPrimeraEjecucion()
    {
        var monitor = ScheduledMonitor.Create("ULEAM", "ULEAM", 30);

        var alert = monitor.RecordRun(DateTimeOffset.UtcNow, 10, 2, 2, 6, null);

        alert.ShouldBeNull();
        monitor.LastNegativeRatio.ShouldBe(0.6);
    }

    [Test]
    public void RecordRun_GeneraAlertaCuandoNegativoSubeSignificativamente()
    {
        var monitor = ScheduledMonitor.Create("ULEAM", "ULEAM", 30);
        var now = DateTimeOffset.UtcNow;
        monitor.RecordRun(now, 10, 6, 3, 1, null);

        var alert = monitor.RecordRun(now.AddMinutes(30), 10, 2, 2, 6, null);

        alert.ShouldNotBeNull();
        alert!.PreviousNegativeRatio.ShouldBe(0.1);
        alert.CurrentNegativeRatio.ShouldBe(0.6);
        alert.Message.ShouldContain("subió");
    }

    [Test]
    public void RecordRun_GeneraAlertaCuandoNegativoBajaSignificativamente()
    {
        var monitor = ScheduledMonitor.Create("ULEAM", "ULEAM", 30);
        var now = DateTimeOffset.UtcNow;
        monitor.RecordRun(now, 10, 1, 2, 7, null);

        var alert = monitor.RecordRun(now.AddMinutes(30), 10, 6, 3, 1, null);

        alert.ShouldNotBeNull();
        alert!.Message.ShouldContain("bajó");
    }

    [Test]
    public void RecordRun_IgnoraVariacionesPequenas()
    {
        var monitor = ScheduledMonitor.Create("ULEAM", "ULEAM", 30);
        var now = DateTimeOffset.UtcNow;
        monitor.RecordRun(now, 10, 4, 2, 4, null);

        var alert = monitor.RecordRun(now.AddMinutes(30), 10, 4, 1, 5, null);

        alert.ShouldBeNull();
    }
}
