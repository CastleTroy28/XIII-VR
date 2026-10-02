using System;using System.Collections.Generic;using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponVisual
{
    private Bounds FlattenPropParts(List<Part> parts)
    {
        bool first=true;Bounds bounds=default;
        foreach(var part in parts)
        {
            // A reflected/scaled imported transform cannot safely be replaced
            // by rotation + lossyScale. Bake its full matrix into owned vertices
            // once, so drawing, surface selection and collision share one space.
            var copy=part.Mesh;
            if(!baked.Contains(copy))
            {
                // Skinned snapshots are already owned. A static prop needs a
                // data-only copy; do not duplicate any game object/components.
                copy=new Mesh{indexFormat=part.Mesh.indexFormat,vertices=part.Mesh.vertices,normals=part.Mesh.normals,
                    tangents=part.Mesh.tangents,uv=part.Mesh.uv,uv2=part.Mesh.uv2,colors=part.Mesh.colors};
                baked.Add(copy);copy.subMeshCount=part.Mesh.subMeshCount;
                for(int sub=0;sub<copy.subMeshCount;sub++)copy.SetTriangles(part.Mesh.GetTriangles(sub),sub,false,0);
            }
            copy.hideFlags=HideFlags.DontUnloadUnusedAsset;
            var matrix=part.Matrix;var normal=matrix.inverse.transpose;
            var vertices=copy.vertices;var normals=copy.normals;var tangents=copy.tangents;
            for(int i=0;i<vertices.Length;i++)
            {
                var point=matrix.MultiplyPoint3x4(vertices[i]);
                if(!Finite(point))throw new InvalidOperationException("Nonfinite held prop surface");
                vertices[i]=point;
                if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
                if(normals.Length==vertices.Length)normals[i]=normal.MultiplyVector(normals[i]).normalized;
                if(tangents.Length==vertices.Length)
                {
                    var t=tangents[i];var direction=matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;
                    tangents[i]=new Vector4(direction.x,direction.y,direction.z,t.w*(matrix.determinant<0?-1:1));
                }
            }
            copy.vertices=vertices;if(normals.Length==vertices.Length)copy.normals=normals;
            if(tangents.Length==vertices.Length)copy.tangents=tangents;
            if(matrix.determinant<0)for(int sub=0;sub<copy.subMeshCount;sub++)
            {
                var indices=copy.GetTriangles(sub);
                for(int i=0;i+2<indices.Length;i+=3)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);
                copy.SetTriangles(indices,sub,false,0);
            }
            copy.RecalculateBounds();part.Mesh=copy;part.BakedSourceMatrix=matrix;part.Matrix=Matrix4x4.identity;
        }
        if(first)throw new InvalidOperationException("Held prop has no readable rendered vertices");
        Bootstrap.Write("PROP SURFACE canonical vertices bounds="+bounds.center.ToString("F6")+" size="+bounds.size.ToString("F6"));
        return bounds;
    }
    private static void PropMeshData(List<Part> parts,Matrix4x4 fit,out System.Numerics.Vector3[] vertices,out int[] triangles)
    {
        var v=new List<System.Numerics.Vector3>();var t=new List<int>();
        foreach(var part in parts)
        {
            var map=fit*part.Matrix;int start=v.Count;
            foreach(var p in part.Mesh.vertices){var point=map.MultiplyPoint3x4(p);v.Add(new System.Numerics.Vector3(point.x,point.y,point.z));}
            foreach(int i in part.Mesh.triangles)t.Add(start+i);
        }
        vertices=v.ToArray();triangles=t.ToArray();
    }
}
