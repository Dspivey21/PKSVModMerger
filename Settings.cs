using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PKSVModMerger;

internal sealed class Settings
{
    public string? OutputFolder { get; set; }
    public List<ModRef> Mods { get; set; } = new();

    public sealed class ModRef
    {
        public string FolderPath { get; set; } = "";
        public string TrpfdPath { get; set; } = "";
    }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PKSVModMerger",
        "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            // Unreadable/corrupt settings shouldn't crash the app — fall through to defaults.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Saving settings is best-effort; never bubble up.
        }
    }
}
