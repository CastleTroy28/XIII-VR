using System;using XiiiXR;using N=System.Numerics.Vector3;using Q=System.Numerics.Quaternion;
class ContactWorldTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(ContactFilter.NonPhysicalName("wpn_chair_mesh VR")&&ContactFilter.NonPhysicalName("XIII VR Weapon Visual prop")&&!ContactFilter.NonPhysicalName("chair_01"),"owned VR chair enters world physics filter");
  Check(ContactFilter.NonPhysicalName("projectileDetection")&&ContactFilter.NonPhysicalName("SwimVolume_03")&&!ContactFilter.NonPhysicalName("dock_planks_01"),"water surface/swim volume blocks the VR hands");
  // 0.1.205: the invisible walls for walking, either spelling, do not stop the hands or a weapon; real colliders do.
  Check(ContactFilter.NonPhysicalName("collider player")&&ContactFilter.NonPhysicalName("collider player (57)")&&ContactFilter.NonPhysicalName("collider_player (3)")
   &&!ContactFilter.NonPhysicalName("collider_detailed")&&!ContactFilter.NonPhysicalName("cp_keypad_01")&&!ContactFilter.NonPhysicalName("collider players_room"),"an invisible wall for walking stops the hands (or a real collider is ignored)");
  using var world=new ContactWorld();world.WeaponPose=default;world.IncludeWeapon=false;var at=new N(0,.4f,.45f);
  // The production adapter receives Unity's fake initial-overlap normal.
  Check(world.Sweep(at,at-new N(0,0,.2f),.05f,out _)>.999f,"touching face traps withdrawal");
  Check(world.Sweep(at,at+new N(0,.3f,0),.05f,out _)>.999f,"touching face traps lifting");
  Check(world.Sweep(at,at+new N(0,0,.2f),.05f,out _)<.01f,"penetrating motion accepted");
  Check(UnityEngine.Physics.InitialHits>=3,"fixture did not exercise Unity zero-distance hits");
  world.PeerPose=new ContactPose(new N(0,2,0),Q.Identity);world.PeerShape=new[]{new ContactSphere(N.Zero,.05f)};
  Check(world.Sweep(new N(-.3f,2,0),new N(.3f,2,0),.05f,out _) < .4f,"other hand/dual weapon can tunnel through peer");
  Check(world.Overlap(new N(.04f,2,0),.05f,out var peerCorrection)&&peerCorrection.X>0,"overlapping hands do not separate");
  Check(world.Sweep(new N(.10f,2,0),new N(.3f,2,0),.05f,out _)>.99f,"peer collision blocks withdrawal");
  world.PeerShape=ContactSolver.Weapon("pistol");world.PeerPose=new ContactPose(new N(0,2,0),Q.Identity);
  Check(!world.Overlap(new N(.040f,2.04f,.14f),.017f,out _),"pistol barrels still blocked at four centimetres centre spacing");
  Check(world.Overlap(new N(.025f,2.04f,.14f),.017f,out _),"narrow pistol collision no longer prevents penetration");
  world.PeerShape=null;
  var shape=new[]{new ContactSphere(N.Zero,.05f)};var solver=new ContactSolver();
  ContactPose P(N p)=>new(p,Q.Identity);
  var p=solver.Solve(P(at),P(at),shape,world);Check(solver.Safe,"cannot seed at contact");
  for(int i=0;i<80;i++)p=solver.Solve(P(new N(0,.4f,.7f)),P(at),shape,world);
  Check(p.Position.Z<.451f,"held controller tunnels through fence");
  for(int i=1;i<=30;i++)p=solver.Solve(P(new N(0,.4f+i*.03f,.7f)),P(at),shape,world);
  Check(solver.Safe&&p.Position.Y>1.25f&&p.Position.Z>.69f,"cannot lift over finite fence edge");
  p=solver.Solve(P(new N(0,1.3f,.2f)),P(at),shape,world);Check(solver.Safe&&p.Position.Z<.201f,"cannot withdraw above obstacle");
  solver.Reset();p=solver.Solve(P(new N(0,.4f,.49f)),P(new N(0,.4f,.49f)),shape,world);
  Check(solver.Safe&&p.Position.Z<.451f,"real initial penetration not separated");
  // Body has walked around the door/frame; the old hand remains behind it.
  solver.Reset();p=solver.Solve(P(new N(0,.4f,.3f)),P(new N(0,.4f,.3f)),shape,world);
  bool recovered=false;for(int i=0;i<12;i++){p=solver.Solve(P(new N(0,.4f,.9f)),P(new N(0,.4f,.8f)),shape,world);recovered|=solver.Recovered;}
  Check(recovered&&p.Position.Z>.89f,"hand remains behind door after body has a clear route to controller");
  solver.Reset();p=solver.Solve(P(new N(0,.4f,.3f)),P(new N(0,.4f,.3f)),shape,world);
  for(int i=0;i<30;i++)p=solver.Solve(P(new N(0,.4f,.9f)),P(new N(0,.4f,.3f)),shape,world);
  Check(p.Position.Z<.451f&&!solver.Recovered,"recovery teleports through a wall when body path is blocked");
  for(int i=0;i<30;i++)p=solver.Solve(P(new N(0,.4f,.55f)),P(new N(0,.4f,.8f)),shape,world);
  Check(p.Position.Z<.451f&&!solver.Recovered,"recovery accepts destination inside door");
  foreach(string ignored in new[]{"collider_player","collider_player (2)","path_blocker","path_blocker (7)","prj_bullet_player(Clone) 2","prj_shotgun(Clone) 3"})
  {
   var c=new UnityEngine.BoxCollider();c.transform.name=ignored;UnityEngine.Physics.Wall=c;
   Check(world.Sweep(at,at+N.UnitZ,.05f,out _)>.99f,"nonphysical boundary blocks VR hand: "+ignored);
   Check(!world.Overlap(new N(0,.4f,.55f),.05f,out _),"nonphysical barrier affects penetration recovery");
  }
  var child=new UnityEngine.BoxCollider();child.transform.parent=new UnityEngine.Transform{name="path_blocker (18)"};UnityEngine.Physics.Wall=child;
  Check(world.Sweep(at,at+N.UnitZ,.05f,out _)>.99f,"path blocker child collider not filtered");
  foreach(string solid in new[]{"cp_crate_01_a","fence_01","wall","Spine_02SHJnt","path_blocker_decoration"})
  {
   var c=new UnityEngine.BoxCollider();c.transform.name=solid;UnityEngine.Physics.Wall=c;
   Check(world.Sweep(at,at+N.UnitZ,.05f,out _)<.01f,"physical geometry was disabled: "+solid);
  }
  // A local loading-port opening must not turn off the barrel or world wall.
  world.IncludeWeapon=true;world.WeaponPose=new(N.Zero,Q.Identity);
  world.WeaponShape=new[]{new ContactSphere(new N(0,1.5f,.2f),.04f),new ContactSphere(new N(0,1.5f,.6f),.04f)};
  world.AccessPoint=new N(0,1.5f,.2f);world.Access=false;
  Check(world.Sweep(new N(0,1.5f,0),new N(0,1.5f,.3f),.03f,out _)<.8f,"weapon did not block before access");
  world.Access=true;world.AccessAmmunition=true;world.AccessAxis=N.UnitZ;world.Probe(true);
  Check(world.Sweep(new N(0,1.5f,.08f),new N(0,1.5f,.23f),.015f,out _)>.99f,"aligned ammunition cannot enter loading port");
  world.Probe(false);
  Check(world.Sweep(new N(0,1.5f,.08f),new N(0,1.5f,.23f),.03f,out _)<.8f,"loading corridor disabled palm collision");
  world.Probe(true);
  Check(world.Sweep(new N(.068f,1.5f,.08f),new N(.068f,1.5f,.23f),.03f,out _)<.9f,"off-axis magazine tunnels into receiver");
  Check(world.Sweep(new N(0,1.5f,.4f),new N(0,1.5f,.8f),.03f,out _)<.7f,"reload access disabled remote barrel");
  Check(!world.Overlap(world.AccessPoint,.015f,out _)&&world.Overlap(new N(0,1.5f,.6f),.03f,out _),"overlap uses a different access mask");
  Check(world.Sweep(at,at+N.UnitZ,.05f,out _)<.01f,"reload opening disabled real wall collision");
  world.WeaponPose=new(new N(0,1.5f,0),Q.Identity);
  var gunShape=new System.Collections.Generic.List<ContactSphere>(ContactSolver.Weapon("pistol"));
  gunShape.Add(new ContactSphere(new N(0,-.025f,0),.055f));world.WeaponShape=gunShape.ToArray();
  world.AccessPoint=new N(0,1.4076f,-.0378f);world.AccessAxis=N.UnitY;world.AccessAmmunition=true;world.Probe(true);
  for(int step=0;step<10;step++)
  {
   var a=world.AccessPoint-N.UnitY*(.15f-step*.013f);var b=a+N.UnitY*.013f;
   Check(world.Sweep(a,b,.018f,out _)>.99f,"real pistol grip envelope blocks feed end before socket");
  }
  // Curved AK bodies need their own wider entrance, while the hand remains solid.
  world.WeaponPose=new(N.Zero,Q.Identity);world.AccessRadius=.09f;world.AccessPoint=new N(0,1.5f,0);world.AccessAxis=N.UnitY;world.Probe(true);
  world.WeaponShape=new[]{new ContactSphere(new N(.04f,1.5f,0),.08f)};
  for(int cycle=0;cycle<12;cycle++)
    Check(world.Sweep(new N(.072f,1.36f,0),new N(.072f,1.49f,0),.025f,out _)>.99f,"curved AK magazine stopped by coarse receiver envelope");
  world.Probe(false);Check(world.Sweep(new N(.072f,1.36f,0),new N(.072f,1.49f,0),.025f,out _)<.9f,"AK access disables palm collision");
  world.IncludeWeapon=false;world.Access=false;
  world.IgnoredRoot=UnityEngine.Physics.Wall.transform;
  Check(world.Sweep(at,at+N.UnitZ,.05f,out _)>.99f,"carried NPC collides with own body");
  world.IgnoredRoot=null;
  Check(world.Sweep(at,at+N.UnitZ,.05f,out _)<=.01f,"release left NPC/world permanently ignored");
  world.Player=UnityEngine.Physics.Wall.transform;
  Check(world.Sweep(at,at+N.UnitZ,.05f,out _)>.99f,"player/self collider not excluded");
  world.Player=null;world.IgnoredRoot=null;UnityEngine.Physics.Wall=new UnityEngine.BoxCollider();
  world.BeginSolve();world.Clear(new N(0,.4f,.55f),.7f);
  int queries=UnityEngine.Physics.OverlapQueries;
  for(int i=0;i<40;i++)Check(world.Overlap(new N(0,.4f,.48f),.05f,out var cached)&&cached.Z<0,"cached collider lost penetration");
  Check(UnityEngine.Physics.OverlapQueries==queries,"each sphere repeats the broad-phase world query");
  world.Overlap(new N(0,2,.55f),.05f,out _);
  Check(UnityEngine.Physics.OverlapQueries==queries+1,"query outside cached region falsely assumed clear");
  world.EndSolve();world.Overlap(new N(0,.4f,.48f),.05f,out _);
  Check(UnityEngine.Physics.OverlapQueries==queries+2,"cache leaked beyond synchronous solve");
  UnityEngine.Physics.Saturated=true;world.BeginSolve();Check(!world.Clear(new N(0,.4f,.55f),.7f),"full query accepted as clear");
  Check(world.Overlap(new N(0,.4f,.48f),.05f,out var full)&&float.IsNaN(full.X),"full buffer allowed penetration");
  world.EndSolve();UnityEngine.Physics.Saturated=false;
  world.IgnoredRoot=UnityEngine.Physics.Wall.transform;world.BeginSolve();
  Check(world.Clear(new N(0,.4f,.55f),.7f)&&!world.Overlap(new N(0,.4f,.48f),.05f,out _),"held collider enters cached set");world.EndSolve();world.IgnoredRoot=null;
  // Exercise the real ContactRig routing: both guns can occupy the same pose.
  WeaponHands.Current=new WeaponHands{DualActive=true};
  using(var contacts=new ContactRig(new CameraRig()))
  {
   var right=new UnityEngine.Vector3(0,2,.3f);var left=right;var q=UnityEngine.Quaternion.identity;
   Check(contacts.ResolveGun("pistol",new(0,0,0),new(0,0,0),false,ref right,ref q),"right pistol unsafe in empty space");
   Check(contacts.ResolveLeftGun(ref left,ref q,null),"left pistol blocked by right pistol");
   Check(N.Distance(left.N,right.N)<.001f,"dual pistols still repel each other");
   UnityEngine.Time.frameCount++;
   right=new(0,2,.3f);
   Check(contacts.ResolveGun("pistol",new(0,0,0),new(0,0,0),false,ref right,ref q)&&N.Distance(left.N,right.N)<.001f,"reverse order re-enables dual collision");
   contacts.ResetGun();right=new(0,.4f,.25f);
   Check(contacts.ResolveGun("pistol",new(0,0,0),new(0,0,0),false,ref right,ref q),"cannot seed pistol before wall");
   for(int i=0;i<20;i++){UnityEngine.Time.frameCount++;right=new(0,.4f,.65f);contacts.ResolveGun("pistol",new(0,0,0),new(0,0,0),false,ref right,ref q);}
   Check(right.z<.5f,"disabling dual contact also disabled world collision");
  }
  Console.WriteLine("PASS: production ContactWorld handles fake initial-overlap normals; withdrawal/tangent/deeper contact; repeated contact; lift above finite fence; penetration; self filter. Mock Unity box, real adapter/solver.");
 }
}
namespace Il2CppInterop.Runtime {static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
 class Il2CppStructArray<T>{readonly T[] a;internal Il2CppStructArray(int n){a=new T[n];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}
 class Il2CppReferenceArray<T> where T:class{readonly T?[] a;internal Il2CppReferenceArray(int n){a=new T?[n];}internal int Length=>a.Length;internal T? this[int i]{get=>a[i];set=>a[i]=value;}}
}
namespace UnityEngine
{
 class Object{static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal static void Destroy(Object o){}}
 class Transform{internal string name="wall";internal Transform? parent;internal Vector3 position=>new(0,0,0);internal Quaternion rotation=>Quaternion.identity;internal bool IsChildOf(Transform t)=>ReferenceEquals(this,t);}
 class GameObject:Object{internal int layer;internal GameObject(string n){}internal SphereCollider AddComponent(Type t)=>new();}
 class BoxCollider:Collider{}
 class CapsuleCollider:Collider{}
 class MeshCollider:Collider{internal bool convex=false;}
 class Collider:Object{internal T? TryCast<T>() where T:class=>this as T;internal string name=>transform.name;internal bool enabled=true;internal Transform transform=new();internal Vector3 ClosestPoint(Vector3 p)=>Vector3.U(N.Clamp(p.N,Physics.Min,Physics.Max));}
 class SphereCollider:Collider{internal float radius;}
 readonly struct Vector3{internal readonly N N;internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal static Vector3 U(N p)=>new(p.X,p.Y,p.Z);internal static Vector3 zero=>new(0,0,0);}
 readonly struct Quaternion{internal readonly float x,y,z,w;internal Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}internal static Quaternion identity=>new(0,0,0,1);}
 struct RaycastHit{internal Collider collider;internal Vector3 normal;internal float distance;}
 enum QueryTriggerInteraction{Ignore}
 static class Physics
 {
  internal static readonly N Min=new(-.4f,0,.5f),Max=new(.4f,.9f,.6f);internal static Collider Wall=new BoxCollider();internal static int InitialHits,OverlapQueries;internal static bool Saturated=false;
  internal static int SphereCastNonAlloc(Vector3 start,float r,Vector3 direction,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> hits,float length,int mask,QueryTriggerInteraction q)
  {
   var a=start.N;var d=direction.N;var closest=N.Clamp(a,Min,Max);
   if(N.Distance(a,closest)<=r+.00001f)
   {InitialHits++;hits[0]=new RaycastHit{collider=Wall,normal=Vector3.U(-d),distance=0};return 1;}
   var min=Min-new N(r);var max=Max+new N(r);float lo=0,hi=length;N n=N.Zero;
   for(int i=0;i<3;i++)
   {
    float v=Get(d,i),p=Get(a,i),mn=Get(min,i),mx=Get(max,i);
    if(Math.Abs(v)<1e-8f){if(p<mn||p>mx)return 0;continue;}
    float t1=(mn-p)/v,t2=(mx-p)/v;var normal=-Axis(i)*Math.Sign(v);if(t1>t2)(t1,t2)=(t2,t1);
    if(t1>lo){lo=t1;n=normal;}hi=Math.Min(hi,t2);if(lo>hi)return 0;
   }
   hits[0]=new RaycastHit{collider=Wall,normal=Vector3.U(n),distance=lo};return 1;
  }
  internal static int OverlapSphereNonAlloc(Vector3 p,float r,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> hits,int mask,QueryTriggerInteraction q)
  {OverlapQueries++;if(Saturated)return hits.Length;if(N.Distance(p.N,N.Clamp(p.N,Min,Max))>=r)return 0;hits[0]=Wall;return 1;}
  internal static bool ComputePenetration(SphereCollider query,Vector3 center,Quaternion qr,Collider c,Vector3 cp,Quaternion cr,out Vector3 direction,out float depth)
  {
   var p=center.N;var delta=p-N.Clamp(p,Min,Max);float length=delta.Length();depth=query.radius-length;
   if(length>1e-8f){direction=Vector3.U(delta/length);return depth>0;}
   float best=float.PositiveInfinity;N n=N.Zero;
   for(int i=0;i<3;i++){float low=Get(p-Min,i),high=Get(Max-p,i);if(low<best){best=low;n=-Axis(i);}if(high<best){best=high;n=Axis(i);}}
   direction=Vector3.U(n);depth=query.radius+best;return true;
  }
  static float Get(N a,int i)=>i==0?a.X:i==1?a.Y:a.Z;static N Axis(int i)=>i==0?N.UnitX:i==1?N.UnitY:N.UnitZ;
 }
}

namespace UnityEngine{static class Time{internal static int frameCount=10;internal static float realtimeSinceStartup=0;internal static float unscaledDeltaTime=1f/90;}}
namespace XiiiXR
{
 class CameraRig{internal UnityEngine.Transform PlayerRoot=new();internal bool Scripted=>false;internal UnityEngine.Vector3 HeadPosition=>new(0,2.18f,0);internal void ResistanceHaptics(float a,bool right=false){}}
 class WeaponImpactAudio{internal static WeaponImpactAudio? Current=null;internal void Knock(UnityEngine.Vector3 p,float speed,string a,string b){}}
 class GrappleVr{internal static GrappleVr? Current=null;internal int Side=0;internal bool TryDeviceShape(out ContactSphere[] s,out ContactPose p){s=System.Array.Empty<ContactSphere>();p=default;return false;}internal bool TryHandShape(out ContactSphere[] s){s=System.Array.Empty<ContactSphere>();return false;}}
 class WeaponHands{internal static WeaponHands? Current=null;internal static bool LeftHanded=false;internal bool DualActive=false;internal bool LeftReloadHolding=>false;internal bool ReloadRight=>false;internal bool ReloadHandHolding(bool right)=>false;internal bool ReloadContactFree=>false;internal bool LeftPistolVisible=>DualActive;internal string Profile=>"pistol";internal bool PrimaryLeft=>false;internal bool CopyInHand(bool right)=>false;internal bool FreeHandCollides(bool right)=>!right;internal bool TryReloadAccess(UnityEngine.Vector3 p,UnityEngine.Quaternion q,out UnityEngine.Vector3 access,out UnityEngine.Vector3 axis){access=axis=default;return false;}}
 class GripCarry{internal static GripCarry? Current=null;internal UnityEngine.Transform? BodyRoot=>null;}
 class InteractionDriver{internal static InteractionDriver? Current=null;internal UnityEngine.Transform? HeldRoot(bool right)=>null;}
 class Flag{internal bool Value=true;}
 static class QualityOptions{internal static Flag Collisions=new();}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){throw new Exception(s);}}
 static class FramePerformance{internal static long Begin()=>0;internal static void End(long start,int x){}}
 class Geometry{internal N Min=>N.Zero;internal N Max=>N.One;internal N Forward=>N.UnitZ;internal System.Collections.Generic.List<(N min,N max)> Sections=new();}
 static class ReloadGripGeometry{internal static int Revision=>0;internal static Geometry? Get(string profile)=>null;}
}
