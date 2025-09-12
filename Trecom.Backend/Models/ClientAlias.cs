namespace Trecom.Backend.Models;

public sealed class ClientAlias
{
    public Guid Id { get; set; }

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    // Dowolne warianty nazw wpisywane przez użytkowników
    public string Alias { get; set; } = default!;
}
