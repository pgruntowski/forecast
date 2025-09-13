namespace Trecom.Backend.Models;

public sealed class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;

    // Leader (np. TeamLeader)
    public Guid LeaderId { get; set; }
    public User Leader { get; set; } = default!;

    public ICollection<User> Members { get; set; } = new List<User>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
