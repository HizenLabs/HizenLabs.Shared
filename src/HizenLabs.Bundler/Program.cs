// HizenLabs.Bundler CLI. Commands:
//
//   bundle    merge a plugin + the shared code it uses into one deployable .cs (stamps [Info]).
//   wire      bootstrap <Plugin>.Kit.g.cs (config/lang bridge) from the plugin's kit classes.
//   version   resolve and print a plugin's version (from --changelog or --version).
//   notes     print the newest changelog entry's body (GitHub release notes).
//   validate  check a CHANGELOG.md against the version policy (start at 1.0.0, canonical bumps).
//
// The version is X.Y.Z from the plugin's CHANGELOG.md (or --version). --dev appends a monotonic
// revision (X.Y.Z.<rev>) so local deploys always hot-reload. bundle keeps the author's neutral
// namespace and `: PluginBase`; the transform pipeline rewrites them into the #if CARBON / #else
// split. Per-platform ref dirs compile-check the emitted file; on failure it is still written and
// the errors print as `<file>(line,col): error CSxxxx: ...` so build tools surface them as navigable.
using HizenLabs.Bundler;
using HizenLabs.Bundler.Transforms;

return Dispatch(args);

static int Dispatch(string[] argv)
{
    if (argv.Length == 0) return Usage(null);
    var rest = argv[1..];
    return argv[0] switch
    {
        "bundle" => Bundle(rest),
        "wire" => Wire(rest),
        "version" => PrintVersion(rest),
        "notes" => PrintNotes(rest),
        "validate" => ValidateChangelog(rest),
        _ => Usage($"unknown command: {argv[0]}"),
    };
}

static int Bundle(string[] args)
{
    string? plugin = null, outPath = null, version = null, changelog = null;
    var sharedDirs = new List<string>();
    string[]? carbonRefs = null, oxideRefs = null;
    var dev = false;
    var partRegions = false;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--plugin": plugin = args[++i]; break;
            case "--shared-dir": sharedDirs.AddRange(SplitDirs(args[++i])); break;
            case "--out": outPath = args[++i]; break;
            case "--version": version = args[++i]; break;
            case "--changelog": changelog = args[++i]; break;
            case "--dev": dev = true; break;
            case "--part-regions": partRegions = true; break;
            case "--carbon-refs": carbonRefs = SplitDirs(args[++i]); break;
            case "--oxide-refs": oxideRefs = SplitDirs(args[++i]); break;
            default: return Usage($"unknown argument: {args[i]}");
        }
    }
    if (plugin is null || sharedDirs.Count == 0)
        return Usage("bundle needs --plugin and --shared-dir");

    var resolved = ResolveVersion(version, changelog, dev);
    if (resolved is null) return 2;

    var pluginFull = Path.GetFullPath(plugin);
    var sharedFiles = sharedDirs
        .Where(Directory.Exists)
        .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
        .Select(Path.GetFullPath)
        .Where(f => !string.Equals(f, pluginFull, StringComparison.OrdinalIgnoreCase)) // never inline the entry
        .Where(f => !IsGenerated(f)) // skip bin/obj output (AssemblyInfo, GlobalUsings, etc.)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    BundleResult result;
    try
    {
        result = Bundler.Bundle(new BundleRequest(
            plugin,
            sharedFiles,
            Transform: new TransformOptions { Version = resolved },
            CarbonRefDirs: carbonRefs,
            OxideRefDirs: oxideRefs,
            PartRegions: partRegions));
    }
    catch (BundleException ex)
    {
        Console.Error.WriteLine($"[bundler] ERROR: {ex.Message}");
        return 1;
    }

    // Always emit (even on a failed compile-check) so the bad bundle is on disk to inspect.
    string? outFull = null;
    if (outPath is not null)
    {
        outFull = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outFull)!);
        File.WriteAllText(outFull, result.Source);
    }
    else
    {
        Console.WriteLine(result.Source);
    }

    Console.Error.WriteLine($"[bundler] {Path.GetFileName(plugin)} v{resolved}: inlined {result.InlinedTypes.Count} shared type(s): {string.Join(", ", result.InlinedTypes)}");
    if (result.ShakenMembers is { Count: > 0 } shakenMembers)
        Console.Error.WriteLine($"[bundler] shook {shakenMembers.Count} unused member(s): {string.Join(", ", shakenMembers)}");

    if (!result.Checked)
    {
        Console.Error.WriteLine("[bundler] compile-check skipped (no --carbon-refs/--oxide-refs given)");
        return 0;
    }

    // The compile-check tree is parsed without a path, so map line/col onto the emitted file.
    var reportPath = outFull ?? "<bundle>";
    foreach (var check in result.Checks)
    {
        if (check.Compiles)
        {
            Console.Error.WriteLine($"[bundler] {check.Platform}: compiles OK");
            continue;
        }

        Console.Error.WriteLine($"[bundler] {check.Platform}: FAILED to compile ({check.Errors.Count} error(s)) -> {reportPath}");
        foreach (var d in check.Errors)
        {
            var pos = d.Location.GetLineSpan().StartLinePosition;
            // MSBuild-recognized format: editors/build logs turn this into a clickable error.
            Console.Error.WriteLine(
                $"{reportPath}({pos.Line + 1},{pos.Character + 1}): error {d.Id}: {d.GetMessage()} [{check.Platform}]");
        }
    }
    return result.Compiles ? 0 : 1;
}

// Bootstrap the per-plugin kit wiring (<Plugin>.Kit.g.cs) from the plugin's config/lang classes.
// --src scans a tree for plugin folders (a folder containing <DirName>.cs); --dir wires one
// folder. --check fails (exit 1) instead of writing, for CI staleness gates.
static int Wire(string[] args)
{
    string src = null, dir = null;
    var check = false;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--src": src = args[++i]; break;
            case "--dir": dir = args[++i]; break;
            case "--check": check = true; break;
            default: return Usage($"unknown argument: {args[i]}");
        }
    }
    if (src is null && dir is null)
        return Usage("wire needs --src or --dir");

    var folders = new List<string>();
    if (dir is not null)
        folders.Add(dir);
    if (src is not null)
        folders.AddRange(Directory.EnumerateDirectories(src, "*", SearchOption.AllDirectories)
            .Where(d => !d.Contains($"{Path.DirectorySeparatorChar}bin") && !d.Contains($"{Path.DirectorySeparatorChar}obj")));

    try
    {
        foreach (var folder in folders)
        {
            var status = KitWire.WireFolder(folder, check);
            if (status is not null)
                Console.Error.WriteLine($"[wire] {status}");
            var menuStatus = MenuWire.WireFolder(folder, check);
            if (menuStatus is not null)
                Console.Error.WriteLine($"[wire] {menuStatus}");
        }
        return 0;
    }
    catch (Exception ex) when (ex is KitWireException or MenuWireException)
    {
        Console.Error.WriteLine($"[wire] ERROR: {ex.Message}");
        return 1;
    }
}

static int PrintVersion(string[] args)
{
    string? version = null, changelog = null;
    var dev = false;
    var all = false;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--version": version = args[++i]; break;
            case "--changelog": changelog = args[++i]; break;
            case "--dev": dev = true; break;
            case "--all": all = true; break;
            default: return Usage($"unknown argument: {args[i]}");
        }
    }

    // --all lists every changelog version, newest first (for the release workflow's accumulation
    // check). It needs a changelog and ignores --dev/--version.
    if (all)
    {
        if (changelog is null) return Usage("version --all needs --changelog");
        if (!File.Exists(changelog)) { Console.Error.WriteLine($"changelog not found: {changelog}"); return 1; }
        foreach (var token in Changelog.VersionTokens(File.ReadAllText(changelog)))
            Console.WriteLine(token);
        return 0;
    }

    var resolved = ResolveVersion(version, changelog, dev);
    if (resolved is null) return 2;
    Console.WriteLine(resolved);
    return 0;
}

static int PrintNotes(string[] args)
{
    string? changelog = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--changelog": changelog = args[++i]; break;
            default: return Usage($"unknown argument: {args[i]}");
        }
    }
    if (changelog is null) return Usage("notes needs --changelog");
    if (!File.Exists(changelog)) { Console.Error.WriteLine($"changelog not found: {changelog}"); return 1; }
    Console.WriteLine(Changelog.TopNotes(File.ReadAllText(changelog)));
    return 0;
}

static int ValidateChangelog(string[] args)
{
    string? changelog = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--changelog": changelog = args[++i]; break;
            default: return Usage($"unknown argument: {args[i]}");
        }
    }
    if (changelog is null) return Usage("validate needs --changelog");
    if (!File.Exists(changelog)) { Console.Error.WriteLine($"changelog not found: {changelog}"); return 1; }

    var text = File.ReadAllText(changelog);
    var problems = Changelog.Validate(text);
    if (problems.Count == 0)
    {
        Console.Error.WriteLine($"[changelog] {changelog}: OK (top {Changelog.TopVersion(text)})");
        return 0;
    }
    Console.Error.WriteLine($"[changelog] {changelog}: INVALID");
    foreach (var p in problems) Console.Error.WriteLine($"  - {p}");
    return 1;
}

// Resolve the version: explicit --version wins, else derive from --changelog's top entry. --dev
// appends the monotonic revision. Prints an error and returns null on failure.
static string? ResolveVersion(string? explicitVersion, string? changelogPath, bool dev)
{
    var version = explicitVersion;
    if (version is null && changelogPath is not null)
    {
        if (!File.Exists(changelogPath)) { Console.Error.WriteLine($"changelog not found: {changelogPath}"); return null; }
        version = Changelog.TopVersion(File.ReadAllText(changelogPath));
        if (version is null) { Console.Error.WriteLine($"no version entries in {changelogPath}"); return null; }
    }
    if (version is null) { Console.Error.WriteLine("need --version or --changelog"); return null; }
    return dev ? PluginVersion.WithDevRevision(version, DateTime.UtcNow) : version;
}

static string[] SplitDirs(string value) =>
    value.Split(new[] { ';', Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// True if the file sits under a bin/ or obj/ directory (MSBuild build output we never inline).
static bool IsGenerated(string fullPath)
{
    var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase)
                       || p.Equals("obj", StringComparison.OrdinalIgnoreCase));
}

static int Usage(string? message)
{
    if (message is not null) Console.Error.WriteLine(message);
    Console.Error.WriteLine(
        "usage:\n" +
        "  hizenbundle bundle --plugin <f> --shared-dir <d>[;<d>...] (--version <v> | --changelog <f>) [--dev]\n" +
        "             [--part-regions] [--out <f>] [--carbon-refs <d>[;...]] [--oxide-refs <d>[;...]]\n" +
        "  hizenbundle wire (--src <d> | --dir <d>) [--check]\n" +
        "  hizenbundle version (--version <v> | --changelog <f>) [--dev] | --all --changelog <f>\n" +
        "  hizenbundle notes --changelog <f>\n" +
        "  hizenbundle validate --changelog <f>");
    return 2;
}
