using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace HizenLabs.Bundler;

/// <summary>
/// Generates the per-plugin kit wiring file (<c>&lt;Plugin&gt;.Kit.g.cs</c>) from the presence of
/// the plugin's kit classes - the bridge is always the same, so authors never write it:
///   - a class deriving <c>BaseConfig</c> produces the <c>config</c> field and the
///     LoadConfig/LoadDefaultConfig/SaveConfig overrides;
///   - a class deriving <c>BaseLang</c> produces the <c>LangKeys</c> enum (one member per string
///     field) and the <c>msg</c> accessor (msg.Get / msg.Chat), enum-keyed so call sites never
///     pass raw strings;
///   - a method tagged <c>[ConfigCommand(nameof(...))]</c> produces permission + covalence
///     command registration from the bound <c>CommandSetting</c> in the config, emitted into the
///     generated LoadConfig so the server owner's values are what registers.
/// One config and one lang per plugin, enforced. An override the author wrote by hand anywhere in
/// the plugin's parts is skipped (the author wins). The wiring is emitted INSIDE the plugin class
/// because the platform members it touches (lang, permission, AddCovalenceCommand) are protected.
/// The output is a real file on disk so the IDE treats it as ordinary source - no
/// source-generator machinery in the IntelliSense loop. Purely syntactic: no semantic model, no
/// game references.
/// </summary>
public static class KitWire
{
    private const string ConfigBase = "BaseConfig";
    private const string LangBase = "BaseLang";
    private const string CommandSettingType = "CommandSetting";
    private const string CommandAttribute = "ConfigCommand";
    // The generated members' names, centralized so renaming is a one-line change.
    private const string ConfigField = "config";
    private const string MsgField = "msg";
    private const string MsgType = "KitMessages";
    private const string KeysEnum = "LangKeys";

    private sealed record BoundCommand(string SettingName, string Handler);

    /// <summary>
    /// Computes the wiring file content for one plugin folder, or null when the plugin has no
    /// kit classes (no file should exist). Throws <see cref="KitWireException"/> on convention
    /// violations (two config/lang classes, unresolvable [ConfigCommand] bindings, a hand-written
    /// LoadConfig alongside [ConfigCommand] methods).
    /// </summary>
    public static string Generate(string pluginName, IEnumerable<(string Path, string Text)> sources)
    {
        var configTypes = new List<string>();
        var langTypes = new List<string>();
        var langFields = new List<(string Name, string Text)>();
        var authoredOverrides = new HashSet<string>(StringComparer.Ordinal);
        var commands = new List<BoundCommand>();
        // Every type declared in the folder (top-level and nested) with its properties, for
        // resolving a CommandSetting property name to its path from the config root.
        var typeProperties = new Dictionary<string, List<(string Name, string Type)>>(StringComparer.Ordinal);
        string ns = null;

        foreach (var (path, text) in sources)
        {
            var root = CSharpSyntaxTree.ParseText(text, path: path).GetRoot();
            foreach (var decl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!typeProperties.TryGetValue(decl.Identifier.Text, out var props))
                    typeProperties[decl.Identifier.Text] = props = new List<(string, string)>();
                foreach (var prop in decl.Members.OfType<PropertyDeclarationSyntax>())
                    props.Add((prop.Identifier.Text, TypeName(prop.Type)));

                if (decl.Identifier.Text == pluginName)
                {
                    ns ??= decl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                    foreach (var method in decl.Members.OfType<MethodDeclarationSyntax>())
                    {
                        if (method.Modifiers.Any(SyntaxKind.OverrideKeyword))
                            authoredOverrides.Add(method.Identifier.Text);
                        var setting = CommandSettingName(method);
                        if (setting is not null)
                            commands.Add(new BoundCommand(setting, method.Identifier.Text));
                        // [MenuLayout(Command = ...)] binds like a [ConfigCommand], with the
                        // handler MenuWire generates.
                        if (MenuWire.CommandBinding(method) is { } menuCommand)
                            commands.Add(new BoundCommand(menuCommand.Setting, menuCommand.Handler));
                    }
                    continue;
                }
                if (decl.Parent is TypeDeclarationSyntax)
                    continue; // nested helper types are not plugin-level kit classes
                if (HasBase(decl, ConfigBase))
                {
                    configTypes.Add(decl.Identifier.Text);
                }
                else if (HasBase(decl, LangBase))
                {
                    langTypes.Add(decl.Identifier.Text);
                    foreach (var field in decl.Members.OfType<FieldDeclarationSyntax>())
                    {
                        if (TypeName(field.Declaration.Type) != "string")
                            continue;
                        if (!field.Modifiers.Any(SyntaxKind.PublicKeyword))
                            continue;
                        foreach (var variable in field.Declaration.Variables)
                        {
                            var defaultText = variable.Initializer?.Value is LiteralExpressionSyntax literal
                                              && literal.IsKind(SyntaxKind.StringLiteralExpression)
                                ? literal.Token.ValueText
                                : variable.Initializer?.Value.ToString() ?? "";
                            langFields.Add((variable.Identifier.Text, defaultText));
                        }
                    }
                }
            }
        }

        if (configTypes.Count > 1)
            throw new KitWireException($"{pluginName}: one config class per plugin - found {string.Join(", ", configTypes)}");
        if (langTypes.Count > 1)
            throw new KitWireException($"{pluginName}: one lang class per plugin - found {string.Join(", ", langTypes)}");

        var config = configTypes.Count == 1 ? configTypes[0] : null;
        var lang = langTypes.Count == 1 ? langTypes[0] : null;
        if (config is null && lang is null && commands.Count == 0)
            return null;

        if (commands.Count > 0 && config is null)
            throw new KitWireException($"{pluginName}: [ConfigCommand] needs a {ConfigBase} class holding the CommandSetting");
        if (commands.Count > 0 && authoredOverrides.Contains("LoadConfig"))
            throw new KitWireException($"{pluginName}: [ConfigCommand] registration is generated into LoadConfig, but LoadConfig is hand-written - remove one or the other");

        var boundPaths = commands
            .Select(c => (Command: c, Path: ResolveSettingPath(pluginName, config, c.SettingName, typeProperties)))
            .ToList();

        var sb = new StringBuilder();
        sb.Append("// <auto-generated>\n");
        sb.Append("//     Generated by 'hizenbundle wire'. DO NOT EDIT - rewritten on every build.\n");
        sb.Append("//     This is the standard kit bridge, bootstrapped from the plugin's kit classes.\n");
        sb.Append("// </auto-generated>\n");
        if (config is not null)
            sb.Append("using HizenLabs.Shared.Config;\n");
        if (lang is not null)
            sb.Append("using HizenLabs.Shared.Lang;\n");
        sb.Append('\n');
        if (ns is not null)
            sb.Append($"namespace {ns};\n\n");
        sb.Append($"public partial class {pluginName}\n{{\n");

        if (config is not null)
            sb.Append($"    private static {config} {ConfigField};\n");
        if (lang is not null)
            sb.Append($"    private static readonly {MsgType} {MsgField} = new();\n");

        if (config is not null)
        {
            if (!authoredOverrides.Contains("LoadConfig"))
            {
                sb.Append($"\n    protected override void LoadConfig()\n    {{\n        base.LoadConfig();\n        {ConfigField} = ConfigKit.Load<{config}>(this);\n");
                for (var i = 0; i < boundPaths.Count; i++)
                {
                    var (command, path) = (boundPaths[i].Command, boundPaths[i].Path);
                    sb.Append($"\n        var cmd{i} = {ConfigField}.{path};\n");
                    sb.Append($"        if (!string.IsNullOrEmpty(cmd{i}.Permission))\n            permission.RegisterPermission(cmd{i}.Permission, this);\n");
                    sb.Append($"        if (!string.IsNullOrEmpty(cmd{i}.Command))\n            AddCovalenceCommand(cmd{i}.Command, nameof({command.Handler}), cmd{i}.Permission);\n");
                }
                sb.Append("    }\n");
            }
            if (!authoredOverrides.Contains("LoadDefaultConfig"))
                sb.Append($"\n    protected override void LoadDefaultConfig()\n    {{\n        base.LoadDefaultConfig();\n        {ConfigField} = ConfigKit.Default<{config}>(this);\n    }}\n");
            if (!authoredOverrides.Contains("SaveConfig"))
                sb.Append($"\n    protected override void SaveConfig()\n    {{\n        base.SaveConfig();\n        ConfigKit.Save(this, {ConfigField});\n    }}\n");
        }

        if (lang is not null)
        {
            if (!authoredOverrides.Contains("LoadDefaultMessages"))
                sb.Append($"\n    protected override void LoadDefaultMessages()\n    {{\n        base.LoadDefaultMessages();\n        {MsgType}.Plugin = this;\n        lang.RegisterMessages(LangKit.BuildDefaults<{lang}>(), this, \"en\");\n    }}\n");

            sb.Append($"\n    private enum {KeysEnum}\n    {{\n");
            for (var i = 0; i < langFields.Count; i++)
            {
                var (name, text) = langFields[i];
                if (i > 0)
                    sb.Append('\n');
                // The default text as the member's doc, so hovering a LangKeys value in the IDE
                // shows the message it stands for.
                sb.Append($"        /// <summary>\n        /// {XmlDocEscape(text)}\n        /// </summary>\n");
                sb.Append($"        {name},\n");
            }
            sb.Append("    }\n");

            sb.Append($"\n    private sealed class {MsgType}\n    {{\n");
            sb.Append($"        internal static {pluginName} Plugin;\n\n");
            sb.Append("        private static readonly string[] _keys =\n        {\n");
            foreach (var (name, _) in langFields)
                sb.Append($"            nameof({lang}.{name}),\n");
            sb.Append("        };\n");
            sb.Append($"\n        public string Get({KeysEnum} key, BasePlayer player = null, object arg1 = null, object arg2 = null, object arg3 = null)\n");
            sb.Append("        {\n            var format = Plugin.lang.GetMessage(_keys[(int)key], Plugin, player?.UserIDString);\n            return LangKit.Format(format, arg1, arg2, arg3);\n        }\n");
            sb.Append($"\n        public void Chat(BasePlayer player, {KeysEnum} key, object arg1 = null, object arg2 = null, object arg3 = null)\n");
            sb.Append("        {\n            player.ChatMessage(Get(key, player, arg1, arg2, arg3));\n        }\n");
            sb.Append("    }\n");
        }

        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>Resolves a CommandSetting property name to its dotted path from the config root
    /// (e.g. "Menu" -> "Commands.Menu"), walking properties whose types are declared in the
    /// plugin folder. Exactly one match required.</summary>
    private static string ResolveSettingPath(
        string pluginName, string configType, string settingName,
        Dictionary<string, List<(string Name, string Type)>> typeProperties)
    {
        var matches = new List<string>();

        void Walk(string type, string prefix, HashSet<string> visited)
        {
            if (!visited.Add(type) || !typeProperties.TryGetValue(type, out var props))
                return;
            foreach (var (name, propType) in props)
            {
                var path = prefix.Length == 0 ? name : $"{prefix}.{name}";
                if (propType == CommandSettingType && name == settingName)
                    matches.Add(path);
                else if (typeProperties.ContainsKey(propType))
                    Walk(propType, path, visited);
            }
            visited.Remove(type);
        }

        Walk(configType, "", new HashSet<string>(StringComparer.Ordinal));

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new KitWireException($"{pluginName}: [ConfigCommand(\"{settingName}\")] has no matching {CommandSettingType} property in {configType}"),
            _ => throw new KitWireException($"{pluginName}: [ConfigCommand(\"{settingName}\")] is ambiguous in {configType}: {string.Join(", ", matches)}"),
        };
    }

    /// <summary>The setting name from a [ConfigCommand(...)] attribute (nameof(...) or a string
    /// literal), or null when the method carries no such attribute.</summary>
    private static string CommandSettingName(MethodDeclarationSyntax method)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(l => l.Attributes))
        {
            var name = attribute.Name switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                QualifiedNameSyntax q => q.Right.Identifier.Text,
                _ => null,
            };
            if (name is not (CommandAttribute or CommandAttribute + "Attribute"))
                continue;

            var arg = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
            return arg switch
            {
                InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } nameOf =>
                    LastIdentifier(nameOf.ArgumentList.Arguments.First().Expression),
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                    literal.Token.ValueText,
                _ => throw new KitWireException($"[ConfigCommand] on {method.Identifier.Text}: expected nameof(...) or a string literal"),
            };
        }
        return null;
    }

    // Escape for xml-doc content, one line per doc line (newlines in the text become /// lines).
    private static string XmlDocEscape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\r\n", "\n").Replace("\n", "\n        /// ");

    private static string LastIdentifier(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
        _ => expr.ToString(),
    };

    private static string TypeName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        PredefinedTypeSyntax p => p.Keyword.Text,
        NullableTypeSyntax n => TypeName(n.ElementType),
        _ => type.ToString(),
    };

    private static bool HasBase(TypeDeclarationSyntax decl, string baseName) =>
        decl.BaseList is not null && decl.BaseList.Types.Any(t =>
            (t.Type is IdentifierNameSyntax id && id.Identifier.Text == baseName) ||
            (t.Type is QualifiedNameSyntax q && q.Right.Identifier.Text == baseName));

    /// <summary>
    /// Wires one plugin folder on disk: writes/updates <c>&lt;Plugin&gt;.Kit.g.cs</c>, or deletes
    /// it when the plugin has no kit classes. Returns a status string for logging, or null when
    /// the folder is not a plugin folder (no &lt;DirName&gt;.cs entry). Only touches the file when
    /// the content actually changed, so builds stay incremental.
    /// </summary>
    public static string WireFolder(string folder, bool checkOnly = false)
    {
        var pluginName = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
        var entry = Path.Combine(folder, pluginName + ".cs");
        if (!File.Exists(entry))
            return null;

        var generatedPath = Path.Combine(folder, pluginName + ".Kit.g.cs");
        var sources = Directory.EnumerateFiles(folder, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f, File.ReadAllText(f)));

        var content = Generate(pluginName, sources);
        var existing = File.Exists(generatedPath) ? File.ReadAllText(generatedPath) : null;
        var normalizedExisting = existing?.Replace("\r\n", "\n");

        if (content is null)
        {
            if (existing is null)
                return $"{pluginName}: no kit classes, no wiring";
            if (checkOnly)
                throw new KitWireException($"{pluginName}: {Path.GetFileName(generatedPath)} is stale (should not exist)");
            File.Delete(generatedPath);
            return $"{pluginName}: removed stale {Path.GetFileName(generatedPath)}";
        }

        if (normalizedExisting == content)
            return $"{pluginName}: wiring up to date";
        if (checkOnly)
            throw new KitWireException($"{pluginName}: {Path.GetFileName(generatedPath)} is stale - run a build (or 'hizenbundle wire') and commit it");
        File.WriteAllText(generatedPath, content);
        return $"{pluginName}: wrote {Path.GetFileName(generatedPath)}";
    }
}

public sealed class KitWireException : Exception
{
    public KitWireException(string message) : base(message) { }
}
