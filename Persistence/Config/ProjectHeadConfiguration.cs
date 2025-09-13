using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class ProjectHeadConfiguration : IEntityTypeConfiguration<ProjectHead>
{
    public void Configure(EntityTypeBuilder<ProjectHead> b)
    {
        b.ToTable("project_heads");
        b.HasKey(h => h.Id);
        b.Property(h => h.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
    }
}
