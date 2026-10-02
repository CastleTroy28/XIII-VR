using System;
using System.Collections.Generic;
using HarmonyLib;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;

// Redirect only the selected local weapon's native muzzle effect. Original
// weapon bones are never moved; pooled particles are borrowed, never destroyed.
internal sealed class MuzzleEffects : IDisposable
{
    private static MuzzleEffects? current;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.muzzle-effects");
    private readonly List<(ParticleSystem particle,Transform anchor)> live=new();
    private Scope? active;
    private bool disposed;
    private float nextReport;
    private sealed class Scope
    {
        internal VFXComponent Vfx=null!;
        internal Transform? Original;
        internal Transform Anchor=null!;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal Scope? Previous;
        internal bool Restored;
        internal readonly List<ParticleSystem> Spawned=new();
    }
    internal MuzzleEffects()
    {
        try
        {
            patches.Patch(AccessTools.DeclaredMethod(typeof(VFXComponent),"ShowMuzzleFlash"),
                prefix:new HarmonyMethod(typeof(MuzzleEffects),nameof(Begin)),
                postfix:new HarmonyMethod(typeof(MuzzleEffects),nameof(End)),
                finalizer:new HarmonyMethod(typeof(MuzzleEffects),nameof(FinalizeFlash)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(VFXComponent),"SpawnParticle"),
                prefix:new HarmonyMethod(typeof(MuzzleEffects),nameof(BeginParticle)),
                postfix:new HarmonyMethod(typeof(MuzzleEffects),nameof(EndParticle)));
            current=this;
            Bootstrap.Write("MUZZLE FX native flash routing ready; selected local weapon only.");
        }
        catch { patches.UnpatchSelf(); throw; }
    }
    private static void Begin(VFXComponent __instance,out Scope? __state)
    {
        __state=null;var c=current;if(c==null || c.disposed) return;
        try
        {
            var hands=WeaponHands.Current;
            if(hands==null || !hands.TryGetEffectMuzzle(__instance.baseEquipable,out var anchor)) return;
            var s=new Scope {Vfx=__instance,Original=__instance.muzzleTransform,Anchor=anchor,
                Position=__instance.muzzlePositionAfterIK,Rotation=__instance.muzzleRotationAfterIK,Previous=c.active};
            __state=s; c.active=s;
            // Native ShowMuzzleFlash uses both this Transform and the cached pose
            // on different player/avatar paths. Override both for the call only.
            __instance.muzzleTransform=anchor;
            __instance.muzzlePositionAfterIK=anchor.position;
            __instance.muzzleRotationAfterIK=anchor.rotation;
        }
        catch(Exception ex) { c.Restore(__state); __state=null; Bootstrap.Warn("MUZZLE FX native fallback: "+ex.Message); }
    }
    private static void BeginParticle(VFXComponent __instance,ref Transform spawnTransform,ref bool attachParticleToSpawnTransform,out Scope? __state)
    {
        __state=null;
        try
        {
            var s=current?.active;
            if(s==null || s.Restored || s.Anchor==null || s.Vfx==null || s.Vfx.Pointer!=__instance.Pointer) return;
            spawnTransform=s.Anchor; attachParticleToSpawnTransform=true; __state=s;
        }
        catch(Exception ex) { Bootstrap.Warn("MUZZLE FX particle routing: "+ex.Message); }
    }
    private static void EndParticle(ParticleSystem __result,Scope? __state)
    {
        try { if(__state!=null && __result!=null && !__state.Spawned.Contains(__result)) __state.Spawned.Add(__result); }
        catch(Exception ex) { Bootstrap.Warn("MUZZLE FX particle capture: "+ex.Message); }
    }
    private static void End(Scope? __state) => current?.Complete(__state);
    private static Exception? FinalizeFlash(Exception? __exception,Scope? __state)
    {
        current?.Complete(__state);
        return __exception; // Preserve the game's exception; always restore scope.
    }
    private void Complete(Scope? s)
    {
        if(s==null || s.Restored) return;
        Restore(s);
        foreach(var particle in s.Spawned)
        {
            try
            {
                if(particle==null || s.Anchor==null) continue;
                live.RemoveAll(x=>x.particle==null || x.particle==particle);
                live.Add((particle,s.Anchor));
                // ShowMuzzleFlash can reparent to the desktop player rig after
                // SpawnParticle. Set the final parent after the whole native call.
                particle.transform.SetParent(s.Anchor,true);
                particle.transform.SetPositionAndRotation(s.Anchor.position,s.Anchor.rotation);
                WeaponHands.Current?.NativeFlashShown(s.Anchor);
                if(Time.realtimeSinceStartup>=nextReport)
                {
                    nextReport=Time.realtimeSinceStartup+2;
                    Bootstrap.Write("MUZZLE FX routed="+particle.name+" position="+s.Anchor.position);
                }
            }
            catch(Exception ex) { Bootstrap.Warn("MUZZLE FX final pose: "+ex.Message); }
        }
        Tick();
    }
    private void Restore(Scope? s)
    {
        if(s==null || s.Restored) return;
        s.Restored=true;
        if(active==s) active=s.Previous;
        try
        {
            if(s.Vfx!=null)
            {
                s.Vfx.muzzleTransform=s.Original!;
                s.Vfx.muzzlePositionAfterIK=s.Position;
                s.Vfx.muzzleRotationAfterIK=s.Rotation;
            }
        }
        catch(Exception ex) { Bootstrap.Warn("MUZZLE FX scope restore: "+ex.Message); }
    }
    internal void Tick()
    {
        for(int i=live.Count-1;i>=0;i--)
        {
            try
            {
                var (particle,anchor)=live[i];
                if(particle==null || anchor==null || particle.transform.parent!=anchor) { live.RemoveAt(i); continue; }
                if(!particle.IsAlive(true)) { particle.transform.SetParent(null,true); live.RemoveAt(i); }
            }
            catch(Exception ex) { live.RemoveAt(i); Bootstrap.Warn("MUZZLE FX pool cleanup: "+ex.Message); }
        }
    }
    internal void Cancel()
    {
        foreach(var (particle,anchor) in live)
            try
            {
                if(particle==null || anchor==null || particle.transform.parent!=anchor) continue;
                particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.transform.SetParent(null,true);
            }
            catch(Exception ex) { Bootstrap.Warn("MUZZLE FX release: "+ex.Message); }
        live.Clear();
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        Cancel(); while(active!=null) Restore(active);
        patches.UnpatchSelf(); if(current==this) current=null;
    }
}
