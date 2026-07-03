using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using FlatMarks.Rendering;
using Dalamud.Bindings.ImGui;

namespace FlatMarks.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration config;
    private readonly NativeWaymarkVfx nativeVfx;

    private int selectedScheme;
    private string newSchemeName = string.Empty;
    private string importCode = string.Empty;
    private string schemeStatus = string.Empty;
    private string profileCode = string.Empty;
    private string profileStatus = string.Empty;

    public ConfigWindow(Configuration config, NativeWaymarkVfx nativeVfx)
        : base("FlatMarks Settings###FlatMarksConfig")
    {
        this.config = config;
        this.nativeVfx = nativeVfx;
        Size = new Vector2(420, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var master = config.MasterEnabled;
        if (ImGui.Checkbox("Enable FlatMarks", ref master))
        {
            config.MasterEnabled = master;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(/flatmarks toggle)");

        ImGui.Separator();

        // Logical flow: the markers → their colors → physical style → the letters → the game's originals.
        if (ImGui.CollapsingHeader("FlatMarks", ImGuiTreeNodeFlags.DefaultOpen))
            DrawMarkerGrid();

        if (ImGui.CollapsingHeader("Color schemes"))
            DrawColorSchemes();

        if (ImGui.CollapsingHeader("Size & Shape"))
            DrawAppearance();

        if (ImGui.CollapsingHeader("Glyphs"))
            DrawGlyphs();

        if (ImGui.CollapsingHeader("Native Waymarks"))
            DrawNativeWaymarks();

        ImGui.Separator();

        // Whole-settings share code (backup or share your entire setup).
        if (ImGui.Button("Export settings"))
        {
            profileCode = config.ExportProfile();
            ImGui.SetClipboardText(profileCode);
            profileStatus = "Settings code copied to clipboard.";
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##profilecode", "Paste a settings code, then Import", ref profileCode, 8192);
        if (ImGui.Button("Import settings"))
        {
            if (config.TryImportProfile(profileCode))
            {
                config.Save();
                nativeVfx.ReapplyAll();
                profileStatus = "Settings imported.";
                profileCode = string.Empty;
            }
            else
            {
                profileStatus = "Invalid settings code.";
            }
        }
        ImGui.SameLine();
        ImGui.TextDisabled(string.IsNullOrEmpty(profileStatus)
            ? "Shares everything except saved schemes."
            : profileStatus);

        ImGui.Separator();
        if (ImGui.Button("Reset visuals to defaults"))
        {
            config.ResetToDefaults();
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(colors, sizes & glyphs — not your saved schemes)");
    }

    private void DrawMarkerGrid()
    {
        // One row per marker: On | Color | Marker | Opacity. Aligned columns, full-width slider.
        const ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;
        if (ImGui.BeginTable("markers", 4, flags))
        {
            ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 26);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 44);
            ImGui.TableSetupColumn("Marker", ImGuiTableColumnFlags.WidthFixed, 96);
            ImGui.TableSetupColumn("Opacity", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // Ordered by color pair: A/1, B/2, C/3, D/4 (indices 0&4, 1&5, 2&6, 3&7).
            foreach (var i in MarkerStyle.ColorPairOrder)
            {
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                var enabled = config.MarkerEnabled[i];
                if (ImGui.Checkbox($"##en{i}", ref enabled))
                {
                    config.MarkerEnabled[i] = enabled;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                var col = MarkerStyle.ToVector4(config.MarkerColors[i]);
                if (ImGui.ColorEdit4($"##col{i}", ref col,
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreview))
                {
                    config.MarkerColors[i] = MarkerStyle.FromVector4(col);
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.AlignTextToFramePadding();
                var shape = MarkerStyle.Shapes[i] == MarkerShape.Circle ? "circle" : "square";
                ImGui.Text($"{MarkerStyle.Labels[i]}  ({shape})");

                ImGui.TableSetColumnIndex(3);
                var op = config.MarkerOpacity[i];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.SliderFloat($"##op{i}", ref op, 0f, 1f, "%.2f"))
                {
                    config.MarkerOpacity[i] = op;
                    config.Save();
                }
            }
            ImGui.EndTable();
        }
    }

    private void DrawColorSchemes()
    {
        var builtIn = ColorSchemes.BuiltIn;
        var saved = config.SavedSchemes;
        var total = builtIn.Length + saved.Count;
        if (selectedScheme >= total) selectedScheme = total - 1;
        if (selectedScheme < 0) selectedScheme = 0;

        // Combined list: built-ins first, then user-saved.
        var names = new string[total];
        for (var i = 0; i < builtIn.Length; i++) names[i] = builtIn[i].Name;
        for (var i = 0; i < saved.Count; i++) names[builtIn.Length + i] = saved[i].Name + " (saved)";

        ImGui.SetNextItemWidth(240);
        ImGui.Combo("##scheme", ref selectedScheme, names, names.Length);
        ImGui.SameLine();
        if (ImGui.Button("Apply"))
        {
            var scheme = selectedScheme < builtIn.Length
                ? builtIn[selectedScheme]
                : saved[selectedScheme - builtIn.Length];
            config.ApplyScheme(scheme);
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Export"))
        {
            var scheme = selectedScheme < builtIn.Length
                ? builtIn[selectedScheme]
                : saved[selectedScheme - builtIn.Length];
            importCode = ColorSchemes.Export(scheme);
            ImGui.SetClipboardText(importCode);
            schemeStatus = "Share code copied to clipboard.";
        }

        var isSaved = selectedScheme >= builtIn.Length;
        if (isSaved)
        {
            ImGui.SameLine();
            if (ImGui.Button("Delete"))
            {
                saved.RemoveAt(selectedScheme - builtIn.Length);
                config.Save();
                schemeStatus = string.Empty;
            }
        }

        ImGui.Separator();

        // Save the current live colors as a named scheme.
        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##schemename", "New scheme name", ref newSchemeName, 48);
        ImGui.SameLine();
        var canSave = !string.IsNullOrWhiteSpace(newSchemeName);
        if (!canSave) ImGui.BeginDisabled();
        if (ImGui.Button("Save current colors"))
        {
            AddOrReplaceScheme(saved, new ColorScheme(newSchemeName.Trim(), (uint[])config.MarkerColors.Clone()));
            schemeStatus = $"Saved '{newSchemeName.Trim()}'.";
            newSchemeName = string.Empty;
        }
        if (!canSave) ImGui.EndDisabled();

        // Import a share code from someone else (or a backup).
        ImGui.SetNextItemWidth(240);
        ImGui.InputTextWithHint("##importcode", "Paste a share code", ref importCode, 512);
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            if (ColorSchemes.TryImport(importCode, out var imported))
            {
                AddOrReplaceScheme(saved, imported);
                schemeStatus = $"Imported '{imported.Name}'.";
                importCode = string.Empty;
            }
            else
            {
                schemeStatus = "Invalid share code.";
            }
        }

        if (!string.IsNullOrEmpty(schemeStatus))
            ImGui.TextDisabled(schemeStatus);
        else
            ImGui.TextDisabled("Save the current colors, or Export/Import a scheme as a share code.");
    }

    private void AddOrReplaceScheme(System.Collections.Generic.List<ColorScheme> saved, ColorScheme scheme)
    {
        var existing = saved.FindIndex(s => string.Equals(s.Name, scheme.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) saved[existing] = scheme; // overwrite same-named
        else saved.Add(scheme);
        config.Save();
    }

    private void DrawAppearance()
    {
        var changed = false;

        var radius = config.Radius;
        if (ImGui.SliderFloat("Radius (yalms)", ref radius, 0.5f, 3.0f)) { config.Radius = radius; changed = true; }

        var fill = config.FillOpacity;
        if (ImGui.SliderFloat("Fill opacity", ref fill, 0f, 1f)) { config.FillOpacity = fill; changed = true; }

        var outAlpha = config.OutlineOpacity;
        if (ImGui.SliderFloat("Outline opacity", ref outAlpha, 0f, 1f)) { config.OutlineOpacity = outAlpha; changed = true; }

        var thick = config.OutlineThickness;
        if (ImGui.SliderFloat("Outline thickness (px)", ref thick, 0f, 10f)) { config.OutlineThickness = thick; changed = true; }

        var height = config.HeightOffset;
        if (ImGui.SliderFloat("Height offset", ref height, 0f, 1f)) { config.HeightOffset = height; changed = true; }

        var dist = config.MaxDistance;
        if (ImGui.SliderFloat("Max draw distance", ref dist, 20f, 400f)) { config.MaxDistance = dist; changed = true; }

        var dim = config.DimBehindWalls;
        if (ImGui.Checkbox("Dim behind walls/objects", ref dim)) { config.DimBehindWalls = dim; changed = true; }
        ImGui.TextDisabled("Turn OFF if markers shimmer near dust/particle effects (e.g. in instances).");

        if (changed) config.Save();
    }

    private void DrawGlyphs()
    {
        var glyph = config.GlyphEnabled;
        if (ImGui.Checkbox("Draw glyphs (letters/numbers)", ref glyph))
        {
            config.GlyphEnabled = glyph;
            config.Save();
        }

        var gscale = config.GlyphScale;
        if (ImGui.SliderFloat("Glyph size", ref gscale, 0.3f, 3.0f))
        {
            config.GlyphScale = gscale;
            config.Save();
        }

        // Order MUST match the GlyphMode enum's int values (ProjectedImage=0 .. BillboardImage=3).
        var mode = (int)config.GlyphMode;
        string[] modes =
        {
            "Flat image (fixed, on floor)",
            "Billboard text (fallback)",
            "None",
            "Face camera (upright, like native floating letters)",
            "Flat + rotates to face you",
        };
        if (ImGui.Combo("Glyph style", ref mode, modes, modes.Length))
        {
            config.GlyphMode = (GlyphMode)mode;
            config.Save();
        }
    }

    private void DrawNativeWaymarks()
    {
        if (!nativeVfx.Available)
        {
            ImGui.TextDisabled("Unavailable: the VFX hook failed to load (likely a game patch).");
            ImGui.TextDisabled("Flat markers still work; the native pillars just can't be hidden.");
            return;
        }

        ImGui.TextDisabled("Hide the native 3D pillar + floating letter, per waymark.");

        // Master toggle: all on / all off.
        var allHidden = Array.TrueForAll(config.HideNativeWaymark, h => h);
        var master = allHidden;
        if (ImGui.Checkbox("Hide all##hideall", ref master))
        {
            for (var i = 0; i < config.HideNativeWaymark.Length; i++) config.HideNativeWaymark[i] = master;
            config.Save();
            nativeVfx.ReapplyAll();
        }

        if (ImGui.BeginTable("hidegrid", 4, ImGuiTableFlags.SizingStretchSame))
        {
            for (var i = 0; i < 8; i++)
            {
                if (i % 4 == 0) ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(i % 4);

                var h = config.HideNativeWaymark[i];
                if (ImGui.Checkbox($"{MarkerStyle.Labels[i]}##hide{i}", ref h))
                {
                    config.HideNativeWaymark[i] = h;
                    config.Save();
                    nativeVfx.ReapplyAll();
                }
            }
            ImGui.EndTable();
        }

        ImGui.TextDisabled("Applies to waymarks as they're (re)placed or on zone change.");
        ImGui.TextDisabled("Client-side only — the party still sees their own waymarks.");
    }
}
