using HizenLabs.Bundler.Transforms;
using Xunit;

namespace HizenLabs.Bundler.Tests;

/// <summary>
/// Golden-file tests: each folder under cases/&lt;name&gt;/ holds input.cs + expected.cs, where
/// &lt;name&gt; matches an <see cref="IPluginTransform.Name"/>. Add a transform -> add a case
/// folder. To author a case, write expected.cs as the output you want, then make the transform
/// produce it. Comparison ignores trailing whitespace and CRLF vs LF.
/// </summary>
public class TransformTests
{
    private static string CasesDir => Path.Combine(AppContext.BaseDirectory, "cases");

    public static IEnumerable<object[]> Cases() =>
        Directory.EnumerateDirectories(CasesDir).Select(d => new object[] { Path.GetFileName(d)! });

    [Theory]
    [MemberData(nameof(Cases))]
    public void Transform_produces_expected_output(string name)
    {
        var dir = Path.Combine(CasesDir, name);
        var input = File.ReadAllText(Path.Combine(dir, "input.cs"));
        var expected = File.ReadAllText(Path.Combine(dir, "expected.cs"));

        var transform = TransformPipeline.Default.Single(t => t.Name == name);
        var actual = TransformPipeline.Run(input, [transform]);

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static string Normalize(string s) =>
        string.Join('\n', s.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd())).TrimEnd();
}
