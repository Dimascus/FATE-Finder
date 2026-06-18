// File location in project: Windows/MainWindow.cs

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FateFinder.Data;
using FateFinder.Services;
using System;
using System.Numerics;

namespace FateFinder.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    // Keep a local reference so we don't call the property repeatedly
    private Configuration   Config  => _plugin.Configuration;
    private FateManager     Manager => _plugin.FateManager;

    public MainWindow(Plugin plugin)
        : base("FATE Finder##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(700, 800),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatusBar();
        ImGui.Separator();
        DrawZoneSelector();
        ImGui.Separator();
        DrawControls();
        ImGui.Separator();
        DrawOptions();
        ImGui.Separator();
        DrawPluginStatus();
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private void DrawStatusBar()
    {
        var state = Manager.State;
        var colour = state switch
        {
            FateFinderState.Stopped        => new Vector4(0.6f, 0.6f, 0.6f, 1f),  // grey
            FateFinderState.Idle           => new Vector4(1.0f, 0.8f, 0.2f, 1f),  // yellow
            FateFinderState.MovingToFate   => new Vector4(0.3f, 0.7f, 1.0f, 1f),  // blue
            FateFinderState.InFate         => new Vector4(0.2f, 1.0f, 0.4f, 1f),  // green
            FateFinderState.FateComplete   => new Vector4(0.6f, 1.0f, 0.6f, 1f),  // light green
            FateFinderState.Teleporting    => new Vector4(0.9f, 0.5f, 1.0f, 1f),  // purple
            FateFinderState.MovingToCenter => new Vector4(0.3f, 0.7f, 1.0f, 1f),  // blue
            _                              => new Vector4(1f,   1f,   1f,   1f),
        };

        using (ImRaii.PushColor(ImGuiCol.Text, colour))
            ImGui.Text($"● {state}");

        ImGui.SameLine();
        ImGui.TextDisabled("—");
        ImGui.SameLine();
        ImGui.TextWrapped(Manager.StatusMessage);

        // FATE progress bar when in FATE
        if (state is FateFinderState.InFate or FateFinderState.MovingToFate && Manager.CurrentFateProgress > 0)
        {
            ImGui.Spacing();
            float progress = Manager.CurrentFateProgress / 100f;
            ImGui.ProgressBar(progress, new Vector2(-1, 0), $"{Manager.CurrentFateName}  {Manager.CurrentFateProgress}%");
        }
    }

    // ── Zone selector ─────────────────────────────────────────────────────────

    private void DrawZoneSelector()
    {
        ImGui.Text("Zone to farm:");
        ImGui.SetNextItemWidth(-1);

        var selectedZone = ZoneData.GetByTerritoryId(Config.SelectedZoneId);
        var previewLabel = selectedZone?.Name ?? "— Select a zone —";

        using var combo = ImRaii.Combo("##ZoneCombo", previewLabel);
        if (!combo.Success) return;

        string? lastExpansion = null;
        foreach (var zone in ZoneData.BicolorZones)
        {
            // Expansion header
            if (zone.Expansion != lastExpansion)
            {
                if (lastExpansion != null) ImGui.Separator();
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1f, 0.8f, 0.3f, 1f)))
                    ImGui.Text(zone.Expansion);
                lastExpansion = zone.Expansion;
            }

            bool isSelected = Config.SelectedZoneId == zone.TerritoryId;
            using (ImRaii.PushIndent(12f))
            {
                if (ImGui.Selectable(zone.Name, isSelected))
                {
                    Config.SelectedZoneId = zone.TerritoryId;
                    Config.Save();
                    // If running, stop so the new zone takes effect cleanly
                    if (Manager.State != FateFinderState.Stopped)
                        Manager.Stop();
                }
            }

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }
    }

    // ── Start / Stop ──────────────────────────────────────────────────────────

    private void DrawControls()
    {
        bool isRunning = Manager.State != FateFinderState.Stopped;

        if (isRunning)
        {
            using (ImRaii.PushColor(ImGuiCol.Button,        new Vector4(0.7f, 0.1f, 0.1f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.2f, 0.2f, 1f)))
            {
                if (ImGui.Button("■  Stop", new Vector2(120, 0)))
                    Manager.Stop();
            }
        }
        else
        {
            bool canStart = Config.SelectedZoneId != 0 && _plugin.NavmeshIPC.IsAvailable;
            using (ImRaii.Disabled(!canStart))
            using (ImRaii.PushColor(ImGuiCol.Button,        new Vector4(0.1f, 0.6f, 0.1f, 1f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.8f, 0.2f, 1f)))
            {
                if (ImGui.Button("▶  Start", new Vector2(120, 0)))
                    Manager.Start();
            }

            if (Config.SelectedZoneId == 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f), "← Select a zone first");
            }
            else if (!_plugin.NavmeshIPC.IsAvailable)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "vnavmesh not found!");
            }
        }
    }

    // ── Options ───────────────────────────────────────────────────────────────

    private void DrawOptions()
    {
        ImGui.Text("Options");
        ImGui.Spacing();

        // Flight toggle
        bool useFlight = Config.UseFlight;
        if (ImGui.Checkbox("Use flight when navigating", ref useFlight))
        {
            Config.UseFlight = useFlight;
            Config.Save();
        }

        // Level sync toggle
        bool autoSync = Config.AutoLevelSync;
        if (ImGui.Checkbox("Attempt auto level sync on FATE entry", ref autoSync))
        {
            Config.AutoLevelSync = autoSync;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Tries to trigger level sync via a GeneralAction.\nFor best results also enable Level Sync in your in-game FATE settings.");

        ImGui.Spacing();
        ImGui.Text("Idle behaviour (when no FATEs are active):");
        ImGui.Spacing();

        DrawIdleBehaviourCheckbox("Wait in place",                  IdleBehavior.WaitInPlace);
        DrawIdleBehaviourCheckbox("Teleport to zone aetheryte",     IdleBehavior.TeleportToAetheryte,
            "Requires the Teleporter plugin by Pohky.");
        DrawIdleBehaviourCheckbox("Fly to zone centre",             IdleBehavior.MoveToZoneCenter);
    }

    private void DrawIdleBehaviourCheckbox(string label, IdleBehavior behaviour, string? tooltip = null)
    {
        bool isChecked = Config.IdleBehavior == behaviour;
        if (ImGui.Checkbox(label, ref isChecked) && isChecked)
        {
            Config.IdleBehavior = behaviour;
            Config.Save();
        }
        if (tooltip != null && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    // ── Plugin status ─────────────────────────────────────────────────────────

    private void DrawPluginStatus()
    {
        ImGui.Text("Plugin Dependencies:");
        ImGui.Spacing();

        DrawDependencyStatus("vnavmesh",                _plugin.NavmeshIPC.IsAvailable);
        DrawDependencyStatus("Rotation Solver Reborn",  _plugin.RotationSolverIPC.IsAvailable);

        // Show navmesh build progress if building
        if (_plugin.NavmeshIPC.IsAvailable && !_plugin.NavmeshIPC.IsReady)
        {
            float prog = _plugin.NavmeshIPC.BuildProgress;
            if (prog >= 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"(building navmesh {prog * 100:0}%)");
            }
        }
    }

    private static void DrawDependencyStatus(string name, bool available)
    {
        var (icon, colour) = available
            ? ("✔", new Vector4(0.2f, 1f, 0.4f, 1f))
            : ("✘", new Vector4(1f,   0.3f, 0.3f, 1f));

        ImGui.TextColored(colour, icon);
        ImGui.SameLine();
        ImGui.Text(name);
    }
}
