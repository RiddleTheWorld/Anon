using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Anon;

public sealed class LookOptions
{
    private const int Size = 26;

    private const int Race = 0, Sex = 1, BodyType = 2, Height = 3, Tribe = 4, Face = 5, Hairstyle = 6;
    private const int SkinColor = 8, EyeColorRight = 9, HairColor = 10, Eyebrows = 14, EyeColorLeft = 15;
    private const int EyeShape = 16, Nose = 17, Jaw = 18, Mouth = 19, FurPattern = 20, MuscleMass = 21;
    private const int TailShape = 22, BustSize = 23;

    private const int Hrothgar = 7;
    private const int FirstNewRace = 7;
    private const int HrothgarFaceOffset = 4;
    private const int PercentRange = 101;
    private const int ColorRange = 48;

    private readonly List<Clan> clans = [];

    private sealed record Clan(
        byte Race, byte Tribe, byte Sex, int Faces, (byte Style, byte Face)[] Hairstyles, int Tails, int Furs,
        int Jaws, int Eyebrows, int Noses, int Mouths, int EyeShapes);

    public LookOptions(ExcelSheet<CharaMakeType> types, ExcelSheet<CharaMakeCustomize> customize)
    {
        foreach (var row in types)
        {
            if (row.Race.RowId == 0) continue;

            clans.Add(new Clan(
                (byte)row.Race.RowId, (byte)row.Tribe.RowId, (byte)row.Gender,
                Count(row, Face),
                Styles(row, customize),
                Count(row, TailShape),
                row.Race.RowId == Hrothgar ? Count(row, FurPattern) : 0,
                Count(row, Jaw), Count(row, Eyebrows), Count(row, Nose), Count(row, Mouth), Count(row, EyeShape)));
        }
    }

    public int ClanCount => clans.Count;

    public byte[] Create(Random random, int race, bool allRaces)
    {
        var pool = clans.Where(clan => race != 0 ? clan.Race == race : allRaces || clan.Race < FirstNewRace).ToList();
        if (pool.Count == 0) pool = clans;

        var clan = pool[random.Next(pool.Count)];
        var look = new byte[Size];

        look[Race] = clan.Race;
        look[Tribe] = clan.Tribe;
        look[Sex] = clan.Sex;
        look[BodyType] = 1;
        look[Height] = (byte)random.Next(PercentRange);
        look[MuscleMass] = (byte)random.Next(PercentRange);
        look[BustSize] = (byte)random.Next(PercentRange);

        look[Face] = Position(random, clan.Faces);
        var styles = clan.Hairstyles.Where(style => style.Face == 0 || style.Face == look[Face] + HrothgarFaceOffset).ToArray();
        look[Hairstyle] = styles.Length > 0 ? styles[random.Next(styles.Length)].Style : (byte)0;
        look[TailShape] = Position(random, clan.Tails);
        look[FurPattern] = Position(random, clan.Furs);

        look[Jaw] = Index(random, clan.Jaws);
        look[Eyebrows] = Index(random, clan.Eyebrows);
        look[Nose] = Index(random, clan.Noses);
        look[Mouth] = Index(random, clan.Mouths);
        look[EyeShape] = Index(random, clan.EyeShapes);

        look[SkinColor] = (byte)random.Next(ColorRange);
        look[HairColor] = (byte)random.Next(ColorRange);
        look[EyeColorRight] = look[EyeColorLeft] = (byte)random.Next(ColorRange);

        return look;
    }

    private static byte Position(Random random, int count) => count == 0 ? (byte)0 : (byte)(random.Next(count) + 1);

    private static byte Index(Random random, int count) => count == 0 ? (byte)0 : (byte)random.Next(count);

    private static CharaMakeType.CharaMakeStructStruct? Menu(CharaMakeType row, int index)
    {
        foreach (var menu in row.CharaMakeStruct)
        {
            if (menu.Customize == index && menu.SubMenuNum > 0) return menu;
        }

        return null;
    }

    private static int Count(CharaMakeType row, int index) => Menu(row, index)?.SubMenuNum ?? 0;

    private static (byte Style, byte Face)[] Styles(CharaMakeType row, ExcelSheet<CharaMakeCustomize> customize)
    {
        if (Menu(row, Hairstyle) is not { } menu) return [];

        return menu.SubMenuParam.Take(menu.SubMenuNum)
            .Select(param => customize.TryGetRow(param, out var entry) ? (entry.FeatureID, entry.Unknown0) : ((byte)0, (byte)0))
            .Where(style => style.Item1 != 0)
            .ToArray();
    }
}
