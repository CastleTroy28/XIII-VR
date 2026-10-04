using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic.AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.98: trigger on an EMPTY hand grabs an NPC body.
// - Knocked out / dead NPC: the touched ragdoll bone is pulled to the hand by a
//   spring joint on a kinematic anchor, so the rest of the body follows with
//   the game's own ragdoll physics and collisions (drag, lift, throw).
// - Conscious NPC: the native hostage take (same rules as the left grip);
//   holding that trigger keeps the hostage, releasing it knocks him out.
internal sealed class BodyGrab:IDisposable
{
    private const float Reach=.13f,Spring=2200,Damper=140,MaxForce=1500;
    private sealed class Hand
    {
        internal NPC? Npc;internal Rigidbody? Bone;internal GameObject? Anchor;internal Rigidbody? AnchorBody;
        internal Vector3 Offset,Local;internal float StretchedSince,NextHaptic;internal SimpleRagdollController? Ragdoll;
        internal bool NeverSleep;
    }
    private readonly Hand[] hands={new(),new()};
    private readonly Il2CppReferenceArray<Collider> nearby=new(64);
    private float nextReject;
    internal bool Holding(bool right)=>hands[right?1:0].Bone!=null;
    internal void Tick(CameraRig rig,bool allowed)
    {
        if(!allowed||!rig.SampleWorldHands(out var l,out var r,out bool lv)){Cancel();return;}
        for(int i=0;i<2;i++)
        {
            try{Step(i,rig,i==1?r:l,i==1?rig.RightControls:rig.LeftControls,i==1||lv);}
            catch(Exception ex){Release(hands[i],"error");Bootstrap.Warn("BODY GRAB: "+ex.Message);}
        }
    }
    private void Step(int side,CameraRig rig,PoseValue pose,HandControls input,bool valid)
    {
        var h=hands[side];bool right=side==1;
        var p=CameraRig.UnityPosition(pose);var q=ControllerAim.Rotation(pose);
        if(h.Bone!=null)
        {
            string why=!valid||!input.Valid?"tracking"
                :(input.Held&HandControls.Trigger)==0?"trigger released"
                :h.Npc==null||h.Anchor==null||h.Bone==null||!h.Bone.gameObject.activeInHierarchy?"gone"
                :h.Npc.actorStatus==ActorStatus.Conscious?"woke up"
                :CarriedNatively(h.Npc)?"native carry":"";
            if(why.Length>0){Release(h,why);return;}
            Hold(h,rig,right,p,q);
            return;
        }
        if(!valid||!input.Valid||(input.Down&HandControls.Trigger)==0)return;
        if(WeaponHands.Current?.HandFree(right)==false||InteractionDriver.Current?.HandOccupied(right)==true)return;
        // Left trigger near the gun during a manual reload belongs to the reload.
        if(right==WeaponHands.Current?.ReloadRight&&WeaponHands.Current?.TryReloadAccess(p,q,out _,out _)==true)return;
        Grab(h,rig,right,p,q);
    }
    private void Grab(Hand h,CameraRig rig,bool right,Vector3 p,Quaternion q)
    {
        int count=Physics.OverlapSphereNonAlloc(p,Reach,nearby,~0,QueryTriggerInteraction.Collide);
        Rigidbody? bone=null;NPC? limp=null,conscious=null;Collider? consciousCollider=null;Vector3 grip=p;float best=float.MaxValue,bestAwake=float.MaxValue;
        for(int i=0;i<Math.Min(count,nearby.Length);i++)
        {
            var c=nearby[i];if(c==null||!c.enabled)continue;
            if(rig.PlayerRoot!=null&&c.transform.IsChildOf(rig.PlayerRoot))continue;
            var npc=c.GetComponentInParent(Il2CppType.Of<NPC>())?.TryCast<NPC>();if(npc==null||CarriedNatively(npc)||NpcAllies.Ally(npc))continue;
            if(!ColliderSurface.TryClosest(c,p,out var point))point=c.bounds.center;
            float d=(point-p).sqrMagnitude;
            if(npc.actorStatus==ActorStatus.KO||npc.actorStatus==ActorStatus.Dead)
            {
                var body=c.attachedRigidbody;if(body==null||!Owned(npc,body)||d>=best)continue;
                best=d;bone=body;limp=npc;grip=point;
            }
            else if(npc.actorStatus==ActorStatus.Conscious&&d<bestAwake){bestAwake=d;conscious=npc;consciousCollider=c;}
        }
        if(bone!=null&&limp!=null){Begin(h,rig,right,limp,bone,grip,p,q);return;}
        if(conscious!=null&&consciousCollider!=null)
        {
            if(GripCarry.Current?.TryTriggerHostage(right,conscious,consciousCollider,p)==true){rig.PunchHaptics(right);return;}
            if(Time.realtimeSinceStartup>=nextReject){nextReject=Time.realtimeSinceStartup+2;Bootstrap.Write("BODY GRAB conscious "+conscious.name+" not takeable (hostage needs the native conditions: from behind, unaware)");}
        }
    }
    private static bool CarriedNatively(NPC npc){var carried=GripCarry.Current?.BodyRoot;return carried!=null&&carried==npc.transform;}
    private static bool Owned(NPC npc,Rigidbody body)
    {
        var all=npc.allRigidBodies;
        if(all==null||all.Length==0)return true;
        for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].Pointer==body.Pointer)return true;
        return false;
    }
    private void Begin(Hand h,CameraRig rig,bool right,NPC npc,Rigidbody bone,Vector3 grip,Vector3 p,Quaternion q)
    {
        var ragdoll=npc.ragdollControllerComponent;
        string before=(ragdoll!=null?ragdoll.m_currentState.ToString():"none")+(npc.isRagdoll?"":" animated");
        var other=hands[right?0:1];
        // A knocked-out NPC still in its fall animation: the game's own
        // animation-to-ragdoll switch first (animator off, bones live).
        if(bone.isKinematic&&npc.usesRagdolls&&!npc.isRagdoll)npc.ActivateRagdollBehaviour(false);
        if(ragdoll!=null&&other.Npc!=null&&other.Npc.Pointer==npc.Pointer)h.NeverSleep=other.NeverSleep;
        else if(ragdoll!=null)
        {
            // A settled (sleeping) ragdoll is frozen kinematic: wake it and
            // keep it awake while held.
            h.NeverSleep=ragdoll.neverSleep;ragdoll.neverSleep=true;
            if(ragdoll.m_currentState!=RagdollState.Alive)ragdoll.SetRagdollState(RagdollState.Alive,false);
        }
        if(bone.isKinematic)
        {
            if(!(other.Npc!=null&&other.Npc.Pointer==npc.Pointer))Restore(ragdoll,h);
            Bootstrap.Write("BODY GRAB rejected "+npc.name+" bone="+bone.name+": ragdoll is kinematic (state="+before+" isRagdoll="+npc.isRagdoll+")");
            return;
        }
        var anchor=new GameObject("XIII VR body grab");anchor.transform.position=grip;
        var anchorBody=anchor.AddComponent(Il2CppType.Of<Rigidbody>()).TryCast<Rigidbody>()!;
        anchorBody.isKinematic=true;anchorBody.useGravity=false;
        var joint=anchor.AddComponent(Il2CppType.Of<ConfigurableJoint>()).TryCast<ConfigurableJoint>()!;
        joint.autoConfigureConnectedAnchor=false;joint.connectedBody=bone;joint.anchor=Vector3.zero;
        var local=bone.transform.InverseTransformPoint(grip);joint.connectedAnchor=local;
        joint.xMotion=ConfigurableJointMotion.Free;joint.yMotion=ConfigurableJointMotion.Free;joint.zMotion=ConfigurableJointMotion.Free;
        joint.angularXMotion=ConfigurableJointMotion.Free;joint.angularYMotion=ConfigurableJointMotion.Free;joint.angularZMotion=ConfigurableJointMotion.Free;
        var drive=new JointDrive{positionSpring=Spring,positionDamper=Damper,maximumForce=MaxForce};
        joint.xDrive=drive;joint.yDrive=drive;joint.zDrive=drive;joint.targetPosition=Vector3.zero;
        joint.enableCollision=false;joint.enablePreprocessing=false;
        bone.WakeUp();
        h.Npc=npc;h.Bone=bone;h.Anchor=anchor;h.AnchorBody=anchorBody;h.Ragdoll=ragdoll;h.Offset=Quaternion.Inverse(q)*(grip-p);h.Local=local;h.StretchedSince=0;h.NextHaptic=0;
        if(right)BodyGrabState.Right=npc.transform;else BodyGrabState.Left=npc.transform;
        rig.PunchHaptics(right);
        Bootstrap.Write("BODY GRAB "+(right?"right":"left")+" "+npc.name+" status="+npc.actorStatus+" bone="+bone.name+" mass="+bone.mass.ToString("F1")+" ragdoll="+before+" isRagdoll="+npc.isRagdoll+" reach="+(grip-p).magnitude.ToString("F2"));
    }
    private void Hold(Hand h,CameraRig rig,bool right,Vector3 p,Quaternion q)
    {
        var target=p+q*h.Offset;
        h.AnchorBody!.MovePosition(target);
        if(h.Ragdoll!=null&&h.Ragdoll.m_currentState!=RagdollState.Alive)h.Ragdoll.SetRagdollState(RagdollState.Alive,false);
        var held=h.Bone!.transform.TransformPoint(h.Local);
        float stretch=(held-target).magnitude,now=Time.realtimeSinceStartup;
        if(stretch>.9f){if(h.StretchedSince==0)h.StretchedSince=now;else if(now-h.StretchedSince>.5f){Release(h,"stretched "+stretch.ToString("F2")+"m");return;}}
        else h.StretchedSince=0;
        // Weight: short pulses that get denser with the pull.
        if(now>=h.NextHaptic&&stretch>.05f){h.NextHaptic=now+Math.Clamp(.35f-stretch*.5f,.1f,.35f);rig.PunchHaptics(right);}
    }
    private void Release(Hand h,string why)
    {
        if(h.Bone==null&&h.Anchor==null)return;
        try
        {
            if(h.Anchor!=null)UnityEngine.Object.Destroy(h.Anchor);
            var other=h==hands[1]?hands[0]:hands[1];
            if(!(other.Npc!=null&&h.Npc!=null&&other.Npc.Pointer==h.Npc.Pointer))Restore(h.Ragdoll,h);
            Bootstrap.Write("BODY GRAB released ("+why+") "+(h.Npc!=null?h.Npc.name:"?"));
        }
        finally
        {
            if(h==hands[1])BodyGrabState.Right=null;else BodyGrabState.Left=null;
            h.Npc=null;h.Bone=null;h.Anchor=null;h.AnchorBody=null;h.Ragdoll=null;
        }
    }
    private static void Restore(SimpleRagdollController? ragdoll,Hand h)
    {
        if(ragdoll==null)return;
        ragdoll.neverSleep=h.NeverSleep;
    }
    internal void Cancel(){foreach(var h in hands)Release(h,"cancel");}
    public void Dispose()=>Cancel();
}
