using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// A container-bound builder: Add* methods need no container argument because the scope IS the
/// container. Structs all the way down - nothing to pool, nothing to leak.
/// </summary>
public readonly struct MenuScope
{
    private readonly Menu _menu;
    public readonly MenuContainer Container;

    internal MenuScope(Menu menu, MenuContainer container)
    {
        _menu = menu;
        Container = container;
    }

    public MenuScope AddPanel(MenuPosition position, MenuOffset offset, Color color, string name = "", bool needsCursor = false, bool needsKeyboard = false) =>
        new(_menu, _menu.CreatePanel(Container, position, offset, color, name, needsCursor, needsKeyboard));

    public MenuScope AddText(MenuPosition position, MenuOffset offset, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, MenuFont font = MenuFont.RobotoCondensedRegular, string name = "") =>
        new(_menu, _menu.CreateText(Container, position, offset, text, fontSize, color, align, font, name));

    /// <summary>Pre-formatted centered text filling the scope - the header one-liner.</summary>
    public MenuScope AddTitle(string text, int fontSize = MenuTheme.TitleFontSize) =>
        AddText(MenuPosition.Full, MenuOffset.Zero, text, fontSize, MenuTheme.TitleText, TextAnchor.MiddleCenter, MenuTheme.TitleFont);

    public MenuScope AddButton(MenuPosition position, MenuOffset offset, string command, Color color, string name = "") =>
        new(_menu, _menu.CreateButton(Container, position, offset, command, color, name));
}
