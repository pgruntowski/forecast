using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class ClientAliasConfiguration : IEntityTypeConfiguration<ClientAlias>
{
    public void Configure(EntityTypeBuilder<ClientAlias> b)
    {
        b.ToTable("client_aliases");

        b.HasKey(a => a.Id);

        b.Property(a => a.Alias)
         .IsRequired()
         .HasMaxLength(255);

        b.HasOne(a => a.Client)
         .WithMany(c => c.Aliases)
         .HasForeignKey(a => a.ClientId)
         .OnDelete(DeleteBehavior.Cascade);

        // Każdy alias ma być unikalny w systemie:
        b.HasIndex(a => a.Alias).IsUnique();
    }
}
