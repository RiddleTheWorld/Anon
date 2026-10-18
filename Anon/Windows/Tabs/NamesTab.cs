using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Anon.Windows.Tabs;

public sealed class NamesTab(Configuration config, Disguise disguise, Aliases aliases) : Tab("Names", config)
{
    private const long SaveDelayMilliseconds = 500;

    private string newCustomPlayer = string.Empty;
    private string? editingPlayer;
    private string editingName = string.Empty;
    private long lastEditAt;
    private bool editPending;

    protected override void DrawContent()
    {
        var names = Config.Names;
        var chat = names.Chat && names.MaskChatPanel;
        if (ImGui.Checkbox("Replace names in chat##names", ref chat))
        {
            names.Chat = chat;
            names.MaskChatPanel = chat;
            Config.Save();
        }

        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Learns names from chat messages and rewrites the chat log text.");
        Check("Replace names in party list##names", ref names.PartyList);
        Check("Replace names in other windows##names", ref names.Windows);
        if (Check("Hide server and data center names##names", ref names.HideWorld)) aliases.RefreshServers();
        DrawFakeDataCenter(names);

        var plateChanged = Check("Hide free company tags##names", ref names.HideFcTag);
        plateChanged |= Check("Hide titles##names", ref names.HideTitle);
        if (plateChanged) Plugin.NamePlates.RequestRedraw();

        ImGui.Separator();
        ImGui.TextDisabled("Custom names: choose what a specific player is called.");
        ImGui.TextDisabled("Any text works; a name is only replaced where it stands as a whole word.");
        ImGui.TextDisabled("Use Add target or type the real name; a player's masked name will not match.");
        ImGui.TextDisabled("Changes apply as you type and save after a short pause.");
        ImGui.TextDisabled("A custom name also applies to players listed on the Players tab.");

        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##customPlayer", ref newCustomPlayer, 32);
        ImGui.SameLine();
        if (ImGui.Button("Add##customNames"))
        {
            AddCustomName(newCustomPlayer);
            newCustomPlayer = string.Empty;
        }

        ImGui.SameLine();
        if (ImGui.Button("Add target##customNames") && Plugin.Targets.Target is { } target) AddCustomName(disguise.RealName(target));

        DrawCustomNames();
    }

    private void DrawFakeDataCenter(NameSettings names)
    {
        var me = Plugin.Objects.LocalPlayer;
        var current = me == null ? string.Empty : Servers.WorldName(me.CurrentWorld);
        var home = me == null ? string.Empty : Servers.WorldName(me.HomeWorld);
        var dataCenters = Servers.AvailableDataCenters(current, home);
        var selected = dataCenters.FirstOrDefault(dataCenter => string.Equals(dataCenter, names.FakeDataCenter, StringComparison.OrdinalIgnoreCase));
        var label = selected ?? "Random (recommended)";

        using (ImRaii.Disabled(!names.HideWorld))
        {
            ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
            using (var combo = ImRaii.Combo("Fake data center##names", label))
            {
                if (combo.Success)
                {
                    if (ImGui.Selectable("Random (recommended)##fakeDc", selected == null))
                    {
                        names.FakeDataCenter = string.Empty;
                        Config.ServerSeed++;
                        Config.Save();
                        aliases.RefreshServers();
                        ImGui.CloseCurrentPopup();
                    }

                    foreach (var dataCenter in dataCenters)
                    {
                        if (!ImGui.Selectable($"{dataCenter}##fakeDc", string.Equals(selected, dataCenter, StringComparison.OrdinalIgnoreCase))) continue;

                        names.FakeDataCenter = dataCenter;
                        Config.Save();
                        aliases.RefreshServers();
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Only changes the fake world and data center labels. Your current and home data centers are not offered.");
        ImGui.TextDisabled("Random avoids your current and home data centers. Reroll names keeps the chosen one and the fake worlds.");
    }

    private void DrawCustomNames()
    {
        var custom = Config.CustomNames;
        foreach (var player in custom.Keys.ToArray())
        {
            var name = editingPlayer == player ? editingName : custom[player];
            ImGui.TextUnformatted(player);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText($"##custom{player}", ref name, 32))
            {
                if (editingPlayer != null && editingPlayer != player) CommitEdit(stopEditing: true);

                editingPlayer = player;
                editingName = name;
                editPending = true;
                lastEditAt = Environment.TickCount64;
                ApplyCustomName(player, name);
            }

            if (ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemDeactivated()) CommitEdit(stopEditing: true);

            ImGui.SameLine();
            if (ImGui.SmallButton($"Random##custom{player}"))
            {
                if (editingPlayer == player) CommitEdit(stopEditing: true);

                custom[player] = Names.Create(Random.Shared);
                SaveNames();
            }

            ImGui.SameLine();
            if (!ImGui.SmallButton($"Remove##custom{player}")) continue;

            if (editingPlayer == player) CommitEdit(stopEditing: true);

            custom.Remove(player);
            SaveNames();
        }

        if (editingPlayer != null && editPending && Environment.TickCount64 - lastEditAt >= SaveDelayMilliseconds)
            CommitEdit(stopEditing: false);
    }

    private bool ApplyCustomName(string player, string name)
    {
        var trimmed = name.Trim();
        if (Config.CustomNames.TryGetValue(player, out var current) && string.Equals(current, trimmed, StringComparison.Ordinal)) return false;

        Config.CustomNames[player] = trimmed;
        aliases.RefreshNames();
        return true;
    }

    private void CommitEdit(bool stopEditing)
    {
        if (editingPlayer is not { } player)
        {
            if (stopEditing) editingPlayer = null;
            return;
        }

        if (!Config.CustomNames.ContainsKey(player))
        {
            editingPlayer = null;
            editingName = string.Empty;
            editPending = false;
            lastEditAt = 0;
            return;
        }

        var changed = ApplyCustomName(player, editingName);

        if (editPending || changed) Config.Save();

        editPending = false;
        lastEditAt = 0;
        if (stopEditing)
        {
            editingPlayer = null;
            editingName = string.Empty;
        }
    }

    public override void Flush() => CommitEdit(stopEditing: true);

    private void AddCustomName(string player)
    {
        CommitEdit(stopEditing: true);
        player = player.Trim();
        var custom = Config.CustomNames;
        if (player.Length == 0 || custom.ContainsKey(player)) return;

        custom[player] = Names.Create(Random.Shared);
        SaveNames();
    }

    private void SaveNames()
    {
        Config.Save();
        aliases.RefreshNames();
    }
}
