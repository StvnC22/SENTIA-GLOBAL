using AnalisisSentimiento.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnalisisSentimiento.Infrastructure.Data.Configurations;

public class ScheduledMonitorConfiguration : IEntityTypeConfiguration<ScheduledMonitor>
{
    public void Configure(EntityTypeBuilder<ScheduledMonitor> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(40);
        builder.Property(x => x.Province).HasMaxLength(80);
        builder.Property(x => x.Canton).HasMaxLength(80);
        builder.Property(x => x.Parish).HasMaxLength(80);
        builder.Property(x => x.OwnerUserName).HasMaxLength(256);
        builder.Property(x => x.LastExecutiveSummary).HasMaxLength(4000);
    }
}
