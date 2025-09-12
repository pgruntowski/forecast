namespace Trecom.Backend.Dto.Projects;

public sealed class ProjectDto
{
    public Guid ProjectId { get; set; }
    public int Version { get; set; }
    public DateTime EffectiveAt { get; set; }

    public Guid AMId { get; set; }
    public Guid ClientId { get; set; }
    public int MarketId { get; set; }
    public string Name { get; set; } = default!;
    public int StatusId { get; set; }

    public decimal Value { get; set; }
    public decimal Margin { get; set; }
    public int ProbabilityPercent { get; set; }
    public string DueQuarter { get; set; } = default!;
    public string? InvoiceMonth { get; set; }
    public string PaymentQuarter { get; set; } = default!;
    public Guid VendorId { get; set; }
    public int ArchitectureId { get; set; }
    public string? Comment { get; set; }
    public bool IsCanceled { get; set; }

    public decimal WeightedMargin { get; set; }

    // Pomocniczo: lista uczestników aktualnej rewizji (do widoku)
    public List<Guid> Participants { get; set; } = new();
}
