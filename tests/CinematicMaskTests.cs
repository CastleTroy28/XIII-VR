using System;using System.Linq;using XiiiXR;using N=System.Numerics;
class CinematicMaskTests
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static bool Covers(HandMeshGeometry g,float x,float y)
 {
  var p=new N.Vector3(x,y,0);
  for(int i=0;i<g.Triangles.Count;i+=3)
  {var a=g.Vertices[g.Triangles[i]];var b=g.Vertices[g.Triangles[i+1]];var c=g.Vertices[g.Triangles[i+2]];
   var u=N.Vector3.Cross(b-a,p-a).Z;var v=N.Vector3.Cross(c-b,p-b).Z;var w=N.Vector3.Cross(a-c,p-c).Z;
   if((u>=0&&v>=0&&w>=0)||(u<=0&&v<=0&&w<=0))return true;}
  return false;
 }
 static void Main()
 {
  var rig=new CameraRig{Scripted=true};var bounds=ViewCoverage.Bounds(rig.FrustumLeft,rig.EyeLeft,rig.FrustumRight,rig.EyeRight,CinematicMask.Depth);var g=CinematicMask.Geometry(bounds);
  Check(!Covers(g,0,0)&&!Covers(g,.4f,.1f),"cinematic picture blocked");
  foreach(float x in new[]{bounds.X+.01f,-1f,1f,bounds.Y-.01f})foreach(float y in new[]{bounds.Z+.01f,0,bounds.W-.01f})Check(Covers(g,x,y),"outside view not blacked out");
  Check(Covers(g,0,bounds.Z+.01f)&&Covers(g,0,bounds.W-.01f),"top/bottom not covered");
  Check(g.Colors.All(c=>c==new N.Vector4(0,0,0,1))&&g.Triangles.Count==48,"surround not opaque black / unexpected geometry cost");
  // 0.1.182: the bars reach a quarter further on every side (a gap showed under them on Quest).
  var wide=CinematicMask.Widen(bounds);float bw=bounds.Y-bounds.X,bh=bounds.W-bounds.Z;
  Check(Math.Abs(wide.X-(bounds.X-bw*.25f))<1e-5f&&Math.Abs(wide.Y-(bounds.Y+bw*.25f))<1e-5f&&Math.Abs(wide.Z-(bounds.Z-bh*.25f))<1e-5f&&Math.Abs(wide.W-(bounds.W+bh*.25f))<1e-5f,"cutscene bars not widened on every side");
  var gw=CinematicMask.Geometry(wide);Check(Covers(gw,0,wide.Z+.01f)&&Covers(gw,0,bounds.Z-bh*.2f)&&!Covers(gw,0,0),"widened bars leave the bottom open or cover the picture");
  using(var mask=new CinematicMask())
  {mask.Render(rig);Check(RigidMeshVisual.Last.Sets==1&&UnityEngine.GameObject.Last.active,"mask not shown");mask.Render(rig);Check(RigidMeshVisual.Last.Sets==1,"mask rebuilt every eye");
   var destroyed=UnityEngine.GameObject.Last;var oldMesh=RigidMeshVisual.Last;
   UnityEngine.Object.Destroy(destroyed);mask.Render(rig);
   Check(RigidMeshVisual.Last!=oldMesh&&oldMesh.Disposed&&RigidMeshVisual.Last.Sets==1,"same-frustum scene reload recreated an empty mask");
   rig.MovieActive=true;mask.Render(rig);Check(!UnityEngine.GameObject.Last.active,"cinematic surround crops the story video");rig.MovieActive=false;
   rig.Scripted=false;mask.Render(rig);Check(!UnityEngine.GameObject.Last.active,"surround remains in gameplay");rig.Scripted=true;mask.Render(rig);Check(UnityEngine.GameObject.Last.active,"surround not restored next cutscene");}
  Check(RigidMeshVisual.Last.Disposed&&UnityEngine.GameObject.Last.Destroyed,"mask leaked across XR restart");
  Console.WriteLine("PASS: wide canted-eye surround covers outside picture, preserves centre, opaque double-sided geometry, cached construction and cinematic/gameplay lifecycle. Actual rendering untested.");
 }
}
namespace XiiiXR
{
 class CameraRig
 {internal bool Scripted;internal bool MovieActive=false;internal UnityEngine.Vector3 HeadPosition=>new();internal UnityEngine.Quaternion HeadRotation=>new();internal EyeFrustum FrustumLeft=>new(-1.8f,1.4f,-1.2f,1.2f);internal EyeFrustum FrustumRight=>new(-1.4f,1.8f,-1.2f,1.2f);internal PoseValue EyeLeft=>new(new(-.035f,0,0),N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY,-.2f));internal PoseValue EyeRight=>new(new(.035f,0,0),N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY,.2f));}
 class RigidMeshVisual:IDisposable{internal static RigidMeshVisual Last=null!;internal int Sets;internal bool Disposed;internal RigidMeshVisual(UnityEngine.Transform t,string s,bool display,bool overlay){if(!overlay)throw new Exception("surround must cover nearer world geometry");Last=this;}internal void Set(HandMeshGeometry g){Sets++;}public void Dispose(){Disposed=true;}}
 static class Bootstrap{internal static void Warn(string s)=>throw new Exception(s);}
}
namespace UnityEngine
{
 class Object
 {internal bool Destroyed;internal static void Destroy(GameObject o){o.Destroyed=true;}
  public static bool operator==(Object? a,Object? b){bool an=ReferenceEquals(a,null)||a.Destroyed,bn=ReferenceEquals(b,null)||b.Destroyed;return an||bn?an==bn:ReferenceEquals(a,b);}
  public static bool operator!=(Object? a,Object? b)=>!(a==b);
  public override bool Equals(object? o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();}
 class GameObject:Object{internal static GameObject Last=null!;internal bool active;internal int layer;internal Transform transform=new();internal GameObject(string s){Last=this;}internal void SetActive(bool b){active=b;}}
 class Transform{internal void SetPositionAndRotation(Vector3 p,Quaternion q){}}
 struct Vector3{internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
 struct Quaternion{public static Vector3 operator*(Quaternion q,Vector3 v)=>v;}
}
