using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace Anon;

public sealed unsafe class TextScrubber : IDisposable
{
    private const int MaxDepth = 3;
    private const int PartyCapacity = 8;
    private const int RevealMilliseconds = 2000;
    private const int SweepMilliseconds = 500;
    private const string PartyList = "_PartyList";
    private const string ServerBar = "_DTR";
    private const string NamePlates = "NamePlate";
    private const string ChatPrefix = "ChatLog";

    private static readonly string[] ChatPanels = ["ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3"];

    private static readonly string[] Windows =
    [
        "_TargetInfo", "_TargetInfoMainTarget", "_FocusTargetInfo", "_ToTarget",
        "Character", "CharacterInspect", "CharaCard", "ItemDetail", "AddonContextMenuTitle", "Talk",
    ];

    private static readonly string[] TeamLists = ["_AllianceList1", "_AllianceList2", "Alliance48", "BannerParty", "PvPMKSPartyList1", "PvPMKSPartyList2", "PvPMKSPartyList3", "PvPMKSBattleLog"];

    private readonly ErrorOnce errors = new("Anon: failed to scrub names from a window");
    private readonly Aliases aliases;
    private readonly Configuration config;
    private readonly (AddonEvent Event, string[] Addons, IAddonLifecycle.AddonEventDelegate Handler)[] listeners;
    private readonly Dictionary<string, (byte[] Text, int Version, bool Enabled)> chatCache = [];
    private readonly (byte[]? Text, int Version)[] partyCache = new (byte[]?, int)[PartyCapacity];
    private long revealChat;
    private long revealParty;
    private long revealWindows;
    private long nextSweep;
    private bool chatMasked;
    private bool partyMasked;
    private bool windowsMasked;
    private WorldLayout worldWritten;
    private float worldRight;
    private bool worldPinned;
    private (string Name, ushort Width) worldMeasured = (string.Empty, 0);

    private record struct WorldLayout(float X, ushort Width, float IconX, float TextX, ushort TextWidth);

    public TextScrubber(Aliases aliases, Configuration config)
    {
        this.aliases = aliases;
        this.config = config;

        listeners =
        [
            (AddonEvent.PostUpdate, ChatPanels, OnChatPanel),
            (AddonEvent.PostRefresh, ChatPanels, OnChatPanel),
            (AddonEvent.PostUpdate, [ServerBar], OnServerBar),
            (AddonEvent.PostRequestedUpdate, [ServerBar], OnServerBar),
            (AddonEvent.PreDraw, [ServerBar], OnServerBarDraw),
            (AddonEvent.PostSetup, Windows, OnWindow),
            (AddonEvent.PostRefresh, Windows, OnWindow),
            (AddonEvent.PostSetup, TeamLists, OnWindow),
            (AddonEvent.PostUpdate, TeamLists, OnWindow),
            (AddonEvent.PostSetup, [PartyList], OnPartyList),
            (AddonEvent.PostRefresh, [PartyList], OnPartyList),
            (AddonEvent.PostRequestedUpdate, [PartyList], OnPartyList),
            (AddonEvent.PostUpdate, [PartyList], OnPartyList),
        ];

        foreach (var (kind, addons, handler) in listeners) Plugin.Addons.RegisterListener(kind, addons, handler);
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        foreach (var (kind, addons, handler) in listeners) Plugin.Addons.UnregisterListener(kind, addons, handler);
        Plugin.Framework.RunOnFrameworkThread(Restore).Wait(1000);
    }

    private void Restore()
    {
        try
        {
            ScrubAllUnits(reveal: true, everything: true);

            foreach (var name in ChatPanels)
            {
                var panel = Plugin.GameGui.GetAddonByName<AddonChatLogPanel>(name);
                if (panel != null && panel->ChatText != null) ScrubChat(panel->ChatText, enabled: false, reveal: true);
            }

            var dtr = Plugin.GameGui.GetAddonByName<AddonDtr>(ServerBar);
            if (worldPinned && dtr != null && dtr->WorldInfoContainer != null && dtr->WorldText != null) UnpinWorld(dtr);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private bool Active => config.IsActive(Plugin.ClientState.TerritoryType);

    private void OnUpdate(IFramework framework)
    {
        var chat = Active && config.Names.Chat && config.Names.MaskChatPanel;
        var party = Active && config.Names.PartyList;
        var windows = Active && config.Names.Windows;
        var until = Environment.TickCount64 + RevealMilliseconds;

        if (chatMasked && !chat)
        {
            revealChat = until;
            chatCache.Clear();
        }

        if (partyMasked && !party)
        {
            revealParty = until;
            Array.Clear(partyCache);
        }

        if (windowsMasked && !windows) revealWindows = until;

        chatMasked = chat;
        partyMasked = party;
        windowsMasked = windows;

        if (windows || Revealing(revealWindows)) SweepWindows(reveal: !windows);
    }

    private static bool Revealing(long until) => until != 0 && Environment.TickCount64 < until;

    private void OnWindow(AddonEvent type, AddonArgs args)
    {
        if (!Active || !config.Names.Windows) return;

        try
        {
            ScrubNodes(&((AtkUnitBase*)args.Addon.Address)->UldManager, 0, servers: false, reveal: false);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void SweepWindows(bool reveal)
    {
        if (Environment.TickCount64 < nextSweep) return;

        nextSweep = Environment.TickCount64 + SweepMilliseconds;
        ScrubAllUnits(reveal, everything: false);
    }

    private void ScrubAllUnits(bool reveal, bool everything)
    {
        try
        {
            var units = &RaptureAtkUnitManager.Instance()->AtkUnitManager.AllLoadedUnitsList;
            for (var i = 0; i < units->Count; i++)
            {
                var unit = units->Entries[i].Value;
                if (unit == null || !unit->IsVisible) continue;

                var name = unit->NameString;
                if (name.StartsWith(ChatPrefix) || (!everything && name is PartyList or ServerBar or NamePlates)) continue;

                ScrubNodes(&unit->UldManager, 0, servers: false, reveal);
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void OnChatPanel(AddonEvent type, AddonArgs args)
    {
        try
        {
            var node = ((AddonChatLogPanel*)args.Addon.Address)->ChatText;
            if (node == null) return;

            var enabled = Active && config.Names.Chat && config.Names.MaskChatPanel;
            var reveal = !enabled && Revealing(revealChat);

            var current = node->NodeText.AsSpan();
            if (!reveal && chatCache.TryGetValue(args.AddonName, out var cached) && cached.Enabled == enabled
                       && cached.Version == aliases.Version && current.SequenceEqual(cached.Text)) return;

            ScrubChat(node, enabled, reveal);
            if (!reveal) chatCache[args.AddonName] = (node->NodeText.AsSpan().ToArray(), aliases.Version, enabled);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void OnPartyList(AddonEvent type, AddonArgs args)
    {
        try
        {
            var enabled = Active && config.Names.PartyList;
            var reveal = !enabled && Revealing(revealParty);
            if (!enabled && !reveal) return;

            var addon = (AddonPartyList*)args.Addon.Address;

            if (!reveal)
            {
                foreach (var member in Plugin.Party) aliases.Find(member.Name.TextValue);
            }

            for (var i = 0; i < addon->PartyMembers.Length; i++)
            {
                var node = addon->PartyMembers[i].Name;
                if (node == null) continue;

                var current = node->NodeText.AsSpan();
                var cached = partyCache[i];
                if (!reveal && cached.Text != null && cached.Version == aliases.Version && current.SequenceEqual(cached.Text)) continue;

                ScrubText(node, reveal);
                if (!reveal) partyCache[i] = (node->NodeText.AsSpan().ToArray(), aliases.Version);
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void OnServerBar(AddonEvent type, AddonArgs args)
    {
        if (!Active || !config.Names.HideWorld) return;

        try
        {
            var dtr = (AddonDtr*)args.Addon.Address;

            ScrubNodes(&dtr->AtkUnitBase.UldManager, 0, servers: true, reveal: false);

            if (dtr->WorldInfoContainer != null && dtr->WorldText != null && FakeWorld() is { } fake) PinWorld(dtr, fake);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void OnServerBarDraw(AddonEvent type, AddonArgs args)
    {
        try
        {
            var dtr = (AddonDtr*)args.Addon.Address;
            if (dtr->WorldInfoContainer == null || dtr->WorldText == null) return;

            if (Active && config.Names.HideWorld && FakeWorld() is { } fake) PinWorld(dtr, fake);
            else if (worldPinned) UnpinWorld(dtr);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private static string? RealWorld() =>
        Plugin.Objects.LocalPlayer?.CurrentWorld.ValueNullable?.Name.ToString() is { Length: > 0 } world ? world : null;

    private string? FakeWorld() => RealWorld() is { } world ? aliases.WorldAlias(world) : null;

    private void PinWorld(AddonDtr* dtr, string fake)
    {
        ShowWorld(dtr, fake);
        worldPinned = true;
    }

    private void UnpinWorld(AddonDtr* dtr)
    {
        if (RealWorld() is { } real) ShowWorld(dtr, real);
        worldPinned = false;
    }

    private void ShowWorld(AddonDtr* dtr, string name)
    {
        if (dtr->WorldText->NodeText.ToString() != name) dtr->WorldText->SetText(name);
        LayOutWorld(dtr, name);
    }

    private void LayOutWorld(AddonDtr* dtr, string name)
    {
        var container = dtr->WorldInfoContainer;
        var text = dtr->WorldText;
        var icon = dtr->WorldVisitImage != null ? &dtr->WorldVisitImage->AtkResNode : null;

        var current = ReadWorld(container, icon, text);
        if (!worldPinned || current != worldWritten) worldRight = container->X + container->Width;

        var width = MeasureWorld(text, name);
        if (width == 0) return;

        var iconWidth = icon != null && icon->IsVisible() ? icon->Width : (ushort)0;
        var fitted = (ushort)(iconWidth + width);
        var target = new WorldLayout(worldRight - fitted, fitted, 0, iconWidth, width);
        if (current == target) return;

        WriteWorld(container, icon, text, target);
        worldWritten = target;
    }

    private static WorldLayout ReadWorld(AtkResNode* container, AtkResNode* icon, AtkTextNode* text) =>
        new(container->X, container->Width, icon != null ? icon->X : 0, text->AtkResNode.X, text->AtkResNode.Width);

    private static void WriteWorld(AtkResNode* container, AtkResNode* icon, AtkTextNode* text, WorldLayout layout)
    {
        container->SetXFloat(layout.X);
        container->SetWidth(layout.Width);
        if (icon != null) icon->SetXFloat(layout.IconX);
        text->AtkResNode.SetXFloat(layout.TextX);
        text->AtkResNode.SetWidth(layout.TextWidth);
    }

    private ushort MeasureWorld(AtkTextNode* text, string name)
    {
        if (worldMeasured.Name == name) return worldMeasured.Width;

        var bytes = Encoding.UTF8.GetBytes(name + '\0');
        ushort width = 0, height = 0;
        fixed (byte* pointer = bytes) text->GetTextDrawSize(&width, &height, pointer);

        worldMeasured = (name, width);
        return width;
    }

    private void ScrubNodes(AtkUldManager* manager, int depth, bool servers, bool reveal)
    {
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null) continue;

            var text = node->GetAsAtkTextNode();
            if (text != null)
            {
                ScrubText(text, reveal, servers);
                continue;
            }

            var component = node->GetAsAtkComponentNode();
            if (component != null && component->Component != null && depth < MaxDepth && !IsTextInput(component->Component))
                ScrubNodes(&component->Component->UldManager, depth + 1, servers, reveal);
        }
    }

    private static bool IsTextInput(AtkComponentBase* component) =>
        AtkComponentBase.MemberFunctionPointers.GetComponentType != null
        && component->GetComponentType() == ComponentType.TextInput;

    private void ScrubChat(AtkTextNode* node, bool enabled, bool reveal)
    {
        var raw = node->NodeText.AsSpan();
        if (raw.IsEmpty) return;

        var linked = ChatNames.Rewrite(raw, (name, shown) =>
        {
            if (enabled && aliases.Find(name) is { } alias) return alias;
            return aliases.Issued(shown) ? name : null;
        });

        var text = new ReadOnlySeString(linked ?? raw.ToArray());
        var replaced = (enabled || reveal) && Replace(ref text, reveal, servers: false);
        if (linked != null || replaced) node->SetText(text.AsSpan());
    }

    private void ScrubText(AtkTextNode* node, bool reveal, bool servers = false)
    {
        var raw = node->NodeText.AsSpan();
        if (raw.IsEmpty) return;

        var text = new ReadOnlySeString(raw.ToArray());
        if (Replace(ref text, reveal, servers)) node->SetText(text.AsSpan());
    }

    private bool Replace(ref ReadOnlySeString text, bool reveal, bool servers)
    {
        var plain = text.ToString();
        var matches = (reveal ? aliases.Reveal(plain) : aliases.Matches(plain, servers)).ToList();

        foreach (var (from, to) in matches)
            text = text.ReplaceText(Encoding.UTF8.GetBytes(from), Encoding.UTF8.GetBytes(to));

        return matches.Count > 0;
    }
}
