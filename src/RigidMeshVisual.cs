using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
internal sealed class RigidMeshVisual : IDisposable
{
    private GameObject? root;
    private Mesh? mesh;
    private MeshRenderer? renderer;
    private Material? basis;
    private readonly List<Material> materials=new();
    private readonly bool luminous,overlay;
    internal RigidMeshVisual(Transform parent,string name,bool display=false,bool overlay=false)
    {
        luminous=display;this.overlay=overlay;
        try
        {
            EnsureBasis();
            root=new GameObject(name);root.layer=parent.gameObject.layer;root.transform.SetParent(parent,false);
            root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name=name;mesh.MarkDynamic();
            root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=!display;
        }
        catch{Dispose();throw;}
    }
    private void EnsureBasis()
    {
        if(basis!=null)return;
        var shader=overlay?Shader.Find("UI/Default"):(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse"));
        if(shader==null)throw new InvalidOperationException("No opaque material for owned VR mesh");
        basis=new Material(shader);basis.color=Color.white;basis.mainTexture=Texture2D.whiteTexture;
        if(overlay){basis.SetInt("unity_GUIZTestMode",(int)CompareFunction.Always);basis.renderQueue=5000;}
        if(luminous){if(basis.HasProperty("_SpecularHighlights"))basis.SetFloat("_SpecularHighlights",0);if(basis.HasProperty("_GlossyReflections"))basis.SetFloat("_GlossyReflections",0);}
        if(basis.HasProperty("_Glossiness"))basis.SetFloat("_Glossiness",luminous?0:.18f);
        basis.hideFlags=HideFlags.DontUnloadUnusedAsset;
    }
    internal void Set(HandMeshGeometry data)
    {
        if(mesh==null || renderer==null)return;
        EnsureBasis();
        var vertices=new Vector3[data.Vertices.Count];var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++){var p=data.Vertices[i];vertices[i]=new Vector3(p.X,p.Y,p.Z);uv[i]=new Vector2(.5f,.5f);}
        mesh.Clear();mesh.vertices=vertices;mesh.uv=uv;
        // UI/Default multiplies vertex color with material tint. Supply white
        // explicitly for the opaque cinematic surround.
        var colors=new Color[vertices.Length];Array.Fill(colors,Color.white);mesh.colors=colors;mesh.subMeshCount=data.Palette.Count;
        while(materials.Count<data.Palette.Count)materials.Add(new Material(basis!){hideFlags=HideFlags.DontUnloadUnusedAsset});
        var selected=new Material[data.Palette.Count];
        for(int slot=0;slot<data.Palette.Count;slot++)
        {
            var c=data.Palette[slot];var color=new Color(c.X,c.Y,c.Z,c.W);var mat=materials[slot];if(mat==null){mat=new Material(basis!){hideFlags=HideFlags.DontUnloadUnusedAsset};materials[slot]=mat;}mat.color=color;
            if(luminous && mat.HasProperty("_EmissionColor")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.8f);}
            selected[slot]=mat;
            var indices=new List<int>();
            for(int t=0;t<data.MaterialSlots.Count;t++)if(data.MaterialSlots[t]==slot)for(int j=0;j<3;j++)indices.Add(data.Triangles[t*3+j]);
            mesh.SetTriangles(indices.ToArray(),slot,true,0);
        }
        renderer.sharedMaterials=selected;mesh.RecalculateNormals();mesh.RecalculateBounds();
    }
    internal void Show(bool visible){if(root!=null)root.SetActive(visible);}
    // 0.1.122: the pouch's own mesh and its placement, for the outline hint.
    internal Mesh? Shape=>mesh;
    internal Transform? Frame=>root!=null?root.transform:null;
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
        if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;
        foreach(var m in materials)if(m!=null)UnityEngine.Object.Destroy(m);materials.Clear();
        if(basis!=null)UnityEngine.Object.Destroy(basis);basis=null;renderer=null;
    }
}
