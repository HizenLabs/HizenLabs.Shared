using HizenLabs.Bundler;
using Xunit;

namespace HizenLabs.Bundler.Tests;

/// <summary>
/// The kit wiring generator: <c>&lt;Plugin&gt;.Kit.g.cs</c> is bootstrapped purely from the
/// presence of the plugin's config/lang classes - authors never write the bridge.
/// </summary>
public class KitWireTests
{
    private const string Entry = """
        namespace Demo;

        public partial class Bar : PluginBase
        {
            private void Init() { }
        }
        """;

    private const string Config = """
        using HizenLabs.Shared.Config;

        namespace Demo;

        public sealed class BarConfig : BaseConfig
        {
            public int Interval { get; set; } = 60;
        }
        """;

    private const string Lang = """
        using HizenLabs.Shared.Lang;

        namespace Demo;

        public sealed class BarLang : BaseLang
        {
            public string Hello = "hi";
        }
        """;

    [Fact]
    public void Config_and_lang_produce_the_full_bridge()
    {
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Lang.cs", Lang) });

        Assert.NotNull(wiring);
        Assert.Contains("namespace Demo;", wiring);
        Assert.Contains("private static BarConfig config;", wiring);
        Assert.Contains("private static readonly BarLang L = new();", wiring);
        Assert.Contains("ConfigKit.Load<BarConfig>(this)", wiring);
        Assert.Contains("ConfigKit.Default<BarConfig>(this)", wiring);
        Assert.Contains("ConfigKit.Save(this, config)", wiring);
        Assert.Contains("LangKit.Register(this, L)", wiring);
    }

    [Fact]
    public void No_kit_classes_means_no_wiring_file()
    {
        Assert.Null(KitWire.Generate("Bar", new[] { ("Bar.cs", Entry) }));
    }

    [Fact]
    public void Lang_only_wires_messages_but_not_config()
    {
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Lang.cs", Lang) });

        Assert.Contains("LangKit.Register", wiring);
        Assert.DoesNotContain("ConfigKit", wiring);
        Assert.DoesNotContain("using HizenLabs.Shared.Config;", wiring);
    }

    [Fact]
    public void An_authored_override_wins_over_the_generated_one()
    {
        var authored = """
            namespace Demo;

            public partial class Bar
            {
                protected override void SaveConfig() { }
            }
            """;
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Custom.cs", authored) });

        Assert.Contains("LoadConfig()", wiring);
        Assert.DoesNotContain("SaveConfig()", wiring);
    }

    [Fact]
    public void Two_config_classes_violate_the_one_per_plugin_rule()
    {
        var second = Config.Replace("BarConfig", "OtherConfig");
        var ex = Assert.Throws<KitWireException>(() =>
            KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("A.cs", Config), ("B.cs", second) }));
        Assert.Contains("one config class per plugin", ex.Message);
    }
}
