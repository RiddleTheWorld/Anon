using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Anon;

public sealed class Aliases(Configuration config)
{
    private const int MaxNames = 1000;
    private const int MintAttempts = 8;

    private readonly HashSet<string> seen = CustomKeys(config);
    private readonly Dictionary<string, string> issued = CustomIssued(config);

    private static Dictionary<string, string> CustomIssued(Configuration config)
    {
        var map = new Dictionary<string, string>();
        foreach (var (real, custom) in config.CustomNames)
            if (custom.Length > 0) map[custom] = real;

        return map;
    }

    private static HashSet<string> CustomKeys(Configuration config) =>
        new(config.CustomNames.Keys.Where(name => name.Length > 0), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> retired = [];
    private readonly Dictionary<string, string> current = [];
    private readonly Dictionary<string, (int Seed, string Alias)> generated = [];
    private readonly Dictionary<string, string> issuedServers = [];
    private Dictionary<string, string> places = [];
    private (uint Current, uint Home, int WorldSeed, int ServerSeed, string DataCenter) placeKey;

    public int Version { get; private set; }
    public bool Active { get; set; }
    public string MyName { get; set; } = string.Empty;

    public string? Find(string name)
    {
        if (name.Length == 0) return null;

        name = RealOf(name);
        if (!HasCustom(name) && issued.ContainsKey(name)) return null;

        if (!Masks(name))
        {
            if (current.Remove(name, out var alias))
            {
                retired[alias] = name;
                Version++;
            }

            return null;
        }

        if (seen.Count < MaxNames && seen.Add(name)) Version++;
        return Fake(name);
    }

    private bool Masks(string name) =>
        Active
        && (string.Equals(name, MyName, StringComparison.OrdinalIgnoreCase) ? config.Self : config.Others)
        && (HasCustom(name) || !config.Whitelist.Contains(name, StringComparer.OrdinalIgnoreCase));

    private bool HasCustom(string name) =>
        config.CustomNames.TryGetValue(name, out var chosen) && chosen.Length > 0;

    private string Fake(string name)
    {
        string alias;
        if (config.CustomNames.TryGetValue(name, out var chosen) && chosen.Length > 0) alias = chosen;
        else if (generated.TryGetValue(name, out var cached) && cached.Seed == config.NameSeed) alias = cached.Alias;
        else generated[name] = (config.NameSeed, alias = Mint(name, config.NameSeed));

        if (current.TryGetValue(name, out var previous) && previous != alias) retired[previous] = name;
        current[name] = alias;
        issued[alias] = name;
        return alias;
    }

    public bool Issued(string text) => issued.ContainsKey(text);

    public IEnumerable<(string Name, string Alias)> Matches(string text, bool servers = false)
    {
        IEnumerable<string> real = servers ? places.Keys : seen;
        foreach (var name in real)
        {
            if (!text.Contains(name) || !Names.EveryMentionStandsAlone(text, name)) continue;

            var alias = servers ? FindServer(name) : Find(name);
            if (alias != null && !alias.Contains(name)) yield return (name, alias);
        }

        List<(string Old, string Owner)>? stale = null;
        foreach (var (old, owner) in servers ? issuedServers : retired)
        {
            if (!text.Contains(old) || !Names.EveryMentionStandsAlone(text, old)) continue;

            stale ??= [];
            stale.Add((old, owner));
        }

        if (stale == null) yield break;

        foreach (var (old, owner) in stale)
        {
            var shown = (servers ? FindServer(owner) : Find(owner)) ?? owner;
            if (!shown.Contains(old)) yield return (old, shown);
        }
    }

    public string RealOf(string text) => issued.GetValueOrDefault(text, text);

    public string Replace(string text, bool servers = false)
    {
        foreach (var (name, alias) in Matches(text, servers)) text = Names.ReplaceWholeWords(text, name, alias);
        return text;
    }

    public void UpdatePlaces(IPlayerCharacter me)
    {
        var fakeDataCenter = config.Names.FakeDataCenter;
        var key = (me.CurrentWorld.RowId, me.HomeWorld.RowId, config.WorldSeed, config.ServerSeed, fakeDataCenter);
        if (key == placeKey) return;

        placeKey = key;
        try
        {
            places = Servers.Aliases(WorldName(me.CurrentWorld), WorldName(me.HomeWorld), config.WorldSeed, config.ServerSeed, fakeDataCenter);
            foreach (var (real, alias) in places) issuedServers[alias] = real;
        }
        catch (Exception ex)
        {
            places = [];
            Plugin.Log.Error(ex, "Failed to build server aliases");
        }
    }

    public void RefreshServers()
    {
        placeKey = (0, 0, 0, 0, string.Empty);
        places = [];
        Version++;
        Plugin.NamePlates.RequestRedraw();
    }

    public void RefreshNames()
    {
        foreach (var (real, custom) in config.CustomNames)
        {
            if (real.Length == 0) continue;

            seen.Add(real);
            if (custom.Length > 0) issued[custom] = real;
        }

        Version++;
        Plugin.NamePlates.RequestRedraw();
    }

    private string? FindServer(string place) =>
        config.Names.HideWorld && Active && places.TryGetValue(place, out var alias) ? alias : null;

    public string? WorldAlias(string world) => FindServer(world);

    private static string WorldName(RowRef<World> world) => world.ValueNullable?.Name.ToString() ?? string.Empty;

    public IEnumerable<(string Fake, string Real)> Reveal(string text) =>
        issued
            .Concat(issuedServers)
            .Where(pair => text.Contains(pair.Key) && !pair.Value.Contains(pair.Key) && Names.EveryMentionStandsAlone(text, pair.Key))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => (pair.Key, pair.Value));

    public void RerollNames()
    {
        foreach (var (name, alias) in current) retired[alias] = name;
        current.Clear();
        config.NameSeed++;
        config.Save();
        Version++;
        Plugin.NamePlates.RequestRedraw();
    }

    public void Trim()
    {
        if (seen.Count < MaxNames) return;

        seen.Clear();
        seen.UnionWith(config.CustomNames.Keys);
        seen.Remove(string.Empty);
        Version++;
    }

    private string Mint(string name, int seed)
    {
        for (var attempt = 0; attempt < MintAttempts; attempt++)
        {
            var alias = Names.Fake(name, seed + attempt);
            if (!Taken(alias, name)) return alias;
        }

        return Names.Fake(name, seed);
    }

    private bool Taken(string alias, string owner) =>
        seen.Contains(alias) || (issued.TryGetValue(alias, out var existing) && existing != owner);
}
