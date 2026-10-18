using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Anon.Windows;

namespace Anon;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static INamePlateGui NamePlates { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IPartyList Party { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/anon";

    private readonly Configuration configuration;
    private readonly Disguise disguise;
    private readonly Aliases aliases;
    private readonly Masking masking;
    private readonly TextScrubber textScrubber;
    private readonly SocialLists socialLists;
    private readonly Hotkeys hotkeys;
    private readonly WindowSystem windows = new("Anon");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        configuration = Configuration.Load();
        Wardrobe.Load();
        var appearances = AppearanceStore.Load(configuration);

        aliases = new Aliases(configuration);
        disguise = new Disguise(aliases, configuration, appearances);
        masking = new Masking(aliases, disguise, configuration);
        textScrubber = new TextScrubber(aliases, configuration);
        socialLists = new SocialLists(aliases, configuration);
        hotkeys = new Hotkeys(disguise, aliases, configuration);

        mainWindow = new MainWindow(configuration, disguise, aliases, appearances);
        windows.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggles the window. Append 'on', 'off', 'reroll', 'names' or 'look'."
        });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        mainWindow.Flush();
        windows.RemoveAllWindows();

        hotkeys.Dispose();
        socialLists.Dispose();
        masking.Dispose();
        textScrubber.Dispose();
        disguise.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                SetEnabled(true);
                break;
            case "off":
                SetEnabled(false);
                break;
            case "reroll":
                disguise.Reroll();
                break;
            case "names":
                aliases.RerollNames();
                break;
            case "look":
                disguise.RerollLook();
                break;
            default:
                mainWindow.Toggle();
                break;
        }
    }

    private void SetEnabled(bool enabled)
    {
        configuration.Enabled = enabled;
        configuration.Save();
    }
}
