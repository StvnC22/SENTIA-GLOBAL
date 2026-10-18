using AnalisisSentimiento.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnalisisSentimiento.Infrastructure.Data.Configurations;

public class ActivityLogEntryConfiguration : IEntityTypeConfiguration<ActivityLogEntry>
{
    public void Configure(EntityTypeBuilder<ActivityLogEntry> builder)
    {
        builder.Property(x => x.UserName).HasMaxLength(256);
        builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(2000);
    }
}
