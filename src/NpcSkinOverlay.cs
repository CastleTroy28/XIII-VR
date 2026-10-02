using System;
using System.Collections.Generic;
using System.Diagnostics;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.147: while an enemy
// reacts or fights, its skinned meshes are drawn by the mod - baked from its
// bones just before the cameras draw (after the mod bent them), shown in
// their place with their own materials, the game's own renderers of it held
// off meanwhile. Only the most detailed level of a LOD group is drawn.
internal sealed class NpcSkinOverlay:IDisposable
{
    private sealed class Part{internal SkinnedMeshRenderer Skin=null!;internal GameObject Go=null!;internal MeshRenderer Renderer=null!;internal MeshFilter Filter=null!;internal Mesh Mesh=null!;internal bool WasOff;internal bool Baked;}
    private readonly List<Part> parts=new();private readonly List<(Renderer r,bool was)> hidden=new();
    private readonly MaterialPropertyBlock block=new();
    private Animator? animator;private AnimatorCullingMode culling;private bool cullingSet;
    private int bakedFrame=-1;private int frames;private double bakeMs;private bool reported;private string name="";
    internal int Vertices{get;private set;}
    internal static NpcSkinOverlay? Create(NPC npc,NpcBones rig,out string report)
    {
        var o=new NpcSkinOverlay{name=npc.name};report="";
        try
        {
            var joints=new HashSet<IntPtr>();
            foreach(var t in new[]{rig.spineLower,rig.spineMedium,rig.spineTop,rig.neckBase,rig.neckTop,rig.head,rig.leftShoulder,rig.rightShoulder,rig.leftElbow,rig.rightElbow,rig.leftWrist,rig.rightWrist})if(t!=null)joints.Add(t.Pointer);
            // The most detailed LOD only (the others are held off).
            HashSet<IntPtr>? lod0=null;var lower=new HashSet<IntPtr>();
            try
            {
                var group=npc.GetComponentInChildren(Il2CppType.Of<LODGroup>(),true)?.TryCast<LODGroup>();
                if(group!=null)
                {
                    var lods=group.GetLODs();
                    for(int i=0;i<lods.Length;i++)foreach(var r in lods[i].renderers)if(r!=null){if(i==0)(lod0??=new()).Add(r.Pointer);else lower.Add(r.Pointer);}
                }
            }
            catch(Exception){}
            int skins=0,using_=0;
            foreach(var c in npc.GetComponentsInChildren(Il2CppType.Of<SkinnedMeshRenderer>(),true))
            {
                var skin=c.TryCast<SkinnedMeshRenderer>();if(skin==null||skin.sharedMesh==null)continue;skins++;
                bool ours=false;var bones=skin.bones;if(bones!=null)foreach(var b in bones)if(b!=null&&joints.Contains(b.Pointer)){ours=true;break;}
                if(!ours)continue;using_++;
                if(lower.Contains(skin.Pointer)&&!(lod0?.Contains(skin.Pointer)??false)){o.hidden.Add((skin,skin.forceRenderingOff));continue;}
                var go=new GameObject("XIII VR drawn "+skin.name);go.layer=skin.gameObject.layer;
                go.transform.SetParent(skin.transform,false);go.SetActive(false);
                var filter=go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!;
                var renderer=go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
                var mesh=new Mesh{name="XIII VR baked "+skin.name};mesh.MarkDynamic();filter.sharedMesh=mesh;
                renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=skin.shadowCastingMode;renderer.receiveShadows=skin.receiveShadows;
                renderer.lightProbeUsage=skin.lightProbeUsage;renderer.reflectionProbeUsage=skin.reflectionProbeUsage;renderer.allowOcclusionWhenDynamic=false;
                o.parts.Add(new Part{Skin=skin,Go=go,Renderer=renderer,Filter=filter,Mesh=mesh,WasOff=skin.forceRenderingOff});
                o.Vertices+=skin.sharedMesh.vertexCount;
            }
            // Its animation must go on while its own renderers are held off.
            try{o.animator=npc.CharacterAnimator;if(o.animator!=null){o.culling=o.animator.cullingMode;o.cullingSet=true;o.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}}catch(Exception){}
            string anim="none";
            try{var a=o.animator;if(a!=null)anim=a.name+" hierarchy="+a.hasTransformHierarchy+" culling="+o.culling+" update="+a.updateMode+" enabled="+a.enabled;}catch(Exception){}
            report=" drawn by the mod: "+o.parts.Count+" of "+skins+" skinned meshes ("+using_+" use its bones, "+o.Vertices+" vertices"+(lod0!=null?", LOD 0 only":"")+"); animator "+anim;
            if(o.parts.Count==0){o.Dispose();return null;}
            return o;
        }
        catch(Exception ex){report=" (drawing it: "+ex.Message+")";o.Dispose();return null;}
    }
    // Just before the cameras: baked from its bones as they are now.
    internal void Draw()
    {
        if(bakedFrame==Time.frameCount)return;bakedFrame=Time.frameCount;
        long start=Stopwatch.GetTimestamp();
        foreach(var p in parts)
        {
            var skin=p.Skin;
            if(skin==null||!skin.enabled||!skin.gameObject.activeInHierarchy){if(p.Go!=null&&p.Go.activeSelf)p.Go.SetActive(false);continue;}
            skin.BakeMesh(p.Mesh,false);p.Mesh.RecalculateBounds();
            if(frames%15==0)p.Renderer.sharedMaterials=skin.sharedMaterials;
            skin.GetPropertyBlock(block);p.Renderer.SetPropertyBlock(block);
            if(!p.Go!.activeSelf)p.Go.SetActive(true);
            skin.forceRenderingOff=true;p.Baked=true;
        }
        foreach(var (r,_) in hidden)if(r!=null)r.forceRenderingOff=true;
        bakeMs+=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;frames++;
        if(!reported&&frames==90){reported=true;Bootstrap.Write("NPC HIT "+name+": drawn by the mod for "+frames+" frames, "+(bakeMs/frames).ToString("F2")+" ms a frame");}
    }
    public void Dispose()
    {
        foreach(var p in parts)
        {
            try{if(p.Skin!=null)p.Skin.forceRenderingOff=p.WasOff;}catch(Exception){}
            try{if(p.Go!=null)UnityEngine.Object.Destroy(p.Go);}catch(Exception){}
            try{if(p.Mesh!=null)UnityEngine.Object.Destroy(p.Mesh);}catch(Exception){}
        }
        parts.Clear();
        foreach(var (r,was) in hidden)try{if(r!=null)r.forceRenderingOff=was;}catch(Exception){}
        hidden.Clear();
        try{if(cullingSet&&animator!=null)animator.cullingMode=culling;}catch(Exception){}
        cullingSet=false;
    }
}
