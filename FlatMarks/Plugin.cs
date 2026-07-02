using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FlatMarks.Data;
using FlatMarks.Rendering;
using FlatMarks.Windows;
using Pictomancy;

namespace FlatMarks;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Hooker { get; private set; } = null!;

    private const string CommandName = "/flatmarks";

    public readonly Configuration Config;

    private readonly WindowSystem windowSystem = new("FlatMarks");
    private readonly ConfigWindow configWindow;
    private readonly FlatMarkRenderer renderer;
    private readonly FallbackRenderer fallbackRenderer;
    private readonly NativeWaymarkVfx nativeVfx;
    private readonly PctContext pctContext;

    // Waymark snapshot refreshed on framework update, consumed on draw (no per-frame alloc in draw path).
    private readonly WaymarkState[] buffer = new WaymarkState[WaymarkReader.MarkerCount];
    private int activeCount;
    private Vector3 cullOrigin;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Sanitize();

        pctContext = PctService.Initialize(PluginInterface);

        renderer = new FlatMarkRenderer(Config, TextureProvider, PluginInterface);
        fallbackRenderer = new FallbackRenderer(Config, GameGui);
        nativeVfx = new NativeWaymarkVfx(Hooker, Config);
        configWindow = new ConfigWindow(Config, nativeVfx);
        windowSystem.AddWindow(configWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open FlatMarks settings. Use '/flatmarks toggle' to enable/disable.",
        });

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi += OpenConfig;
        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.OpenMainUi -= OpenConfig;
        CommandManager.RemoveHandler(CommandName);

        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        renderer.Dispose();
        nativeVfx.Dispose();
        pctContext.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            Config.MasterEnabled = !Config.MasterEnabled;
            Config.Save();
            return;
        }

        configWindow.Toggle();
    }

    private void OpenConfig() => configWindow.Toggle();

    private void OnFrameworkUpdate(IFramework _)
    {
        // Prune destroyed native waymark VFX from tracking (runs regardless of draw gating).
        nativeVfx.Update();

        // Refresh the snapshot off the draw path (spec 1d). Keep this the only place that touches game memory.
        activeCount = 0;
        if (!ShouldDraw()) return;

        try
        {
            activeCount = WaymarkReader.ReadAll(buffer);
            cullOrigin = ObjectTable.LocalPlayer?.Position ?? default;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FlatMarks: failed reading waymarks");
            activeCount = 0;
        }
    }

    private void OnDraw()
    {
        windowSystem.Draw();

        if (activeCount == 0 || !ShouldDraw()) return;

        var span = buffer.AsSpan(0, activeCount);
        try
        {
            if (Config.ForceFallbackRenderer)
                fallbackRenderer.Draw(span, cullOrigin);
            else
                renderer.Draw(span, cullOrigin);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FlatMarks: draw error");
        }
    }

    private bool ShouldDraw()
    {
        if (!Config.MasterEnabled) return false;
        if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer == null) return false;
        if (ClientState.IsPvP) return false; // disabled in PvP out of caution (spec 1d)

        if (Condition[ConditionFlag.BetweenAreas]
            || Condition[ConditionFlag.BetweenAreas51]
            || Condition[ConditionFlag.WatchingCutscene]
            || Condition[ConditionFlag.WatchingCutscene78]
            || Condition[ConditionFlag.OccupiedInCutSceneEvent])
            return false;

        return true;
    }
}
