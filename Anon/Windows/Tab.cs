using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Anon.Windows.Tabs;

public abstract class Tab(string title, Configuration config)
{
    protected Configuration Config { get; } = config;

    public bool Shown { get; private set; }

    public void Draw()
    {
        using var item = ImRaii.TabItem(title);
        Shown = item.Success;
        if (Shown) DrawContent();
        else Flush();
    }

    protected abstract void DrawContent();

    public virtual void Flush()
    {
    }

    protected bool Check(string label, ref bool value)
    {
        var changed = ImGui.Checkbox(label, ref value);
        if (changed) Config.Save();
        return changed;
    }
}
