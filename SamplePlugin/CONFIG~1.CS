using Dalamud.Configuration;
using System;

namespace FateFinder;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    /// <summary>Territory ID of the zone to farm. 0 means none selected.</summary>
    public uint SelectedZoneId { get; set; } = 0;

    /// <summary>What the plugin does while waiting for a FATE to spawn.</summary>
    public IdleBehavior IdleBehavior { get; set; } = IdleBehavior.WaitInPlace;

    /// <summary>Use flight when navigating (requires unlocked flight in zone).</summary>
    public bool UseFlight { get; set; } = true;

    /// <summary>Attempt to level-sync automatically when entering a FATE.</summary>
    public bool AutoLevelSync { get; set; } = true;

    /// <summary>Skip Collection-type FATEs (gather/deliver items).</summary>
    public bool SkipCollectionFates { get; set; } = true;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

public enum IdleBehavior
{
    /// <summary>Stand still and poll until a FATE spawns.</summary>
    WaitInPlace,

    /// <summary>Teleport to the zone's nearest aetheryte (requires Teleporter plugin).</summary>
    TeleportToAetheryte,

    /// <summary>Fly to the zone's center point and wait there.</summary>
    MoveToZoneCenter
}
