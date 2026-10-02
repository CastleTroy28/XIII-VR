using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
// Replace only local character velocity while a hand holds a nearby ladder.
// Native UpdateCharacterPosition still owns collision and movement.
internal sealed partial class HandClimbing:IDisposable
{
 private static HandClimbing? current;
 private readonly CameraRig rig;private readonly Harmony patches=new("xiii.vr.xrbootstrap.climbing");
 private readonly Il2CppReferenceArray<Collider> nearby=new(32);
 private readonly Transform?[] ladder=new Transform?[2];private readonly Vector3[] anchor=new Vector3[2];
 private PhysicalDoors? doors;private LooseProps? props;private bool allowed;
 private CustomCharacterController? character;private Transform? player;private Vector3 velocity;private int frame=-1;
 internal bool Active=>ladder[0]!=null||ladder[1]!=null||overTop;
 internal bool Holding(bool right)=>ladder[right?1:0]!=null;
 // 0.1.151: a hand holding a ceiling fan's blade (CeilingFans), not a ladder.
 private readonly bool[] onFan=new bool[2];private readonly CeilingFans fans=new();
 internal HandClimbing(CameraRig rig)
 {
  this.rig=rig;
  patches.Patch(AccessTools.DeclaredMethod(typeof(CustomCharacterController),"UpdateCharacterPosition"),prefix:new HarmonyMethod(typeof(HandClimbing),nameof(Move)));
  current=this;
 }
 private static void Move(CustomCharacterController __instance,ref Vector3 velocity)
 {
  var c=current;if(c==null)return;
  try {
  if(c.frame!=Time.frameCount&&c.doors!=null&&c.props!=null)c.Tick(c.allowed&&WindowFocus.Playable&&rigSafe(c),c.doors,c.props);
  if(c.character==null||c.character.Pointer!=__instance.Pointer||!c.Active||c.frame!=Time.frameCount)return;
  velocity=c.velocity;
  }catch(Exception ex){c.Cancel();Bootstrap.Warn("CLIMB cancelled: "+ex.Message);}
 }
 private static bool rigSafe(HandClimbing c)=>c.rig.HeadTrackingValid&&!c.rig.Scripted&&GameUiControls.Current?.BlocksGameplay!=true&&Time.timeScale>0;
 internal void Tick(bool allowed,PhysicalDoors doors,LooseProps props)
 {
  this.allowed=allowed;this.doors=doors;this.props=props;
  if(frame==Time.frameCount){if(!allowed)Cancel();return;}
  frame=Time.frameCount;
  if(player!=rig.PlayerRoot){Cancel();player=rig.PlayerRoot;character=player==null?null:player.GetComponent(Il2CppType.Of<CustomCharacterController>())?.TryCast<CustomCharacterController>();}
  if(!allowed||character==null||GripCarry.Current?.BodyRoot!=null||!rig.SampleWorldHands(out var left,out var right,out bool validLeft)){Cancel();fans.Tick(player,false,default,false,default,false);return;}
  // 0.1.151: the ceiling fans: an outline where a free hand reaches one, the ride.
  {
   bool FreeFor(int i)=>WeaponHands.Current?.HandFree(i==1)!=false&&!doors.Holding(i==1)&&!props.Holding(i==1)&&ladder[i]==null;
   // 0.1.154: and where each free hand points (the fan lights up when pointed at).
   fans.Tick(player,true,CameraRig.UnityPosition(left),GloveVisual.Rotation(left,false)*Vector3.forward,validLeft&&FreeFor(0),CameraRig.UnityPosition(right),GloveVisual.Rotation(right,true)*Vector3.forward,FreeFor(1));
  }
  // 0.1.171: going over the top of the ladder (HandClimbing.Top).
  if(overTop){TickOverTop();return;}
  // 0.1.92: left stick forward hands the ladder back to the game (its own
  // climb continues and steps off at the top); the hands grab again only
  // after the stick is released.
  var stick=rig.LeftStick;
  if(stick.Valid&&stick.Value.Y>.5f&&!onFan[0]&&!onFan[1]){if(Active)Bootstrap.Write("CLIMB released to native ladder movement (left stick forward)");Cancel();return;}
  Vector3 error=Vector3.zero;int count=0;bool pull=false;
  // 0.1.170: under water the
  // arms swim (strokes); a hand with its grip held does not take hold of a
  // ladder or a climb volume there (it would hold the swimmer in place).
  // At the surface a ladder is taken as before (to climb out of the water).
  bool underWater=LocomotionDriver.Current?.Submerged==true;
  for(int i=0;i<2;i++)
  {
   var input=i==0?rig.LeftControls:rig.RightControls;var p=CameraRig.UnityPosition(i==0?left:right);
   if(!input.Valid||(i==0&&!validLeft)||(input.Held&HandControls.Grip)==0||WeaponHands.Current?.HandFree(i==1)==false||doors.Holding(i==1)||props.Holding(i==1)){Drop(i);afterTop[i]=false;continue;}
   if(underWater&&!onFan[i]){if(ladder[i]!=null){Drop(i);Bootstrap.Write("CLIMB "+(i==0?"left":"right")+" hand let go of the ladder under water (the arms swim there)");}continue;}
   // 0.1.151: a grip press at a ceiling fan's blade (outlined in white): the hand hangs on it.
   if(ladder[i]==null&&(input.Down&HandControls.Grip)!=0&&fans.TryGrab(i,p)&&fans.Pivot!=null)
   {ladder[i]=fans.Pivot;onFan[i]=true;rig.PunchHaptics(i==1);}
   // 0.1.171: after going over the top, a hand takes hold again only after its grip was let go.
   if(ladder[i]==null&&!afterTop[i])
   {
    int n=Physics.OverlapSphereNonAlloc(p,.13f,nearby,~0,QueryTriggerInteraction.Collide);
    if(n<nearby.Length)for(int j=0;j<n;j++)
    {
     var c=nearby[j];if(c==null||c.transform.IsChildOf(player))continue;
     Transform? found=null;
     for(var t=c.transform;t!=null;t=t.parent)
      if(t.name.IndexOf("ladder",StringComparison.OrdinalIgnoreCase)>=0){found=t;break;}
     var volume=c.GetComponentInParent(Il2CppType.Of<ClimbVolume>())?.TryCast<ClimbVolume>();
     if(found==null&&volume!=null)found=volume.transform;
     if(found==null)continue;
     // Native climb volumes identify usable ladders even when rungs have no individual colliders.
     if(c.isTrigger){if(volume==null)continue;}
     else if(!ColliderSurface.TryClosest(c,p,out var point)||(point-p).sqrMagnitude>.0169f)continue;
     ladder[i]=found;anchor[i]=found.InverseTransformPoint(p);rig.PunchHaptics(i==1);Measure(i,found,volume,c,p);
     // 0.1.170: a hand taking hold is written (it was silent).
     if(Time.realtimeSinceStartup>=nextGrabReport){nextGrabReport=Time.realtimeSinceStartup+1;Bootstrap.Write("CLIMB "+(i==0?"left":"right")+" hand took hold of "+found.name+(volume!=null?" (the game's climb volume)":"")+(LocomotionDriver.Current?.Swimming==true?" at the water's surface":""));}
     break;
    }
   }
   if(ladder[i]!=null)
   {
    Vector3 target;
    if(onFan[i]){if(!fans.Anchor(i,out target)){Drop(i);continue;}}
    else target=ladder[i]!.TransformPoint(anchor[i]);
    var delta=target-p;
    // 0.1.154: a blade taken from afar: carried to it first (up to 3 s).
    if(onFan[i]&&fans.Pulling(i))
    {
     if(delta.sqrMagnitude<FanMath.Arrive*FanMath.Arrive)fans.Arrived(i);
     else if(fans.PullSeconds(i)>FanMath.PullGiveUp){Bootstrap.Write("FAN the blade was not reached in "+FanMath.PullGiveUp.ToString("F0")+" s ("+delta.magnitude.ToString("F2")+" m left): let go");Drop(i);continue;}
     else pull=true;
    }
    else if(delta.sqrMagnitude>(onFan[i]?.64f:.36f)){if(onFan[i])Bootstrap.Write("FAN the hand was pulled off the blade ("+delta.magnitude.ToString("F2")+" m)");Drop(i);continue;}
    error+=delta;count++;
   }
  }
  bool riding=onFan[0]||onFan[1];
  var v=count==0?System.Numerics.Vector3.Zero:pull?FanMath.Pull(ContactWorld.V(error/count),Time.unscaledDeltaTime):ClimbHandMath.Velocity(ContactWorld.V(error/count),Time.unscaledDeltaTime,riding?FanRideSpeed:2);
  velocity=ContactWorld.U(v);
  if(!riding&&!pull&&count>0)TryOverTop(v.Y);
 }
 internal const float FanRideSpeed=4f;
 private float nextGrabReport;
 private void Drop(int i){ladder[i]=null;if(onFan[i]){onFan[i]=false;fans.Release(i);}}
 internal void Cancel(){Drop(0);Drop(1);velocity=Vector3.zero;overTop=false;}
 public void Dispose(){Cancel();fans.Dispose();patches.UnpatchSelf();if(current==this)current=null;}
}
