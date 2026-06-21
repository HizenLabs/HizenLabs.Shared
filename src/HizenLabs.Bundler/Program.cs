// HizenLabs.Bundler CLI — merge a plugin + the shared code it uses into one .cs.
//
//   hizenbundle --plugin <file> --shared-dir <dir> --namespace <ns> [--out <file>]
using HizenLabs.Bundler;

var opts = ParseArgs(args);
if (opts is null) return 2;

var sharedFiles = Directory.EnumerateFiles(opts.SharedDir, "*.cs", SearchOption.AllDirectories).ToList();
var result = Bundler.Bundle(new BundleRequest(opts.Plugin, sharedFiles, opts.Namespace));

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
if (result.Compiles)
{
    Console.Error.WriteLine("[bundler] output compiles ✓");
    return 0;
}

Console.Error.WriteLine("[bundler] output FAILED to compile:");
foreach (var d in result.Diagnostics)
    Console.Error.WriteLine($"  {d.Id}: {d.GetMessage()}");
return 1;

static Options? ParseArgs(string[] args)
{
    string? plugin = null, sharedDir = null, ns = null, outPath = null;
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--plugin": plugin = args[++i]; break;
            case "--shared-dir": sharedDir = args[++i]; break;
            case "--namespace": ns = args[++i]; break;
            case "--out": outPath = args[++i]; break;
            default:
                Console.Error.WriteLine($"unknown argument: {args[i]}");
                return null;
        }
    }
    if (plugin is null || sharedDir is null || ns is null)
    {
        Console.Error.WriteLine("usage: hizenbundle --plugin <file> --shared-dir <dir> --namespace <ns> [--out <file>]");
        return null;
    }
    return new Options(plugin, sharedDir, ns, outPath);
}

sealed record Options(string Plugin, string SharedDir, string Namespace, string? Out);
