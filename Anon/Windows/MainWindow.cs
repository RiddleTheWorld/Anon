using System;
using System.Numerics;
using Anon.Windows.Tabs;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Anon.Windows;

public sealed class MainWindow : Window
{
    private readonly KeybindsTab keybinds;
    private readonly Tab[] tabs;
    private bool drawn;

    public MainWindow(Configuration config, Disguise disguise, Aliases aliases, AppearanceStore appearances)
        : base("Anon###AnonMain", ImGuiWindowFlags.AlwaysAutoResize)
    {
        keybinds = new KeybindsTab(config);
        tabs =
        [
            new GeneralTab(config, disguise, aliases),
            new LookTab(config, disguise, aliases, appearances),
            new NamesTab(config, disguise, aliases),
            new GearTab(config),
            new PlayersTab(config, disguise),
            new FakeConvoTab(config),
            keybinds,
            new SafetyTab(config),
        ];
    }

    public override void OnClose()
    {
        keybinds.StopCapture();
        Flush();
    }

    public void Flush()
    {
        foreach (var tab in tabs) tab.Flush();
    }

    public override void Update()
    {
        if (!drawn)
        {
            keybinds.StopCapture();
            Flush();
        }

        drawn = false;
    }

    public override void PreDraw()
    {
        base.PreDraw();
        var max = (ImGui.GetMainViewport().WorkSize - new Vector2(16, 16)) / ImGuiHelpers.GlobalScale;
        SizeConstraints = new WindowSizeConstraints { MaximumSize = max };
    }

    public override void Draw()
    {
        KeepOnScreen();
        drawn = true;
        var visible = false;
        using (var bar = ImRaii.TabBar("##anonTabs", ImGuiTabBarFlags.FittingPolicyScroll))
        {
            visible = bar.Success;
            if (visible)
            {
                foreach (var tab in tabs) tab.Draw();
            }
        }

        keybinds.Update(visible);
    }

    private static void KeepOnScreen()
    {
        var viewport = ImGui.GetMainViewport();
        var min = viewport.WorkPos + new Vector2(8, 8);
        var max = viewport.WorkPos + viewport.WorkSize - new Vector2(8, 8);
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        var x = Math.Clamp(pos.X, min.X, Math.Max(min.X, max.X - size.X));
        var y = Math.Clamp(pos.Y, min.Y, Math.Max(min.Y, max.Y - size.Y));
        if (x == pos.X && y == pos.Y) return;

        ImGui.SetWindowPos(new Vector2(x, y));
    }
}
