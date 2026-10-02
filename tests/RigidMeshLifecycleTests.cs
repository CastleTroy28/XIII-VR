using System;
using System.Collections.Generic;
using System.Linq;
using XiiiXR;
using UnityEngine;
class RigidMeshLifecycleTests
{
 static void Main(){
 var parent=new GameObject("parent");var visual=new RigidMeshVisual(parent.transform,"test",true);
 var geometry=HandMeshGeometry.BuildWatch(false);visual.Set(geometry);
 var owned=UnityEngine.Object.All.OfType<Material>().ToArray();
 if(owned.Any(x=>x.hideFlags!=HideFlags.DontUnloadUnusedAsset))throw new Exception("unprotected material");
 foreach(var m in owned)UnityEngine.Object.Destroy(m);
 // Palette size stays unchanged: Set must replace fake-null cached slots too.
 visual.Set(geometry);
 var renderer=UnityEngine.Object.All.OfType<MeshRenderer>().Single();
 if(renderer.sharedMaterials.Any(x=>x==null))throw new Exception("invalid palette after unload");
 // Force palette growth after losing only the basis.
 var basis=UnityEngine.Object.All.OfType<Material>().Last(x=>!x.dead&&!renderer.sharedMaterials.Contains(x));
 UnityEngine.Object.Destroy(basis);geometry.Palette.Add(new System.Numerics.Vector4(1));visual.Set(geometry);
 visual.Dispose();
 if(UnityEngine.Object.All.OfType<Material>().Any(x=>!x.dead))throw new Exception("material leak");
 Console.WriteLine("PASS destroyed basis/palette recovery, palette growth, owned disposal");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{public static Type Of<T>()=>typeof(T);}}
namespace UnityEngine.Rendering{enum ShadowCastingMode{Off}enum CompareFunction{Always}}
namespace UnityEngine
{
 enum HideFlags{DontUnloadUnusedAsset}
 class Object{
 internal static readonly List<Object> All=new();internal bool dead;public HideFlags hideFlags;
 public Object(){All.Add(this);}public static void Destroy(Object o){o.dead=true;}
 public static bool operator ==(Object? a,Object? b)=>ReferenceEquals(a,b)||((a is null||a.dead)&&(b is null||b.dead));
 public static bool operator !=(Object? a,Object? b)=>!(a==b);
 public override bool Equals(object? o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();
 public T? TryCast<T>()where T:class=>this as T;
 }
 class GameObject:Object{public int layer;public readonly Transform transform;public GameObject(string name){transform=new Transform(this);}public void SetActive(bool b){}public Object AddComponent(Type t)=>(Object)Activator.CreateInstance(t)!;}
 class Transform{public GameObject gameObject;public Vector3 localPosition,localScale;public Quaternion localRotation;public Transform(GameObject g){gameObject=g;}public void SetParent(Transform p,bool b){}}
 struct Vector3{public Vector3(float x,float y,float z){}public static Vector3 zero=>new();public static Vector3 one=>new();}
 struct Vector2{public Vector2(float x,float y){}}
 struct Quaternion{public static Quaternion identity=>new();}
 struct Color{public Color(float x,float y,float z,float w){}public static Color white=>new();public static Color operator *(Color c,float f)=>c;}
 class Shader:Object{public static Shader Find(string n)=>new();}
 class Texture2D:Object{public static Texture2D whiteTexture=new();}
 class Material:Object{
 public Material(Shader s){}public Material(Material source){if(source==null)throw new ArgumentNullException(nameof(source));}
 public Color color{set{if(dead)throw new NullReferenceException("destroyed material");}}
 public Texture2D? mainTexture;public int renderQueue;public void SetInt(string n,int v){}public bool HasProperty(string n)=>true;public void SetFloat(string n,float v){}public void EnableKeyword(string n){}public void SetColor(string n,Color c){}
 }
 class Mesh:Object{public string name="";public Vector3[] vertices=Array.Empty<Vector3>();public Vector2[] uv=Array.Empty<Vector2>();public Color[] colors=Array.Empty<Color>();public int subMeshCount;public void MarkDynamic(){}public void Clear(){}public void SetTriangles(int[] a,int s,bool b,int o){}public void RecalculateNormals(){}public void RecalculateBounds(){}}
 class MeshFilter:Object{public Mesh? sharedMesh;}
 class MeshRenderer:Object{public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Material[] sharedMaterials=Array.Empty<Material>();}
}
