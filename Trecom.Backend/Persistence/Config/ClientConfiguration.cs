using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");

        b.HasKey(c => c.Id);

        b.Property(c => c.CanonicalName)
         .IsRequired()
         .HasMaxLength(255);

        b.HasIndex(c => c.CanonicalName).IsUnique();

        b.Property(c => c.TaxId).HasMaxLength(32);
        b.Property(c => c.City).HasMaxLength(128);

        b.Property(c => c.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
        b.Property(c => c.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");
    }
}
