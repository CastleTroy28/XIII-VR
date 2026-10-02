using System;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// CPU snapshots in a single, explicit mesh-local space. The private renderer is
// identity-scaled, including while baking imported meshes with a scaled parent.
// It borrows the mesh, but every transform it writes belongs to this class.
internal sealed class NativeSkinSnapshot : IDisposable
{
    private GameObject? root;
    private SkinnedMeshRenderer? baker;
    private Transform[] owned=Array.Empty<Transform>();
    private readonly SkinnedMeshRenderer source;
    internal readonly Mesh Original;
    internal readonly Transform[] Bones;
    internal readonly Matrix4x4[] Bind,Live;
    internal Matrix4x4[]? Initial {get;private set;}
    private int liveFrame=-1;
    internal bool Valid=>root!=null&&baker!=null&&Original!=null&&source!=null&&source.sharedMesh==Original;
    internal NativeSkinSnapshot(SkinnedMeshRenderer skin)
    {
        source=skin;Original=skin.sharedMesh;
        Bones=skin.bones;Bind=Original.bindposes;Live=new Matrix4x4[Bones.Length];
        if(Bones.Length==0 || Bones.Length!=Bind.Length)throw new InvalidOperationException("Skin bone/bind-pose count mismatch: "+skin.name);
        try
        {
            root=new GameObject("XIII private skin snapshot "+skin.name);
            // The player may survive a streamed bank/flashback scene. Keep its
            // identity-scaled private bones until their owner explicitly disposes
            // them, instead of tying them to the scene active at construction.
            UnityEngine.Object.DontDestroyOnLoad(root);
            baker=root.AddComponent(Il2CppType.Of<SkinnedMeshRenderer>()).TryCast<SkinnedMeshRenderer>()!;
            baker.enabled=false;baker.sharedMesh=Original;
            owned=new Transform[Bones.Length];
            for(int i=0;i<owned.Length;i++)
            {
                var go=new GameObject("snapshot bone "+i);go.transform.SetParent(root.transform,false);owned[i]=go.transform;
            }
            baker.bones=owned;
        }
        catch{Dispose();throw;}
    }
    internal void Invalidate()=>liveFrame=-1;
    internal void UseInitialFrame(NativeSkinSnapshot reference)
    {
        if(reference.Original!=Original||reference.Initial==null||reference.Initial.Length!=Live.Length)
            throw new InvalidOperationException("Mismatched duplicate weapon skin");
        Initial=(Matrix4x4[])reference.Initial.Clone();
    }
    internal void Sample()
    {
        if(liveFrame==Time.frameCount)return;
        if(source==null || source.sharedMesh!=Original)throw new InvalidOperationException("Snapshot source changed");
        var frame=source.worldToLocalMatrix;
        for(int i=0;i<Bones.Length;i++)
        {
            if(Bones[i]==null)throw new InvalidOperationException("Snapshot source bone destroyed");
            Live[i]=frame*Bones[i].localToWorldMatrix;
        }
        Initial??=(Matrix4x4[])Live.Clone();
        liveFrame=Time.frameCount;
    }
    // 0.1.129: bones collapsed to a point in the live bake (a pulled grenade pin).
    internal bool[]? Hidden{get;set;}
    private static Matrix4x4 Collapse(Matrix4x4 m)=>m*Matrix4x4.Scale(new Vector3(.001f,.001f,.001f));
    internal void Bake(Mesh output,bool neutral=false)
    {
        if(baker==null)throw new ObjectDisposedException(nameof(NativeSkinSnapshot));
        if(!neutral)Sample();
        var hidden=neutral?null:Hidden;
        for(int i=0;i<owned.Length;i++)
        {
            var m=neutral?Bind[i].inverse:Live[i];if(hidden!=null&&i<hidden.Length&&hidden[i])m=Collapse(m);
            SetOwned(owned[i],m,neutral?Bind[i].inverse.rotation:Quaternion.Inverse(source.transform.rotation)*Bones[i].rotation);
        }
        baker.BakeMesh(output,false);
        if(output.vertexCount!=Original.vertexCount)throw new InvalidOperationException("Snapshot topology changed");
    }
    // 0.1.129: the live pose with only the masked bones (keep=true) or all
    // but them (keep=false); the others are collapsed to points.
    internal void BakeMasked(Mesh output,bool[] mask,bool keep)
    {
        Sample();
        var pose=new Matrix4x4[Live.Length];
        for(int i=0;i<pose.Length;i++)pose[i]=(i<mask.Length&&mask[i])==keep?Live[i]:Collapse(Live[i]);
        BakePose(output,pose);
    }
    internal void BakePose(Mesh output,Matrix4x4[] pose)
    {
        if(baker==null || pose.Length!=owned.Length)throw new InvalidOperationException("Invalid private pose");
        for(int i=0;i<owned.Length;i++)SetOwned(owned[i],pose[i],pose[i].rotation);
        baker.BakeMesh(output,false);
        if(output.vertexCount!=Original.vertexCount)throw new InvalidOperationException("Pose topology changed");
    }
    // 0.1.120: the live (animated) gun, with some bones (the crossbow bolt) put
    // back in their bind place relative to a reference bone of the gun. The
    // bind pose of the whole rig is not the gun's held orientation (the
    // crossbow hung straight down with the neutral bake).
    // 0.1.122: the crossbow's bind pose keeps the bolt beside the gun (it
    // floated in the air). With a relation taken from a loaded crossbow's
    // live pose (LoadedRelation), the bolt is put on its rail.
    private Matrix4x4[]? restoredPose;
    // 0.1.123: a second set of bones (the crossbow's string, drawn by the bolt)
    // placed by its own relation to the same reference bone.
    internal void BakeRestored(Mesh output,int reference,int[] restore,Matrix4x4[]? relation,int[]? extra,Matrix4x4[]? extraRelation)
    {
        Sample();
        restoredPose??=new Matrix4x4[Live.Length];Array.Copy(Live,restoredPose,Live.Length);
        if(reference>=0&&reference<Live.Length)
        {
            var frame=Live[reference];
            if(relation!=null&&relation.Length==restore.Length)Place(frame,restore,relation);
            else
            {
                var bind=frame*Bind[reference];
                foreach(int i in restore)if(i>=0&&i<restoredPose.Length)restoredPose[i]=bind*Bind[i].inverse;
            }
            if(extra!=null&&extraRelation!=null&&extra.Length==extraRelation.Length)Place(frame,extra,extraRelation);
        }
        BakePose(output,restoredPose);
    }
    private void Place(Matrix4x4 frame,int[] bones,Matrix4x4[] relation)
    {
        for(int k=0;k<bones.Length&&k<relation.Length;k++){int i=bones[k];if(i>=0&&i<restoredPose!.Length)restoredPose[i]=frame*relation[k];}
    }
    // The bones' placement relative to the reference bone in the pose sampled
    // when the gun was taken (Initial), if none of them was hidden there
    // (scaled away). Mesh-space matrices of this rig.
    internal Matrix4x4[]? LoadedRelation(int reference,int[] bones)
    {
        var initial=Initial;
        if(initial==null||reference<0||reference>=initial.Length||bones.Length==0)return null;
        var inverse=initial[reference].inverse;var relation=new Matrix4x4[bones.Length];
        for(int k=0;k<bones.Length;k++)
        {
            int i=bones[k];if(i<0||i>=initial.Length)return null;
            float scale=SkinScale(initial[i]*Bind[i]);
            if(!(scale>.3f&&scale<3f))return null;
            relation[k]=inverse*initial[i];
        }
        return relation;
    }
    internal static float SkinScale(Matrix4x4 m)=>m.m00*(m.m11*m.m22-m.m12*m.m21)-m.m01*(m.m10*m.m22-m.m12*m.m20)+m.m02*(m.m10*m.m21-m.m11*m.m20);
    // A pose (Live or Initial) with the given bones placed by the relation.
    internal void BakeRelation(Mesh output,Matrix4x4[] basePose,int reference,int[] bones,Matrix4x4[] relation)
    {
        restoredPose??=new Matrix4x4[Live.Length];Array.Copy(basePose,restoredPose,Math.Min(basePose.Length,restoredPose.Length));
        Place(basePose[reference],bones,relation);
        BakePose(output,restoredPose);
    }
    internal void BakeAttachment(Mesh output,int weaponRoot)
    {
        Sample();
        // Reload animations shrink the parked shell/magazine to zero. Preserve
        // the fitted weapon root but reconstruct child geometry from bind pose.
        var frame=weaponRoot>=0?(Initial??Live)[weaponRoot]*Bind[weaponRoot]:Matrix4x4.identity;
        var pose=new Matrix4x4[Bind.Length];
        for(int i=0;i<pose.Length;i++)pose[i]=frame*Bind[i].inverse;
        BakePose(output,pose);
    }
    private static void SetOwned(Transform bone,Matrix4x4 matrix,Quaternion fallback)
    {
        SnapshotPoseMath.Decompose(NativeHandVisual.N(matrix),new System.Numerics.Quaternion(fallback.x,fallback.y,fallback.z,fallback.w),out var p,out var q,out var s);
        bone.localPosition=new Vector3(p.X,p.Y,p.Z);bone.localRotation=new Quaternion(q.X,q.Y,q.Z,q.W);bone.localScale=new Vector3(s.X,s.Y,s.Z);
    }
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;baker=null;owned=Array.Empty<Transform>();
        // Never destroy the borrowed mesh or touch source bones/renderer flags.
    }
}
