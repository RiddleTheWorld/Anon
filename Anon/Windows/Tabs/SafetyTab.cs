using Dalamud.Bindings.ImGui;

namespace Anon.Windows.Tabs;

public sealed class SafetyTab(Configuration config) : Tab("Safety", config)
{
    protected override void DrawContent()
    {
        var safety = Config.Safety;
        Check("Pause redraws in duties##safety", ref safety.PauseInDuty);
        Check("Pause redraws in combat##safety", ref safety.PauseInCombat);
        ImGui.TextDisabled("Names stay masked while paused. Only model redraws wait.");
        ImGui.SliderInt("Redraws per frame##safety", ref safety.MaxRedrawsPerFrame, 1, 10);
        if (ImGui.IsItemDeactivatedAfterEdit()) Config.Save();

        ImGui.Separator();
        Check("Disable in listed zones##safety", ref safety.DisableInZones);
        ImGui.TextDisabled("Listed zones restore names and saved appearances until the zone is removed.");

        var zone = Plugin.ClientState.TerritoryType;
        var listed = safety.Zones.Contains(zone);
        if (ImGui.Button($"{(listed ? "Remove" : "Add")} current zone ({zone})##safety"))
        {
            if (listed) safety.Zones.Remove(zone);
            else safety.Zones.Add(zone);
            Config.Save();
        }

        ImGui.TextDisabled($"{safety.Zones.Count} zones listed");
    }
}
