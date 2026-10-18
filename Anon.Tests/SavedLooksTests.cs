using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Anon.Tests;

public class SavedLooksTests
{
    private static byte[] Look(byte fill)
    {
        var look = new byte[SavedLooks.Size];
        look[0] = fill;
        return look;
    }

    private static Dictionary<string, string> Looks() => new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> Assignments() => new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void A_look_survives_being_stored_and_read_back()
    {
        var look = Look(7);
        look[25] = 200;

        Assert.True(SavedLooks.TryDecode(SavedLooks.Encode(look), out var read));

        Assert.Equal(look, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 at all!")]
    public void Unreadable_stored_text_is_rejected(string? stored)
    {
        Assert.False(SavedLooks.TryDecode(stored, out var look));
        Assert.Empty(look);
    }

    [Fact]
    public void A_look_of_the_wrong_length_is_rejected()
    {
        Assert.False(SavedLooks.TryDecode(Convert.ToBase64String(new byte[SavedLooks.Size - 1]), out _));
        Assert.False(SavedLooks.TryDecode(Convert.ToBase64String(new byte[SavedLooks.Size + 1]), out _));
    }

    [Fact]
    public void An_assigned_player_gets_their_look()
    {
        var looks = Looks();
        var assignments = Assignments();
        looks["look one"] = SavedLooks.Encode(Look(3));
        assignments["Gamma Tester"] = "look one";

        var look = SavedLooks.For(assignments, looks, "Gamma Tester");

        Assert.NotNull(look);
        Assert.Equal(3, look![0]);
    }

    [Fact]
    public void A_player_without_an_assignment_gets_nothing()
    {
        Assert.Null(SavedLooks.For(Assignments(), Looks(), "Nobody Here"));
        Assert.Null(SavedLooks.For(Assignments(), Looks(), string.Empty));
    }

    [Fact]
    public void An_assignment_pointing_at_a_missing_or_corrupt_look_is_ignored()
    {
        var assignments = Assignments();
        assignments["Gamma Tester"] = "gone";

        Assert.Null(SavedLooks.For(assignments, Looks(), "Gamma Tester"));

        var looks = Looks();
        looks["gone"] = "rubbish";
        Assert.Null(SavedLooks.For(assignments, looks, "Gamma Tester"));
    }

    [Fact]
    public void Renaming_moves_every_player_using_the_look()
    {
        var looks = Looks();
        var assignments = Assignments();
        looks["look one"] = SavedLooks.Encode(Look(1));
        assignments["A"] = "look one";
        assignments["B"] = "look one";
        assignments["C"] = "other";

        SavedLooks.Rename(looks, assignments, "look one", "look two");

        Assert.False(looks.ContainsKey("look one"));
        Assert.True(looks.ContainsKey("look two"));
        Assert.Equal("look two", assignments["A"]);
        Assert.Equal("look two", assignments["B"]);
        Assert.Equal("other", assignments["C"]);
        Assert.NotNull(SavedLooks.For(assignments, looks, "A"));
    }

    [Fact]
    public void Removing_a_look_also_clears_its_assignments()
    {
        var looks = Looks();
        var assignments = Assignments();
        looks["look one"] = SavedLooks.Encode(Look(1));
        looks["keep"] = SavedLooks.Encode(Look(2));
        assignments["A"] = "look one";
        assignments["B"] = "keep";

        SavedLooks.Remove(looks, assignments, "look one");

        Assert.False(looks.ContainsKey("look one"));
        Assert.True(looks.ContainsKey("keep"));
        Assert.False(assignments.ContainsKey("A"));
        Assert.Equal("keep", assignments["B"]);
    }

    [Fact]
    public void Two_captures_of_the_same_player_do_not_overwrite_each_other()
    {
        var looks = Looks();
        looks["Gamma Tester"] = SavedLooks.Encode(Look(1));

        Assert.Equal("Gamma Tester 2", SavedLooks.FreeLabel(looks, "Gamma Tester"));
        Assert.Equal("Look", SavedLooks.FreeLabel(Looks(), "   "));
    }

    [Fact]
    public void Used_by_lists_every_player_shown_with_a_look()
    {
        var assignments = Assignments();
        assignments["A"] = "look one";
        assignments["B"] = "other";
        assignments["C"] = "look one";

        Assert.Equal(["A", "C"], SavedLooks.UsedBy(assignments, "look one").OrderBy(n => n).ToArray());
    }

    [Fact]
    public void A_glamour_survives_being_stored_and_read_back()
    {
        var look = Look(9);
        var glam = Enumerable.Range(1, SavedLooks.GlamSlots).Select(slot => (ulong)(slot * 1111)).ToArray();

        Assert.True(SavedLooks.TryDecodeEntry(SavedLooks.Encode(look, glam), out var readLook, out var readGlam));

        Assert.Equal(look, readLook);
        Assert.NotNull(readGlam);
        Assert.Equal(glam, readGlam!);
    }

    [Fact]
    public void An_entry_saved_before_glamour_could_be_copied_still_reads()
    {
        Assert.True(SavedLooks.TryDecodeEntry(SavedLooks.Encode(Look(2)), out var look, out var glam));

        Assert.Equal(2, look[0]);
        Assert.Null(glam);
    }

    [Fact]
    public void Has_glam_reports_what_the_entry_holds()
    {
        var looks = Looks();
        looks["with"] = SavedLooks.Encode(Look(1), new ulong[SavedLooks.GlamSlots]);
        looks["without"] = SavedLooks.Encode(Look(1));

        Assert.True(SavedLooks.HasGlam(looks, "with"));
        Assert.False(SavedLooks.HasGlam(looks, "without"));
        Assert.False(SavedLooks.HasGlam(looks, "missing"));
    }

    [Fact]
    public void A_glamour_of_the_wrong_length_is_rejected()
    {
        var shortEntry = new byte[SavedLooks.Size + ((SavedLooks.GlamSlots - 1) * 8)];

        Assert.False(SavedLooks.TryDecodeEntry(Convert.ToBase64String(shortEntry), out _, out var glam));
        Assert.Null(glam);
    }

    [Fact]
    public void An_exact_copy_survives_being_stored_and_read_back()
    {
        var look = Look(7);
        var glam = Enumerable.Range(1, SavedLooks.GlamSlots).Select(slot => (ulong)(slot * 1111)).ToArray();
        var extras = Enumerable.Range(1, SavedLooks.ExtraValues).Select(i => (ulong)i * 0x0101_0101_0101UL).ToArray();
        extras[^1] = ulong.MaxValue;

        Assert.True(SavedLooks.TryDecodeEntry(SavedLooks.Encode(look, glam, extras), out var readLook, out var readGlam, out var readExtras));

        Assert.Equal(look, readLook);
        Assert.Equal(glam, readGlam!);
        Assert.NotNull(readExtras);
        Assert.Equal(extras, readExtras!);
    }

    [Fact]
    public void An_entry_saved_before_exact_copies_still_reads_without_extras()
    {
        var glam = new ulong[SavedLooks.GlamSlots];
        glam[0] = 42;

        Assert.True(SavedLooks.TryDecodeEntry(SavedLooks.Encode(Look(3), glam), out var look, out var readGlam, out var extras));

        Assert.Equal(3, look[0]);
        Assert.Equal(42UL, readGlam![0]);
        Assert.Null(extras);
    }

    [Fact]
    public void A_face_only_entry_has_neither_glamour_nor_extras()
    {
        Assert.True(SavedLooks.TryDecodeEntry(SavedLooks.Encode(Look(4)), out _, out var glam, out var extras));

        Assert.Null(glam);
        Assert.Null(extras);
    }

    [Fact]
    public void Has_extras_reports_what_the_entry_holds()
    {
        var looks = Looks();
        looks["exact"] = SavedLooks.Encode(Look(1), new ulong[SavedLooks.GlamSlots], new ulong[SavedLooks.ExtraValues]);
        looks["glam"] = SavedLooks.Encode(Look(1), new ulong[SavedLooks.GlamSlots]);
        looks["face"] = SavedLooks.Encode(Look(1));

        Assert.True(SavedLooks.HasExtras(looks, "exact"));
        Assert.True(SavedLooks.HasGlam(looks, "exact"));
        Assert.False(SavedLooks.HasExtras(looks, "glam"));
        Assert.False(SavedLooks.HasExtras(looks, "face"));
        Assert.False(SavedLooks.HasExtras(looks, "missing"));
    }

    [Fact]
    public void A_copy_remembers_whether_it_was_the_real_look_or_what_was_seen()
    {
        var looks = Looks();
        var glam = new ulong[SavedLooks.GlamSlots];
        var extras = new ulong[SavedLooks.ExtraValues];
        looks["seen"] = SavedLooks.Encode(Look(1), glam, extras, asSeen: true);
        looks["real"] = SavedLooks.Encode(Look(1), glam, extras);
        looks["glam"] = SavedLooks.Encode(Look(1), glam);

        Assert.True(SavedLooks.IsAsSeen(looks, "seen"));
        Assert.False(SavedLooks.IsAsSeen(looks, "real"));
        Assert.False(SavedLooks.IsAsSeen(looks, "glam"));
        Assert.False(SavedLooks.IsAsSeen(looks, "missing"));
        Assert.True(SavedLooks.HasExtras(looks, "seen"));
    }

    [Fact]
    public void An_as_seen_copy_survives_a_rename()
    {
        var looks = Looks();
        var assignments = Assignments();
        looks["look one"] = SavedLooks.Encode(Look(1), new ulong[SavedLooks.GlamSlots], new ulong[SavedLooks.ExtraValues], asSeen: true);

        SavedLooks.Rename(looks, assignments, "look one", "look two");

        Assert.True(SavedLooks.IsAsSeen(looks, "look two"));
    }

    [Fact]
    public void An_exact_copy_from_before_the_real_or_seen_choice_reads_as_real()
    {
        var full = SavedLooks.Size + (SavedLooks.GlamSlots * 8) + (SavedLooks.ExtraValues * 8);
        var looks = Looks();
        looks["old"] = Convert.ToBase64String(new byte[full]);

        Assert.True(SavedLooks.TryDecodeEntry(looks["old"], out _, out _, out var extras));
        Assert.NotNull(extras);
        Assert.False(SavedLooks.IsAsSeen(looks, "old"));
    }

    [Fact]
    public void A_copy_holds_both_versions_and_the_switch_picks_one()
    {
        var realGlam = Enumerable.Repeat(11UL, SavedLooks.GlamSlots).ToArray();
        var realExtras = Enumerable.Repeat(12UL, SavedLooks.ExtraValues).ToArray();
        var seenGlam = Enumerable.Repeat(21UL, SavedLooks.GlamSlots).ToArray();
        var seenExtras = Enumerable.Repeat(22UL, SavedLooks.ExtraValues).ToArray();
        var stored = SavedLooks.EncodeBoth(Look(1), realGlam, realExtras, Look(2), seenGlam, seenExtras);

        Assert.True(SavedLooks.TryDecodeEntry(stored, false, out var realLook, out var glam, out var extras));
        Assert.Equal(1, realLook[0]);
        Assert.Equal(realGlam, glam!);
        Assert.Equal(realExtras, extras!);

        Assert.True(SavedLooks.TryDecodeEntry(stored, true, out var seenLook, out glam, out extras));
        Assert.Equal(2, seenLook[0]);
        Assert.Equal(seenGlam, glam!);
        Assert.Equal(seenExtras, extras!);
    }

    [Fact]
    public void A_copy_with_one_version_shows_it_whichever_way_the_switch_is()
    {
        var stored = SavedLooks.Encode(Look(5), new ulong[SavedLooks.GlamSlots], new ulong[SavedLooks.ExtraValues], asSeen: true);

        Assert.True(SavedLooks.TryDecodeEntry(stored, false, out var real, out _, out _));
        Assert.True(SavedLooks.TryDecodeEntry(stored, true, out var seen, out _, out _));
        Assert.Equal(real, seen);
        Assert.Equal(5, real[0]);
    }

    [Fact]
    public void Shape_reports_which_parts_a_copy_has()
    {
        var glam = new ulong[SavedLooks.GlamSlots];
        var extras = new ulong[SavedLooks.ExtraValues];

        var both = SavedLooks.Describe(SavedLooks.EncodeBoth(Look(1), glam, extras, Look(2), glam, extras));
        Assert.True(both.HasValue);
        Assert.True(both.Value.Both);
        Assert.True(both.Value.Glam);
        Assert.True(both.Value.Extras);
        Assert.False(both.Value.AsSeen);

        var seen = SavedLooks.Describe(SavedLooks.Encode(Look(1), glam, extras, asSeen: true));
        Assert.True(seen.HasValue);
        Assert.True(seen.Value.AsSeen);
        Assert.False(seen.Value.Both);

        var real = SavedLooks.Describe(SavedLooks.Encode(Look(1), glam, extras));
        Assert.True(real.HasValue);
        Assert.False(real.Value.AsSeen);

        var face = SavedLooks.Describe(SavedLooks.Encode(Look(1)));
        Assert.True(face.HasValue);
        Assert.False(face.Value.Glam);
        Assert.False(face.Value.Extras);

        Assert.Null(SavedLooks.Describe("not base64 at all!"));
    }

    [Fact]
    public void Has_both_reports_two_version_copies_only()
    {
        var looks = Looks();
        var glam = new ulong[SavedLooks.GlamSlots];
        var extras = new ulong[SavedLooks.ExtraValues];
        looks["both"] = SavedLooks.EncodeBoth(Look(1), glam, extras, Look(2), glam, extras);
        looks["one"] = SavedLooks.Encode(Look(1), glam, extras);

        Assert.True(SavedLooks.HasBoth(looks, "both"));
        Assert.True(SavedLooks.HasExtras(looks, "both"));
        Assert.True(SavedLooks.HasGlam(looks, "both"));
        Assert.False(SavedLooks.IsAsSeen(looks, "both"));
        Assert.False(SavedLooks.HasBoth(looks, "one"));
        Assert.False(SavedLooks.HasBoth(looks, "missing"));
    }

    [Fact]
    public void A_two_version_copy_of_the_wrong_length_is_rejected()
    {
        var dual = 2 * (SavedLooks.Size + (SavedLooks.GlamSlots * 8) + (SavedLooks.ExtraValues * 8));

        foreach (var length in new[] { dual - 1, dual + 1 })
        {
            Assert.False(SavedLooks.TryDecodeEntry(Convert.ToBase64String(new byte[length]), true, out _, out var glam, out _));
            Assert.Null(glam);
        }
    }

    [Fact]
    public void An_exact_copy_of_the_wrong_length_is_rejected()
    {
        var full = SavedLooks.Size + (SavedLooks.GlamSlots * 8) + (SavedLooks.ExtraValues * 8);

        foreach (var length in new[] { full - 1, full + 2, full - 8 })
        {
            Assert.False(SavedLooks.TryDecodeEntry(Convert.ToBase64String(new byte[length]), out _, out _, out var extras));
            Assert.Null(extras);
        }
    }
}
