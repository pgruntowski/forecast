using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> b)
    {
        b.ToTable("teams");

        b.HasKey(t => t.Id);

        b.Property(t => t.Name)
         .IsRequired()
         .HasMaxLength(150);

        b.HasIndex(t => t.Name).IsUnique();

        b.Property(t => t.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
        b.Property(t => t.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");

        b.HasOne(t => t.Leader)
         .WithMany()
         .HasForeignKey(t => t.LeaderId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
