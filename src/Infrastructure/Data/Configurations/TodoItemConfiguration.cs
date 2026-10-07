using AnalisisSentimiento.Domain.Entities;
using AnalisisSentimiento.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnalisisSentimiento.Infrastructure.Data.Configurations;

public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();

        builder
            .Property(t => t.Priority)
            .HasConversion(priority => priority.Value, value => PriorityLevel.FromValue(value));
    }
}
