using System;

namespace HizenLabs.Shared.UI;

/// <summary>
/// Marks a page builder for the menu wire generator. The method receives the player and the
/// layout's page regions, and becomes a member of the menu's generated page enum:
/// <code>
/// [MenuPage(Default = true)]
/// private void BuildTaskList(BasePlayer player, AppLayout.Page page)
/// {
///     page.Content.AddText(...);
/// }
/// </code>
/// The owning menu resolves in order: <see cref="Parent"/>, a method name prefix matching a
/// menu name (BuildMain_TaskList), the [MenuLayout] declared in the same file, the plugin's
/// sole [MenuLayout]. The page name defaults to the method name with "Build" (and any menu
/// prefix) stripped.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MenuPageAttribute : Attribute
{
    /// <summary>Name of the owning menu, when the method name and file don't imply it.</summary>
    public string Parent { get; set; }

    /// <summary>The page's enum member name, when it differs from the derived one.</summary>
    public string Page { get; set; }

    /// <summary>Marks the page the menu opens on. Required on exactly one page of a
    /// multi-page menu; a single-page menu defaults implicitly.</summary>
    public bool Default { get; set; }
}
