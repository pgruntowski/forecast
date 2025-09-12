namespace Trecom.Backend.Models;

public sealed class Client
{
    public Guid Id { get; set; }

    // Kanoniczna nazwa (np. "Firma1 S.A.")
    public string CanonicalName { get; set; } = default!;

    // (opcjonalnie) identyfikatory do deduplikacji
    public string? TaxId { get; set; }    // NIP/KRS
    public string? City { get; set; }

    public ICollection<ClientAlias> Aliases { get; set; } = new List<ClientAlias>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
