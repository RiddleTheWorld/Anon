using System;
using System.IO;
using System.Linq;
using Lumina;
using Lumina.Excel.Sheets;
using Xunit;

namespace Anon.Tests;

public class LookOptionsTests
{
    private const string SqPack = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
    private const int Samples = 20000;

    private const int Race = 0, Tribe = 4, Face = 5, Hairstyle = 6, Eyebrows = 14, Jaw = 18, Mouth = 19, Fur = 20, Tail = 22;
    private const int Roegadyn = 5, Miqote = 4, Hrothgar = 7;

    private static readonly Lazy<LookOptions?> Options = new(() =>
    {
        if (!Directory.Exists(SqPack)) return null;

        var data = new GameData(SqPack, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
        return new LookOptions(data.GetExcelSheet<CharaMakeType>()!, data.GetExcelSheet<CharaMakeCustomize>()!);
    });

    private static byte[][] Looks(int race = 0, bool allRaces = true)
    {
        if (Options.Value is not { } options)
            throw new InvalidOperationException($"Game data not found at {SqPack}");

        var random = new Random(1234);
        return Enumerable.Range(0, Samples).Select(_ => options.Create(random, race, allRaces)).ToArray();
    }

    [Fact]
    public void Game_data_is_available() => Assert.NotNull(Options.Value);

    [Fact]
    public void Every_race_clan_and_gender_is_available()
    {
        if (Options.Value is not { } options) return;
        Assert.Equal(32, options.ClanCount);
    }

    [Fact]
    public void Hrothgar_never_get_a_hairstyle_that_does_not_exist_for_them()
    {
        foreach (var look in Looks(Hrothgar))
        {
            Assert.Equal(Hrothgar, look[Race]);
            Assert.True(look[Hairstyle] >= 9, $"hairstyle {look[Hairstyle]}");
        }
    }

    [Fact]
    public void Hrothgar_hairstyles_match_their_face()
    {
        foreach (var look in Looks(Hrothgar))
        {
            if (look[Hairstyle] > 24) continue;

            var face = (look[Hairstyle] - 9) % 8 / 2 + 1;
            Assert.Equal(face, look[Face]);
        }
    }

    [Fact]
    public void Hrothgar_get_a_fur_pattern_and_short_fangs()
    {
        foreach (var look in Looks(Hrothgar))
        {
            Assert.InRange(look[Fur], 1, 5);
            Assert.InRange(look[Mouth], 0, 2);
        }
    }

    [Fact]
    public void Other_races_keep_lip_colour_at_zero()
    {
        foreach (var look in Looks())
        {
            if (look[Race] != Hrothgar) Assert.Equal(0, look[Fur]);
        }
    }

    [Fact]
    public void Miqote_use_all_eight_tails_and_nothing_more()
    {
        if (Options.Value == null) return;

        var tails = Looks(Miqote).Select(look => look[Tail]).Distinct().OrderBy(tail => tail).ToArray();
        Assert.Equal(Enumerable.Range(1, 8).Select(tail => (byte)tail), tails);
    }

    [Fact]
    public void Roegadyn_only_get_their_three_jaws()
    {
        foreach (var look in Looks(Roegadyn)) Assert.InRange(look[Jaw], 0, 2);
    }

    [Fact]
    public void Eyebrows_stay_inside_every_clans_range()
    {
        foreach (var look in Looks()) Assert.InRange(look[Eyebrows], 0, 5);
    }

    [Fact]
    public void Tails_and_ears_start_at_one_where_the_race_has_them()
    {
        foreach (var look in Looks())
        {
            if (look[Race] is 1 or 5) Assert.Equal(0, look[Tail]);
            else Assert.True(look[Tail] >= 1, $"race {look[Race]} tail {look[Tail]}");
        }
    }

    [Fact]
    public void Core_races_only_unless_all_races_are_allowed()
    {
        foreach (var look in Looks(allRaces: false)) Assert.InRange(look[Race], 1, 6);
    }

    [Fact]
    public void Clan_matches_race()
    {
        foreach (var look in Looks()) Assert.True(look[Tribe] == look[Race] * 2 || look[Tribe] == look[Race] * 2 - 1);
    }

    [Fact]
    public void Every_generated_look_has_a_face_and_hairstyle()
    {
        foreach (var look in Looks())
        {
            Assert.True(look[Face] > 0, $"race {look[Race]} tribe {look[Tribe]} has face {look[Face]}");
            Assert.True(look[Hairstyle] > 0, $"race {look[Race]} tribe {look[Tribe]} has hairstyle {look[Hairstyle]}");
        }
    }
}
