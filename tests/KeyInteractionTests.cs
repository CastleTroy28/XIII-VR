using System;using XiiiXR;using UnityEngine;
class KeyInteractionTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main()
 {
  var inv=new PlayerEquipableInventory();var who=new IInteractionActor(inv);var rig=new CameraRig();var ray=new RaycastSystem{InteractionActor=who};var action=new RaycastAction();
  var hit=new RaycastHit{point=new Vector3(0,0,.1f)};var k=new KeyUnlockGesture(rig);
  inv.Owned=false;Check(!k.Intercept(action,who,ray,hit)&&!k.Active&&action.Resolves==0,"missing key starts native animation or unlocks");inv.Owned=true;
  action.Blocked=true;Check(!k.Intercept(action,who,ray,hit)&&!k.Active,"blocked door draws key");action.Blocked=false;
  // Reproduce car failure: owned inventory object has lost its renderer.
  inv.Item.MeshAvailable=false;
  var wrongId=new PlayMagic.Weapons.Equipable{identifier="other"};
  var wrongSlot=new PlayMagic.Weapons.Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.Card};
  inv.itemConfig=new(){itemPrefabs=new(){wrongId,wrongSlot}};
  Check(!k.Intercept(action,who,ray,hit)&&!k.Active&&action.Resolves==0,"missing model falls back to native animation or wrong key/card prefab");
  var prefab=new PlayMagic.Weapons.Equipable();inv.itemConfig.itemPrefabs.Add(prefab);
  Check(!k.Intercept(action,who,ray,hit)&&k.Active&&action.Resolves==0&&inv.Removes==0,"drawing unlocks or consumes item");
  Check(HeldItemVisual.Last==prefab&&inv.currentEquipable!=prefab,"matching prefab recovery equips gameplay prefab");
  Check(NativeItemCue.Last==PlayMagic.Weapons.AudioComponent.AudioTrigger.Equip,"draw plays unlock cue");
  k.Tick(true);Time.realtimeSinceStartup=1;rig.Q=new Quaternion(0,0,.48f,.877f);k.Tick(true);Check(action.Resolves==0,"counterclockwise unlocks");
  rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=2;k.Tick(true);
  Check(action.Resolves==1&&action.Condition&&!k.Active&&action.OpenEvents==0&&action.Notifications==1,"gesture fails unlock-only transition");
  // 0.1.84: the player's own key-turn recording plays when the hand completes the turn.
  Check(inv.Removes==0&&ChairImpactClip.Plays==1&&NativeItemCue.Last!=PlayMagic.Weapons.AudioComponent.AudioTrigger.KeyItem,"persistent key consumed or key-turn recording missing");
  Check(KeyUnlockGesture.Completing==null&&HeldItemVisual.Disposed==1,"completion leaks scope/preview");
  action=new RaycastAction();rig.Q=Quaternion.identity;Check(!k.Intercept(action,who,ray,hit),"native animation not suppressed");k.Tick(false);Check(!k.Active&&action.Resolves==0,"focus loss unlocks");
  k.Intercept(action,who,ray,hit);rig.RightControls=new HandControls{Down=HandControls.B};k.Tick(true);Check(!k.Active&&action.Resolves==0,"cancel unlocks");rig.RightControls=new();
  k.Intercept(action,who,ray,hit);inv.currentEquipable=new();k.Tick(true);Check(!k.Active,"switching weapon retains key");
  k.Intercept(action,who,ray,hit);k.Tick(true);inv.Owned=false;rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=3;k.Tick(true);Check(!k.Active&&action.Resolves==0,"lost owned key still unlocks");inv.Owned=true;
  action=new RaycastAction{conditional=RaycastAction.InteractionConditionals.Keycard};rig.Q=Quaternion.identity;rig.P=new();
  k.Intercept(action,who,ray,hit);k.Tick(true);rig.P=new Vector3(0,-.12f,0);Time.realtimeSinceStartup=4;k.Tick(true);
  Check(action.Condition&&!k.Active&&action.OpenEvents==1&&!action.LinkedDoorLocked,"card reader resolved but linked door remains locked");
  Check(action.ReaderIndicatorGreen&&action.StoryEvents==1,"card reader indicator/story events lost");
  Check(!action.resolveConditionalWhenResolved,"temporary native card dispatch mode leaked");
  k.Tick(true);Check(action.OpenEvents==1&&action.StoryEvents==1,"completed swipe repeats receiver events");
  // 0.1.203: touching the reader's collider for CardHold opens it.
  {
   var box=new Collider{bounds=new Bounds{min=new Vector3(-.02f,-.02f,.08f),max=new Vector3(.02f,.02f,.12f)}};
   var reader=new RaycastAction{conditional=RaycastAction.InteractionConditionals.Keycard};reader.transform.Children=new Obj[]{box};
   rig.P=new Vector3(0,0,-.5f);rig.Q=Quaternion.identity;Time.realtimeSinceStartup=30;
   Check(!k.Intercept(reader,who,ray,hit)&&k.Active,"card not drawn for a reader");
   k.Tick(true);rig.P=new Vector3(0,0,-.06f);
   for(int i=1;i<=20;i++){Time.realtimeSinceStartup=30+i*.05f;k.Tick(true);}
   Check(reader.Resolves==0&&k.Active,"card opens 4 cm before the reader");
   rig.P=new Vector3(0,0,-.01f);Time.realtimeSinceStartup=32;k.Tick(true);Check(reader.Resolves==0,"card opens at the first touch");
   Time.realtimeSinceStartup=32.1f;k.Tick(true);Check(reader.Resolves==0,"card opens before the hold");
   Time.realtimeSinceStartup=32.2f;k.Tick(true);
   Check(reader.Resolves==1&&reader.Condition&&!k.Active&&reader.OpenEvents==1&&reader.StoryEvents==1&&reader.ReaderIndicatorGreen,"card held to the reader does not open it (or loses its events)");
   Check(!reader.resolveConditionalWhenResolved,"temporary native card dispatch mode leaked (touch)");
   // The drawn card's own points count, wherever the controller is; a disabled collider does not.
   var tall=new Collider{bounds=new Bounds{min=new Vector3(-.02f,-.02f,.08f),max=new Vector3(.02f,.02f,.30f)}};
   var reader2=new RaycastAction{conditional=RaycastAction.InteractionConditionals.Keycard};reader2.transform.Children=new Obj[]{tall};
   rig.P=new Vector3(0,0,-.5f);Time.realtimeSinceStartup=40;k.Intercept(reader2,who,ray,hit);
   HeldItemVisual.Probe=new[]{new Vector3(0,0,.6f),new Vector3(0,.01f,.25f)};tall.enabled=false;
   k.Tick(true);Time.realtimeSinceStartup=40.2f;k.Tick(true);Check(reader2.Resolves==0,"a disabled collider still reads the card");
   tall.enabled=true;Time.realtimeSinceStartup=41;k.Tick(true);Time.realtimeSinceStartup=41.2f;k.Tick(true);
   Check(reader2.Resolves==1&&!k.Active,"the drawn card's point touching the reader does not open it");
   HeldItemVisual.Probe=null;Time.realtimeSinceStartup=4;
   Console.WriteLine("PASS: a card held to the reader (its collider, or the drawn card's points) for "+UnlockGestureMath.CardHold+" s opens it with its native reader events; 4 cm away it does not; disabled colliders are ignored.");
  }
  action=new RaycastAction();rig.P=new();rig.Q=Quaternion.identity;inv.Item.CurrentEquipableParameters.removeAfterUse=true;
  k.Intercept(action,who,ray,hit);k.Tick(true);rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=5;k.Tick(true);Check(inv.Removes==1,"single-use key not removed after success");
  inv.Item.MeshAvailable=true;k.Intercept(new RaycastAction(),who,ray,hit);
  Check(HeldItemVisual.Last==inv.Item,"usable owned model needlessly replaced by prefab");k.Cancel();
  inv.Item.MeshAvailable=false;inv.Item.identifier="";
  Check(!k.Intercept(new RaycastAction(),who,ray,hit)&&!k.Active,"empty item identifier permits unrelated prefab");
  // 0.1.103: a lockpick lock is opened with the same 45-degree turn.
  inv.Item.MeshAvailable=true;inv.Item.identifier="eqp_lockpick";inv.Item.CurrentEquipableParameters.removeAfterUse=false;int removes=inv.Removes;
  var pickLock=new RaycastAction{conditional=RaycastAction.InteractionConditionals.Lockpick};rig.P=new();rig.Q=Quaternion.identity;
  Check(!k.Intercept(pickLock,who,ray,hit)&&k.Active&&k.Lockpick&&k.GripProfile=="screwdriver"&&pickLock.Resolves==0,"lockpick lock does not draw the lockpick");
  Check(LockpickTimerMath.Time(float.NaN)==LockpickTimerMath.DefaultTime&&LockpickTimerMath.Time(0)==LockpickTimerMath.DefaultTime&&LockpickTimerMath.Time(4)==4&&LockpickTimerMath.Time(100)==LockpickTimerMath.MaxTime
   &&LockpickTimerMath.Remaining(1,2,2.5f)==.5f&&LockpickTimerMath.Remaining(1,2,9)==0&&LockpickTimerMath.PulledOut(.5f)&&!LockpickTimerMath.PulledOut(.2f)&&LockpickTimerMath.PulledOut(float.NaN),"lockpick time math");
  // 0.1.220: the turn starts the game's lockpicking: its time counted down on its HUD, its sound and noise; pulled out it stops.
  var noise=new PlayMagic.Weapons.NoiseCreationComponent();inv.Item.Lockpick=new PlayMagic.Weapons.LockpickComponent{lockpickTime=.5f,noiseComponent=noise};int started=NativeItemCue.Started;
  k.Tick(true);rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=6;k.Tick(true);
  Check(pickLock.Resolves==0&&k.Picking&&GameUIManager.Shown&&Math.Abs(GameUIManager.LastTime-.5f)<1e-4f&&NativeItemCue.Started==started+1,"the turned pick does not start the game's lockpicking (its HUD, its time, its sound)");
  Time.realtimeSinceStartup=6.25f;k.Tick(true);Check(pickLock.Resolves==0&&Math.Abs(GameUIManager.LastTime-.25f)<1e-3f&&noise.Noises>0,"lockpicking not counted down (or no noise)");
  rig.P=new Vector3(0,0,-1);Time.realtimeSinceStartup=6.3f;k.Tick(true);Check(!k.Picking&&!GameUIManager.Shown&&k.Active&&pickLock.Resolves==0&&NativeItemCue.Stopped>0,"the pick pulled out of the lock keeps picking");
  rig.P=new();rig.Q=Quaternion.identity;Time.realtimeSinceStartup=6.35f;k.Tick(true);rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=6.5f;k.Tick(true);
  Check(k.Picking&&pickLock.Resolves==0&&Math.Abs(GameUIManager.LastTime-.5f)<1e-4f,"turned again, the lockpicking does not start over");
  Time.realtimeSinceStartup=6.9f;k.Tick(true);Check(pickLock.Resolves==0,"the lock opens before the game's time");
  Time.realtimeSinceStartup=7.05f;k.Tick(true);
  Check(pickLock.Resolves==1&&pickLock.Condition&&!k.Active&&!k.Lockpick&&inv.Removes==removes&&!GameUIManager.Shown&&!k.Picking,"lockpick time up does not unlock (or consumed the lockpick, or left the HUD on)");
  // The game's instant lockpicking: the turn opens at once.
  inv.Item.Lockpick=new PlayMagic.Weapons.LockpickComponent{lockpickTime=5,playerState=new PlayerState{InstantLockPick=true}};
  var quick=new RaycastAction{conditional=RaycastAction.InteractionConditionals.Lockpick};rig.Q=Quaternion.identity;k.Intercept(quick,who,ray,hit);
  Time.realtimeSinceStartup=7.1f;k.Tick(true);rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=7.25f;k.Tick(true);
  Check(quick.Resolves==1&&!k.Picking&&!k.Active,"instant lockpicking still counts down");
  inv.Item.Lockpick=null;
  // 0.1.150: a left-hander turns the key with the left hand; the right one does nothing, the left Y puts it away.
  WeaponHands.LeftHanded=true;
  {
   var lock2=new RaycastAction();rig.P=new Vector3(5,5,5);rig.Q=Quaternion.identity;rig.LP=new();rig.LQ=Quaternion.identity;
   Check(!k.Intercept(lock2,who,ray,hit)&&k.Active,"left-handed: key not drawn");
   k.Tick(true);rig.Q=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=7;k.Tick(true);
   Check(lock2.Resolves==0&&k.Active,"left-handed: the right hand turned the key");
   rig.LQ=new Quaternion(0,0,-.48f,.877f);Time.realtimeSinceStartup=8;k.Tick(true);
   Check(lock2.Resolves==1&&!k.Active,"left-handed: the left hand's turn does not unlock");
   var lock3=new RaycastAction();rig.LQ=Quaternion.identity;k.Intercept(lock3,who,ray,hit);
   rig.RightControls=new HandControls{Down=HandControls.B};k.Tick(true);Check(k.Active,"left-handed: the right B put the key away");rig.RightControls=new();
   rig.LeftControls=new HandControls{Down=HandControls.B};k.Tick(true);Check(!k.Active&&lock3.Resolves==0,"left-handed: the left Y does not put the key away");rig.LeftControls=new();
   rig.P=new();rig.Q=Quaternion.identity;
  }
  WeaponHands.LeftHanded=false;
  CarKey();
  Console.WriteLine("PASS: 0.1.220 a lockpick turned in the lock starts the game's lockpicking (its time on its HUD countdown, its sound, its noise); pulled out it stops, turned again it starts over; the time up opens the lock; the game's instant lockpicking opens at once.");
  Console.WriteLine("PASS: a left-hander turns the key with the left hand (the right hand and its B ignored, the left Y puts it away).");
  Console.WriteLine("PASS: lockpick draw + 45-degree turn unlock; production gesture and mesh source recovery: both car inventory/prefab meshless; detached key02, matching pickup and shared native metal-key fallback; no draw without ownership, no card substitution, original key consumed only after clockwise unlock. Existing draw-only, cancellation and card receiver-chain cases preserved. Unity/native adapters simulated.");
 }
 static void CarKey()
 {
  var inv=new PlayerEquipableInventory();inv.Item.identifier="eqp_key_02";inv.Item.MeshAvailable=false;
  var missingPrefab=new PlayMagic.Weapons.Equipable{identifier="eqp_key_02",MeshAvailable=false};
  var shared=new PlayMagic.Weapons.Equipable{identifier="eqp_key_01"};
  inv.itemConfig=new(){itemPrefabs=new(){missingPrefab,shared}};
  var model=new MeshRenderer{name="key_02",Filter=new(){sharedMesh=new(){vertexCount=60}}};
  var wrong=new MeshRenderer{name="key_01",Filter=new(){sharedMesh=new(){vertexCount=60}}};
  var root=new Transform{Children=new Obj[]{wrong,model}};
  inv.customCharacterController=new(){CurrentFpsRigReference=new(){transform=root}};
  var who=new IInteractionActor(inv);var rig=new CameraRig();var ray=new RaycastSystem{InteractionActor=who};var hit=new RaycastHit{point=new(0,0,.1f)};
  using var k=new KeyUnlockGesture(rig);var action=new RaycastAction();
  inv.Owned=false;k.Intercept(action,who,ray,hit);Check(!k.Active&&root.Scans==0,"renderer fallback grants unowned car key");inv.Owned=true;
  action.Blocked=true;k.Intercept(action,who,ray,hit);Check(!k.Active&&root.Scans==0,"blocked target scans key models");action.Blocked=false;
  k.Intercept(action,who,ray,hit);
  Check(k.Active&&HeldItemVisual.Last==inv.Item&&HeldItemVisual.Rendered==model,"meshless car key fails to recover detached key02 or uses shared key before exact model");
  Check(action.Resolves==0&&inv.Removes==0&&inv.currentEquipable!=inv.Item,"detached visual starts native use/equip");
  Check(NativeItemCue.Item==inv.Item,"detached preview changes sound identity");
  k.Tick(true);Check(root.Scans==1,"key model rescanned on every gesture tick");k.Cancel();
  inv.customCharacterController.CurrentFpsRigReference=null;inv.customCharacterController.tinyArmsMesh=root;
  k.Intercept(new(),who,ray,hit);Check(k.Active&&HeldItemVisual.Rendered==model,"tiny arms detached key unavailable");k.Cancel();
  inv.customCharacterController=null;
  var pickup=new GameObject();pickup.transform.Children=new Obj[]{model};inv.itemConfig.pickupPrefabs.Add("eqp_key_02",pickup);
  k.Intercept(new(),who,ray,hit);Check(k.Active&&HeldItemVisual.Rendered==model&&HeldItemVisual.Last==inv.Item,"matching pickup model not recovered");k.Cancel();
  inv.itemConfig.pickupPrefabs.Clear();
  inv.Item.CurrentEquipableParameters.removeAfterUse=true;action=new();
  k.Intercept(action,who,ray,hit);
  Check(k.Active&&HeldItemVisual.Last==shared&&action.Resolves==0&&inv.Removes==0,"meshless inventory AND prefab cannot draw shared metal key");
  Check(NativeItemCue.Item==inv.Item&&inv.currentEquipable!=shared,"shared visual substitutes inventory identity");
  rig.Q=Quaternion.identity;Time.realtimeSinceStartup=10;k.Tick(true);
  rig.Q=new(0,0,-.48f,.877f);Time.realtimeSinceStartup=11;k.Tick(true);
  Check(action.Condition&&action.OpenEvents==0&&!k.Active&&inv.Removes==1&&inv.Removed==inv.Item&&NativeItemCue.Item==inv.Item,"shared visual unlock fails or consumes/sounds wrong key");
  // 0.1.75: the bank car is a locked target without a Door. Unlocking it must
  // run its native receivers (mission end + cutscene) exactly once, like a card.
  var car=new RaycastAction{DoorParent=null};inv.Item.CurrentEquipableParameters.removeAfterUse=false;
  k.Intercept(car,who,ray,hit);Check(k.Active,"car key not drawn");
  rig.Q=Quaternion.identity;Time.realtimeSinceStartup=20;k.Tick(true);
  rig.Q=new(0,0,-.48f,.877f);Time.realtimeSinceStartup=21;k.Tick(true);
  Check(car.Condition&&!k.Active&&car.OpenEvents==1&&car.StoryEvents==1,"car unlock does not fire native mission events");
  Check(!car.resolveConditionalWhenResolved&&KeyUnlockGesture.Completing==null,"car dispatch mode or scope leaked");
  k.Tick(true);Check(car.StoryEvents==1,"car events repeated");
  inv.itemConfig.itemPrefabs.Remove(shared);inv.allEquipment.Add("eqp_key_01",shared);
  k.Intercept(new(),who,ray,hit);Check(k.Active&&HeldItemVisual.Last==shared,"native inventory model dictionary not used");k.Cancel();
  inv.Item.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Card;action=new(){conditional=RaycastAction.InteractionConditionals.Keycard};
  k.Intercept(action,who,ray,hit);Check(!k.Active&&action.Resolves==0,"metal model substituted for missing card");
  inv.Item.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Key;inv.Item.identifier="";
  k.Intercept(new(),who,ray,hit);Check(!k.Active,"empty identifier accepts common model");
  inv.Item.identifier="eqp_key_02";shared.MeshAvailable=false;
  k.Intercept(new(),who,ray,hit);Check(!k.Active,"unusable sources start invisible gesture");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Obj{static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal T? TryCast<T>()where T:class=>this as T;}
 class GameObject:Obj{internal bool activeInHierarchy=true;internal Transform transform=new();internal PlayerEquipableInventory? Inv;internal Obj? GetComponentInChildren(Type t,bool x)=>Inv;}
 class Transform:Obj{internal Obj[] Children=Array.Empty<Obj>();internal int Scans;internal string name="target";internal Transform? parent=>null;internal Vector3 position=>new();internal Vector3 InverseTransformPoint(Vector3 p)=>p;internal Vector3 TransformPoint(Vector3 p)=>p;internal Obj[] GetComponentsInChildren(Type t,bool x){Scans++;return Children;}}
 class Mesh:Obj{internal string name="";internal int vertexCount;}
 class MeshFilter:Obj{internal Mesh? sharedMesh;}
 class Renderer:Obj{internal string name="";internal MeshFilter? Filter;internal Obj? GetComponent(Type type)=>Filter;}
 class MeshRenderer:Renderer{}
 class SkinnedMeshRenderer:Renderer{internal Mesh? sharedMesh=>null;}
 struct Vector3{internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}internal static Vector3 zero=>new();internal float magnitude=>MathF.Sqrt(x*x+y*y+z*z);internal static Vector3 Max(Vector3 a,Vector3 b)=>new(MathF.Max(a.x,b.x),MathF.Max(a.y,b.y),MathF.Max(a.z,b.z));public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);}
 struct Quaternion{internal float x,y,z,w;internal Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}internal static Quaternion identity=>new(0,0,0,1);public static Vector3 operator*(Quaternion q,Vector3 v){var p=System.Numerics.Vector3.Transform(new(v.x,v.y,v.z),new System.Numerics.Quaternion(q.x,q.y,q.z,q.w));return new(p.X,p.Y,p.Z);}}
 class Collider:Obj{internal bool enabled=true;internal GameObject gameObject=new();internal Bounds bounds;}
 struct Bounds{internal Vector3 min,max;}
 struct RaycastHit{internal Vector3 point;internal Collider? collider=>null;}
 static class Time{internal static float realtimeSinceStartup;}
}
class IInteractionActor{readonly PlayerEquipableInventory inv;internal IInteractionActor(PlayerEquipableInventory v){inv=v;}internal int GetActorID()=>0;internal GameObject GetGameObject()=>new(){Inv=inv};}
class RaycastSystem{internal IInteractionActor? InteractionActor;}
class RaycastAction:Obj
{
 internal enum InteractionConditionals{Key,Keycard,Lockpick}internal InteractionConditionals conditional=InteractionConditionals.Key;internal int keyTypeToCheck=>1;internal int keyCardTypeToCheck=>2;
 internal Transform transform=new();internal string name=>"door";internal bool Condition,Blocked;internal int Resolves,OpenEvents,Notifications,StoryEvents;internal bool LinkedDoorLocked=true,ReaderIndicatorGreen;internal bool isActiveAndEnabled=>true;
 internal bool resolveConditionalWhenResolved,conditionalResolved;
 internal bool GetConditionState()=>Condition;internal bool IsRaycastPingValid(IInteractionActor a,RaycastHit h)=>true;internal bool IsActorValid(IInteractionActor a)=>true;internal bool IsInteractionBlocked(IInteractionActor a)=>Blocked;
 internal Obj? DoorParent=new PlayMagic.AI.Door();internal Obj? GetComponentInParent(Type t)=>t==typeof(PlayMagic.AI.Door)?DoorParent:null;
 // Native ResolveConditional RVA 0x51fae0: first unresolved call records the
 // conditional without dispatch unless the coroutine enabled resolve mode.
 internal void ResolveConditional(IInteractionActor a){Resolves++;Condition=true;if(!resolveConditionalWhenResolved&&!conditionalResolved){conditionalResolved=true;return;}if(KeyUnlockGesture.Completing!=this){OpenEvents++;LinkedDoorLocked=false;ReaderIndicatorGreen=true;StoryEvents++;}}
 internal void NotifyAchievementsAboutUnlocking(InteractionConditionals c){Notifications++;}
}
class PlayerEquipableInventory:Obj
{
 internal enum ActiveEquipmentSlot{Card,Key,Lockpick}internal bool IsInventoryBlocked=>false;internal bool isInTransit=>false;internal bool Owned=true;internal int Removes;internal PlayMagic.Weapons.Equipable Item=new(),currentEquipable=new();internal PlayerItemInventoryConfig? itemConfig;
 internal PlayMagic.CustomCharacterController? customCharacterController;internal System.Collections.Generic.Dictionary<string,PlayMagic.Weapons.Equipable> allEquipment=new();internal PlayMagic.Weapons.Equipable? Removed;
 internal bool HasItemToResolveConditional(RaycastAction.InteractionConditionals c,int type)=>Owned;
 internal PlayMagic.Weapons.Equipable GetEquipableFromSlot(ActiveEquipmentSlot s)=>Item;internal void Remove(PlayMagic.Weapons.Equipable e){Removes++;Removed=e;}
}
class PlayerItemInventoryConfig{internal System.Collections.Generic.List<PlayMagic.Weapons.Equipable> itemPrefabs=new();internal System.Collections.Generic.Dictionary<string,GameObject> pickupPrefabs=new();}
namespace PlayMagic{class CustomCharacterController{internal FpsMeshReference? CurrentFpsRigReference;internal Transform? tinyArmsMesh;}class FpsMeshReference{internal Transform transform=new();}}
namespace PlayMagic.AI{class Door:Obj{internal Transform transform=new();}}
namespace PlayMagic.Weapons{class Equipable:Obj{internal SkinnedMeshRenderer[]? skinnedMeshRenderers=>null;internal Obj[] GetComponentsInChildren(Type t,bool x)=>Array.Empty<Obj>();internal Parameters CurrentEquipableParameters=new();internal string name="key",identifier="key01";internal PlayerEquipableInventory.ActiveEquipmentSlot slot=PlayerEquipableInventory.ActiveEquipmentSlot.Key;internal bool MeshAvailable=true;internal LockpickComponent? Lockpick;internal T? GetOptionalEquipableComponent<T>() where T:class=>Lockpick as T;}class Parameters{internal bool removeAfterUse;}class AudioComponent{internal enum AudioTrigger{Equip,KeyItem,Lockpick}}class LockpickComponent{internal float lockpickTime=3;internal global::PlayerState? playerState;internal NoiseCreationComponent? noiseComponent;}class NoiseCreationComponent{internal int Noises;internal void GenerateNoise(int priority,bool nonThreatening,float amount){Noises++;}}}
class PlayerState{internal bool InstantLockPick;}
static class GameUIManager{internal static bool Shown;internal static float LastTime;internal static void ToggleLockpickingHud(bool show,int player){Shown=show;}internal static void UpdateLockpickingHud(float time,int player){LastTime=time;}}
namespace FMOD{enum RESULT{OK}}
namespace FMOD.Studio{enum PLAYBACK_STATE{PLAYING,STOPPED}enum STOP_MODE{ALLOWFADEOUT,IMMEDIATE}struct EventInstance{internal RESULT getPlaybackState(out PLAYBACK_STATE s){s=PLAYBACK_STATE.PLAYING;return RESULT.OK;}internal RESULT stop(STOP_MODE m){XiiiXR.NativeItemCue.Stopped++;return RESULT.OK;}internal RESULT release()=>RESULT.OK;}}
namespace XiiiXR
{
 class ChairImpactClip{internal static int Plays;internal ChairImpactClip(Func<byte[]> w,string n,float g,float d){}internal bool Play(Vector3 p){Plays++;return true;}internal void Dispose(){}}
 static class KeyTurnSound{internal static byte[] Wav()=>new byte[0];}
 struct HandControls{internal const ulong B=1;internal ulong Down;}
 class CameraRig{internal Vector3 P,LP;internal Quaternion Q=Quaternion.identity,LQ=Quaternion.identity;internal HandControls RightControls,LeftControls;internal bool SampleWorldHands(out PoseValue a,out PoseValue b,out bool c){a=new(){P=LP,Q=LQ};b=new(){P=P,Q=Q};c=true;return true;}internal static Vector3 UnityPosition(PoseValue h)=>h.P;internal void DisarmTrigger(){}internal void PunchHaptics(bool right){}}
 struct PoseValue{internal Vector3 P;internal Quaternion Q;}static class ControllerAim{internal static Quaternion Rotation(PoseValue h)=>h.Q;}
 static class ColliderSurface{internal static bool TryClosest(Collider c,Vector3 p,out Vector3 hit){hit=p;return false;}}
 static class ContactWorld{internal static System.Numerics.Vector3 V(Vector3 p)=>new(p.x,p.y,p.z);}
 class NativeHandVisual{}
 class WeaponHands{internal static bool LeftHanded;}
 class HeldItemVisual:IDisposable{internal const int ProbePoints=15;internal static Vector3[]? Probe;internal int WorldProbe(Vector3[] into){if(Probe==null)return 0;Array.Copy(Probe,into,Probe.Length);return Probe.Length;}internal static int Disposed;internal static Renderer? Rendered;internal static PlayMagic.Weapons.Equipable? Last;internal static HeldItemVisual Create(PlayMagic.Weapons.Equipable e){if(!e.MeshAvailable)throw new Exception("missing model");Last=e;Rendered=null;return new();}internal static HeldItemVisual CreateFromRenderers(PlayMagic.Weapons.Equipable e,System.Collections.Generic.List<Renderer> sources){Last=e;Rendered=sources[0];return new();}internal void Pose(Transform? h){}internal void PreparePinch(NativeHandVisual h){}internal bool FistHand(NativeHandVisual h,ref Vector3 p,ref Quaternion q)=>false;public void Dispose(){Disposed++;}}
 static class NativeItemCue{internal static PlayMagic.Weapons.Equipable? Item;internal static PlayMagic.Weapons.AudioComponent.AudioTrigger Last;internal static int Started,Stopped;internal static void Play(PlayMagic.Weapons.Equipable e,PlayMagic.Weapons.AudioComponent.AudioTrigger t,Vector3 p){Last=t;Item=e;}
  internal static bool Start(PlayMagic.Weapons.Equipable e,PlayMagic.Weapons.AudioComponent.AudioTrigger t,Vector3 p,out FMOD.Studio.EventInstance i){i=default;Started++;Item=e;return true;}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}static class UiLanguage{internal static bool Russian=>true;}
}
