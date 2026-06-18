// File location in project: IPC/NavmeshIPC.cs

using Dalamud.Plugin.Ipc;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace FateFinder.IPC;

/// <summary>
/// Wraps all IPC calls to the vnavmesh plugin (awgil/ffxiv_navmesh).
/// All methods return safe defaults if vnavmesh is not installed or not ready.
/// IPC method names sourced from: vnavmesh/IPCProvider.cs
/// </summary>
public sealed class NavmeshIPC : IDisposable
{
    // ── Nav state ──────────────────────────────────────────────────────────
    private readonly ICallGateSubscriber<bool>                           _isReady;
    private readonly ICallGateSubscriber<float>                          _buildProgress;

    // ── Simple move (pathfind + follow in one call) ────────────────────────
    private readonly ICallGateSubscriber<Vector3, bool, bool>            _simpleMoveTo;
    private readonly ICallGateSubscriber<Vector3, bool, float, bool>     _simpleMoveCloseTo;
    private readonly ICallGateSubscriber<bool>                           _simpleMoveInProgress;

    // ── Path control ───────────────────────────────────────────────────────
    private readonly ICallGateSubscriber<object>                         _pathStop;
    private readonly ICallGateSubscriber<bool>                           _pathIsRunning;
    private readonly ICallGateSubscriber<bool>                           _pathMovementAllowed;
    private readonly ICallGateSubscriber<bool, object>                   _pathSetMovementAllowed;

    public NavmeshIPC()
    {
        var pi = Plugin.PluginInterface;

        _isReady               = pi.GetIpcSubscriber<bool>                          ("vnavmesh.Nav.IsReady");
        _buildProgress         = pi.GetIpcSubscriber<float>                         ("vnavmesh.Nav.BuildProgress");

        _simpleMoveTo          = pi.GetIpcSubscriber<Vector3, bool, bool>           ("vnavmesh.SimpleMove.PathfindAndMoveTo");
        _simpleMoveCloseTo     = pi.GetIpcSubscriber<Vector3, bool, float, bool>    ("vnavmesh.SimpleMove.PathfindAndMoveCloseTo");
        _simpleMoveInProgress  = pi.GetIpcSubscriber<bool>                          ("vnavmesh.SimpleMove.PathfindInProgress");

        _pathStop              = pi.GetIpcSubscriber<object>                        ("vnavmesh.Path.Stop");
        _pathIsRunning         = pi.GetIpcSubscriber<bool>                          ("vnavmesh.Path.IsRunning");
        _pathMovementAllowed   = pi.GetIpcSubscriber<bool>                          ("vnavmesh.Path.GetMovementAllowed");
        _pathSetMovementAllowed= pi.GetIpcSubscriber<bool, object>                  ("vnavmesh.Path.SetMovementAllowed");
    }

    public void Dispose() { }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>True when the navmesh for the current zone is built and ready.</summary>
    public bool IsReady
    {
        get { try { return _isReady.InvokeFunc(); } catch { return false; } }
    }

    /// <summary>Returns build progress 0–1, or -1 if no build is in progress.</summary>
    public float BuildProgress
    {
        get { try { return _buildProgress.InvokeFunc(); } catch { return -1f; } }
    }

    /// <summary>
    /// Pathfind and move to <paramref name="dest"/>.
    /// Returns true if the request was accepted.
    /// </summary>
    public bool PathfindAndMoveTo(Vector3 dest, bool fly)
    {
        try { return _simpleMoveTo.InvokeFunc(dest, fly); }
        catch { return false; }
    }

    /// <summary>
    /// Pathfind and move to within <paramref name="range"/> metres of <paramref name="dest"/>.
    /// </summary>
    public bool PathfindAndMoveCloseTo(Vector3 dest, bool fly, float range)
    {
        try { return _simpleMoveCloseTo.InvokeFunc(dest, fly, range); }
        catch { return false; }
    }

    /// <summary>True while a pathfind request is being processed.</summary>
    public bool IsPathfindInProgress
    {
        get { try { return _simpleMoveInProgress.InvokeFunc(); } catch { return false; } }
    }

    /// <summary>True while the character is actively following a path.</summary>
    public bool IsPathRunning
    {
        get { try { return _pathIsRunning.InvokeFunc(); } catch { return false; } }
    }

    /// <summary>Stop all active navigation immediately.</summary>
    public void StopNavigation()
    {
        try { _pathStop.InvokeAction(); } catch { }
    }

    /// <summary>Whether vnavmesh is currently allowed to move the character.</summary>
    public bool IsMovementAllowed
    {
        get { try { return _pathMovementAllowed.InvokeFunc(); } catch { return false; } }
        set { try { _pathSetMovementAllowed.InvokeAction(value); } catch { } }
    }

    /// <summary>True if vnavmesh is installed and functional.</summary>
    public bool IsAvailable
    {
        get
        {
            try { _isReady.InvokeFunc(); return true; }
            catch { return false; }
        }
    }
}
