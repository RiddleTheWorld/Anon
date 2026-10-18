using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Anon;

public sealed class AppearanceStore
{
    private sealed class FileModel
    {
        public int Version { get; set; } = 1;
        public List<FileEntry> Appearances { get; set; } = [];
    }

    private sealed class FileEntry
    {
        public string Label { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
    }

    private readonly record struct Info(bool Both, bool AsSeen, bool Glam, bool Extras, string Hash);

    private readonly string path;
    private readonly Dictionary<string, string> looks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Info> infos = new(StringComparer.OrdinalIgnoreCase);

    public string? Error { get; private set; }

    public int Count => looks.Count;

    public IEnumerable<string> Labels => looks.Keys;

    private AppearanceStore()
    {
        path = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "appearances.json");
    }

    public static AppearanceStore Load(Configuration config)
    {
        var store = new AppearanceStore();
        store.Open(config);
        return store;
    }

    public string? Find(string? label) =>
        label != null && looks.TryGetValue(label, out var data) ? data : null;

    public bool IsAsSeenOnly(string label) =>
        infos.TryGetValue(label, out var info) && info.AsSeen && !info.Both;

    public string Kind(string label)
    {
        if (!infos.TryGetValue(label, out var info)) return "unreadable";

        if (info.Both) return "exact copy (real + what you see)";
        if (info.Extras) return info.AsSeen ? "exact copy (what you see only - copy again for both)" : "exact copy (real only)";
        if (info.Glam) return "face + glamour, copy again for an exact copy";
        return "face only";
    }

    public string? DuplicateOf(string data)
    {
        var hash = Hash(data);
        foreach (var (label, info) in infos)
        {
            if (string.Equals(info.Hash, hash, StringComparison.Ordinal)) return label;
        }

        return null;
    }

    public string Add(string wanted, string data)
    {
        var label = SavedLooks.FreeLabel(looks, wanted);
        looks[label] = data;
        infos[label] = Describe(data);
        Save();
        return label;
    }

    public bool Remove(string label, Dictionary<string, string> assignments)
    {
        if (!looks.ContainsKey(label)) return false;

        SavedLooks.Remove(looks, assignments, label);
        infos.Remove(label);
        Save();
        return true;
    }

    public string Rename(string from, string to, Dictionary<string, string> assignments)
    {
        if (from == to || !looks.Remove(from, out var data)) return from;

        infos.Remove(from);
        var free = SavedLooks.FreeLabel(looks, to);
        looks[free] = data;
        infos[free] = Describe(data);

        foreach (var (player, label) in new List<KeyValuePair<string, string>>(assignments))
        {
            if (label == from) assignments[player] = free;
        }

        Save();
        return free;
    }

    private void Open(Configuration config)
    {
        if (File.Exists(path))
        {
            try
            {
                var model = JsonConvert.DeserializeObject<FileModel>(File.ReadAllText(path));
                foreach (var entry in model?.Appearances ?? [])
                {
                    var label = entry.Label.Trim();
                    if (label.Length == 0 || string.IsNullOrEmpty(entry.Data)) continue;
                    if (!looks.TryAdd(label, entry.Data)) continue;
                }

                Rebuild();
                return;
            }
            catch (Exception ex)
            {
                Error = $"Could not read appearances.json: {ex.Message}";
                try { File.Move(path, path + ".bad", overwrite: true); } catch { }
            }
        }

        foreach (var (label, data) in config.SavedLooks) looks.TryAdd(label, data);

        Rebuild();
        if (looks.Count > 0) Save();

        if (config.SavedLooks.Count > 0 && Error == null)
        {
            config.SavedLooks.Clear();
            config.Save();
        }
    }

    private void Rebuild()
    {
        infos.Clear();
        foreach (var (label, data) in looks) infos[label] = Describe(data);
    }

    private static Info Describe(string data)
    {
        var shape = SavedLooks.Describe(data);
        return new Info(shape?.Both ?? false, shape?.AsSeen ?? false, shape?.Glam ?? false, shape?.Extras ?? false, Hash(data));
    }

    private static string Hash(string data) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var model = new FileModel
            {
                Appearances = looks.Select(look => new FileEntry
                {
                    Label = look.Key,
                    Data = look.Value,
                }).ToList(),
            };

            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(model, Formatting.Indented), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);

            Error = null;
        }
        catch (Exception ex)
        {
            Error = $"Could not save appearances.json: {ex.Message}";
        }
    }
}
