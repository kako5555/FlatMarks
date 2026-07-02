using System;
using System.Linq;
using System.Text;

namespace FlatMarks.Rendering;

/// <summary>A named set of 8 marker colors (ABGR), index order A,B,C,D,1,2,3,4.</summary>
[Serializable]
public class ColorScheme
{
    public string Name { get; set; } = "";
    public uint[] Colors { get; set; } = new uint[8];

    public ColorScheme() { }

    public ColorScheme(string name, uint[] colors)
    {
        Name = name;
        Colors = colors;
    }

    public ColorScheme Clone() => new(Name, (uint[])Colors.Clone());
}

/// <summary>
/// Built-in color palettes. The colorblind-friendly sets are drawn from the Okabe–Ito
/// accessible qualitative palette, picking four maximally-separable hues per deficiency type.
/// Each palette repeats its four colors across the letter (A-D) and number (1-4) pairs.
/// </summary>
public static class ColorSchemes
{
    private static uint Rgb(byte r, byte g, byte b)
        => 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;

    private static uint[] Pair(uint a, uint b, uint c, uint d)
        => new[] { a, b, c, d, a, b, c, d };

    public static readonly ColorScheme[] BuiltIn =
    {
        new("Default (game)", (uint[])MarkerStyle.DefaultColors.Clone()),

        // Red-blind: brighten reds toward orange, keep a strong blue/yellow/purple spread.
        new("Colorblind — Protanopia", Pair(
            Rgb(0xE6, 0x9F, 0x00),  // orange
            Rgb(0xF0, 0xE4, 0x42),  // yellow
            Rgb(0x00, 0x72, 0xB2),  // blue
            Rgb(0xCC, 0x79, 0xA7))),// reddish purple

        // Green-blind: vermillion + sky blue read as clearly distinct from yellow/purple.
        new("Colorblind — Deuteranopia", Pair(
            Rgb(0xD5, 0x5E, 0x00),  // vermillion
            Rgb(0xF0, 0xE4, 0x42),  // yellow
            Rgb(0x56, 0xB4, 0xE9),  // sky blue
            Rgb(0xCC, 0x79, 0xA7))),// reddish purple

        // Blue-yellow-blind: separate mainly along the red-green axis instead.
        new("Colorblind — Tritanopia", Pair(
            Rgb(0xFF, 0x40, 0x40),  // red
            Rgb(0xFF, 0x80, 0xC0),  // pink
            Rgb(0x00, 0xA0, 0xA0),  // teal
            Rgb(0xA0, 0x00, 0xFF))),// violet
    };

    private const string CodePrefix = "FMS1:";

    /// <summary>Encode a scheme as a portable share code (name + 8 ABGR colors, base64).</summary>
    public static string Export(ColorScheme scheme)
    {
        var name = scheme.Name.Replace('|', ' ');
        var colors = string.Join(",", scheme.Colors.Select(c => c.ToString("X8")));
        var payload = name + "|" + colors;
        return CodePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>Decode a share code produced by <see cref="Export"/>. Returns false on any malformed input.</summary>
    public static bool TryImport(string code, out ColorScheme scheme)
    {
        scheme = new ColorScheme();
        try
        {
            code = code.Trim();
            if (code.StartsWith(CodePrefix, StringComparison.Ordinal))
                code = code[CodePrefix.Length..];

            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(code));
            var parts = payload.Split('|');
            if (parts.Length != 2) return false;

            var colorParts = parts[1].Split(',');
            if (colorParts.Length != 8) return false;

            var colors = new uint[8];
            for (var i = 0; i < 8; i++)
                colors[i] = Convert.ToUInt32(colorParts[i], 16);

            scheme = new ColorScheme(string.IsNullOrWhiteSpace(parts[0]) ? "Imported" : parts[0].Trim(), colors);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
