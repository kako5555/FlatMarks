using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace FlatMarks.Data;

/// <summary>Snapshot of one field waymark's state for a single frame.</summary>
public readonly record struct WaymarkState(int Index, bool Active, Vector3 Position);

/// <summary>
/// Thin unsafe wrapper over <see cref="MarkingController"/>. Index order matches the
/// game's FieldMarkers span: 0=A, 1=B, 2=C, 3=D, 4=One, 5=Two, 6=Three, 7=Four.
/// </summary>
public static class WaymarkReader
{
    public const int MarkerCount = 8;

    /// <summary>
    /// Fills <paramref name="buffer"/> (length 8) with the current waymark states and returns
    /// the number written (always 8, or 0 if the controller is unavailable). Allocation-free so
    /// it can run in the framework update / draw hot path.
    /// </summary>
    public static unsafe int ReadAll(Span<WaymarkState> buffer)
    {
        if (buffer.Length < MarkerCount)
            throw new ArgumentException($"buffer must hold at least {MarkerCount} entries", nameof(buffer));

        var mc = MarkingController.Instance();
        if (mc == null)
            return 0;

        var markers = mc->FieldMarkers;
        for (var i = 0; i < MarkerCount; i++)
        {
            var fm = markers[i];
            buffer[i] = new WaymarkState(i, fm.Active, fm.Position);
        }

        return MarkerCount;
    }
}
