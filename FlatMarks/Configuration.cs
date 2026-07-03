using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Dalamud.Configuration;
using FlatMarks.Rendering;

namespace FlatMarks;

public enum GlyphMode
{
    /// <summary>Project a glyph texture flat onto the floor (default, stays flat).</summary>
    ProjectedImage,
    /// <summary>Pictomancy world-space text (billboarded, falls back to ImGui — no UI masking/depth).</summary>
    BillboardText,
    /// <summary>No glyph, shape only.</summary>
    None,
    /// <summary>Glyph texture as a camera-facing billboard, like the native floating waymark letters.</summary>
    BillboardImage,
    /// <summary>Glyph stays flat on the floor but yaw-rotates to face the camera (reads upright from any angle).</summary>
    FlatFacing,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ---- Master ----
    public bool MasterEnabled { get; set; } = true;

    // ---- Per-marker (index order A,B,C,D,1,2,3,4) ----
    public bool[] MarkerEnabled { get; set; } = { true, true, true, true, true, true, true, true };
    public uint[] MarkerColors { get; set; } = (uint[])MarkerStyle.DefaultColors.Clone();
    /// <summary>Per-marker opacity multiplier (0..1), applied on top of the global fill/outline opacity.</summary>
    public float[] MarkerOpacity { get; set; } = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    // ---- User-saved color schemes ----
    public List<ColorScheme> SavedSchemes { get; set; } = new();

    /// <summary>Copy a scheme's colors onto the live markers.</summary>
    public void ApplyScheme(ColorScheme scheme)
    {
        if (scheme.Colors is { Length: 8 })
            MarkerColors = (uint[])scheme.Colors.Clone();
    }

    // ---- Geometry / appearance ----
    public float Radius { get; set; } = 1.25f;         // native circle radius; 0.5 .. 3.0
    public float FillOpacity { get; set; } = 0.35f;    // 0 .. 1
    public float OutlineOpacity { get; set; } = 1.0f;  // 0 .. 1
    public float OutlineThickness { get; set; } = 3.0f;// px
    public float HeightOffset { get; set; } = 0.05f;   // world units above the marker Position.Y
    public float MaxDistance { get; set; } = 200.0f;   // distance cull (yalms)

    /// <summary>
    /// Dim marker pixels that are behind scene geometry (occlusion). When on, Pictomancy samples the
    /// scene depth per-pixel — which makes markers shimmer/warp where transparent particle VFX (dust,
    /// haze) drift through, common in instances. Turn off to draw at full alpha (also shows through walls).
    /// </summary>
    public bool DimBehindWalls { get; set; } = true;

    // ---- Glyph ----
    public bool GlyphEnabled { get; set; } = true;
    public GlyphMode GlyphMode { get; set; } = GlyphMode.ProjectedImage;
    public float GlyphScale { get; set; } = 1.0f;      // glyph size as a multiple of Radius; 0.3 .. 3.0

    // ---- Native waymarks (Phase 3) ----
    /// <summary>Per-marker: hide the game's native 3D waymark VFX (pillar + floating letter) via alpha.</summary>
    public bool[] HideNativeWaymark { get; set; } = new bool[8];

    // ---- Internal / advanced ----
    /// <summary>Force the ImGui WorldToScreen fallback renderer instead of Pictomancy (dev flag).</summary>
    public bool ForceFallbackRenderer { get; set; } = false;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    // ---- Whole-settings share code (excludes saved schemes, which have their own codes) ----
    private const string ProfilePrefix = "FMSET1:";

    public string ExportProfile()
    {
        var p = new ConfigProfile
        {
            MasterEnabled = MasterEnabled,
            MarkerEnabled = MarkerEnabled,
            MarkerColors = MarkerColors,
            MarkerOpacity = MarkerOpacity,
            Radius = Radius,
            FillOpacity = FillOpacity,
            OutlineOpacity = OutlineOpacity,
            OutlineThickness = OutlineThickness,
            HeightOffset = HeightOffset,
            MaxDistance = MaxDistance,
            DimBehindWalls = DimBehindWalls,
            GlyphEnabled = GlyphEnabled,
            GlyphMode = (int)GlyphMode,
            GlyphScale = GlyphScale,
            HideNativeWaymark = HideNativeWaymark,
        };
        var json = JsonSerializer.Serialize(p);
        return ProfilePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>Apply a settings share code to this config. Returns false (and changes nothing) on bad input.</summary>
    public bool TryImportProfile(string code)
    {
        try
        {
            code = code.Trim();
            if (code.StartsWith(ProfilePrefix, StringComparison.Ordinal))
                code = code[ProfilePrefix.Length..];

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(code));
            var p = JsonSerializer.Deserialize<ConfigProfile>(json);
            if (p is null) return false;

            MasterEnabled = p.MasterEnabled;
            if (p.MarkerEnabled is { Length: 8 }) MarkerEnabled = (bool[])p.MarkerEnabled.Clone();
            if (p.MarkerColors is { Length: 8 }) MarkerColors = (uint[])p.MarkerColors.Clone();
            if (p.MarkerOpacity is { Length: 8 }) MarkerOpacity = (float[])p.MarkerOpacity.Clone();
            Radius = p.Radius;
            FillOpacity = p.FillOpacity;
            OutlineOpacity = p.OutlineOpacity;
            OutlineThickness = p.OutlineThickness;
            HeightOffset = p.HeightOffset;
            MaxDistance = p.MaxDistance;
            DimBehindWalls = p.DimBehindWalls;
            GlyphEnabled = p.GlyphEnabled;
            GlyphMode = Enum.IsDefined(typeof(GlyphMode), p.GlyphMode) ? (GlyphMode)p.GlyphMode : GlyphMode;
            GlyphScale = p.GlyphScale;
            if (p.HideNativeWaymark is { Length: 8 }) HideNativeWaymark = (bool[])p.HideNativeWaymark.Clone();

            Sanitize();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Restore all visual defaults (keeps enable toggles as-is).</summary>
    public void ResetToDefaults()
    {
        MarkerColors = (uint[])MarkerStyle.DefaultColors.Clone();
        MarkerOpacity = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        Radius = 1.25f;
        FillOpacity = 0.35f;
        OutlineOpacity = 1.0f;
        OutlineThickness = 3.0f;
        HeightOffset = 0.05f;
        MaxDistance = 200.0f;
        DimBehindWalls = true;
        GlyphEnabled = true;
        GlyphMode = GlyphMode.ProjectedImage;
        GlyphScale = 1.0f;
    }

    /// <summary>Repair arrays if a stale/older config deserialized with the wrong length.</summary>
    public void Sanitize()
    {
        if (MarkerEnabled is not { Length: 8 })
        {
            var fixedEnabled = new bool[8];
            for (var i = 0; i < 8; i++) fixedEnabled[i] = MarkerEnabled is not null && i < MarkerEnabled.Length ? MarkerEnabled[i] : true;
            MarkerEnabled = fixedEnabled;
        }
        if (MarkerColors is not { Length: 8 })
        {
            var fixedColors = (uint[])MarkerStyle.DefaultColors.Clone();
            if (MarkerColors is not null)
                for (var i = 0; i < 8 && i < MarkerColors.Length; i++) fixedColors[i] = MarkerColors[i];
            MarkerColors = fixedColors;
        }
        if (MarkerOpacity is not { Length: 8 })
        {
            var fixedOp = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            if (MarkerOpacity is not null)
                for (var i = 0; i < 8 && i < MarkerOpacity.Length; i++) fixedOp[i] = MarkerOpacity[i];
            MarkerOpacity = fixedOp;
        }
        if (HideNativeWaymark is not { Length: 8 })
        {
            var fixedHide = new bool[8];
            if (HideNativeWaymark is not null)
                for (var i = 0; i < 8 && i < HideNativeWaymark.Length; i++) fixedHide[i] = HideNativeWaymark[i];
            HideNativeWaymark = fixedHide;
        }
    }
}

/// <summary>Serializable subset of settings shared by the whole-settings export code (no saved schemes).</summary>
public class ConfigProfile
{
    public bool MasterEnabled { get; set; } = true;
    public bool[] MarkerEnabled { get; set; } = { true, true, true, true, true, true, true, true };
    public uint[] MarkerColors { get; set; } = new uint[8];
    public float[] MarkerOpacity { get; set; } = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    public float Radius { get; set; } = 1.25f;
    public float FillOpacity { get; set; } = 0.35f;
    public float OutlineOpacity { get; set; } = 1.0f;
    public float OutlineThickness { get; set; } = 3.0f;
    public float HeightOffset { get; set; } = 0.05f;
    public float MaxDistance { get; set; } = 200.0f;
    public bool DimBehindWalls { get; set; } = true;
    public bool GlyphEnabled { get; set; } = true;
    public int GlyphMode { get; set; }
    public float GlyphScale { get; set; } = 1.0f;
    public bool[] HideNativeWaymark { get; set; } = new bool[8];
}
