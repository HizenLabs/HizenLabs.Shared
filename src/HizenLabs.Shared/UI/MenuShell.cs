using System;
using UnityEngine;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Compiles a layout's shell: the static chrome of a menu, written with the SAME scope/builder
/// API dynamic content uses, captured as an immortal ready-to-send payload instead of sent.
/// Layout types call this once per cache key and hand the bytes to <see cref="Menu.AttachShell"/>
/// on every open. The builder delegate and its allocations are one-time by design.
/// <code>
/// shell.Payload = MenuShell.Build(menuId, menu =>
/// {
///     var root = menu.CreateParent(layer, MenuPosition.Full, menuId);
///     var window = root.AddPanel(MenuPosition.Center, new(-200f, -90f, 200f, 90f), MenuTheme.Background);
///     window.AddBorders(MenuTheme.Border);
/// });
/// </code>
/// </summary>
public static class MenuShell
{
    public static byte[] Build(string menuId, Action<Menu> build)
    {
        using var menu = Menu.Create(null, menuId);
        build(menu);
        return menu.ExportShell();
    }
}
