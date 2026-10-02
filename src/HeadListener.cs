using System;
using FMODUnity;
using HarmonyLib;
using UnityEngine;
namespace XiiiXR;
// 0.1.159: the game's FMOD listener sits on
// its camera, and the mod turns that camera to the head only while drawing
// (TrackedCamera: OnPreCull .. OnPostRender); for the rest of the frame, when
// the listener reads it, it faces the character's heading. Physical turns
// were not heard. The listener is set to the head (where it is and where it
// looks) right after the game sets it, each frame.
internal static class HeadListener
{
    private static readonly Harmony patches=new("xiii.vr.xrbootstrap.listener");
    private static bool installed,failed,reported;private static float nextReport;
    internal static void Install()
    {
        if(installed)return;installed=true;
        try
        {
            patches.Patch(AccessTools.Method(typeof(StudioListener),"Update"),postfix:new HarmonyMethod(typeof(HeadListener),nameof(After)));
            Bootstrap.Write("SOUND the game's listener follows the head (physical turns and leaning)");
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("SOUND listener unchanged: "+ex.Message);}
    }
    private static void After(StudioListener __instance)
    {
        if(failed||__instance==null)return;
        try
        {
            var rig=CameraRig.Current;if(rig==null||!rig.HeadTrackingValid)return;
            int index=__instance.ListenerNumber;if(index<0)return;
            var system=RuntimeManager.StudioSystem;
            FMOD.ATTRIBUTES_3D a=default;FMOD.VECTOR attenuation=default;
            if(system.getListenerAttributes(index,out a,out attenuation)!=FMOD.RESULT.OK)return;
            var head=rig.HeadPosition;var q=rig.HeadRotation;
            var forward=q*Vector3.forward;var up=q*Vector3.up;
            if(!Finite(head)||!Finite(forward)||!Finite(up))return;
            var gameForward=new Vector3(a.forward.x,a.forward.y,a.forward.z);
            a.position=RuntimeUtils.ToFMODVector(head);a.forward=RuntimeUtils.ToFMODVector(forward);a.up=RuntimeUtils.ToFMODVector(up);
            var result=__instance.attenuationObject!=null?system.setListenerAttributes(index,a,attenuation):system.setListenerAttributes(index,a);
            if(result!=FMOD.RESULT.OK){failed=true;Bootstrap.Warn("SOUND listener not set to the head: "+result);return;}
            float now=Time.realtimeSinceStartup;
            if(!reported||now>=nextReport&&Math.Abs(Yaw(gameForward)-Yaw(forward))>20)
            {
                reported=true;nextReport=now+60;
                Bootstrap.Write("SOUND listener "+index+" on "+__instance.name+": the game had it facing "+Yaw(gameForward).ToString("F0")+" degrees, the head faces "+Yaw(forward).ToString("F0")+"; set to the head");
            }
        }
        catch(Exception ex){failed=true;Bootstrap.Warn("SOUND listener off: "+ex.Message);}
    }
    private static float Yaw(Vector3 v)=>Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg;
    private static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
}
