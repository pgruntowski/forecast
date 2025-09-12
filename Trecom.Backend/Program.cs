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

// DbContext – Postgres
builder.Services.AddDbContext<TrecomDbContext>(opts =>
{
    var cs = builder.Configuration.GetConnectionString("Postgres");
    opts.UseNpgsql(cs);
});

// (opcjonalnie) umo¿liw snake_case mappingu
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
var tenantId = builder.Configuration["AzureAd:TenantId"];

builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
{
    o.ResponseType = "code";
    o.UsePkce = true;
    o.SaveTokens = true;

    // kieruj i waliduj wy³¹cznie na Twój tenant
    o.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = $"https://login.microsoftonline.com/{tenantId}/v2.0"
    };

    // wymuœ œwie¿e logowanie (¿eby nie wziê³o starej sesji MSA)
    o.Prompt = "login";
});
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Trecom backend up");

// test:
app.MapGet("/login", (HttpContext ctx) =>
    Results.Challenge(new() { RedirectUri = "/api/me" },
        [OpenIdConnectDefaults.AuthenticationScheme]));

// Wylogowanie (opcjonalnie)
app.MapGet("/logout", (HttpContext ctx) =>
    Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
        [OpenIdConnectDefaults.AuthenticationScheme,
         Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme]));

// Testowy endpoint wymagaj¹cy logowania
app.MapGet("/api/me", [Authorize] (ClaimsPrincipal user) => new
{
    Name = user.Identity?.Name,
    Email = user.FindFirstValue("preferred_username") ?? user.FindFirstValue(ClaimTypes.Email),
    TenantId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid"),
    ObjectId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier"),
    Roles = user.Claims
                   .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
                   .Select(c => c.Value)
                   .ToArray()
});

app.MapGet("/debug/claims", [Authorize] (ClaimsPrincipal u)
    => u.Claims.Select(c => new { c.Type, c.Value }));

app.Run();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrecomDbContext>();
    if (!db.Users.Any())
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
        db.SaveChanges();
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrecomDbContext>();
    if (!db.Teams.Any())
    {
        var admin = db.Users.FirstOrDefault(u => u.Email == "admin@trecom.local")
                    ?? new User { Id = Guid.NewGuid(), FirstName = "System", LastName = "Admin", Email = "admin@trecom.local", Role = UserRole.Admin };
        if (admin.Id == default) db.Users.Add(admin);

        var team = new Team { Id = Guid.NewGuid(), Name = "Sales A", LeaderId = admin.Id };
        db.Teams.Add(team);

        var client = new Client { Id = Guid.NewGuid(), CanonicalName = "Firma1 S.A.", City = "Wroc³aw" };
        db.Clients.Add(client);
        db.ClientAliases.Add(new ClientAlias { Id = Guid.NewGuid(), Client = client, Alias = "Firma1" });
        db.ClientAliases.Add(new ClientAlias { Id = Guid.NewGuid(), Client = client, Alias = "Firma 1 SA" });

        db.SaveChanges();
    }
}