namespace HizenLabs.Shared.Config;

/// <summary>
/// Base for a plugin's config class (one per plugin). Derive and declare settings as properties
/// with initializers - the initializer IS the default. No wiring to write: the KitWire pre-build
/// step ('hizenbundle wire') sees the class and generates &lt;Plugin&gt;.Kit.g.cs with a static
/// <c>config</c> field and the LoadConfig/LoadDefaultConfig/SaveConfig overrides backed by
/// <see cref="ConfigKit"/>. Use <c>[JsonProperty("Friendly Name")]</c> for the keys server
/// owners see in the JSON file.
/// </summary>
public abstract class BaseConfig
{
}
