using System.ComponentModel.DataAnnotations;

namespace Trecom.Backend.Dto;

public sealed class UserDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CreateUserDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = default!;
    [Required, MaxLength(100)]
    public string LastName { get; set; } = default!;
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = default!;
    [Required]
    public string Role { get; set; } = "AM"; // AM, TeamLeader, Board, Admin, SuperAdmin
    public Guid? ManagerId { get; set; }
}

public sealed class UpdateUserDto
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = default!;
    [Required, MaxLength(100)]
    public string LastName { get; set; } = default!;
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = default!;
    [Required]
    public string Role { get; set; } = "AM";
    public bool IsActive { get; set; } = true;
    public Guid? ManagerId { get; set; }

    // (opcjonalnie) ochrona współbieżności — RowVersion jako base64
    public string? RowVersion { get; set; }
}
