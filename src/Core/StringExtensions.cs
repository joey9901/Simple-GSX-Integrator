namespace SimpleGsxIntegrator.Core;

public static class StringExtensions
{
    public static bool HasAny(this string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static bool HasAll(this string text, params string[] keywords) =>
        keywords.All(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
