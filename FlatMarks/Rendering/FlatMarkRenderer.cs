using System;
using System.IO;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FlatMarks.Data;
using Pictomancy;

namespace FlatMarks.Rendering;

/// <summary>
/// Primary renderer: draws each active waymark as a flat, world-space shape (circle for letters,
/// square for numbers) with an optional floor-projected glyph, using Pictomancy. World-space fills
/// project onto terrain so markers follow uneven floors (spec 1b/1c).
/// </summary>
public sealed class FlatMarkRenderer : IDisposable
{
    private readonly Configuration config;
    private readonly ITextureProvider textureProvider;
    private readonly string resDir;

    // Camera yaw, refreshed once per Draw() for the flat-facing glyph mode.
    private float camYaw;
    private bool haveCam;

    public FlatMarkRenderer(Configuration config, ITextureProvider textureProvider, IDalamudPluginInterface pluginInterface)
    {
        this.config = config;
        this.textureProvider = textureProvider;
        this.resDir = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName ?? ".", "res");
    }

    public void Dispose() { }

    /// <summary>Draw all supplied markers. <paramref name="origin"/> is the cull reference (player position).</summary>
    public void Draw(ReadOnlySpan<WaymarkState> markers, Vector3 origin)
    {
        // Pictomancy composites on dispose of the returned list. Null-check is mandatory.
        using var drawList = PctService.Draw(hints: new()
        {
            DefaultParams = new() { OcclusionTolerance = 0.5f, OccludedAlpha = 0.35f },
        });
        if (drawList == null) return;

        // Read the camera yaw once per frame for the flat-facing glyph mode.
        haveCam = CameraReader.TryGetYaw(out camYaw);

        for (var i = 0; i < markers.Length; i++)
        {
            var wm = markers[i];
            if (!wm.Active || !config.MarkerEnabled[wm.Index]) continue;

            var p = wm.Position with { Y = wm.Position.Y + config.HeightOffset };
            if (Vector3.Distance(origin, p) > config.MaxDistance) continue;

            var baseColor = config.MarkerColors[wm.Index];
            var markerOpacity = config.MarkerOpacity[wm.Index];
            var fill = MarkerStyle.WithAlpha(baseColor, config.FillOpacity * markerOpacity);
            var outline = MarkerStyle.WithAlpha(baseColor, config.OutlineOpacity * markerOpacity);
            var radius = config.Radius;

            if (MarkerStyle.Shapes[wm.Index] == MarkerShape.Circle)
                DrawCircle(drawList, p, radius, fill, outline);
            else
                DrawSquare(drawList, p, radius, fill, outline);

            DrawGlyph(drawList, wm.Index, p, radius, markerOpacity);
        }
    }

    private void DrawCircle(PctDrawList drawList, Vector3 p, float radius, uint fill, uint outline)
    {
        drawList.AddCircleFilled(p, radius, fill);
        if (config.OutlineThickness > 0f)
            drawList.AddCircle(p, radius, outline, numSegments: 0, thickness: config.OutlineThickness);
    }

    private void DrawSquare(PctDrawList drawList, Vector3 p, float radius, uint fill, uint outline)
    {
        // Axis-aligned square in the world XZ plane. Native squares are narrower than circles
        // (half-width 1.1 vs radius 1.25), so scale to match the game (spec 1c).
        var h = radius * MarkerStyle.SquareToCircleRatio;
        var a = p with { X = p.X - h, Z = p.Z - h };
        var b = p with { X = p.X + h, Z = p.Z - h };
        var c = p with { X = p.X + h, Z = p.Z + h };
        var d = p with { X = p.X - h, Z = p.Z + h };

        drawList.AddQuadFilled(a, b, c, d, fill);
        if (config.OutlineThickness > 0f)
            drawList.AddQuad(a, b, c, d, outline, thickness: config.OutlineThickness);
    }

    private void DrawGlyph(PctDrawList drawList, int index, Vector3 p, float radius, float opacity)
    {
        if (!config.GlyphEnabled || config.GlyphMode == GlyphMode.None) return;
        if (opacity <= 0.01f) return; // fully-dimmed marker: hide the glyph too

        if (config.GlyphMode == GlyphMode.BillboardText)
        {
            // Camera-facing text fallback: no occlusion / UI mask (spec note). Nudged up to sit above the fill.
            var textColor = MarkerStyle.WithAlpha(0xFFFFFFFF, opacity);
            drawList.AddText(p with { Y = p.Y + 0.02f }, textColor, MarkerStyle.Labels[index], scale: radius * config.GlyphScale);
            return;
        }

        var wrap = LoadGlyph(index);
        if (wrap == null) return;

        var side = radius * config.GlyphScale;

        if (config.GlyphMode == GlyphMode.BillboardImage)
        {
            // Camera-facing quad, like the native floating waymark letters. Pictomancy handles the
            // camera math. Lift the center by half its height so it rests on the marker.
            var pos = p with { Y = p.Y + side * 0.5f };
            drawList.AddBillboard(wrap.Handle, pos, new Vector2(side, side));
            return;
        }

        // Flat-on-floor textured quad: right along +X, down along +Z (PictomancyDemo pattern).
        var right = new Vector3(side, 0, 0);
        var down = new Vector3(0, 0, side);

        if (config.GlyphMode == GlyphMode.FlatFacing && haveCam)
        {
            // Yaw-rotate the flat quad so its bottom edge points toward the camera → reads upright
            // from any angle while staying on the floor. Pure rotation of the default (down=+Z)
            // frame, so the glyph never mirrors — only the spin direction matters, and it must
            // match DirH (0 = north, increases clockwise).
            var s = MathF.Sin(camYaw);
            var c = MathF.Cos(camYaw);
            right = new Vector3(c * side, 0, -s * side);
            down = new Vector3(s * side, 0, c * side);
        }

        drawList.AddImage(wrap.Handle, p with { Y = p.Y + 0.02f }, right, down);
    }

    private Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? LoadGlyph(int index)
    {
        try
        {
            var path = Path.Combine(resDir, MarkerStyle.GlyphFile(index));
            // Dalamud caches the GPU texture; GetWrapOrEmpty returns a provider-owned wrap (do not dispose).
            return textureProvider.GetFromFile(path).GetWrapOrEmpty();
        }
        catch
        {
            return null;
        }
    }
}
