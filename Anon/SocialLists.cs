using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Anon;

public sealed unsafe class SocialLists : IDisposable
{
    private const int StringsPerMember = 5;
    private const int FcNameOffset = 2;

    private static readonly AddonEvent[] ListEvents =
        [AddonEvent.PreSetup, AddonEvent.PreRequestedUpdate];

    private static readonly (string Addon, StringArrayType Type, int Members)[] Lists =
    [
        ("PartyMemberList", StringArrayType.PartyMemberList, 48),
        ("FriendList", StringArrayType.FriendList, 200),
        ("FreeCompanyMember", StringArrayType.FreeCompanyMember, 200),
    ];

    private readonly ErrorOnce errors = new("Anon: failed to mask a social list");
    private readonly Aliases aliases;
    private readonly Configuration config;
    private readonly Dictionary<string, (string Name, string FcTag)> applied = [];
    private bool masked;

    public SocialLists(Aliases aliases, Configuration config)
    {
        this.aliases = aliases;
        this.config = config;
        foreach (var list in Lists)
        {
            foreach (var type in ListEvents) Plugin.Addons.RegisterListener(type, list.Addon, OnList);
        }

        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        foreach (var list in Lists)
        {
            foreach (var type in ListEvents) Plugin.Addons.UnregisterListener(type, list.Addon, OnList);
        }

        Plugin.Framework.RunOnFrameworkThread(Restore).Wait(1000);
    }

    private void OnUpdate(IFramework framework)
    {
        var enabled = config.Names.Windows && aliases.Active;
        if (masked && !enabled) Restore();
        masked = enabled;
    }

    private void OnList(AddonEvent type, AddonArgs args)
    {
        if (!config.Names.Windows || !aliases.Active) return;

        try
        {
            foreach (var list in Lists)
            {
                if (list.Addon != args.AddonName) continue;

                Mask(list.Type, list.Members);
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }
    }

    private void Mask(StringArrayType type, int members)
    {
        var data = AtkStage.Instance()->GetStringArrayData(type);
        if (data == null) return;

        for (var member = 0; member < members && (member + 1) * StringsPerMember <= data->Size; member++)
        {
            var index = member * StringsPerMember;
            var cell = data->Span[index].ToString();
            if (cell.Length == 0) continue;

            var known = applied.TryGetValue(cell, out var prior);
            var name = known ? prior.Name : cell;
            var alias = aliases.Find(name);

            if (alias == null)
            {
                if (!known) continue;

                data->SetValue(index, name, suppressUpdates: true);
                if (prior.FcTag.Length > 0) data->SetValue(index + FcNameOffset, prior.FcTag, suppressUpdates: true);
                applied.Remove(cell);
                continue;
            }

            var tag = UpdateTag(data, index, known ? prior.FcTag : string.Empty);

            if (alias != cell) data->SetValue(index, alias, suppressUpdates: true);

            if (known) applied.Remove(cell);
            applied[alias] = (name, tag);
        }
    }

    private string UpdateTag(StringArrayData* data, int index, string hidden)
    {
        var slot = index + FcNameOffset;
        if (!config.Names.HideFcTag)
        {
            if (hidden.Length > 0) data->SetValue(slot, hidden, suppressUpdates: true);
            return string.Empty;
        }

        if (hidden.Length > 0) return hidden;

        var shown = data->Span[slot].ToString();
        if (shown.Length > 0) data->SetValue(slot, string.Empty, suppressUpdates: true);
        return shown;
    }

    private void Restore()
    {
        try
        {
            foreach (var list in Lists)
            {
                var data = AtkStage.Instance()->GetStringArrayData(list.Type);
                if (data == null) continue;

                for (var member = 0; member < list.Members && (member + 1) * StringsPerMember <= data->Size; member++)
                {
                    var index = member * StringsPerMember;
                    if (!applied.TryGetValue(data->Span[index].ToString(), out var value)) continue;

                    data->SetValue(index, value.Name, suppressUpdates: true);
                    if (value.FcTag.Length > 0) data->SetValue(index + FcNameOffset, value.FcTag, suppressUpdates: true);
                }
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex);
        }

        applied.Clear();
    }
}
