using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Anon;

public sealed unsafe class Hotkeys : IDisposable
{
    public static bool Capturing;

    private readonly Disguise disguise;
    private readonly Aliases aliases;
    private readonly Configuration config;
    private readonly HashSet<Keybind> held = [];
    private bool typing;

    public Hotkeys(Disguise disguise, Aliases aliases, Configuration config)
    {
        this.disguise = disguise;
        this.aliases = aliases;
        this.config = config;
        Plugin.Framework.Update += OnUpdate;
        Plugin.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.PluginInterface.UiBuilder.Draw -= OnDraw;
    }

    private void OnDraw() => typing = ImGui.GetIO().WantTextInput;

    private void OnUpdate(IFramework framework)
    {
        var binds = config.Keybinds;
        var module = RaptureAtkModule.Instance();
        var blocked = Capturing || typing || module == null || module->AtkModule.IsTextInputActive();

        Check(binds.Toggle, blocked, Toggle);
        Check(binds.Reroll, blocked, disguise.Reroll);
        Check(binds.RerollLook, blocked, disguise.RerollLook);
        Check(binds.RerollNames, blocked, aliases.RerollNames);
    }

    private void Check(Keybind bind, bool blocked, Action action)
    {
        if (!bind.IsPressed())
        {
            held.Remove(bind);
            return;
        }

        if (held.Add(bind) && !blocked) action();
    }

    private void Toggle()
    {
        config.Enabled = !config.Enabled;
        config.Save();
    }
}
