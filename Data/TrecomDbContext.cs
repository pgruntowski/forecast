using Microsoft.EntityFrameworkCore;
using Trecom.Backend.Models;
using Trecom.Backend.Persistence.Config;

namespace Trecom.Backend.Data;

public sealed class TrecomDbContext : DbContext
{
    public TrecomDbContext(DbContextOptions<TrecomDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientAlias> ClientAliases => Set<ClientAlias>();
    public DbSet<ProjectHead> ProjectHeads => Set<ProjectHead>();
    public DbSet<ProjectRevision> ProjectRevisions => Set<ProjectRevision>();
    public DbSet<ProjectParticipantRev> ProjectParticipantsRev => Set<ProjectParticipantRev>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TeamConfiguration());
        modelBuilder.ApplyConfiguration(new ClientConfiguration());
        modelBuilder.ApplyConfiguration(new ClientAliasConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectHeadConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectParticipantRevConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        TouchTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void TouchTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is User && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var e in entries)
        {
            if (e.State == EntityState.Added)
                e.Property(nameof(User.CreatedAt)).CurrentValue = DateTime.UtcNow;

            e.Property(nameof(User.UpdatedAt)).CurrentValue = DateTime.UtcNow;
        }
    }
}
