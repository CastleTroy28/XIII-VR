using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace XiiiXR;
// 0.1.159: the game turns the strip once, at the hit, from the
// character's heading. The HUD in the headset follows the head, so with the
// body turned (or turning afterwards) it pointed wrong. Where the shot came
// from (PlayerState.ReceiveDamage's origin) is kept with the strip, and the
// strip is turned every frame to point at it from where the head looks
// (SpotMath.Strip).
internal static class DamageIndicators
{
    private static readonly Harmony patches=new("xiii.vr.xrbootstrap.damage");
    private static bool installed,failed;private static int reports;
    private static Vector3 source;private static float sourceAt=-10;
    private sealed class Live
    {
        internal HUDDamageIndicatorComponent? C;internal RectTransform? T;internal Vector3 Source;internal bool FromHead;
        internal float Z0,R0,Applied=float.NaN,Since;internal bool Has;
    }
    private static readonly List<Live> live=new();
    internal static void Install()
    {
        if(installed)return;installed=true;
        try
        {
            patches.Patch(AccessTools.Method(typeof(PlayerState),nameof(PlayerState.ReceiveDamage)),prefix:new HarmonyMethod(typeof(DamageIndicators),nameof(Damaged)));
            patches.Patch(AccessTools.Method(typeof(HUDDamageIndicatorComponent),nameof(HUDDamageIndicatorComponent.Initialize)),postfix:new HarmonyMethod(typeof(DamageIndicators),nameof(Shown)));
            Bootstrap.Write("DAMAGE STRIP the hit direction strip points at the shooter from the head (physical turns)");
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("DAMAGE STRIP unchanged: "+ex.Message);}
    }
    // Where the damage to the player came from.
    private static void Damaged(PlayerState __instance,Vector3 origin)
    {
        if(failed||__instance==null)return;
        try
        {
            var root=CameraRig.Current?.PlayerRoot;if(root==null)return;
            var t=__instance.transform;if(!t.IsChildOf(root)&&!root.IsChildOf(t))return;
            if(!Finite(origin)||origin.sqrMagnitude<1e-6f)return;
            source=origin;sourceAt=Time.realtimeSinceStartup;
        }
        catch(Exception){}
    }
    private static void Shown(HUDDamageIndicatorComponent __instance,float angles)
    {
        if(failed||__instance==null)return;
        try
        {
            var rig=CameraRig.Current;var root=rig?.PlayerRoot;
            if(rig==null||root==null||rig.Frontend)return;
            if(Time.realtimeSinceStartup-sourceAt>.25f){Report("DAMAGE STRIP shown with no known source (angle "+angles.ToString("F0")+"): left as the game has it");return;}
            var t=__instance.rectTransform;if(t==null)return;
            float body=Heading(root.position,root.forward,source),head=Heading(rig.HeadPosition,rig.HeadRotation*Vector3.forward,source);
            int reference=SpotMath.Reference(angles,body,head);
            if(reference<0){Report("DAMAGE STRIP the game's angle "+angles.ToString("F0")+" is neither the character's "+body.ToString("F0")+" nor the head's "+head.ToString("F0")+": left as the game has it");return;}
            for(int i=live.Count-1;i>=0;i--)if(live[i].C==null||live[i].C!.Pointer==__instance.Pointer)live.RemoveAt(i);
            if(live.Count>=16)live.RemoveAt(0);
            live.Add(new Live{C=__instance,T=t,Source=source,FromHead=reference==1,Since=Time.realtimeSinceStartup});
            if(reports<6&&Math.Abs(SpotMath.Delta(body,head))>20){reports++;Bootstrap.Write("DAMAGE STRIP hit from "+body.ToString("F0")+" degrees of the character's heading, "+head.ToString("F0")+" of the head's (the game's angle "+angles.ToString("F0")+", from the "+(reference==1?"head":"character")+"): the strip turned to the head");}
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("DAMAGE STRIP off: "+ex.Message);}
    }
    // Every frame before the HUD is drawn (CameraRig.RenderCanvases).
    internal static void Tick(CameraRig rig)
    {
        if(failed||live.Count==0)return;
        try
        {
            var root=rig.PlayerRoot;float now=Time.realtimeSinceStartup;
            var headForward=rig.HeadRotation*Vector3.forward;var headAt=rig.HeadPosition;
            for(int i=live.Count-1;i>=0;i--)
            {
                var l=live[i];var c=l.C;var t=l.T;
                if(c==null||t==null||root==null||!c.gameObject.activeInHierarchy||now-l.Since>15){live.RemoveAt(i);continue;}
                var e=t.localEulerAngles;
                // The game's own angle (first seen, or set anew by the game).
                if(!l.Has||Math.Abs(SpotMath.Delta(e.z,l.Applied))>.5f)
                {
                    l.Z0=e.z;l.R0=l.FromHead?Heading(headAt,headForward,l.Source):Heading(root.position,root.forward,l.Source);l.Has=float.IsFinite(l.R0);
                    if(!l.Has){live.RemoveAt(i);continue;}
                }
                float z=SpotMath.Strip(l.Z0,l.R0,Heading(headAt,headForward,l.Source));
                if(!float.IsFinite(z))continue;
                e.z=z;t.localEulerAngles=e;l.Applied=t.localEulerAngles.z;
            }
        }
        catch(Exception ex){failed=true;live.Clear();Bootstrap.Warn("DAMAGE STRIP off: "+ex.Message);}
    }
    private static void Report(string text){if(reports<6){reports++;Bootstrap.Write(text);}}
    // Degrees to the right of a heading (flat).
    private static float Heading(Vector3 from,Vector3 forward,Vector3 to)
    {
        forward.y=0;var d=to-from;d.y=0;
        if(forward.sqrMagnitude<1e-6f||d.sqrMagnitude<1e-6f)return float.NaN;
        return Vector3.SignedAngle(forward,d,Vector3.up);
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
}
