using System.ComponentModel.DataAnnotations;
using Trecom.Backend.Validation; // QuarterFormat / YearMonthFormat

namespace Trecom.Backend.Models;

public sealed class ProjectRevision
{
    public Guid ProjectId { get; set; }
    public ProjectHead Project { get; set; } = default!;
    public int Version { get; set; }                       // 1,2,3,...

    // Metadane
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public Guid AuthorId { get; set; }

    // Stan biznesowy
    [Required] public Guid AMId { get; set; }
    [Required] public Guid ClientId { get; set; }
    [Required] public int MarketId { get; set; }
    [Required, MaxLength(255)] public string Name { get; set; } = default!;
    [Required] public int StatusId { get; set; }

    [Required] public decimal Value { get; set; }
    [Required] public decimal Margin { get; set; }
    [Range(0, 100)] public int ProbabilityPercent { get; set; }

    [Required, QuarterFormat] public string DueQuarter { get; set; } = default!;
    [YearMonthFormat] public string? InvoiceMonth { get; set; }
    [Required, QuarterFormat] public string PaymentQuarter { get; set; } = default!;

    [Required] public Guid VendorId { get; set; }
    [Required] public int ArchitectureId { get; set; }

    public string? Comment { get; set; }
    public bool IsCanceled { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Wygoda (nie mapujemy)
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal WeightedMargin => Math.Round(Margin * ProbabilityPercent / 100m, 2);

    // Nawigacja do uczestników tej rewizji (opcjonalnie, ale wygodnie)
    public ICollection<ProjectParticipantRev> Participants { get; set; } = new List<ProjectParticipantRev>();
}
