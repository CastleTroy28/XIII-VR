using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
namespace XiiiXR;
// 0.1.156: the game's stealth arcs (HUDSpotIcon) turned from the character's
// heading to the head's, every time the game turns them (SpotMath).
internal static class SpotIndicators
{
    private static readonly Harmony patches=new("xiii.vr.xrbootstrap.spots");
    private static bool installed,failed;private static int sign,reports,stillCount;
    private sealed class Seen{internal float GameZ,Applied,RefZ,RefAngle;internal bool Has;}
    private static readonly Dictionary<IntPtr,Seen> seen=new();
    internal static void Install()
    {
        if(installed)return;installed=true;
        try
        {
            var m=AccessTools.Method(typeof(HUDSpotIcon),nameof(HUDSpotIcon.UpdateRotation));
            patches.Patch(m,prefix:new HarmonyMethod(typeof(SpotIndicators),nameof(Before)),postfix:new HarmonyMethod(typeof(SpotIndicators),nameof(After)));
            Bootstrap.Write("SPOT ICONS the stealth arcs follow the head (physical turns)");
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("SPOT ICONS unchanged: "+ex.Message);}
    }
    // The game's own last angle back, so its own turning (smoothing) never builds on ours.
    private static void Before(HUDSpotIcon __instance)
    {
        if(failed||__instance==null)return;
        try
        {
            if(!seen.TryGetValue(__instance.Pointer,out var s)||!s.Has)return;
            var t=__instance.m_transform;if(t==null)return;
            var e=t.localEulerAngles;if(Math.Abs(SpotMath.Delta(e.z,s.Applied))<.01f){e.z=s.GameZ;t.localEulerAngles=e;}
        }
        catch(Exception){}
    }
    private static void After(HUDSpotIcon __instance)
    {
        if(failed||__instance==null)return;
        try
        {
            var rig=CameraRig.Current;var t=__instance.m_transform;var target=__instance.m_target;var player=__instance.m_player;
            if(rig==null||t==null||target==null||player==null||rig.Frontend||rig.Scripted)return;
            float fromPlayer=Heading(player.position,player.forward,target.position);
            float fromHead=Heading(rig.HeadPosition,rig.HeadRotation*Vector3.forward,target.position);
            if(!float.IsFinite(fromPlayer)||!float.IsFinite(fromHead))return;
            var e=t.localEulerAngles;float gameZ=e.z;
            if(seen.Count>64)seen.Clear();
            if(!seen.TryGetValue(__instance.Pointer,out var s)){s=new Seen();seen[__instance.Pointer]=s;}
            // The game turns something else (not this transform): left as the game has it.
            if(!s.Has){s.RefZ=gameZ;s.RefAngle=fromPlayer;}
            else if(sign==0&&Math.Abs(SpotMath.Delta(s.RefAngle,fromPlayer))>=15)
            {
                // How the game's own angle followed the enemy's since the first look.
                int k=SpotMath.Sign(s.RefZ,s.RefAngle,gameZ,fromPlayer);
                if(k!=0){sign=k;Bootstrap.Write("SPOT ICONS the game turns an arc "+(k<0?"clockwise":"anticlockwise")+" for an enemy to the right");}
                else if(Math.Abs(SpotMath.Delta(s.RefZ,gameZ))<1&&++stillCount>=3){failed=true;Bootstrap.Write("SPOT ICONS the game does not turn the arc itself ("+t.name+"): left unchanged");return;}
                s.RefZ=gameZ;s.RefAngle=fromPlayer;
            }
            float z=SpotMath.Corrected(gameZ,fromPlayer,fromHead,sign);
            e.z=z;t.localEulerAngles=e;
            s.GameZ=gameZ;s.Applied=t.localEulerAngles.z;s.Has=true;
            if(reports<6&&Math.Abs(SpotMath.Delta(fromPlayer,fromHead))>20){reports++;Bootstrap.Write("SPOT ICON "+target.name+": "+fromPlayer.ToString("F0")+" degrees from the character's heading, "+fromHead.ToString("F0")+" from the head's; arc "+gameZ.ToString("F0")+" -> "+z.ToString("F0"));}
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("SPOT ICONS off: "+ex.Message);}
    }
    // Degrees to the right of a heading (flat).
    private static float Heading(Vector3 from,Vector3 forward,Vector3 to)
    {
        forward.y=0;var d=to-from;d.y=0;
        if(forward.sqrMagnitude<1e-6f||d.sqrMagnitude<1e-6f)return float.NaN;
        return Vector3.SignedAngle(forward,d,Vector3.up);
    }
}
