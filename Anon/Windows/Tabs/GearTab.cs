using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Anon.Windows.Tabs;

public sealed class GearTab(Configuration config) : Tab("Gear", config)
{
    protected override void DrawContent()
    {
        var gear = Config.Gear;
        using (var combo = ImRaii.Combo("Outfit##gear", gear.Style.ToString()))
        {
            if (combo.Success)
            {
                foreach (var style in Enum.GetValues<OutfitStyle>())
                {
                    if (!ImGui.Selectable($"{style}##outfit", style == gear.Style)) continue;
                    gear.Style = style;
                    Config.Save();
                }
            }
        }

        ImGui.TextDisabled("Real keeps their gear, Plain strips it, Random gives a stable random outfit.");
        Check("Hide weapons##gear", ref gear.HideWeapons);
    }
}
