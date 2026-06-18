// File location in project: Services/FateManager.cs

using Dalamud.Game.ClientState.Fates;
using Dalamud.Plugin.Services;
using FateFinder.Data;
using System;
using System.Linq;
using System.Numerics;

namespace FateFinder.Services;

// ── State machine ──────────────────────────────────────────────────────────────
public enum FateFinderState
{
    Stopped,         // Plugin not started
    Idle,            // In zone, scanning for FATEs
    MovingToFate,    // Navigating toward selected FATE
    InFate,          // Inside FATE circle, RSR fighting
    FateComplete,    // Brief cooldown after FATE ends
    Teleporting,     // Waiting for zone-change after teleport
    MovingToCenter,  // Flying to zone centre (idle behaviour)
}

public sealed class FateManager : IDisposable
{
    // ── Dependencies ─────────────────────────────────────────────────────────
    private readonly Plugin          _plugin;
    private Configuration            Config  => _plugin.Configuration;

    // ── State ─────────────────────────────────────────────────────────────────
    public  FateFinderState State        { get; private set; } = FateFinderState.Stopped;
    public  string          StatusMessage { get; private set; } = "Stopped.";
    public  string          CurrentFateName { get; private set; } = string.Empty;
    public  byte            CurrentFateProgress { get; private set; }
    public  float           CurrentFateTimeRemaining { get; private set; }

    private ushort          _currentFateId   = 0;
    private DateTime        _cooldownEnds    = DateTime.MinValue;
    private bool            _rsrWasEnabled   = false;

    // ── Teleporter IPC ────────────────────────────────────────────────────────
    // Requires the "Teleporter" plugin by Pohky.
    // IPC signature: (uint aetheryteId, byte subIndex) → bool
    private Dalamud.Plugin.Ipc.ICallGateSubscriber<uint, byte, bool>? _teleportIpc;

    public FateManager(Plugin plugin)
    {
        _plugin = plugin;

        // Hook territory change to detect when a teleport lands
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;

        // Try to wire up Teleporter IPC
        TryInitTeleporter();
    }

    public void Dispose()
    {
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Stop();
    }

    // ── Public control ────────────────────────────────────────────────────────

    public void Start()
    {
        if (Config.SelectedZoneId == 0)
        {
            StatusMessage = "No zone selected.";
            return;
        }
        State         = FateFinderState.Idle;
        StatusMessage = "Started. Scanning for FATEs...";
        Plugin.Log.Information("[FateFinder] Started.");
    }

    public void Stop()
    {
        if (State == FateFinderState.Stopped) return;

        _plugin.NavmeshIPC.StopNavigation();

        if (_rsrWasEnabled)
        {
            _plugin.RotationSolverIPC.Disable();
            _rsrWasEnabled = false;
        }

        _currentFateId = 0;
        State          = FateFinderState.Stopped;
        StatusMessage  = "Stopped.";
        Plugin.Log.Information("[FateFinder] Stopped.");
    }

    // ── Framework tick ────────────────────────────────────────────────────────

    public void OnFrameworkUpdate(IFramework _)
    {
        if (State == FateFinderState.Stopped) return;
        if (Plugin.ClientState.LocalPlayer == null) return;

        switch (State)
        {
            case FateFinderState.Idle:          TickIdle();          break;
            case FateFinderState.MovingToFate:  TickMovingToFate();  break;
            case FateFinderState.InFate:        TickInFate();        break;
            case FateFinderState.FateComplete:  TickFateComplete();  break;
            case FateFinderState.Teleporting:   /* wait for event */ break;
            case FateFinderState.MovingToCenter:TickMovingToCenter();break;
        }
    }

    // ── State handlers ────────────────────────────────────────────────────────

    private void TickIdle()
    {
        // Must be in the correct zone
        if (Plugin.ClientState.TerritoryType != Config.SelectedZoneId)
        {
            var zone = ZoneData.GetByTerritoryId(Config.SelectedZoneId);
            StatusMessage = zone != null
                ? $"Not in {zone.Name}. Teleport there or use the idle behaviour options."
                : "Not in selected zone.";
            return;
        }

        var fate = SelectBestFate();
        if (fate != null)
        {
            _currentFateId = fate.FateId;
            State          = FateFinderState.MovingToFate;
            StatusMessage  = $"Moving to FATE: {fate.Name}";
            Plugin.Log.Debug($"[FateFinder] Targeting FATE {fate.FateId} '{fate.Name}' at {fate.Position}");

            _plugin.NavmeshIPC.PathfindAndMoveCloseTo(fate.Position, Config.UseFlight, fate.Radius * 0.8f);
            return;
        }

        // No FATEs — apply idle behaviour
        StatusMessage = "No FATEs active. Waiting...";
        ApplyIdleBehavior();
    }

    private void TickMovingToFate()
    {
        var fate = GetCurrentFate();
        if (fate == null || fate.State is FateState.Complete or FateState.Failed)
        {
            // FATE ended before we arrived
            Plugin.Log.Debug("[FateFinder] FATE ended during navigation — returning to Idle.");
            _plugin.NavmeshIPC.StopNavigation();
            _currentFateId = 0;
            State          = FateFinderState.Idle;
            StatusMessage  = "FATE ended. Scanning for next...";
            return;
        }

        UpdateCurrentFateInfo(fate);

        // Check arrival: within FATE radius
        var player   = Plugin.ClientState.LocalPlayer!.Position;
        var distance = Vector3.Distance(player, fate.Position);
        if (distance <= fate.Radius + 5f)
        {
            Plugin.Log.Debug($"[FateFinder] Arrived at FATE '{fate.Name}'.");
            _plugin.NavmeshIPC.StopNavigation();

            TryLevelSync(fate);

            _plugin.RotationSolverIPC.EnableAuto();
            _rsrWasEnabled = true;

            State         = FateFinderState.InFate;
            StatusMessage = $"In FATE: {fate.Name} ({fate.Progress}%)";
        }
    }

    private void TickInFate()
    {
        var fate = GetCurrentFate();

        if (fate == null || fate.State == FateState.Complete)
        {
            // FATE finished!
            Plugin.Log.Information($"[FateFinder] FATE '{CurrentFateName}' complete.");
            _plugin.RotationSolverIPC.Disable();
            _rsrWasEnabled = false;
            _currentFateId = 0;
            _cooldownEnds  = DateTime.Now.AddSeconds(2);
            State          = FateFinderState.FateComplete;
            StatusMessage  = "FATE complete! Searching for next...";
            return;
        }

        if (fate.State == FateState.Failed)
        {
            Plugin.Log.Warning($"[FateFinder] FATE '{fate.Name}' failed.");
            _plugin.RotationSolverIPC.Disable();
            _rsrWasEnabled = false;
            _currentFateId = 0;
            State          = FateFinderState.Idle;
            StatusMessage  = "FATE failed. Scanning for next...";
            return;
        }

        UpdateCurrentFateInfo(fate);

        // If we drifted outside the FATE circle, navigate back
        var player   = Plugin.ClientState.LocalPlayer!.Position;
        var distance = Vector3.Distance(player, fate.Position);
        if (distance > fate.Radius + 10f && !_plugin.NavmeshIPC.IsPathRunning)
        {
            Plugin.Log.Debug("[FateFinder] Drifted outside FATE — returning to centre.");
            _plugin.NavmeshIPC.PathfindAndMoveCloseTo(fate.Position, Config.UseFlight, fate.Radius * 0.5f);
        }
    }

    private void TickFateComplete()
    {
        if (DateTime.Now >= _cooldownEnds)
        {
            State = FateFinderState.Idle;
        }
    }

    private void TickMovingToCenter()
    {
        // If a FATE appeared while we were heading to centre, jump on it
        var fate = SelectBestFate();
        if (fate != null)
        {
            _plugin.NavmeshIPC.StopNavigation();
            State = FateFinderState.Idle;   // Will pick it up on next tick
            return;
        }

        if (!_plugin.NavmeshIPC.IsPathRunning && !_plugin.NavmeshIPC.IsPathfindInProgress)
        {
            State         = FateFinderState.Idle;
            StatusMessage = "Reached zone centre. Waiting for FATEs...";
        }
    }

    // ── Idle behaviour ────────────────────────────────────────────────────────

    private void ApplyIdleBehavior()
    {
        switch (Config.IdleBehavior)
        {
            case IdleBehavior.TeleportToAetheryte:
                TryTeleportToZone();
                break;

            case IdleBehavior.MoveToZoneCenter:
                MoveToZoneCenter();
                break;

            // WaitInPlace: nothing to do
        }
    }

    private void TryTeleportToZone()
    {
        // Only re-trigger if we're actually in the wrong zone
        if (Plugin.ClientState.TerritoryType == Config.SelectedZoneId) return;

        var zone = ZoneData.GetByTerritoryId(Config.SelectedZoneId);
        if (zone == null) return;

        if (_teleportIpc == null)
        {
            StatusMessage = "Teleporter plugin not found. Please install it.";
            return;
        }

        try
        {
            bool ok = _teleportIpc.InvokeFunc(zone.AetheryteId, 0);
            if (ok)
            {
                State         = FateFinderState.Teleporting;
                StatusMessage = $"Teleporting to {zone.AetheryteDisplayName}...";
                Plugin.Log.Information($"[FateFinder] Teleporting to aetheryte {zone.AetheryteId}.");
            }
            else
            {
                StatusMessage = $"Teleport to {zone.AetheryteDisplayName} failed. Are you in a safe location?";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[FateFinder] Teleport IPC error: {ex.Message}");
            StatusMessage = "Teleport failed (IPC error).";
        }
    }

    private void MoveToZoneCenter()
    {
        if (Plugin.ClientState.TerritoryType != Config.SelectedZoneId) return;
        if (_plugin.NavmeshIPC.IsPathRunning) return;

        var zone = ZoneData.GetByTerritoryId(Config.SelectedZoneId);
        if (zone == null) return;

        _plugin.NavmeshIPC.PathfindAndMoveTo(zone.ZoneCenter, Config.UseFlight);
        State         = FateFinderState.MovingToCenter;
        StatusMessage = "Moving to zone centre...";
    }

    // ── FATE selection ────────────────────────────────────────────────────────

    /// <summary>
    /// Priority algorithm:
    /// 1. Filter out FATEs with 0% progress AND less than 3 minutes remaining (hopeless).
    /// 2. FATEs with any progress > 0 rank higher (others are already fighting them).
    ///    Among those: higher progress = higher priority.
    /// 3. FATEs at 0% are ranked by lowest time-remaining first (most urgent).
    /// </summary>
    private IFate? SelectBestFate()
    {
        const float hopelessThreshold = 3 * 60f; // 3 minutes in seconds

        var candidates = Plugin.FateTable
            .Where(f => f.TerritoryType == Config.SelectedZoneId)
            .Where(f => f.State is FateState.Running or FateState.Preparation)
            .Where(f => !(f.Progress == 0 && f.State == FateState.Running && f.TimeRemaining < hopelessThreshold))
            .ToList();

        if (candidates.Count == 0) return null;

        return candidates
            .OrderByDescending(f => f.Progress > 0 ? 1 : 0)   // progress > 0 first
            .ThenByDescending(f => f.Progress)                  // highest progress wins
            .ThenBy(f => f.TimeRemaining)                       // at 0%, most urgent (least time)
            .First();
    }

    private IFate? GetCurrentFate()
    {
        if (_currentFateId == 0) return null;
        return Plugin.FateTable.FirstOrDefault(f => f.FateId == _currentFateId);
    }

    private void UpdateCurrentFateInfo(IFate fate)
    {
        CurrentFateName          = fate.Name.ToString();
        CurrentFateProgress      = fate.Progress;
        CurrentFateTimeRemaining = fate.TimeRemaining;
        StatusMessage            = $"FATE: {CurrentFateName} — {CurrentFateProgress}% ({TimeSpan.FromSeconds(CurrentFateTimeRemaining):mm\\:ss} left)";
    }

    // ── Level sync ────────────────────────────────────────────────────────────

    private void TryLevelSync(IFate fate)
    {
        if (!Config.AutoLevelSync) return;
        var player = Plugin.ClientState.LocalPlayer;
        if (player == null || player.Level <= fate.Level) return;

        Plugin.Log.Debug($"[FateFinder] Player level {player.Level} > FATE level {fate.Level} — attempting sync.");

        // ⚠️ TODO: Implement reliable level sync via ClientStructs.
        //    In FFXIV, level sync is triggered through the FATE HUD popup that appears
        //    when you enter the FATE circle while overlevelled.  The General Action
        //    approach below may not reliably trigger it on all game versions.
        //    A robust implementation should use FFXIVClientStructs to find the
        //    AgentFateReward/AddonFateReward and activate the sync button programmatically.
        //
        //    For now, ensure "Level Sync" is enabled in your in-game FATE settings,
        //    or click the sync button manually when it appears.
        try
        {
            unsafe
            {
                // General Action 14 has historically been used for Level Sync in some contexts.
                // Verify this ID for your game version.
                FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance()
                    ->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, 14);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug($"[FateFinder] Level sync action failed (non-critical): {ex.Message}");
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnTerritoryChanged(ushort territoryId)
    {
        if (State == FateFinderState.Teleporting)
        {
            if (territoryId == Config.SelectedZoneId)
            {
                State         = FateFinderState.Idle;
                StatusMessage = "Arrived in zone. Scanning for FATEs...";
                Plugin.Log.Information("[FateFinder] Teleport landed in target zone.");
            }
            else
            {
                // Teleport went somewhere unexpected; keep waiting
                Plugin.Log.Warning($"[FateFinder] TerritoryChanged to {territoryId}, expected {Config.SelectedZoneId}.");
            }
            return;
        }

        // Any unexpected zone change while running → stop to avoid confusion
        if (State != FateFinderState.Stopped && territoryId != Config.SelectedZoneId)
        {
            Plugin.Log.Information("[FateFinder] Zone changed unexpectedly — stopping.");
            Stop();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TryInitTeleporter()
    {
        try
        {
            _teleportIpc = Plugin.PluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");
            // Probe (this will throw if not installed)
            Plugin.Log.Debug("[FateFinder] Teleporter plugin found.");
        }
        catch
        {
            _teleportIpc = null;
            Plugin.Log.Debug("[FateFinder] Teleporter plugin not found — teleport idle behaviour unavailable.");
        }
    }
}
