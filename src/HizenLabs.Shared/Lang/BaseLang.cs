namespace HizenLabs.Shared.Lang;

/// <summary>
/// Base for a plugin's language class (one per plugin). Declare each message as a public string
/// field whose initializer is the English default - the field NAME is the lang key:
/// <code>
/// public sealed class MyPluginLang : BaseLang
/// {
///     public string NoPermission = "You do not have permission to use this command.";
///     public string SaveSuccess  = "Saved {0} entities in {1} ms";
/// }
/// </code>
/// No wiring to write: the KitWire pre-build step ('hizenbundle wire') sees the class and
/// generates &lt;Plugin&gt;.Kit.g.cs with a <c>LangKeys</c> enum (one member per field), a
/// <c>msg</c> accessor (<c>msg.Get(LangKeys.SaveSuccess, player, count, ms)</c> /
/// <c>msg.Chat(player, LangKeys.NoPermission)</c>), and a LoadDefaultMessages override that
/// registers the defaults. Per-player language is the platform's job (players pick via /lang,
/// owners translate the generated oxide/lang files); only the "en" defaults are registered here.
/// </summary>
public abstract class BaseLang : Facepunch.Pool.IPooled
{
    // Instances are transient default-holders rented by LangKit during registration; the field
    // initializers re-run on construction, not on pool cycles, so defaults are read before Free.
    public void EnterPool()
    {
    }

    public void LeavePool()
    {
    }
}
