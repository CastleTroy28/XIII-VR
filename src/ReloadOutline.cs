using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.122: the blinking white outline around the part of the gun (or the
// belt pouch) the player has to take next. The part's own mesh, a little
// larger and turned inside out (OutlineShell), drawn in an unlit white that
// pulses; no collider, no shadows.
internal sealed class ReloadOutline:IDisposable
{
    internal const float Thickness=.0025f;
    private GameObject? root;private MeshFilter? filter;private Material? material;
    private readonly Dictionary<int,(int vertices,Mesh shell)> shells=new();
    // 0.1.123: the rim was black in the game: the Standard shader's emission
    // (black albedo + white emission) is not in the game's build. Now an unlit
    // colour shader draws it white: Hidden/Internal-Colored (Unity's own, in
    // every build; culling set here), else Unlit/Color, else Standard with a
    // white albedo as well as the emission.
    private string shaderName="";private bool lit;
    internal ReloadOutline()
    {
        root=new GameObject("XIII reload outline");root.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(root);
        filter=root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!;
        var renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
        material=CreateMaterial(out shaderName,out lit);
        renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        Bootstrap.Write("RELOAD OUTLINE white rim shader="+shaderName+(lit?" (lit fallback)":""));
    }
    private static Material CreateMaterial(out string used,out bool lit)
    {
        lit=false;
        var shader=Shader.Find("Hidden/Internal-Colored");
        if(shader!=null)
        {
            var m=new Material(shader){hideFlags=HideFlags.DontUnloadUnusedAsset};used=shader.name;
            // The shell is turned inside out: cull its (now) back faces.
            m.SetInt("_Cull",(int)CullMode.Back);m.SetInt("_ZWrite",1);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);
            m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.Zero);
            m.renderQueue=2450;m.color=Color.white;return m;
        }
        shader=Shader.Find("Unlit/Color");
        if(shader!=null){used=shader.name;return new Material(shader){hideFlags=HideFlags.DontUnloadUnusedAsset,color=Color.white};}
        shader=Shader.Find("Standard")??throw new InvalidOperationException("no outline shader");
        var s=new Material(shader){hideFlags=HideFlags.DontUnloadUnusedAsset};used=shader.name;lit=true;s.color=Color.white;
        if(s.HasProperty("_Glossiness"))s.SetFloat("_Glossiness",0);
        if(s.HasProperty("_Metallic"))s.SetFloat("_Metallic",0);
        s.EnableKeyword("_EMISSION");
        return s;
    }
    // meshToWorld: from the source mesh's own coordinates to the world.
    internal bool Show(Mesh? source,Matrix4x4 meshToWorld)
    {
        if(root==null||filter==null||material==null||source==null)return false;
        // 0.1.152: a mirrored place (the left-hander's belt, scaled -1)
        // cannot be taken apart into a turn and a positive size - its turn came
        // out wrong. Un-mirror it first, then mirror the outline back.
        bool mirrored=meshToWorld.determinant<0;
        var m=mirrored?meshToWorld*Matrix4x4.Scale(new Vector3(-1,1,1)):meshToWorld;
        var scale=m.lossyScale;float s=(Math.Abs(scale.x)+Math.Abs(scale.y)+Math.Abs(scale.z))/3;
        if(!(s>1e-5f)||!float.IsFinite(s))return false;
        var shell=Shell(source,Thickness/s);if(shell==null)return false;
        if(filter.sharedMesh!=shell)filter.sharedMesh=shell;
        root.transform.SetPositionAndRotation(m.GetColumn(3),m.rotation);root.transform.localScale=mirrored?new Vector3(-scale.x,scale.y,scale.z):scale;
        float b=OutlineShell.Blink(Time.realtimeSinceStartup);
        if(lit)material.SetColor("_EmissionColor",Color.white*(1.3f*b));
        else material.color=new Color(b,b,b,1);
        if(!root.activeSelf)root.SetActive(true);
        return true;
    }
    internal void Hide(){if(root!=null&&root.activeSelf)root.SetActive(false);}
    private Mesh? Shell(Mesh source,float thickness)
    {
        int id=source.GetInstanceID();int count=source.vertexCount;
        if(shells.TryGetValue(id,out var cached)&&cached.vertices==count&&cached.shell!=null)return cached.shell;
        var vertices=source.vertices;var normals=source.normals;var triangles=source.triangles;
        var points=new N[vertices.Length];var ns=new N[normals.Length==vertices.Length?vertices.Length:0];
        for(int i=0;i<vertices.Length;i++)points[i]=new N(vertices[i].x,vertices[i].y,vertices[i].z);
        for(int i=0;i<ns.Length;i++)ns[i]=new N(normals[i].x,normals[i].y,normals[i].z);
        var (outer,faces)=OutlineShell.Build(points,ns.Length>0?ns:null,triangles,thickness);
        if(faces.Length<3)return null;
        var shell=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};shell.name="XIII outline of "+source.name;
        if(outer.Length>65000)shell.indexFormat=IndexFormat.UInt32;
        var v=new Vector3[outer.Length];for(int i=0;i<v.Length;i++)v[i]=new Vector3(outer[i].X,outer[i].Y,outer[i].Z);
        shell.vertices=v;shell.triangles=faces;shell.RecalculateNormals();shell.RecalculateBounds();
        if(shells.TryGetValue(id,out var old)&&old.shell!=null)UnityEngine.Object.Destroy(old.shell);
        shells[id]=(count,shell);
        return shell;
    }
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;filter=null;
        if(material!=null)UnityEngine.Object.Destroy(material);material=null;
        foreach(var s in shells.Values)if(s.shell!=null)UnityEngine.Object.Destroy(s.shell);shells.Clear();
    }
}
