using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Anon.Windows.Tabs;

public sealed class PlayersTab(Configuration config, Disguise disguise) : Tab("Players", config)
{
    private string newPlayer = string.Empty;
    private string search = string.Empty;
    private bool nearbyOpen;

    protected override void DrawContent()
    {
        ImGui.TextDisabled("Listed players keep their real look and name.");

        DrawNearby();

        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##playersName", "Real name", ref newPlayer, 32);
        ImGui.SameLine();
        if (ImGui.Button("Add##players"))
        {
            AddPlayer(newPlayer);
            newPlayer = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.Button("Add target##players") && Plugin.Targets.Target is { } target) AddPlayer(disguise.RealName(target));

        var whitelist = Config.Whitelist;
        for (var i = 0; i < whitelist.Count; i++)
        {
            ImGui.TextUnformatted(whitelist[i]);
            ImGui.SameLine();
            if (!ImGui.SmallButton($"Remove##player{i}")) continue;

            whitelist.RemoveAt(i);
            Config.Save();
            break;
        }
    }

    private void DrawNearby()
    {
        var width = 320 * ImGuiHelpers.GlobalScale;
        if (ImGui.Button($"{(nearbyOpen ? "Hide nearby players" : "Pick a nearby player")}##playersNearby", new Vector2(width, 0)))
        {
            nearbyOpen = !nearbyOpen;
            if (nearbyOpen)
            {
                search = string.Empty;
                ImGui.SetKeyboardFocusHere();
            }
        }

        if (!nearbyOpen) return;

        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##playersSearch", "Search", ref search, 32);

        if (ImGui.BeginChild("##playersNearbyList", new Vector2(width, 220 * ImGuiHelpers.GlobalScale), true))
        {
            var matches = 0;
            foreach (var player in Nearby())
            {
                if (search.Length > 0
                    && !player.Shown.Contains(search, StringComparison.OrdinalIgnoreCase)
                    && !player.Real.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;

                matches++;
                var label = string.Equals(player.Shown, player.Real, StringComparison.Ordinal)
                    ? player.Real
                    : $"{player.Shown}  ({player.Real})";
                if (player.IsMe) label += "  (you)";
                label += $"  {player.Distance:0} m";

                if (Config.Whitelist.Contains(player.Real, StringComparer.OrdinalIgnoreCase))
                {
                    ImGui.TextDisabled($"{label}  - listed");
                    continue;
                }

                if (!ImGui.Selectable($"{label}##nearby{player.Real}")) continue;

                AddPlayer(player.Real);
                nearbyOpen = false;
                break;
            }

            if (matches == 0) ImGui.TextDisabled(search.Length > 0 ? "No match." : "Nobody nearby.");
        }

        ImGui.EndChild();
    }

    private List<(string Shown, string Real, float Distance, bool IsMe)> Nearby()
    {
        var me = Plugin.Objects.LocalPlayer;
        var origin = me?.Position ?? Vector3.Zero;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var players = new List<(string Shown, string Real, float Distance, bool IsMe)>();

        foreach (var player in Plugin.Objects.PlayerObjects)
        {
            var real = disguise.RealName(player);
            if (real.Length == 0 || !seen.Add(real)) continue;

            var isMe = me != null && player.Address == me.Address;
            players.Add((player.Name.TextValue, real, Vector3.Distance(origin, player.Position), isMe));
        }

        players.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return players;
    }

    private void AddPlayer(string name)
    {
        name = name.Trim();
        var whitelist = Config.Whitelist;
        if (name.Length == 0 || whitelist.Contains(name, StringComparer.OrdinalIgnoreCase)) return;

        whitelist.Add(name);
        Config.Save();
    }
}
