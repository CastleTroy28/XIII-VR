using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime;
namespace XiiiXR;
// Static batching bakes vertices into the batch's coordinate space. Extract
// only this renderer's submeshes before moving it, preserving its materials.
internal sealed class MovablePropMesh:IDisposable
{
    private readonly List<(MeshRenderer renderer,MeshFilter filter,Mesh original,Mesh owned,int first,int count,int lightmap,bool wasStatic)> saved=new();
    private readonly HashSet<int> failed=new();
    internal bool Prepare(Renderer renderer)
    {
        if(!renderer.isPartOfStaticBatch)return true;
        var r=renderer.TryCast<MeshRenderer>();if(r==null||failed.Contains(r.GetInstanceID()))return false;
        Mesh? mesh=null;
        try
        {
            var f=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>();var original=f?.sharedMesh;
            if(f==null||original==null||!original.isReadable)return false;
            int first=r.subMeshStartIndex,count=r.sharedMaterials.Length;
            if(count<1||first+count>original.subMeshCount)return false;
            var source=original.vertices;var uv=original.uv;var normals=original.normals;var uv2=original.uv2;
            var map=new Dictionary<int,int>();var vertices=new List<Vector3>();var tex=new List<Vector2>();var tex2=new List<Vector2>();var ns=new List<Vector3>();var tris=new List<int[]>();
            var matrix=r.transform.worldToLocalMatrix*r.localToWorldMatrix;
            for(int part=0;part<count;part++)
            {
                var indices=original.GetIndices(first+part);var result=new int[indices.Length];
                for(int j=0;j<indices.Length;j++)
                {
                    int old=indices[j];
                    if(!map.TryGetValue(old,out int index))
                    {
                        index=vertices.Count;map.Add(old,index);vertices.Add(matrix.MultiplyPoint3x4(source[old]));
                        tex.Add(old<uv.Length?uv[old]:Vector2.zero);tex2.Add(old<uv2.Length?uv2[old]:Vector2.zero);
                        ns.Add(old<normals.Length?matrix.MultiplyVector(normals[old]).normalized:Vector3.up);
                    }
                    result[j]=index;
                }
                tris.Add(result);
            }
            if(vertices.Count==0||vertices.Count>65535)return false;
            mesh=new Mesh();mesh.name="XIII movable "+r.name;mesh.vertices=vertices.ToArray();mesh.uv=tex.ToArray();mesh.uv2=tex2.ToArray();mesh.normals=ns.ToArray();mesh.subMeshCount=count;
            for(int i=0;i<count;i++)mesh.SetTriangles(tris[i],i,true,0);mesh.RecalculateBounds();
            saved.Add((r,f,original,mesh,first,count,r.lightmapIndex,r.gameObject.isStatic));
            r.SetStaticBatchInfo(0,0);f.sharedMesh=mesh;r.lightmapIndex=-1;r.gameObject.isStatic=false;
            return true;
        }
        catch(Exception ex)
        {if(mesh!=null)UnityEngine.Object.Destroy(mesh);failed.Add(r.GetInstanceID());Bootstrap.Warn("WORLD PROP mesh unavailable="+r.name+": "+ex.Message);return false;}
    }
    public void Dispose()
    {
        foreach(var s in saved)
        {
            if(s.renderer!=null&&s.filter!=null){s.filter.sharedMesh=s.original;s.renderer.SetStaticBatchInfo(s.first,s.count);s.renderer.lightmapIndex=s.lightmap;s.renderer.gameObject.isStatic=s.wasStatic;}
            if(s.owned!=null)UnityEngine.Object.Destroy(s.owned);
        }
        saved.Clear();failed.Clear();
    }
}
