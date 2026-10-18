using Xunit;

namespace Anon.Tests;

public class ExtrasPlanTests
{
    private const int Glasses = 0;
    private const int Hat = 2;

    private static ulong Weapon(ushort id, ushort type) => id | ((ulong)type << 16);

    private static ulong? Step(ExtrasState state, int index, ref ulong current, ulong? target, bool gameAccepts = true)
    {
        var value = ExtrasPlan.Next(state, index, current, target);
        if (value is not { } written) return null;

        if (gameAccepts) current = written;
        ExtrasPlan.Wrote(state, index, current);
        return written;
    }

    [Fact]
    public void Applying_writes_the_wanted_value_and_remembers_the_real_one()
    {
        var state = new ExtrasState();
        ulong glasses = 5;

        Assert.Equal(9UL, Step(state, Glasses, ref glasses, 9));
        Assert.Equal(9UL, glasses);
        Assert.Equal(5UL, state.Real[Glasses]);
        Assert.Null(Step(state, Glasses, ref glasses, 9));
    }

    [Fact]
    public void A_value_the_game_refuses_is_not_sent_again_every_frame()
    {
        var state = new ExtrasState();
        ulong hat = 0;

        Assert.Equal(1UL, Step(state, Hat, ref hat, 1, gameAccepts: false));
        Assert.Null(Step(state, Hat, ref hat, 1, gameAccepts: false));
        Assert.Null(Step(state, Hat, ref hat, 1, gameAccepts: false));
    }

    [Fact]
    public void A_change_the_player_makes_becomes_their_new_real_value()
    {
        var state = new ExtrasState();
        ulong glasses = 5;
        Step(state, Glasses, ref glasses, 9);

        glasses = 7;
        Assert.Equal(9UL, Step(state, Glasses, ref glasses, 9));
        Assert.Equal(7UL, state.Real[Glasses]);

        Assert.Equal(7UL, Step(state, Glasses, ref glasses, null));
        Assert.Equal(7UL, glasses);
    }

    [Fact]
    public void Removing_the_look_puts_the_real_value_back_once()
    {
        var state = new ExtrasState();
        ulong glasses = 5;
        Step(state, Glasses, ref glasses, 9);

        Assert.Equal(5UL, Step(state, Glasses, ref glasses, null));
        Assert.Equal(5UL, glasses);
        Assert.Null(state.Real[Glasses]);
        Assert.Null(Step(state, Glasses, ref glasses, null));
    }

    [Fact]
    public void Nothing_is_restored_for_a_value_that_was_never_changed()
    {
        var state = new ExtrasState();
        ulong glasses = 5;

        Assert.Null(Step(state, Glasses, ref glasses, null));
        Assert.Null(Step(state, Glasses, ref glasses, 5));
        Assert.Null(Step(state, Glasses, ref glasses, null));
        Assert.Equal(5UL, glasses);
    }

    [Fact]
    public void Switching_to_another_saved_look_keeps_the_original_real_value()
    {
        var state = new ExtrasState();
        ulong glasses = 5;
        Step(state, Glasses, ref glasses, 9);

        Assert.Equal(3UL, Step(state, Glasses, ref glasses, 3));
        Assert.Equal(5UL, state.Real[Glasses]);

        Assert.Equal(5UL, Step(state, Glasses, ref glasses, null));
    }

    [Fact]
    public void Weapons_of_the_same_type_are_applied()
    {
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.MainHand] = Weapon(1645, 1);
        wanted[ExtrasPlan.OffHand] = Weapon(1695, 1);

        var state = new ExtrasState();
        Assert.Equal(Weapon(1645, 1), ExtrasPlan.Target(state, ExtrasPlan.MainHand, wanted, currentMainHand: Weapon(1601, 1)));
        Assert.Equal(Weapon(1695, 1), ExtrasPlan.Target(state, ExtrasPlan.OffHand, wanted, currentMainHand: Weapon(1601, 1)));
    }

    [Fact]
    public void Weapons_of_another_type_are_left_alone()
    {
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.MainHand] = Weapon(201, 2);
        wanted[ExtrasPlan.OffHand] = Weapon(101, 2);
        wanted[Glasses] = 9;

        var state = new ExtrasState();
        Assert.Null(ExtrasPlan.Target(state, ExtrasPlan.MainHand, wanted, currentMainHand: Weapon(1601, 1)));
        Assert.Null(ExtrasPlan.Target(state, ExtrasPlan.OffHand, wanted, currentMainHand: Weapon(1601, 1)));
        Assert.Equal(9UL, ExtrasPlan.Target(state, Glasses, wanted, currentMainHand: Weapon(1601, 1)));
    }

    [Fact]
    public void The_weapon_check_uses_the_real_weapon_not_one_already_applied()
    {
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.MainHand] = Weapon(1645, 1);

        var state = new ExtrasState();
        state.Real[ExtrasPlan.MainHand] = Weapon(1601, 1);

        Assert.Equal(Weapon(1645, 1), ExtrasPlan.Target(state, ExtrasPlan.MainHand, wanted, currentMainHand: Weapon(201, 2)));
    }

    [Fact]
    public void No_saved_look_means_no_target()
    {
        Assert.Null(ExtrasPlan.Target(new ExtrasState(), Glasses, null, currentMainHand: Weapon(1601, 1)));
    }

    [Fact]
    public void The_type_field_decides_not_the_model_id_range()
    {
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.MainHand] = Weapon(1601, 1);

        var state = new ExtrasState();
        Assert.Equal(Weapon(1601, 1), ExtrasPlan.Target(state, ExtrasPlan.MainHand, wanted, currentMainHand: Weapon(417, 1)));
        Assert.Null(ExtrasPlan.Target(state, ExtrasPlan.MainHand, wanted, currentMainHand: Weapon(417, 2)));
    }

    [Fact]
    public void A_saved_hidden_weapon_does_not_hide_the_weapon()
    {
        var state = new ExtrasState();
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.WeaponsHidden] = 1;

        Assert.Equal(0UL, ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, wanted, currentMainHand: 0));
        Assert.Equal(1UL, ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, wanted, currentMainHand: 0, hideWeapons: true));
    }

    [Fact]
    public void A_look_without_extras_leaves_the_weapon_setting_alone()
    {
        Assert.Null(ExtrasPlan.Target(new ExtrasState(), ExtrasPlan.WeaponsHidden, null, currentMainHand: 0));
    }

    [Fact]
    public void Hiding_weapons_overrides_the_saved_value_and_works_without_one()
    {
        var state = new ExtrasState();
        var wanted = new ulong[SavedLooks.ExtraValues];
        wanted[ExtrasPlan.WeaponsHidden] = 0;

        Assert.Equal(1UL, ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, wanted, currentMainHand: Weapon(1601, 1), hideWeapons: true));
        Assert.Equal(1UL, ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, null, currentMainHand: Weapon(1601, 1), hideWeapons: true));
    }

    [Fact]
    public void Hiding_weapons_can_be_turned_off_again()
    {
        var state = new ExtrasState();
        ulong hidden = 0;

        var hide = ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, null, currentMainHand: 0, hideWeapons: true);
        Assert.Equal(1UL, Step(state, ExtrasPlan.WeaponsHidden, ref hidden, hide));
        Assert.Equal(1UL, hidden);
        Assert.Equal(0UL, state.Real[ExtrasPlan.WeaponsHidden]);

        var show = ExtrasPlan.Target(state, ExtrasPlan.WeaponsHidden, null, currentMainHand: 0);
        Assert.Null(show);
        Assert.Equal(0UL, Step(state, ExtrasPlan.WeaponsHidden, ref hidden, show));
        Assert.Equal(0UL, hidden);
        Assert.Null(state.Real[ExtrasPlan.WeaponsHidden]);
    }
}
