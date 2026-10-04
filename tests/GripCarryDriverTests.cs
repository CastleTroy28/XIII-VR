using System;using System.Linq;using System.Collections.Generic;using XiiiXR;using UnityEngine;using PlayMagic.AI;
class GripCarryDriverTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var npc=new NPC();var target=new NPCHittable{Npc=npc};npc.Target=target;
  var ray=new RaycastSystem{raycastHittable=new NPCHittable(),rayHit=new RaycastHit{point=new Vector3(9,9,9),distance=9},hasRayHit=false};
  var prior=ray.raycastHittable;
  var hostage=new PlayerHostageController{playerRaycast=ray};var bodies=new PickupBodiesController{playerRaycast=ray};
  var h=new PlayerEquipableHandler{playerHostageController=hostage,pickupBodiesController=bodies};
  var rig=new CameraRig();rig.PlayerRoot.Components=new Component[]{h};rig.LeftControls=new(true,HandControls.Grip,0,0);
  // A saturated query used to skip EVERY candidate. A held grip without Down
  // must still acquire a conscious NPC, independent of busy right-hand input.
  Physics.Items=Enumerable.Range(0,95).Select(_=>new Collider()).Append(new Collider{Npc=npc,Target=target,Trigger=true}).ToArray();
  using var carry=new GripCarry(rig);
  void Tick(bool allowed=true,bool left=true){Time.frameCount++;Time.realtimeSinceStartup+=.2f;carry.Tick(rig.PlayerRoot,new(true,0,0,0),allowed,ray,left);}
  Tick(false,true);Check(hostage.Taken==1&&carry.HidesLeft,"held left grip failed with saturated triggers/busy right hand");
  Check(Physics.LastQuery==QueryTriggerInteraction.Collide,"NPC trigger targets excluded");
  Check(ray.raycastHittable==prior&&!ray.hasRayHit&&ray.rayHit.distance==9,"left validation corrupted right ray");
  Tick();Check(rig.Haptics>0&&hostage.Taken==1,"carry vibration/acquisition latch broken");
  rig.LeftControls=new(true,0,0,HandControls.Grip);Tick();Tick();
  Check(hostage.Released==1&&npc.Knocked==1&&!carry.HidesLeft,"grip release fails to release/knock out/hide cleanup");
  npc.actorStatus=ActorStatus.Conscious;hostage.Permit=false;rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);
  Tick();Check(hostage.Taken==1,"native hostage permission bypassed");hostage.Permit=true;
  InteractionDriver.Current=new InteractionDriver{Occupied=true};Tick();Check(hostage.Taken==1,"left hand grabbed two objects");InteractionDriver.Current=null;
  Tick();Check(hostage.Taken==2,"reacquisition after prior release fails");
  rig.Tracked=false;Tick();Tick();Check(hostage.Released==2&&npc.Knocked==1,"tracking loss knocked out hostage");rig.Tracked=true;
  npc.actorStatus=ActorStatus.Unconscious;rig.LeftControls=new(true,HandControls.Grip,0,0);Tick();Check(bodies.Taken==0,"body picked up without new grip press");
  rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);Tick();Check(bodies.Taken==1&&carry.HidesLeft,"body grip press stopped working");
  rig.LeftControls=new(true,0,0,HandControls.Grip);Tick();Tick();
  // 0.1.98: trigger on an empty hand takes a conscious hostage; that trigger holds him.
  npc.actorStatus=ActorStatus.Conscious;rig.LeftControls=new(true,0,0,0);Tick();Tick();
  int taken=hostage.Taken,released=hostage.Released,knocked=npc.Knocked;var touched=new Collider{Npc=npc,Target=target};
  Check(carry.TryTriggerHostage(true,npc,touched,new Vector3(1,1,1))&&hostage.Taken==taken+1&&carry.HidesHand(true)&&!carry.HidesLeft,"right trigger hostage not taken");
  Check(!carry.TryTriggerHostage(false,npc,touched,new Vector3(1,1,1))&&hostage.Taken==taken+1,"second hostage while holding one");
  rig.RightControls=new(true,HandControls.Trigger,0,0);Tick();Tick();Check(hostage.Released==released,"held right trigger released the hostage");
  rig.RightControls=new(true,0,0,HandControls.Trigger);Tick();Tick();Check(hostage.Released==released+1&&npc.Knocked==knocked+1,"right trigger release fails to release/knock out");
  hostage.Permit=false;npc.actorStatus=ActorStatus.Conscious;Check(!carry.TryTriggerHostage(false,npc,touched,new Vector3(1,1,1)),"trigger hostage bypassed native permission");hostage.Permit=true;
  // 0.1.218: an ally is never taken, even where the game's check would let him be.
  NpcAllies.Allies.Add(npc);Check(!carry.TryTriggerHostage(false,npc,touched,new Vector3(1,1,1))&&hostage.Taken==taken+1&&NpcAllies.Told>0,"an ally taken hostage");NpcAllies.Allies.Clear();
  // 0.1.112: grabbing from behind (the NPC faces away from the head) works even when the
  // native check (body/camera based) refuses; never when the NPC is not in a hostage state.
  rig.LeftControls=new(true,0,0,0);rig.RightControls=new(true,0,0,0);Tick();Tick();
  hostage.Permit=false;npc.actorStatus=ActorStatus.Conscious;npc.transform.forward=new Vector3(-.707f,0,-.707f);
  hostage.State=false;rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);int before=hostage.Taken;Tick();
  Check(hostage.Taken==before,"hostage taken from an NPC not in the game's hostage state");
  rig.LeftControls=new(true,0,0,0);Tick();hostage.State=true;rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);Tick();
  Check(hostage.Taken==before+1,"left grip from behind did not take the hostage");
  rig.LeftControls=new(true,0,0,HandControls.Grip);Tick();Tick();npc.actorStatus=ActorStatus.Conscious;npc.transform.forward=new Vector3(0,0,1);hostage.Permit=true;
  Check(HostageMath.Behind(new System.Numerics.Vector3(0,0,1),new System.Numerics.Vector3(0,0,1),new System.Numerics.Vector3(0,0,0))
   &&!HostageMath.Behind(new System.Numerics.Vector3(0,0,-1),new System.Numerics.Vector3(0,0,1),new System.Numerics.Vector3(0,0,0))
   &&!HostageMath.Behind(new System.Numerics.Vector3(0,0,1),new System.Numerics.Vector3(0,0,3),new System.Numerics.Vector3(0,0,0)),"behind test");
  hostage.Throw=true;bool threw=false;try{CarryTargetValidation.Hostage(hostage,target,new Vector3(1,2,3));}catch(InvalidOperationException){threw=true;}
  Check(threw&&ray.raycastHittable==prior&&!ray.hasRayHit&&ray.rayHit.distance==9,"exception leaks left validation ray");
  // 0.1.167: a hostage who dies in the hands is let go of at once (the grip still held), not knocked out, not taken again.
  hostage.Throw=false;hostage.Permit=true;hostage.State=true;rig.LeftControls=new(true,0,0,0);rig.RightControls=new(true,0,0,0);Tick();Tick();
  npc.actorStatus=ActorStatus.Conscious;rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);int took=hostage.Taken,freed=hostage.Released,kos=npc.Knocked;Tick();
  Check(hostage.Taken==took+1,"hostage for the death check not taken");
  rig.LeftControls=new(true,HandControls.Grip,0,0);Tick();Check(hostage.Released==freed,"a living hostage let go while the grip is held");
  npc.actorStatus=ActorStatus.Dead;Tick();Tick();
  Check(hostage.Released==freed+1&&npc.Knocked==kos&&!carry.HidesLeft,"a dead hostage stays in the hands (or was knocked out)");
  Tick();Tick();Check(hostage.Taken==took+1,"the dead hostage taken again by the grip still held");
  // 0.1.168: an enemy the player disarmed is taken from any side, whatever its AI state; its fight ends first. An armed one is not.
  rig.LeftControls=new(true,0,0,0);Tick();Tick();
  int taken2=hostage.Taken,told=0;GripCarry.Disarmed=n=>n==npc;GripCarry.TakingHostage=n=>{if(n==npc)told++;};
  npc.actorStatus=ActorStatus.Conscious;hostage.Permit=false;hostage.State=false;npc.transform.forward=new Vector3(0,0,-1);
  rig.LeftControls=new(true,HandControls.Grip,0,0);Tick();Tick();
  Check(hostage.Taken==taken2&&told==0,"a disarmed enemy taken by a fist held closed (a punch), not a fresh grip press");
  rig.LeftControls=new(true,0,0,0);Tick();
  rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);Tick();
  Check(hostage.Taken==taken2+1&&told==1,"a disarmed enemy not taken hostage from the front");
  rig.LeftControls=new(true,0,0,HandControls.Grip);Tick();Tick();
  GripCarry.Disarmed=_=>false;npc.actorStatus=ActorStatus.Conscious;rig.LeftControls=new(true,0,0,0);Tick();Tick();
  rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);Tick();
  Check(hostage.Taken==taken2+1&&told==1,"an armed enemy taken hostage from the front");
  rig.LeftControls=new(true,0,0,HandControls.Grip);Tick();Tick();GripCarry.Disarmed=null;GripCarry.TakingHostage=null;hostage.Permit=true;hostage.State=true;
  Console.WriteLine("PASS: a disarmed enemy is taken hostage from any side. PASS: a hostage dying in the hands is let go of at once. PASS: production carry driver; full buffer including NPC trigger; held left grip independent of right interaction; scoped native target validation/restoration on failure; native rejection; occupied hand; haptics; release knockout; tracking loss; body press/release; from-behind hostage by grip with the head behind the NPC (game hostage state required); right/left trigger hostage hold/release/permission. Native game/physics simulated.");
 }
}
namespace HarmonyLib
{
 class Harmony{internal Harmony(string s){}internal void Patch(object m,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null,HarmonyMethod? finalizer=null){}internal void UnpatchSelf(){}}
 class HarmonyMethod{internal HarmonyMethod(Type t,string s){}}
 static class AccessTools{internal static object DeclaredMethod(Type t,string s)=>new();}
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
 class Il2CppReferenceArray<T>{readonly T[] a;internal Il2CppReferenceArray(int size){a=new T[size];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}
}
namespace UnityEngine
{
 class ObjectBase{internal string name="test";internal IntPtr Pointer=IntPtr.Zero;internal int GetInstanceID()=>GetHashCode();internal T? TryCast<T>() where T:class=>this as T;}
 class Component:ObjectBase
 {
  internal Transform transform=new();internal NPC? Npc=null;internal NPCHittable? Target=null;
  internal Component? GetComponentInParent(Type t)=>t==typeof(NPC)?Npc:t==typeof(NPCHittable)?Target:null;
  internal Component? GetComponentInChildren(Type t,bool include)=>t==typeof(NPCHittable)?Target:null;
 }
 class Transform:ObjectBase{internal Transform? parent=null;internal Vector3 position=new();internal Vector3 forward=new(0,0,1);internal Component[] Components=Array.Empty<Component>();internal Component[] GetComponentsInChildren(Type t,bool include)=>Components.Where(t.IsInstanceOfType).ToArray();}
 struct Vector3{internal float x,y,z;internal Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}internal float magnitude=>MathF.Sqrt(x*x+y*y+z*z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
 struct Quaternion{}
 struct RaycastHit{internal Vector3 point;internal float distance;}
 class Collider:Component{internal bool Trigger=false;}
 enum QueryTriggerInteraction{Ignore,Collide}
 static class Physics
 {
  internal static Collider[] Items=Array.Empty<Collider>();internal static QueryTriggerInteraction LastQuery;
  internal static int OverlapSphereNonAlloc(Vector3 p,float r,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> a,int mask,QueryTriggerInteraction q)
  {LastQuery=q;var found=Items.Where(c=>!c.Trigger||q==QueryTriggerInteraction.Collide).Take(a.Length).ToArray();for(int i=0;i<found.Length;i++)a[i]=found[i];return found.Length;}
 }
 static class Application{internal static bool isFocused=true;}
 static class Time{internal static int frameCount;internal static float realtimeSinceStartup,timeScale=1;}
}
interface IRaycastHittable{}
class NPCHittable:Component,IRaycastHittable{}
class OwnerInfo{internal bool IsPlayer=>true;internal bool IsInvalid=>false;}
class PlayerEquipableInventory:ObjectBase
{
 internal enum ActiveEquipmentSlot{Enviromental,Fist}internal PlayMagic.Weapons.Equipable? currentEquipable=null;internal bool isInTransit=false;internal void TryDropCurrentEquipableAndAmmo(){currentEquipable=null;}
}
class PlayerEquipableHandler:Component{internal PlayerEquipableInventory inventory=new();internal PlayerHostageController? playerHostageController;internal PickupBodiesController? pickupBodiesController;internal OwnerInfo GetOwner()=>new();}
class RaycastSystem:Component{internal IRaycastHittable? raycastHittable;internal RaycastHit rayHit;internal bool hasRayHit;internal IRaycastHittable? RaycastHittable=>raycastHittable;}
class PlayerCarryAIController:Component
{internal Transform? bodySpawnBone=null;
 internal RaycastSystem? playerRaycast;internal Transform? playerCameraPosition=new();internal NPC? bodyNPC;internal bool isInTransition=false;internal bool hasBody=>bodyNPC!=null;internal int Released;
 internal void ReleaseBody(){Released++;bodyNPC=null;}
}
class PlayerHostageController:PlayerCarryAIController
{
 internal bool Permit=true,Throw=false,State=true;internal int Taken;
 internal bool IsAIInCorrectState(NPC n)=>State;internal bool IsFacingAwayFromPlayer(NPC n)=>false;internal bool IsInValidHostageTakedownDistance(NPC n)=>false;
 internal bool CanTakeHostage(IRaycastHittable t){if(Throw)throw new InvalidOperationException();return Permit&&playerRaycast?.raycastHittable==t&&playerRaycast.hasRayHit&&((NPCHittable)t).Npc?.actorStatus==ActorStatus.Conscious;}
 internal void TakeHostage(NPC n,Transform? original,bool reset){Taken++;bodyNPC=n;}
}
class PickupBodiesController:PlayerCarryAIController
{
 internal int Taken;internal bool CanTakeBody(IRaycastHittable t,Vector3 p)=>playerRaycast?.raycastHittable==t&&((NPCHittable)t).Npc?.actorStatus==ActorStatus.Unconscious;
 internal void TakeBody(NPC n,Transform? original){Taken++;bodyNPC=n;}
}
namespace PlayMagic.AI{enum ActorStatus{Conscious,Unconscious,Dead}class NPC:Component{internal ActorStatus actorStatus=ActorStatus.Conscious;internal int Knocked;internal void Knockout(OwnerInfo owner,bool instant){Knocked++;actorStatus=ActorStatus.Unconscious;}}}
namespace PlayMagic.Weapons{class Equipable:ObjectBase{internal string identifier="test";internal PlayerEquipableInventory.ActiveEquipmentSlot slot=PlayerEquipableInventory.ActiveEquipmentSlot.Fist;}}
namespace XiiiXR
{
 struct PoseValue{internal Vector3 Position;internal PoseValue(Vector3 p){Position=p;}}
 class CameraRig
 {
  internal Transform PlayerRoot=new();internal Vector3 HeadPosition=new(1,1.6f,1);internal HandControls LeftControls,RightControls;internal bool Tracked=true,HeadTrackingValid=true,Scripted=false;internal int Haptics;
  internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){l=r=new PoseValue(new Vector3(1,1,1));valid=Tracked;return true;}
  internal static Vector3 UnityPosition(PoseValue p)=>p.Position;internal void PunchHaptics(bool right){if(!right)Haptics++;}
 }
 class GameUiControls{internal static GameUiControls? Current=null;internal bool BlocksGameplay=>false;}
 class WeaponHands{internal static WeaponHands? Current=null;internal bool HandFree(bool right)=>true;internal static bool LeftHanded=>false;internal bool PrimaryLeft=>false;internal static WeaponGripMode GripMode=WeaponGripMode.Always;internal bool AwaitsThrow(PlayMagic.Weapons.Equipable e)=>false;}
 enum WeaponGripMode{Hold=0,Toggle=1,Always=2}
 class InteractionDriver{internal static InteractionDriver? Current;internal bool Occupied;internal bool HandOccupied(bool right)=>!right&&Occupied;}
 class ContactRig{internal static ContactRig? Current=null;internal bool ResolveHand(bool r,bool attached,ref Vector3 p,ref Quaternion q)=>true;}
 static class ControllerAim{internal static Quaternion Rotation(PoseValue p)=>new();}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
namespace XiiiXR { static class NpcAllies { internal static readonly System.Collections.Generic.HashSet<PlayMagic.AI.NPC> Allies=new();internal static int Told;internal static bool Ally(PlayMagic.AI.NPC? n)=>n!=null&&Allies.Contains(n);internal static bool GameAllowsHostage(PlayMagic.AI.NPC n)=>!Ally(n);internal static void Refused(PlayMagic.AI.NPC? n,string what){Told++;} } }
namespace XiiiXR { static class WindowFocus { internal static bool Playable=>UnityEngine.Application.isFocused; } }
