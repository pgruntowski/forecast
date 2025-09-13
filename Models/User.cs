namespace Trecom.Backend.Models;

public enum UserRole
{
    AM = 0,
    TeamLeader = 1,
    Board = 2,
    Admin = 3,
    SuperAdmin = 4
}

public sealed class User
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;   // unique

    public string? PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.AM;
    public bool IsActive { get; set; } = true;

    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public ICollection<User> DirectReports { get; set; } = new List<User>();
    public Guid? TeamId { get; set; }
    public Team? Team { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Wygodny full name do UI (nie mapujemy do DB)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string FullName => $"{LastName} {FirstName}";
}
