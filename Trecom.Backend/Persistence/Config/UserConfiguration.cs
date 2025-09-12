using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL;
namespace Trecom.Backend.Persistence.Config;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users"); // snake_case w Postgresie jest milej

        b.HasKey(u => u.Id);

        b.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        b.Property(u => u.LastName).IsRequired().HasMaxLength(100);
        b.Property(u => u.Email).IsRequired().HasMaxLength(255);

        b.HasIndex(u => u.Email).IsUnique();

        b.Property(u => u.Role).IsRequired();
        b.Property(u => u.IsActive).HasDefaultValue(true);

        b.Property(u => u.CreatedAt).HasDefaultValueSql("now() at time zone 'utc'");
        b.Property(u => u.UpdatedAt).HasDefaultValueSql("now() at time zone 'utc'");

        // Concurrency token (Postgres): bytea/timestamp – tu prosty token
        b.Property<uint>("xmin")
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Hierarchia (self ref)
        b.HasOne(u => u.Manager)
         .WithMany(m => m.DirectReports)
         .HasForeignKey(u => u.ManagerId)
         .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(u => u.Team)
         .WithMany(t => t.Members)
         .HasForeignKey(u => u.TeamId)
         .OnDelete(DeleteBehavior.SetNull);
    }
}
