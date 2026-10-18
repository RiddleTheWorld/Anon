using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Anon;

public sealed class GearState
{
    public readonly ulong?[] Real = new ulong?[Outfit.Slots];
    public readonly ulong?[] Applied = new ulong?[Outfit.Slots];
}

public static unsafe class Outfit
{
    public const int Slots = 10;

    public static bool Apply(Character* character, GearState state, ulong[]? wanted)
    {
        var changed = false;
        for (var slot = 0; slot < Slots; slot++)
        {
            var equipmentSlot = (DrawDataContainer.EquipmentSlot)slot;
            var current = character->DrawData.Equipment(equipmentSlot).Value;

            if (wanted == null)
            {
                if (state.Real[slot] is not { } real) continue;

                state.Real[slot] = null;
                state.Applied[slot] = null;
                if (current == real) continue;

                Load(character, equipmentSlot, real);
                changed = true;
                continue;
            }

            if (current == wanted[slot]) continue;
            if (state.Applied[slot] != current) state.Real[slot] = current;

            Load(character, equipmentSlot, wanted[slot]);
            state.Applied[slot] = wanted[slot];
            changed = true;
        }

        return changed;
    }

    public static ulong Real(Character* character, GearState state, int slot)
    {
        var current = character->DrawData.Equipment((DrawDataContainer.EquipmentSlot)slot).Value;
        return state.Real[slot] is { } real && state.Applied[slot] == current ? real : current;
    }

    private static void Load(Character* character, DrawDataContainer.EquipmentSlot slot, ulong value)
    {
        var id = new EquipmentModelId { Value = value };
        character->DrawData.LoadEquipment(slot, &id, true);
    }

    public static ulong[] Capture(Character* character)
    {
        var glam = new ulong[Slots];
        for (var slot = 0; slot < Slots; slot++)
        {
            glam[slot] = character->DrawData.Equipment((DrawDataContainer.EquipmentSlot)slot).Value;
        }

        return glam;
    }
}
