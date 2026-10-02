using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PlayMagic;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.119: stationary (mounted) machine gun in VR.
//  - taken with BOTH grips (InteractionDriver asks TurretTarget), left by
//    letting go of both grips;
//  - while on it, the VR hands are hidden (the game's own arms hold the gun);
//  - the right trigger fires (WeaponHands lets the press through), both
//    controllers vibrate while it fires;
//  - the gun turns where the controllers point: after the game's own mount
//    rotation each frame the two joints are turned to that direction, within
//    the gun's own limits; the VR view stays world-fixed while the seat turns.
// 0.1.120:
//  - by default the gun is steered by its handles (VR SETTINGS "Mounted gun"):
//    hands moved right turn the barrel left, hands moved down raise it, like
//    a real mounted gun; measured in the tracking space from where the hands
//    were when the gun was taken (the seat turning does not feed back);
//  - the game's arms holding it are cut from the shoulder to the elbow.
// 0.1.138: sideways it went the same way as the hands; now the other way (hands left, barrel right), gentler near
// the centre; both axes smoothed against the shaking of the shots.
internal sealed class MountedGunVr:IDisposable
{
    internal static MountedGunVr? Current;
    internal static bool HidesHands=>Current?.mounted==true;
    private readonly CameraRig rig;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.mounted");
    private readonly Dictionary<IntPtr,(Quaternion h,Quaternion v)> rest=new();
    private CustomCharacterController? character;private MountedWeaponComponent? mount;
    private Transform? horizontal,vertical,aim;private Quaternion baseH,baseV;
    private float yawLimit=180,pitchLimit=60;
    private bool mounted,desiredValid,patched;private Vector3 desired;
    private float mountedSince,releasedSince=-1,leaveAt=-1,nextHaptic,nextFind,nextReport,nextError;
    private int pressFrame=-10;
    // Handles (lever) steering.
    private bool leverReady,leftStart,leverValid,settling;private System.Numerics.Vector3 startLeft,startRight,facing;private float leverYaw,leverPitch;
    private MountedArms? arms;
    private readonly MountedAimMath.Smooth yawSmooth=new(),pitchSmooth=new();
    internal bool Mounted=>mounted;
    // 0.1.142: the gun's pivot and heading (its turn about the vertical, not
    // its tilt), for the view that turns with it.
    // 0.1.143: the heading is the turn this mod gives the gun (+ = right), the
    // same number the view turns by (0.1.142 read the joint's direction; the
    // log showed the view had not turned).
    internal bool TryHeading(out Vector3 pivot,out float heading)
    {
        pivot=Vector3.zero;heading=0;
        if(!mounted||horizontal==null||!Handles)return false;
        pivot=horizontal.position;heading=appliedYaw;
        return float.IsFinite(pivot.sqrMagnitude)&&float.IsFinite(heading);
    }
    private float appliedYaw,lastSide;
    internal MountedGunVr(CameraRig owner){rig=owner;Current=this;}
    internal void Tick()
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            if(character==null||character.gameObject==null){if(now<nextFind)return;nextFind=now+1;Find();if(character==null){if(mounted)End("character gone");return;}}
            bool on=character.isMounted&&character.mount!=null&&!rig.Scripted;
            if(on!=mounted){if(on)Begin(now);else End("left");}
            if(!mounted)return;
            Aim();Haptics(now);Leave(now);
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("MOUNTED GUN: "+ex.Message);}
        }
    }
    private void Find()
    {
        var root=rig.PlayerRoot;if(root==null){character=null;return;}
        character=root.GetComponentInChildren(Il2CppType.Of<CustomCharacterController>(),true)?.TryCast<CustomCharacterController>();
        if(character!=null&&!patched)
        {
            patched=true;
            try
            {
                patches.Patch(AccessTools.DeclaredMethod(typeof(CustomCharacterController),nameof(CustomCharacterController.UpdateMountRotation)),postfix:new HarmonyMethod(typeof(MountedGunVr),nameof(AfterMountRotation)));
                Bootstrap.Write("MOUNTED GUN controller aim installed");
            }
            catch(Exception ex){Bootstrap.Warn("MOUNTED GUN aim unavailable (the game's own turning stays): "+ex.Message);}
        }
    }
    private void Begin(float now)
    {
        mount=character!.mount;mounted=true;mountedSince=now;releasedSince=-1;leaveAt=-1;desiredValid=false;appliedYaw=0;lastSide=0;
        horizontal=mount.mountRotationHorizontal;vertical=mount.mountRotationVertical;
        aim=null;
        if(vertical!=null)
        {
            aim=vertical.Find("TurretCameraPosition");
            if(aim==null)foreach(var t in vertical.GetComponentsInChildren(Il2CppType.Of<Transform>(),true)){var tr=t.TryCast<Transform>();if(tr!=null&&tr.name.ToLowerInvariant().Contains("camera")){aim=tr;break;}}
            aim??=vertical;
        }
        if(horizontal!=null&&vertical!=null)
        {
            if(!rest.TryGetValue(mount.Pointer,out var r)){r=(horizontal.localRotation,vertical.localRotation);rest[mount.Pointer]=r;}
            baseH=r.h;baseV=r.v;
        }
        var h=mount.HorizontalRotationClamp;var v=mount.VerticalRotationClamp;
        yawLimit=MountedAimMath.Limit(h.x,h.y,180);pitchLimit=MountedAimMath.Limit(v.x,v.y,60);
        leverReady=leverValid=false;leverYaw=leverPitch=0;
        try{arms?.Dispose();arms=new MountedArms(mount.armsObject);}catch(Exception ex){arms=null;Bootstrap.Warn("MOUNTED ARMS unavailable (whole arms shown): "+ex.Message);}
        var parentUp=horizontal!=null&&horizontal.parent!=null?horizontal.parent.up:Vector3.up;
        Bootstrap.Write("MOUNTED GUN on "+mount.GetIdentifier+" turnAxis="+parentUp.ToString("F2")+" horizontal="+(horizontal!=null?horizontal.name:"none")+" vertical="+(vertical!=null?vertical.name:"none")+" aim="+(aim!=null?aim.name:"none")
            +" clampH="+h+" clampV="+v+" limits=±"+yawLimit.ToString("F0")+"/±"+pitchLimit.ToString("F0")+" steering="+(Handles?"handles (inverted)":"pointing")+" arms cut="+(arms?.Count??0)+"; VR hands hidden");
    }
    private void End(string why)
    {
        mounted=false;mount=null;horizontal=vertical=aim=null;desiredValid=false;leaveAt=-1;leverReady=leverValid=false;
        try{arms?.Dispose();}catch(Exception ex){Bootstrap.Warn("MOUNTED ARMS restore: "+ex.Message);}arms=null;
        Bootstrap.Write("MOUNTED GUN off ("+why+"); VR hands back");
    }
    private static bool Handles=>QualityOptions.MountedHandles?.Value!=false;
    // Where both controllers point (the one that is tracked, if only one).
    private void Aim()
    {
        if(Handles){Lever();return;}
        if(!rig.SampleWorldHands(out var left,out var right,out bool leftValid))return;
        var target=ControllerAim.Rotation(right)*Vector3.forward;
        if(leftValid)target+=ControllerAim.Rotation(left)*Vector3.forward;
        if(target.sqrMagnitude<1e-6f)return;target.Normalize();
        float k=1-MathF.Exp(-12*Math.Max(0,Time.unscaledDeltaTime));
        desired=desiredValid?Vector3.Slerp(desired,target,k).normalized:target;desiredValid=true;
    }
    // The hands' move since the gun was taken, in the real room (tracking
    // space), sideways in the frame the head faced then, and up.
    private void Lever()
    {
        if(!rig.PhysicalHand(true,out var right))return;
        bool left=rig.PhysicalHand(false,out var leftHand);
        if(!leverReady||Time.realtimeSinceStartup-mountedSince<MountedAimMath.Settle)
        {
            // 0.1.123: centred where the hands come to rest (gun straight meanwhile).
            if(!rig.PhysicalHeadPose(out var head))return;
            facing=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ,head.Rotation);
            startRight=right;startLeft=leftHand;leftStart=left;leverReady=true;settling=true;
            leverYaw=leverPitch=0;leverValid=true;yawSmooth.Reset();pitchSmooth.Reset();return;
        }
        if(settling){settling=false;Bootstrap.Write("MOUNTED GUN handles: centre right="+startRight+" left="+(leftStart?startLeft.ToString():"none")+" (up/down "+(MountedAimMath.HandleLength*100).ToString("F0")+" cm per radian; sideways the barrel goes the way the hands move, 75 deg at 18 cm, smoothed; the view "+(QualityOptions.MountedViewTurns?.Value!=false?"turns with the gun)":"stays put)"));}
        var now=right;var start=startRight;
        if(left&&leftStart){now=(right+leftHand)*.5f;start=(startRight+startLeft)*.5f;}
        if(!MountedAimMath.Offset(now,start,facing,out float side,out float lift))return;
        lastSide=side;
        // 0.1.138: sideways the other way round and gentler (SideLever), both
        // smoothed against the shaking of the shots (One Euro).
        float yaw=MountedAimMath.SideLever(side,yawLimit),pitch=MountedAimMath.Lever(lift,MountedAimMath.HandleLength,pitchLimit);
        if(!float.IsFinite(yaw)||!float.IsFinite(pitch))return;
        float dt=Math.Max(0,Time.unscaledDeltaTime);
        if(!leverValid){yawSmooth.Reset();pitchSmooth.Reset();}
        leverYaw=yawSmooth.Step(yaw,dt);leverPitch=pitchSmooth.Step(pitch,dt);leverValid=true;
    }
    // The game's arms: cut, drawn after the game's animation of this frame.
    internal void Render()
    {
        if(!mounted||arms==null)return;
        try{arms.Render();}catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("MOUNTED ARMS: "+ex.Message);}}
    }
    private static void AfterMountRotation(CustomCharacterController __instance)
    {
        var c=Current;
        if(c==null||!c.mounted||c.character==null||__instance.Pointer!=c.character.Pointer)return;
        try{c.Apply();}catch(Exception ex){if(Time.realtimeSinceStartup>=c.nextError){c.nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("MOUNTED GUN aim: "+ex.Message);}}
    }
    private void Apply()
    {
        bool lever=Handles;
        if(lever?!leverValid:!desiredValid)return;
        if(horizontal==null||vertical==null||aim==null)return;
        horizontal.localRotation=baseH;vertical.localRotation=baseV;
        var up=horizontal.parent!=null?horizontal.parent.up:Vector3.up;
        var yaw=lever?leverYaw:MountedAimMath.Yaw(ContactWorld.V(aim.forward),ContactWorld.V(desired),ContactWorld.V(up),yawLimit);
        if(float.IsFinite(yaw)){horizontal.rotation=Quaternion.AngleAxis(yaw,up)*horizontal.rotation;appliedYaw=yaw;}
        var forward=aim.forward;
        var pitch=lever?leverPitch:MountedAimMath.Pitch(ContactWorld.V(forward),ContactWorld.V(desired),ContactWorld.V(up),pitchLimit);
        var axis=Vector3.Cross(Vector3.ProjectOnPlane(forward,up),up);
        if(float.IsFinite(pitch)&&axis.sqrMagnitude>1e-8f)vertical.rotation=Quaternion.AngleAxis(pitch,axis.normalized)*vertical.rotation;
        if(Time.realtimeSinceStartup>=nextReport){nextReport=Time.realtimeSinceStartup+5;Bootstrap.Write("MOUNTED GUN aim ("+(lever?"handles":"pointing")+") yaw="+yaw.ToString("F1")+" pitch="+pitch.ToString("F1")+" barrelHeading="+(Mathf.Atan2(aim.forward.x,aim.forward.z)*Mathf.Rad2Deg).ToString("F0")+(lever?" hands moved "+(lastSide*100).ToString("F1")+" cm (+ = the tracking frame's right)":""));}
    }
    private void Haptics(float now)
    {
        if(mount==null||now<nextHaptic||!rig.RightControls.Valid||(rig.RightControls.Held&HandControls.Trigger)==0)return;
        bool firing;try{firing=!mount.m_overheated&&(mount.IsActive()||mount.m_primaryFireComponent?.IsActive()==true);}catch(Exception){firing=true;}
        if(!firing)return;
        nextHaptic=now+.09f;rig.ShotHaptics("m60",true);
    }
    // Letting go of both grips leaves the gun (after a moment on it).
    private void Leave(float now)
    {
        bool held=rig.RightControls.Valid&&(rig.RightControls.Held&HandControls.Grip)!=0||rig.LeftControls.Valid&&(rig.LeftControls.Held&HandControls.Grip)!=0;
        if(held){releasedSince=-1;return;}
        if(releasedSince<0)releasedSince=now;
        if(leaveAt<0&&now-mountedSince>1&&now-releasedSince>.35f)
        {
            leaveAt=now;pressFrame=Time.frameCount+1;
            Bootstrap.Write("MOUNTED GUN both grips released: leaving");
        }
        if(leaveAt>=0&&now-leaveAt>1.5f&&character!=null&&character.isMounted)
        {
            leaveAt=now+30;
            try{character.UnMountCharacter(true);Bootstrap.Write("MOUNTED GUN left through the game's unmount (interact did not leave)");}
            catch(Exception ex){Bootstrap.Warn("MOUNTED GUN unmount: "+ex.Message);}
        }
    }
    // While on the gun the game's Interact is only this short press (leaving).
    internal bool InteractPress(int phase)
    {
        int f=Time.frameCount;
        return phase switch{0=>f>=pressFrame&&f<pressFrame+3,1=>f==pressFrame,_=>f==pressFrame+3};
    }
    public void Dispose()
    {
        if(Current==this)Current=null;
        try{patches.UnpatchSelf();}catch(Exception){}
        try{arms?.Dispose();}catch(Exception){}arms=null;
        mounted=false;
    }
}
