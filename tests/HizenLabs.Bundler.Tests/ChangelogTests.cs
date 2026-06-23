using HizenLabs.Bundler;
using Xunit;

namespace HizenLabs.Bundler.Tests;

public class ChangelogTests
{
    private const string Valid =
        "# Changelog\n\n" +
        "## [2.0.0] - 2026-06-23\n- breaking redo\n- another note\n\n" +
        "## [1.1.0] - 2026-05-01\n- a feature\n\n" +
        "## [1.0.1]\n- a fix\n\n" +
        "## [1.0.0] - 2026-04-01\n- first release\n";

    [Fact]
    public void TopVersion_is_the_newest_entry()
    {
        Assert.Equal("2.0.0", Changelog.TopVersion(Valid));
        Assert.Equal(new[] { "2.0.0", "1.1.0", "1.0.1", "1.0.0" }, Changelog.VersionTokens(Valid));
    }

    [Fact]
    public void TopNotes_is_the_newest_entry_body()
    {
        Assert.Equal("- breaking redo\n- another note", Changelog.TopNotes(Valid));
    }

    [Fact]
    public void Valid_changelog_has_no_problems()
    {
        Assert.Empty(Changelog.Validate(Valid));
    }

    [Fact]
    public void Oldest_must_be_1_0_0()
    {
        var text = "## [1.1.0]\n- x\n\n## [1.0.1]\n- y\n";
        var problems = Changelog.Validate(text);
        Assert.Contains(problems, p => p.Contains("oldest entry must be 1.0.0"));
    }

    [Fact]
    public void Rejects_a_skipped_patch()
    {
        // 1.0.2 directly above 1.0.0 skips 1.0.1.
        var text = "## [1.0.2]\n- x\n\n## [1.0.0]\n- y\n";
        Assert.Contains(Changelog.Validate(text), p => p.Contains("not a single major/minor/patch bump"));
    }

    [Fact]
    public void Rejects_a_non_resetting_major_bump()
    {
        // A major bump must reset minor+patch to 0: 1.1.0 -> 2.0.0, not 2.1.0.
        var text = "## [2.1.0]\n- x\n\n## [1.1.0]\n- y\n\n## [1.0.0]\n- z\n";
        Assert.Contains(Changelog.Validate(text), p => p.Contains("not a single major/minor/patch bump"));
    }

    [Fact]
    public void Rejects_a_non_strict_version_token()
    {
        var text = "## [1.0]\n- x\n";
        Assert.Contains(Changelog.Validate(text), p => p.Contains("not a strict X.Y.Z"));
    }

    [Fact]
    public void Empty_changelog_reports_no_entries()
    {
        Assert.Contains(Changelog.Validate("# Changelog\n\nnothing yet\n"), p => p.Contains("no version entries"));
    }
}
