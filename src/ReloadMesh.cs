using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// Owned, rigid copy of the game's magazine/shell; no collider or native
// weapon component is instantiated, and no borrowed mesh is modified.
internal sealed class ReloadMesh:IDisposable
{
    private readonly GameObject root;
    private readonly Mesh mesh;
    internal readonly Vector3 Center;
    internal readonly Vector3 Min,Max;
    private readonly float maxSize;
    internal System.Numerics.Vector3[] GripPoints{get;private set;}=Array.Empty<System.Numerics.Vector3>();
    internal ReloadMesh(Mesh source,Material[] materials,Matrix4x4 frame,bool[] selected,float maxSize=.55f)
    {
        this.maxSize=maxSize;root=new GameObject("XIII reload ammunition");root.SetActive(false);mesh=new Mesh();
        try
        {
            var vertices=source.vertices;var normals=source.normals;var uv=source.uv;
            var points=new List<Vector3>();var ns=new List<Vector3>();var coords=new List<Vector2>();
            var sourceColors=source.colors;var colors=new List<Color>();
            var sourceTangents=source.tangents;var tangents=new List<Vector4>();
            bool mirrored=frame.determinant<0;
            int[] remap=new int[vertices.Length];Array.Fill(remap,-1);
            var normalFrame=frame.inverse.transpose;
            Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=-min;
            for(int i=0;i<vertices.Length;i++)if(selected[i]){var p=frame.MultiplyPoint3x4(vertices[i]);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            if(!float.IsFinite(min.sqrMagnitude)||(max-min).magnitude>maxSize||(max-min).magnitude<.015f)throw new InvalidOperationException("Native magazine/shell bounds unavailable");
            Center=(min+max)*.5f;Min=min-Center;Max=max-Center;
            for(int i=0;i<vertices.Length;i++)if(selected[i])
            {
                remap[i]=points.Count;points.Add(frame.MultiplyPoint3x4(vertices[i])-Center);
                ns.Add(normals.Length==vertices.Length?normalFrame.MultiplyVector(normals[i]).normalized:Vector3.up);
                coords.Add(uv.Length==vertices.Length?uv[i]:Vector2.zero);
                colors.Add(sourceColors.Length==vertices.Length?sourceColors[i]:Color.white);
                if(sourceTangents.Length==vertices.Length){var t=sourceTangents[i];var v=frame.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;tangents.Add(new Vector4(v.x,v.y,v.z,mirrored?-t.w:t.w));}
            }
            if(points.Count<6)throw new InvalidOperationException("Native ammunition geometry missing");
            GripPoints=new System.Numerics.Vector3[points.Count];
            for(int i=0;i<points.Count;i++)GripPoints[i]=new System.Numerics.Vector3(points[i].x,points[i].y,points[i].z);
            mesh.vertices=points.ToArray();mesh.normals=ns.ToArray();mesh.uv=coords.ToArray();mesh.colors=colors.ToArray();
            if(tangents.Count==points.Count)mesh.tangents=tangents.ToArray();mesh.subMeshCount=source.subMeshCount;
            int faces=0;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                int[] triangles=source.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i+2<triangles.Length;i+=3)if(selected[triangles[i]]&&selected[triangles[i+1]]&&selected[triangles[i+2]])
                {kept.Add(remap[triangles[i]]);kept.Add(remap[triangles[i+(mirrored?2:1)]]);kept.Add(remap[triangles[i+(mirrored?1:2)]]);}
                faces+=kept.Count;mesh.SetTriangles(kept.ToArray(),sub,false,0);
            }
            if(faces<6)throw new InvalidOperationException("Native magazine/shell triangles unavailable");mesh.RecalculateBounds();
            root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            var renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        catch{Dispose();throw;}
    }
    // 0.1.122: the rigid ammunition geometry (relative to Center), for the outline hint.
    internal Mesh Shape=>mesh;
    internal ReloadMesh Copy()
    {
        var flags=new bool[mesh.vertexCount];Array.Fill(flags,true);
        return new ReloadMesh(mesh,root.GetComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!.sharedMaterials,Matrix4x4.identity,flags,maxSize);
    }
    // 0.1.183: kept across levels (a still copy's magazine to drop).
    internal ReloadMesh Keep(){UnityEngine.Object.DontDestroyOnLoad(root);mesh.hideFlags=HideFlags.DontUnloadUnusedAsset;return this;}
    internal void Pose(Vector3 position,Quaternion rotation){root.transform.SetPositionAndRotation(position,rotation);root.SetActive(true);}
    internal void Hide()=>root.SetActive(false);
    public void Dispose(){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(mesh);}
}
