// File location in project: Plugin.cs

using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FateFinder.IPC;
using FateFinder.Services;
using FateFinder.Windows;

namespace FateFinder;

public sealed class Plugin : IDalamudPlugin
{
    // ── Dalamud services ──────────────────────────────────────────────────────
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager        CommandManager   { get; private set; } = null!;
    [PluginService] internal static IClientState           ClientState      { get; private set; } = null!;
    [PluginService] internal static IFateTable             FateTable        { get; private set; } = null!;
    [PluginService] internal static IFramework             Framework        { get; private set; } = null!;
    [PluginService] internal static IDataManager           DataManager      { get; private set; } = null!;
    [PluginService] internal static IPluginLog             Log              { get; private set; } = null!;
    [PluginService] internal static ICondition             Condition        { get; private set; } = null!;
    [PluginService] internal static IPlayerState           PlayerState      { get; private set; } = null!;
    [PluginService] internal static IChatGui               ChatGui          { get; private set; } = null!;
    [PluginService] internal static IToastGui              ToastGui         { get; private set; } = null!;

    // ── Commands ──────────────────────────────────────────────────────────────
    private const string CommandName    = "/ff";
    private const string CommandNameAlt = "/fatefinder";

    // ── Plugin systems ────────────────────────────────────────────────────────
    public Configuration   Configuration    { get; init; }
    public NavmeshIPC      NavmeshIPC        { get; init; }
    public RotationSolverIPC RotationSolverIPC { get; init; }
    public FateManager     FateManager      { get; init; }

    public readonly WindowSystem WindowSystem = new("FateFinder");
    private MainWindow   MainWindow   { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration     = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        NavmeshIPC        = new NavmeshIPC();
        RotationSolverIPC = new RotationSolverIPC();
        FateManager       = new FateManager(this);

        MainWindow   = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open / close FATE Finder"
        });
        CommandManager.AddHandler(CommandNameAlt, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open / close FATE Finder"
        });

        PluginInterface.UiBuilder.Draw        += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi  += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Hook the framework update to run the FATE manager state machine
        Framework.Update += FateManager.OnFrameworkUpdate;

        Log.Information("[FateFinder] Plugin loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= FateManager.OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw         -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi   -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

        FateManager.Dispose();
        NavmeshIPC.Dispose();
        RotationSolverIPC.Dispose();

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        ConfigWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(CommandNameAlt);

        Log.Information("[FateFinder] Plugin unloaded.");
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    public void ToggleMainUi()   => MainWindow.Toggle();
    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
