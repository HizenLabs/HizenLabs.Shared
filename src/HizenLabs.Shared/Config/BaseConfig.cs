namespace HizenLabs.Shared.Config;

/// <summary>
/// Base for a plugin's config class. Derive, declare settings as properties with initializers
/// (the initializer IS the default), and wire the plugin with the standard bridge:
/// <code>
/// private static MyPluginConfig config;
/// protected override void LoadConfig()        { base.LoadConfig(); config = ConfigKit.Load&lt;MyPluginConfig&gt;(this); }
/// protected override void LoadDefaultConfig() { base.LoadDefaultConfig(); config = ConfigKit.Default&lt;MyPluginConfig&gt;(this); }
/// protected override void SaveConfig()        { base.SaveConfig(); ConfigKit.Save(this, config); }
/// </code>
/// Use <c>[JsonProperty("Friendly Name")]</c> for the keys server owners see.
/// </summary>
public abstract class BaseConfig
{
}
