using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.141: an enemy's gun is grabbed by its barrel with either
// grip and pushed aside; a blow of the other hand — an open palm (the
// controller let go) or a fist — makes the enemy let go of it, and it stays
// in the hand that held it.
//  - grip at the front part of an armed enemy's gun (within 13 cm): held;
//    while held the enemy does not shoot or walk, and its upper body and gun
//    arm are turned (after its animation) so the held point follows the hand;
//  - a fist on the enemy (any part) or an open hand swung at its gun hand,
//    its gun or its head (1.2 m/s or faster), or a hard yank of the holding
//    hand away from it (2.2 m/s): it lets go (the game drops the gun as a
//    pickup, the hand takes it), then it is stunned for a moment;
//  - letting go of the grip lets go of the gun.
internal sealed partial class NpcHitReactions
{
    // 0.1.151: 24 cm
    // (was 16) of the barrel, the gun or the enemy's gun hand.
    internal const float GrabReach=.24f,WristReach=.20f,GrabStrikeSpeed=1.2f,SlapReach=.13f,YankSpeed=2.2f,MaxSpineTurn=35f,MaxArmTurn=60f;
    // 0.1.146: the hand at most this fast (m/s) when the grip closes on the gun
    // (0.1.151: 2.6, was 0.9 - his reaching hands closed at 1.3-2.3 m/s; punches
    // are 3-6). A grip closed faster (a reach, under 3.2 m/s) still grabs if the
    // hand stops at the gun within 0.4 s with the grip held.
    internal const float GrabMaxSpeed=2.6f,LateGrabFrom=3.2f,LateGrabWindow=.4f,LateGrabSpeed=1.2f;
    private readonly float[] lateGrabUntil=new float[2];
    private sealed class GunGrab
    {
        internal NPC Npc=null!;internal EnemyInventory Inv=null!;internal Equipable Weapon=null!;internal int Side;
        internal Vector3 Local,Hand,LastHand,Velocity;internal float Since,LastTime=-1;
        internal Transform? Spine,Shoulder;internal Transform? Wrist;
    }
    private readonly GunGrab?[] grabs=new GunGrab?[2];
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> near=new(96);
    private readonly Vector3[] slapLast=new Vector3[2];private readonly float[] slapTime={-1,-1};
    internal bool Grabbing(bool right)=>grabs[right?1:0]!=null;
    private int GrabSide(IntPtr npc)
    {
        for(int s=0;s<2;s++){var g=grabs[s];if(g!=null&&g.Npc!=null&&g.Npc.Pointer==npc)return s;}
        return -1;
    }
    private static string Side(int s)=>s==0?"left":"right";
    // From WeaponHands.Tick, before the holsters (a grip press on an enemy's
    // gun is not also a take of a weapon of the player's).
    internal void TickGrab(CameraRig rig)
    {
        if(!WeaponOptions.NpcGunGrab.Value){ReleaseAllGrabs(null);return;}
        try
        {
            if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)){ReleaseAllGrabs("tracking lost");return;}
            float now=Time.time;
            for(int s=0;s<2;s++)
            {
                var c=s==1?rig.RightControls:rig.LeftControls;bool valid=c.Valid&&(s==1||leftValid);
                var pose=s==0?l:r;var palm=CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,s==1)*new Vector3(0,-.02f,.06f);
                var g=grabs[s];
                if(g!=null)
                {
                    if(!valid||(c.Held&HandControls.Grip)==0){ReleaseGrab(s,"the grip let go");continue;}
                    if(g.Npc==null||g.Weapon==null||!g.Npc.IsAlive||!g.Npc.IsConscious||g.Npc.isRagdoll||g.Inv.CurrentWeapon==null||g.Inv.CurrentWeapon.Pointer!=g.Weapon.Pointer){ReleaseGrab(s,"the enemy is down or has no gun");continue;}
                    float dt=g.LastTime<0?0:now-g.LastTime;
                    if(dt>1e-4f&&dt<.1f)g.Velocity=Vector3.Lerp(g.Velocity,(palm-g.LastHand)/dt,.5f);
                    g.LastHand=palm;g.LastTime=now;g.Hand=palm;
                    // A hard yank away from the enemy.
                    var away=palm-g.Npc.transform.position;away.y=0;
                    if(now-g.Since>.3f&&away.sqrMagnitude>1e-4f&&Vector3.Dot(g.Velocity,away.normalized)>YankSpeed){var t=DisarmGrabbed(s,"a yank");if(reports++<80)Bootstrap.Write("NPC GUN"+t);continue;}
                    continue;
                }
                bool fresh=(c.Down&HandControls.Grip)!=0,late=!fresh&&lateGrabUntil[s]>now&&(c.Held&HandControls.Grip)!=0;
                if(!valid||(c.Held&HandControls.Grip)==0)lateGrabUntil[s]=0;
                if(!valid||!fresh&&!late)continue;
                // 0.1.146: a grab is a grip pressed with the hand (nearly)
                // still at the gun, not a fist closed during a swing.
                float pressDt=now-slapTime[s];
                float pressSpeed=slapTime[s]>=0&&pressDt>1e-4f&&pressDt<.1f?(palm-slapLast[s]).magnitude/pressDt:0;
                if(pressSpeed>(late?LateGrabSpeed:GrabMaxSpeed))
                {
                    if(fresh&&pressSpeed<LateGrabFrom)lateGrabUntil[s]=now+LateGrabWindow;
                    if(fresh&&Time.realtimeSinceStartup>=nextGrabReport&&NearNpc(palm)){nextGrabReport=Time.realtimeSinceStartup+2;Bootstrap.Write("NPC GUN "+Side(s)+" grip closed during a swing ("+pressSpeed.ToString("F1")+" m/s): "+(pressSpeed<LateGrabFrom?"a grab if the hand stops at the gun":"a fist, not a grab"));}
                    continue;
                }
                lateGrabUntil[s]=0;
                string? busy=WeaponHands.Current?.HandFree(s==1)==false?"it holds a weapon or is at a body place":InteractionDriver.Current?.HandOccupied(s==1)==true?"it holds a door, prop or body":null;
                if(busy!=null)
                {
                    if(Time.realtimeSinceStartup>=nextGrabReport&&NearNpc(palm)){nextGrabReport=Time.realtimeSinceStartup+2;Bootstrap.Write("NPC GUN "+Side(s)+" grip near an enemy: not a grab ("+busy+")");}
                    continue;
                }
                TryGrab(s,palm,rig);
            }
            // The other hand's open palm swung at the held enemy.
            for(int s=0;s<2;s++)
            {
                int o=1-s;var g=grabs[s];var c=o==1?rig.RightControls:rig.LeftControls;
                var pose=o==0?l:r;var palm=CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,o==1)*new Vector3(0,-.02f,.06f);
                float last=slapTime[o];var from=slapLast[o];slapLast[o]=palm;slapTime[o]=now;
                if(g==null||grabs[o]!=null||!c.Valid||(o==0&&!leftValid)||(c.Held&HandControls.Grip)!=0||last<0)continue;
                float dt=now-last;if(!(dt>1e-4f)||dt>.1f)continue;
                float speed=(palm-from).magnitude/dt;if(speed<GrabStrikeSpeed)continue;
                if(!SlapTarget(g,from,palm,out var point,out string part))continue;
                Hit(g.Npc,null,point,palm-from,speed,false,from);
                if(grabs[s]!=null){var t=DisarmGrabbed(s,"an open-hand slap ("+part+")");if(reports++<80)Bootstrap.Write("NPC GUN"+t);}
                rig.PunchHaptics(o==1);
            }
        }
        catch(Exception ex){ReleaseAllGrabs(null);if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC GUN grab: "+ex.Message);}}
    }
    // 0.1.142: within reach of the gun's barrel line or of its drawn mesh (the
    // hand went through the gun and nothing held it: no log either -
    // now every grip press near an armed enemy says why when it takes nothing).
    private float nextGrabReport;
    private void TryGrab(int s,Vector3 palm,CameraRig rig)
    {
        int count=Physics.OverlapSphereNonAlloc(palm,1.2f,near,~0,QueryTriggerInteraction.Collide);
        GunGrab? best=null;float bestD=GrabReach;var seen=new HashSet<IntPtr>();string why="";
        for(int i=0;i<Math.Min(count,near.Length);i++)
        {
            var col=near[i];if(col==null)continue;
            NPC? npc=null;try{npc=col.GetComponentInParent(Il2CppType.Of<NPC>())?.TryCast<NPC>();}catch(Exception){}
            if(npc==null||!seen.Add(npc.Pointer))continue;
            if(NpcAllies.Ally(npc)){why+=" "+npc.name+": an ally;";continue;}
            if(!npc.IsAlive||!npc.IsConscious||npc.isRagdoll){why+=" "+npc.name+": down;";continue;}
            if(npc.isHeldByPlayer||GrabSide(npc.Pointer)>=0){why+=" "+npc.name+": held already;";continue;}
            var inv=AnimOf(npc)?.Inventory;if(inv==null)inv=npc.GetComponentInChildren(Il2CppType.Of<EnemyInventory>(),true)?.TryCast<EnemyInventory>();
            if(inv==null){why+=" "+npc.name+": no inventory;";continue;}
            var weapon=inv.CurrentWeapon;
            if(weapon==null||!Armed(inv.CurrentWeaponSlot)){why+=" "+npc.name+": no gun in its hands ("+inv.CurrentWeaponSlot+");";continue;}
            Transform? muzzle=null;try{muzzle=inv.GetCurrentWeaponMuzzle();}catch(Exception){}
            var rear=weapon.transform.position;var front=muzzle!=null?muzzle.position:rear+weapon.transform.forward*.5f;
            var ab=front-rear;float len2=ab.sqrMagnitude;
            // The barrel line (not the handle), and the drawn gun itself.
            float t=len2<1e-4f?1:Mathf.Clamp(Vector3.Dot(palm-rear,ab)/len2,.05f,1.15f);var at=rear+ab*t;
            float d=Vector3.Distance(palm,at);
            float meshD=MeshDistance(weapon,palm,out var meshPoint);
            if(meshD<d){d=meshD;at=meshPoint;}
            // 0.1.151: the enemy's hand holding it counts as the gun.
            var bones=BonesOf(npc);float wristD=float.PositiveInfinity;
            if(bones!=null)foreach(var w in new[]{bones.rightWrist,bones.leftWrist})
                if(w!=null&&Vector3.Distance(w.position,rear)<.25f)wristD=Math.Min(wristD,Vector3.Distance(palm,w.position));
            if(wristD<WristReach&&wristD*GrabReach/WristReach<d){d=wristD*GrabReach/WristReach;at=rear;}
            if(d>=bestD){why+=" "+npc.name+": gun "+d.ToString("F2")+" m from the palm (barrel line "+Vector3.Distance(palm,rear+ab*t).ToString("F2")+", mesh "+(float.IsFinite(meshD)?meshD.ToString("F2"):"none")+", its hand "+(float.IsFinite(wristD)?wristD.ToString("F2"):"none")+", muzzle "+(muzzle!=null?muzzle.name:"none")+");";continue;}
            bestD=d;
            var rig2=bones;Transform? shoulder=null,wrist=null;
            if(rig2!=null)
            {
                float dr=rig2.rightWrist!=null?Vector3.Distance(rig2.rightWrist.position,rear):99,dl=rig2.leftWrist!=null?Vector3.Distance(rig2.leftWrist.position,rear):99;
                bool right=dr<=dl;shoulder=right?rig2.rightShoulder:rig2.leftShoulder;wrist=right?rig2.rightWrist:rig2.leftWrist;
            }
            best=new GunGrab{Npc=npc,Inv=inv,Weapon=weapon,Side=s,Local=weapon.transform.InverseTransformPoint(at),Hand=palm,LastHand=palm,Since=Time.time,Spine=rig2?.spineTop,Shoulder=shoulder,Wrist=wrist};
        }
        if(best==null)
        {
            if(why.Length>0&&Time.realtimeSinceStartup>=nextGrabReport){nextGrabReport=Time.realtimeSinceStartup+2;Bootstrap.Write("NPC GUN "+Side(s)+" grip: nothing taken (within "+GrabReach.ToString("F2")+" m of the gun):"+why);}
            return;
        }
        grabs[s]=best;StopFiring(best.Npc);rig.PunchHaptics(s==1);
        if(reports++<80)Bootstrap.Write("NPC GUN "+Side(s)+" hand grabbed "+best.Npc.name+"'s "+best.Weapon.name+" by the barrel ("+bestD.ToString("F2")+" m): it cannot shoot; push it aside, a blow of the other hand (palm or fist) or a yank makes it let go");
    }
    private bool NearNpc(Vector3 palm)
    {
        int count=Physics.OverlapSphereNonAlloc(palm,.6f,near,~0,QueryTriggerInteraction.Collide);
        for(int i=0;i<Math.Min(count,near.Length);i++){var col=near[i];if(col!=null&&col.GetComponentInParent(Il2CppType.Of<NPC>())!=null)return true;}
        return false;
    }
    // Distance from the palm to the gun's drawn meshes (their boxes, 0 inside).
    private static float MeshDistance(Equipable weapon,Vector3 palm,out Vector3 point)
    {
        point=palm;float best=float.PositiveInfinity;
        try
        {
            foreach(var o in weapon.GetComponentsInChildren(Il2CppType.Of<Renderer>(),false))
            {
                var r=o.TryCast<Renderer>();if(r==null||!r.enabled)continue;
                var b=r.bounds;if(b.size.sqrMagnitude<1e-6f||b.size.magnitude>2.5f)continue;
                var c=b.ClosestPoint(palm);float d=Vector3.Distance(c,palm);
                if(d<best){best=d;point=c;}
            }
        }
        catch(Exception){}
        return best;
    }
    // An open-hand swing from a to b passing the held enemy's gun hand, gun or head.
    private static bool SlapTarget(GunGrab g,Vector3 a,Vector3 b,out Vector3 point,out string part)
    {
        point=b;part="";float margin=float.PositiveInfinity;
        var targets=new List<(Vector3 at,string name,float reach)>();
        if(g.Wrist!=null)targets.Add((g.Wrist.position,"its gun hand",SlapReach));
        if(g.Weapon!=null)targets.Add((g.Weapon.transform.position,"the gun",SlapReach));
        var bones=Current?.BonesOf(g.Npc);var head=bones!=null&&bones.Usable?bones.Head:null;if(head!=null)targets.Add((head.position+Vector3.up*.08f,"its head",.16f));
        foreach(var (at,name,reach) in targets)
        {
            float d=HitReactionMath.Segment(ContactWorld.V(at),ContactWorld.V(a),ContactWorld.V(b));
            if(d<reach&&d-reach<margin){margin=d-reach;point=at;part=name;}
        }
        return part.Length>0;
    }
    // After the enemy's animation: its upper body, then its gun arm, turned so
    // that the held point of the gun follows the hand.
    private void ApplyGrabs()
    {
        for(int s=0;s<2;s++)
        {
            var g=grabs[s];if(g==null)continue;
            try
            {
                if(g.Weapon==null||g.Npc==null)continue;
                var w=g.Weapon.transform;
                Turn(g.Spine,w,g.Local,g.Hand,MaxSpineTurn*.5f);
                Turn(g.Shoulder,w,g.Local,g.Hand,MaxArmTurn);
                // The reaction layer's guard sees these bones as animated next frame.
                if(bodies.TryGetValue(g.Npc.Pointer,out var b))
                {
                    int top=(int)HitJoint.SpineTop;if(b.Joints[top]!=null&&b.Joints[top]==g.Spine){b.Applied[top]=g.Spine!.localRotation;}
                    for(int i=(int)HitJoint.ArmL;i<=(int)HitJoint.ArmR;i++)if(b.Joints[i]!=null&&b.Joints[i]==g.Shoulder)b.Applied[i]=g.Shoulder!.localRotation;
                }
            }
            catch(Exception ex){ReleaseGrab(s,"pose failed: "+ex.Message);}
        }
    }
    private static void Turn(Transform? pivot,Transform gun,Vector3 local,Vector3 hand,float maxDegrees)
    {
        if(pivot==null)return;
        var held=gun.TransformPoint(local);var from=held-pivot.position;var to=hand-pivot.position;
        if(from.sqrMagnitude<1e-4f||to.sqrMagnitude<1e-4f)return;
        var q=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(from,to),maxDegrees);
        pivot.rotation=q*pivot.rotation;
    }
    // The held enemy lets go: the game drops the gun, the holding hand takes it.
    private string DisarmGrabbed(int s,string why)
    {
        var g=grabs[s];if(g==null)return "";
        grabs[s]=null;
        var at=g.Weapon!=null?g.Weapon.transform.position:g.Hand;
        string result=Disarm(g.Npc,null,null,at,true);
        StunFor(g.Npc,.45f,false);
        if(result.Contains("DISARMED")){handPickupSide=s;handPickupAt=at;handPickupUntil=Time.realtimeSinceStartup+1.5f;}
        return " "+Side(s)+" hand: "+g.Npc.name+" let go of its gun after "+why+":"+result;
    }
    private int handPickupSide=-1;private Vector3 handPickupAt;private float handPickupUntil;
    // The dropped gun (the game's pickup) into the hand that held it.
    internal void TickHandPickup()
    {
        if(handPickupSide<0)return;
        if(Time.realtimeSinceStartup>handPickupUntil){if(reports++<80)Bootstrap.Write("NPC GUN the dropped gun did not show up as a pickup; take it from the ground");handPickupSide=-1;return;}
        WeaponPickup? best=null;float bestD=1.5f;
        foreach(var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<WeaponPickup>()))
        {
            var p=o.TryCast<WeaponPickup>();if(p==null||!p.isActiveAndEnabled||p.wasPicked||p.isAIOnlyWeapon)continue;
            float d=Vector3.Distance(p.transform.position,handPickupAt);if(d<bestD){bestD=d;best=p;}
        }
        if(best==null)return;
        int s=handPickupSide;handPickupSide=-1;
        var hands=WeaponHands.Current;
        bool ok=hands?.PickUpInto(best,s)==true;
        if(ok)hands!.KeepSnatched(s);
        if(reports++<80)Bootstrap.Write("NPC GUN "+best.name+" into the "+Side(s)+" hand: "+(ok?"taken (it stays in the hand when the grip lets go)":"the game refused (take it from the ground)"));
    }
    private void ReleaseGrab(int s,string? why)
    {
        var g=grabs[s];if(g==null)return;grabs[s]=null;
        if(why!=null&&reports++<80)Bootstrap.Write("NPC GUN "+Side(s)+" hand let go of "+(g.Npc!=null?g.Npc.name:"the enemy")+"'s gun ("+why+")");
        if(g.Npc!=null&&g.Npc.IsAlive)StunFor(g.Npc,.4f,false);
    }
    private void ReleaseAllGrabs(string? why){ReleaseGrab(0,why);ReleaseGrab(1,why);handPickupSide=-1;}
}
