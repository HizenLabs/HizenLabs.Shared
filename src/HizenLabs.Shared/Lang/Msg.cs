using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace HizenLabs.Shared.Lang;

/// <summary>
/// Localized message access with an allocation-light format path, built for a long-lived
/// single-threaded server process:
///   - fixed-arity generic overloads: no params object[] and no boxing for common value types
///   - format strings are parsed ONCE into a template, cached per string INSTANCE in a
///     ConditionalWeakTable (lang.GetMessage returns the same cached instance every call, so the
///     lookup is reference-keyed; edited/reloaded lang files fall out of the table naturally)
///   - a reused StringBuilder fills the template (single-threaded; nested calls fall back to a
///     fresh builder)
/// The one unavoidable allocation is the result string itself. Formats using specifiers or
/// alignment ({0:0.0}, {0,5}) fall back to string.Format. A translation referencing a slot the
/// call didn't supply renders the raw placeholder instead of throwing - a bad lang file must
/// never take down a hook.
/// Bound to the plugin by <see cref="LangKit.Register{T}"/>; each bundled plugin carries its own
/// copy of this type, so the static state is per-plugin at runtime.
/// </summary>
public static class Msg
{
    private static PluginBase _plugin;

    /// <summary>Binds message lookup to the plugin instance (done by LangKit.Register).</summary>
    public static void Bind(PluginBase plugin) => _plugin = plugin;

    /// <summary>The message format for the given key, in the player's language (null player = server language).</summary>
    public static string Text(string key, BasePlayer player = null)
        => GetFormat(key, player);

    public static string Text<T1>(string key, BasePlayer player, T1 a1)
        => Format(GetFormat(key, player), 1, a1, 0, 0, 0);

    public static string Text<T1, T2>(string key, BasePlayer player, T1 a1, T2 a2)
        => Format(GetFormat(key, player), 2, a1, a2, 0, 0);

    public static string Text<T1, T2, T3>(string key, BasePlayer player, T1 a1, T2 a2, T3 a3)
        => Format(GetFormat(key, player), 3, a1, a2, a3, 0);

    public static string Text<T1, T2, T3, T4>(string key, BasePlayer player, T1 a1, T2 a2, T3 a3, T4 a4)
        => Format(GetFormat(key, player), 4, a1, a2, a3, a4);

    public static void Chat(BasePlayer player, string key)
        => player.ChatMessage(Text(key, player));

    public static void Chat<T1>(BasePlayer player, string key, T1 a1)
        => player.ChatMessage(Text(key, player, a1));

    public static void Chat<T1, T2>(BasePlayer player, string key, T1 a1, T2 a2)
        => player.ChatMessage(Text(key, player, a1, a2));

    public static void Chat<T1, T2, T3>(BasePlayer player, string key, T1 a1, T2 a2, T3 a3)
        => player.ChatMessage(Text(key, player, a1, a2, a3));

    public static void Chat<T1, T2, T3, T4>(BasePlayer player, string key, T1 a1, T2 a2, T3 a3, T4 a4)
        => player.ChatMessage(Text(key, player, a1, a2, a3, a4));

    private static string GetFormat(string key, BasePlayer player)
        => _plugin.lang.GetMessage(key, _plugin, player?.UserIDString);

    // ---- template formatting ----

    private static string Format<T1, T2, T3, T4>(string format, int argCount, T1 a1, T2 a2, T3 a3, T4 a4)
    {
        var template = _templates.GetValue(format, static f => Template.Parse(f));
        if (template.IsPassthrough)
            return Passthrough(format, argCount, a1, a2, a3, a4);

        var sb = Rent();
        var literals = template.Literals;
        var slots = template.Slots;
        for (var i = 0; i < slots.Length; i++)
        {
            sb.Append(literals[i]);
            var slot = slots[i];
            if (slot >= argCount)
            {
                sb.Append('{').Append(slot).Append('}'); // missing arg: render the placeholder
                continue;
            }
            switch (slot)
            {
                case 0: Append(sb, a1); break;
                case 1: Append(sb, a2); break;
                case 2: Append(sb, a3); break;
                default: Append(sb, a4); break;
            }
        }
        sb.Append(literals[slots.Length]);
        var result = sb.ToString();
        Return(sb);
        return result;
    }

    private static void Append<T>(StringBuilder sb, T value)
    {
        // The JIT specializes these per value-type T, so the matched branch is a direct,
        // box-free append and the rest fold away.
        if (value is string s) { sb.Append(s); return; }
        if (value is int i) { sb.Append(i); return; }
        if (value is uint ui) { sb.Append(ui); return; }
        if (value is long l) { sb.Append(l); return; }
        if (value is ulong ul) { sb.Append(ul); return; }
        if (value is float f) { sb.Append(f); return; }
        if (value is double d) { sb.Append(d); return; }
        if (value is bool b) { sb.Append(b); return; }
        if (value is char c) { sb.Append(c); return; }
        sb.Append(value?.ToString()); // constrained call: no box when the struct overrides ToString
    }

    private static string Passthrough<T1, T2, T3, T4>(string format, int argCount, T1 a1, T2 a2, T3 a3, T4 a4)
    {
        try
        {
            return argCount switch
            {
                1 => string.Format(format, a1),
                2 => string.Format(format, a1, a2),
                3 => string.Format(format, a1, a2, a3),
                _ => string.Format(format, a1, a2, a3, a4),
            };
        }
        catch (FormatException)
        {
            return format; // malformed translation: show it raw rather than throw in a hook
        }
    }

    private static readonly ConditionalWeakTable<string, Template> _templates = new();

    private sealed class Template
    {
        /// <summary>Literal runs between arg slots; always Slots.Length + 1 entries.</summary>
        public readonly string[] Literals;
        public readonly int[] Slots;

        /// <summary>Format uses features the fast path doesn't cover ({0:spec}, {0,align}, stray braces).</summary>
        public bool IsPassthrough => Literals is null;

        private Template(string[] literals, int[] slots)
        {
            Literals = literals;
            Slots = slots;
        }

        private static readonly Template _passthrough = new(null, null);

        public static Template Parse(string format)
        {
            var literals = new List<string>(4);
            var slots = new List<int>(4);
            var current = new StringBuilder(format.Length);

            for (var i = 0; i < format.Length; i++)
            {
                var ch = format[i];
                if (ch == '{')
                {
                    if (i + 1 < format.Length && format[i + 1] == '{') { current.Append('{'); i++; continue; }
                    var slot = 0;
                    var j = i + 1;
                    var digits = 0;
                    while (j < format.Length && format[j] >= '0' && format[j] <= '9')
                    {
                        slot = slot * 10 + (format[j] - '0');
                        j++; digits++;
                    }
                    if (digits == 0 || j >= format.Length || format[j] != '}')
                        return _passthrough; // {0:spec}, {0,align}, or malformed
                    literals.Add(current.ToString());
                    current.Clear();
                    slots.Add(slot);
                    i = j;
                    continue;
                }
                if (ch == '}')
                {
                    if (i + 1 < format.Length && format[i + 1] == '}') { current.Append('}'); i++; continue; }
                    return _passthrough;
                }
                current.Append(ch);
            }

            literals.Add(current.ToString());
            return new Template(literals.ToArray(), slots.ToArray());
        }
    }

    // Reused builder: the server runs plugin code on Unity's main thread, so a single cached
    // instance covers steady state; a nested/concurrent call just pays for a fresh one.
    private static readonly StringBuilder _builder = new(256);
    private static bool _builderInUse;

    private static StringBuilder Rent()
    {
        if (_builderInUse)
            return new StringBuilder(256);
        _builderInUse = true;
        return _builder.Clear();
    }

    private static void Return(StringBuilder sb)
    {
        if (ReferenceEquals(sb, _builder))
            _builderInUse = false;
    }
}
