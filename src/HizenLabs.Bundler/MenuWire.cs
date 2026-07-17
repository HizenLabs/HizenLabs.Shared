using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace HizenLabs.Bundler;

/// <summary>
/// Generates the per-plugin menu wiring file (<c>&lt;Plugin&gt;.Menu.g.cs</c>) from the plugin's
/// menu builder methods - the plumbing around a menu is always the same shape, so authors only
/// write the builders:
///   - a method tagged <c>[MenuLayout("id")]</c> creates the shell layout and names the menu
///     (method name minus "Build", or Name = ...); its return type is the layout the pages
///     bind through;
///   - methods tagged <c>[MenuPage]</c> build one page each and become members of the menu's
///     generated page enum. The owning menu resolves in order: Parent = ..., a method name
///     prefix matching a menu name (BuildMain_TaskList), the [MenuLayout] in the same file,
///     the plugin's sole [MenuLayout].
/// Per menu it emits the page enum (default page first, so <c>default</c> is the opening
/// page), id constants, a MenuViewers field, Show/Close methods, the close-button command
/// handler, and a navigation command handler ("&lt;menuId&gt;.nav") that page-enum buttons
/// (AppLayout.AddHeaderButton(label, page)) target. Once per plugin it emits Menu_OnPlayerDisconnected/Menu_Unload cleanup and the
/// game hooks that call them - when the author already defines a hook, the call is inserted
/// into their method instead. A <c>Command = nameof(...)</c> on [MenuLayout] produces a
/// CommandShow&lt;Name&gt; handler; KitWire registers it like a [ConfigCommand].
/// Purely syntactic, like KitWire: no semantic model, no game references.
/// </summary>
public static class MenuWire
{
    private const string LayoutAttribute = "MenuLayout";
    private const string PageAttribute = "MenuPage";
    private const string BuilderPrefix = "Build";

    private sealed class MenuModel
    {
        public string Id;
        public string Name;
        public string Builder;
        public string LayoutType;
        public string Command;
        public string FilePath;
        public List<PageModel> Pages = new();
    }

    private sealed class PageModel
    {
        public string Name;
        public string Builder;
        public bool Default;
        public string FilePath;
        public int Position;
    }

    private sealed class HookSite
    {
        public string FilePath;
        public MethodDeclarationSyntax Method;
    }

    /// <summary>
    /// Wires one plugin folder on disk: writes/updates <c>&lt;Plugin&gt;.Menu.g.cs</c> (or
    /// deletes it when the plugin declares no menus) and inserts missing cleanup calls into
    /// author-written hooks. Returns a status string for logging, or null when the folder is
    /// not a plugin folder. Only touches files whose content actually changed.
    /// </summary>
    public static string WireFolder(string folder, bool checkOnly = false)
    {
        var pluginName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        var entry = Path.Combine(folder, pluginName + ".cs");
        if (!File.Exists(entry))
            return null;

        var generatedPath = Path.Combine(folder, pluginName + ".Menu.g.cs");
        var sources = Directory.EnumerateFiles(folder, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .ToList();

        var content = Generate(pluginName, sources, out var hookEdits);
        var existing = File.Exists(generatedPath) ? File.ReadAllText(generatedPath) : null;
        var normalizedExisting = existing?.Replace("\r\n", "\n");

        if (content is null)
        {
            if (existing is null)
                return $"{pluginName}: no menus, no menu wiring";
            if (checkOnly)
                throw new MenuWireException($"{pluginName}: {Path.GetFileName(generatedPath)} is stale (should not exist)");
            File.Delete(generatedPath);
            return $"{pluginName}: removed stale {Path.GetFileName(generatedPath)}";
        }

        if (normalizedExisting == content && hookEdits.Count == 0)
            return $"{pluginName}: menu wiring up to date";
        if (checkOnly)
            throw new MenuWireException($"{pluginName}: menu wiring is stale - run a build (or 'hizenbundle wire') and commit it");

        foreach (var (path, newText) in hookEdits)
            File.WriteAllText(path, newText);
        if (normalizedExisting != content)
            File.WriteAllText(generatedPath, content);

        var edited = hookEdits.Count == 0
            ? ""
            : $" (inserted cleanup calls into {string.Join(", ", hookEdits.Select(e => Path.GetFileName(e.Path)))})";
        return $"{pluginName}: wrote {Path.GetFileName(generatedPath)}{edited}";
    }

    /// <summary>
    /// Computes the wiring file content for one plugin, or null when no method carries
    /// [MenuLayout]. hookEdits receives author files that need a cleanup call inserted into an
    /// existing hook, as full replacement texts. Throws <see cref="MenuWireException"/> on
    /// convention violations.
    /// </summary>
    public static string Generate(
        string pluginName,
        IReadOnlyList<(string Path, string Text)> sources,
        out List<(string Path, string NewText)> hookEdits)
    {
        var menus = new List<MenuModel>();
        var pageMethods = new List<(MethodDeclarationSyntax Method, string FilePath)>();
        var hooks = new Dictionary<string, HookSite>(StringComparer.Ordinal);
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        string ns = null;

        foreach (var (path, text) in sources)
        {
            texts[path] = text;
            var root = CSharpSyntaxTree.ParseText(text, path: path).GetRoot();
            foreach (var decl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (decl.Identifier.Text != pluginName)
                    continue;
                ns ??= decl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

                foreach (var method in decl.Members.OfType<MethodDeclarationSyntax>())
                {
                    var name = method.Identifier.Text;
                    if (name is "OnPlayerDisconnected" or "Unload" && !hooks.ContainsKey(name))
                        hooks[name] = new HookSite { FilePath = path, Method = method };

                    if (FindAttribute(method, LayoutAttribute) is { } layoutAttr)
                        menus.Add(ReadMenu(pluginName, method, layoutAttr, path));
                    else if (FindAttribute(method, PageAttribute) is not null)
                        pageMethods.Add((method, path));
                }
            }
        }

        hookEdits = new List<(string, string)>();
        if (menus.Count == 0)
        {
            if (pageMethods.Count > 0)
                throw new MenuWireException($"{pluginName}: [MenuPage] methods found but no [MenuLayout]");
            return null;
        }

        foreach (var duplicate in menus.GroupBy(m => m.Name).Where(g => g.Count() > 1))
            throw new MenuWireException($"{pluginName}: two menus named {duplicate.Key} - set Name on one");
        foreach (var duplicate in menus.GroupBy(m => m.Id).Where(g => g.Count() > 1))
            throw new MenuWireException($"{pluginName}: two menus with id {duplicate.Key}");

        foreach (var (method, path) in pageMethods)
            BindPage(pluginName, menus, method, path);

        foreach (var menu in menus)
            FinishMenu(pluginName, menu);

        // Author-written hooks get the cleanup call inserted; hooks the author has not written
        // are emitted into the generated file instead.
        var emitHooks = new Dictionary<string, bool>
        {
            ["OnPlayerDisconnected"] = !hooks.ContainsKey("OnPlayerDisconnected"),
            ["Unload"] = !hooks.ContainsKey("Unload"),
        };
        var pendingInserts = new List<(HookSite Site, string Call)>();
        if (hooks.TryGetValue("OnPlayerDisconnected", out var disconnectHook))
        {
            var player = disconnectHook.Method.ParameterList.Parameters.FirstOrDefault()?.Identifier.Text
                ?? throw new MenuWireException($"{pluginName}: OnPlayerDisconnected has no parameters - add the Menu_OnPlayerDisconnected(player) call yourself");
            QueueInsert(pluginName, pendingInserts, disconnectHook, $"Menu_OnPlayerDisconnected({player});");
        }
        if (hooks.TryGetValue("Unload", out var unloadHook))
            QueueInsert(pluginName, pendingInserts, unloadHook, "Menu_Unload();");

        // Rightmost insertion first, so earlier spans in the same file stay valid.
        foreach (var group in pendingInserts.GroupBy(i => i.Site.FilePath))
        {
            var text = texts[group.Key];
            foreach (var (site, call) in group.OrderByDescending(i => i.Site.Method.SpanStart))
                text = InsertIntoBody(text, site.Method, call);
            hookEdits.Add((group.Key, text));
        }

        return Emit(pluginName, ns, menus, emitHooks);
    }

    /// <summary>The config-command binding from a [MenuLayout(Command = ...)] attribute: the
    /// CommandSetting name and the generated CommandShow handler, or null when the method has
    /// no such attribute or no Command. KitWire registers these alongside [ConfigCommand]s.</summary>
    public static (string Setting, string Handler)? CommandBinding(MethodDeclarationSyntax method)
    {
        if (FindAttribute(method, LayoutAttribute) is not { } attr)
            return null;
        var command = NamedArg(attr, "Command");
        if (command is null)
            return null;
        var name = NamedArg(attr, "Name") ?? StripPrefix(method.Identifier.Text);
        if (string.IsNullOrEmpty(name))
            return null;
        return (command, "CommandShow" + name);
    }

    // ---- model discovery ----

    private static MenuModel ReadMenu(string pluginName, MethodDeclarationSyntax method, AttributeSyntax attr, string path)
    {
        var id = attr.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals is null)?.Expression
            is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : throw new MenuWireException($"{pluginName}: [MenuLayout] on {method.Identifier.Text} needs a string literal id");

        var name = NamedArg(attr, "Name") ?? StripPrefix(method.Identifier.Text);
        if (!IsIdentifier(name))
            throw new MenuWireException($"{pluginName}: cannot derive a menu name from {method.Identifier.Text} - set Name on its [MenuLayout]");

        var layoutType = method.ReturnType is PredefinedTypeSyntax { Keyword.Text: "void" }
            ? throw new MenuWireException($"{pluginName}: {method.Identifier.Text} must return its layout (e.g. AppLayout) so pages can bind through it")
            : method.ReturnType.ToString();

        return new MenuModel
        {
            Id = id,
            Name = name,
            Builder = method.Identifier.Text,
            LayoutType = layoutType,
            Command = NamedArg(attr, "Command"),
            FilePath = path,
        };
    }

    private static void BindPage(string pluginName, List<MenuModel> menus, MethodDeclarationSyntax method, string path)
    {
        var attr = FindAttribute(method, PageAttribute);
        var methodName = method.Identifier.Text;
        var stripped = StripPrefix(methodName);

        MenuModel parent = null;
        string pageName = NamedArg(attr, "Page");

        if (NamedArg(attr, "Parent") is { } parentName)
        {
            parent = menus.FirstOrDefault(m => m.Name == parentName)
                ?? throw new MenuWireException($"{pluginName}: [MenuPage(Parent = \"{parentName}\")] on {methodName} matches no [MenuLayout]");
        }
        else if (stripped is not null && menus.FirstOrDefault(m => stripped.StartsWith(m.Name + "_", StringComparison.Ordinal)) is { } byPrefix)
        {
            parent = byPrefix;
            pageName ??= stripped[(byPrefix.Name.Length + 1)..];
        }
        else
        {
            var sameFile = menus.Where(m => m.FilePath == path).ToList();
            parent = sameFile.Count switch
            {
                1 => sameFile[0],
                > 1 => throw new MenuWireException($"{pluginName}: {methodName} is ambiguous between menus in its file ({string.Join(", ", sameFile.Select(m => m.Name))}) - prefix the method or set Parent"),
                _ when menus.Count == 1 => menus[0],
                _ => throw new MenuWireException($"{pluginName}: cannot resolve the menu {methodName} belongs to - prefix the method (Build{menus[0].Name}_...) or set Parent"),
            };
        }

        pageName ??= stripped;
        if (!IsIdentifier(pageName))
            throw new MenuWireException($"{pluginName}: cannot derive a page name from {methodName} - set Page on its [MenuPage]");
        if (method.ParameterList.Parameters.Count != 2)
            throw new MenuWireException($"{pluginName}: {methodName} must take (BasePlayer player, {parent.LayoutType}.Page page)");

        parent.Pages.Add(new PageModel
        {
            Name = pageName,
            Builder = methodName,
            Default = BoolArg(attr, "Default"),
            FilePath = path,
            Position = method.SpanStart,
        });
    }

    private static void FinishMenu(string pluginName, MenuModel menu)
    {
        if (menu.Pages.Count == 0)
            throw new MenuWireException($"{pluginName}: menu {menu.Name} has no [MenuPage] methods");
        foreach (var duplicate in menu.Pages.GroupBy(p => p.Name).Where(g => g.Count() > 1))
            throw new MenuWireException($"{pluginName}: menu {menu.Name} has two pages named {duplicate.Key}");

        var defaults = menu.Pages.Count(p => p.Default);
        if (menu.Pages.Count == 1)
            menu.Pages[0].Default = true;
        else if (defaults != 1)
            throw new MenuWireException($"{pluginName}: menu {menu.Name} needs Default = true on exactly one page - found {defaults}");

        menu.Pages = menu.Pages
            .OrderByDescending(p => p.Default)
            .ThenBy(p => p.FilePath, StringComparer.Ordinal)
            .ThenBy(p => p.Position)
            .ToList();
    }

    // ---- emission ----

    private static string Emit(string pluginName, string ns, List<MenuModel> menus, Dictionary<string, bool> emitHooks)
    {
        var sb = new StringBuilder();
        sb.Append("// <auto-generated>\n");
        sb.Append("//     Generated by 'hizenbundle wire'. DO NOT EDIT - rewritten on every build.\n");
        sb.Append("//     Menu wiring bootstrapped from the plugin's [MenuLayout]/[MenuPage] methods.\n");
        sb.Append("// </auto-generated>\n");
        sb.Append("using HizenLabs.Shared.UI;\n");
        sb.Append("using HizenLabs.Shared.UI.Layouts;\n");
        sb.Append("using UnityEngine;\n\n");
        if (ns is not null)
            sb.Append($"namespace {ns};\n\n");
        sb.Append($"public partial class {pluginName}\n{{\n");

        foreach (var menu in menus)
        {
            var n = menu.Name;
            sb.Append($"    /// <summary>Pages of the {n} menu ({menu.Id}).</summary>\n");
            sb.Append($"    private enum {n}Page\n    {{\n");
            foreach (var page in menu.Pages)
                sb.Append($"        {page.Name},\n");
            sb.Append("    }\n\n");

            sb.Append($"    /// <summary>Element namespace of the {n} menu.</summary>\n");
            sb.Append($"    private const string {n}MenuId = \"{menu.Id}\";\n\n");
            sb.Append($"    /// <summary>Element namespace of {n} page sends.</summary>\n");
            sb.Append($"    private const string {n}PageId = \"{menu.Id}.page\";\n\n");

            sb.Append($"    /// <summary>Who has the {n} menu open and which page they see.</summary>\n");
            sb.Append($"    private readonly MenuViewers<{n}Page> {n}Viewers = new();\n\n");

            sb.Append($"    /// <summary>Shows the {n} menu on the given page. resend forces the shell out even\n");
            sb.Append("    /// when the menu is tracked as open (the shell's root replaces itself client-side, so\n");
            sb.Append("    /// this heals a stale record); page navigation inside the open menu skips the shell.\n");
            sb.Append("    /// The viewer is tracked only when the sends actually reach the client.</summary>\n");
            sb.Append($"    private void Show{n}(BasePlayer player, {n}Page page = default, bool resend = false)\n    {{\n");
            sb.Append("        if (player == null || player.net?.connection == null)\n");
            sb.Append("            return;\n\n");
            sb.Append($"        if (resend || !{n}Viewers.IsOpen(player))\n        {{\n");
            sb.Append($"            using var menu = Menu.Create(this, {n}MenuId);\n");
            sb.Append($"            menu.CloseCommand = \"{menu.Id}.close\";\n");
            sb.Append($"            {menu.Builder}(menu);\n");
            sb.Append("            if (!menu.Send(player))\n");
            sb.Append("                return;\n        }\n\n");
            sb.Append($"        using var pageMenu = Menu.Create(this, {n}PageId);\n");
            sb.Append($"        var scope = {menu.LayoutType}.CreatePage(pageMenu, {n}MenuId);\n");
            sb.Append("        switch (page)\n        {\n");
            foreach (var page in menu.Pages)
                sb.Append($"            case {n}Page.{page.Name}: {page.Builder}(player, scope); break;\n");
            sb.Append("        }\n\n");
            sb.Append("        if (pageMenu.Send(player))\n");
            sb.Append($"            {n}Viewers.SetPage(player, page);\n    }}\n\n");

            sb.Append($"    private void Close{n}(BasePlayer player)\n    {{\n");
            sb.Append($"        Menu.Close(player, {n}MenuId);\n");
            sb.Append($"        {n}Viewers.Remove(player);\n    }}\n\n");

            sb.Append($"    [MenuCommand(\"{menu.Id}.close\")]\n");
            sb.Append($"    private void On{n}Closed(ConsoleSystem.Arg arg)\n    {{\n");
            sb.Append("        var player = arg.Player();\n");
            sb.Append("        if (player != null)\n");
            sb.Append($"            {n}Viewers.Remove(player);\n    }}\n\n");

            sb.Append($"    // Navigation glue: buttons created with AddHeaderButton(label, {n}Page.X) run this.\n");
            sb.Append($"    [MenuCommand(\"{menu.Id}.nav\")]\n");
            sb.Append($"    private void On{n}Navigate(ConsoleSystem.Arg arg)\n    {{\n");
            sb.Append("        var player = arg.Player();\n");
            sb.Append("        var page = arg.GetInt(0, -1);\n");
            sb.Append($"        if (player != null && page >= 0 && page < {menu.Pages.Count})\n");
            sb.Append($"            Show{n}(player, ({n}Page)page);\n    }}\n\n");

            if (menu.Command is not null)
            {
                sb.Append("    // resend: a user-initiated open must not trust the open-menu tracking - if the\n");
                sb.Append("    // client lost the menu while the record says open, a tracked-open send would skip\n");
                sb.Append("    // the shell and the page would parent into nothing. The full send is safe because\n");
                sb.Append("    // the shell root replaces itself client-side.\n");
                sb.Append($"    private void CommandShow{n}(BasePlayer player, string command, string[] args)\n    {{\n");
                sb.Append($"        Show{n}(player, resend: true);\n    }}\n\n");
            }
        }

        sb.Append("    private void Menu_OnPlayerDisconnected(BasePlayer player)\n    {\n");
        foreach (var menu in menus)
            sb.Append($"        {menu.Name}Viewers.Remove(player);\n");
        sb.Append("    }\n\n");

        sb.Append("    // Closes for every player, tracked or not: the tracking can be stale, and destroying\n");
        sb.Append("    // an id the client does not have is a no-op.\n");
        sb.Append("    private void Menu_Unload()\n    {\n");
        sb.Append("        foreach (var player in BasePlayer.activePlayerList)\n        {\n");
        foreach (var menu in menus)
        {
            sb.Append($"            {menu.Name}Viewers.Remove(player);\n");
            sb.Append($"            Menu.Close(player, {menu.Name}MenuId);\n");
        }
        sb.Append("        }\n    }\n");

        if (emitHooks["OnPlayerDisconnected"])
        {
            sb.Append("\n    private void OnPlayerDisconnected(BasePlayer player, string reason)\n    {\n");
            sb.Append("        Menu_OnPlayerDisconnected(player);\n    }\n");
        }
        if (emitHooks["Unload"])
        {
            sb.Append("\n    private void Unload()\n    {\n");
            sb.Append("        Menu_Unload();\n    }\n");
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    // ---- hook call insertion ----

    private static void QueueInsert(string pluginName, List<(HookSite, string)> pending, HookSite site, string call)
    {
        var method = site.Method;
        if (method.Body is null)
            throw new MenuWireException($"{pluginName}: {method.Identifier.Text} is expression-bodied - convert it to a block so the {call} call can be inserted");
        var target = call[..call.IndexOf('(')];
        if (method.Body.DescendantNodes().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.Text == target))
            return;
        pending.Add((site, call));
    }

    /// <summary>Inserts a statement as the first line of the method's body, indented one level
    /// past the opening brace's line.</summary>
    private static string InsertIntoBody(string text, MethodDeclarationSyntax method, string call)
    {
        var brace = method.Body.OpenBraceToken;
        var lineStart = text.LastIndexOf('\n', brace.SpanStart) + 1;
        var indent = text[lineStart..brace.SpanStart];
        if (indent.Trim().Length > 0)
            indent = new string(' ', 8);
        return text.Insert(brace.Span.End, $"\n{indent}    {call}");
    }

    // ---- syntax helpers ----

    private static AttributeSyntax FindAttribute(MethodDeclarationSyntax method, string name)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            var attrName = attribute.Name switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                QualifiedNameSyntax q => q.Right.Identifier.Text,
                _ => null,
            };
            if (attrName == name || attrName == name + "Attribute")
                return attribute;
        }
        return null;
    }

    /// <summary>A named argument's string value (literal or nameof(...)), or null when absent.</summary>
    private static string NamedArg(AttributeSyntax attr, string name)
    {
        var arg = attr.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == name);
        return arg?.Expression switch
        {
            null => null,
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameOf =>
                LastIdentifier(nameOf.ArgumentList.Arguments.First().Expression),
            _ => throw new MenuWireException($"[{LayoutAttribute}/{PageAttribute}] {name}: expected nameof(...) or a string literal"),
        };
    }

    private static bool BoolArg(AttributeSyntax attr, string name) =>
        attr.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == name)
            ?.Expression.IsKind(SyntaxKind.TrueLiteralExpression) == true;

    /// <summary>The method name with the "Build" prefix removed, or null when it has no such
    /// prefix or nothing follows it.</summary>
    private static string StripPrefix(string methodName) =>
        methodName.StartsWith(BuilderPrefix, StringComparison.Ordinal) && methodName.Length > BuilderPrefix.Length
            ? methodName[BuilderPrefix.Length..]
            : null;

    private static bool IsIdentifier(string name) =>
        !string.IsNullOrEmpty(name)
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    private static string LastIdentifier(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => expr.ToString(),
    };
}

public sealed class MenuWireException : Exception
{
    public MenuWireException(string message) : base(message) { }
}
