// File location in project: Windows/ConfigWindow.cs

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace FateFinder.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private Configuration   Config => _plugin.Configuration;

    public ConfigWindow(Plugin plugin)
        : base("FATE Finder — Settings##ConfigWindow",
               ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar)
    {
        _plugin = plugin;
        Size        = new Vector2(380, 200);
        SizeCondition = Dalamud.Interface.Windowing.ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextWrapped("All main settings are available on the main window (▶ Start / ■ Stop).");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text("FATE Finder v1.0");
        ImGui.TextDisabled("By Dimascus — github.com/Dimascus/FATE-Finder");
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f),
            "Requires: vnavmesh + Rotation Solver Reborn");
    }
}
