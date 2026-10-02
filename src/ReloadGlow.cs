using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.122: white, only where no part outline exists (ReloadOutline).
// 0.1.120: a faint, slowly pulsing glow around the part of the gun the
// player has to take next during a hand reload (magazine, belt pouch, well,
// bolt, M60 cover). An unlit, transparent sphere; no collider, no shadows.
internal sealed class ReloadGlow:IDisposable
{
    internal const float MinAlpha=.10f,MaxAlpha=.55f,Hertz=1.25f;
    private GameObject? root;private Material? material;private Mesh? mesh;
    internal ReloadGlow()
    {
        root=new GameObject("XIII reload glow");root.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(root);
        mesh=Sphere(12,8);
        var shader=Shader.Find("Sprites/Default")??Shader.Find("UI/Default")??throw new InvalidOperationException("no transparent shader");
        material=new Material(shader){renderQueue=3100};material.color=new Color(1,1,1,MinAlpha);
        root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
        var r=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
        r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
    }
    internal static float Alpha(float time)=>MinAlpha+(MaxAlpha-MinAlpha)*(.5f+.5f*MathF.Sin(time*Hertz*2*MathF.PI));
    internal void Show(Vector3 world,float radius)
    {
        if(root==null||material==null)return;
        root.transform.SetPositionAndRotation(world,Quaternion.identity);root.transform.localScale=Vector3.one*(radius*2);
        var c=material.color;c.a=Alpha(Time.realtimeSinceStartup);material.color=c;
        if(!root.activeSelf)root.SetActive(true);
    }
    internal void Hide(){if(root!=null&&root.activeSelf)root.SetActive(false);}
    private static Mesh Sphere(int around,int rings)
    {
        var v=new Vector3[(around+1)*(rings+1)];int n=0;
        for(int r=0;r<=rings;r++){float a=MathF.PI*r/rings;for(int s=0;s<=around;s++){float b=2*MathF.PI*s/around;v[n++]=new Vector3(MathF.Sin(a)*MathF.Cos(b),MathF.Cos(a),MathF.Sin(a)*MathF.Sin(b))*.5f;}}
        var t=new int[around*rings*6];int k=0;
        for(int r=0;r<rings;r++)for(int s=0;s<around;s++)
        {int i=r*(around+1)+s,j=i+around+1;t[k++]=i;t[k++]=j;t[k++]=i+1;t[k++]=i+1;t[k++]=j;t[k++]=j+1;}
        var m=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};m.name="XIII reload glow";m.vertices=v;m.triangles=t;
        var colors=new Color[v.Length];Array.Fill(colors,Color.white);m.colors=colors;m.RecalculateBounds();return m;
    }
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
        if(material!=null)UnityEngine.Object.Destroy(material);material=null;
        if(mesh!=null)UnityEngine.Object.Destroy(mesh);mesh=null;
    }
}
