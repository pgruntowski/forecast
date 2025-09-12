using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class ProjectRevisionConfiguration : IEntityTypeConfiguration<ProjectRevision>
{
    public void Configure(EntityTypeBuilder<ProjectRevision> b)
    {
        b.ToTable("project_revisions");
        b.HasKey(r => new { r.ProjectId, r.Version });

        b.Property(r => r.Name).IsRequired().HasMaxLength(255);
        b.Property(r => r.Value).HasColumnType("numeric(18,2)");
        b.Property(r => r.Margin).HasColumnType("numeric(18,2)");

        b.Property(r => r.DueQuarter).IsRequired().HasMaxLength(10);
        b.Property(r => r.PaymentQuarter).IsRequired().HasMaxLength(10);
        b.Property(r => r.InvoiceMonth).HasMaxLength(7);

        b.HasOne(r => r.Project).WithMany(h => h.Revisions)
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(r => r.Participants).WithOne(p => p.Revision)
            .HasForeignKey(p => new { p.ProjectId, p.Version })
            .OnDelete(DeleteBehavior.Cascade);

        // Indeksy pod filtry
        b.HasIndex(r => r.EffectiveAt);
        b.HasIndex(r => new { r.ProjectId, r.EffectiveAt });
        b.HasIndex(r => r.StatusId);
        b.HasIndex(r => r.MarketId);
        b.HasIndex(r => r.AMId);
        b.HasIndex(r => r.ClientId);
        b.HasIndex(r => r.VendorId);
        b.HasIndex(r => r.ArchitectureId);
        b.HasIndex(r => r.IsCanceled);
        b.HasIndex(r => r.DueQuarter);
        b.HasIndex(r => r.InvoiceMonth);
    }
}
