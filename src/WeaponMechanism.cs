using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// Visual-only fallback for explicitly named mechanical bones. No guessed vertex
// region and no writes to the game's skeleton, animator triggers or fire state.
internal sealed class WeaponMechanism
{
    private readonly List<int> vertices=new();
    private readonly List<float> weights=new();
    private readonly List<(Transform bone,Vector3 p,Quaternion q)> bones=new();
    private readonly string profile;
    private float lastNativeMotion=float.NegativeInfinity;
    private bool failed;
    private int rootIndex=-1;
    private readonly List<int> moving=new();
    private Matrix4x4[]? ownedPose;
    // 0.1.119: M60 top cover — its bones (and their children) turned about the
    // hinge, as a mesh-space transform set by WeaponVisual each frame.
    internal int[]? CoverBones;internal Matrix4x4 CoverPose=Matrix4x4.identity;internal bool CoverActive;
    // 0.1.195: its moving part keeps the pose it has (only drawn back),
    // and the handle's own mesh pieces (not rigged) are drawn back with it.
    internal bool LiveRelative;internal int[]? ExtraVertices;
    internal IReadOnlyList<int> MovingBones=>moving;
    internal WeaponMechanism(SkinnedMeshRenderer source,string kind)
    {
        profile=kind;
        try
        {
            var sourceBones=source.bones; var names=new List<string>();var matched=new HashSet<int>();
            for(int i=0;i<sourceBones.Length;i++)
            {
                var bone=sourceBones[i]; if(bone==null) continue;
                names.Add(bone.name);
                if(bone.name.Equals("wpn_"+profile+"_BND_JNT",StringComparison.OrdinalIgnoreCase))rootIndex=i;
                if(MechanismMath.Matches(bone.name,profile))
                { matched.Add(i); bones.Add((bone,bone.localPosition,bone.localRotation)); }
            }
            Bootstrap.Write("WEAPON BONES "+profile+" "+string.Join(",",names));
            if(matched.Count==0) { failed=true; Bootstrap.Warn("WEAPON MECHANISM no named slide/bolt/pump binding for "+profile); return; }
            for(int i=0;i<sourceBones.Length;i++)
                foreach(int joint in matched)if(i==joint||sourceBones[i].IsChildOf(sourceBones[joint])){moving.Add(i);break;}
            var data=source.sharedMesh.boneWeights;
            if(data.Length!=source.sharedMesh.vertexCount) throw new InvalidOperationException("Bone weights unavailable");
            for(int i=0;i<data.Length;i++)
            {
                var w=data[i];float weight=0;
                if(matched.Contains(w.boneIndex0)) weight+=w.weight0;
                if(matched.Contains(w.boneIndex1)) weight+=w.weight1;
                if(matched.Contains(w.boneIndex2)) weight+=w.weight2;
                if(matched.Contains(w.boneIndex3)) weight+=w.weight3;
                if(weight>.001f) { vertices.Add(i); weights.Add(Mathf.Clamp01(weight)); }
            }
            Bootstrap.Write("WEAPON MECHANISM bound="+profile+" joints="+matched.Count+" vertices="+vertices.Count);
        }
        catch(Exception ex) { failed=true; Bootstrap.Warn("WEAPON MECHANISM native-animation only: "+ex.Message); }
    }
    // Mechanical joints use bind-relative CLOSED transforms. Adding an offset
    // to the live empty-magazine animation left the pistol slide open twice.
    internal Matrix4x4 GripCorrection(NativeSkinSnapshot snapshot)
    {
        if(failed||rootIndex<0||moving.Count==0)return Matrix4x4.identity;
        snapshot.Sample();
        return NativeHandVisual.U(GripSpaceMath.FrozenRoot(NativeHandVisual.N((snapshot.Initial??snapshot.Live)[rootIndex]),NativeHandVisual.N(snapshot.Live[rootIndex])));
    }
    internal bool BakeOwned(NativeSkinSnapshot snapshot,Mesh mesh,Matrix4x4 partToBarrel,float scale,float travel,bool stable=false)
    {
        if(failed||rootIndex<0||moving.Count==0)return false;
        snapshot.Sample();ownedPose??=new Matrix4x4[snapshot.Live.Length];
        Array.Copy(snapshot.Live,ownedPose,ownedPose.Length);
        var rootFrame=stable?(snapshot.Initial??snapshot.Live)[rootIndex]*snapshot.Bind[rootIndex]:snapshot.Live[rootIndex]*snapshot.Bind[rootIndex];
        if(stable)
        {
            // Cancel root motion, retaining live child transforms and scales.
            // Restoring every bone from bind pose resurrects hidden attachments
            // (notably the pistol suppressor and parked reload ammunition).
            var liveRoot=snapshot.Live[rootIndex]*snapshot.Bind[rootIndex];
            var correction=rootFrame*liveRoot.inverse;
            for(int i=0;i<ownedPose.Length;i++)ownedPose[i]=correction*snapshot.Live[i];
        }
        var delta=partToBarrel.inverse.MultiplyVector(Vector3.back*(travel/scale));
        var translation=Matrix4x4.Translate(delta);
        if(LiveRelative)foreach(int i in moving)ownedPose[i]=translation*ownedPose[i];
        else foreach(int i in moving)ownedPose[i]=translation*rootFrame*snapshot.Bind[i].inverse;
        if(CoverActive&&CoverBones!=null)foreach(int i in CoverBones)if(i>=0&&i<ownedPose.Length)ownedPose[i]=CoverPose*ownedPose[i];
        snapshot.BakePose(mesh,ownedPose);
        if(ExtraVertices!=null&&ExtraVertices.Length>0&&travel>1e-5f)
        {
            var data=mesh.vertices;
            foreach(int i in ExtraVertices)if(i>=0&&i<data.Length)data[i]+=delta;
            mesh.vertices=data;mesh.RecalculateBounds();
        }
        return true;
    }
    internal void Apply(Mesh mesh,Matrix4x4 partToBarrel,float fitScale,float shotAge,float now,float manualRack=0)
    {
        if(failed) return;
        try
        {
            for(int i=0;i<bones.Count;i++)
            {
                var b=bones[i];if(b.bone==null) continue;
                var p=b.bone.localPosition;var q=b.bone.localRotation;
                if((p-b.p).sqrMagnitude>.00000001f || Quaternion.Angle(q,b.q)>.1f) lastNativeMotion=now;
                bones[i]=(b.bone,p,q);
            }
            float cycle=MechanismMath.Cycle(shotAge,profile);
            if(vertices.Count==0 || manualRack<=0&&(cycle<=0 || now-lastNativeMotion<.20f)) return;
            var data=mesh.vertices;
            var delta=partToBarrel.inverse.MultiplyVector(Vector3.back*((manualRack>0?manualRack:MechanismMath.Travel(profile)*cycle)/fitScale));
            foreach(int i in vertices) if(i>=data.Length) throw new InvalidOperationException("Mechanical mesh topology changed");
            for(int i=0;i<vertices.Count;i++) data[vertices[i]]+=delta*weights[i];
            mesh.vertices=data; mesh.RecalculateBounds();
        }
        catch(Exception ex) { failed=true; Bootstrap.Warn("WEAPON MECHANISM fallback disabled: "+ex.Message); }
    }
}
