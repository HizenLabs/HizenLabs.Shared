namespace HizenLabs.Shared.Lang;

/// <summary>
/// Localized message access. The format comes from the platform's lang API (which resolves the
/// player's chosen language); args go through the pooled <see cref="TempArguments"/> and plain
/// string.Format, so nothing is allocated per call except the formatted string itself.
/// Bound to the plugin by <see cref="LangKit.Register{T}"/>; the bundler inlines a private copy
/// of this type into each plugin, so the static state is per-plugin at runtime.
/// </summary>
public static class Msg
{
    private static PluginBase _plugin;

    /// <summary>Binds message lookup to the plugin instance (done by LangKit.Register).</summary>
    public static void Bind(PluginBase plugin) => _plugin = plugin;

    /// <summary>The localized message for the key, formatted with any args (null player = server language).</summary>
    public static string Text(string key, BasePlayer player = null, object arg1 = null, object arg2 = null, object arg3 = null)
    {
        var format = _plugin.lang.GetMessage(key, _plugin, player?.UserIDString);
        if (arg1 == null)
        {
            return format;
        }

        using var args = TempArguments.Create(arg1, arg2, arg3);
        return args.StringFormat(format);
    }

    /// <summary>Sends the localized message to the player's chat.</summary>
    public static void Chat(BasePlayer player, string key, object arg1 = null, object arg2 = null, object arg3 = null)
    {
        player.ChatMessage(Text(key, player, arg1, arg2, arg3));
    }
}
