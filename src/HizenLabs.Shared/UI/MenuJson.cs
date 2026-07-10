using System.Globalization;
using System.Text;
using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Writes CUI elements directly as JSON - the exact flat format CommunityEntity.AddUI parses,
/// so there is no intermediate element tree to build or pool. Component writers assume they run
/// between BeginElement/EndElement and manage their own commas.
/// </summary>
internal static class MenuJson
{
    private const string DefaultFont = "robotocondensed-regular.ttf";

    private static readonly string[] _alignNames =
    {
        "UpperLeft", "UpperCenter", "UpperRight",
        "MiddleLeft", "MiddleCenter", "MiddleRight",
        "LowerLeft", "LowerCenter", "LowerRight",
    };

    /// <summary>Opens an element. Pass a null parent for update elements (the name addresses an
    /// existing element; components below patch it).</summary>
    public static void BeginElement(StringBuilder sb, ref int count, string name, string parent, bool update)
    {
        if (count > 0)
            sb.Append(',');
        count++;

        sb.Append("{\"name\":\"");
        Escape(sb, name);
        sb.Append('"');
        if (parent is not null)
        {
            sb.Append(",\"parent\":\"");
            Escape(sb, parent);
            sb.Append('"');
        }
        if (update)
            sb.Append(",\"update\":true");
        sb.Append(",\"components\":[");
    }

    public static void EndElement(StringBuilder sb)
    {
        sb.Append("]}");
    }

    public static void Rect(StringBuilder sb, MenuPosition position, MenuOffset offset)
    {
        Comma(sb);
        sb.Append("{\"type\":\"RectTransform\",\"anchormin\":\"");
        F(sb, position.XMin); sb.Append(' '); F(sb, position.YMin);
        sb.Append("\",\"anchormax\":\"");
        F(sb, position.XMax); sb.Append(' '); F(sb, position.YMax);
        sb.Append("\",\"offsetmin\":\"");
        F(sb, offset.XMin); sb.Append(' '); F(sb, offset.YMin);
        sb.Append("\",\"offsetmax\":\"");
        F(sb, offset.XMax); sb.Append(' '); F(sb, offset.YMax);
        sb.Append("\"}");
    }

    public static void Image(StringBuilder sb, Color color)
    {
        Comma(sb);
        sb.Append("{\"type\":\"UnityEngine.UI.Image\",\"color\":\"");
        sb.Append(MenuColor.ToCui(color));
        sb.Append("\"}");
    }

    public static void Text(StringBuilder sb, string text, int fontSize, Color color, TextAnchor align)
    {
        Comma(sb);
        sb.Append("{\"type\":\"UnityEngine.UI.Text\",\"text\":\"");
        Escape(sb, text);
        sb.Append("\",\"fontSize\":");
        sb.Append(fontSize);
        sb.Append(",\"font\":\"");
        sb.Append(DefaultFont);
        sb.Append("\",\"align\":\"");
        sb.Append(_alignNames[(int)align]);
        sb.Append("\",\"color\":\"");
        sb.Append(MenuColor.ToCui(color));
        sb.Append("\"}");
    }

    public static void Button(StringBuilder sb, string command, Color color)
    {
        Comma(sb);
        sb.Append("{\"type\":\"UnityEngine.UI.Button\",\"command\":\"");
        Escape(sb, command);
        sb.Append("\",\"color\":\"");
        sb.Append(MenuColor.ToCui(color));
        sb.Append("\"}");
    }

    public static void Cursor(StringBuilder sb)
    {
        Comma(sb);
        sb.Append("{\"type\":\"NeedsCursor\"}");
    }

    public static void Keyboard(StringBuilder sb)
    {
        Comma(sb);
        sb.Append("{\"type\":\"NeedsKeyboard\"}");
    }

    private static void Comma(StringBuilder sb)
    {
        if (sb[sb.Length - 1] != '[')
            sb.Append(',');
    }

    /// <summary>Float in the invariant "0.###" shape the client parses.</summary>
    private static void F(StringBuilder sb, float value)
    {
        sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void Escape(StringBuilder sb, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
    }
}
