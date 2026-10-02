using System;using System.Linq;using System.Collections.Generic;using XiiiXR;using UnityEngine;
using Numerics=System.Numerics;
class CarryAnchorRuntimeTests
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 static void Near(Vector3 a,Vector3 b,string message){Check((a-b).magnitude<.00008f,message+" got "+a.ToString("F5")+" expected "+b.ToString("F5"));}
 static void Main()
 {
  int cases=0;
  foreach(bool hostage in new[]{false,true})foreach(bool right in new[]{false,true})foreach(float inherited in new[]{.01f,.2f,1f,3f})
  {
   var scene=new Transform{localScale=Vector3.one*1.1f};
   var body=new Transform{parent=scene,localScale=new Vector3(.9f,1.05f,.95f),localPosition=new Vector3(4,0,2)};
   var hips=new Transform{parent=body,localPosition=new Vector3(0,.8f,0),name="hips"};
   var head=new Transform{parent=hips,localPosition=new Vector3(0,.7f,.08f),name="head"};
   var foot=new Transform{parent=body,localPosition=new Vector3(0,.05f,0)};
   body.Components=new object[]{new SkinnedMeshRenderer{rootBone=hips}};
   var npc=new PlayMagic.AI.NPC{transform=body,Head=head};
   var spawn=new Transform{localScale=Vector3.one*inherited,localPosition=new Vector3(1,1.4f,3),name="BodyPickUpAnchor"};
   PlayerCarryAIController owner=hostage?new PlayerHostageController():new PickupBodiesController();owner.bodySpawnBone=spawn;
   var rig=new CameraRig();var c=new GripCarry(rig);c.Take(owner,npc,right);
   Check(c.PatchCount==3,"drop completion restore is not registered on base and derived controllers");
   var original=CarryBodyScale.Axes(body);var local=body.localScale;float height=(head.position-foot.position).magnitude;
   c.RenderBody();Near(CarryBodyScale.Axes(body),original,"pending native pickup changed world size");
   // Actual native coroutine: parent=bodySpawnParent; local position zero,
   // rotation identity, local scale ONE. .188 never undid the inherited scale.
   body.parent=spawn;body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;body.localScale=Vector3.one;
   owner.isInTransition=true;owner.bodyNPC=npc;
   for(int frame=0;frame<180;frame++)
   {
    float yaw=frame*3.1f;
    rig.HeadPosition=new Vector3(1+frame*.006f,1.6f+(frame>80?-.4f:0),3);
    rig.HeadRotation=Quaternion.Euler((frame%70)-35,yaw,0);
    spawn.localRotation=Quaternion.Euler(0,-yaw*.3f,0);
    // Native code/animator may reset root values again; don't accumulate.
    if(frame%11==0)body.localScale=Vector3.one;
    owner.isInTransition=frame<10;c.RenderBody();c.RenderBody();
    Check(c.TryCarryHand(right,out var hand,out var rotation,out var elbow),"occupied hand not anchored");
    Check(!c.TryCarryHand(!right,out _,out _,out _),"free hand stolen by hostage");
    var hold=CarryHoldMath.Hand((hostage?head:hips).position.N,CarryAnchorMath.Heading(rig.HeadRotation.N,Numerics.Quaternion.Identity),hostage,right);
    Near(hand,new Vector3(hold.position),"hand and neck use different anchor");
    Near(CarryBodyScale.Axes(body),original,"native reparent/animation/turn changes body size");
    Check(Math.Abs((head.position-foot.position).magnitude-height)<.0001f,"rendered head-to-foot length changed");
    var heading=CarryAnchorMath.Heading(rig.HeadRotation.N,Numerics.Quaternion.Identity);
    var target=CarryAnchorMath.Target(rig.HeadPosition.N,heading,hostage,right);
    Near((hostage?head:hips).position,new Vector3(target),"head/corpse pivot is misplaced after resizing, turning or crouching");
   }
   // The real drop resets scale AGAIN after reparenting and clears bodyNPC.
   var state=c.BeforeDrop(owner);body.parent=scene;body.localScale=Vector3.one;owner.bodyNPC=null;
   Check(c.AfterDrop(state,null)==null,"drop adds exception");Near(body.localScale,local,"authored scale lost after drop");
   Near(CarryBodyScale.Axes(body),original,"world size changed after drop");
   c.Take(owner,npc,right);body.parent=spawn;body.localScale=Vector3.one;owner.bodyNPC=npc;owner.isInTransition=false;c.RenderBody();
   Near(CarryBodyScale.Axes(body),original,"repeat pickup captured already reduced size");
   var error=new Exception("native failure");state=c.BeforeDrop(owner);body.parent=scene;body.localScale=Vector3.one;owner.bodyNPC=null;
   Check(ReferenceEquals(c.AfterDrop(state,error),error),"drop exception swallowed");Near(body.localScale,local,"failed drop loses authored scale");
   cases++;
  }
  // A rotated nonuniform parent: correct actual world basis lengths, not a
  // naive division by parent.lossyScale. Restoration recovers exact local TRS.
  var root=new Transform{localScale=new Vector3(.8f,1.2f,1.1f)};var saved=new CarryBodyScale(root);var want=CarryBodyScale.Axes(root);
  root.parent=new Transform{localScale=new Vector3(.1f,.2f,.4f),localRotation=Quaternion.Euler(0,42,0)};
  for(int i=0;i<80;i++){root.rotation=Quaternion.Euler(0,i*7,0);Check(saved.Apply(),"valid rotated nonuniform parent rejected");Near(CarryBodyScale.Axes(root),want,"nonuniform parent distorts axis lengths");}
  root.parent=null;root.localScale=Vector3.one;saved.Restore();Near(root.localScale,new Vector3(.8f,1.2f,1.1f),"exact local restore");
  var invalid=new Transform{localScale=new Vector3(0,1,1)};Check(!new CarryBodyScale(invalid).Apply(),"zero scale produced NaN/Infinity");
  // An interrupted pickup/drop without its completion callback is recovered
  // by the production render lifecycle; unrelated controllers cannot restore.
  var o=new PlayerHostageController();var t=new Transform{localScale=Vector3.one*.93f};var n=new PlayMagic.AI.NPC{transform=t};var cc=new GripCarry(new CameraRig());
  cc.Take(o,n,false);Check(cc.BeforeDrop(new PlayerHostageController())==null,"unrelated controller owns snapshot");t.localScale=Vector3.one;o.bodyNPC=null;cc.RenderBody();Near(t.localScale,Vector3.one*.93f,"cancel without callback loses original scale");
  Console.WriteLine($"PASS: production anchor + scale + drop hooks: {cases} native pickup fixtures, 2880 frames / 5760 render callbacks, both hands, hostage/corpse, .01/.2/1/3 inherited scales, animation reset, crouch/turn, repeat grab, exact drop restoration, exceptions, cancellation and nonuniform parents. Synthetic Unity transforms; no in-game VR test.");
 }
}
namespace XiiiXR
{
 internal sealed partial class GripCarry
 {
  internal static GripCarry? Current;private CameraRig rig;private PlayerCarryAIController? carrier;private bool releaseBody,bodyRight;
  private readonly HarmonyLib.Harmony patches=new();internal bool HoldingBody=>carrier!=null&&carrier.hasBody&&!carrier.isInTransition&&!releaseBody;
  internal GripCarry(CameraRig r){rig=r;Current=this;InstallBodyAnchor();}
  internal int PatchCount=>patches.Count;
  partial void InstallBodyAnchor();partial void RememberBodySize(PlayMagic.AI.NPC npc);partial void BeforeBodyRelease();
  internal void Take(PlayerCarryAIController c,PlayMagic.AI.NPC npc,bool right){carrier=c;bodyRight=right;releaseBody=false;RememberBodySize(npc);c.bodyNPC=npc;c.isInTransition=true;}
  internal CarryBodyScale? BeforeDrop(PlayerCarryAIController c){releaseBody=true;BeforeBodyRelease();BeginSizeRestore(c,out var s);return s;}
  internal Exception? AfterDrop(CarryBodyScale? s,Exception? e)=>EndSizeRestore(e,s);
  private static Numerics.Vector3 V(Vector3 v)=>v.N;private static Quaternion U(Numerics.Quaternion q)=>new(q);
 }
 class CameraRig{internal Vector3 HeadPosition=new(1,1.6f,3);internal Quaternion HeadRotation=Quaternion.identity;internal bool Scripted=false,HeadTrackingValid=true;}
 class NpcBones{internal bool Usable=>Head!=null;internal Transform Head=null!;internal Transform? neckBase=>Head;internal Transform? neckTop=>Head;internal static NpcBones Of(PlayMagic.AI.NPC n,out string s){s="";return new(){Head=n.Head!};}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}static class Extensions{internal static T? TryCast<T>(this object o) where T:class=>o as T;}}
namespace HarmonyLib{class Harmony{internal int Count;internal void Patch(object m,HarmonyMethod? prefix=null,HarmonyMethod? finalizer=null){Count++;}}class HarmonyMethod{internal HarmonyMethod(Type t,string n){}}static class AccessTools{internal static object DeclaredMethod(Type t,string n)=>new();}}
namespace PlayMagic.AI{class NPC{internal Transform transform=new();internal Transform? Head;internal string name=>transform.name;}}
class PlayerCarryAIController
{
 internal readonly IntPtr Pointer=(IntPtr)(++next);static int next;internal PlayMagic.AI.NPC? bodyNPC;internal Transform? bodySpawnBone;internal bool isInTransition;internal bool hasBody=>bodyNPC!=null;
}
class PlayerHostageController:PlayerCarryAIController{}class PickupBodiesController:PlayerCarryAIController{}
namespace UnityEngine
{
 struct Vector3
 {
  internal Numerics.Vector3 N;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(Numerics.Vector3 n){N=n;}
  internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal float magnitude=>N.Length();internal float sqrMagnitude=>N.LengthSquared();
  internal static Vector3 zero=>new(Numerics.Vector3.Zero);internal static Vector3 one=>new(Numerics.Vector3.One);internal static Vector3 right=>new(Numerics.Vector3.UnitX);internal static Vector3 up=>new(Numerics.Vector3.UnitY);internal static Vector3 forward=>new(Numerics.Vector3.UnitZ);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.N-b.N);public static Vector3 operator*(Vector3 a,float b)=>new(a.N*b);
  internal string ToString(string format)=>N.ToString(format);
 }
 struct Quaternion
 {
  internal Numerics.Quaternion N;internal Quaternion(Numerics.Quaternion n){N=n;}internal Quaternion(float x,float y,float z,float w){N=new(x,y,z,w);}internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal float w=>N.W;
  internal static Quaternion identity=>new(Numerics.Quaternion.Identity);
  internal static Quaternion Euler(float x,float y,float z)=>new(Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));
  internal static Quaternion Inverse(Quaternion q)=>new(Numerics.Quaternion.Inverse(q.N));public static Quaternion operator*(Quaternion a,Quaternion b)=>new(Numerics.Quaternion.Normalize(a.N*b.N));
  internal Vector3 eulerAngles{get{var f=Numerics.Vector3.Transform(Numerics.Vector3.UnitZ,N);return new(0,MathF.Atan2(f.X,f.Z)*180/MathF.PI,0);}}
 }
 class Transform
 {
  internal Transform? parent;internal string name="NPC";internal Vector3 localPosition=Vector3.zero,localScale=Vector3.one;internal Quaternion localRotation=Quaternion.identity;internal object[] Components=Array.Empty<object>();
  Numerics.Matrix4x4 Matrix=>Numerics.Matrix4x4.CreateScale(localScale.N)*Numerics.Matrix4x4.CreateFromQuaternion(localRotation.N)*Numerics.Matrix4x4.CreateTranslation(localPosition.N)*(parent?.Matrix??Numerics.Matrix4x4.Identity);
  internal Vector3 position{get=>new(Numerics.Vector3.Transform(Numerics.Vector3.Zero,Matrix));set{Numerics.Matrix4x4.Invert(parent?.Matrix??Numerics.Matrix4x4.Identity,out var inverse);localPosition=new(Numerics.Vector3.Transform(value.N,inverse));}}
  internal Quaternion rotation{get=>(parent?.rotation??Quaternion.identity)*localRotation;set=>localRotation=Quaternion.Inverse(parent?.rotation??Quaternion.identity)*value;}
  internal bool IsChildOf(Transform other)=>this==other||parent?.IsChildOf(other)==true;
  internal Vector3 TransformVector(Vector3 v)=>new(Numerics.Vector3.TransformNormal(v.N,Matrix));
  internal object[] GetComponentsInChildren(Type t,bool inactive)=>Components.Where(t.IsInstanceOfType).ToArray();
 }
 class SkinnedMeshRenderer{internal Transform? rootBone;}
 static class Time{internal static float realtimeSinceStartup=1;}
}
