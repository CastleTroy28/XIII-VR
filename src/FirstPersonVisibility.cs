using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
internal sealed class FirstPersonVisibility
{
    private readonly Dictionary<int,(Renderer renderer,bool enabled)> originals = new();
    private readonly Dictionary<int,(Animator animator,AnimatorCullingMode culling)> animators=new();
    private readonly Dictionary<int,(SkinnedMeshRenderer skin,bool offscreen)> skins=new();
    private float nextScan;
    private bool failed;
    internal void Tick(CustomCharacterController? player,bool hide)
    {
        if (failed) return;
        try
        {
            if (player == null || !hide) { Restore(); return; }
            if (Time.realtimeSinceStartup >= nextScan)
            {
                nextScan = Time.realtimeSinceStartup + 1;
                var model = player.CurrentFpsRigReference;
                if (model != null)
                {
                    Track(model.fpsMainAnimator);Track(model.fpsLeftHandAnimator);Track(model.animatedTarget);
                    var skin=model.m_fpsMesh;
                    if(skin!=null&&!skins.ContainsKey(skin.GetInstanceID()))skins.Add(skin.GetInstanceID(),(skin,skin.updateWhenOffscreen));
                    Collect(model.transform,player.transform);
                    Collect(model.fpsLeftHandAnimator?.transform,player.transform);
                }
                Collect(player.tinyArmsMesh,player.transform);
            }
            // Hidden native arms still drive the grip sampled by our owned skin.
            foreach(var a in animators.Values)if(a.animator!=null)a.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var s in skins.Values)if(s.skin!=null)s.skin.updateWhenOffscreen=true;
            foreach (var entry in originals.Values) if (entry.renderer != null) entry.renderer.enabled = false;
        }
        catch (Exception ex) { failed = true; Restore(); Bootstrap.Warn("First-person model hiding unavailable; locomotion continues. " + ex.Message); }
    }
    private void Track(Animator? animator)
    {if(animator!=null&&!animators.ContainsKey(animator.GetInstanceID()))animators.Add(animator.GetInstanceID(),(animator,animator.cullingMode));}
    private void Collect(Transform? model,Transform root)
    {
        if (model == null || model == root || !model.IsChildOf(root)) return;
        foreach (var component in model.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var renderer = component.TryCast<Renderer>(); if (renderer == null) continue;
            // A native pickup reparents the NPC below the FPS arm. It is not
            // part of that arm and must never enter the hidden-renderer set.
            if(renderer.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null)continue;
            // 0.1.193: the game hangs its grappling hook, cable and pulley on the
            // wrist of that separate left arm; since 0.1.190 they were hidden
            // with it. The grapple (GrappleVr) shows and places them itself.
            if(OwnVisibility(renderer.transform,model))continue;
            // Pooled muzzle particles can later be reused under the VR weapon.
            // Do not retain a renderer override that would hide the reused FX.
            if(renderer.TryCast<ParticleSystemRenderer>()!=null) continue;
            int id = renderer.GetInstanceID();
            if (!originals.ContainsKey(id))
            {
                originals.Add(id,(renderer,renderer.enabled));
                Bootstrap.Write("LOCOMOTION MODEL hidden=" + renderer.name);
            }
            renderer.enabled = false;
        }
    }
    internal static bool Gadget(string? name)=>(name??"").StartsWith("eqp_grappling",StringComparison.OrdinalIgnoreCase);
    private static bool OwnVisibility(Transform? t,Transform root)
    {
        for(int i=0;t!=null&&i<24;i++,t=t.parent){if(Gadget(t.name))return true;if(t==root)break;}
        return false;
    }
    internal void Restore()
    {
        foreach(var a in animators.Values)if(a.animator!=null)a.animator.cullingMode=a.culling;
        foreach(var s in skins.Values)if(s.skin!=null)s.skin.updateWhenOffscreen=s.offscreen;
        animators.Clear();skins.Clear();
        foreach (var entry in originals.Values)
            try { if (entry.renderer != null) entry.renderer.enabled = entry.enabled; }
            catch (Exception ex) { Bootstrap.Warn("Model visibility restore: " + ex.Message); }
        originals.Clear(); nextScan = 0;
    }
}
