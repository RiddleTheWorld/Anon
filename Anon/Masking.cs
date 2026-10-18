using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Anon;

public sealed class Masking : IDisposable
{
    private readonly ErrorOnce errors = new("Anon: failed to mask a chat message or nameplate");
    private readonly Aliases aliases;
    private readonly Disguise disguise;
    private readonly Configuration config;
    private const int WriteBackFrames = 12;
    private readonly Dictionary<ulong, int> writeBacks = [];
    private string lastSelfPlate = string.Empty;

    public Masking(Aliases aliases, Disguise disguise, Configuration config)
    {
        this.aliases = aliases;
        this.disguise = disguise;
        this.config = config;

        Plugin.Chat.ChatMessage += OnChat;
        Plugin.NamePlates.OnNamePlateUpdate += OnNamePlate;
    }

    public void Dispose()
    {
        Plugin.Chat.ChatMessage -= OnChat;
        Plugin.NamePlates.OnNamePlateUpdate -= OnNamePlate;

        try
        {
            RestorePlates();
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private unsafe void RestorePlates()
    {
        var numbers = NamePlateNumberArray.Instance();
        var strings = AtkStage.Instance()->GetStringArrayData(StringArrayType.NamePlate);
        if (numbers == null || strings == null) return;

        var restored = 0;
        for (var i = 0; i < Math.Min(numbers->ActiveNamePlateCount, numbers->ObjectData.Length); i++)
        {
            ref var plate = ref numbers->ObjectData[i];
            if (Plugin.Objects.SearchByEntityId(plate.EntityId) is not IPlayerCharacter player) continue;
            var id = player.GameObjectId;
            if (!writeBacks.ContainsKey(id)) continue;

            strings->SetValue(i, disguise.RealName((Character*)player.Address), suppressUpdates: true);
            plate.UpdateFlags |= 1;
            restored++;
        }

        if (config.Diagnostics) Plugin.Log.Information($"Anon: unload wrote back {restored} nameplates");
    }

    private static unsafe void FlagPlate(ulong entityId)
    {
        var numbers = NamePlateNumberArray.Instance();
        if (numbers == null) return;

        for (var i = 0; i < Math.Min(numbers->ActiveNamePlateCount, numbers->ObjectData.Length); i++)
        {
            if (numbers->ObjectData[i].EntityId != entityId) continue;

            numbers->ObjectData[i].UpdateFlags |= 1;
            return;
        }
    }

    private void OnChat(IHandleableChatMessage message)
    {
        if (!config.Names.Chat) return;

        try
        {
            Learn(message.Sender);
            Learn(message.Message);
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void Learn(SeString text)
    {
        foreach (var player in text.Payloads.OfType<PlayerPayload>()) aliases.Find(player.PlayerName);
    }

    private unsafe void OnNamePlate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        try
        {
            foreach (var handler in handlers)
            {
                if (handler.PlayerCharacter is not { } player)
                {
                    MaskTitle(handler);
                    continue;
                }

                var character = (Character*)player.Address;
                var id = player.GameObjectId;
                var real = disguise.RealName(character);
                var found = aliases.Find(real);
                if (config.Diagnostics && id == Plugin.Objects.LocalPlayer?.GameObjectId)
                {
                    var plate = found ?? (writeBacks.ContainsKey(id) ? $"writing back \"{real}\"" : "(game's own)");
                    var state = $"real=\"{real}\" object=\"{player.Name.TextValue}\" plate={plate} text=\"{handler.Name.TextValue}\"";
                    if (state != lastSelfPlate) Plugin.Log.Information($"[probe] my nameplate {state}");
                    lastSelfPlate = state;
                }

                if (found is { } alias)
                {
                    writeBacks[id] = WriteBackFrames;
                    handler.Name = alias;
                    if (config.Names.HideFcTag) handler.RemoveFreeCompanyTag();
                    if (config.Names.HideTitle) handler.RemoveTitle();
                    continue;
                }

                if (!writeBacks.TryGetValue(id, out var left)) continue;

                if (handler.Name.TextValue == real && character->NameString == real)
                {
                    writeBacks.Remove(id);
                    continue;
                }

                handler.Name = real;
                handler.Title = handler.InfoView.Title;
                handler.FreeCompanyTag = handler.InfoView.FreeCompanyTag;

                FlagPlate(id);

                if (left > 1) writeBacks[id] = left - 1;
                else writeBacks.Remove(id);
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void MaskTitle(INamePlateUpdateHandler handler)
    {
        var title = handler.Title.TextValue;
        if (title.Length == 0) return;

        var masked = aliases.Replace(title);
        if (masked != title) handler.Title = masked;
    }
}
