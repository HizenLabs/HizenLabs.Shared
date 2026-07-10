using System.Collections.Generic;
using System.Reflection;

namespace HizenLabs.Shared.Lang;

/// <summary>
/// Runtime behind <see cref="BaseLang"/> and the generated <c>msg</c> accessor. Deliberately
/// plugin-free: the platform's lang members (lang.RegisterMessages / lang.GetMessage) are
/// protected on the plugin base, so the generated wiring inside the plugin class makes those
/// calls and this kit only does the work that needs no plugin access.
/// </summary>
public static class LangKit
{
    /// <summary>
    /// The default (English) messages of a lang class: field name = key, field initializer =
    /// text. Reflection runs only here, at plugin load / lang rebuild (cold path).
    /// </summary>
    public static Dictionary<string, string> BuildDefaults<T>() where T : BaseLang, new()
    {
        var defaults = new T();
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var messages = new Dictionary<string, string>(fields.Length);
        foreach (var field in fields)
        {
            if (field.FieldType != typeof(string))
                continue;
            messages[field.Name] = (string)field.GetValue(defaults) ?? string.Empty;
        }
        return messages;
    }

    /// <summary>
    /// Formats a resolved message with the given args through the pooled
    /// <see cref="TempArguments"/>; nothing is allocated except the result string. No-arg calls
    /// return the format unchanged.
    /// </summary>
    public static string Format(string format, object arg1 = null, object arg2 = null, object arg3 = null)
    {
        if (arg1 == null)
        {
            return format;
        }

        using var args = TempArguments.Create(arg1, arg2, arg3);
        return args.StringFormat(format);
    }
}
