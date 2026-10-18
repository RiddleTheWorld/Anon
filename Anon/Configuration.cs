using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Newtonsoft.Json;

namespace Anon;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; }
    public bool Enabled;
    public bool Self;
    public bool Others;
    public bool Diagnostics;
    public int LookSeed = Random.Shared.Next();
    public int NameSeed = Random.Shared.Next();
    public int WorldSeed = Random.Shared.Next();
    public int ServerSeed = Random.Shared.Next();
    public KeybindSettings Keybinds = new();
    public List<string> Whitelist = [];
    public Dictionary<string, string> CustomNames = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> SavedLooks = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> LookAssignments = new(StringComparer.OrdinalIgnoreCase);
    public bool ShowAsSeen;
    public LookSettings Look = new();
    public NameSettings Names = new();
    public GearSettings Gear = new();
    public SafetySettings Safety = new();

    public static Configuration Load()
    {
        if (Plugin.PluginInterface.GetPluginConfig() is Configuration saved)
        {
            saved.Normalize();
            return saved;
        }

        var created = new Configuration();
        created.Save();
        return created;
    }

    private void Normalize()
    {
        CustomNames = new Dictionary<string, string>(CustomNames, StringComparer.OrdinalIgnoreCase);
        SavedLooks = new Dictionary<string, string>(SavedLooks, StringComparer.OrdinalIgnoreCase);
        LookAssignments = new Dictionary<string, string>(LookAssignments, StringComparer.OrdinalIgnoreCase);
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    public bool IsActive(uint territoryType) => Enabled && !(Safety.DisableInZones && Safety.Zones.Contains(territoryType));
}

public class LookSettings
{
    public bool SameRaceAsMe;
    public bool AllRaces;
}

public class NameSettings
{
    public bool Chat;
    public bool MaskChatPanel;
    public bool PartyList;
    public bool Windows;
    public bool HideWorld;
    public string FakeDataCenter = string.Empty;
    public bool HideFcTag;
    public bool HideTitle;
}

public class Keybind
{
    public VirtualKey Key = VirtualKey.NO_KEY;
    public bool Ctrl;
    public bool Shift;
    public bool Alt;

    [JsonIgnore]
    public bool IsSet => Key != VirtualKey.NO_KEY;

    public bool Matches(Keybind other) => IsSet && Key == other.Key && Ctrl == other.Ctrl && Shift == other.Shift && Alt == other.Alt;

    public bool IsPressed() => IsSet
        && Plugin.KeyState[Key]
        && Plugin.KeyState[VirtualKey.CONTROL] == Ctrl
        && Plugin.KeyState[VirtualKey.SHIFT] == Shift
        && Plugin.KeyState[VirtualKey.MENU] == Alt;

    public void Clear()
    {
        Key = VirtualKey.NO_KEY;
        Ctrl = Shift = Alt = false;
    }

    public override string ToString()
    {
        if (!IsSet) return "None";

        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }
}

public class KeybindSettings
{
    public Keybind Toggle = new();
    public Keybind Reroll = new();
    public Keybind RerollLook = new();
    public Keybind RerollNames = new();

    [JsonIgnore]
    public Keybind[] All => [Toggle, Reroll, RerollLook, RerollNames];
}

public enum OutfitStyle
{
    Real,
    Plain,
    Random,
}

public class GearSettings
{
    public OutfitStyle Style;
    public bool HideWeapons;
}

public class SafetySettings
{
    public bool PauseInDuty;
    public bool PauseInCombat;
    public int MaxRedrawsPerFrame = 3;
    public bool DisableInZones;
    public List<uint> Zones = [];
}
