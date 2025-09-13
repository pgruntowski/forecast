using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trecom.Backend.Models;

namespace Trecom.Backend.Persistence.Config;

public sealed class ProjectParticipantRevConfiguration : IEntityTypeConfiguration<ProjectParticipantRev>
{
    public void Configure(EntityTypeBuilder<ProjectParticipantRev> b)
    {
        b.ToTable("project_participants_rev");
        b.HasKey(x => new { x.ProjectId, x.Version, x.UserId });

        b.HasOne(x => x.Revision)
            .WithMany(r => r.Participants)
            .HasForeignKey(x => new { x.ProjectId, x.Version })
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.UserId); // szybkie „moje projekty”
    }
}
