using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;

namespace Anon.Windows.Tabs;

public sealed class KeybindsTab(Configuration config) : Tab("Keybinds", config)
{
    private static readonly Vector4 ErrorColor = new(1f, 0.5f, 0.4f, 1f);

    private Keybind? capturing;
    private string? message;

    public void Update(bool visible)
    {
        if (!visible || !Shown) capturing = null;
        Hotkeys.Capturing = capturing != null;
    }

    public void StopCapture()
    {
        capturing = null;
        Hotkeys.Capturing = false;
    }

    protected override void DrawContent()
    {
        var binds = Config.Keybinds;
        ImGui.TextDisabled("Click a keybind, then press the keys you want. Escape cancels.");
        ImGui.TextDisabled("The key still reaches the game, so pick a combination you don't use elsewhere.");
        DrawKeybind("Toggle on/off", binds.Toggle);
        DrawKeybind("Reroll names", binds.RerollNames);
        DrawKeybind("Reroll looks", binds.RerollLook);
        DrawKeybind("Reroll all", binds.Reroll);

        if (message != null) ImGui.TextColored(ErrorColor, message);
    }

    private void DrawKeybind(string label, Keybind bind)
    {
        var listening = capturing == bind;
        ImGui.TextUnformatted(label);
        ImGui.SameLine(200 * ImGuiHelpers.GlobalScale);
        if (ImGui.Button($"{(listening ? "Press keys..." : bind.ToString())}##{label}", new Vector2(160 * ImGuiHelpers.GlobalScale, 0)))
        {
            capturing = listening ? null : bind;
            message = null;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"Clear##{label}"))
        {
            bind.Clear();
            Config.Save();
            if (listening) capturing = null;
        }

        if (listening) Capture(bind);
    }

    private void Capture(Keybind bind)
    {
        if (Plugin.KeyState.IsVirtualKeyValid(VirtualKey.ESCAPE) && Plugin.KeyState[VirtualKey.ESCAPE])
        {
            capturing = null;
            message = null;
            return;
        }

        var key = Plugin.KeyState.GetValidVirtualKeys().FirstOrDefault(IsBindable);
        if (key == VirtualKey.NO_KEY) return;

        var candidate = new Keybind
        {
            Key = key,
            Ctrl = Plugin.KeyState[VirtualKey.CONTROL],
            Shift = Plugin.KeyState[VirtualKey.SHIFT],
            Alt = Plugin.KeyState[VirtualKey.MENU],
        };

        if (Config.Keybinds.All.Any(other => other != bind && other.Matches(candidate)))
        {
            message = $"{candidate} is already used by another action.";
            return;
        }

        bind.Key = candidate.Key;
        bind.Ctrl = candidate.Ctrl;
        bind.Shift = candidate.Shift;
        bind.Alt = candidate.Alt;
        Config.Save();
        capturing = null;
        message = null;
    }

    private static bool IsBindable(VirtualKey key) =>
        Plugin.KeyState[key]
        && key is not (VirtualKey.NO_KEY or VirtualKey.ESCAPE or VirtualKey.CONTROL or VirtualKey.SHIFT or VirtualKey.MENU
            or VirtualKey.LBUTTON or VirtualKey.RBUTTON or VirtualKey.MBUTTON or VirtualKey.XBUTTON1 or VirtualKey.XBUTTON2);
}
