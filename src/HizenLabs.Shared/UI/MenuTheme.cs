using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// The shared look: every layout shell and pre-formatted helper (AddTitle, ...) reads its colors
/// and sizes from here, so restyling all menus is an edit to this file. Plugins can still pass
/// explicit values at any call site; these are only the defaults.
/// </summary>
public static class MenuTheme
{
    public static readonly Color Background = new(0.09f, 0.09f, 0.09f, 0.96f);
    public static readonly Color Section = new(1f, 1f, 1f, 0.06f);
    public static readonly Color TitleText = Color.white;
    public static readonly Color BodyText = new(0.85f, 0.85f, 0.85f, 1f);

    public static readonly Color WindowBackground = new(0f, 0f, 0f, 1f);
    public static readonly Color Border = new(0.388f, 0.388f, 0.388f, 1f);
    public static readonly Color MutedText = new(0.388f, 0.388f, 0.388f, 1f);
    public static readonly Color Watermark = new(0.388f, 0.388f, 0.388f, 0.2f);

    public const MenuFont TitleFont = MenuFont.RobotoCondensedBold;
    public const int TitleFontSize = 18;
    public const int BodyFontSize = 14;
}
