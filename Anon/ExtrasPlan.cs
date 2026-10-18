namespace Anon;

public sealed class ExtrasState
{
    public readonly ulong?[] Real = new ulong?[SavedLooks.ExtraValues];
    public readonly ulong?[] Applied = new ulong?[SavedLooks.ExtraValues];
    public readonly ulong?[] Left = new ulong?[SavedLooks.ExtraValues];
}

public static class ExtrasPlan
{
    public const int WeaponsHidden = 5;
    public const int MainHand = 7;
    public const int OffHand = 8;

    public static ulong? Target(ExtrasState state, int index, ulong[]? wanted, ulong currentMainHand, bool hideWeapons = false)
    {
        if (index == WeaponsHidden) return hideWeapons ? 1 : (wanted == null ? null : 0);

        if (wanted == null) return null;
        if (index is not (MainHand or OffHand)) return wanted[index];

        return SameWeaponType(state.Real[MainHand] ?? currentMainHand, wanted[MainHand]) ? wanted[index] : null;
    }

    public static ulong? Next(ExtrasState state, int index, ulong current, ulong? target)
    {
        if (target is not { } want)
        {
            if (state.Real[index] is not { } real) return null;

            state.Real[index] = null;
            state.Applied[index] = null;
            state.Left[index] = null;
            return current == real ? null : real;
        }

        if (current == want) return null;
        if (state.Applied[index] == want && state.Left[index] == current) return null;
        if (state.Applied[index] == null || state.Left[index] != current) state.Real[index] = current;

        state.Applied[index] = want;
        return want;
    }

    public static void Wrote(ExtrasState state, int index, ulong after)
    {
        if (state.Applied[index] != null) state.Left[index] = after;
    }

    public static bool SameWeaponType(ulong a, ulong b) => (ushort)(a >> 16) == (ushort)(b >> 16);
}
