using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal sealed class CameraEffects : IDisposable
{
    // Restore the .33 rendering policy. Native desktop image effects are not
    // stereo-safe on this game's multipass route: enabling them in .34 produced
    // near-identical eye captures. AA=None alone does not bypass their final blit.
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "CTAA_PC", "RenderPostCTAA", "CustomPostProcessing", "DeferredFogEffect",
        "PostProcessMaterial", "UnityEngine.PostProcessing.PostProcessingBehaviour",
        "UnityEngine.Rendering.PostProcessing.PostProcessLayer"
    };
    private readonly Dictionary<int, (Camera camera, List<Behaviour> scripts)> candidates = new();
    private readonly Dictionary<int, Behaviour> disabled = new();
    private readonly List<int> stale = new();
    private bool disposed;
    internal bool Bypassed => !disposed;
    internal void Inspect(Camera camera, bool log)
    {
        if (disposed) return;
        // Discovery is infrequent; the pre-render guard below only visits this
        // camera's cached effects and allocates no component arrays per eye.
        stale.Clear();
        foreach (var pair in candidates) if (pair.Value.camera == null) stale.Add(pair.Key);
        foreach (int id in stale) candidates.Remove(id);
        stale.Clear();
        foreach (var pair in disabled) if (pair.Value == null) stale.Add(pair.Key);
        foreach (int id in stale) disabled.Remove(id);
        var scripts = new List<Behaviour>();
        foreach (var component in camera.GetComponents(Il2CppType.Of<MonoBehaviour>()))
        {
            if (component == null) continue;
            var script = component.TryCast<Behaviour>();
            if (script == null) continue;
            string type = component.GetIl2CppType().FullName ?? string.Empty;
            if (log) Bootstrap.Write("CAMERA SCRIPT camera=" + camera.name + " type=" + type + " enabled=" + script.enabled);
            if (Names.Contains(type)&&!StoryColorEffect.Owns(script)) scripts.Add(script);
        }
        candidates[camera.GetInstanceID()] = (camera, scripts);
        BeforeRender(camera);
    }
    internal void BeforeRender(Camera camera)
    {
        if (disposed || !candidates.TryGetValue(camera.GetInstanceID(), out var entry)) return;
        foreach (var script in entry.scripts)
        {
            if (script == null || !script.enabled) continue;
            int id = script.GetInstanceID();
            // Remember even initially inactive effects if the native Timeline
            // later enables them: stopping VR restores the game's latest intent.
            bool first = !disabled.ContainsKey(id);
            disabled[id] = script;
            script.enabled = false;
            if (first) Bootstrap.Write("EFFECT stereo bypass camera=" + camera.name + " id=" + id);
        }
    }
    internal void Toggle()
    {
        if (disposed) return;
        // F7 must not reintroduce the broken mono postprocessing in the headset.
        // F10/Dispose still restores the native desktop renderer.
        foreach (var entry in candidates.Values) if (entry.camera != null) BeforeRender(entry.camera);
        Bootstrap.Write("EFFECT stereo bypass remains active in VR; native effects restore on F10.");
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var script in disabled.Values)
            try { if (script != null) script.enabled = true; }
            catch (Exception ex) { Bootstrap.Warn("Effect restore: " + ex.Message); }
        disabled.Clear(); candidates.Clear(); stale.Clear();
    }
}
