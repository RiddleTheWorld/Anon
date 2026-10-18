using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;

namespace Anon;

public static class Wardrobe
{
    private const ulong ModelMask = 0xFFFFFF;
    private const int ArmorSlots = 5;

    // The racial starter set of each race and sex, indexed (race - 1) * 2 + sex, as model id | variant << 16.
    // Same table Glamourer uses: these sets only exist for their own body, in all four armor slots.
    private static readonly ulong[] RacialSets =
    [
        0x020054, 0x020055, 0x020056, 0x020057, 0x02005C, 0x02005D, 0x020058, 0x020059,
        0x02005A, 0x02005B, 0x020101, 0x020102, 0x010255, 0x01033D, 0x0102E8, 0x010245,
    ];

    private static volatile ulong[][]? pools;
    private static volatile Dictionary<(int Slot, ulong Model), uint>? locked;

    public static void Load() => Task.Run(Build);

    public static ulong[]? Pick(Random random) =>
        pools?.Select(pool => pool.Length == 0 ? 0 : pool[random.Next(pool.Length)]).ToArray();

    public static ulong Fit(int slot, ulong value, int race, int sex)
    {
        var model = value & ModelMask;
        if (model == 0 || race < 1 || sex is < 0 or > 1) return value;

        var body = (race - 1) * 2 + sex;
        if (slot < ArmorSlots && Array.IndexOf(RacialSets, model) >= 0)
            return body < RacialSets.Length ? RacialSets[body] | (value & ~ModelMask) : 0;

        if (locked is not { } map || !map.TryGetValue((slot, model), out var allowed)) return value;
        return body < 32 && (allowed & (1u << body)) != 0 ? value : 0;
    }

    private static bool FitsEveryone(EquipRaceCategory race) =>
        race.Hyur && race.Elezen && race.Lalafell && race.Miqote && race.Roegadyn && race.AuRa && race.Hrothgar && race.Viera && race.Male && race.Female;

    private static uint Bodies(EquipRaceCategory race)
    {
        bool[] races = [race.Hyur, race.Elezen, race.Lalafell, race.Miqote, race.Roegadyn, race.AuRa, race.Hrothgar, race.Viera];
        var mask = 0u;
        for (var i = 0; i < races.Length; i++)
        {
            if (!races[i]) continue;
            if (race.Male) mask |= 1u << (i * 2);
            if (race.Female) mask |= 1u << (i * 2 + 1);
        }

        return mask;
    }

    private static void Build()
    {
        try
        {
            var found = Enumerable.Range(0, Outfit.Slots).Select(_ => new List<ulong>()).ToArray();
            var restricted = new Dictionary<(int Slot, ulong Model), uint>();
            var open = new HashSet<(int Slot, ulong Model)>();
            foreach (var item in Plugin.Data.GetExcelSheet<Item>())
            {
                if (item.ModelMain == 0 || !item.EquipSlotCategory.IsValid || !item.EquipRestriction.IsValid) continue;

                var category = item.EquipSlotCategory.Value;
                sbyte[] slots = [category.Head, category.Body, category.Gloves, category.Legs, category.Feet, category.Ears, category.Neck, category.Wrists, category.FingerR, category.FingerL];
                var model = (item.ModelMain & 0xFFFF) | (((item.ModelMain >> 16) & 0xFF) << 16);
                var everyone = FitsEveryone(item.EquipRestriction.Value);
                var bodies = everyone ? 0 : Bodies(item.EquipRestriction.Value);

                // Scion Thaumaturge's Moccasins and Scion Chronocler's Ringbands are flagged but render on everyone.
                var flagged = !everyone && bodies != 0 && item.RowId is not (13699 or 13700);
                for (var slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] != 1) continue;

                    if (everyone) open.Add((slot, model));
                    else if (flagged) restricted[(slot, model)] = restricted.GetValueOrDefault((slot, model)) | bodies;
                }

                if (item.LevelEquip == 0 || item.Name.IsEmpty || !everyone || slots.Contains((sbyte)-1)) continue;

                for (var slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] == 1) found[slot].Add(model);
                }
            }

            foreach (var key in open) restricted.Remove(key);

            pools = found.Select(models => models.Distinct().ToArray()).ToArray();
            locked = restricted;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to build the outfit list");
        }
    }
}
