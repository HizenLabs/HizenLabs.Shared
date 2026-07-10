using HizenLabs.Shared.UI;
using System.Collections.Generic;

namespace HizenLabs.Shared.UI.Layouts;

/// <summary>
/// A centered confirmation dialog: message area plus confirm/cancel button slots. The shell (the
/// frame and slots) is precompiled per menu id; labels and commands are per-open:
/// <code>
/// using var menu = Menu.Create(this, MenuId.Confirm);
/// var dialog = ConfirmDialog.Create(menu);
/// dialog.Message.AddTitle("Delete snapshot 42?");
/// dialog.Confirm.Set("Yes", "ts.confirm 42");
/// dialog.Cancel.Set("No", "ts.cancel");
/// menu.Send(player);
/// </code>
/// Pair the button commands with [MenuCommand] handlers - Menu.CreateButton routes them through
/// Carbon's protected-command table automatically.
/// </summary>
public readonly struct ConfirmDialog
{
    public readonly MenuScope Message;
    public readonly MenuButton Confirm;
    public readonly MenuButton Cancel;

    private ConfirmDialog(MenuScope message, MenuButton confirm, MenuButton cancel)
    {
        Message = message;
        Confirm = confirm;
        Cancel = cancel;
    }

    public static ConfirmDialog Create(Menu menu, Menu.Layer layer = Menu.Layer.Overlay)
    {
        var shell = GetShell(layer, menu.Id);
        menu.AttachShell(shell.Payload);
        return new ConfirmDialog(
            menu.Scope(shell.Message),
            menu.Button(shell.Confirm),
            menu.Button(shell.Cancel));
    }

    // ---- shell plumbing (one compile per key, immortal cache) ----

    private sealed class Shell
    {
        public byte[] Payload;
        public string Message;
        public string Confirm;
        public string Cancel;
    }

    private static readonly Dictionary<(Menu.Layer, string), Shell> _shells = new();

    private static Shell GetShell(Menu.Layer layer, string menuId)
    {
        var key = (layer, menuId);
        if (_shells.TryGetValue(key, out var shell))
            return shell;

        shell = new Shell
        {
            Message = menuId + ".msg",
            Confirm = menuId + ".yes",
            Cancel = menuId + ".no",
        };

        var background = menuId + ".bg";
        var sb = MenuShell.Begin();
        var count = 0;

        MenuJson.BeginElement(sb, ref count, menuId, Menu.LayerName(layer), update: false);
        MenuJson.Rect(sb, MenuPosition.Center, new MenuOffset(-210f, -90f, 210f, 90f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, background, menuId, update: false);
        MenuJson.Rect(sb, MenuPosition.Full, MenuOffset.Zero);
        MenuJson.Image(sb, MenuTheme.Background);
        MenuJson.Cursor(sb);
        MenuJson.EndElement(sb);

        // Message fills everything above the button row.
        MenuJson.BeginElement(sb, ref count, shell.Message, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0.35f, 1f, 1f), new MenuOffset(12f, 4f, -12f, -8f));
        MenuJson.EndElement(sb);

        // Button slots: pure containers so Set() controls the visuals per open.
        MenuJson.BeginElement(sb, ref count, shell.Confirm, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0f, 0f, 0.5f, 0.35f), new MenuOffset(12f, 12f, -6f, -8f));
        MenuJson.EndElement(sb);

        MenuJson.BeginElement(sb, ref count, shell.Cancel, background, update: false);
        MenuJson.Rect(sb, new MenuPosition(0.5f, 0f, 1f, 0.35f), new MenuOffset(6f, 12f, -12f, -8f));
        MenuJson.EndElement(sb);

        shell.Payload = MenuShell.Finish(ref sb);
        _shells[key] = shell;
        return shell;
    }
}
