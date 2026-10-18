using System;
using Dalamud.Bindings.ImGui;

namespace Anon.Windows.Tabs;

public sealed class GeneralTab(Configuration config, Disguise disguise, Aliases aliases) : Tab("General", config)
{
    protected override void DrawContent()
    {
        Check("Enabled##general", ref Config.Enabled);
        Check("Me##general", ref Config.Self);
        Check("Everyone else##general", ref Config.Others);

        Check("Diagnostics##general", ref Config.Diagnostics);

        ImGui.Separator();
        ActionButton("Reroll names##general", "New generated player names. Fake worlds and the data center stay; custom names stay as set.", aliases.RerollNames);

        ImGui.SameLine();
        ActionButton("Reroll looks##general", "New random faces and outfits. Assigned saved looks stay.", disguise.RerollLook);

        ImGui.SameLine();
        ActionButton("Reroll all##general", "Rerolls names, fake worlds, a random data center (unless you picked one), and random looks.", disguise.Reroll);
        ImGui.TextDisabled("Reroll looks changes random faces and outfits. Saved looks stay until removed.");
    }

    private static void ActionButton(string label, string tooltip, Action action)
    {
        if (ImGui.Button(label)) action();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
    }
}
