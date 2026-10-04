using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private readonly ContactFilter throwFilter=new();
    // 0.1.149: the game's thing in the hand can be thrown (a bottle, the ashtray; not a chair or a broom).
    internal bool PropThrowable=>profile=="prop"&&propThrow!=null;
    private Quaternion throwRotation;
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> throwHits=new(32);
    private Equipable? throwSpent;
    private ThrowingComponent? propThrow;
    private RigidMeshVisual? throwVisual;
    private float nextThrowPreview;
    private GameObject? throwRoot;
    private bool throwAiming;
    private Vector3 throwOrigin,throwLaunch;
    private float launchUntil;
    private float nextThrowError;
    private string throwStage="idle";
    private bool throwTaskIssued;
    private void TickPropThrow()
    {
        try{UpdatePropThrow();}
        catch(Exception ex){ClearThrow();if(Time.realtimeSinceStartup>=nextThrowError){nextThrowError=Time.realtimeSinceStartup+5;Bootstrap.Warn("PROP THROW stage="+throwStage+": "+ex);}}
    }
    private void UpdatePropThrow()
    {
        if(throwSpent!=null)
        {
            var spent=throwSpent;throwSpent=null;
            if(inventory?.currentEquipable!=null&&inventory.currentEquipable.Pointer==spent.Pointer)inventory.RemoveAndSwitch(spent);
            return;
        }
        if(profile!="prop"||weapon==null||!CanControl(playerId)||inventory?.isInTransit==true)
        {throwAiming=false;if(throwRoot!=null)throwRoot.SetActive(false);return;}
        if(propThrow==null||propThrow.baseEquipable==null||propThrow.baseEquipable.Pointer!=weapon.Pointer)
            propThrow=weapon.GetComponent(Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>();
        if(propThrow==null)return;
        RenderPose();if(!poseValid||visual==null)return;
        // 0.1.150: in the hand that holds it (the left one for a left-hander).
        int side=PrimaryLeft?0:1;
        var input=side==1?rig.RightControls:rig.LeftControls;
        if(!input.Valid){if(throwAiming){throwAiming=false;throwRoot?.SetActive(false);}return;}
        // 0.1.149: held by the right grip
        // ("Weapon in hand" setting); letting go in a swing throws it (a harder
        // swing a little further than the game's own speed), letting go without
        // one drops it from the hand. "Throwing: hold + flight path": the arc is
        // drawn while the grip is held, letting go throws along the controller.
        var mode=GripMode;bool held=(input.Held&HandControls.Grip)!=0,down=(input.Down&HandControls.Grip)!=0;
        int key=weapon.GetInstanceID();float now=Time.realtimeSinceStartup;
        bool fresh=ThrowHeld(side,key,mode,held);
        // 0.1.222: grabbed and let go in a swing before the game had it in the hand.
        NoteLateThrow(side,key,fresh,held,mode,now);
        if(!rig.SampleWorldHands(out var left,out var right,out bool leftValid)||side==0&&!leftValid)return;
        var aim=ControllerAim.Rotation(side==1?right:left)*Vector3.forward;
        var origin=visual.FittedToWorld.MultiplyPoint3x4(HandleSided(NativeGrip(true,Vector3.zero)));
        float native=ThrowTrajectory.Speed(weapon.CurrentEquipableParameters.primaryProjectileSpeed);
        bool arc=QualityOptions.ThrowArc.Value;
        if(LateThrow(side,key,origin,native,now))return;
        if(held&&throwGrips[side].Armed)AimPropLanding(side,origin,aim,native,arc);
        if(arc&&held&&throwGrips[side].Armed){throwAiming=true;DrawThrowPreview(origin+aim*.09f,aim*native,true,4);}
        else if(throwAiming){throwAiming=false;throwRoot?.SetActive(false);}
        Vector3 direction=default;float speed=0;string why="";
        var step=throwGrips[side].Step(mode,held,down,()=>
        {
            if(arc){direction=aim;why="aimed";return true;}
            return SwingThrow(side,out direction,out speed,out why);
        });
        if(step==ThrowGripStep.None)return;
        // 0.1.222: this let-go is dealt with (not thrown again as a late swing next frame).
        throwHeldKey[side]=-1;letGo[side].Spend();lateKey=-1;
        if(throwAiming){throwAiming=false;throwRoot?.SetActive(false);}
        throwRotation=visual.FittedToWorld.rotation;
        if(step==ThrowGripStep.Throw)
        {
            // 0.1.150: the grip's own hold of the thing (GripCarry: a deliberate
            // hold let go drops it through the game) must not drop it as well.
            GripCarry.Current?.Forget(weapon);
            throwOrigin=origin+direction*.09f;
            throwLaunch=direction*(arc?native:native*Math.Clamp(speed/3f,.8f,1.35f));
            LaunchProp((side==0?"left ":"")+(arc?"aimed, grip let go":"grip let go handSpeed="+speed.ToString("F2")),side==1);
            return;
        }
        // 0.1.150: let go without a swing: the game's own drop (GripCarry: a
        // deliberate hold puts it down where it can be taken again, a tap keeps it).
        Bootstrap.Write("PROP let go without a swing ("+why+(speed>0?" handSpeed="+speed.ToString("F2"):"")+"): put down");
    }
    // 0.1.159: right=false: thrown by the left hand (its controller pulses, not the right one).
    private void LaunchProp(string how,bool right=true)
    {
        throwStage="release CanStart";
        if(propThrow==null||!propThrow.CanStart())return;
        launchUntil=Time.realtimeSinceStartup+2;throwTaskIssued=false;throwProjectiles.Clear();throwTraceLeft=12;throwTraceFrom=Time.realtimeSinceStartup;
        WriteMuzzle(propThrow);
        // 0.1.214: where it lands, marked until it gets there.
        FreezeLanding(right?1:0,throwOrigin,throwLaunch,Physics.gravity);
        propThrow.usageType=propThrow.isPrimary?PlayerEquipableHandler.UsageType.primary:PlayerEquipableHandler.UsageType.secondary;
        // Begin initializes damage/ammo/animation dependencies that HandleProjectile
        // alone skipped. Execute once; suppress the later animation duplicate.
        throwStage="Begin";propThrow.Begin(propThrow.usageType);
        if(throwSpent==null){throwStage="ExecuteFireTask";propThrow.ExecuteFireTask();}
        rig.PunchHaptics(right);if(!right)leftShotUntil=Time.realtimeSinceStartup+.8f;throwStage="await launch";
        Bootstrap.Write("PROP THROW release "+how+" velocity="+throwLaunch.ToString("F2"));
    }
    // Flight path preview (gravity arc for props, straight line for knives),
    // stopped at the first solid hit.
    private void DrawThrowPreview(Vector3 origin,Vector3 launch,bool gravity,float seconds)
    {
        if(throwRoot==null)
        {
            throwRoot=new GameObject("XIII prop throw preview");
            throwVisual=new RigidMeshVisual(throwRoot.transform,"Throw arc",true);
        }
        throwRoot.SetActive(true);
        if(Time.realtimeSinceStartup<nextThrowPreview)return;nextThrowPreview=Time.realtimeSinceStartup+.04f;
        var mesh=new HandMeshGeometry(true);
        var color=new System.Numerics.Vector4(.9f,.8f,.35f,1);
        var previous=origin;
        var simulation=ContactWorld.V(origin);var velocity=ContactWorld.V(launch);
        var g=gravity?ContactWorld.V(Physics.gravity):System.Numerics.Vector3.Zero;
        float step=Math.Clamp(Time.fixedDeltaTime,.005f,.04f);
        for(int i=0;i<(int)(seconds/step);i++)
        {
            var point=ContactWorld.U(ThrowTrajectory.Step(ref simulation,ref velocity,g,step));
            var delta=point-previous;bool stop=false;float distance=delta.magnitude;
            int count=Physics.SphereCastNonAlloc(previous,.025f,delta.normalized,throwHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count>=throwHits.Length){throwRoot.SetActive(false);return;}
            for(int k=0;k<count;k++){var hit=throwHits[k];if(hit.collider!=null&&!hit.collider.transform.IsChildOf(rig.PlayerRoot)&&!throwFilter.Excluded(hit.collider)&&hit.distance<distance){point=hit.point;distance=hit.distance;stop=true;}}
            Segment(previous,point);if(stop)break;previous=point;
        }
        throwVisual!.Set(mesh);throwVisual.Show(true);
        void Segment(Vector3 a,Vector3 b)
        {
            var side=Vector3.Cross(b-a,rig.HeadPosition-a).normalized*.002f;
            var x=ContactWorld.V(a-side);var y=ContactWorld.V(a+side);var z=ContactWorld.V(b+side);var w=ContactWorld.V(b-side);
            mesh.Quad(x,y,z,w,color);mesh.Quad(x,y,z,w,color,true);
        }
    }
    // 0.1.138: after a throw of ours the same thrower launches nothing more
    // for a while (a second grenade came from the same throw, next to the
    // first, and exploded too), unless a new throw of ours starts.
    private IntPtr spentThrow;private float spentUntil;private int spentBlocked;
    private Vector3 throwSpin;
    internal const float SpentWindow=3f;
    private bool SpentDuplicate(ThrowingComponent t,string what)
    {
        if(spentThrow==IntPtr.Zero||t==null||t.Pointer!=spentThrow)return false;
        float now=Time.realtimeSinceStartup;
        if(now>=spentUntil){spentThrow=IntPtr.Zero;return false;}
        if(launchUntil>now)return false;   // a new throw of ours
        if(spentBlocked++<6)Bootstrap.Write("THROW second launch blocked: "+what+" "+(now-(spentUntil-SpentWindow)).ToString("F2")+" s after the throw ("+profile+")");
        return true;
    }
    // 0.1.138: what the thrower does after a throw of ours (the log shows
    // where a second projectile would come from).
    private int throwTraceLeft;private float throwTraceFrom;
    private void TraceThrow(ThrowingComponent t,string what)
    {
        if(throwTraceLeft<=0||t==null)return;
        bool mine=propThrow!=null&&propThrow.Pointer==t.Pointer||t.Pointer==spentThrow;if(!mine)return;
        throwTraceLeft--;Bootstrap.Write("THROW trace "+what+" +"+(Time.realtimeSinceStartup-throwTraceFrom).ToString("F2")+" s ("+profile+", launch "+(launchUntil>Time.realtimeSinceStartup?"pending":"done")+")");
    }
    private static void ThrowEnding(ThrowingComponent __instance){Current?.TraceThrow(__instance,"End");}
    private static void ThrowEndThrow(ThrowingComponent __instance){Current?.TraceThrow(__instance,"EndThrow");}
    private static bool ThrowHandleProjectile(ThrowingComponent __instance)
    {
        var c=Current;if(c==null)return true;
        c.TraceThrow(__instance,"HandleProjectile");
        return !c.SpentDuplicate(__instance,"HandleProjectile");
    }
    // Projectiles handed to the thrower during a throw of ours: the one we
    // launch flies, any other one of the same throw is removed.
    private IntPtr ourProjectile;
    private readonly System.Collections.Generic.List<Projectile> throwProjectiles=new();
    private static void ThrowStarted(ThrowingComponent __instance,Projectile projectile)
    {
        var c=Current;if(c==null||projectile==null||__instance==null)return;
        c.TraceThrow(__instance,"StartThrow "+projectile.name);
        if(projectile.Pointer==c.ourProjectile)return;
        if(c.SpentDuplicate(__instance,"StartThrow "+projectile.name)){RemoveProjectile(projectile);return;}
        if(c.propThrow!=null&&c.propThrow.Pointer==__instance.Pointer&&Time.realtimeSinceStartup<=c.launchUntil&&c.throwProjectiles.Count<8)c.throwProjectiles.Add(projectile);
    }
    private static bool PropFireTask(ThrowingComponent __instance)
    {
        var c=Current;
        c?.TraceThrow(__instance,"ExecuteFireTask");
        // 0.1.140: the game's own second launch also stopped its in-hand fuse sound.
        if(c!=null&&c.SpentDuplicate(__instance,"ExecuteFireTask")){SilenceGameFuse(__instance);return false;}
        if(c==null||c.propThrow==null||c.propThrow.Pointer!=__instance.Pointer)return true;
        if(c.throwSpent!=null||c.throwTaskIssued)return false;
        if(Time.realtimeSinceStartup>c.launchUntil)return false;
        c.throwTaskIssued=true;return true;
    }
    private static bool PropLaunched(ThrowingComponent __instance,Projectile instancedProjectile,ref Vector3 hitPoint,ref bool missTarget)
    {
        var c=Current;
        if(c!=null&&c.KnifeLaunch(__instance,instancedProjectile,ref hitPoint,ref missTarget))return true;
        if(c!=null&&instancedProjectile!=null)c.TraceThrow(__instance,"LaunchProjectile "+instancedProjectile.name);
        if(c!=null&&instancedProjectile!=null&&instancedProjectile.Pointer==c.ourProjectile&&__instance.Pointer==c.spentThrow&&Time.realtimeSinceStartup<c.spentUntil)return false;   // ours again: it keeps our flight
        if(c!=null&&instancedProjectile!=null&&c.SpentDuplicate(__instance,"LaunchProjectile "+instancedProjectile.name)){RemoveProjectile(instancedProjectile);return false;}
        if(c==null||c.propThrow==null||c.propThrow.Pointer!=__instance.Pointer||Time.realtimeSinceStartup>c.launchUntil||instancedProjectile==null)return true;
        var body=instancedProjectile.projectileRigidBody;if(body==null){Bootstrap.Warn("PROP THROW missing native rigidbody; native launch retained");return true;}
        // SetUpProjectile has already assigned native damage, owner and collision
        // listeners. Replace launch only: native AddForce must not also run after
        // the preview velocity is applied, or the trajectories differ at step one.
        instancedProjectile.transform.SetPositionAndRotation(c.throwOrigin,c.throwRotation);
        instancedProjectile.origin=c.throwOrigin;instancedProjectile.ResetTrail();instancedProjectile.SetGravity(true);
        body.isKinematic=false;body.useGravity=true;body.drag=0;body.position=c.throwOrigin;
        body.velocity=c.throwLaunch;body.angularVelocity=c.throwSpin;
        // 0.1.138: from now on this thrower's further launches are duplicates.
        c.spentThrow=__instance.Pointer;c.spentUntil=Time.realtimeSinceStartup+SpentWindow;c.spentBlocked=0;c.ourProjectile=instancedProjectile.Pointer;
        foreach(var other in c.throwProjectiles)
            if(other!=null&&other.Pointer!=instancedProjectile.Pointer){Bootstrap.Write("THROW second projectile of the same throw removed: "+other.name);RemoveProjectile(other);}
        c.throwProjectiles.Clear();
        if(c.profile=="grenade")c.QuietFuse(instancedProjectile);
        // A thrown prop leaves the inventory; a grenade stack is counted by
        // the game (checked in CheckGrenadeAmmo).
        if(c.profile=="prop")c.throwSpent=__instance.baseEquipable;
        c.launchUntil=0;c.visual?.Hide();
        Bootstrap.Write("PROP THROW launched "+instancedProjectile.name+" origin="+c.throwOrigin+" velocity="+c.throwLaunch+" spin="+c.throwSpin.magnitude.ToString("F1")+" gravity="+Physics.gravity+" drag=0");
        c.throwSpin=Vector3.zero;
        return false;
    }
    // A second projectile of one throw: gone before it flies (silent).
    private static void RemoveProjectile(Projectile p)
    {
        try{p.TryCast<DetonatingProjectile>()?.StopExplosionSound();}catch(Exception){}
        try{var body=p.projectileRigidBody;if(body!=null&&!body.isKinematic){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}}catch(Exception){}
        try{p.gameObject.SetActive(false);}catch(Exception ex){Bootstrap.Warn("THROW second projectile: "+ex.Message);}
    }
    private void ClearThrow()
    {throwSpin=Vector3.zero;throwProjectiles.Clear();swings[0].Reset();swings[1].Reset();throwGrips[0].Reset();throwGrips[1].Reset();throwHeldKey[0]=throwHeldKey[1]=-1;knifeAiming=false;knifePressFrame=-1;knifeLaunchUntil=0;knifeByGrip=false;knifeReleasedAt=-10;knifeHiddenUntil=0;throwAiming=false;propThrow=null;launchUntil=0;throwSpent=null;throwTaskIssued=false;if(throwRoot!=null)throwRoot.SetActive(false);}
    // 0.1.158: the middle of the thing drawn in the hand, in the world
    // (GripCarry puts a thing let go of down there; mirrored in the left hand).
    // 0.1.161: where the drawn long thing's head points (a broom's bristles), in the world.
    internal bool TryHeldPropHead(Equipable item,out Vector3 world)
    {
        world=Vector3.zero;
        if(item==null||weapon==null||visual==null||weapon.Pointer!=item.Pointer||visual.Profile!="prop"||visual.LongGripOrigin==null)return false;
        var a=visual.LongGripAxis;if(visual.GeometryMirrored)a=new Vector3(-a.x,a.y,a.z);
        world=visual.FittedToWorld.MultiplyVector(a);
        if(!(world.sqrMagnitude>1e-8f)||!float.IsFinite(world.sqrMagnitude))return false;
        world.Normalize();return true;
    }
    internal bool TryHeldPropCenter(Equipable item,out Vector3 world)
    {
        world=Vector3.zero;
        if(item==null||weapon==null||visual==null||weapon.Pointer!=item.Pointer||visual.Profile!="prop")return false;
        var c=visual.PropCenter;if(visual.GeometryMirrored)c=new Vector3(-c.x,c.y,c.z);
        world=visual.FittedToWorld.MultiplyPoint3x4(c);
        return float.IsFinite(world.x)&&float.IsFinite(world.y)&&float.IsFinite(world.z);
    }
}
