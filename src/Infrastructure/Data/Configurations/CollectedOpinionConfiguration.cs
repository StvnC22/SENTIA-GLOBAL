using AnalisisSentimiento.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnalisisSentimiento.Infrastructure.Data.Configurations;

public class CollectedOpinionConfiguration : IEntityTypeConfiguration<CollectedOpinion>
{
    public void Configure(EntityTypeBuilder<CollectedOpinion> builder)
    {
        builder.Property(x => x.Platform).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AuthorName).HasMaxLength(300);
        builder.Property(x => x.AuthorHandle).HasMaxLength(300);
        builder.Property(x => x.AuthorUrl).HasMaxLength(1000);
        builder.Property(x => x.Text).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.CommentUrl).HasMaxLength(1000);
        builder.Property(x => x.PostTitle).HasMaxLength(500);
        builder.Property(x => x.PostUrl).HasMaxLength(1000);
        builder.Property(x => x.Province).HasMaxLength(80);
        builder.Property(x => x.Canton).HasMaxLength(80);
        builder.Property(x => x.Parish).HasMaxLength(80);
        builder.Property(x => x.SentimentLabel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SentimentDisplayName).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.Platform, x.ExternalId }).IsUnique();
    }
}
