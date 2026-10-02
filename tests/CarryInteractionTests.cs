using System;using XiiiXR;using UnityEngine;
class CarryInteractionTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var c=new InteractionDriver();InteractionDriver.Current=c;var arms=new PlayerArmsAnimationControl{isInteracting=true,transform=new Transform{parent=c.Root}};
  c.Press(true);bool state=InteractionDriver.BeginTest(arms);Check(state&&!arms.isInteracting,"carry latch blocks free main hand");
  var error=new Exception("native query failed");Check(InteractionDriver.EndTest(arms,state,error)==error&&arms.isInteracting,"native exception does not restore latch");
  c.OccupiedRight=true;Check(!InteractionDriver.BeginTest(arms)&&arms.isInteracting,"occupied hand bypasses native interaction lock");
  c.Press(false);Check(InteractionDriver.BeginTest(arms)&&!arms.isInteracting,"free other hand cannot interact");InteractionDriver.EndTest(arms,true,null);
  c.Enabled=false;Check(!InteractionDriver.BeginTest(arms),"menu/story/input gate bypassed");c.Enabled=true;
  c.Holding=false;Check(!InteractionDriver.BeginTest(arms),"non-carry action bypasses native latch");c.Holding=true;
  arms.transform=new Transform();Check(!InteractionDriver.BeginTest(arms),"another player's animation state altered");arms.transform.parent=c.Root;
  c.BodyTarget=true;Check(!InteractionDriver.BeginTest(arms),"second NPC can be taken while carrying");c.BodyTarget=false;
  var body=c.Body;var held=new Collider{transform=new Transform{parent=body}};var self=new Collider{transform=new Transform{parent=c.Root}};
  var wall=new Collider();var pickup=new Collider();
  var ray=new RaycastSystem{hasRayHit=true,rayHit=new RaycastHit{collider=held,distance=.15f}};
  Physics.Hits=new[]{new RaycastHit{collider=pickup,distance=2},new RaycastHit{collider=held,distance=.15f},new RaycastHit{collider=self,distance=.1f},new RaycastHit{collider=wall,distance=1}};
  c.SkipTest(ray);Check(ray.rayHit.collider==wall&&ray.raycastHittable==wall,"held body filter takes through a nearer wall");
  ray.rayHit=new RaycastHit{collider=held};Physics.Hits=new[]{new RaycastHit{collider=held,distance=.1f},new RaycastHit{collider=pickup,distance=1}};
  c.SkipTest(ray);Check(ray.rayHit.collider==pickup&&ray.raycastHittable==pickup,"held NPC masks a reachable pickup");
  ray.rayHit=new RaycastHit{collider=held};Physics.Hits=new[]{new RaycastHit{collider=held}};
  c.SkipTest(ray);Check(!ray.hasRayHit&&ray.raycastHittable==null,"no-target ray retains old held NPC highlight");
  ray.hasRayHit=true;ray.rayHit=new RaycastHit{collider=held};Physics.Full=true;c.SkipTest(ray);
  Check(ray.rayHit.collider==held,"saturated query selected from an incomplete hit list");Physics.Full=false;
  Console.WriteLine("PASS: production carry interaction: free/occupied hands, scoped latch restoration including errors, native gameplay/owner/body gates, carried-NPC ray filtering, walls and query saturation.");
 }
}
namespace XiiiXR
{
 internal sealed partial class InteractionDriver
 {
  internal static InteractionDriver? Current;
  private readonly HarmonyLib.Harmony patches=new();private readonly GripCarry carry=new();
  private readonly InputState input=new(),offInput=new();private Transform? root=new();
  internal Transform Root=>root!;internal Transform Body=>carry.BodyRoot!;
  internal bool Holding{set=>carry.HoldingBody=value;}internal bool Enabled=true,OccupiedRight=false,BodyTarget=false;
  private static bool MainRight=>true;private static bool OffRight=>false;
  private bool Allowed()=>Enabled;private void Sample(){}private bool HandOccupied(bool right)=>right==OccupiedRight;
  internal void Press(bool main){input.Action.Down=main;offInput.Action.Down=!main;}
  private bool PrepareRay(out Vector3 p,out Quaternion q){p=default;q=default;return Enabled;}
  internal static bool BeginTest(PlayerArmsAnimationControl a){BeginCarryInteraction(a,out bool state);return state;}
  internal static Exception? EndTest(PlayerArmsAnimationControl a,bool s,Exception? e)=>EndCarryInteraction(e,a,s);
  internal void SkipTest(RaycastSystem r)=>SkipCarriedNpc(r);
 }
 class InputState{internal Edge Action=new();}class Edge{internal bool Down;}
 class GripCarry{internal bool HoldingBody=true;internal Transform? BodyRoot=new();}
}
namespace HarmonyLib{class Harmony{internal void Patch(object a,HarmonyMethod? prefix=null,HarmonyMethod? finalizer=null){}}class HarmonyMethod{internal HarmonyMethod(Type t,string n){}}static class AccessTools{internal static object DeclaredMethod(Type t,string n)=>new();}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays{class Il2CppStructArray<T>{readonly T[] a;internal Il2CppStructArray(int n){a=new T[n];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}}
namespace UnityEngine
{
 class Transform{internal Transform? parent=null;internal bool IsChildOf(Transform p)=>ReferenceEquals(this,p)||parent?.IsChildOf(p)==true;}
 class Collider{internal Transform transform=new();}
 struct Vector3{internal static Vector3 forward=>default;}
 struct Quaternion{public static Vector3 operator*(Quaternion q,Vector3 p)=>p;}
 struct RaycastHit{internal Collider? collider;internal float distance;}
 static class Physics
 {
  internal static RaycastHit[] Hits=Array.Empty<RaycastHit>();internal static bool Full=false;
  internal static int RaycastNonAlloc(Vector3 p,Vector3 d,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> a,float distance,int mask,int query)
  {for(int i=0;i<Hits.Length;i++)a[i]=Hits[i];return Full?a.Length:Hits.Length;}
 }
}
class PlayerArmsAnimationControl{internal Transform transform=new();internal bool isInteracting;}
class RaycastSystem
{
 internal bool hasRayHit;internal RaycastHit rayHit;internal float rayDistance=3;internal int rayLayerMasks=-1,queryTriggerInteraction=0;internal object? raycastHittable;
 internal void UpdateRaycastHittable(RaycastHit hit){rayHit=hit;raycastHittable=hit.collider;}
}
