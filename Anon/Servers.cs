using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Anon;

public static class Servers
{
    private static readonly Dictionary<string, string[]> Known = new()
    {
        ["Aether"] = ["Adamantoise", "Cactuar", "Faerie", "Gilgamesh", "Jenova", "Midgardsormr", "Sargatanas", "Siren"],
        ["Primal"] = ["Behemoth", "Excalibur", "Exodus", "Famfrit", "Hyperion", "Lamia", "Leviathan", "Ultros"],
        ["Crystal"] = ["Balmung", "Brynhildr", "Coeurl", "Diabolos", "Goblin", "Malboro", "Mateus", "Zalera"],
        ["Dynamis"] = ["Cuchulainn", "Golem", "Halicarnassus", "Kraken", "Maduin", "Marilith", "Rafflesia", "Seraph"],
        ["Chaos"] = ["Cerberus", "Louisoix", "Moogle", "Omega", "Phantom", "Ragnarok", "Sagittarius", "Spriggan"],
        ["Light"] = ["Alpha", "Lich", "Odin", "Phoenix", "Raiden", "Shiva", "Twintania", "Zodiark"],
        ["Shadow"] = ["Innocence", "Pixie", "Titania", "Tycoon"],
        ["Materia"] = ["Bismarck", "Ravana", "Sephirot", "Sophia", "Zurvan"],
        ["Elemental"] = ["Aegis", "Atomos", "Carbuncle", "Garuda", "Gungnir", "Kujata", "Tonberry", "Typhon"],
        ["Gaia"] = ["Alexander", "Bahamut", "Durandal", "Fenrir", "Ifrit", "Ridill", "Tiamat", "Ultima"],
        ["Mana"] = ["Anima", "Asura", "Chocobo", "Hades", "Ixion", "Masamune", "Pandaemonium", "Titan"],
        ["Meteor"] = ["Belias", "Mandragora", "Ramuh", "Shinryu", "Unicorn", "Valefor", "Yojimbo", "Zeromus"],
    };

    private static Dictionary<string, string[]>? realms;

    private static Dictionary<string, string[]> Realms => realms ??= Verify();

    public static Dictionary<string, string> Aliases(string currentWorld, string homeWorld, int worldSeed, int dataCenterSeed, string? preferredDataCenter = null)
    {
        var current = (World: currentWorld, DataCenter: DataCenterOf(currentWorld));
        var home = (World: homeWorld, DataCenter: DataCenterOf(homeWorld));
        var aliases = new Dictionary<string, string>();
        var others = AvailableDataCenters(currentWorld, homeWorld);
        if (others.Count == 0) return aliases;

        var preferred = string.IsNullOrWhiteSpace(preferredDataCenter)
            ? null
            : others.FirstOrDefault(dataCenter => string.Equals(dataCenter, preferredDataCenter, StringComparison.OrdinalIgnoreCase));

        var dataCenterRandom = new Random(Names.Seed(current.World + home.World, dataCenterSeed));
        var worldRandom = new Random(Names.Seed(current.World + home.World, worldSeed));
        var currentDataCenter = preferred ?? others[dataCenterRandom.Next(others.Count)];
        var homeDataCenter = preferred ?? (current.DataCenter == home.DataCenter ? currentDataCenter : others[dataCenterRandom.Next(others.Count)]);
        var fakeWorld = PickWorld(currentDataCenter, worldRandom, null);

        Add(aliases, current.DataCenter, currentDataCenter);
        Add(aliases, home.DataCenter, homeDataCenter);
        Add(aliases, current.World, fakeWorld);
        if (home.World != current.World) Add(aliases, home.World, PickWorld(homeDataCenter, worldRandom, fakeWorld));

        return aliases;
    }

    public static IReadOnlyList<string> AvailableDataCenters(string currentWorld, string homeWorld)
    {
        var current = DataCenterOf(currentWorld);
        var home = DataCenterOf(homeWorld);
        return Realms.Keys
            .Where(dataCenter => !string.Equals(dataCenter, current, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(dataCenter, home, StringComparison.OrdinalIgnoreCase))
            .OrderBy(dataCenter => dataCenter)
            .ToArray();
    }

    public static string WorldName(RowRef<World> world) => world.ValueNullable?.Name.ToString() ?? string.Empty;

    private static string DataCenterOf(string world) => Known.FirstOrDefault(realm => realm.Value.Contains(world)).Key ?? string.Empty;

    private static void Add(Dictionary<string, string> aliases, string real, string alias)
    {
        if (real.Length > 0) aliases[real] = alias;
    }

    private static string PickWorld(string dataCenter, Random random, string? exclude)
    {
        var worlds = Realms[dataCenter].Where(world => world != exclude).ToArray();
        if (worlds.Length == 0) worlds = Realms[dataCenter];

        return worlds[random.Next(worlds.Length)];
    }

    private static Dictionary<string, string[]> Verify()
    {
        var inGame = Plugin.Data.GetExcelSheet<World>().Where(world => world.IsPublic).Select(world => world.Name.ToString()).ToHashSet();
        return Known
            .Select(realm => (realm.Key, Worlds: realm.Value.Where(inGame.Contains).ToArray()))
            .Where(realm => realm.Worlds.Length > 0)
            .ToDictionary(realm => realm.Key, realm => realm.Worlds);
    }
}
