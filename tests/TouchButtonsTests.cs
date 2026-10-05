using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;
class TouchButtonsTests
{
 static void Check(bool ok,string error){if(!ok)throw new Exception(error);}
 static (TouchButtons touch,CameraRig rig,RaycastAction action) Setup(bool sibling=false)
 {
  var root=new Transform{name="elevator_01_button"};var target=root.Child("raycast_target");var action=new RaycastAction{transform=target};target.Action=action;
  var shape=sibling?root.Child("trigger_elevator_button"):target;
  Physics.Scene=new[]{new Collider{transform=shape}};
  var rig=new CameraRig();WeaponHands.Current=new();GripCarry.Current=null;
  var touch=new TouchButtons();action.Driver=touch;return(touch,rig,action);
 }
 // root / action object / collider object (or the action's own object), as the game's prefabs are.
 static (TouchButtons touch,CameraRig rig,RaycastAction action,Collider collider) Build(string root,string target,string? shape,params (string,int,bool)[] events)
 {
  var r=new Transform{name=root};var t=r.Child(target);var action=new RaycastAction{transform=t};t.Action=action;action.Events.AddRange(events);
  var collider=new Collider{transform=shape==null?t:t.Child(shape)};Physics.Scene=new[]{collider};
  var rig=new CameraRig();WeaponHands.Current=new();GripCarry.Current=null;
  var touch=new TouchButtons();action.Driver=touch;return(touch,rig,action,collider);
 }
 static bool Press(TouchButtons touch,CameraRig rig,IInteractionActor who)
 {
  int before=Bootstrap.Lines.Count;rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);
  Time.realtimeSinceStartup+=1;return Bootstrap.Lines.Count>before;
 }
 static string Last=>Bootstrap.Lines.Count==0?"":Bootstrap.Lines[^1];
 static void Main()
 {
  Controls();
  Strikes();
  var (touch,rig,a)=Setup(true);var who=new IInteractionActor();var w=WeaponHands.Current!;w.Armed=true;
  w.Palm=new Vector3(0,0,-.4f);w.Tip=new Vector3(.04f,0,-.09f);touch.Tick(rig,true,who);
  w.Tip=new Vector3(.04f,0,-.035f);touch.Tick(rig,true,who);
  Check(a.Pings==1&&rig.RightHaptics==1,"off-centre stopped weapon cannot press sibling elevator action");
  Check(a.SawInjected,"native press lacks scoped interaction input");
  w.Tip=new Vector3(.04f,0,-.034f);touch.Tick(rig,true,who);Check(a.Pings==1,"held contact repeats press");
  w.Tip=new Vector3(.04f,0,-.3f);touch.Tick(rig,true,who);Check(a.Pings==1,"withdrawal presses button");
  w.Tip=new Vector3(.04f,0,-.035f);touch.Tick(rig,true,who);Check(a.Pings==2,"release and re-contact not rearmed");
  (touch,rig,a)=Setup();w=WeaponHands.Current!;w.Armed=true;w.Tip=new Vector3(0,0,-.7f);
  w.Palm=new Vector3(0,0,-.065f);touch.Tick(rig,true,who);w.Palm=new Vector3(0,0,-.059f);touch.Tick(rig,true,who);
  Check(a.Pings==1,"armed palm disabled when muzzle points away");
  (touch,rig,a)=Setup();w=WeaponHands.Current!;w.Armed=true;w.Palm=new Vector3(0,0,-.4f);w.Tip=new Vector3(0,0,-.0565f);
  touch.Tick(rig,true,who);w.Tip=new Vector3(0,0,-.056f);touch.Tick(rig,true,who);
  Check(a.Pings==1,"slow contact smaller than 2mm/frame discarded");
  foreach(string rejection in new[]{"blocked","actor","ping","locked","inactive","namedDoor"})
  {
   (touch,rig,a)=Setup();
   if(rejection=="blocked")a.Blocked=true;if(rejection=="actor")a.ActorValid=false;if(rejection=="ping")a.PingValid=false;
   if(rejection=="locked")a.conditional=RaycastAction.InteractionConditionals.Key;
   if(rejection=="inactive")a.isActiveAndEnabled=false;if(rejection=="namedDoor")a.transform.parent!.name="door";
   rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);
   Check(a.Pings==0,"touch bypasses native restriction: "+rejection);
  }
  (touch,rig,a)=Setup();rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,false,who);
  touch.Tick(rig,true,who);Check(a.Pings==0,"focus/tracking recovery causes synthetic press");
  (touch,rig,a)=Setup();a.Throws=true;rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);
  try{touch.Tick(rig,true,who);throw new Exception("native throw missing");}catch(InvalidOperationException){}
  Check(!touch.Injecting,"exception leaks injected interaction button");
  (touch,rig,a)=Setup(true);var second=a.transform.parent!.Child("second_target");second.Action=new RaycastAction{transform=second};
  rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);
  Check(a.Pings==0&&second.Action.Pings==0,"ambiguous panel presses arbitrary action");
  (touch,rig,a)=Setup();rig.LeftValid=true;rig.L=rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);
  rig.L=rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);
  Check(a.Pings==1&&rig.LeftHaptics==1,"two simultaneous hands double-activate button");
  (touch,rig,a)=Setup();rig.LeftValid=true;GripCarry.Current=new(){HidesLeft=true};rig.L=new Vector3(0,.008f,-.15f);rig.R=new Vector3(2,0,0);
  touch.Tick(rig,true,who);rig.L=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);Check(a.Pings==0,"hidden occupied carry hand presses button");
  Console.WriteLine("PASS: production button driver with geometric box contacts: stopped weapon, off-axis volume, armed palm, slow touch, generic/sibling elevator actions, debounce/re-entry, two hands, carry, native permission/lock gates, focus and exception cleanup. Unity/native actions simulated.");
 }
 const int Input=2;
 static void Controls()
 {
  var who=new IInteractionActor();
  // The power box that cuts the alarms: no button name anywhere; it animates its switch and turns the alarms off.
  var (touch,rig,a,c)=Build("power_box_06_b","raycast_target","trigger",("CustomAnimationToolHandle",Input,false),("DisableAlarmsEvent",Input,false),("SoundSender",Input,false));
  Check(Press(touch,rig,who)&&a.Pings==1&&Last.StartsWith("TOUCH BUTTON raycast_target via=trigger")&&Last.Contains("by what it runs: CustomAnimationToolHandle, DisableAlarmsEvent, SoundSender"),"the power box switch that cuts the alarms is not pressed by a touch: "+Last);
  // The lift of cp_elevator_02: its button runs a script.
  (touch,rig,a,c)=Build("elevator_02","raycast_target","Collider",("UnityEventHandler",Input,false));
  Check(Press(touch,rig,who)&&a.Pings==1,"a lift button without a button name is not pressed by a touch");
  // Named buttons still press, with nothing readable on them.
  (touch,rig,a,c)=Build("elevator_01_button","raycast_target",null);
  Check(Press(touch,rig,who)&&a.Pings==1&&Last.Contains("(named: none)"),"a named lift button no longer presses");
  // What a touch never does, though it is an interaction.
  foreach(var (what,events,size,hittable,door,note) in new (string,(string,int,bool)[],float,int,bool,string?)[]
  {
   ("nothing on use",new (string,int,bool)[0],.04f,10,false,"it runs nothing on use"),
   ("only on entering, not on use",new[]{("UnityEventHandler",4,false)},.04f,10,false,"it runs nothing on use"),
   ("only a sound",new[]{("SoundSender",Input,false),("EmitAISoundEvent",Input,false)},.04f,10,false,"it runs nothing on use"),
   ("a pick-up",new[]{("PickUpItem",Input,false),("UnityEventHandler",Input,false)},.04f,10,false,null),
   ("a ladder",new[]{("LadderGrab",Input,false)},.04f,10,false,null),
   ("a zipline",new[]{("ZiplineEvent",Input,false)},.04f,10,false,null),
   ("a cabinet's own leaf",new[]{("CustomAnimationToolHandle",Input,true)},.04f,10,false,null),
   ("a door's interaction",new[]{("CustomAnimationToolHandle",Input,false)},.04f,10,true,null),
   ("a pickable thing",new[]{("UnityEventHandler",Input,false)},.04f,12,false,null),
   ("a room-sized trigger",new[]{("PlayableDirectorStart",Input,false)},2.5f,10,false,"a large volume"),
  })
  {
   (touch,rig,a,c)=Build("machine_01","raycast_target","trigger",events);c.Size=size;a.HittableType=hittable;a.DoorOwned=door;
   bool logged=Press(touch,rig,who);
   Check(a.Pings==0,"a touch presses "+what);
   Check(note==null?!logged:logged&&Last.StartsWith("TOUCH BUTTON nothing pressed at trigger < raycast_target < machine_01: "+note),"the log does not say why a touch pressed nothing ("+what+"): "+(logged?Last:"no line"));
  }
  // The alarm box is named a button, but a touch raising the alarm would fail the mission.
  (touch,rig,a,c)=Build("alarm_box_01_button","raycast_target","pc_collider",("CustomAnimationToolHandle",Input,false),("AlarmActivator",Input,false));
  Check(Press(touch,rig,who)&&a.Pings==0&&Last.Contains("it raises the alarm (never by a touch; Grip+A does it) (raycast_target; on use: AlarmActivator, CustomAnimationToolHandle)"),"a touch raises the alarm (or the log does not say so): "+Last);
  // A game refusal is said too, once per collider and 15 s.
  (touch,rig,a,c)=Build("power_box_06_b","raycast_target","trigger",("DisableAlarmsEvent",Input,false));a.Blocked=true;
  Check(Press(touch,rig,who)&&a.Pings==0&&Last.EndsWith("blocked by the game now (raycast_target)"),"a refused touch is not explained: "+Last);
  int lines=Bootstrap.Lines.Count;rig.R=new Vector3(0,.008f,-.15f);touch.Tick(rig,true,who);rig.R=new Vector3(0,.008f,-.08f);touch.Tick(rig,true,who);
  Check(Bootstrap.Lines.Count==lines,"the refusal is logged every frame");
  Console.WriteLine("PASS: controls by what they run: the alarm power box switch and an unnamed lift button are pressed by a touch; nothing on use, entering-only, sound-only, pick-ups, ladders, ziplines, a cabinet's own leaf, a door's interaction, pickables and room-sized triggers are not (the log says why); a named alarm box never raises the alarm by a touch; refusals logged once.");
 }
 // 0.1.244: a blow breaks a thing the game breaks when used (the vent behind
 // the leaves in the sanctuary's entrance: 2.2 m, its use shatters it); a
 // touch does not, and a blow uses nothing else.
 static void Strikes()
 {
  var who=new IInteractionActor();var hit=new RaycastHit();
  var (touch,rig,a,c)=Build("foliage_vent_01","State_Action","cp_foliage_vent_01_mesh",("DestructableObjectHandle",Input,false),("DestructableObjectHandle",Input,false),("ObjectEnableStateSet",Input,false),("SoundSender",Input,false));c.Size=2.23f;
  Check(Press(touch,rig,who)&&a.Pings==0&&Last.Contains("a large volume"),"a touch breaks the vent (or says nothing): "+Last);
  Check(touch.Strike(c,hit,who,out string note)&&a.Pings==1&&a.SawInjected&&note.StartsWith("State_Action via=cp_foliage_vent_01_mesh (on use: DestructableObjectHandle x2, ObjectEnableStateSet, SoundSender)"),"a blow does not break the vent: "+note);
  Check(!touch.Strike(c,hit,who,out note)&&a.Pings==1&&note.Length==0,"the vent used again by the next blow at once");
  Time.realtimeSinceStartup+=2.1f;
  Check(touch.Strike(c,hit,who,out note)&&a.Pings==2,"the vent never used again after its two seconds");
  // The game's own refusals keep it whole and say why.
  (touch,rig,a,c)=Build("foliage_vent_01","State_Action","pc_collider",("DestructableObjectHandle",Input,false));a.Blocked=true;
  Check(!touch.Strike(c,hit,who,out note)&&a.Pings==0&&note.StartsWith("blocked by the game now (State_Action"),"a blocked vent broken (or not said): "+note);
  (touch,rig,a,c)=Build("crate_01","State_Action","pc_collider",("DestructableObjectHandle",Input,false));a.conditional=RaycastAction.InteractionConditionals.Key;
  Check(!touch.Strike(c,hit,who,out note)&&a.Pings==0&&note.StartsWith("needs Key"),"a thing that needs a key broken by a blow: "+note);
  // What a blow never uses.
  foreach(var (what,events,hittable,door) in new (string,(string,int,bool)[],int,bool)[]
  {
   ("a lift button",new[]{("UnityEventHandler",Input,false)},10,false),
   ("an alarm box that breaks",new[]{("DestructableObjectHandle",Input,false),("AlarmActivator",Input,false)},10,false),
   ("a pick-up",new[]{("DestructableObjectHandle",Input,false),("PickUpItem",Input,false)},10,false),
   ("a cabinet's leaf",new[]{("DestructableObjectHandle",Input,false),("CustomAnimationToolHandle",Input,true)},10,false),
   ("broken only on entering",new[]{("DestructableObjectHandle",4,false)},10,false),
   ("a door's interaction",new[]{("DestructableObjectHandle",Input,false)},10,true),
   ("a pickable thing",new[]{("DestructableObjectHandle",Input,false)},12,false),
  })
  {
   (touch,rig,a,c)=Build("machine_01","raycast_target","trigger",events);a.HittableType=hittable;a.DoorOwned=door;
   Check(!touch.Strike(c,hit,who,out note)&&a.Pings==0&&note.Length==0,"a blow uses "+what);
  }
  Check(TouchControlMath.Breaks(new[]{("SoundSender",Input,false),("DestructableObjectHandle",Input|4,false)})&&!TouchControlMath.Breaks(new (string,int,bool)[0]),"what breaks");
  Console.WriteLine("PASS: 0.1.244 a blow breaks what the game breaks when used (the 2.2 m vent in the sanctuary's entrance), a touch does not; once in two seconds; the game's block and conditions kept; never a button, an alarm, a pick-up, a leaf, a door, a pickable or an entering trigger.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays{class Il2CppReferenceArray<T>{readonly T[] data;internal Il2CppReferenceArray(int n){data=new T[n];}internal int Length=>data.Length;internal T this[int i]{get=>data[i];set=>data[i]=value;}}}
namespace UnityEngine
{
 class Obj{static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal T? TryCast<T>()where T:class=>this as T;}
 class Transform:Obj
 {
  internal string name="";internal Transform? parent;internal List<Transform> Children=new();internal RaycastAction? Action;
  internal int childCount=>Children.Count;internal Transform GetChild(int i)=>Children[i];internal Obj? GetComponent(Type t)=>Action;
  internal Transform Child(string n){var c=new Transform{name=n,parent=this};Children.Add(c);return c;}
 }
 struct Vector3
 {
  internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}internal static Vector3 zero=>new();
  internal float sqrMagnitude=>x*x+y*y+z*z;internal float magnitude=>MathF.Sqrt(sqrMagnitude);internal Vector3 normalized=>magnitude>1e-8f?this/magnitude:zero;
  internal static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);public static Vector3 operator/(Vector3 a,float b)=>a*(1/b);
 }
 struct Quaternion{public static Vector3 operator*(Quaternion q,Vector3 v)=>v;}
 struct Ray{internal Vector3 origin,direction;internal Ray(Vector3 p,Vector3 d){origin=p;direction=d;}}
 struct RaycastHit{internal Vector3 point,normal;}
 class Collider:Obj
 {
  internal Transform transform=new();internal string name=>transform.name;internal float Size=.04f;
  internal Obj? GetComponentInParent(Type t){for(var p=transform;p!=null;p=p.parent)if(p.Action!=null)return p.Action;return null;}
  internal Vector3 ClosestPoint(Vector3 p)=>new(Math.Clamp(p.x,-.02f,.02f),Math.Clamp(p.y,-.02f,.02f),Math.Clamp(p.z,0,.02f));
  internal bool Raycast(Ray ray,out RaycastHit hit,float distance)
  {
   hit=default;float enter=0,exit=distance;var normal=Vector3.zero;
   for(int axis=0;axis<3;axis++)
   {
    float p=axis==0?ray.origin.x:axis==1?ray.origin.y:ray.origin.z,d=axis==0?ray.direction.x:axis==1?ray.direction.y:ray.direction.z;
    float min=axis==2?0:-.02f,max=.02f;if(Math.Abs(d)<1e-8f){if(p<min||p>max)return false;continue;}
    float a=(min-p)/d,b=(max-p)/d,sign=d>0?-1:1;if(a>b)(a,b)=(b,a);
    if(a>enter){enter=a;normal=axis==0?new(sign,0,0):axis==1?new(0,sign,0):new(0,0,sign);}exit=Math.Min(exit,b);if(enter>exit)return false;
   }
   if(enter<=0||enter>distance)return false;hit=new(){point=ray.origin+ray.direction*enter,normal=normal};return true;
  }
 }
 enum QueryTriggerInteraction{Collide}
 static class Time{internal static float realtimeSinceStartup;}
 static class Physics
 {
  internal static Collider[] Scene=Array.Empty<Collider>();
  internal static int OverlapSphereNonAlloc(Vector3 p,float radius,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> result,int mask,QueryTriggerInteraction query)
  {int n=0;foreach(var c in Scene)if((c.ClosestPoint(p)-p).sqrMagnitude<=radius*radius&&n<result.Length)result[n++]=c;return n;}
 }
}
class IInteractionActor{}
class RaycastAction:UnityEngine.Obj
{
 internal enum InteractionConditionals{Nothing,Key}internal InteractionConditionals conditional;
 internal UnityEngine.Transform transform=new();internal string name=>transform.name;
 internal bool isActiveAndEnabled=true,Blocked,ActorValid=true,PingValid=true,Throws,SawInjected,DoorOwned;internal int Pings,HittableType=10;internal XiiiXR.TouchButtons? Driver;
 private static long nextPointer=1000;internal readonly IntPtr Pointer=new(++nextPointer);
 internal List<(string type,int trigger,bool leaf)> Events=new();
 internal bool IsInteractionBlocked(IInteractionActor a)=>Blocked;internal bool IsActorValid(IInteractionActor a)=>ActorValid;internal bool IsRaycastPingValid(IInteractionActor a,UnityEngine.RaycastHit h)=>PingValid;
 internal void PingRaycastHittable(IInteractionActor a,UnityEngine.RaycastHit h,out bool valid){SawInjected=Driver?.Injecting==true;if(Throws)throw new InvalidOperationException();Pings++;valid=true;}
}
namespace XiiiXR
{
 class CameraRig{internal Transform PlayerRoot=new();internal Vector3 L=new(2,0,0),R;internal bool LeftValid;internal int RightHaptics,LeftHaptics;internal bool SampleWorldHands(out Vector3 l,out Vector3 r,out bool lv){l=L;r=R;lv=LeftValid;return true;}internal static Vector3 UnityPosition(Vector3 p)=>p;internal void PunchHaptics(bool right){if(right)RightHaptics++;else LeftHaptics++;}}
 class WeaponHands{internal static WeaponHands? Current;internal bool Armed;internal Vector3 Palm,Tip;internal bool TryButtonContacts(bool right,out Vector3 p,out Vector3 tip){p=Palm;tip=Tip;return right&&Armed;}}
 class GripCarry{internal static GripCarry? Current;internal bool HidesLeft;}
 static class GloveVisual{internal static Quaternion Rotation(Vector3 pose,bool right)=>new();}
 static class ColliderSurface{internal static bool TryClosest(Collider c,Vector3 p,out Vector3 q){q=c.ClosestPoint(p);return true;}}
 static class Bootstrap{internal static List<string> Lines=new();internal static void Write(string s){Lines.Add(s);}}
 // The game-side reader, over the stand-in's fields (the real one reads the receivers' events).
 static class TouchControlReader
 {
  internal static TouchControlMath.Verdict Read(RaycastAction a,UnityEngine.Collider c,bool named,out string events)=>Read(a,c,named,out events,out _);
  internal static TouchControlMath.Verdict Read(RaycastAction a,UnityEngine.Collider c,bool named,out string events,out bool breaks)
  {
   events="";breaks=false;
   if(!named&&a.HittableType!=10)return TouchControlMath.Verdict.NotInteraction;
   if(a.DoorOwned)return TouchControlMath.Verdict.DoorAction;
   events=TouchControlMath.Summary(a.Events);breaks=TouchControlMath.Breaks(a.Events);
   return TouchControlMath.Classify(a.Events,named,c.Size);
  }
 }
}
