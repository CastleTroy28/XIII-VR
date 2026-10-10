using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.255: a model in flat tones (ToneMesh: one material a tone, its own normals), lit as the game's things:
// the ammunition belt. A child of the given root; built again when its shape changes.
internal sealed class ToneMeshVisual : IDisposable
{
    private GameObject? root;
    private Mesh? mesh;
    private MeshRenderer? renderer;
    private Material[]? materials;
    internal ToneMeshVisual(Transform parent,string name)
    {
        try
        {
            root=new GameObject(name);root.layer=parent.gameObject.layer;root.transform.SetParent(parent,false);
            root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name=name;mesh.indexFormat=IndexFormat.UInt32;mesh.MarkDynamic();
            root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        }
        catch{Dispose();throw;}
    }
    private static Shader Opaque()=>Shader.Find("Standard")??Shader.Find("Legacy Shaders/Diffuse")??throw new InvalidOperationException("No opaque material for the mod's model");
    internal void Set(ToneMesh shape)
    {
        if(mesh==null||renderer==null)return;
        var vertices=new Vector3[shape.Points.Length];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++){var p=shape.Points[i];var n=shape.Normals[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);normals[i]=new Vector3(n.X,n.Y,n.Z);uv[i]=new Vector2(.5f,.5f);}
        mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=shape.Tones.Length;
        if(materials==null||materials.Length!=shape.Palette.Length)
        {
            if(materials!=null)foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);
            materials=new Material[shape.Palette.Length];
        }
        for(int t=0;t<shape.Tones.Length;t++)
        {
            var c=shape.Palette[t];
            if(materials[t]==null)
            {
                var m=new Material(Opaque()){hideFlags=HideFlags.DontUnloadUnusedAsset};m.mainTexture=Texture2D.whiteTexture;
                if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.15f);
                materials[t]=m;
            }
            materials[t].color=new Color(c.X,c.Y,c.Z,1);
            mesh.SetTriangles(shape.Tones[t],t,false,0);
        }
        mesh.RecalculateBounds();renderer.sharedMaterials=materials;
    }
    internal Mesh? Shape=>mesh;
    internal Transform? Frame=>root!=null?root.transform:null;
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
        if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;
        if(materials!=null)foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);materials=null;renderer=null;
    }
}
