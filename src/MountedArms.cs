using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.120: the game's own arms that hold a stationary gun, cut from the
// shoulder to the elbow. Each arm mesh is baked every frame (after the game's
// animation) into an owned mesh without the triangles of the upper arm; the
// game's renderer is only switched off for drawing (forceRenderingOff), its
// own enabled state stays the game's.
internal sealed class MountedArms:IDisposable
{
    private sealed class Part
    {
        internal SkinnedMeshRenderer Source=null!;internal NativeSkinSnapshot Snapshot=null!;internal Mesh Mesh=null!;
        internal GameObject Root=null!;internal MeshRenderer Renderer=null!;internal int[][] Kept=null!;internal bool Dead;
    }
    private readonly List<Part> parts=new();
    private readonly List<(Animator animator,AnimatorCullingMode mode)> animators=new();
    private float nextError;
    internal int Count=>parts.Count;
    internal MountedArms(GameObject? arms)
    {
        if(arms==null){Bootstrap.Write("MOUNTED ARMS none on this gun");return;}
        foreach(var c in arms.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),true))
        {
            var skin=c.TryCast<SkinnedMeshRenderer>();if(skin==null||skin.sharedMesh==null)continue;
            try{var p=Build(skin);if(p!=null)parts.Add(p);}
            catch(Exception ex){Bootstrap.Warn("MOUNTED ARMS "+skin.name+" left whole: "+ex.Message);}
        }
        if(parts.Count==0)return;
        // The game's renderers are not drawn now; keep the arms animating.
        var found=new List<Animator>();
        var up=arms.GetComponentInParent(Il2CppType.Of<Animator>())?.TryCast<Animator>();if(up!=null)found.Add(up);
        foreach(var c in arms.GetComponentsInChildren(Il2CppType.Of<Animator>(),true)){var a=c.TryCast<Animator>();if(a!=null&&!found.Any(x=>x.Pointer==a.Pointer))found.Add(a);}
        foreach(var a in found){animators.Add((a,a.cullingMode));a.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
    }
    private static Part? Build(SkinnedMeshRenderer skin)
    {
        var bones=skin.bones;int n=bones.Length;
        var names=new string[n];var parents=new int[n];var index=new Dictionary<IntPtr,int>();
        for(int i=0;i<n;i++){names[i]=bones[i]!=null?bones[i].name:"";if(bones[i]!=null)index[bones[i].Pointer]=i;}
        for(int i=0;i<n;i++)
        {
            parents[i]=-1;
            for(var t=bones[i]!=null?bones[i].parent:null;t!=null;t=t.parent)if(index.TryGetValue(t.Pointer,out int p)){parents[i]=p;break;}
        }
        var keepBones=ArmCropMath.KeepBones(names,parents,out string how);
        Bootstrap.Write("MOUNTED ARMS "+skin.name+" bones="+string.Join(",",names)+" elbow by "+how);
        if(keepBones==null)return null;
        var snapshot=new NativeSkinSnapshot(skin);Mesh? mesh=null;GameObject? root=null;
        try
        {
            mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name="XIII mounted arms "+skin.name;mesh.MarkDynamic();
            // Weight of the kept bones per vertex: their two-centimetre shift.
            snapshot.Bake(mesh,true);var rest=mesh.vertices;
            var pose=new Matrix4x4[n];var shift=Matrix4x4.Translate(new Vector3(.02f,0,0));
            for(int i=0;i<n;i++){pose[i]=snapshot.Bind[i].inverse;if(keepBones[i])pose[i]=shift*pose[i];}
            snapshot.BakePose(mesh,pose);var moved=mesh.vertices;
            if(moved.Length!=rest.Length||rest.Length==0)throw new InvalidOperationException("probe vertices "+moved.Length+"/"+rest.Length);
            var keep=new float[rest.Length];
            for(int i=0;i<keep.Length;i++){float w=(moved[i].x-rest[i].x)/.02f;keep[i]=float.IsFinite(w)?Math.Clamp(w,0,1):0;}
            var kept=new int[mesh.subMeshCount][];int total=0,left=0;
            for(int s=0;s<kept.Length;s++){var t=mesh.GetTriangles(s);total+=t.Length/3;kept[s]=ArmCropMath.Triangles(t,keep);left+=kept[s].Length/3;}
            if(left==0)throw new InvalidOperationException("nothing below the elbow");
            root=new GameObject("XIII mounted arms "+skin.name);root.layer=skin.gameObject.layer;root.transform.SetParent(skin.transform,false);
            root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
            var renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.enabled=false;
            Bootstrap.Write("MOUNTED ARMS "+skin.name+" cut at the elbow: triangles "+left+"/"+total+" kept bones="+string.Join(",",names.Where((x,i)=>keepBones[i])));
            return new Part{Source=skin,Snapshot=snapshot,Mesh=mesh,Root=root,Renderer=renderer,Kept=kept};
        }
        catch
        {
            snapshot.Dispose();if(root!=null)UnityEngine.Object.Destroy(root);if(mesh!=null)UnityEngine.Object.Destroy(mesh);
            throw;
        }
    }
    // Before the cameras draw (after the game's animation of this frame).
    internal void Render()
    {
        foreach(var p in parts)
        {
            if(p.Dead)continue;
            try
            {
                SkinnedMeshRenderer? source=p.Source;MeshRenderer? shown=p.Renderer;Mesh? mesh=p.Mesh;
                if(source==null||shown==null||mesh==null){p.Dead=true;continue;}
                source.forceRenderingOff=true;
                if(!source.enabled||!source.gameObject.activeInHierarchy){shown.enabled=false;continue;}
                p.Snapshot.Bake(mesh);
                for(int s=0;s<p.Kept.Length&&s<mesh.subMeshCount;s++)mesh.SetTriangles(p.Kept[s],s,false,0);
                shown.enabled=true;
            }
            catch(Exception ex)
            {
                // Give the game's whole arms back rather than show nothing.
                p.Dead=true;
                try{if(p.Renderer!=null)p.Renderer.enabled=false;if(p.Source!=null)p.Source.forceRenderingOff=false;}catch(Exception){}
                if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("MOUNTED ARMS crop stopped, whole arms shown: "+ex.Message);}
            }
        }
    }
    public void Dispose()
    {
        foreach(var p in parts)
        {
            try{if(p.Source!=null)p.Source.forceRenderingOff=false;}catch(Exception){}
            try{if(p.Root!=null)UnityEngine.Object.Destroy(p.Root);}catch(Exception){}
            try{if(p.Mesh!=null)UnityEngine.Object.Destroy(p.Mesh);}catch(Exception){}
            try{p.Snapshot.Dispose();}catch(Exception){}
        }
        parts.Clear();
        foreach(var (animator,mode) in animators)try{if(animator!=null)animator.cullingMode=mode;}catch(Exception){}
        animators.Clear();
    }
}
