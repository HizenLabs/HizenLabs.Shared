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

    /// <summary>A pure positioning container (no visual) - section bounds, slots, spacers.
    /// replace makes the add destroy any same-named predecessor first (see Menu.CreateContainer).</summary>
    public MenuScope AddContainer(MenuPosition position, MenuOffset offset, string name = "", bool replace = false) =>
        new(_menu, _menu.CreateContainer(Container, position, offset, name, replace));

    public MenuScope AddPanel(MenuPosition position, MenuOffset offset, Color color, string name = "", bool needsCursor = false, bool needsKeyboard = false) =>
        new(_menu, _menu.CreatePanel(Container, position, offset, color, name, needsCursor, needsKeyboard));

    public MenuScope AddText(MenuPosition position, MenuOffset offset, string text, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleCenter, MenuFont font = MenuFont.RobotoCondensedRegular, string name = "") =>
        new(_menu, _menu.CreateText(Container, position, offset, text, fontSize, color, align, font, name));

    /// <summary>Pre-formatted centered text filling the scope - the header one-liner.</summary>
    public MenuScope AddTitle(string text, int fontSize = MenuTheme.TitleFontSize) =>
        AddText(MenuPosition.Full, MenuOffset.Zero, text, fontSize, MenuTheme.TitleText, TextAnchor.MiddleCenter, MenuTheme.TitleFont);

    public MenuScope AddButton(MenuPosition position, MenuOffset offset, string command, Color color, string name = "") =>
        new(_menu, _menu.CreateButton(Container, position, offset, command, color, name));

    /// <summary>An on/off switch bound to a command (see <see cref="Menu.CreateToggle"/>).</summary>
    public MenuContainer AddToggle(MenuPosition position, MenuOffset offset, bool isOn, string command, string name = "") =>
        _menu.CreateToggle(Container, position, offset, isOn, command, name);

    /// <summary>A minus/value/plus stepper (see <see cref="Menu.CreateStepper"/>).</summary>
    public MenuContainer AddStepper(MenuPosition position, MenuOffset offset, string value, string command, string name = "", float buttonWidth = 30f) =>
        _menu.CreateStepper(Container, position, offset, value, command, name, buttonWidth);

    /// <summary>A row of exclusive options (see <see cref="Menu.CreateSegmented"/>).</summary>
    public MenuContainer AddSegmented(MenuPosition position, MenuOffset offset, string[] options, int active, string command, string name = "", float gap = 2f) =>
        _menu.CreateSegmented(Container, position, offset, options, active, command, name, gap);

    /// <summary>Four border panels around this scope.</summary>
    public MenuScope AddBorders(Color color, float thickness = 1f)
    {
        _menu.CreateBorders(Container, color, thickness);
        return this;
    }

    /// <summary>The standard close button, sized to this bar (see Menu.CreateCloseButton).</summary>
    public MenuScope AddCloseButton(string closeTarget, float barHeight, float inset = 10f, float padding = 10f, string command = null)
    {
        _menu.CreateCloseButton(Container, closeTarget, barHeight, inset, padding, command);
        return this;
    }
}
    