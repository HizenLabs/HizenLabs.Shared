using HizenLabs.Bundler;
using Xunit;

namespace HizenLabs.Bundler.Tests;

/// <summary>
/// The kit wiring generator: <c>&lt;Plugin&gt;.Kit.g.cs</c> is bootstrapped purely from the
/// presence of the plugin's kit classes - authors never write the bridge.
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
        using HizenLabs.Shared.Commands;
        using HizenLabs.Shared.Config;

        namespace Demo;

        public sealed class BarConfig : BaseConfig
        {
            public GeneralSettings General { get; set; } = new();
            public CommandsSection Commands { get; set; } = new();

            public sealed class GeneralSettings
            {
                public int Interval { get; set; } = 60;
            }

            public sealed class CommandsSection
            {
                public CommandSetting Menu { get; set; } = new("bar", "bar.use");
            }
        }
        """;

    private const string Lang = """
        using HizenLabs.Shared.Lang;

        namespace Demo;

        public sealed class BarLang : BaseLang
        {
            public string Hello = "hi";
            public string Bye = "bye {0}";
        }
        """;

    [Fact]
    public void Config_and_lang_produce_the_full_bridge()
    {
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Lang.cs", Lang) });

        Assert.NotNull(wiring);
        Assert.Contains("namespace Demo;", wiring);
        Assert.Contains("private static BarConfig config;", wiring);
        Assert.Contains("ConfigKit.Load<BarConfig>(this)", wiring);
        Assert.Contains("ConfigKit.Default<BarConfig>(this)", wiring);
        Assert.Contains("ConfigKit.Save(this, config)", wiring);
        Assert.Contains("LangKit.BuildDefaults<BarLang>()", wiring);
    }

    [Fact]
    public void Lang_produces_the_enum_and_the_msg_accessor()
    {
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Lang.cs", Lang) });

        // Enum members mirror the lang fields, in declaration order.
        Assert.Contains("private enum LangKeys", wiring);
        Assert.Contains("        Hello,\n        Bye,", wiring);

        // msg.Get/msg.Chat resolve keys through a generated name table (no enum.ToString()).
        Assert.Contains("private static readonly KitMessages msg = new();", wiring);
        Assert.Contains("nameof(BarLang.Hello),", wiring);
        Assert.Contains("public string Get(LangKeys key", wiring);
        Assert.Contains("public void Chat(BasePlayer player, LangKeys key", wiring);

        // Lang-only plugins get no config machinery.
        Assert.DoesNotContain("ConfigKit", wiring);
        Assert.DoesNotContain("using HizenLabs.Shared.Config;", wiring);
    }

    [Fact]
    public void No_kit_classes_means_no_wiring_file()
    {
        Assert.Null(KitWire.Generate("Bar", new[] { ("Bar.cs", Entry) }));
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

    // ---- [ConfigCommand] bindings ----

    private const string CommandHandler = """
        using HizenLabs.Shared.Commands;

        namespace Demo;

        public partial class Bar
        {
            [ConfigCommand(nameof(BarConfig.CommandsSection.Menu))]
            private void ShowMenuCommand(BasePlayer player, string command, string[] args) { }
        }
        """;

    [Fact]
    public void ConfigCommand_generates_permission_and_command_registration()
    {
        var wiring = KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Commands.cs", CommandHandler) });

        // The setting name resolves to its dotted path from the config root.
        Assert.Contains("var cmd0 = config.Commands.Menu;", wiring);
        Assert.Contains("permission.RegisterPermission(cmd0.Permission, this);", wiring);
        Assert.Contains("AddCovalenceCommand(cmd0.Command, nameof(ShowMenuCommand), cmd0.Permission);", wiring);
    }

    [Fact]
    public void ConfigCommand_with_an_unknown_setting_fails_the_build()
    {
        var handler = CommandHandler.Replace("nameof(BarConfig.CommandsSection.Menu)", "\"Nope\"");
        var ex = Assert.Throws<KitWireException>(() =>
            KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Commands.cs", handler) }));
        Assert.Contains("no matching CommandSetting", ex.Message);
    }

    [Fact]
    public void ConfigCommand_alongside_a_handwritten_LoadConfig_fails_the_build()
    {
        var authored = """
            namespace Demo;

            public partial class Bar
            {
                protected override void LoadConfig() { }
            }
            """;
        var ex = Assert.Throws<KitWireException>(() =>
            KitWire.Generate("Bar", new[] { ("Bar.cs", Entry), ("Bar.Config.cs", Config), ("Bar.Commands.cs", CommandHandler), ("Bar.Custom.cs", authored) }));
        Assert.Contains("LoadConfig is hand-written", ex.Message);
    }
}
