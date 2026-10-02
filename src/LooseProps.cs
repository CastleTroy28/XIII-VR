using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using PlayMagic.Weapons;
namespace XiiiXR;
// Hold real physics bodies using bounded velocities. Never parent/teleport them
// through walls or replace native pickup, NPC and projectile ownership.
internal sealed class LooseProps:IDisposable
{
    private sealed class Hand
    {
        internal Rigidbody? Body;internal Vector3 Offset,Previous,Velocity;
        internal Quaternion Relative,PreviousRotation;internal Vector3 Angular;
        internal bool Sampled,Gravity;internal float Drag,AngularDrag;internal readonly HeldPropMotion Motion=new();internal CollisionDetectionMode Collision;
        internal RigidbodyInterpolation Interpolation;internal bool Consumed;
    }
    private readonly Hand[] hands={new(),new()};
    private readonly Il2CppReferenceArray<Collider> overlaps=new(48);
    private readonly Il2CppStructArray<RaycastHit> hits=new(48);
    private readonly Dictionary<int,float> bumped=new();
    private readonly WorldPropBodies bodies=new();
    private readonly PunchingBags bags=new();
    private readonly List<(Rigidbody body,CollisionDetectionMode collision,RigidbodyInterpolation interpolation,float until)> released=new();
    private readonly ContactFilter filter=new();
    private float nextError;
    internal bool ConsumesRight=>Consumes(true);
    internal bool Consumes(bool right)=>hands[right?1:0].Consumed||hands[right?1:0].Body!=null;
    internal Transform? HeldRoot(bool right)=>hands[right?1:0].Body==null?null:hands[right?1:0].Body!.transform;
    internal bool Holding(bool right)=>hands[right?1:0].Body!=null;
    internal void Tick(CameraRig rig,bool allowed,PhysicalDoors doors)
    {
        RestoreFlights(false);
        try{bags.Tick();}catch(Exception ex){Bootstrap.Warn("BAG tick: "+ex.Message);}
        if(!allowed||!rig.SampleWorldHands(out var l,out var r,out bool lv)){Cancel();return;}
        for(int i=0;i<2;i++)
        {
            try{Step(i,rig,i==1?r:l,i==1?rig.RightControls:rig.LeftControls,(i==1||lv)&&!doors.Holding(i==1));}
            catch(Exception ex){Release(hands[i],false);if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("LOOSE PROPS: "+ex);}}
        }
    }
    private void Step(int side,CameraRig rig,PoseValue pose,HandControls input,bool valid)
    {
        var h=hands[side];h.Consumed=false;
        bool free=WeaponHands.Current?.HandFree(side==1)!=false&&(side==1||GripCarry.Current?.HidesLeft!=true);
        if(!valid||!input.Valid){Release(h,false);h.Sampled=false;return;}
        Vector3 p=CameraRig.UnityPosition(pose);Quaternion q=ControllerAim.Rotation(pose);
        if(!free){Release(h,false);if(side==1&&WeaponHands.Current?.TryMeleeTip(out var tip)==true)p=tip;else{h.Sampled=false;return;}}
        float dt=Math.Clamp(Time.unscaledDeltaTime,.004f,.05f);
        var delta=h.Sampled?p-h.Previous:Vector3.zero;
        if(delta.sqrMagnitude>.25f){Release(h,false);h.Sampled=false;delta=Vector3.zero;}
        var measured=Vector3.ClampMagnitude(delta/dt,8);
        h.Velocity=Vector3.Lerp(h.Velocity,measured,.55f);
        if(h.Sampled){var dq=q*Quaternion.Inverse(h.PreviousRotation);dq.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;h.Angular=Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad/dt),18);}
        if(h.Body!=null)
        {
            h.Consumed=true;
            if((input.Held&HandControls.Grip)==0){Release(h,true);}
            else
            {
                var target=p+q*h.Offset;
                if((target-h.Body.position).sqrMagnitude>.36f){Release(h,false);}
                else
                {
                    h.Motion.Follow(h.Body,target,q*h.Relative,dt);
                    h.Body.WakeUp();
                }
            }
        }
        else if(free&&(input.Down&HandControls.Grip)!=0)
        {
            int count=Physics.OverlapSphereNonAlloc(p,.16f,overlaps,~0,QueryTriggerInteraction.Ignore);
            if(count<overlaps.Length)
            {
                Rigidbody? chosen=null;float nearest=.16f;
                for(int j=0;j<count;j++)
                {
                    var c=overlaps[j];var body=Candidate(c,rig,true);if(body==null||body==hands[1-side].Body)continue;
                    if(!ColliderSurface.TryClosest(c,p,out var point))continue;
                    float d=(point-p).magnitude;if(d<nearest){nearest=d;chosen=body;}
                }
                if(chosen==null)ReportMiss(side,count,rig,p);
                if(chosen!=null)
                {
                    for(int flight=released.Count-1;flight>=0;flight--)if(released[flight].body==chosen)
                    {var old=released[flight];chosen.collisionDetectionMode=old.collision;chosen.interpolation=old.interpolation;released.RemoveAt(flight);}
                    h.Body=chosen;h.Gravity=chosen.useGravity;h.Drag=chosen.drag;h.AngularDrag=chosen.angularDrag;h.Motion.Bind(chosen);h.Collision=chosen.collisionDetectionMode;h.Interpolation=chosen.interpolation;
                    h.Offset=Quaternion.Inverse(q)*(chosen.position-p);h.Relative=Quaternion.Inverse(q)*chosen.rotation;
                    chosen.useGravity=false;chosen.velocity=Vector3.zero;chosen.angularVelocity=Vector3.zero;chosen.isKinematic=true;chosen.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;chosen.interpolation=RigidbodyInterpolation.Interpolate;
                    h.Consumed=true;rig.PunchHaptics(side==1);Bootstrap.Write("LOOSE PROP grip side="+side+" object="+chosen.name);
                }
            }
        }
        else if(h.Sampled&&delta.sqrMagnitude>1e-7f&&measured.magnitude>.7f)
        {
            int count=Physics.SphereCastNonAlloc(h.Previous,.065f,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(count<hits.Length)for(int j=0;j<count;j++)
            {
                var hit=hits[j];
                // 0.1.100: punching bags first (their own, stronger impulse);
                // the generic loose-prop push is capped for small props.
                if(bags.TryHit(hit.collider,hit.point,measured,rig.PlayerRoot)){rig.PunchHaptics(side==1);continue;}
                var body=Candidate(hit.collider,rig,true);
                if(body==null){bags.Report(hit.collider);continue;}
                if(body==hands[1-side].Body)continue;
                int id=body.GetInstanceID();if(bumped.TryGetValue(id,out float at)&&Time.realtimeSinceStartup<at)continue;
                if(bumped.Count>256)bumped.Clear();bumped[id]=Time.realtimeSinceStartup+.2f;
                body.AddForceAtPosition(Vector3.ClampMagnitude(measured*body.mass*.55f,5),hit.point,ForceMode.Impulse);rig.PunchHaptics(side==1);
            }
        }
        h.Previous=p;h.PreviousRotation=q;h.Sampled=true;
    }
    private Rigidbody? Candidate(Collider c,CameraRig rig,bool create)=>Candidate(c,rig,create,out _);
    private Rigidbody? Candidate(Collider c,CameraRig rig,bool create,out string why)
    {
        why="";
        if(c==null||!c.enabled||c.isTrigger){why="trigger volume";return null;}
        if(filter.Excluded(c)){why="level geometry";return null;}
        if(c.transform.IsChildOf(rig.PlayerRoot)){why="the player";return null;}
        bool named=PhysicalHandsMath.SmallProp(c.name)||(c.attachedRigidbody!=null&&PhysicalHandsMath.SmallProp(c.attachedRigidbody.name));
        if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null){why="a person";return null;}
        if(c.GetComponentInParent(Il2CppType.Of<Projectile>())!=null){why="a thrown thing";return null;}
        if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.Door>())!=null){why="a door";return null;}
        var equip=c.GetComponentInParent(Il2CppType.Of<Equipable>())?.TryCast<Equipable>();
        if(equip!=null){why="a weapon";return null;}
        // A chair/ashtray may also be a native pickup. Keep mission items and
        // weapon pickups protected, but allow named environmental props.
        if(c.GetComponentInParent(Il2CppType.Of<PickableItem>())!=null){why="the game's pickup (taken by pointing and the grip)";return null;}
        var action=c.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
        if(action!=null&&(!named||action.conditional!=RaycastAction.InteractionConditionals.Nothing)){why="the game's interaction";return null;}
        if(c.GetComponentInParent(Il2CppType.Of<CustomAnimationTool>())!=null){why="animated by the game";return null;}
        var body=bodies.Resolve(c,out why);
        return body;
    }
    // 0.1.149: a grip that took nothing says what was in reach and why not.
    private float nextMiss;
    private void ReportMiss(int side,int count,CameraRig rig,Vector3 p)
    {
        if(Time.realtimeSinceStartup<nextMiss)return;
        var seen=new List<string>();
        for(int j=0;j<count&&seen.Count<6;j++)
        {
            var c=overlaps[j];if(c==null)continue;
            Candidate(c,rig,false,out var why);if(why=="level geometry"||why=="the player"||why=="trigger volume")continue;
            seen.Add(c.name+(why.Length>0?" ("+why+")":""));
        }
        if(seen.Count==0)return;
        nextMiss=Time.realtimeSinceStartup+1.5f;
        Bootstrap.Write("LOOSE PROP "+(side==1?"right":"left")+" grip took nothing: "+string.Join(", ",seen));
    }

    private void Release(Hand h,bool thrown)
    {
        if(h.Body!=null)
        {
            h.Body.isKinematic=false;h.Body.useGravity=h.Gravity;h.Body.drag=h.Drag;h.Body.angularDrag=h.AngularDrag;h.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            if(thrown)released.Add((h.Body,h.Collision,h.Interpolation,Time.realtimeSinceStartup+3));
            else{h.Body.collisionDetectionMode=h.Collision;h.Body.interpolation=h.Interpolation;}
            h.Body.velocity=thrown?Vector3.ClampMagnitude(h.Velocity,8):Vector3.zero;
            h.Body.angularVelocity=thrown?h.Angular:Vector3.zero;
        }
        h.Body=null;
    }
    private void RestoreFlights(bool all)
    {
        for(int i=released.Count-1;i>=0;i--)
        {
            var f=released[i];if(f.body!=null&&!all&&Time.realtimeSinceStartup<f.until&&!f.body.IsSleeping())continue;
            if(f.body!=null){f.body.collisionDetectionMode=f.collision;f.body.interpolation=f.interpolation;}
            released.RemoveAt(i);
        }
    }
    internal void Cancel(){foreach(var h in hands){Release(h,false);h.Sampled=false;h.Consumed=false;h.Velocity=Vector3.zero;}}
    public void Dispose(){Cancel();RestoreFlights(true);bodies.Dispose();bags.Dispose();bumped.Clear();}
}
