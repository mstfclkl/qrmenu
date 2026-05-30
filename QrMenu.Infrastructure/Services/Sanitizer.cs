using System.Text.RegularExpressions;
namespace QrMenu.Infrastructure.Services;
public static class Sanitizer
{
    private static readonly Regex TagPattern = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"\s{2,}", RegexOptions.Compiled);
    public static string? StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return MultiSpace.Replace(TagPattern.Replace(input, " "), " ").Trim();
    }
    public static string? Clean(string? input) => StripHtml(input);
    public static string? CleanTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var tags = input.Split(',').Select(t => StripHtml(t)?.ToLowerInvariant()).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct();
        return string.Join(",", tags);
    }
}
