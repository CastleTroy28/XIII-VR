using System;using XiiiXR;using UnityEngine;using PlayMagic;using Il2CppInterop.Runtime.InteropTypes.Arrays;
class TeleportTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var rig=new CameraRig();var player=new CustomCharacterController();using var d=new TeleportDriver();
  d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"unarmed release teleports");
  d.Tick(rig,player,true,false,true);d.Tick(rig,player,false,true,true);Check(player.controller.Moves==1&&player.transform.position.z==2,"clear floor did not use native controller move");
  void Reset(){d.Cancel();player.transform.position=Vector3.zero;player.controller.Moves=0;Physics.Wall=Physics.BlockBody=Physics.Occupied=Physics.Saturated=false;rig.HandValid=true;}
  Reset();Physics.Wall=true;d.Tick(rig,player,true,false,true);d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"wall selected as floor");
  Reset();Physics.BlockBody=true;d.Tick(rig,player,true,false,true);d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"body teleports through obstacle below ray");
  Reset();d.Tick(rig,player,true,false,true);Physics.Occupied=true;d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"destination not revalidated at release");
  Reset();d.Tick(rig,player,true,false,true);rig.HandValid=false;d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"tracking loss teleports");
  Reset();Physics.Saturated=true;d.Tick(rig,player,true,false,true);d.Tick(rig,player,false,true,true);Check(player.controller.Moves==0,"truncated collision buffer accepted");
  Reset();d.Tick(rig,player,true,false,true);d.Tick(rig,player,false,true,false);Check(player.controller.Moves==0,"menu/focus lock teleports");
  Check(!TeleportGeometry.Landing(1,45,8,0)&&!TeleportGeometry.Landing(1,45,2,2)&&!TeleportGeometry.Landing(0,45,2,0)&&!TeleportGeometry.Landing(float.NaN,45,2,0),"landing limits missing");
  Console.WriteLine("PASS: actual teleport adapter requires armed release, native capsule Move, floor/slope/range limits, free body path and destination; rechecks obstacles; tracking/locks/buffer overflow cancel. Mock physics, not in-game navigation.");
 }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
 class Il2CppStructArray<T>{readonly T[] a;internal Il2CppStructArray(int n){a=new T[n];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}
 class Il2CppReferenceArray<T>{readonly T?[] a;internal Il2CppReferenceArray(int n){a=new T[n];}internal int Length=>a.Length;internal T? this[int i]{get=>a[i];set=>a[i]=value;}}
}
namespace UnityEngine
{
 struct Vector3
 {
  internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}
  internal static Vector3 zero=>new();internal static Vector3 one=>new(1,1,1);internal static Vector3 up=>new(0,1,0);internal static Vector3 down=>new(0,-1,0);internal static Vector3 forward=>new(0,0,1);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);public static Vector3 operator /(Vector3 a,float b)=>a*(1/b);
  internal float sqrMagnitude=>x*x+y*y+z*z;internal float magnitude=>MathF.Sqrt(sqrMagnitude);internal static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;
 }
 struct Quaternion{public static Vector3 operator *(Quaternion a,Vector3 b)=>b;}
 class Transform{internal Vector3 position,lossyScale=Vector3.one;internal Vector3 TransformPoint(Vector3 p)=>p+position;internal bool IsChildOf(Transform p)=>this==p;}
 class Collider{internal bool enabled=true,isTrigger=false;internal Transform transform=new();}
 class CharacterController:Collider{internal float radius=.3f,height=1.8f,skinWidth=.03f,slopeLimit=45;internal Vector3 center=new(0,.9f,0);internal int Moves;internal void Move(Vector3 delta){Moves++;transform.position+=delta;}}
 struct RaycastHit{internal Collider? collider;internal Vector3 point,normal;internal float distance;}
 enum QueryTriggerInteraction{Ignore}
 static class Physics
 {
  internal static bool Wall,BlockBody,Occupied,Saturated;
  internal static int SphereCastNonAlloc(Vector3 p,float r,Vector3 d,Il2CppStructArray<RaycastHit> hits,float length,int mask,QueryTriggerInteraction q)
  {if(Saturated)return hits.Length;hits[0]=new RaycastHit{collider=new Collider(),point=new(0,0,2),normal=Wall?Vector3.forward:Vector3.up,distance=.01f};return 1;}
  internal static int OverlapCapsuleNonAlloc(Vector3 p,Vector3 e,float r,Il2CppReferenceArray<Collider> colliders,int mask,QueryTriggerInteraction q)
  {if(!Occupied)return 0;colliders[0]=new Collider();return 1;}
  internal static int CapsuleCastNonAlloc(Vector3 p,Vector3 e,float r,Vector3 direction,Il2CppStructArray<RaycastHit> hits,float length,int mask,QueryTriggerInteraction q)
  {if(!BlockBody)return 0;hits[0]=new RaycastHit{collider=new Collider()};return 1;}
 }
 static class Time{internal static float realtimeSinceStartup=>1;}
}
namespace PlayMagic
{
 class CustomCharacterController
 {
  internal bool restrictMovement=false,IsSpawning=false,isMounted=false,isDoingZipline=false;
  internal enum PlayerStates{Idling=1,Walking=2,Running=3,Crouching=4}
  internal PlayerStates CurrentPlayerState=PlayerStates.Idling;
  internal CharacterController controller=new();internal Transform transform=>controller.transform;internal Vector3 FeetPosition=>transform.position;
 }
}
namespace XiiiXR
{
 struct PoseValue{internal Vector3 Position;}
 class CameraRig{internal bool HandValid=true;internal Vector3 HeadPosition=>new(0,1.6f,0);internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){l=new(){Position=new(0,1.3f,0)};r=default;valid=HandValid;return true;}internal static Vector3 UnityPosition(PoseValue p)=>p.Position;internal void ResetRenderCaches(){}}
 static class ControllerAim{internal static Quaternion Rotation(PoseValue p)=>new();}
 class TeleportArc:IDisposable{internal void Hide(){}internal void Show(Vector3[] p,int count,bool valid){}public void Dispose(){}}
 class ContactRig{internal static ContactRig? Current=>null;internal void ResetGun(){}internal void ResetHand(bool r){}}
 class WeaponHands{internal static WeaponHands? Current=>null;internal void OnRelocated(){}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
