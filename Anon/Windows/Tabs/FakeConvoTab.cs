using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Anon.Windows.Tabs;

public sealed class FakeConvoTab(Configuration config) : Tab("Fake Convo", config)
{
    private static readonly (string Label, byte Kind)[] Channels =
    [
        ("Say", 10),
        ("Yell", 30),
        ("Shout", 11),
        ("Tell (outgoing)", 12),
        ("Tell (incoming)", 13),
        ("Party", 14),
        ("Alliance", 15),
        ("Linkshell 1", 16),
        ("Linkshell 2", 17),
        ("Linkshell 3", 18),
        ("Linkshell 4", 19),
        ("Linkshell 5", 20),
        ("Linkshell 6", 21),
        ("Linkshell 7", 22),
        ("Linkshell 8", 23),
        ("Free Company", 24),
        ("Novice Network", 27),
        ("PvP Team", 36),
    ];

    private int channel;
    private int member;
    private string sender = string.Empty;
    private string message = string.Empty;
    private string? status;

    protected override void DrawContent()
    {
        ImGui.TextDisabled("Writes a line into your own chat log. Nothing is sent, and nobody else sees it.");
        ImGui.TextDisabled("It is text only: no clickable player link, FC tag, or world suffix unless you type one.");
        ImGui.TextDisabled("Uses the current time, like a newly received line.");

        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        if (ImGui.BeginCombo("##fakeChannel", Channels[channel].Label))
        {
            for (var i = 0; i < Channels.Length; i++)
            {
                if (ImGui.Selectable(Channels[i].Label, i == channel)) channel = i;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("channel");

        ImGui.SetNextItemWidth(180 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##fakeSender", "Name or recipient", ref sender, 64);
        ImGui.SameLine();
        ImGui.TextDisabled("name");

        ImGui.TextDisabled("Channel mode uses the name above, with the channel icon and colour.");
        ImGui.TextDisabled("Outgoing tells use it as the recipient; every other channel uses it as the speaker.");

        if (Channels[channel].Kind == 14)
        {
            if (member > 8) member = 8;

            ImGui.SetNextItemWidth(120 * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("##fakeMember", member == 0 ? "None" : member.ToString()))
            {
                if (ImGui.Selectable("None", member == 0)) member = 0;
                for (var i = 1; i <= 8; i++)
                {
                    if (ImGui.Selectable(i.ToString(), i == member)) member = i;
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            ImGui.TextDisabled("member #");
            if (member == 0) ImGui.TextDisabled("pick an occupied slot to get the game's number badge");
        }

        ImGui.SetNextItemWidth(320 * ImGuiHelpers.GlobalScale);
        var entered = ImGui.InputTextWithHint("##fakeMessage", "Message, then Enter", ref message, 400, ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.Button("Add line##fakeAdd") || entered) Post();

        ImGui.SameLine();
        if (ImGui.Button("Read back##fakeRead"))
        {
            status = FakeConvo.LastStored() is { } stored ? $"Stored: {stored}" : "Could not read the log.";
        }

        if (status != null)
        {
            ImGui.TextWrapped(status);
        }
    }

    private void Post()
    {
        var name = sender.Trim();
        var text = message.Trim();
        if (text.Length == 0)
        {
            status = "Type a message first.";
            return;
        }

        if (name.Length == 0)
        {
            status = "A name or recipient is needed.";
            return;
        }

        var shown = FakeConvo.NoBreak(name);
        var (label, kind) = Channels[channel];
        var numbered = member > 0 && (kind is 14 or 15);
        var added = numbered
            ? FakeConvo.PrintNumbered(kind, member, shown, text)
            : FakeConvo.Print(kind, shown, text);

        if (added)
        {
            status = $"Added to {label}.";
        }
        else
        {
            status = numbered
                ? "Could not add the numbered line: the game function is missing on this build."
                : "Could not add the line: the chat log is not ready, or the game function is missing.";
        }
    }
}
