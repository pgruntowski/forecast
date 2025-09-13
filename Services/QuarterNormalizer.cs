using System.Text.RegularExpressions;

namespace Trecom.Backend.Services;

public static class QuarterNormalizer
{
    public static string Normalize(string input)
    {
        var s = input.Trim().Replace("-", " ");
        s = Regex.Replace(s, @"\s+", " ");                // pojedyncze spacje
        s = s.ToUpperInvariant();
        s = Regex.Replace(s, @"Q\s*([1-4])", "Q$1");      // "Q 1" -> "Q1"
        return s;
    }

    public static string? NormalizeYearMonth(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return input.Trim();
    }
}
