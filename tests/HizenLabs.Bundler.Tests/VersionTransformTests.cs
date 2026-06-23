using HizenLabs.Bundler;
using HizenLabs.Bundler.Transforms;
using Xunit;

namespace HizenLabs.Bundler.Tests;

/// <summary>
/// The golden-file harness (<see cref="TransformTests"/>) covers the in-place rewrite of an existing
/// [Info]. These cover what it can't: the dev-revision helper and the insert-when-missing path (one
/// case folder per transform name, so a second "version-*" folder won't bind).
/// </summary>
public class VersionTransformTests
{
    [Fact]
    public void DevRevision_is_monotonic_and_appends_a_fourth_component()
    {
        var t0 = new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(1);
        Assert.True(PluginVersion.DevRevision(t1) > PluginVersion.DevRevision(t0));

        var v = PluginVersion.WithDevRevision("1.2.3", t0);
        Assert.StartsWith("1.2.3.", v);
        Assert.True(Version.TryParse(v, out var parsed) && parsed.Revision >= 0);
    }

    [Fact]
    public void Adds_Info_attribute_when_missing()
    {
        var input = "namespace HizenLabs.Plugins.Sample;\n\npublic class SamplePlugin\n{\n}\n";
        var actual = TransformPipeline.Run(input, [new VersionTransform()], new TransformOptions { Version = "1.2.3" });
        Assert.Contains("[Info(\"Sample Plugin\", \"hizen\", \"1.2.3\")]\npublic class SamplePlugin", actual);
    }
}
