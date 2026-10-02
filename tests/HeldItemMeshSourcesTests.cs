using System;using XiiiXR;using UnityEngine;using PlayMagic.Weapons;
class HeldItemMeshSourcesTests
{
 static void Check(bool ok,string error){if(!ok)throw new Exception(error);}
 static void Main()
 {
  var key=new SkinnedMeshRenderer{name="eqp_key_01_mesh",sharedMesh=new Mesh{vertexCount=60}};
  var source=new Equipable{skinnedMeshRenderers=new[]{key}};
  var got=HeldItemMeshSources.Find(source);
  Check(got.Count==1&&got[0]==key,"item skin outside Equipable hierarchy is lost");
  source.Children=new Obj[]{key};got=HeldItemMeshSources.Find(source);
  Check(got.Count==1,"explicit and child renderer duplicated");
  var empty=new MeshRenderer{name="key_lod0",Filter=new MeshFilter{sharedMesh=new Mesh()}};
  var low=new MeshRenderer{name="key_lod1",Filter=new MeshFilter{sharedMesh=new Mesh{vertexCount=24}}};
  source.skinnedMeshRenderers=null;source.Children=new Obj[]{empty,low};got=HeldItemMeshSources.Find(source);
  Check(got.Count==1&&got[0]==low,"empty high-detail mesh shadows usable LOD");
  var high=new MeshRenderer{name="key_lod0",Filter=new MeshFilter{sharedMesh=new Mesh{vertexCount=60}}};
  var arm=new SkinnedMeshRenderer{name="arms",sharedMesh=new Mesh{vertexCount=100}};
  var vfx=new MeshRenderer{name="vfx",Filter=new MeshFilter{sharedMesh=new Mesh{vertexCount=10}}};
  source.Children=new Obj[]{low,high,arm,vfx};got=HeldItemMeshSources.Find(source);
  Check(got.Count==1&&got[0]==high,"LOD duplicates or arm/effect geometry copied");
  source.Children=Array.Empty<Obj>();Check(HeldItemMeshSources.Find(source).Count==0,"empty source not passed to recovery");
  Check(source.IncludeInactive&&source.Scans==5,"inactive inventory skin omitted or extra scans");
  var car=new MeshRenderer{name="key_02",Filter=new(){sharedMesh=new(){name="key_02",vertexCount=40}}};
  var other=new MeshRenderer{name="key_020",Filter=new(){sharedMesh=new(){vertexCount=40}}};
  var carLow=new MeshRenderer{name="key_02_lod1",Filter=new(){sharedMesh=new(){vertexCount=20}}};
  var root=new Transform{Children=new Obj[]{other,arm,vfx,key,carLow,car}};
  got=HeldItemMeshSources.FindIn(root,"eqp_key_02");
  Check(got.Count==1&&got[0]==car,"detached key02 search includes another key/arm or duplicate LOD");
  Check(root.IncludeInactive,"hidden detached car key excluded");
  car.Filter.sharedMesh.vertexCount=0;got=HeldItemMeshSources.FindIn(root,"eqp_key_02");
  Check(got.Count==1&&got[0]==carLow,"empty detached LOD0 shadows valid key mesh");
  car.Filter.sharedMesh.vertexCount=40;car.name="generic renderer";root.Children=new Obj[]{car};
  Check(HeldItemMeshSources.FindIn(root,"eqp_key_02").Count==1,"exact shared mesh ID lost behind generic renderer name");
  car.name="key_02_LOD0(Clone)";car.Filter.sharedMesh.name="";
  Check(HeldItemMeshSources.FindIn(root,"EQP_KEY_02").Count==1,"clone/case/LOD suffix changes key identity");
  Check(HeldItemMeshSources.FindIn(root,"").Count==0,"empty ID matches unrelated mesh");
  root.Children=new Obj[]{car,arm,vfx};Check(HeldItemMeshSources.FindIn(root).Count==1,"matching pickup copies arms or effects");
  root.Children=new Obj[300];for(int i=0;i<root.Children.Length;i++)root.Children[i]=other;root.Children[299]=car;
  Check(HeldItemMeshSources.FindIn(root,"eqp_key_02").Count==0,"detached lookup lost its local scan bound");
  Console.WriteLine("PASS: explicit migrated skin links, duplicate suppression, inactive discovery, empty-LOD fallback, arm/effect exclusion; detached car-key full ID/mesh name, clone/LOD normalization, no near-name match, bounded lookup and exact pickup geometry. Unity references simulated; rendering not tested.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Obj{static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal T? TryCast<T>()where T:class=>this as T;}
 class Mesh:Obj{internal string name="";internal int vertexCount;}
 class MeshFilter:Obj{internal Mesh? sharedMesh;}
 class Renderer:Obj{internal string name="";internal MeshFilter? Filter;internal Obj? GetComponent(Type type)=>Filter;}
 class MeshRenderer:Renderer{}
 class SkinnedMeshRenderer:Renderer{internal Mesh? sharedMesh;}
 class Transform:Obj{internal Obj[] Children=Array.Empty<Obj>();internal bool IncludeInactive;internal Obj[] GetComponentsInChildren(Type type,bool inactive){IncludeInactive=inactive;return Children;}}
}
namespace PlayMagic.Weapons
{
 class Equipable:Obj{internal SkinnedMeshRenderer[]? skinnedMeshRenderers;internal Obj[] Children=Array.Empty<Obj>();internal int Scans;internal bool IncludeInactive;internal Obj[] GetComponentsInChildren(Type t,bool inactive){Scans++;IncludeInactive=inactive;return Children;}}
}
