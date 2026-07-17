using System;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Marks a menu's layout builder for the menu wire generator (<c>hizenbundle wire</c>). The
/// method receives the shell menu and creates the layout, applying anything constant across
/// pages (title, summary):
/// <code>
/// [MenuLayout("ts.main", Command = nameof(TaskSchedulerConfig.CommandsSection.Menu))]
/// private AppLayout BuildMain(Menu menu)
/// {
///     return AppLayout.Create(menu).SetTitle("TASK SCHEDULER");
/// }
/// </code>
/// The generator emits the per-menu plumbing into <c>&lt;Plugin&gt;.Menu.g.cs</c>: the page
/// enum, Show/Close methods, viewer tracking, the close-button command handler, and the
/// disconnect/unload cleanup. Pages are methods marked with <see cref="MenuPageAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MenuLayoutAttribute : Attribute
{
    public MenuLayoutAttribute(string id)
    {
        Id = id;
    }

    /// <summary>The client-side element namespace: root name, destroy anchor, and prefix for
    /// generated child names. Must be unique across plugins (e.g. "ts.main").</summary>
    public string Id { get; }

    /// <summary>The menu's name in generated members (ShowMain, MainPage, MainViewers).
    /// Defaults to the method name with its "Build" prefix stripped.</summary>
    public string Name { get; set; }

    /// <summary>Name of the CommandSetting property in the plugin's config that opens this
    /// menu. Registration is generated the same way as [ConfigCommand].</summary>
    public string Command { get; set; }

    /// <summary>Per-player view-state class for this menu (active filter, list page, selected
    /// item - anything sends must share while the menu is open). The generated viewers field
    /// becomes MenuViewers&lt;Page, State&gt;; read/write via Viewers.State(player). The class
    /// must implement Pool.IPooled and reset its fields in EnterPool.</summary>
    public System.Type State { get; set; }
}
