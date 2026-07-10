using System.Collections.Generic;

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
/// generates &lt;Plugin&gt;.Kit.g.cs with a static <c>L</c> field and a LoadDefaultMessages
/// override that calls <see cref="LangKit.Register{T}"/>. After registration each field holds
/// its own key (self-keying), so call sites pass fields as keys:
/// <c>Msg.Chat(player, L.SaveSuccess, count, ms)</c>. Per-player language is the platform's job
/// (players pick via /lang, owners translate the generated oxide/lang files); only the "en"
/// defaults are registered here.
/// </summary>
public abstract class BaseLang
{
    /// <summary>The default (English) messages, captured on first registration so re-registration
    /// (the platform re-invokes LoadDefaultMessages on lang rebuilds) stays correct after the
    /// fields have been self-keyed.</summary>
    internal Dictionary<string, string> Defaults;
}
