using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal static class HeldItemMeshSources
{
    internal static List<Renderer> Find(Equipable source)
    {
        var candidates=new List<Renderer>();
        // Native inventory migration may attach an item's skin outside its
        // Equipable hierarchy. Its explicit renderer links remain authoritative.
        if(source.skinnedMeshRenderers!=null)foreach(var skin in source.skinnedMeshRenderers)if(skin!=null)candidates.Add(skin);
        foreach(var c in source.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {var r=c.TryCast<Renderer>();if(r!=null)candidates.Add(r);}
        return Select(candidates);
    }
    // Called only when drawing an item, on its pickup prefab or local FPS rig.
    // A detached key renderer must match the full model ID, never just "key".
    internal static List<Renderer> FindIn(Transform root,string? identifier=null)
    {
        var candidates=new List<Renderer>();int count=0;
        string? expected=identifier==null?null:ModelName(identifier);
        if(expected!=null&&expected.Length==0)return candidates;
        foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            if(++count>256)break;
            var r=c.TryCast<Renderer>();if(r==null)continue;
            if(expected!=null&&ModelName(r.name)!=expected&&ModelName(MeshOf(r)?.name??"")!=expected)continue;
            candidates.Add(r);
        }
        return Select(candidates);
    }
    private static Mesh? MeshOf(Renderer r)
    {
        var skin=r.TryCast<SkinnedMeshRenderer>();
        return skin!=null?skin.sharedMesh:r.TryCast<MeshRenderer>()!=null?r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh:null;
    }
    private static string ModelName(string value)
    {
        string n=value.Trim().ToLowerInvariant();
        if(n.EndsWith("(clone)",StringComparison.Ordinal))n=n.Substring(0,n.Length-7).TrimEnd();
        if(n.StartsWith("eqp_",StringComparison.Ordinal))n=n.Substring(4);
        int lod=n.LastIndexOf("_lod",StringComparison.Ordinal);
        if(lod>=0&&lod+5==n.Length&&char.IsDigit(n[n.Length-1]))n=n.Substring(0,lod);
        return n;
    }
    private static List<Renderer> Select(List<Renderer> candidates)
    {
        int bestLod=int.MaxValue;
        var sources=new List<Renderer>();var seen=new HashSet<int>();
        void Add(Renderer? r)
        {
            if(r==null||!seen.Add(r.GetInstanceID()))return;
            var mesh=MeshOf(r);
            // Empty LOD0 renderers must not hide a usable lower-detail model.
            if(mesh==null||mesh.vertexCount==0||mesh.vertexCount>200000)return;
            string n=r.name.ToLowerInvariant();
            if(n.Contains("flash")||n.Contains("vfx")||n.Contains("arm")&&!n.Contains("medkit"))return;
            sources.Add(r);bestLod=Math.Min(bestLod,Lod(n));
        }
        foreach(var r in candidates)Add(r);
        sources.RemoveAll(r=>Lod(r.name.ToLowerInvariant())!=bestLod);
        return sources;
    }
    private static int Lod(string n)
    {int i=n.IndexOf("lod",StringComparison.Ordinal);if(i<0)return 0;i+=3;while(i<n.Length&&(n[i]=='_'||n[i]==' '))i++;return i<n.Length&&char.IsDigit(n[i])?n[i]-'0':0;}
}
