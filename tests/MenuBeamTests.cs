using System;using System.Linq;using XiiiXR;using UnityEngine;using N=System.Numerics.Vector3;
class MenuBeamTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  using var beam=new MenuBeam();GameObject.FailSphere=true;bool failed=false;
  try{beam.Show(new(0,0,0),new(0,0,2),true);}catch{failed=true;}
  Check(failed&&GameObject.All.All(g=>g.Destroyed),"partial ray construction survives exception");
  GameObject.FailSphere=false;beam.Show(new(1,1,1),new(1,1,3),true);
  var parts=GameObject.All.Where(g=>!g.Destroyed).ToArray();Check(parts.Length==2&&parts.All(g=>g.active),"ray cannot recover from partial creation");
  Check(parts.All(g=>g.layer==5&&!g.Collider.enabled&&g.Collider.Destroyed),"visual ray blocks physics");
  // 0.1.200: drawn after any world canvas (the death panel's order is 300).
  Check(parts.All(g=>g.Renderer.sortingOrder==MenuBeam.RaySortingOrder&&MenuBeam.RaySortingOrder>300),"the ray drawn under a panel");
  Check(parts[0].transform.position.N==new N(1,1,2)&&parts[0].transform.localScale.z==2,"ray does not span controller to hit");
  Check(parts[1].transform.position.N==new N(1,1,3),"hit marker at wrong point");
  beam.Hide();Check(parts.All(g=>!g.active),"ray persists after menu closes");
  beam.Show(new(0,0,0),new(1,0,0),false);Check(GameObject.All.Count(g=>!g.Destroyed)==2,"every pointer tick allocates another ray");
  beam.Dispose();Check(GameObject.All.All(g=>g.Destroyed),"ray leaked on stop");
  Console.WriteLine("PASS: 0.1.200 the ray and its dot are drawn over the death panel (sorted after world canvases).");
  Console.WriteLine("PASS: actual mesh ray recovers from stripped/failed creation, spans controller to hit, reuses geometry, disables all colliders, hides and disposes.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine.Rendering{enum ShadowCastingMode{Off}}
namespace UnityEngine
{
 class Object{internal bool Destroyed;internal static void Destroy(Object o){o.Destroyed=true;}internal static void DontDestroyOnLoad(Object o){}internal T? TryCast<T>() where T:class=>this as T;}
 class GameObject:Object
 {
  internal static readonly System.Collections.Generic.List<GameObject> All=new();internal static bool FailSphere;
  internal string name="";internal int layer;internal bool active;internal Transform transform=new();internal Collider Collider=new();internal Renderer Renderer=new();
  internal static GameObject CreatePrimitive(PrimitiveType type){if(FailSphere&&type==PrimitiveType.Sphere)throw new Exception("injected creation failure");var g=new GameObject();All.Add(g);return g;}
  internal Object GetComponent(Type t)=>t==typeof(Collider)?Collider:Renderer;internal void SetActive(bool value){active=value;}
 }
 class Transform{internal Vector3 position,localScale;internal void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;}}
 class Collider:Object{internal bool enabled=true;}
 class Renderer:Object{internal Material? sharedMaterial;internal Rendering.ShadowCastingMode shadowCastingMode;internal bool receiveShadows;internal int sortingOrder;}
 class Material:Object{internal Color color;internal int renderQueue;internal Material(Shader s){}}
 class Shader:Object{internal static Shader Find(string s)=>new();}
 enum PrimitiveType{Cube,Sphere}
 readonly struct Color{internal Color(float r,float g,float b,float a){}}
 readonly struct Quaternion{internal static Quaternion LookRotation(Vector3 v)=>default;}
 readonly struct Vector3
 {
  internal readonly N N;internal float z=>N.Z;internal float magnitude=>N.Length();internal Vector3(float x,float y,float z){N=new(x,y,z);}Vector3(N n){N=n;}
  internal static Vector3 one=>new(1,1,1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.N-b.N);public static Vector3 operator*(Vector3 a,float b)=>new(a.N*b);
 }
}
