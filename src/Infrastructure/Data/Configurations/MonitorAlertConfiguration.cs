using AnalisisSentimiento.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnalisisSentimiento.Infrastructure.Data.Configurations;

public class MonitorAlertConfiguration : IEntityTypeConfiguration<MonitorAlert>
{
    public void Configure(EntityTypeBuilder<MonitorAlert> builder)
    {
        builder.Property(x => x.MonitorName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
    }
}
