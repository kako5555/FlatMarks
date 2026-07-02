# FlatMarks

By **Kako5555**.

A Dalamud plugin for FFXIV that renders the 8 field waymarks (A/B/C/D and 1/2/3/4) as **flat, 2D markers lying on the floor** instead of the game's 3D holographic pillars — like a raid-plan diagram, in-game.

Markers are drawn in **world space**, so they project onto uneven terrain (slopes, stairs) and are correctly clipped behind the camera and around the native UI.

## Features

- Flat circle (letters) / square (numbers) markers sized to match the real waymarks
- Per-marker enable, color, and opacity
- Glyph styles: flat on the floor, **flat that rotates to face you**, camera-facing billboard, text, or none — with a size slider
- **Hide the native 3D waymarks** (pillar + floating letter), per waymark — client-side only, your party is unaffected
- **Color schemes**: game default + colorblind-friendly presets (protanopia, deuteranopia, tritanopia); save your own and share them via import/export codes
- Export/import your **entire settings** as a share code

## Install (recommended)

1. In-game, open `/xlsettings` → **Experimental**.
2. Under **Custom Plugin Repositories**, add:
   ```
   https://raw.githubusercontent.com/kako5555/FlatMarks/main/pluginmaster.json
   ```
   Click the **+**, then **Save**.
3. Open `/xlplugins`, search **FlatMarks**, and install.
4. Place waymarks (or load a preset) — flat markers appear on the floor.

Prefer a manual download? Grab `latest.zip` from the [Releases](https://github.com/kako5555/FlatMarks/releases) page.

## Commands

- `/flatmarks` — open the settings window
- `/flatmarks toggle` — master enable/disable

## Building from source

Requires the .NET SDK matching `Dalamud.NET.Sdk/15.0.0` (net10.0) and a working XIVLauncher/Dalamud install.

```bash
cd FlatMarks
dotnet build -c Release
```

The installable package is written to `FlatMarks/bin/Release/FlatMarks/latest.zip`. For a dev install, add that build-output folder under `/xlsettings` → Experimental → **Dev Plugin Locations**, then enable it in `/xlplugins` → Dev Tools.

## Notes & credits

- Rendering uses **[Pictomancy](https://github.com/sourpuh/ffxiv_pictomancy)**. The world-space rendering approach, `MarkingController` usage, and the native-waymark hide technique were studied from **[Waymark Studio](https://github.com/sourpuh/ffxiv_waymarkstudio)** (both by [sourpuh](https://github.com/sourpuh)). Waymark Studio is AGPL, so this plugin is licensed **AGPL-3.0-or-later** accordingly.
- Glyph textures in `res/` are generated programmatically; no game or third-party assets are bundled.
- PvP is disabled entirely out of caution.

## Non-goals

- Waymark placement/presets (see Waymark Studio / WaymarkPresetPlugin)
- Networked/shared custom markers; PvP support
