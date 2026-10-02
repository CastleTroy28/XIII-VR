using System;using System.Collections.Generic;using System.Linq;using XiiiXR;using UnityEngine;using N=System.Numerics;
class NpcFistPoseTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Near(Quaternion a,Quaternion b,string s)=>Check(Math.Abs(Quaternion.Dot(a,b))>.99999f,s);
 static void Main()
 {
  foreach(bool right in new[]{true,false})foreach(bool prefix in new[]{true,false})
  {
   var npc=new PlayMagic.AI.NPC();var root=new Transform("root");root.localRotation=Quaternion.AngleAxis(37,new(0,1,0));
   var wrist=new Transform("wrist",root){localPosition=new Vector3(.2f,1.5f,.3f)};
   wrist.localRotation=Quaternion.AngleAxis(53,new(0,0,1));
   var bones=new List<Transform>{root,wrist};var fingers=new List<Transform>();
   for(int c=0;c<5;c++)
   {
    Transform parent=wrist;
    for(int j=0;j<3;j++)
    {
     var t=new Transform((prefix?"skeleton:":"")+NpcFist.Name(right,c,j),parent);
     float mirror=right?1:-1;
     t.localPosition=j==0?(c<4?new Vector3((c-1.5f)*.02f*mirror,0,.09f):new Vector3(-.052f*mirror,-.005f,.035f)):new Vector3(0,0,j==1?.035f:.024f);
     bones.Add(t);fingers.Add(t);parent=t;
    }
   }
   var mesh=new Mesh{bindposes=bones.Select(t=>t.localToWorldMatrix.inverse).ToArray()};
   npc.Skins.Add(new SkinnedMeshRenderer{bones=bones.ToArray(),sharedMesh=mesh});
   var pose=NpcFist.Create(npc,wrist,right,out var why);Check(pose!=null,"bind hand failed: "+why);
   // An asymmetric gun-grip animation before disarm must be completely replaced.
   for(int i=0;i<fingers.Count;i++)fingers[i].localRotation=Quaternion.AngleAxis(13+i,new Vector3(0,1,0))*Quaternion.AngleAxis(-12,new Vector3(0,0,1));
   var original=fingers.Select(t=>t.localRotation).ToArray();var wristOriginal=wrist.localRotation;
   pose!.RestoreWrist();pose.Orient(Vector3.forward,Vector3.up,1);pose.Clench(1);
   var closed=fingers.Select(t=>t.localRotation).ToArray();var wr=wrist.localRotation;var point=pose.Knuckle;
   Check(Vector3.Dot((point-wrist.position).normalized,Vector3.forward)>.995f,"knuckles not aligned with punch direction");
   for(int i=0;i<12;i++)Check(Math.Abs(Quaternion.Dot(original[i],closed[i]))<.95f,"a finger kept its gun pose");
   // The first phalanx must curl towards the palm in both mirrored hands.
   var first=fingers[1].position-fingers[0].position;
   Check(first.y<-.02f,"finger curled away from palm: "+first.y);
   for(int frame=0;frame<300;frame++)
   {
    pose.RestoreWrist();pose.Orient(Vector3.forward,Vector3.up,1);pose.Clench(1);
    Near(wrist.localRotation,wr,"wrist drifts under repeated render");
    for(int i=0;i<15;i++)Near(fingers[i].localRotation,closed[i],"finger drifts under repeated render");
   }
   pose.Release();Near(wrist.localRotation,wristOriginal,"wrist not restored");
   for(int i=0;i<15;i++)Near(fingers[i].localRotation,original[i],"finger not restored");
   // Animator writes a different underlying pose; release must return that new pose.
   var fresh=Quaternion.AngleAxis(18,new(1,0,0));fingers[0].localRotation=fresh;
   pose.Clench(1);pose.Release();Near(fingers[0].localRotation,fresh,"fresh animation lost");
  }
  Console.WriteLine("PASS NpcFist production pose: mirrored/namespaced rigs, rotated bind hierarchy, absolute closed fingers, anatomical wrist alignment, 300 repeated renders without drift, restore and fresh-animation ownership. Synthetic skeletons, not in-game rendering.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace PlayMagic.AI
{
 class NPC:UnityEngine.Object
 {
  internal List<UnityEngine.SkinnedMeshRenderer> Skins=new();
  internal IEnumerable<UnityEngine.Object> GetComponentsInChildren(Type t,bool all)=>Skins;
 }
}
namespace UnityEngine
{
 class Object
 {
  static int next;internal IntPtr Pointer=new(++next);internal T? TryCast<T>() where T:class=>this as T;
 }
 class Transform:Object
 {
  internal string name;internal Transform? parent;internal Vector3 localPosition;internal Quaternion localRotation=Quaternion.identity;
  internal Transform(string name,Transform? parent=null){this.name=name;this.parent=parent;}
  internal Matrix4x4 localToWorldMatrix=>new(N.Matrix4x4.CreateFromQuaternion(localRotation.q)*N.Matrix4x4.CreateTranslation(localPosition.v)*(parent?.localToWorldMatrix.m??N.Matrix4x4.Identity));
  internal Vector3 position=>localToWorldMatrix.MultiplyPoint3x4(Vector3.zero);
  internal Quaternion rotation{get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
  internal Vector3 TransformPoint(Vector3 p)=>localToWorldMatrix.MultiplyPoint3x4(p);
 }
 class Mesh{internal Matrix4x4[] bindposes=Array.Empty<Matrix4x4>();}
 class SkinnedMeshRenderer:Object{internal Mesh? sharedMesh;internal Transform[] bones=Array.Empty<Transform>();}
 struct Vector3
 {
  internal N.Vector3 v;internal Vector3(float x,float y,float z){v=new(x,y,z);}internal Vector3(N.Vector3 v){this.v=v;}
  internal float x=>v.X;internal float y=>v.Y;internal float z=>v.Z;internal float sqrMagnitude=>v.LengthSquared();internal Vector3 normalized=>new(N.Vector3.Normalize(v));
  internal static Vector3 zero=>new(0,0,0);internal static Vector3 up=>new(0,1,0);internal static Vector3 forward=>new(0,0,1);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.v+b.v);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.v-b.v);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.v*b);public static Vector3 operator/(Vector3 a,float b)=>new(a.v/b);
  internal static float Dot(Vector3 a,Vector3 b)=>N.Vector3.Dot(a.v,b.v);
  internal static float SignedAngle(Vector3 a,Vector3 b,Vector3 axis)=>MathF.Atan2(N.Vector3.Dot(N.Vector3.Cross(a.v,b.v),axis.v),N.Vector3.Dot(a.v,b.v))*180/MathF.PI;
 }
 struct Quaternion
 {
  internal N.Quaternion q;internal Quaternion(N.Quaternion q){this.q=q;}
  internal static Quaternion identity=>new(N.Quaternion.Identity);
  internal static Quaternion AngleAxis(float d,Vector3 a)=>new(N.Quaternion.CreateFromAxisAngle(N.Vector3.Normalize(a.v),d*MathF.PI/180));
  internal static Quaternion Inverse(Quaternion a)=>new(N.Quaternion.Inverse(a.q));
  internal static float Dot(Quaternion a,Quaternion b)=>N.Quaternion.Dot(a.q,b.q);
  internal static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>new(N.Quaternion.Slerp(a.q,b.q,t));
  public static Quaternion operator*(Quaternion a,Quaternion b)=>new(a.q*b.q);
  internal static Quaternion LookRotation(Vector3 forward,Vector3 up)
  {
   var z=N.Vector3.Normalize(forward.v);var x=N.Vector3.Normalize(N.Vector3.Cross(up.v,z));var y=N.Vector3.Cross(z,x);
   return new(N.Quaternion.CreateFromRotationMatrix(new N.Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1)));
  }
 }
 struct Matrix4x4
 {
  internal N.Matrix4x4 m;internal Matrix4x4(N.Matrix4x4 m){this.m=m;}
  internal Matrix4x4 inverse{get{N.Matrix4x4.Invert(m,out var inv);return new(inv);}}
  internal Vector3 MultiplyPoint3x4(Vector3 p)=>new(N.Vector3.Transform(p.v,m));
  internal Vector3 MultiplyVector(Vector3 p)=>new(N.Vector3.TransformNormal(p.v,m));
  public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new(b.m*a.m);
  internal float determinant=>m.GetDeterminant();
  internal Vector3 GetColumn(int i)=>i==1?new(m.M21,m.M22,m.M23):new(m.M31,m.M32,m.M33);
 }
 static class Mathf{internal static float Clamp01(float x)=>Math.Clamp(x,0,1);}
}
