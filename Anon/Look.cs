using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;

namespace Anon;

public static unsafe class Look
{
    private static LookOptions? options;

    public static byte[] Create(Random random, LookSettings settings, int myRace)
    {
        options ??= new LookOptions(Plugin.Data.GetExcelSheet<CharaMakeType>(), Plugin.Data.GetExcelSheet<CharaMakeCustomize>());

        CustomizeData look = default;
        options.Create(random, settings.SameRaceAsMe ? myRace : 0, settings.AllRaces).CopyTo(look.Data);

        if (CustomizeData.MemberFunctionPointers.Normalize != null) look.Normalize(&look);
        return look.Data.ToArray();
    }

    public static byte[]? Capture(Character* character)
    {
        var data = character->DrawData.CustomizeData.Data;
        return data[0] == 0 ? null : data.ToArray();
    }

    public static byte[] Normalized(byte[] look)
    {
        if (look.Length != SavedLooks.Size) return look;

        CustomizeData data = default;
        look.CopyTo(data.Data);
        if (CustomizeData.MemberFunctionPointers.Normalize != null) data.Normalize(&data);
        return data.Data.ToArray();
    }
}
