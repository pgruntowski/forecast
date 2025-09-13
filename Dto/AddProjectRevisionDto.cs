using System.ComponentModel.DataAnnotations;
using Trecom.Backend.Validation;

namespace Trecom.Backend.Dto.Projects;

public sealed class AddProjectRevisionDto
{
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

    public DateTime? EffectiveAt { get; set; } // null -> UtcNow

    // NOWE: pełna lista uczestników dla tej rewizji (jeśli null -> sklonujemy poprzednią)
    public List<Guid>? Participants { get; set; }
}
