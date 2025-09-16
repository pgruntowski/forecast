using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Trecom.Backend.Data;
using Trecom.Backend.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TrecomDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Sql"),
        sql => sql.EnableRetryOnFailure()));

// ↓ Konfiguracja auth
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

var tenantId = builder.Configuration["AzureAd:TenantId"];

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
{
    o.ResponseType = OpenIdConnectResponseType.Code;
    o.UsePkce = true;
    o.SaveTokens = true;

    o.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
        NameClaimType = "name",
        RoleClaimType = "roles"
    };

    // UWAGA: "login" wymusza każde logowanie; na produkcji zwykle lepiej bez tego.
    // o.Prompt = "login";

    o.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = async ctx =>
        {
            using var scope = ctx.HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TrecomDbContext>();

            var user = ctx.Principal!;
            string oid = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")!;
            string? email = user.FindFirstValue("preferred_username")
                           ?? user.FindFirstValue(ClaimTypes.Email)
                           ?? user.FindFirst("emails")?.Value; // czasem AAD daje "emails"

            string firstName = user.FindFirstValue("given_name") ?? "";
            string lastName = user.FindFirstValue("family_name") ?? "";
            string? aadRole = user.Claims.FirstOrDefault(c => c.Type == "roles" || c.Type == ClaimTypes.Role)?.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                // jako awaryjny fallback
                email = $"{oid}@unknown.local";
            }

            var now = DateTime.UtcNow;

            // upsert po email (albo po OID jeżeli wprowadzisz kolumnę Oid w tabeli Users)
            var dbUser = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

            if (dbUser is null)
            {
                dbUser = new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = string.IsNullOrWhiteSpace(firstName) ? "Unknown" : firstName,
                    LastName = string.IsNullOrWhiteSpace(lastName) ? "User" : lastName,
                    Email = email,
                    Role = MapRole(aadRole),   // patrz helper poniżej
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Users.Add(dbUser);
            }
            else
            {
                // delikatny update bez nadpisywania świadomych zmian z panelu
                if (string.IsNullOrWhiteSpace(dbUser.FirstName) && !string.IsNullOrWhiteSpace(firstName))
                    dbUser.FirstName = firstName;
                if (string.IsNullOrWhiteSpace(dbUser.LastName) && !string.IsNullOrWhiteSpace(lastName))
                    dbUser.LastName = lastName;

                // (opcjonalnie) aktualizuj rolę tylko jeśli pochodzi z AAD
                var mapped = MapRole(aadRole);
                if (mapped != dbUser.Role && mapped != UserRole.AM) // nie nadpisuj na AM gdy brak roli
                    dbUser.Role = mapped;

                dbUser.IsActive = true;
                dbUser.UpdatedAt = now;
            }

            await db.SaveChangesAsync();

            // dołóż własny claim z ID użytkownika z DB — wygodne w kontrolerach
            var identity = (ClaimsIdentity)ctx.Principal.Identity!;
            identity.AddClaim(new Claim("app:userId", dbUser.Id.ToString()));

            // (opcjonalnie) dołóż claim roli aby [Authorize(Roles="…")] działało nawet gdy rola pochodzi z DB
            identity.AddClaim(new Claim(identity.RoleClaimType, dbUser.Role.ToString()));
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

// === MIGRACJE + SEED PRZED Run ===
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrecomDbContext>();
    await db.Database.MigrateAsync();

    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            FirstName = "System",
            LastName = "Admin",
            Email = "admin@example.com",
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }
}

app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Trecom backend up");

// Prostą pętlę logowania/wylogowania możesz zostawić
app.MapGet("/login", (HttpContext ctx) =>
    Results.Challenge(new() { RedirectUri = "/api/me" },
        [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapGet("/logout", (HttpContext ctx) =>
    Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
        [OpenIdConnectDefaults.AuthenticationScheme,
         Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme]));

app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) => new
{
    Name = user.Identity?.Name,
    Email = user.FindFirstValue("preferred_username") ?? user.FindFirstValue(ClaimTypes.Email),
    TenantId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid"),
    ObjectId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier"),
    AppUserId = user.FindFirstValue("app:userId"),
    Roles = user.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "roles").Select(c => c.Value).ToArray()
});

app.MapGet("/debug/claims", [Authorize] (ClaimsPrincipal u)
    => u.Claims.Select(c => new { c.Type, c.Value }));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();

// ——— Helpery ———
static UserRole MapRole(string? aadRole) => aadRole?.ToLowerInvariant() switch
{
    "admin" => UserRole.Admin,
    "superadmin" => UserRole.SuperAdmin,
    "board" => UserRole.Board,
    "teamleader" => UserRole.TeamLeader,
    "am" => UserRole.AM,
    _ => UserRole.AM // domyślna
};
