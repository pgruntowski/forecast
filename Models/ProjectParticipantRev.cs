namespace Trecom.Backend.Models;

public sealed class ProjectParticipantRev
{
    public Guid ProjectId { get; set; }
    public int Version { get; set; }

    public Guid UserId { get; set; }     // uczestnik
    public bool IsOwner { get; set; }    // gdy trzeba odróżnić „głównego” AM

    // (opcjonalnie) nawigacje:
    public ProjectRevision Revision { get; set; } = default!;
    public User? User { get; set; }
}
