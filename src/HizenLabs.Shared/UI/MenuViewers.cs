using System.Collections.Generic;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Server-side record of who has a menu open and which page they are viewing. The client's UI
/// state cannot be queried, so this is the authority: record on every send that Menu.Send
/// reports as delivered, clear when the close command fires and on disconnect. For the record
/// to stay accurate the menu's close button must notify the server - pass a closeCommand to
/// the layout (see AppLayout.Create) and clear the player in its handler. The record can still
/// go stale (a close command that never lands), so a user-initiated open should not trust it:
/// the generated CommandShow* handlers force a full shell re-send, which is safe because shell
/// roots replace themselves client-side.
///
/// The page type is the plugin's own enum, so lookups are typed and allocation-free. One
/// instance per menu, held for the plugin's lifetime:
/// <code>
/// private readonly MenuViewers&lt;MenuPage&gt; _viewers = new();
///
/// // send path                       // close command / OnPlayerDisconnected
/// _viewers.SetPage(player, page);    _viewers.Remove(player);
///
/// // live push to everyone viewing a page (element updates or a page rebuild)
/// using var menu = Menu.Create(this, MenuId.Page);
/// menu.UpdateText(MenuId.TaskStatus, "RUNNING", ...);
/// _viewers.Send(menu, MenuPage.TaskList);
/// </code>
/// </summary>
public class MenuViewers<TPage> where TPage : struct, System.Enum
{
    private readonly Dictionary<ulong, TPage> _pages = new();

    public int Count => _pages.Count;

    /// <summary>Whether the player has the menu open (per server-side tracking).</summary>
    public bool IsOpen(BasePlayer player) => _pages.ContainsKey(player.userID);

    public bool TryGetPage(BasePlayer player, out TPage page) =>
        _pages.TryGetValue(player.userID, out page);

    /// <summary>Records that the player is viewing the given page. Call on every send.</summary>
    public void SetPage(BasePlayer player, TPage page) => _pages[player.userID] = page;

    /// <summary>Clears the player's record. Returns true if they were tracked as open.</summary>
    public bool Remove(BasePlayer player) => _pages.Remove(player.userID);

    /// <summary>Whether anyone is viewing the given page.</summary>
    public bool AnyOn(TPage page)
    {
        foreach (var entry in _pages)
        {
            if (EqualityComparer<TPage>.Default.Equals(entry.Value, page))
                return true;
        }

        return false;
    }

    /// <summary>Sends the menu to every player viewing the given page.</summary>
    public void Send(Menu menu, TPage page)
    {
        foreach (var player in BasePlayer.activePlayerList)
        {
            if (_pages.TryGetValue(player.userID, out var current)
                && EqualityComparer<TPage>.Default.Equals(current, page))
            {
                menu.Send(player);
            }
        }
    }

    /// <summary>Sends the menu to every tracked viewer regardless of page.</summary>
    public void Send(Menu menu)
    {
        foreach (var player in BasePlayer.activePlayerList)
        {
            if (_pages.ContainsKey(player.userID))
                menu.Send(player);
        }
    }
}
