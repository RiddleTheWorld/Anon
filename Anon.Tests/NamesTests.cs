using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Anon.Tests;

public class NamesTests
{
    private const int MaxNameLength = 20;

    private static readonly Regex GameName = new(@"^[A-Z][a-z]+ [A-Z][a-z]+$");

    [Fact]
    public void Seed_is_fixed_across_sessions()
    {
        Assert.Equal(1022276249, Names.Seed("Alpha Tester", 4231));
    }

    [Fact]
    public void Fake_is_fixed_across_dotnet_versions()
    {
        Assert.Equal("Oscar Weber", Names.Fake("Alpha Tester", 4231));
    }

    [Fact]
    public void Fake_gives_the_same_name_for_the_same_player_and_seed()
    {
        Assert.Equal(Names.Fake("Alpha Tester", 4231), Names.Fake("Alpha Tester", 4231));
    }

    [Fact]
    public void Fake_changes_with_the_seed_and_the_player()
    {
        var bySeed = Enumerable.Range(0, 200).Select(seed => Names.Fake("Alpha Tester", seed)).Distinct().Count();
        var byPlayer = Enumerable.Range(0, 200).Select(n => Names.Fake($"Player {n}", 4231)).Distinct().Count();

        Assert.True(bySeed > 150, $"only {bySeed} distinct names over 200 seeds");
        Assert.True(byPlayer > 150, $"only {byPlayer} distinct names over 200 players");
    }

    [Fact]
    public void The_pool_of_names_is_large()
    {
        var random = new Random(99);
        var names = Enumerable.Range(0, 5000).Select(_ => Names.Create(random)).Distinct().Count();

        Assert.True(names > 4000, $"only {names} distinct names over 5000 draws");
    }

    [Fact]
    public void Every_name_is_two_capitalised_words_within_the_game_limit()
    {
        var random = new Random(1);
        for (var i = 0; i < 10000; i++)
        {
            var name = Names.Create(random);
            Assert.Matches(GameName, name);
            Assert.True(name.Length <= MaxNameLength, name);
        }
    }

    [Fact]
    public void A_short_name_is_not_replaced_inside_a_longer_word()
    {
        const string Text = "Alliance Raid with Allagan gear";

        Assert.Equal(Text, Names.ReplaceWholeWords(Text, "Al", "Zed Zedson"));
        Assert.False(Names.EveryMentionStandsAlone(Text, "Al"));
    }

    [Fact]
    public void A_name_next_to_a_word_that_contains_it_is_not_touched()
    {
        Assert.False(Names.EveryMentionStandsAlone("Al: Alliance", "Al"));
    }

    [Fact]
    public void A_standing_name_is_replaced_next_to_a_word_that_contains_it()
    {
        Assert.Equal("Zed: Alliance", Names.ReplaceWholeWords("Al: Alliance", "Al", "Zed"));
    }

    [Fact]
    public void Punctuation_still_counts_as_a_word_boundary()
    {
        Assert.Equal("Zed, hello.", Names.ReplaceWholeWords("Al, hello.", "Al", "Zed"));
        Assert.Equal("Zed's gear", Names.ReplaceWholeWords("Al's gear", "Al", "Zed"));
        Assert.Equal("Zed\nhi", Names.ReplaceWholeWords("Al\nhi", "Al", "Zed"));
        Assert.True(Names.EveryMentionStandsAlone("Al, hello. Al's gear", "Al"));
    }

    [Fact]
    public void A_name_at_either_end_of_the_text_is_replaced()
    {
        Assert.Equal("Zed hi", Names.ReplaceWholeWords("Al hi", "Al", "Zed"));
        Assert.Equal("hi Zed", Names.ReplaceWholeWords("hi Al", "Al", "Zed"));
    }

    [Fact]
    public void Text_without_the_name_is_returned_unchanged()    {
        Assert.Equal("nothing here", Names.ReplaceWholeWords("nothing here", "Al", "Zed"));
        Assert.True(Names.EveryMentionStandsAlone("nothing here", "Al"));
    }
}
