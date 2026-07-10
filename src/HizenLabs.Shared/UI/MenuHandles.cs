using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Handle to a menu element. The id IS the element's client-side name - the one address that
/// works for creating children and updating across sends, on both platforms. Declare stable ids
/// as constants; the implicit conversion lifts them into handles with no allocation.
/// </summary>
public readonly struct MenuContainer
{
    public readonly string Id;

    public MenuContainer(string id)
    {
        Id = id;
    }

    public static implicit operator MenuContainer(string id) => new(id);
}

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

/// <summary>
/// A button slot in a layout shell: Set() fills it with the clickable + label for this open.
/// The visual fills the slot, so the shell controls where and how big the button is.
/// </summary>
public readonly struct MenuButton
{
    private readonly Menu _menu;
    public readonly MenuContainer Slot;

    internal MenuButton(Menu menu, MenuContainer slot)
    {
        _menu = menu;
        Slot = slot;
    }

    public void Set(string label, string command) =>
        Set(label, command, MenuTheme.Section, MenuTheme.BodyText);

    public void Set(string label, string command, Color background, Color textColor)
    {
        var button = _menu.CreateButton(Slot, MenuPosition.Full, MenuOffset.Zero, command, background);
        _menu.CreateText(button, MenuPosition.Full, MenuOffset.Zero, label, MenuTheme.BodyFontSize, textColor);
    }
}
