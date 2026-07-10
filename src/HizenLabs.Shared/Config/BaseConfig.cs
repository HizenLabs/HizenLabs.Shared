namespace HizenLabs.Shared.Config;

/// <summary>
/// Base for a plugin's config class (one per plugin). Derive and declare settings as properties
/// with initializers - the initializer IS the default. No wiring to write: the KitWire pre-build
/// step ('hizenbundle wire') sees the class and generates &lt;Plugin&gt;.Kit.g.cs with a static
/// <c>config</c> field and the LoadConfig/LoadDefaultConfig/SaveConfig overrides backed by
/// <see cref="ConfigKit"/>. Use <c>[JsonProperty("Friendly Name")]</c> for the keys server
/// owners see in the JSON file.
/// Runtime changes persist via <c>config.Save()</c>:
/// <code>
/// config.General.UpdateInterval = 120f;
/// config.Save();
/// </code>
/// </summary>
public abstract class BaseConfig
{
    // Set by ConfigKit when the config is loaded/created; internal so Newtonsoft never sees it.
    internal PluginBase Plugin;

    /// <summary>Writes the current state to the plugin's config file.</summary>
    public void Save()
    {
        if (Plugin != null)
        {
            ConfigKit.Save(Plugin, this);
        }
    }
}
