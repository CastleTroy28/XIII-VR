using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.215: the bazooka's rocket in the hand that took it from the pouch: the
// game's own rocket (the projectile the bazooka launches: its meshes and
// materials, nothing of its scripts), or a plain one drawn by the mod when
// that cannot be read. Its forward is the rocket's flight direction (+Z).
internal sealed class RocketVisual:IDisposable
{
    private GameObject? root;
    private RigidMeshVisual? plain;
    internal float Length{get;private set;}=.6f;
    // From the middle to the tail along -forward, and to the middle from the root.
    internal Vector3 Middle{get;private set;}
    internal string Source{get;private set;}="";
    // 0.1.226: the radius of its motor tube where the hand holds it (a fifth of its widest, the warhead's).
    internal float TubeRadius{get;private set;}=.024f;
    internal bool Valid=>root!=null;
    internal static RocketVisual Create(GameObject? prefab)
    {
        var v=new RocketVisual();
        try{if(prefab==null||!v.Copy(prefab))v.Plain();}
        catch(Exception ex){Bootstrap.Warn("BAZOOKA rocket mesh: "+ex.Message+" (a plain one drawn)");v.Dispose();v=new RocketVisual();v.Plain();}
        return v;
    }
    // The game's rocket: every mesh renderer under its prefab, placed as in it.
    private bool Copy(GameObject prefab)
    {
        root=new GameObject("XIII bazooka rocket in hand");root.SetActive(false);
        var toRoot=prefab.transform.worldToLocalMatrix;bool any=false;Bounds bounds=default;
        foreach(var component in prefab.GetComponentsInChildren(Il2CppType.Of<MeshRenderer>(),true))
        {
            var renderer=component.TryCast<MeshRenderer>();if(renderer==null)continue;
            var mesh=renderer.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh;if(mesh==null||mesh.vertexCount==0)continue;
            var m=toRoot*renderer.transform.localToWorldMatrix;
            var part=new GameObject("part");part.transform.SetParent(root.transform,false);
            part.transform.localPosition=m.GetColumn(3);part.transform.localRotation=m.rotation;part.transform.localScale=m.lossyScale;
            part.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            var r=part.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;r.sharedMaterials=renderer.sharedMaterials;r.shadowCastingMode=ShadowCastingMode.Off;
            var b=mesh.bounds;
            foreach(var corner in new[]{new Vector3(b.min.x,b.min.y,b.min.z),new Vector3(b.max.x,b.max.y,b.max.z),new Vector3(b.min.x,b.max.y,b.max.z),new Vector3(b.max.x,b.min.y,b.min.z)})
            {var p=m.MultiplyPoint3x4(corner);if(!any){bounds=new Bounds(p,Vector3.zero);any=true;}else bounds.Encapsulate(p);}
        }
        if(!any){UnityEngine.Object.Destroy(root);root=null;return false;}
        Length=Math.Clamp(bounds.size.z,.15f,1.5f);Middle=bounds.center;Source=prefab.name;
        TubeRadius=Math.Clamp(Math.Max(bounds.size.x,bounds.size.y)*.21f,.017f,.032f);
        return true;
    }
    // A plain rocket: an olive tube with a grey warhead and fins, 0.6 m.
    private void Plain()
    {
        root=new GameObject("XIII bazooka rocket in hand (plain)");root.SetActive(false);
        plain=new RigidMeshVisual(root.transform,"XIII bazooka rocket mesh",false);
        var mesh=new HandMeshGeometry(true);
        var body=new System.Numerics.Vector4(.32f,.36f,.22f,1);var head=new System.Numerics.Vector4(.45f,.45f,.47f,1);var fin=new System.Numerics.Vector4(.2f,.22f,.16f,1);
        const int n=12;const float r=.035f,back=-.30f,front=.12f,tip=.30f;
        static System.Numerics.Vector3 P(float radius,float a,float z)=>new(radius*MathF.Cos(a),radius*MathF.Sin(a),z);
        for(int i=0;i<n;i++)
        {
            float a0=i*2*MathF.PI/n,a1=(i+1)*2*MathF.PI/n;
            mesh.Quad(P(r,a0,back),P(r,a1,back),P(r,a1,front),P(r,a0,front),body);
            mesh.Tri(P(r*1.25f,a0,front),P(r*1.25f,a1,front),new(0,0,tip),head);
            mesh.Quad(P(r,a0,front),P(r,a1,front),P(r*1.25f,a1,front),P(r*1.25f,a0,front),head);
            mesh.Tri(new(0,0,back),P(r,a1,back),P(r,a0,back),body);
        }
        for(int k=0;k<4;k++){float a=k*MathF.PI/2;mesh.Quad(P(r,a,back),P(r*2.4f,a,back),P(r*2.4f,a,back+.08f),P(r,a,back+.12f),fin);mesh.Quad(P(r,a,back),P(r*2.4f,a,back),P(r*2.4f,a,back+.08f),P(r,a,back+.12f),fin,true);}
        plain.Set(mesh);plain.Show(true);
        Length=tip-back;Middle=new Vector3(0,0,(tip+back)*.5f);Source="plain";TubeRadius=.032f;
    }
    // The rocket held with its middle at `middle`, pointing along `forward`.
    internal void Pose(Vector3 middle,Quaternion rotation)
    {
        if(root==null)return;
        root.transform.SetPositionAndRotation(middle-rotation*Middle,rotation);
        if(!root.activeSelf)root.SetActive(true);
    }
    // 0.1.217: its root placed directly (held in the fist: WeaponHands.RocketHold).
    internal void PoseRoot(Vector3 position,Quaternion rotation)
    {
        if(root==null)return;
        root.transform.SetPositionAndRotation(position,rotation);
        if(!root.activeSelf)root.SetActive(true);
    }
    // The tail's end in its own frame.
    internal Vector3 Tail=>Middle-Vector3.forward*(Length*.5f);
    internal void Hide(){if(root!=null&&root.activeSelf)root.SetActive(false);}
    public void Dispose()
    {
        plain?.Dispose();plain=null;
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
    }
}
