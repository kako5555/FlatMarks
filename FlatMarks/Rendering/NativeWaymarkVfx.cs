using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Pictomancy.VfxDraw;
using SourOmen.Structs;

namespace FlatMarks.Rendering;

/// <summary>
/// Phase 3: hides the native 3D waymark VFX (holographic pillar + floating letter/number).
/// These are static <c>.avfx</c> objects under <c>vfx/common/eff/fld_mark_*</c>. We hook the game's
/// VFX-create function, and for matched waymark VFX force the instance color alpha to 0 (the same,
/// crash-safe technique Waymark Studio uses — we never return null or destroy the object).
/// </summary>
public sealed unsafe class NativeWaymarkVfx : IDisposable
{
    private const string WaymarkPathPrefix = "vfx/common/eff/fld_mark_";

    [Signature(VfxFunctions.CreateVfxSig, DetourName = nameof(CreateVfxDetour))]
    private readonly Hook<VfxFunctions.CreateVfxDelegate> createVfxHook = null!;

    // Tracked live waymark VFX, keyed by waymark index (0=A .. 7=Four). Vfx.IsValid lets us prune
    // destroyed VFX without a separate destroy hook, so we never write to freed memory.
    private readonly Dictionary<int, Vfx> tracked = new();
    private readonly Configuration config;

    /// <summary>True if the create hook resolved and is active; false disables the feature gracefully.</summary>
    public bool Available { get; private set; }

    public NativeWaymarkVfx(IGameInteropProvider hooker, Configuration config)
    {
        this.config = config;
        try
        {
            hooker.InitializeFromAttributes(this);
            if (createVfxHook is not null)
            {
                createVfxHook.Enable();
                Available = true;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "FlatMarks: failed to hook waymark VFX creation; hide-native disabled");
            Available = false;
        }
    }

    /// <summary>Re-apply per-marker hide state to all tracked VFX (call after a toggle changes).</summary>
    public void ReapplyAll()
    {
        foreach (var (idx, vfx) in tracked)
        {
            if (vfx.IsValid)
                vfx.UpdateColor(new Vector4(1, 1, 1, AlphaFor(idx)));
        }
    }

    /// <summary>Prune tracked VFX whose game object has been destroyed. Call each frame.</summary>
    public void Update()
    {
        if (!Available || tracked.Count == 0) return;

        List<int>? dead = null;
        foreach (var (idx, vfx) in tracked)
        {
            if (!vfx.IsValid)
                (dead ??= new()).Add(idx);
        }
        if (dead is not null)
            foreach (var idx in dead) tracked.Remove(idx);
    }

    public void Dispose()
    {
        // Restore visibility on unload so we never leave the game's waymarks invisible.
        foreach (var (_, vfx) in tracked)
        {
            if (vfx.IsValid)
                vfx.UpdateColor(new Vector4(1, 1, 1, 1));
        }
        tracked.Clear();
        createVfxHook?.Dispose();
    }

    private float AlphaFor(int idx)
        => idx >= 0 && idx < config.HideNativeWaymark.Length && config.HideNativeWaymark[idx] ? 0f : 1f;

    private VfxData* CreateVfxDetour(byte* path, VfxInitData* init, byte a3, byte a4,
        float originX, float originY, float originZ,
        float sizeX, float sizeY, float sizeZ, float angle, float duration, int a13)
    {
        var vfxData = createVfxHook.Original(path, init, a3, a4, originX, originY, originZ,
                                             sizeX, sizeY, sizeZ, angle, duration, a13);
        try
        {
            if (vfxData != null)
            {
                var pathStr = MemoryHelper.ReadStringNullTerminated((nint)path);
                if (pathStr.StartsWith(WaymarkPathPrefix, StringComparison.Ordinal))
                {
                    var idx = IndexFromPath(pathStr);
                    if (idx >= 0)
                    {
                        var position = new Vector3(originX, originY, originZ);
                        var size = new Vector3(sizeX, sizeY, sizeZ);
                        tracked[idx] = Vfx.Wrap(vfxData, position, size, angle);
                        vfxData->Instance->Color = new Vector4(1, 1, 1, AlphaFor(idx));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "FlatMarks: waymark VFX detour error");
        }

        return vfxData;
    }

    /// <summary>Maps the char after the path prefix (a/b/c/d or 1/2/3/4) to the waymark index.</summary>
    private static int IndexFromPath(string path)
    {
        if (path.Length <= WaymarkPathPrefix.Length) return -1;
        return path[WaymarkPathPrefix.Length] switch
        {
            'a' => 0, 'b' => 1, 'c' => 2, 'd' => 3,
            '1' => 4, '2' => 5, '3' => 6, '4' => 7,
            _ => -1,
        };
    }
}
