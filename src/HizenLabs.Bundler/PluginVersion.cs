namespace HizenLabs.Bundler;

/// <summary>
/// The public version of a plugin always comes from its changelog (<see cref="Changelog.TopVersion"/>,
/// an X.Y.Z). For LOCAL builds we append a monotonic dev revision -> X.Y.Z.&lt;rev&gt; so every deploy
/// is a distinct version: Carbon/Oxide cache by version, so without a bump a hot-reload can be
/// skipped. Releases omit the revision and ship the clean X.Y.Z.
/// </summary>
public static class PluginVersion
{
    // Fixed epoch for the dev revision. Minutes since this stay well under int.MaxValue for ~4000
    // years and never reset (unlike minute-of-month/day), so local versions strictly increase.
    private static readonly DateTime DevEpoch = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Minutes since the dev epoch - the 4th (revision) component for local builds.</summary>
    public static int DevRevision(DateTime now) => (int)(now.ToUniversalTime() - DevEpoch).TotalMinutes;

    /// <summary>Appends the dev revision to a public X.Y.Z version, giving X.Y.Z.&lt;rev&gt;.</summary>
    public static string WithDevRevision(string version, DateTime now) => $"{version}.{DevRevision(now)}";
}
