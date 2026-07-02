using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace FlatMarks.Data;

/// <summary>Reads the active game camera's horizontal orientation.</summary>
public static class CameraReader
{
    /// <summary>
    /// Gets the camera's horizontal facing (radians). <c>Game.Camera.DirH</c>: 0 is north,
    /// increasing clockwise (viewed from above). Used to yaw-rotate flat glyphs toward the camera.
    /// </summary>
    public static unsafe bool TryGetYaw(out float yaw)
    {
        yaw = 0f;

        var cm = CameraManager.Instance();
        if (cm == null) return false;

        var cam = cm->GetActiveCamera();
        if (cam == null) return false;

        yaw = cam->DirH;
        return true;
    }
}
