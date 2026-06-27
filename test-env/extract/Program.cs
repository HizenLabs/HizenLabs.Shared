using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

if (args.Length < 1)
{
    Console.WriteLine("Usage: extractpdb <dir-with-dlls> [recurse]");
    return 1;
}

string root = args[0];
bool recurse = args.Length > 1 && args[1].Equals("recurse", StringComparison.OrdinalIgnoreCase);
var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

int extracted = 0, none = 0, errors = 0;

foreach (var dll in Directory.EnumerateFiles(root, "*.dll", option))
{
    try
    {
        using var fs = File.OpenRead(dll);
        using var pe = new PEReader(fs);
        if (!pe.HasMetadata) continue;

        bool found = false;
        foreach (var entry in pe.ReadDebugDirectory())
        {
            if (entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb) continue;

            using var provider = pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            var reader = provider.GetMetadataReader();

            byte[] bytes = new byte[reader.MetadataLength];
            unsafe { Marshal.Copy((IntPtr)reader.MetadataPointer, bytes, 0, reader.MetadataLength); }

            string pdbPath = Path.ChangeExtension(dll, ".pdb");
            File.WriteAllBytes(pdbPath, bytes);
            Console.WriteLine($"  extracted: {Path.GetFileName(pdbPath)}");
            extracted++;
            found = true;
        }
        if (!found) none++;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  skip {Path.GetFileName(dll)}: {ex.Message}");
        errors++;
    }
}

Console.WriteLine($"\nDone. Extracted {extracted}, no-embedded-pdb {none}, errors {errors}.");
return 0;
