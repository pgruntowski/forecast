namespace Trecom.Backend.Models;

public sealed class ProjectHead
{
    public Guid Id { get; set; }                          // stałe ID projektu
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProjectRevision> Revisions { get; set; } = new List<ProjectRevision>();
}
