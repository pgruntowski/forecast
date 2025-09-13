using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Trecom.Backend.Validation;

public sealed class YearMonthFormatAttribute : ValidationAttribute
{
    private static readonly Regex Rx = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is null) return ValidationResult.Success; // nullable allowed
        if (value is not string s) return new ValidationResult("Invalid value.");
        if (!Rx.IsMatch(s.Trim())) return new ValidationResult("Month must match 'YYYY-MM' (e.g. 2025-07).");
        return ValidationResult.Success;
    }
}