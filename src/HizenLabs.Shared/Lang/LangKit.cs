using System.Collections.Generic;
using System.Reflection;

namespace HizenLabs.Shared.Lang;

/// <summary>
/// Registration plumbing behind <see cref="BaseLang"/>. Reflection runs exactly once per plugin
/// load (cold path); after that, message lookup is the platform's lang API and formatting goes
/// through <see cref="Msg"/>'s pooled path.
/// </summary>
public static class LangKit
{
    /// <summary>
    /// Registers the lang class's fields as the plugin's "en" messages and self-keys the
    /// instance: after this call every field holds its own name, so fields are passed directly
    /// as lang keys. Also binds <see cref="Msg"/> to the plugin. Safe to call repeatedly (the
    /// platform re-invokes LoadDefaultMessages when lang files rebuild).
    /// </summary>
    public static void Register<T>(PluginBase plugin, T instance) where T : BaseLang
    {
        Msg.Bind(plugin);

        if (instance.Defaults is null)
        {
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var defaults = new Dictionary<string, string>(fields.Length);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string))
                    continue;
                defaults[field.Name] = (string)field.GetValue(instance) ?? string.Empty;
                field.SetValue(instance, field.Name); // self-key: the field now holds its lang key
            }
            instance.Defaults = defaults;
        }

        plugin.lang.RegisterMessages(instance.Defaults, plugin, "en");
    }
}
