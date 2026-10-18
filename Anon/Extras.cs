using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Anon;

public static unsafe class Extras
{
    public const int Glasses0 = 0;
    public const int Glasses1 = 1;
    public const int HatHidden = 2;
    public const int VisorToggled = 3;
    public const int EarsHidden = 4;
    public const int WeaponsHidden = ExtrasPlan.WeaponsHidden;
    public const int Crest = 6;
    public const int MainHand = ExtrasPlan.MainHand;
    public const int OffHand = ExtrasPlan.OffHand;

    private const string CrestChangeSignature = "E8 ?? ?? ?? ?? 4C 8D A6 ?? ?? ?? ?? 41 BF";

    private static delegate* unmanaged<DrawDataContainer*, byte, void> crestChange;
    private static bool crestResolved;

    public static ulong[] Capture(Character* character)
    {
        var data = &character->DrawData;
        var values = new ulong[SavedLooks.ExtraValues];
        for (var i = 0; i < values.Length; i++) values[i] = Read(data, i);

        values[WeaponsHidden] = 0;
        return values;
    }

    public static bool Apply(Character* character, ExtrasState state, ulong[]? wanted, bool hideWeapons = false)
    {
        var data = &character->DrawData;
        var ready = character->IsReadyToDraw();
        var changed = false;

        for (var i = 0; i < SavedLooks.ExtraValues; i++)
        {
            var current = Read(data, i);
            var target = ExtrasPlan.Target(state, i, wanted, Read(data, MainHand), hideWeapons);
            if (ExtrasPlan.Next(state, i, current, target) is not { } value) continue;

            Write(data, i, value, ready);
            ExtrasPlan.Wrote(state, i, Read(data, i));
            changed = true;
        }

        return changed;
    }

    private static ulong Read(DrawDataContainer* data, int index) => index switch
    {
        Glasses0 => data->GlassesIds[0],
        Glasses1 => data->GlassesIds[1],
        HatHidden => data->IsHatHidden ? 1UL : 0UL,
        VisorToggled => data->IsVisorToggled ? 1UL : 0UL,
        EarsHidden => data->VieraEarsHidden ? 1UL : 0UL,
        WeaponsHidden => data->IsWeaponHidden ? 1UL : 0UL,
        Crest => data->FreeCompanyCrestBitfield,
        MainHand => data->Weapon(DrawDataContainer.WeaponSlot.MainHand).ModelId.Value,
        OffHand => data->Weapon(DrawDataContainer.WeaponSlot.OffHand).ModelId.Value,
        _ => 0,
    };

    private static void Write(DrawDataContainer* data, int index, ulong value, bool ready)
    {
        var on = value != 0;
        switch (index)
        {
            case Glasses0 or Glasses1:
                if (ready && DrawDataContainer.MemberFunctionPointers.SetGlasses != null) data->SetGlasses(index - Glasses0, (ushort)value);
                else data->GlassesIds[index - Glasses0] = (ushort)value;
                break;
            case HatHidden:
                if (ready && DrawDataContainer.MemberFunctionPointers.HideHeadgear != null) data->HideHeadgear(0, on);
                else data->IsHatHidden = on;
                break;
            case VisorToggled:
                if (ready && DrawDataContainer.MemberFunctionPointers.SetVisor != null) data->SetVisor(on);
                else data->IsVisorToggled = on;
                break;
            case EarsHidden:
                if (ready && DrawDataContainer.MemberFunctionPointers.HideVieraEars != null) data->HideVieraEars(on);
                else data->VieraEarsHidden = on;
                break;
            case WeaponsHidden:
                if (ready && DrawDataContainer.MemberFunctionPointers.HideWeapons != null) data->HideWeapons(on);
                else data->IsWeaponHidden = on;
                break;
            case Crest:
                var change = ready ? CrestChange() : null;
                if (change != null) change(data, (byte)value);
                else data->FreeCompanyCrestBitfield = (byte)value;
                break;
            case MainHand or OffHand:
                var slot = index == MainHand ? DrawDataContainer.WeaponSlot.MainHand : DrawDataContainer.WeaponSlot.OffHand;
                if (ready && DrawDataContainer.MemberFunctionPointers.LoadWeapon != null)
                    data->LoadWeapon(slot, new WeaponModelId { Value = value }, 1, 0, 0, 0, false);
                else data->Weapon(slot).ModelId.Value = value;
                break;
        }
    }

    private static delegate* unmanaged<DrawDataContainer*, byte, void> CrestChange()
    {
        if (crestResolved) return crestChange;

        crestResolved = true;
        try
        {
            if (Plugin.SigScanner.TryScanText(CrestChangeSignature, out var address))
                crestChange = (delegate* unmanaged<DrawDataContainer*, byte, void>)address;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Anon: could not find the free company crest function; crests apply on the next redraw");
        }

        return crestChange;
    }
}
