using System.Text.RegularExpressions;

namespace HizenLabs.Bundler;

/// <summary>
/// Parses a plugin's CHANGELOG.md (Keep a Changelog style) and enforces our version policy. Entries
/// are <c>## [X.Y.Z] - optional date</c> headers, newest first; the bullets beneath one are its
/// release notes. The version is always the top entry - the trailing date is informational and
/// ignored. Policy (see docs/bundler-roadmap.md): the oldest entry must be 1.0.0, and each newer
/// entry must be exactly one canonical semver bump above the one below it (major resets minor+patch
/// to 0, minor resets patch to 0).
/// </summary>
public static class Changelog
{
    // "## [ 1.2.3 ] - 2026-06-23" captures "1.2.3". Anchored to a line start; trailing text ignored.
    private static readonly Regex HeaderPattern =
        new(@"^[ \t]*##[ \t]*\[[ \t]*([^\]\s]+)[ \t]*\]", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>Version tokens in document order (newest first). Empty if the file has no entries.</summary>
    public static IReadOnlyList<string> VersionTokens(string text) =>
        HeaderPattern.Matches(text).Select(m => m.Groups[1].Value).ToList();

    /// <summary>The newest (top) version token, or null if the changelog has no entries.</summary>
    public static string? TopVersion(string text) => VersionTokens(text).FirstOrDefault();

    /// <summary>
    /// The bullet body of the newest entry: lines after its header up to the next "##" header,
    /// trimmed. Used as the GitHub release notes. Empty when there are no entries.
    /// </summary>
    public static string TopNotes(string text)
    {
        var header = HeaderPattern.Match(text);
        if (!header.Success) return "";
        var bodyStart = text.IndexOf('\n', header.Index);
        if (bodyStart < 0) return "";
        var next = HeaderPattern.Match(text, bodyStart);
        var bodyEnd = next.Success ? next.Index : text.Length;
        return text[bodyStart..bodyEnd].Trim('\n', '\r', ' ', '\t');
    }

    /// <summary>
    /// Validates the policy and returns the problems found (empty list = valid): every token must be
    /// a strict X.Y.Z, the oldest must be 1.0.0, and each step up must be a canonical bump.
    /// </summary>
    public static IReadOnlyList<string> Validate(string text)
    {
        var problems = new List<string>();
        var tokens = VersionTokens(text);
        if (tokens.Count == 0)
        {
            problems.Add("no version entries found (expected '## [X.Y.Z]' headers)");
            return problems;
        }

        // Newest-first. Parse strict X.Y.Z up front; bad tokens make ordering meaningless, so stop.
        var versions = new List<(int Major, int Minor, int Patch)>();
        foreach (var t in tokens)
        {
            if (TryParseStrict(t, out var v))
                versions.Add(v);
            else
                problems.Add($"version '{t}' is not a strict X.Y.Z");
        }
        if (problems.Count > 0) return problems;

        if (versions[^1] != (1, 0, 0))
            problems.Add($"oldest entry must be 1.0.0 but is {Fmt(versions[^1])}");

        // Each newer (i) must be exactly one canonical bump above the older entry below it (i+1).
        for (var i = 0; i < versions.Count - 1; i++)
        {
            if (!IsCanonicalBump(older: versions[i + 1], newer: versions[i]))
                problems.Add($"{Fmt(versions[i])} is not a single major/minor/patch bump above {Fmt(versions[i + 1])}");
        }

        return problems;
    }

    private static bool IsCanonicalBump((int Major, int Minor, int Patch) older, (int Major, int Minor, int Patch) newer) =>
        (newer.Major == older.Major + 1 && newer.Minor == 0 && newer.Patch == 0) ||
        (newer.Major == older.Major && newer.Minor == older.Minor + 1 && newer.Patch == 0) ||
        (newer.Major == older.Major && newer.Minor == older.Minor && newer.Patch == older.Patch + 1);

    private static bool TryParseStrict(string token, out (int Major, int Minor, int Patch) v)
    {
        v = default;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;
        if (int.TryParse(parts[0], out var major) && major >= 0 &&
            int.TryParse(parts[1], out var minor) && minor >= 0 &&
            int.TryParse(parts[2], out var patch) && patch >= 0)
        {
            v = (major, minor, patch);
            return true;
        }
        return false;
    }

    private static string Fmt((int Major, int Minor, int Patch) v) => $"{v.Major}.{v.Minor}.{v.Patch}";
}
