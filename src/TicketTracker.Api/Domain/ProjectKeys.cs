using System.Text.RegularExpressions;

namespace TicketTracker.Api.Domain;

public static class ProjectKeys
{
    private static readonly Regex Pattern = new(
        "^[A-Z]{2,10}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Normalize(string key) => key.Trim();

    public static bool IsValid(string? key) => key is not null && Pattern.IsMatch(key);
}
