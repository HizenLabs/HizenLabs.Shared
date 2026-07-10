using UnityEngine;

namespace HizenLabs.Shared.UI;

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
