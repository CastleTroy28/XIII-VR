using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XiiiXR;
using N=System.Numerics;
class HandSkinLifecycleTests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Near(Vector3 p,N.Vector3 expected,string why)=>Check(N.Vector3.Distance(p.Value,expected)<1e-5f,why);
    static void Main()
    {
        var sourceRoot=new GameObject("persistent player");UnityEngine.Object.DontDestroyOnLoad(sourceRoot);
        var source=(SkinnedMeshRenderer)sourceRoot.AddComponent(typeof(SkinnedMeshRenderer));
        var original=new Mesh{vertices=new[]{new Vector3(1,0,0)},bindposes=new[]{Matrix4x4.identity}};
        source.sharedMesh=original;source.bones=new[]{sourceRoot.transform};source.enabled=true;
        var owner=new GameObject("retained VR hand");UnityEngine.Object.DontDestroyOnLoad(owner);
        var hand=new NativeHandVisual(source,owner);
        var rest=new[]{Matrix4x4.identity};
        var curl=new[]{new Matrix4x4(N.Matrix4x4.CreateRotationZ(MathF.PI/2))};
        var held=new[]{new Matrix4x4(N.Matrix4x4.CreateRotationY(-MathF.PI/2))};
        var first=hand.Target();hand.Bake(rest);Near(first.vertices[0],N.Vector3.UnitX,"initial open hand");
        int resets=hand.Resets;hand.Bake(curl);Near(first.vertices[0],N.Vector3.UnitY,"initial fist deforms");
        Check(hand.Resets==resets,"healthy buffer invalidated every frame");
        // Reproduce what made the old scratch mesh unsafe: no Unity component
        // owns it. A CoreCLR wrapper does not count as an IL2CPP scene reference.
        var oldUnrooted=new Mesh();UnityEngine.Object.UnloadUnusedAssets();
        Check(oldUnrooted==null,"mock failed to unload an unrooted mesh");
        Check(first!=null&&ReferenceEquals(first,hand.Target()),"streaming unloaded the live hand bake target");
        for(int scene=0;scene<3;scene++)
        {
            UnityEngine.Object.UnloadScene();UnityEngine.Object.UnloadUnusedAssets();
            Check(hand.Usable,"private skin bones died during bank/flashback scene unload");
            hand.Bake(rest);Near(hand.Target().vertices[0],N.Vector3.UnitX,"open palm after flashback");
            hand.Bake(curl);Near(hand.Target().vertices[0],N.Vector3.UnitY,"fist after flashback");
            hand.Bake(held);Near(hand.Target().vertices[0],N.Vector3.UnitZ,"authored equipment grip after flashback");
        }
        Check(hand.Resets==resets,"scene change needlessly rebuilds protected buffers");
        // Even explicit destruction must not silently preserve the last pose.
        UnityEngine.Object.Destroy(first!);Check(!ReferenceEquals(first,null)&&first==null,"need Unity fake-null wrapper");
        var replacement=hand.Target();Check(replacement!=null&&!ReferenceEquals(replacement,first),"missing target not recreated");
        Check(hand.Resets==resets+1,"cached unchanged grip not dirtied after recovery");
        hand.Bake(held);Near(replacement!.vertices[0],N.Vector3.UnitZ,"same held pose cannot be baked after recovery");
        Check(Bootstrap.Messages.Count==1,"recovery not logged once");
        Check(original.hideFlags==HideFlags.None&&source.enabled,"borrowed source mutated");
        Near(sourceRoot.transform.localPosition,N.Vector3.Zero,"source skeleton moved");
        Check(sourceRoot.transform.localRotation.Value==N.Quaternion.Identity,"source skeleton rotated");
        var baker=UnityEngine.Object.All.OfType<SkinnedMeshRenderer>().Single(x=>x!=source&&!x.dead);
        UnityEngine.Object.Destroy(baker.gameObject);Check(!hand.Usable,"lost private renderer is still a valid hand");
        hand.Dispose();hand.Dispose();
        Check(replacement==null&&original!=null&&source!=null,"dispose leaks owned mesh or destroys borrowed assets");
        Check(!UnityEngine.Object.All.OfType<GameObject>().Any(x=>!x.dead&&x.name.StartsWith("snapshot bone")),"private bone leak");
        using(var rebuilt=new NativeHandVisual(source!,owner))
        {
            rebuilt.Bake(curl);Near(rebuilt.Target().vertices[0],N.Vector3.UnitY,"rebinding after private renderer destruction fails");
            rebuilt.DestroyVisible();Check(!rebuilt.Usable,"destroyed visible mesh not detected");
        }
        Check(!UnityEngine.Object.All.OfType<Mesh>().Any(x=>!x.dead&&(x.hideFlags&HideFlags.DontUnloadUnusedAsset)!=0),"protected mesh leak");
        // 0.1.122: a bone scaled away (a hidden crossbow bolt) is not taken as its loaded place.
        Check(Math.Abs(NativeSkinSnapshot.SkinScale(Matrix4x4.identity)-1)<1e-5f&&Math.Abs(NativeSkinSnapshot.SkinScale(new Matrix4x4(N.Matrix4x4.CreateScale(.5f)))-.125f)<1e-5f&&NativeSkinSnapshot.SkinScale(new Matrix4x4(N.Matrix4x4.CreateScale(0)))==0,"bone scale of a skin matrix");
        Console.WriteLine("PASS production hand resource owner + NativeSkinSnapshot: simulated asset/scene unload, fists and authored poses, fake-null buffer recovery, dirty-cache reset, renderer invalidation, rebind and disposal; native source untouched.");
        Console.WriteLine("Unity lifecycle/CPU skinning simulated; actual bank assets and headset not available.");
    }
}
namespace XiiiXR
{
    // Only the geometry/controller portion is replaced. Resource creation,
    // recovery, validity, disposal and the private bone bake are production code.
    internal sealed partial class NativeHandVisual:IDisposable
    {
        private readonly bool rightHand=true;
        internal int Resets;
        internal NativeHandVisual(SkinnedMeshRenderer source,GameObject owner)
        {
            snapshot=new NativeSkinSnapshot(source);mesh=new Mesh{hideFlags=HideFlags.DontUnloadUnusedAsset};
            ((MeshFilter)owner.AddComponent(typeof(MeshFilter))).sharedMesh=mesh;
        }
        internal Mesh Target()=>EnsureBakeTarget();
        internal bool Usable=>RenderResourcesValid;
        internal void Bake(Matrix4x4[] pose)=>snapshot!.BakePose(Target(),pose);
        internal void DestroyVisible()=>UnityEngine.Object.Destroy(mesh!);
        internal void ResetRenderPose(){Resets++;snapshot?.Invalidate();}
        public void Dispose()=>DisposeSkinResources();
        internal static N.Matrix4x4 N(Matrix4x4 m)=>m.Value;
    }
    internal static class Bootstrap{internal static readonly List<string> Messages=new();internal static void Write(string message)=>Messages.Add(message);}
}
namespace Il2CppInterop.Runtime{static class Il2CppType{public static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
    [Flags] enum HideFlags{None=0,DontUnloadUnusedAsset=32}
    class Object
    {
        internal static readonly List<Object> All=new();internal bool dead;internal string name="";internal HideFlags hideFlags;
        internal Object(){All.Add(this);}
        public static bool operator ==(Object? a,Object? b)=>ReferenceEquals(a,b)||((a is null||a.dead)&&(b is null||b.dead));
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? o)=>ReferenceEquals(this,o);public override int GetHashCode()=>base.GetHashCode();
        internal T? TryCast<T>()where T:class=>this as T;
        internal static void DontDestroyOnLoad(Object o)=>((GameObject)o).persistent=true;
        internal static void Destroy(Object o)
        {
            o.dead=true;if(o is not GameObject go)return;
            foreach(var child in All.OfType<Transform>().Where(x=>x.parent==go.transform).ToArray())Destroy(child.gameObject);
            foreach(var component in go.components)component.dead=true;go.transform.dead=true;
        }
        internal static void UnloadScene()
        {
            foreach(var go in All.OfType<GameObject>().Where(x=>!x.dead&&x.transform.parent==null&&!x.persistent).ToArray())Destroy(go);
        }
        internal static void UnloadUnusedAssets()
        {
            var referenced=All.OfType<SkinnedMeshRenderer>().Where(x=>!x.dead).Select(x=>x.sharedMesh)
                .Concat(All.OfType<MeshFilter>().Where(x=>!x.dead).Select(x=>x.sharedMesh)).ToHashSet();
            foreach(var m in All.OfType<Mesh>().Where(x=>!x.dead&&!referenced.Contains(x)&&(x.hideFlags&HideFlags.DontUnloadUnusedAsset)==0))Destroy(m);
        }
    }
    class GameObject:Object
    {
        internal bool persistent;internal readonly Transform transform;internal readonly List<Component> components=new();
        internal GameObject(string label){name=label;transform=new Transform(this);}
        internal Component AddComponent(Type type){var c=(Component)Activator.CreateInstance(type)!;c.gameObject=this;components.Add(c);return c;}
    }
    class Component:Object{internal GameObject gameObject=null!;internal Transform transform=>gameObject.transform;}
    class Transform:Object
    {
        internal readonly GameObject gameObject;internal Transform? parent;
        internal Vector3 localPosition;internal Quaternion localRotation=Quaternion.identity;internal Vector3 localScale=new(1,1,1);
        internal Transform(GameObject owner){gameObject=owner;}
        internal void SetParent(Transform target,bool world){parent=target;}
        internal Matrix4x4 localToWorldMatrix=>new(N.Matrix4x4.CreateScale(localScale.Value)*N.Matrix4x4.CreateFromQuaternion(localRotation.Value)*N.Matrix4x4.CreateTranslation(localPosition.Value)*(parent?.localToWorldMatrix.Value??N.Matrix4x4.Identity));
        internal Quaternion rotation=>localToWorldMatrix.rotation;
    }
    class Mesh:Object
    {internal Matrix4x4[] bindposes=Array.Empty<Matrix4x4>();internal Vector3[] vertices=Array.Empty<Vector3>();internal int vertexCount=>vertices.Length;}
    class MeshFilter:Component{internal Mesh? sharedMesh;}
    class SkinnedMeshRenderer:Component
    {
        internal Mesh sharedMesh=null!;internal Transform[] bones=Array.Empty<Transform>();internal bool enabled;
        internal Matrix4x4 worldToLocalMatrix=>transform.localToWorldMatrix.inverse;
        internal void BakeMesh(Mesh target,bool useScale)
        {
            if(target==null||sharedMesh==null||bones.Any(x=>x==null))throw new Exception("invalid bake resources");
            target.vertices=sharedMesh.vertices.Select(v=>new Vector3(N.Vector3.Transform(v.Value,(bones[0].localToWorldMatrix*sharedMesh.bindposes[0]).Value))).ToArray();
        }
    }
    readonly struct Vector3
    {
        internal readonly N.Vector3 Value;internal float x=>Value.X;internal float y=>Value.Y;internal float z=>Value.Z;
        internal Vector3(float x,float y,float z){Value=new(x,y,z);}internal Vector3(N.Vector3 v){Value=v;}internal static Vector3 zero=>new(N.Vector3.Zero);
    }
    readonly struct Quaternion
    {
        internal readonly N.Quaternion Value;internal float x=>Value.X;internal float y=>Value.Y;internal float z=>Value.Z;internal float w=>Value.W;
        internal Quaternion(float x,float y,float z,float w){Value=new(x,y,z,w);}internal Quaternion(N.Quaternion q){Value=q;}
        internal static Quaternion identity=>new(N.Quaternion.Identity);internal static Quaternion Inverse(Quaternion q)=>new(N.Quaternion.Inverse(q.Value));
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(a.Value*b.Value);
    }
    readonly struct Matrix4x4
    {
        internal readonly N.Matrix4x4 Value;internal Matrix4x4(N.Matrix4x4 value){Value=value;}
        internal static Matrix4x4 identity=>new(N.Matrix4x4.Identity);internal static Matrix4x4 Scale(Vector3 v)=>new(N.Matrix4x4.CreateScale(v.Value));
        internal Matrix4x4 inverse{get{N.Matrix4x4.Invert(Value,out var v);return new(v);}}
        internal Quaternion rotation{get{N.Matrix4x4.Decompose(Value,out _,out var q,out _);return new(q);}}
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>new(b.Value*a.Value);
        // Unity m_rc (column vectors) = System.Numerics M(c+1)(r+1) (row vectors).
        internal float m00=>Value.M11;internal float m01=>Value.M21;internal float m02=>Value.M31;
        internal float m10=>Value.M12;internal float m11=>Value.M22;internal float m12=>Value.M32;
        internal float m20=>Value.M13;internal float m21=>Value.M23;internal float m22=>Value.M33;
    }
    static class Time{internal static int frameCount=>1;}
}
