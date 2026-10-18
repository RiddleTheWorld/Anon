using System;
using System.Collections.Generic;
using Lumina.Text;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using Xunit;

namespace Anon.Tests;

public class ChatNamesTests
{
    private const ushort Odin = 66;

    private static readonly Dictionary<string, string> Fakes = new()
    {
        ["Alpha Tester"] = "Fake Alpha",
        ["Beta Tester"] = "Fake Beta",
    };

    private static string? Masked(string name, string shown) => Fakes.GetValueOrDefault(name);

    private static string? Real(string name, string shown) => Fakes.ContainsValue(shown) ? name : null;

    private static void Player(SeStringBuilder builder, string linkName, string shown)
    {
        builder.BeginMacro(MacroCode.Link);
        builder.AppendUIntExpression((uint)LinkMacroPayloadType.Character);
        builder.AppendUIntExpression(0);
        builder.AppendUIntExpression(Odin);
        builder.AppendUIntExpression(0);
        builder.AppendStringExpression(linkName);
        builder.EndMacro();
        builder.Append(shown);
        End(builder);
    }

    private static void End(SeStringBuilder builder)
    {
        builder.BeginMacro(MacroCode.Link);
        builder.AppendUIntExpression((uint)LinkMacroPayloadType.Terminator);
        builder.AppendUIntExpression(0);
        builder.AppendUIntExpression(0);
        builder.AppendUIntExpression(0);
        builder.AppendStringExpression(string.Empty);
        builder.EndMacro();
    }

    private static byte[] Line(Action<SeStringBuilder> build)
    {
        var builder = new SeStringBuilder();
        build(builder);
        return builder.ToArray();
    }

    private static string Text(byte[] line) => new ReadOnlySeString(line).ExtractText();

    private static List<string> LinkNames(byte[] line)
    {
        var names = new List<string>();
        foreach (var payload in new ReadOnlySeStringSpan(line))
        {
            if (payload.MacroCode != MacroCode.Link) continue;
            if (!payload.TryGetExpression(out var kind, out _, out _, out _, out var name)) continue;
            if (!kind.TryGetUInt(out var type) || type != (uint)LinkMacroPayloadType.Character) continue;
            if (name.TryGetString(out var text)) names.Add(text.ExtractText());
        }

        return names;
    }

    [Fact]
    public void Masks_the_shown_name_of_a_player_link()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "Alpha Tester"); b.Append(": hello"); });

        var result = ChatNames.Rewrite(line, Masked);

        Assert.NotNull(result);
        Assert.Equal("Fake Alpha: hello", Text(result));
    }

    [Fact]
    public void Keeps_the_real_name_inside_the_link_so_tells_still_work()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "Alpha Tester"); b.Append(": hello"); });

        var result = ChatNames.Rewrite(line, Masked)!;

        Assert.Equal(["Alpha Tester"], LinkNames(result));
    }

    [Fact]
    public void Masks_every_chat_shape_because_each_one_wraps_the_name_in_a_link()
    {
        string[][] shapes =
        [
            ["", ": hi"],
            ["(", ") hi"],
            ["[1]<", "> hi"],
            ["[FC]<", "> hi"],
            ["", " >> hi"],
            ["", " shouts: hi"],
        ];

        foreach (var shape in shapes)
        {
            var line = Line(b => { b.Append(shape[0]); Player(b, "Alpha Tester", "Alpha Tester"); b.Append(shape[1]); });

            var result = ChatNames.Rewrite(line, Masked);

            Assert.NotNull(result);
            Assert.Equal($"{shape[0]}Fake Alpha{shape[1]}", Text(result));
        }
    }

    [Fact]
    public void Masks_abbreviated_names_because_the_link_carries_the_full_name()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "A. Tester"); b.Append(": hi"); });

        var result = ChatNames.Rewrite(line, Masked);

        Assert.Equal("Fake Alpha: hi", Text(result!));
    }

    [Fact]
    public void Masks_several_players_on_one_line()
    {
        var line = Line(b =>
        {
            Player(b, "Alpha Tester", "Alpha Tester");
            b.Append(" waves to ");
            Player(b, "Beta Tester", "Beta Tester");
            b.Append(".");
        });

        var result = ChatNames.Rewrite(line, Masked);

        Assert.Equal("Fake Alpha waves to Fake Beta.", Text(result!));
    }

    [Fact]
    public void Restores_the_real_name_when_masking_is_off()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "Fake Alpha"); b.Append(": hello"); });

        var result = ChatNames.Rewrite(line, Real);

        Assert.Equal("Alpha Tester: hello", Text(result!));
    }

    [Fact]
    public void Leaves_an_abbreviated_real_name_alone_when_masking_is_off()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "A. Tester"); b.Append(": hi"); });

        Assert.Null(ChatNames.Rewrite(line, Real));
    }

    [Fact]
    public void Passes_the_shown_text_so_the_caller_can_tell_an_alias_from_a_real_name()
    {
        var line = Line(b => { Player(b, "Alpha Tester", "A. Tester"); b.Append(": hi"); });
        var shown = new List<string>();

        ChatNames.Rewrite(line, (name, text) => { shown.Add(text); return null; });

        Assert.Equal(["A. Tester"], shown);
    }

    [Fact]
    public void Returns_null_when_nothing_changes()
    {
        var line = Line(b => { Player(b, "Someone Else", "Someone Else"); b.Append(": hello"); });

        Assert.Null(ChatNames.Rewrite(line, Masked));
    }

    [Fact]
    public void Leaves_text_outside_player_links_alone()
    {
        var line = Line(b => b.Append("Alpha Tester gains the effect of True North."));

        Assert.Null(ChatNames.Rewrite(line, Masked));
    }

    [Fact]
    public void Leaves_item_links_alone()
    {
        var line = Line(b =>
        {
            b.BeginMacro(MacroCode.Link);
            b.AppendUIntExpression((uint)LinkMacroPayloadType.Item);
            b.AppendUIntExpression(1);
            b.AppendUIntExpression(0);
            b.AppendUIntExpression(0);
            b.AppendStringExpression("Alpha Tester");
            b.EndMacro();
            b.Append("Alpha Tester");
            End(b);
        });

        Assert.Null(ChatNames.Rewrite(line, Masked));
    }

    [Fact]
    public void Replaces_a_name_split_across_text_pieces_once()
    {
        var line = Line(b =>
        {
            b.BeginMacro(MacroCode.Link);
            b.AppendUIntExpression((uint)LinkMacroPayloadType.Character);
            b.AppendUIntExpression(0);
            b.AppendUIntExpression(Odin);
            b.AppendUIntExpression(0);
            b.AppendStringExpression("Alpha Tester");
            b.EndMacro();
            b.Append("Alpha ");
            b.BeginMacro(MacroCode.Italic);
            b.AppendUIntExpression(0);
            b.EndMacro();
            b.Append("Tester");
            End(b);
            b.Append(": hi");
        });

        var result = ChatNames.Rewrite(line, Masked);

        Assert.Equal("Fake Alpha: hi", Text(result!));
    }

    [Fact]
    public void Reports_every_player_it_finds()
    {
        var line = Line(b =>
        {
            Player(b, "Alpha Tester", "Alpha Tester");
            b.Append(" and ");
            Player(b, "Someone Else", "Someone Else");
        });
        var found = new List<string>();

        ChatNames.Rewrite(line, (name, shown) => { found.Add(name); return Masked(name, shown); });

        Assert.Equal(["Alpha Tester", "Someone Else"], found);
    }
}
