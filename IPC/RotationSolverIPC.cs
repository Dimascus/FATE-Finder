// File location in project: IPC/RotationSolverIPC.cs

using Dalamud.Plugin.Ipc;
using System;

namespace FateFinder.IPC;

/// <summary>
/// Operation modes exposed by Rotation Solver Reborn.
/// Must match the OperationMode enum in RotationSolver.Basic.
/// </summary>
public enum RSROperationMode : byte
{
    Nothing = 0,  // RSR is off
    Manual  = 1,  // RSR in suggestion/manual mode
    Auto    = 2,  // RSR in full auto mode
}

/// <summary>
/// Wraps IPC calls to the Rotation Solver Reborn plugin (FFXIV-CombatReborn/RotationSolverReborn).
///
/// ⚠️ IMPORTANT — verify IPC method names before shipping:
///   The names below ("RotationSolverReborn.GetOperationMode" / "SetOperationMode")
///   are based on the known RSR IPC pattern.  Open RSR's source and search for
///   GetIpcProvider calls to confirm the exact registered strings for your version.
///
/// All methods degrade gracefully (no-op / false) when RSR is not installed.
/// </summary>
public sealed class RotationSolverIPC : IDisposable
{
    private ICallGateSubscriber<byte>?        _getOperationMode;
    private ICallGateSubscriber<byte, object>? _setOperationMode;

    // Cached availability flag — re-checked on each Enable/Disable call
    private bool _available;

    public RotationSolverIPC()
    {
        TryInitialize();
    }

    private void TryInitialize()
    {
        try
        {
            _getOperationMode = Plugin.PluginInterface.GetIpcSubscriber<byte>        ("RotationSolverReborn.GetOperationMode");
            _setOperationMode = Plugin.PluginInterface.GetIpcSubscriber<byte, object>("RotationSolverReborn.SetOperationMode");

            // Probe to confirm RSR is actually loaded
            _getOperationMode.InvokeFunc();
            _available = true;
            Plugin.Log.Information("[RSR IPC] Rotation Solver Reborn connected.");
        }
        catch
        {
            _available = false;
            Plugin.Log.Warning("[RSR IPC] Rotation Solver Reborn not found — combat automation disabled.");
        }
    }

    public void Dispose() { }

    /// <summary>True if Rotation Solver Reborn is installed and responding.</summary>
    public bool IsAvailable => _available;

    /// <summary>Returns the current operation mode, or Nothing if RSR is unavailable.</summary>
    public RSROperationMode GetMode()
    {
        if (!_available) return RSROperationMode.Nothing;
        try { return (RSROperationMode)_getOperationMode!.InvokeFunc(); }
        catch { _available = false; return RSROperationMode.Nothing; }
    }

    /// <summary>Switch RSR to full Auto mode (combat will be handled automatically).</summary>
    public void EnableAuto()
    {
        if (!_available) { TryInitialize(); }
        if (!_available) return;
        try
        {
            _setOperationMode!.InvokeAction((byte)RSROperationMode.Auto);
            Plugin.Log.Debug("[RSR IPC] Set mode → Auto");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[RSR IPC] EnableAuto failed: {ex.Message}");
            _available = false;
        }
    }

    /// <summary>Turn RSR off (Nothing mode).</summary>
    public void Disable()
    {
        if (!_available) return;
        try
        {
            _setOperationMode!.InvokeAction((byte)RSROperationMode.Nothing);
            Plugin.Log.Debug("[RSR IPC] Set mode → Nothing");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning($"[RSR IPC] Disable failed: {ex.Message}");
            _available = false;
        }
    }
}
