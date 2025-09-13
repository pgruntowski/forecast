using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Trecom.Backend.Validation;

public sealed class QuarterFormatAttribute : ValidationAttribute
{
    private static readonly Regex Rx = new(@"^\d{4}\sQ[1-4]$", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not string s) return new ValidationResult("Quarter is required.");
        s = s.Trim();
        if (!Rx.IsMatch(s)) return new ValidationResult("Quarter must match 'YYYY QN' (e.g. 2025 Q3).");
        return ValidationResult.Success;
    }
}