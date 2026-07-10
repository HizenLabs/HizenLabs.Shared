using System.Collections.Generic;

namespace HizenLabs.Shared.Lang;

/// <summary>
/// Base for a plugin's language class. Declare each message as a public string field whose
/// initializer is the English default - the field NAME is the lang key:
/// <code>
/// public sealed class MyPluginLang : BaseLang
/// {
///     public string NoPermission = "You do not have permission to use this command.";
///     public string SaveSuccess  = "Saved {0} entities in {1} ms";
/// }
/// </code>
/// Wire the plugin with the standard bridge:
/// <code>
/// private static readonly MyPluginLang L = new();
/// protected override void LoadDefaultMessages() { base.LoadDefaultMessages(); LangKit.Register(this, L); }
/// </code>
/// After registration each field holds its own key (self-keying), so call sites pass fields as
/// keys: <c>Msg.Chat(player, L.SaveSuccess, count, ms)</c>. Per-player language is the platform's
/// job (players pick via /lang, owners translate the generated oxide/lang files); we only
/// register the "en" defaults.
/// </summary>
public abstract class BaseLang
{
    /// <summary>The default (English) messages, captured on first registration so re-registration
    /// (the platform re-invokes LoadDefaultMessages on lang rebuilds) stays correct after the
    /// fields have been self-keyed.</summary>
    internal Dictionary<string, string> Defaults;
}
