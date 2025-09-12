using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trecom.Backend.Data;
using Trecom.Backend.Dto;
using Trecom.Backend.Models;

namespace Trecom.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UsersController : ControllerBase
{
    private readonly TrecomDbContext _db;

    public UsersController(TrecomDbContext db) => _db = db;

    // GET: api/users?role=AM&search=jank&skip=0&take=20&includeInactive=false
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers(
        [FromQuery] string? role,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] bool includeInactive = false)
    {
        var q = _db.Users.AsQueryable();

        if (!includeInactive)
            q = q.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var parsedRole))
            q = q.Where(u => u.Role == parsedRole);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u =>
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s));
        }

        var items = await q
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 200))
            .Select(u => new UserDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET: api/users/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u is null) return NotFound();

        return new UserDto
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        };
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest($"Unknown role: {dto.Role}");

        // prosta walidacja unikalności email
        var emailExists = await _db.Users.AnyAsync(x => x.Email == dto.Email);
        if (emailExists) return Conflict("Email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Role = role,
            IsActive = true,
            ManagerId = dto.ManagerId
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, result);
    }

    // PUT: api/users/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest($"Unknown role: {dto.Role}");

        // zabezpieczenie unikalności email przy edycji
        var emailTaken = await _db.Users.AnyAsync(x => x.Email == dto.Email && x.Id != id);
        if (emailTaken) return Conflict("Email already in use by another user.");

        u.FirstName = dto.FirstName;
        u.LastName = dto.LastName;
        u.Email = dto.Email;
        u.Role = role;
        u.IsActive = dto.IsActive;
        u.ManagerId = dto.ManagerId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("Concurrency conflict. Reload the entity and retry.");
        }

        return new UserDto
        {
            Id = u.Id,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        };
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool soft = true)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return NotFound();

        if (soft)
        {
            u.IsActive = false; // soft delete
            await _db.SaveChangesAsync();
            return NoContent();
        }
        else
        {
            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
