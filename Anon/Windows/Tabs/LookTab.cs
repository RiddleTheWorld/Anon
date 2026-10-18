using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Anon.Windows.Tabs;

public sealed class LookTab(Configuration config, Disguise disguise, Aliases aliases, AppearanceStore appearances) : Tab("Look", config)
{
    private const int LooksPerPage = 20;

    private static readonly Vector4 ErrorColor = new(1f, 0.5f, 0.4f, 1f);
    private static readonly Vector4 WornColor = new(0.5f, 0.9f, 0.5f, 1f);
    private static readonly Vector4 WornButton = new(0.2f, 0.5f, 0.25f, 1f);
    private static readonly Vector4 DeleteButton = new(0.45f, 0.18f, 0.18f, 1f);

    private string? editingLook;
    private string editingName = string.Empty;
    private string lookSearch = string.Empty;
    private int lookPage;
    private string? deletingLook;
    private string? message;

    protected override void DrawContent()
    {
        var look = Config.Look;
        Check("Same race as me##look", ref look.SameRaceAsMe);

        using (ImRaii.Disabled(look.SameRaceAsMe))
        {
            Check("Include Hrothgar and Viera##look", ref look.AllRaces);
        }

        ImGui.Separator();
        ImGui.TextDisabled("Saved appearances: copy a face and glamour, name it, then apply it to anyone.");
        ImGui.TextDisabled("Visual only, and it stays until you remove it.");

        ImGui.TextUnformatted("Show:");
        ImGui.SameLine();
        if (ImGui.RadioButton("Real look##showMode", !Config.ShowAsSeen)) SetShowMode(false);
        ImGui.SameLine();
        if (ImGui.RadioButton("What I see##showMode", Config.ShowAsSeen)) SetShowMode(true);
        ImGui.TextDisabled(Config.ShowAsSeen
            ? "Saved looks show exactly what you saw when you copied them, disguise included."
            : "Saved looks show how the person really looks, without any disguise.");

        if (ImGui.Button("Copy from target##savedLook")) Copy(Plugin.Targets.Target, "target");
        ImGui.SameLine();
        if (ImGui.Button("Copy my look##savedLook")) Copy(Plugin.Objects.LocalPlayer, "character");

        if (message != null) ImGui.TextColored(ErrorColor, message);

        DrawWearing("You", Plugin.Objects.LocalPlayer);
        DrawWearing("Target", Plugin.Targets.Target);

        DrawSavedLooks();
    }

    private void DrawWearing(string who, IGameObject? obj)
    {
        if (obj is not IPlayerCharacter || disguise.Wearing(obj.Address) is not { } wearing) return;

        var face = wearing.Face ? "face on" : "face not on yet";
        var glam = wearing.Glam ? "glamour on" : "glamour not on yet";
        ImGui.TextColored(WornColor, $"{who}: \"{wearing.Label}\", {face}, {glam}");
    }

    private void Copy(IGameObject? source, string who)
    {
        if (source == null)
        {
            message = $"Nothing to copy: no {who}.";
            return;
        }

        var real = disguise.Capture(source.Address, false);
        var seen = disguise.Capture(source.Address, true);
        if (real.Look == null || seen.Look == null)
        {
            message = "That character's model is not loaded yet.";
            return;
        }

        var name = Config.ShowAsSeen ? source.Name.TextValue : disguise.RealName(source);
        var stored = SavedLooks.EncodeBoth(real.Look, real.Gear, real.Extras, seen.Look, seen.Gear, seen.Extras);
        if (appearances.DuplicateOf(stored) is { } existing)
        {
            message = $"That appearance is already saved as \"{existing}\".";
            return;
        }

        var label = appearances.Add(name, stored);
        message = $"Saved \"{label}\" with both its real look and what you see. Use Show to switch.";
    }

    private void SetShowMode(bool asSeen)
    {
        if (Config.ShowAsSeen == asSeen) return;

        Config.ShowAsSeen = asSeen;
        Config.Save();
        message = asSeen ? "Showing what you saw when copying." : "Showing real looks.";
    }

    private void DrawSavedLooks()
    {
        if (!ImGui.CollapsingHeader($"Saved appearances ({appearances.Count})###savedAppearances", ImGuiTreeNodeFlags.DefaultOpen)) return;

        if (appearances.Count == 0)
        {
            ImGui.TextDisabled("None saved yet. Use Copy from target or Copy my look.");
            return;
        }

        ImGui.TextDisabled("Saved in appearances.json between sessions. Deleting one puts anyone wearing it back.");
        if (appearances.Error is { } error) ImGui.TextColored(ErrorColor, error);

        if (appearances.Count > LooksPerPage || lookSearch.Length > 0)
        {
            ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputTextWithHint("##savedLookSearch", "Search saved appearances", ref lookSearch, 64)) lookPage = 0;
        }

        var query = lookSearch.Trim();
        var labels = appearances.Labels
            .Where(label => query.Length == 0 || label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (labels.Length == 0)
        {
            ImGui.TextDisabled("No match.");
            return;
        }

        var pages = Math.Max(1, (labels.Length + LooksPerPage - 1) / LooksPerPage);
        lookPage = Math.Clamp(lookPage, 0, pages - 1);

        if (pages > 1)
        {
            if (ImGui.SmallButton("<##lookPagePrev") && lookPage > 0) lookPage--;
            ImGui.SameLine();
            ImGui.TextDisabled($"Page {lookPage + 1} / {pages}");
            ImGui.SameLine();
            if (ImGui.SmallButton(">##lookPageNext") && lookPage < pages - 1) lookPage++;
            ImGui.Separator();
        }

        var me = aliases.MyName;
        var target = Plugin.Targets.Target is IPlayerCharacter player ? disguise.RealName(player) : null;

        foreach (var label in labels.Skip(lookPage * LooksPerPage).Take(LooksPerPage))
        {
            var name = editingLook == label ? editingName : label;

            ImGui.SetNextItemWidth(170 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText($"##lookName{label}", ref name, 64))
            {
                editingLook = label;
                editingName = name;
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                editingLook = null;
                Commit(label, name);
            }

            var onMe = me.Length > 0 && IsWearing(me, label);
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Button, WornButton, onMe))
            {
                if (ImGui.SmallButton($"{(onMe ? "On me" : "To me")}##applyMe{label}")) Toggle(label, me);
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip(onMe ? "Click to take it off you." : "Show this look on you.");

            var onTarget = target != null && IsWearing(target, label);
            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Button, WornButton, onTarget))
            {
                if (ImGui.SmallButton($"{(onTarget ? "On target" : "To target")}##applyTarget{label}"))
                {
                    if (target != null) Toggle(label, target);
                    else message = "Nothing to apply to: target a player first.";
                }
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip(onTarget ? "Click to take it off your target." : "Show this look on your target.");

            ImGui.SameLine();
            if (deletingLook == label)
            {
                using (ImRaii.PushColor(ImGuiCol.Button, DeleteButton))
                {
                    if (ImGui.SmallButton($"Confirm##confirmDelete{label}")) Delete(label);
                }

                ImGui.SameLine();
                if (ImGui.SmallButton($"Cancel##cancelDelete{label}")) deletingLook = null;
            }
            else
            {
                using (ImRaii.PushColor(ImGuiCol.Button, DeleteButton))
                {
                    if (ImGui.SmallButton($"Delete##removeLook{label}")) deletingLook = label;
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delete this saved appearance. Click again to confirm.");
            }

            var kind = appearances.Kind(label);
            var usedBy = SavedLooks.UsedBy(Config.LookAssignments, label).ToArray();
            ImGui.TextDisabled(usedBy.Length > 0
                ? $"    {kind}, on {string.Join(", ", usedBy)}"
                : $"    {kind}");
        }
    }

    private void Delete(string label)
    {
        if (editingLook == label) editingLook = null;
        deletingLook = null;

        var users = SavedLooks.UsedBy(Config.LookAssignments, label).ToArray();
        appearances.Remove(label, Config.LookAssignments);
        Config.Save();
        message = users.Length == 0
            ? $"Deleted \"{label}\"."
            : $"Deleted \"{label}\" and put {string.Join(", ", users)} back.";
    }

    private bool IsWearing(string player, string label) =>
        Config.LookAssignments.TryGetValue(player, out var worn) && string.Equals(worn, label, StringComparison.OrdinalIgnoreCase);

    private void Toggle(string label, string player)
    {
        if (player.Length == 0)
        {
            message = "That player has no name yet.";
            return;
        }

        if (IsWearing(player, label))
        {
            Config.LookAssignments.Remove(player);
            Config.Save();
            message = $"Took \"{label}\" off {player}. The look is still saved.";
            return;
        }

        Assign(label, player);
    }

    private void Commit(string label, string typed)
    {
        var wanted = string.Join(' ', typed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (wanted.Length == 0)
        {
            message = "A name is needed.";
            return;
        }

        if (wanted == label) return;

        var free = appearances.Rename(label, wanted, Config.LookAssignments);
        Config.Save();
        message = free == wanted ? $"Renamed to \"{free}\"." : $"Renamed to \"{free}\" (\"{wanted}\" was taken).";
    }

    private void Assign(string label, string player)
    {
        if (player.Length == 0)
        {
            message = "That player has no name yet.";
            return;
        }

        Config.LookAssignments[player] = label;
        Config.Save();
        message = $"{player} now shows \"{label}\".";
    }
}
