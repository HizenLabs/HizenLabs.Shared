using Facepunch;
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
    public virtual bool Remove(BasePlayer player) => _pages.Remove(player.userID);

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

/// <summary>
/// MenuViewers with a per-player state bag: view state that must survive between sends while
/// the menu is open (active filter, list page, selected item) but is NOT persistent data.
/// Declare the state class on the layout - <c>[MenuLayout(..., State = typeof(MyState))]</c> -
/// and the generated viewers field carries it; page builders and command handlers read and
/// write it through <see cref="State"/>:
/// <code>
/// // [MenuCommand] handler: mutate the state, then re-show the page - the rebuild reads it.
/// MainViewers.State(player).Filter = arg.GetInt(0);
/// ShowMain(player, MainPage.Logs);
/// </code>
/// The bag is pooled: it is created on first access and freed when the player's record clears
/// (close command, disconnect, unload), so the state class MUST reset its fields in
/// <c>EnterPool</c> - a stale field would leak one player's view state to the next.
/// </summary>
public class MenuViewers<TPage, TState> : MenuViewers<TPage>
    where TPage : struct, System.Enum
    where TState : class, Pool.IPooled, new()
{
    private readonly Dictionary<ulong, TState> _states = new();

    /// <summary>The player's state bag, created on first access.</summary>
    public TState State(BasePlayer player)
    {
        if (!_states.TryGetValue(player.userID, out var state))
            _states[player.userID] = state = Pool.Get<TState>();
        return state;
    }

    public override bool Remove(BasePlayer player)
    {
        if (_states.TryGetValue(player.userID, out var state))
        {
            _states.Remove(player.userID);
            Pool.Free(ref state);
        }

        return base.Remove(player);
    }
}
