using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Anon;

public sealed unsafe class Disguise : IDisposable
{
    private const int ReportMilliseconds = 2000;

    private static readonly ulong[] PlainOutfit = new ulong[Outfit.Slots];

    private sealed class Original(string name)
    {
        public readonly string Name = name;
        public readonly GearState Gear = new();
        public readonly ExtrasState Extras = new();
        public byte[]? Look;
        public byte[]? Target;

        public string? SavedLabel;
        public string? SavedText;
        public bool SavedSeen;
        public byte[]? Saved;
        public ulong[]? SavedGlam;
        public ulong[]? SavedExtras;
        public int TargetKey;
        public ulong[]? Outfit;
        public int OutfitSeed;
        public bool NeedsRedraw;
    }

    private readonly ErrorOnce errors = new("Anon: failed to disguise a player");
    private readonly Aliases aliases;
    private readonly Configuration config;
    private readonly AppearanceStore appearances;
    private readonly Pacing pacing;
    private readonly Dictionary<ulong, Original> originals = [];
    private readonly HashSet<ulong> seen = [];
    private readonly HashSet<ulong> platesPending = [];
    private long nextReport;

    private int restored;
    private int looks;
    private int gear;
    private int myRace;
    private bool wasActive;

    public Disguise(Aliases aliases, Configuration config, AppearanceStore appearances)
    {
        this.aliases = aliases;
        this.config = config;
        this.appearances = appearances;
        pacing = new Pacing(config);
        Plugin.Framework.Update += OnUpdate;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        RestoreAll();
    }

    public string RealName(IGameObject obj)
    {
        var character = (Character*)obj.Address;
        return obj is IPlayerCharacter ? RealName(character) : character->NameString;
    }

    public string RealName(Character* character) =>
        originals.TryGetValue(character->ContentId, out var original) ? original.Name : aliases.RealOf(character->NameString);

    private static ulong ContentId(IGameObject obj) => ((Character*)obj.Address)->ContentId;

    public void Reroll()
    {
        config.LookSeed++;
        config.WorldSeed++;
        config.ServerSeed++;
        aliases.RerollNames();
    }

    public void RerollLook()
    {
        config.LookSeed++;
        config.Save();
    }

    private void OnTerritoryChanged(uint territory) => pacing.StartBurst();

    private void OnUpdate(IFramework framework)
    {
        aliases.Active = config.IsActive(Plugin.ClientState.TerritoryType);
        if (aliases.Active != wasActive)
        {
            wasActive = aliases.Active;
            pacing.StartBurst();
        }

        if (Pacing.IsBetweenAreas()) return;

        pacing.Begin();

        var me = Plugin.Objects.LocalPlayer;
        myRace = MyRace();
        aliases.MyName = me == null ? string.Empty : RealName(me);
        if (me != null) aliases.UpdatePlaces(me);

        Apply();
    }

    private void Apply()
    {
        var players = 0;
        var masked = 0;
        var unidentified = 0;
        var wrote = false;

        aliases.Trim();
        seen.Clear();
        var active = aliases.Active;

        foreach (var player in Plugin.Objects.PlayerObjects)
        {
            players++;

            var character = (Character*)player.Address;
            var id = character->ContentId;
            if (id == 0)
            {
                unidentified++;
                continue;
            }

            seen.Add(id);

            try
            {
                originals.TryGetValue(id, out var original);

                if (character->IsReadyToDraw() && platesPending.Remove(id)) wrote = true;

                var real = original?.Name ?? aliases.RealOf(character->NameString);
                var alias = active ? aliases.Find(real) : null;

                var label = active && config.LookAssignments.TryGetValue(real, out var assigned) ? assigned : null;
                var saved = SavedEntry(original, label);
                var useWearerReal = label != null && !config.ShowAsSeen && appearances.IsAsSeenOnly(label);
                if (alias == null && saved.Look == null && saved.Glam == null && saved.Extras == null)
                {
                    if (original == null) continue;

                    if (character->NameString != original.Name)
                    {
                        character->SetName(original.Name);
                        platesPending.Add(id);
                        wrote = true;
                    }

                    if (!RestorePlayer(character, original, force: !active)) continue;
                    originals.Remove(id);
                    restored++;
                    continue;
                }

                if (original == null)
                {
                    originals[id] = original = new Original(aliases.RealOf(character->NameString));
                }

                var wanted = alias ?? original.Name;
                if (character->NameString != wanted)
                {
                    character->SetName(wanted);
                    platesPending.Add(id);
                    wrote = true;
                }

                if (alias != null) masked++;

                var lookChanged = useWearerReal
                    ? RestoreLook(character, original)
                    : saved.Look != null || alias != null
                        ? ApplyLook(original, character, saved.Look)
                        : RestoreLook(character, original);
                if (lookChanged)
                {
                    looks++;
                    continue;
                }

                if (useWearerReal)
                {
                    if (ApplyGearValues(original, character, null))
                    {
                        gear++;
                        continue;
                    }
                }
                else if (saved.Glam != null)
                {
                    if (ApplyGearValues(original, character, saved.Glam))
                    {
                        gear++;
                        continue;
                    }
                }
                else if (alias != null)
                {
                    if (ApplyGear(original, character)) gear++;
                }
                else if (Outfit.Apply(character, original.Gear, Wearable(character, original.Gear, null)))
                {
                    gear++;
                }

                if (ApplyExtras(original, character, useWearerReal ? null : saved.Extras)) gear++;
            }
            catch (Exception ex)
            {
                errors.Report(ex);
            }
        }

        if (wrote) Plugin.NamePlates.RequestRedraw();

        if (unidentified == 0 && Plugin.Objects.LocalPlayer is { } me && seen.Contains(ContentId(me))) Prune();

        LogCounts(players, masked, unidentified);
    }

    private (byte[]? Look, ulong[]? Glam, ulong[]? Extras) SavedEntry(Original? original, string? label)
    {
        if (label == null) return (null, null, null);

        var stored = appearances.Find(label);
        var asSeen = config.ShowAsSeen;
        if (original != null && original.SavedLabel == label && original.SavedSeen == asSeen
            && string.Equals(original.SavedText, stored, StringComparison.Ordinal))
            return (original.Saved, original.SavedGlam, original.SavedExtras);

        byte[]? look = null;
        ulong[]? glam = null;
        ulong[]? extras = null;
        if (SavedLooks.TryDecodeEntry(stored, asSeen, out var bytes, out var slots, out var values))
        {
            look = Look.Normalized(bytes);
            glam = slots;
            extras = values;
        }

        if (original != null)
        {
            original.SavedLabel = label;
            original.SavedText = stored;
            original.SavedSeen = asSeen;
            original.Saved = look;
            original.SavedGlam = glam;
            original.SavedExtras = extras;
        }

        return (look, glam, extras);
    }

    private bool ApplyExtras(Original original, Character* character, ulong[]? wanted)
    {
        var ready = character->IsReadyToDraw();
        if (ready && !pacing.CanRedraw) return false;

        var changed = Extras.Apply(character, original.Extras, wanted, hideWeapons: config.Gear.HideWeapons);
        if (!ready || !changed) return false;

        pacing.Spend();
        return true;
    }

    private bool RestoreLook(Character* character, Original original)
    {
        if (original.Look == null) return false;

        return ApplyLookData(original, character, original.Look);
    }

    private bool ApplyGearValues(Original original, Character* character, ulong[]? glam)
    {
        var ready = character->IsReadyToDraw();
        if (ready && !pacing.CanRedraw) return false;

        var changed = Outfit.Apply(character, original.Gear, Wearable(character, original.Gear, glam));
        if (!ready || !changed) return false;

        pacing.Spend();
        return true;
    }

    private bool ApplyLook(Original original, Character* character, byte[]? saved)
    {
        var data = character->DrawData.CustomizeData.Data;
        if (data[0] == 0) return false;

        original.Look ??= data.ToArray();

        return ApplyLookData(original, character, saved ?? TargetLook(original));
    }

    private bool ApplyLookData(Original original, Character* character, byte[] look)
    {
        var data = character->DrawData.CustomizeData.Data;
        var ready = character->IsReadyToDraw();

        if (data.SequenceEqual(look))
        {
            if (!original.NeedsRedraw || !ready || !pacing.CanRedraw) return false;

            original.NeedsRedraw = false;
            Redraw(character);
            pacing.Spend();
            return true;
        }

        if (ready && !pacing.CanRedraw) return false;

        look.CopyTo(data);
        if (!ready)
        {
            original.NeedsRedraw = true;
            return false;
        }

        original.NeedsRedraw = false;
        Redraw(character);
        pacing.Spend();
        return true;
    }

    private bool ApplyGear(Original original, Character* character)
    {
        var ready = character->IsReadyToDraw();
        if (ready && !pacing.CanRedraw) return false;

        var changed = Outfit.Apply(character, original.Gear, Wearable(character, original.Gear, WantedOutfit(original)));
        if (!ready || !changed) return false;

        pacing.Spend();
        return true;
    }

    private ulong[]? WantedOutfit(Original original)
    {
        var style = config.Gear.Style;
        if (style == OutfitStyle.Plain) return PlainOutfit;
        if (style != OutfitStyle.Random) return null;

        if (original.Outfit == null || original.OutfitSeed != config.LookSeed)
        {
            original.Outfit = Wardrobe.Pick(new Random(Names.Seed(original.Name, config.LookSeed + 1)));
            original.OutfitSeed = config.LookSeed;
        }

        return original.Outfit;
    }

    private bool RestorePlayer(Character* character, Original original, bool force = false)
    {
        var ready = character->IsReadyToDraw();
        if (ready && !pacing.CanRedraw && !force) return false;

        var changed = false;

        var data = character->DrawData.CustomizeData.Data;
        if (original.Look != null)
        {
            if (!data.SequenceEqual(original.Look))
            {
                original.Look.CopyTo(data);
                if (!ready) original.NeedsRedraw = true;
                changed = true;
            }
            else if (original.NeedsRedraw)
            {
                changed = true;
            }
        }

        if (Outfit.Apply(character, original.Gear, null)) changed = true;
        if (Extras.Apply(character, original.Extras, null)) changed = true;

        if (ready && changed)
        {
            original.NeedsRedraw = false;
            Redraw(character);
            pacing.Spend();
        }

        return ready || !original.NeedsRedraw;
    }

    private byte[] TargetLook(Original original)
    {
        var settings = config.Look;
        var key = HashCode.Combine(config.LookSeed, myRace, settings.SameRaceAsMe, settings.AllRaces);
        if (original.Target == null || original.TargetKey != key)
        {
            original.Target = Look.Create(new Random(Names.Seed(original.Name, config.LookSeed)), settings, myRace);
            original.TargetKey = key;
        }

        return original.Target;
    }

    public byte[]? RealLook(nint address)
    {
        var character = (Character*)address;
        if (originals.TryGetValue(character->ContentId, out var original) && original.Look != null) return original.Look.ToArray();

        return Look.Capture(character);
    }

    public ulong[] RealGear(nint address)
    {
        var character = (Character*)address;
        var glam = Outfit.Capture(character);
        if (!originals.TryGetValue(character->ContentId, out var original)) return glam;

        for (var slot = 0; slot < Outfit.Slots; slot++)
        {
            if (original.Gear.Real[slot] is { } real) glam[slot] = real;
        }

        return glam;
    }

    public (string Label, bool Face, bool Glam)? Wearing(nint address)
    {
        var character = (Character*)address;
        if (!config.LookAssignments.TryGetValue(RealName(character), out var label)) return null;

        originals.TryGetValue(character->ContentId, out var original);
        var saved = SavedEntry(original, label);
        var useWearerReal = !config.ShowAsSeen && appearances.IsAsSeenOnly(label);

        var face = useWearerReal
            ? original?.Look == null || character->DrawData.CustomizeData.Data.SequenceEqual(original.Look)
            : saved.Look == null || character->DrawData.CustomizeData.Data.SequenceEqual(saved.Look);

        var glam = true;
        if (useWearerReal)
        {
            for (var slot = 0; slot < Outfit.Slots; slot++)
            {
                if (original?.Gear.Real[slot] is { } real
                    && character->DrawData.Equipment((DrawDataContainer.EquipmentSlot)slot).Value != real) glam = false;
            }
        }
        else if (saved.Glam != null)
        {
            for (var slot = 0; slot < Outfit.Slots; slot++)
            {
                if (character->DrawData.Equipment((DrawDataContainer.EquipmentSlot)slot).Value != saved.Glam[slot]) glam = false;
            }
        }

        return (label, face, glam);
    }

    public (byte[]? Look, ulong[] Gear, ulong[] Extras) Capture(nint address, bool asSeen)
    {
        if (!asSeen) return (RealLook(address), RealGear(address), RealExtras(address));

        var character = (Character*)address;
        return (Look.Capture(character), Outfit.Capture(character), Extras.Capture(character));
    }

    public ulong[] RealExtras(nint address)
    {
        var character = (Character*)address;
        var values = Extras.Capture(character);
        if (!originals.TryGetValue(character->ContentId, out var original)) return values;

        for (var i = 0; i < values.Length; i++)
        {
            if (original.Extras.Real[i] is { } real) values[i] = real;
        }

        return values;
    }

    private void Prune()
    {
        platesPending.RemoveWhere(id => !seen.Contains(id));

        List<ulong>? stale = null;
        foreach (var (id, original) in originals)
        {
            if (seen.Contains(id)) continue;

            if (config.LookAssignments.ContainsKey(original.Name)) continue;

            if (original.Extras.Real[Extras.WeaponsHidden] == 0) continue;

            stale ??= [];
            stale.Add(id);
        }

        if (stale == null) return;

        foreach (var id in stale) originals.Remove(id);
    }

    private void RestoreAll()
    {
        var wrote = 0;
        var restored = new HashSet<ulong>();

        void Restore(IGameObject player)
        {
            var character = (Character*)player.Address;
            if (!restored.Add(character->ContentId)) return;
            if (!originals.TryGetValue(character->ContentId, out var original)) return;

            try
            {
                character->SetName(original.Name);
                RestorePlayer(character, original, force: true);
                wrote++;
            }
            catch (Exception ex)
            {
                errors.Report(ex);
            }
        }

        foreach (var player in Plugin.Objects.PlayerObjects) Restore(player);

        if (Plugin.Objects.LocalPlayer is { } me) Restore(me);

        if (config.Diagnostics)
            Plugin.Log.Information($"Anon: unload restored {wrote} of {originals.Count} recorded names");

        originals.Clear();
        if (wrote > 0) Plugin.NamePlates.RequestRedraw();
    }

    private static void Redraw(Character* character)
    {
        character->DisableDraw();
        character->EnableDraw();
    }

    // Race- and gender-locked gear has no model for any other body, so on a new race or sex those slots
    // render as nothing; swap them for something the current look can wear.
    private static ulong[]? Wearable(Character* character, GearState state, ulong[]? wanted)
    {
        var customize = character->DrawData.CustomizeData;
        ulong[]? fitted = null;
        for (var slot = 0; slot < Outfit.Slots; slot++)
        {
            var shown = wanted?[slot] ?? Outfit.Real(character, state, slot);
            var fit = Wardrobe.Fit(slot, shown, customize.Race, customize.Sex);
            if (fit == shown) continue;

            if (fitted == null)
            {
                fitted = new ulong[Outfit.Slots];
                for (var i = 0; i < Outfit.Slots; i++) fitted[i] = wanted?[i] ?? Outfit.Real(character, state, i);
            }

            fitted[slot] = fit;
        }

        return fitted ?? wanted;
    }

    private int MyRace()
    {
        var player = Plugin.Objects.LocalPlayer;
        if (player == null) return 1;
        var character = (Character*)player.Address;
        if (originals.TryGetValue(character->ContentId, out var original) && original.Look != null) return original.Look[0];
        return character->DrawData.CustomizeData.Race;
    }

    private void LogCounts(int players, int masked, int unidentified)
    {
        if (Environment.TickCount64 < nextReport) return;

        nextReport = Environment.TickCount64 + ReportMilliseconds;
        if (config.Diagnostics)
        {
            if (Plugin.Objects.LocalPlayer is { } me)
            {
                var character = (Character*)me.Address;
                Plugin.Log.Information($"[probe] me object=\"{character->NameString}\" real=\"{aliases.MyName}\" record={originals.ContainsKey(character->ContentId)} active={aliases.Active} self={config.Self}");
            }
            Plugin.Log.Information(
                $"Anon: saw {players} players, masked {masked}, records {originals.Count}, {unidentified} without a content id; " +
                $"since last report restored {restored}, {looks} looks, {gear} gear");
        }

        restored = looks = gear = 0;
    }
}
