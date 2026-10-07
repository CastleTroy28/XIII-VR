using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
namespace XiiiXR;
// 0.1.195. The Uzi's rig has
// one moving bone ("cover", 14 vertices: a small part in the handle's slot);
// the handle itself is not rigged. Its mesh pieces are found around that
// part (KnobMath) and drawn back with it, both from the pose they have. The
// gun's mesh is also written once to the BepInEx config folder (for checking).
internal sealed partial class WeaponVisual
{
    private static readonly HashSet<string> dumpedMeshes=new();
    private void PrepareKnob(Part part)
    {
        var s=part.Snapshot;var m=part.Mechanism;if(s==null||m==null)return;
        m.LiveRelative=true;
        try
        {
            var bones=s.Bones;var moving=new bool[bones.Length];foreach(int i in m.MovingBones)if(i>=0&&i<moving.Length)moving[i]=true;
            var own=new bool[bones.Length];
            for(int i=0;i<bones.Length;i++)if(bones[i]!=null){string n=bones[i].name.ToLowerInvariant();own[i]=moving[i]||n=="wpn_"+Profile+"_bnd_jnt";}
            var weights=s.Original.boneWeights;var vertices=part.Mesh.vertices;var triangles=part.Mesh.triangles;
            if(weights.Length!=vertices.Length){Bootstrap.Warn("UZI HANDLE weights unavailable");return;}
            int Dominant(BoneWeight w)=>w.weight0>=w.weight1&&w.weight0>=w.weight2&&w.weight0>=w.weight3?w.boneIndex0:w.weight1>=w.weight2&&w.weight1>=w.weight3?w.boneIndex1:w.weight2>=w.weight3?w.boneIndex2:w.boneIndex3;
            var map=fitMatrix*part.Matrix;var points=new System.Numerics.Vector3[vertices.Length];
            var seed=new bool[vertices.Length];var excluded=new bool[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var p=map.MultiplyPoint3x4(vertices[i]);points[i]=new System.Numerics.Vector3(p.x,p.y,p.z);
                int b=Dominant(weights[i]);bool inside=b>=0&&b<own.Length;
                seed[i]=inside&&moving[b];excluded[i]=!inside||!own[b];
            }
            // 0.1.198: the knob is rigged (the "aim" bone, moved by the mechanism): nothing extra.
            m.ExtraVertices=null;
            int knob=0;foreach(bool x in seed)if(x)knob++;
            Bootstrap.Write("UZI HANDLE the top knob is the gun's own moving part: "+knob+" vertices drawn back with it");
            DumpMesh(part,points,weights,triangles);
        }
        catch(Exception ex){Bootstrap.Warn("UZI HANDLE: "+ex.Message);}
    }
    // 0.1.251: the crossbows' meshes too (each of the three once a game), for checking their sights and bands.
    private void DumpModel(Part part)
    {
        if(part.Snapshot==null||dumpedMeshes.Contains(ModelKey))return;
        try
        {
            var weights=part.Snapshot.Original.boneWeights;var vertices=part.Mesh.vertices;
            if(weights.Length!=vertices.Length)return;
            var map=fitMatrix*part.Matrix;var points=new System.Numerics.Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++){var p=map.MultiplyPoint3x4(vertices[i]);points[i]=new System.Numerics.Vector3(p.x,p.y,p.z);}
            DumpMesh(part,points,weights,part.Mesh.triangles);
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON MESH dump: "+ex.Message);}
    }
    // The gun's mesh (fitted frame, each vertex's main bone) for offline checks.
    private void DumpMesh(Part part,System.Numerics.Vector3[] points,BoneWeight[] weights,int[] triangles)
    {
        if(!dumpedMeshes.Add(ModelKey.Length>0?ModelKey:Profile))return;
        try
        {
            var names=new string[part.Snapshot!.Bones.Length];for(int i=0;i<names.Length;i++)names[i]=part.Snapshot.Bones[i]?.name??"";
            var sb=new StringBuilder();var inv=CultureInfo.InvariantCulture;
            sb.Append("# XIII VR mesh ").Append(part.Source.name).Append(" fitted frame; v x y z bone weight; t a b c\n");
            for(int i=0;i<names.Length;i++)sb.Append("b ").Append(i).Append(' ').Append(names[i]).Append('\n');
            for(int i=0;i<points.Length;i++)
            {
                var w=weights[i];sb.Append("v ").Append(points[i].X.ToString("F5",inv)).Append(' ').Append(points[i].Y.ToString("F5",inv)).Append(' ').Append(points[i].Z.ToString("F5",inv))
                    .Append(' ').Append(w.boneIndex0).Append(' ').Append(w.weight0.ToString("F2",inv)).Append('\n');
            }
            for(int t=0;t+2<triangles.Length;t+=3)sb.Append("t ").Append(triangles[t]).Append(' ').Append(triangles[t+1]).Append(' ').Append(triangles[t+2]).Append('\n');
            var file=Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-mesh-"+(ModelKey.Length>0?ModelKey:Profile)+".txt");File.WriteAllText(file,sb.ToString());
            Bootstrap.Write("WEAPON MESH written: "+file);
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON MESH dump: "+ex.Message);}
    }
}
