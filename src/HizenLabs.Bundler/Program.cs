// HizenLabs.Bundler CLI - merge a plugin + the shared code it uses into one deployable .cs.
//
//   hizenbundle --plugin <file> --shared-dir <dir> [--out <file>]
//               [--carbon-refs <dir>[;<dir>...]] [--oxide-refs <dir>[;<dir>...]]
//
// The bundler keeps the author's neutral namespace and `: PluginBase`; the transform pipeline
// rewrites them into the #if CARBON / #else split. Pass per-platform reference dirs to have the
// emitted file compile-checked under Carbon and/or Oxide (game + framework assemblies).
using HizenLabs.Bundler;

var opts = ParseArgs(args);
if (opts is null) return 2;

var sharedFiles = Directory.EnumerateFiles(opts.SharedDir, "*.cs", SearchOption.AllDirectories).ToList();
var result = Bundler.Bundle(new BundleRequest(
    opts.Plugin,
    sharedFiles,
    Transform: null,
    CarbonRefDirs: opts.CarbonRefs,
    OxideRefDirs: opts.OxideRefs));

if (opts.Out is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(opts.Out))!);
    File.WriteAllText(opts.Out, result.Source);
}
else
{
    Console.WriteLine(result.Source);
}

Console.Error.WriteLine($"[bundler] inlined {result.InlinedTypes.Count} shared type(s): {string.Join(", ", result.InlinedTypes)}");

if (!result.Checked)
{
    Console.Error.WriteLine("[bundler] compile-check skipped (no --carbon-refs/--oxide-refs given)");
    return 0;
}

foreach (var check in result.Checks)
{
    if (check.Compiles)
    {
        Console.Error.WriteLine($"[bundler] {check.Platform}: compiles OK");
    }
    else
    {
        Console.Error.WriteLine($"[bundler] {check.Platform}: FAILED to compile:");
        foreach (var d in check.Errors)
            Console.Error.WriteLine($"  {d.Id}: {d.GetMessage()}");
    }
}
return result.Compiles ? 0 : 1;

static Options? ParseArgs(string[] args)
{
    string? plugin = null, sharedDir = null, outPath = null;
    string[]? carbonRefs = null, oxideRefs = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--plugin": plugin = args[++i]; break;
            case "--shared-dir": sharedDir = args[++i]; break;
            case "--out": outPath = args[++i]; break;
            case "--carbon-refs": carbonRefs = SplitDirs(args[++i]); break;
            case "--oxide-refs": oxideRefs = SplitDirs(args[++i]); break;
            default:
                Console.Error.WriteLine($"unknown argument: {args[i]}");
                return null;
        }
    }
    if (plugin is null || sharedDir is null)
    {
        Console.Error.WriteLine(
            "usage: hizenbundle --plugin <file> --shared-dir <dir> [--out <file>] " +
            "[--carbon-refs <dir>[;<dir>...]] [--oxide-refs <dir>[;<dir>...]]");
        return null;
    }
    return new Options(plugin, sharedDir, outPath, carbonRefs, oxideRefs);
}

static string[] SplitDirs(string value) =>
    value.Split(new[] { ';', Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

sealed record Options(string Plugin, string SharedDir, string? Out, string[]? CarbonRefs, string[]? OxideRefs);
