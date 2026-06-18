// File location in project: Data/ZoneData.cs

using System.Collections.Generic;
using System.Numerics;

namespace FateFinder.Data;

/// <summary>
/// Describes a Bicolor Gemstone farming zone.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="TerritoryId">Dalamud TerritoryType ID for this zone.</param>
/// <param name="Expansion">Expansion label for UI grouping.</param>
/// <param name="AetheryteId">Lumina Aetheryte row ID of the zone's primary aetheryte (used for teleport).</param>
/// <param name="ZoneCenter">Approximate center-of-zone world position used for "Move to Zone Center" idle.</param>
/// <param name="AetheryteDisplayName">Human-readable aetheryte name shown in the UI.</param>
/// <remarks>
/// Territory IDs and Aetheryte IDs come from Lumina data.
/// Verify with: DataManager.GetExcelSheet&lt;TerritoryType&gt;() and DataManager.GetExcelSheet&lt;Aetheryte&gt;().
/// Dawntrail IDs (marked) are approximate — cross-check against live game data.
/// </remarks>
public sealed record ZoneInfo(
    string Name,
    uint TerritoryId,
    string Expansion,
    uint AetheryteId,
    Vector3 ZoneCenter,
    string AetheryteDisplayName);

public static class ZoneData
{
    /// <summary>All Bicolor Gemstone farming zones, grouped by expansion.</summary>
    public static readonly IReadOnlyList<ZoneInfo> BicolorZones = new List<ZoneInfo>
    {
        // ─── Shadowbringers (5.x) ────────────────────────────────────────
        new("Lakeland",               813,  "Shadowbringers", 132, new Vector3(  20f,  -22f,  592f), "Fort Jobb"),
        new("Kholusia",               814,  "Shadowbringers", 134, new Vector3(-326f,   55f, -416f), "Stilltide"),
        new("Amh Araeng",             815,  "Shadowbringers", 136, new Vector3( 270f,   -8f,  -15f), "Mord Souq"),
        new("Il Mheg",                816,  "Shadowbringers", 133, new Vector3(-105f,   50f, -572f), "Lydha Lran"),
        new("The Rak'tika Greatwood", 817,  "Shadowbringers", 135, new Vector3( -26f,   -8f, -148f), "Slitherbough"),
        new("The Tempest",            818,  "Shadowbringers", 137, new Vector3(-745f, -775f,  130f), "The Ondo Cups"),

        // ─── Endwalker (6.x) ─────────────────────────────────────────────
        new("Labyrinthos",            956,  "Endwalker",      166, new Vector3(-182f,    8f,  230f), "The Archeion"),
        new("Thavnair",               957,  "Endwalker",      167, new Vector3( 130f,    5f, -310f), "Yedlihmad"),
        new("Garlemald",              958,  "Endwalker",      168, new Vector3( 395f,   50f,  530f), "Camp Broken Glass"),
        new("Mare Lamentorum",        959,  "Endwalker",      169, new Vector3(-320f, -160f,  395f), "Sinus Lacrimarum"),
        new("Ultima Thule",           960,  "Endwalker",      171, new Vector3( 430f,  290f,  198f), "Base Omicron"),
        new("Elpis",                  961,  "Endwalker",      170, new Vector3(-330f,   35f, -440f), "Anagnorisis"),

        // ─── Dawntrail (7.x) — verify IDs against live Lumina ───────────
        new("Urqopacha",              1185, "Dawntrail",      243, new Vector3( -45f,   85f,  420f), "Wachunpelo"),
        new("Kozama'uka",             1186, "Dawntrail",      244, new Vector3( -64f,   25f, -370f), "Ok'hanu"),
        new("Yak T'el",               1187, "Dawntrail",      245, new Vector3( 230f,   95f,  -85f), "Hhusatahwi"),
        new("Shaaloani",              1188, "Dawntrail",      246, new Vector3(  -8f,   10f,   50f), "Alaqa"),
        new("Heritage Found",         1189, "Dawntrail",      247, new Vector3(-350f,   35f,  -85f), "The Outskirts"),
        new("Living Memory",          1190, "Dawntrail",      248, new Vector3(   0f,    0f,    0f), "Skoenskoenval"),
    };

    /// <summary>Returns zone info for a given TerritoryType ID, or null if not found.</summary>
    public static ZoneInfo? GetByTerritoryId(uint territoryId)
    {
        foreach (var z in BicolorZones)
            if (z.TerritoryId == territoryId) return z;
        return null;
    }
}
