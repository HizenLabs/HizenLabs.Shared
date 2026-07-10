using System;

namespace HizenLabs.Shared.Commands;

/// <summary>
/// Binds a handler method to a <see cref="CommandSetting"/> property in the plugin's config, by
/// property name:
/// <code>
/// [ConfigCommand(nameof(MyPluginConfig.CommandsSection.Menu))]
/// private void ShowMenuCommand(BasePlayer player, string command, string[] args) { ... }
/// </code>
/// The KitWire pre-build step resolves the name against the config type (build error if it
/// doesn't exist or is ambiguous) and generates the registration after config load - the
/// permission is registered and the command added via AddCovalenceCommand, so the name and
/// permission the server owner set in the JSON are what's live.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ConfigCommandAttribute : Attribute
{
    /// <summary>Name of the CommandSetting property in the config (e.g. nameof(...Menu)).</summary>
    public string Setting { get; }

    public ConfigCommandAttribute(string setting)
    {
        Setting = setting;
    }
}
