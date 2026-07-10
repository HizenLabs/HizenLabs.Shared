using System;
using System.IO;

namespace HizenLabs.Shared.Config;

/// <summary>
/// Load/save plumbing behind <see cref="BaseConfig"/>. All JSON work happens here, at plugin
/// load/save only - steady-state config access is a plain static field read on the plugin.
/// </summary>
public static class ConfigKit
{
    /// <summary>
    /// Reads the plugin's config file into <typeparamref name="T"/>. A missing/corrupt file is
    /// backed up (once, to "&lt;file&gt;.broken") and replaced with defaults instead of taking the
    /// plugin down. The parsed config is written back so keys added in an update appear in the
    /// file immediately.
    /// </summary>
    public static T Load<T>(PluginBase plugin) where T : BaseConfig, new()
    {
        try
        {
            var config = plugin.Config.ReadObject<T>()
                ?? throw new InvalidOperationException("config file is empty");
            plugin.Config.WriteObject(config, true);
            return config;
        }
        catch (Exception ex)
        {
            plugin.PrintWarning($"Failed to load config ({ex.Message}); backing up the broken file and starting from defaults.");
            Backup(plugin);
            return Default<T>(plugin);
        }
    }

    /// <summary>Creates a default <typeparamref name="T"/> and writes it as the config file.</summary>
    public static T Default<T>(PluginBase plugin) where T : BaseConfig, new()
    {
        var config = new T();
        plugin.Config.WriteObject(config, true);
        return config;
    }

    /// <summary>Writes the given config to the plugin's config file.</summary>
    public static void Save<T>(PluginBase plugin, T config) where T : BaseConfig
    {
        if (config is null)
            return;
        plugin.Config.WriteObject(config, true);
    }

    private static void Backup(PluginBase plugin)
    {
        try
        {
            var path = plugin.Config.Filename;
            if (File.Exists(path))
                File.Copy(path, path + ".broken", overwrite: true);
        }
        catch
        {
            // best effort - never let a backup failure mask the original problem
        }
    }
}
