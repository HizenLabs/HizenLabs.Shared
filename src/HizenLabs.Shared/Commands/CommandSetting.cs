using Newtonsoft.Json;

namespace HizenLabs.Shared.Commands;

/// <summary>
/// A server-owner-configurable command: the name players type and the permission that gates it,
/// both living in the plugin's config so owners can remap either without a plugin update.
/// Declare as a config property and bind a handler to it with <see cref="ConfigCommandAttribute"/>;
/// the generated wiring registers the permission and the covalence command after config load.
/// </summary>
public sealed class CommandSetting
{
    [JsonProperty("Command")]
    public string Command { get; set; }

    [JsonProperty("Permission")]
    public string Permission { get; set; }

    public CommandSetting()
    {
    }

    public CommandSetting(string command, string permission = null)
    {
        Command = command;
        Permission = permission;
    }
}
