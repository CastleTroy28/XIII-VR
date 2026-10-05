using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// A render-only copy. No Equipable/FireComponent/Animator/collider is cloned.
// Original game transforms stay under their original animation hierarchy.
internal sealed partial class WeaponVisual : IDisposable
{
    internal bool ManualMode;
    internal bool StableGun;
    // 0.1.229: the bazooka is drawn as it was when taken (its fit and its
    // grips were measured so), not moved by the game's animation of it: after
    // a shot (its recoil, its reload, its empty pose) the whole drawn bazooka
    // was up to 15 cm off the hands holding its grips. Only its rocket moves
    // (NativeSkinSnapshot.BakeStill).
    internal bool HeldStill=>Profile=="bazooka";
    private readonly List<Mesh> baked = new();
    private ReloadGripMath? reloadGrip;
    private ReloadMesh? reloadMesh;private Part? magazinePart;private int[][]? magazineKept,magazineFull;private bool magazineHidden,reloadProbeDone;
    private ReloadHint? reloadHint;private float manualRack;private bool slideLocked;
    internal Vector3 ReloadPort {get;private set;}
    internal Vector3 MagazineCenter=>reloadMesh?.Center??ReloadPort;
    internal Vector3 InsertAxis {get;private set;}=Vector3.up;
    internal ReloadMesh? Ammunition=>reloadMesh;
    internal int[][]? MagazineKept=>magazineKept;
    internal int[][]? MagazineFull=>magazineFull;
    internal Vector3 EffectMuzzle{get;private set;}
    // 0.1.122: rigid copies of the charging handle/slide and the M60 cover for
    // the outline hint (fitted gun space; the bolt relative to BoltShapeCenter).
    internal Mesh? BoltShape{get;private set;}
    internal Vector3 BoltShapeCenter{get;private set;}
    internal Mesh? CoverShape{get;private set;}
    private static Mesh? ExtractPart(Mesh source,Matrix4x4 toFit,bool[] selected,bool centered,out Vector3 center,string name)
    {
        center=Vector3.zero;
        var vertices=source.vertices;var normals=source.normals;if(selected.Length!=vertices.Length)return null;
        var remap=new int[vertices.Length];Array.Fill(remap,-1);var points=new List<Vector3>();var ns=new List<Vector3>();
        var normalFrame=toFit.inverse.transpose;
        for(int i=0;i<vertices.Length;i++)if(selected[i]){remap[i]=points.Count;points.Add(toFit.MultiplyPoint3x4(vertices[i]));ns.Add(normals.Length==vertices.Length?normalFrame.MultiplyVector(normals[i]).normalized:Vector3.up);}
        if(points.Count<4)return null;
        if(centered){var min=points[0];var max=points[0];foreach(var p in points){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}center=(min+max)*.5f;for(int i=0;i<points.Count;i++)points[i]-=center;}
        bool mirrored=toFit.determinant<0;var kept=new List<int>();
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var t=source.GetTriangles(sub);
            for(int i=0;i+2<t.Length;i+=3)if(remap[t[i]]>=0&&remap[t[i+1]]>=0&&remap[t[i+2]]>=0)
            {kept.Add(remap[t[i]]);kept.Add(remap[t[i+(mirrored?2:1)]]);kept.Add(remap[t[i+(mirrored?1:2)]]);}
        }
        if(kept.Count<3)return null;
        var mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name=name;
        if(points.Count>65000)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices=points.ToArray();mesh.normals=ns.ToArray();mesh.triangles=kept.ToArray();mesh.RecalculateBounds();
        return mesh;
    }
    private static bool[] Weighted(BoneWeight[] weights,HashSet<int> bones)
    {
        var selected=new bool[weights.Length];
        for(int i=0;i<weights.Length;i++){var w=weights[i];selected[i]=(bones.Contains(w.boneIndex0)?w.weight0:0)+(bones.Contains(w.boneIndex1)?w.weight1:0)+(bones.Contains(w.boneIndex2)?w.weight2:0)+(bones.Contains(w.boneIndex3)?w.weight3:0)>.5f;}
        return selected;
    }
    internal Vector3 ReloadBolt {get;private set;}
    internal bool ReloadAvailable=>reloadMesh!=null;
    internal bool ReloadUnavailable{get;private set;}
    private int[]? arrowBones;private int arrowReference=-1;private Part? arrowPart;
    // 0.1.122: the crossbow bolt's place on the rail (from a loaded crossbow).
    private Matrix4x4[]? arrowRelation;
    internal bool LoadedAtBuild=true;
    private static string BoltFile=>System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-crossbow-bolt.txt");
    private static void SaveBolt(string[] names,Matrix4x4[] relation)=>SaveRelation(BoltFile,"bolt",names,relation);
    private static Matrix4x4[]? LoadBolt(string[] names)=>LoadRelation(BoltFile,"bolt",names);
    private static void SaveRelation(string file,string what,string[] names,Matrix4x4[] relation)
    {
        try
        {
            var data=new float[relation.Length][];
            for(int k=0;k<relation.Length;k++){data[k]=new float[16];for(int i=0;i<16;i++)data[k][i]=relation[k][i];}
            System.IO.File.WriteAllText(file,BoltRelationText.Format(names,data));
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON "+what+" placement not saved: "+ex.Message);}
    }
    private static Matrix4x4[]? LoadRelation(string file,string what,string[] names)
    {
        try
        {
            if(!System.IO.File.Exists(file))return null;
            var data=BoltRelationText.Parse(System.IO.File.ReadAllText(file),names);if(data==null)return null;
            var relation=new Matrix4x4[data.Length];
            for(int k=0;k<data.Length;k++){var m=new Matrix4x4();for(int i=0;i<16;i++)m[i]=data[k][i];relation[k]=m;}
            return relation;
        }
        catch(Exception ex){Bootstrap.Warn("WEAPON "+what+" placement not read: "+ex.Message);return null;}
    }
    // The loaded bolt's rear end (at the string) and its length, fitted space.
    internal Vector3 ArrowRear=>reloadMesh==null?Vector3.zero:reloadMesh.Center+new Vector3(0,0,reloadMesh.Min.z);
    internal float ArrowLength=>reloadMesh==null?0:reloadMesh.Max.z-reloadMesh.Min.z;
    // The held bolt shown on the rail, its rear this far ahead of the string.
    internal void PoseArrowOnRail(float along)
    {if(reloadMesh!=null)reloadMesh.Pose(FittedToWorld.MultiplyPoint3x4(reloadMesh.Center+Vector3.forward*along),FittedToWorld.rotation);}
    // 0.1.118: centre of the fitted model (the grenade body for the pin pull).
    internal Vector3 ItemCenter{get;private set;}
    private int reloadFailures;
    private readonly List<NativeSkinSnapshot> snapshots=new();
    private Matrix4x4 fitMatrix;
    internal ContactSphere[]? ContactShape {get;private set;}
    internal string Profile { get; private set; }="";
    private float createdAt;
    private readonly Dictionary<Renderer,bool> propSources=new();
    internal Vector3 PropCenter {get;private set;}
    internal float PropRadius {get;private set;}
    internal bool IsChair {get;private set;}
    internal bool ControlledPropGrip=>Profile=="prop"&&PropGripGeometry.Controlled(propGrip);
    internal Vector3 PropGripContact {get;private set;}
    // 0.1.100: broom/mop handle axis (fitted space). LongGripAxis starts at the
    // grip point near the handle end and points towards the brush/head.
    internal Vector3? LongGripOrigin {get;private set;}
    // 0.1.152: the other end of a long stick (the shovel's blade, the broom's brush), fitted space.
    internal Vector3? LongHeadEnd {get;private set;}
    internal Vector3 LongGripAxis {get;private set;}
    internal float PropGripThickness {get;private set;}
    internal ChairGripSurface? ChairSurface {get;private set;}
    private static readonly HashSet<string> dumpedSections=new();
    private string propGrip="prop_unknown";
    internal string GripProfile=>Profile=="prop"?propGrip:Profile;
    internal bool ReadyForGripCapture=>Time.realtimeSinceStartup-createdAt>.4f;
    internal bool AuthoredGripAvailable { get; private set; }=true;
    internal Matrix4x4 GameWorldToFitted
    {
        get
        {
            if(Profile=="prop"||StableGun)foreach(var part in animatedParts)
            {
                if(part.Source==null)continue;
                var correction=Matrix4x4.identity;
                if(part.Snapshot!=null&&StableGun)
                    correction=BreakAction&&part==breakPart?BreakCorrection(part):part.Mechanism?.GripCorrection(part.Snapshot)??Matrix4x4.identity;
                return fitMatrix*part.Matrix*part.BakedSourceMatrix*correction*part.Source.worldToLocalMatrix;
            }
            return fitMatrix*(sourceRoot!.localToWorldMatrix*rootToBarrel).inverse;
        }
    }
    internal string AnchorKey=>GripProfile+":"+(root==null?0:root.GetInstanceID())+":"+StableGun;
    internal Matrix4x4 FittedToWorld=>root!.transform.localToWorldMatrix;
    private readonly List<Part> animatedParts = new();
    private readonly List<(Animator animator,AnimatorCullingMode mode,bool enabled)> animators = new();
    private PlayMagic.Weapons.AnimationComponent? weaponAnimation;
    private bool oldOffscreen,animationOwned;
    private Transform? sourceMuzzle;
    private float fitScale,lastShot=float.NegativeInfinity;
    internal void NotifyShot(float now) => lastShot=now;
    internal void ResetCycle() => lastShot=float.NegativeInfinity;
    private Transform? sourceRoot;
    private Matrix4x4 rootToBarrel;
    private int animatedFrame=-1;
    private bool animationFailed;
    private float animationRetryAt;
    private float nextAnimationReport;
    private GameObject? flash;
    private Material? flashMaterial;
    private float nativeFlashUntil;
    private GameObject? root;
    internal Transform? MuzzleAnchor { get; private set; }
    internal void NativeFlashShown() { nativeFlashUntil=Time.realtimeSinceStartup+.12f; if(flash!=null) flash.SetActive(false); }
    internal Vector3 MuzzleOffset { get; private set; }
    internal bool Exists => root != null;
    private sealed class Part
    {
        internal Renderer Source = null!;
        internal Mesh Mesh = null!;
        internal Matrix4x4 BakedSourceMatrix=Matrix4x4.identity;
        internal Matrix4x4 Matrix;
        internal SkinnedMeshRenderer? Skin;
        internal NativeSkinSnapshot? Snapshot;
        internal bool WasOffscreen;
        internal Mesh? Spare;
        internal MeshFilter? Filter;
        internal Transform? Copy;
        internal Bounds InitialBounds;
        internal WeaponMechanism? Mechanism;
    }
    // 0.1.124: the gun as it is drawn now, for still copies (the body places,
    // the floor): each part's current mesh (baked: the copy must duplicate
    // it; otherwise the game's own mesh asset), its materials and its place
    // relative to the visual's root (the fitted frame).
    // 0.1.156: a copy
    // made while the thing was in the left hand took its mirrored drawing (a
    // mirrored place cannot be told back apart into a turn and a size: the
    // copy lay beside the hand) - it is taken unmirrored now; and a copy can
    // be drawn in the pose the thing had when the right hand held it (poses:
    // each skinned part's bones then), not in whatever the game's animation
    // had it in at the moment it was put away.
    private readonly List<Mesh> stillTemp=new();
    // 0.1.183: magazine: the part holding the magazine (MagazineKept /
    // MagazineFull: its triangles without / with it, for a copy whose
    // magazine is out).
    internal List<(Mesh mesh,bool baked,Material[] materials,Matrix4x4 local,bool magazine)> StillParts(Matrix4x4[]?[]? poses=null)
    {
        var list=new List<(Mesh,bool,Material[],Matrix4x4,bool)>();if(root==null)return list;
        foreach(var m in stillTemp)if(m!=null)UnityEngine.Object.Destroy(m);stillTemp.Clear();
        var toRoot=root.transform.worldToLocalMatrix;
        for(int i=0;i<animatedParts.Count;i++)
        {
            var part=animatedParts[i];
            if(part.Copy==null||part.Filter==null)continue;var mesh=part.Filter.sharedMesh;if(mesh==null||mesh.vertexCount==0)continue;
            var pose=poses!=null&&i<poses.Length?poses[i]:null;
            if(pose!=null&&part.Snapshot!=null&&pose.Length==part.Snapshot.Live.Length)
            {
                try{var held=new Mesh{name="XIII held pose "+Profile,hideFlags=HideFlags.DontUnloadUnusedAsset};part.Snapshot.BakePose(held,pose);stillTemp.Add(held);mesh=held;}
                catch(Exception ex){Bootstrap.Warn("WEAPON still pose "+Profile+": "+ex.Message);}
            }
            var local=toRoot*part.Copy.localToWorldMatrix;
            if(geometryMirrored)local=Matrix4x4.Scale(new Vector3(-1,1,1))*local;
            list.Add((mesh,part.Skin!=null,part.Source!=null?part.Source.sharedMaterials:Array.Empty<Material>(),local,part==magazinePart&&magazineKept!=null));
        }
        return list;
    }
    // Each skinned part's bones now (for a copy drawn as held).
    internal Matrix4x4[]?[] LivePoses()
    {
        var poses=new Matrix4x4[]?[animatedParts.Count];
        // 0.1.229: a bazooka is drawn still (its copies: as drawn).
        if(HeldStill)return poses;
        for(int i=0;i<animatedParts.Count;i++)
        {
            var s=animatedParts[i].Snapshot;if(s==null)continue;
            try{s.Sample();poses[i]=(Matrix4x4[])s.Live.Clone();}catch(Exception){}
        }
        return poses;
    }
    internal static WeaponVisual Create(Equipable weapon,Transform muzzle,string profile)
    {
        var visual = new WeaponVisual();
        try { visual.Build(weapon,muzzle,profile); return visual; }
        catch { visual.Dispose(); throw; }
    }
    internal void MatchPrimaryFit(WeaponVisual primary)
    {
        if(sourceRoot!=primary.sourceRoot||Profile!=primary.Profile||animatedParts.Count!=primary.animatedParts.Count)
            throw new InvalidOperationException("Mismatched dual pistol visual sources");
        // Both pistols use the same fitted handle. Re-fitting the second copy
        // during a dual draw animation changes its size/origin under the hand.
        fitMatrix=primary.fitMatrix;fitScale=primary.fitScale;rootToBarrel=primary.rootToBarrel;
        MuzzleOffset=primary.MuzzleOffset;EffectMuzzle=primary.EffectMuzzle;ContactShape=primary.ContactShape;
        AuthoredGripAvailable=primary.AuthoredGripAvailable;
        if(MuzzleAnchor!=null)MuzzleAnchor.localPosition=EffectMuzzle;
        if(flash!=null)flash.transform.localPosition=EffectMuzzle+Vector3.forward*.025f;
        foreach(var part in animatedParts)
        {
            var original=primary.animatedParts.Find(p=>p.Source==part.Source);
            if(original==null)throw new InvalidOperationException("Dual pistol mesh source changed");
            part.Matrix=original.Matrix;part.BakedSourceMatrix=original.BakedSourceMatrix;part.InitialBounds=original.InitialBounds;
            if(part.Snapshot!=null&&original.Snapshot!=null)part.Snapshot.UseInitialFrame(original.Snapshot);
            var geometry=part.Copy!.parent;
            geometry.localPosition=new Vector3(fitMatrix.m03,fitMatrix.m13,fitMatrix.m23);
            geometry.localRotation=fitMatrix.rotation;geometry.localScale=fitMatrix.lossyScale;
            part.Copy.localPosition=new Vector3(part.Matrix.m03,part.Matrix.m13,part.Matrix.m23);
            part.Copy.localRotation=part.Matrix.rotation;part.Copy.localScale=part.Matrix.lossyScale;
        }
        animatedFrame=-1;
    }
    private Vector3? attachmentFront;
    // The bounds (mesh space) of a skinned gun without its silencer, and the
    // shown silencer's front middle (barrel frame; null for a hidden one).
    // 0.1.243: a hidden silencer (shrunk to a point outside the gun) left out too.
    private static bool TrimAttachment(NativeSkinSnapshot snapshot,Mesh mesh,Matrix4x4 toFrame,out Bounds trimmed,out Vector3? front)
    {
        trimmed=default;front=null;
        var bones=snapshot.Bones;var mark=new bool[bones.Length];bool any=false;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&WeaponGeometry.Attachment(bones[i].name)){mark[i]=true;any=true;}
        if(!any)return false;
        for(int i=0;i<bones.Length;i++)if(!mark[i]&&bones[i]!=null)for(int j=0;j<bones.Length;j++)if(mark[j]&&bones[i].IsChildOf(bones[j])){mark[i]=true;break;}
        var weights=snapshot.Original.boneWeights;var vertices=mesh.vertices;
        if(weights.Length!=vertices.Length)return false;
        bool On(int b)=>b>=0&&b<mark.Length&&mark[b];
        var points=new System.Numerics.Vector3[vertices.Length];var attached=new bool[vertices.Length];int count=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];points[i]=new System.Numerics.Vector3(v.x,v.y,v.z);var w=weights[i];
            attached[i]=(On(w.boneIndex0)?w.weight0:0)+(On(w.boneIndex1)?w.weight1:0)+(On(w.boneIndex2)?w.weight2:0)+(On(w.boneIndex3)?w.weight3:0)>.5f;
            if(attached[i])count++;
        }
        if(!WeaponGeometry.Split(points,attached,out var min,out var max,out var attachMin,out var attachMax))return false;
        trimmed=new Bounds();trimmed.SetMinMax(new Vector3(min.X,min.Y,min.Z),new Vector3(max.X,max.Y,max.Z));
        var whole=mesh.bounds;
        if(!WeaponGeometry.Shown(min,max,attachMin,attachMax,.10f))
        {
            Bootstrap.Write("WEAPON VISUAL "+snapshot.Original.name+" fitted without its hidden silencer ("+count+" of "+vertices.Length+" vertices at one point): the gun's own size "+trimmed.size.ToString("F4")+" (with it "+whole.size.ToString("F4")+")");
            return true;
        }
        // Its front: the farthest silencer point along the barrel, in its middle across.
        bool has=false;Vector3 lo=default,hi=default;
        for(int i=0;i<vertices.Length;i++)if(attached[i])
        {var p=toFrame.MultiplyPoint3x4(vertices[i]);if(!has){lo=hi=p;has=true;}else{lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);}}
        front=new Vector3((lo.x+hi.x)*.5f,(lo.y+hi.y)*.5f,hi.z);
        Bootstrap.Write("WEAPON VISUAL "+snapshot.Original.name+" fitted without its silencer ("+count+" of "+vertices.Length+" vertices): the gun's own size "+trimmed.size.ToString("F4")+", the silencer ahead of the muzzle");
        return true;
    }
    // 0.1.228: the bazooka without its rocket (its bones and those under them):
    // the bounds of the rest, the tube, wherever the game holds the rocket.
    private static bool TrimRocket(NativeSkinSnapshot snapshot,Mesh mesh,out Bounds tube)
    {
        tube=default;
        var bones=snapshot.Bones;var mark=new bool[bones.Length];bool any=false;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&BazookaTubeMath.RocketBone(bones[i].name)){mark[i]=true;any=true;}
        if(!any)return false;
        for(int i=0;i<bones.Length;i++)if(!mark[i]&&bones[i]!=null)for(int j=0;j<bones.Length;j++)if(mark[j]&&bones[j]!=null&&bones[i].IsChildOf(bones[j])){mark[i]=true;break;}
        var weights=snapshot.Original.boneWeights;var vertices=mesh.vertices;
        if(weights.Length!=vertices.Length)return false;
        bool On(int b)=>b>=0&&b<mark.Length&&mark[b];
        var points=new System.Numerics.Vector3[vertices.Length];var rocket=new bool[vertices.Length];int count=0;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];points[i]=new System.Numerics.Vector3(v.x,v.y,v.z);var w=weights[i];
            rocket[i]=(On(w.boneIndex0)?w.weight0:0)+(On(w.boneIndex1)?w.weight1:0)+(On(w.boneIndex2)?w.weight2:0)+(On(w.boneIndex3)?w.weight3:0)>.5f;
            if(rocket[i])count++;
        }
        if(!WeaponGeometry.WithoutAttachment(points,rocket,0,out var min,out var max,out _,out _))return false;
        tube=new Bounds();tube.SetMinMax(new Vector3(min.X,min.Y,min.Z),new Vector3(max.X,max.Y,max.Z));
        Bootstrap.Write("WEAPON VISUAL bazooka fitted without its rocket ("+count+" of "+vertices.Length+" vertices) at its usual scale: the same size and handles wherever the game holds the rocket");
        return true;
    }
    private void Build(Equipable weapon,Transform muzzle,string profile)
    {
        sourceMuzzle=muzzle;Profile=profile;createdAt=Time.realtimeSinceStartup;sourceRoot=weapon.transform;
        rootToBarrel=sourceRoot.worldToLocalMatrix*Matrix4x4.TRS(muzzle.position,muzzle.rotation,Vector3.one);
        var sources = new List<Renderer>(); int bestLod = int.MaxValue;
        var brokenRoots=new List<Transform>();
        if(profile=="prop")foreach(var c in weapon.GetComponentsInChildren(Il2CppType.Of<DestructableWeaponPart>(),true))
        {var part=c.TryCast<DestructableWeaponPart>();if(part?.pieces!=null)foreach(var piece in part.pieces)if(piece!=null)brokenRoots.Add(piece.transform);}
        foreach (var component in weapon.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var renderer = component.TryCast<Renderer>(); if (renderer == null) continue;
            if (renderer.TryCast<MeshRenderer>() == null && renderer.TryCast<SkinnedMeshRenderer>() == null) continue;
            var owner=renderer.GetComponentInParent(Il2CppType.Of<Equipable>())?.TryCast<Equipable>();
            if(owner!=null&&owner.Pointer!=weapon.Pointer)continue;
            string name = renderer.name.ToLowerInvariant();
            if(profile=="prop")
            {
                // Inactive broken variants and projectile meshes are not the held prop.
                bool fragment=!renderer.gameObject.activeInHierarchy||name.Contains("remains")||name.Contains("broken")||name.Contains("fragment");
                for(var parent=renderer.transform;parent!=null&&parent!=weapon.transform;parent=parent.parent)
                {var label=parent.name.ToLowerInvariant();if(label.Contains("remains")||label.Contains("broken")||label.Contains("fragment")){fragment=true;break;}}
                foreach(var broken in brokenRoots)if(renderer.transform.IsChildOf(broken)){fragment=true;break;}
                if(renderer.GetComponentInParent(Il2CppType.Of<Projectile>())!=null)fragment=true;
                if(fragment)continue;
            }
            if(name.Contains("player_arm")||name.Contains("arms")||name.Contains("hands")||name.Contains("sleeve"))continue;
            if (name.Contains("flash") || name.Contains("trail") || name.Contains("laser")) continue;
            sources.Add(renderer);
            int lod = Lod(name); if (lod >= 0) bestLod = Math.Min(bestLod,lod);
        }
        var parts = new List<Part>();
        Matrix4x4 frame = Matrix4x4.TRS(muzzle.position,muzzle.rotation,Vector3.one).inverse;
        bool hasBounds = false; Bounds bounds = default;bool bazookaTrimmed=false;
        foreach (var source in sources)
        {
            int lod = Lod(source.name.ToLowerInvariant()); if (lod >= 0 && lod != bestLod) continue;
            if (source.isPartOfStaticBatch) { Bootstrap.Warn("WEAPON VISUAL skipped static-batched mesh " + source.name); continue; }
            Mesh? mesh;NativeSkinSnapshot? snapshot=null;
            var skin = source.TryCast<SkinnedMeshRenderer>();
            if (skin != null)
            {
                mesh = new Mesh(); baked.Add(mesh);
                try { snapshot=new NativeSkinSnapshot(skin);snapshots.Add(snapshot);snapshot.Bake(mesh); }
                catch(Exception ex)
                {
                    snapshot?.Dispose();snapshot=null;AuthoredGripAvailable=false;skin.BakeMesh(mesh);
                    Bootstrap.Warn("WEAPON canonical snapshot unavailable; legacy visual retained, authored grip disabled: "+ex.Message);
                }
            }
            else mesh = source.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount == 0 || mesh.vertexCount > 200000) continue;
            var matrix = frame * source.localToWorldMatrix;
            // 0.1.194: the double-barrelled shotgun laid along its barrels, fitted without its shells.
            bool breakHere=false;
            if(profile=="shotgun"&&snapshot!=null&&!BreakAction&&parts.Count==0)
            {
                try
                {
                    if(PrepareBreak(snapshot,mesh,matrix,out var turn))
                    {
                        breakHere=true;frame=turn*frame;matrix=frame*source.localToWorldMatrix;rootToBarrel=rootToBarrel*turn.inverse;
                    }
                }
                catch(Exception ex){BreakAction=false;breakBounds=null;Bootstrap.Warn("BREAK ACTION shotgun kept as drawn: "+ex.Message);}
            }
            Bounds box = mesh.bounds;
            // 0.1.228: the bazooka's rocket is left out of the fit (TrimRocket).
            if(profile=="bazooka"&&snapshot!=null&&parts.Count==0)
            {
                try{if(TrimRocket(snapshot,mesh,out var tube)){box=tube;bazookaTrimmed=true;}}
                catch(Exception ex){Bootstrap.Warn("WEAPON VISUAL bazooka fitted with its rocket: "+ex.Message);}
            }
            // 0.1.194: a shown silencer is left out of the fit (0.1.243: a hidden one too: the gun's own size).
            if(profile!="prop"&&snapshot!=null)
            {
                try{if(TrimAttachment(snapshot,mesh,matrix,out var trimmed,out var front)){box=trimmed;attachmentFront=front??attachmentFront;}}
                catch(Exception ex){Bootstrap.Warn("WEAPON VISUAL attachment left in the fit: "+ex.Message);}
            }
            var min = box.min; var max = box.max;
            // Already in the barrel frame (exact, without its shells).
            var cornerFrame=matrix;
            if(breakHere&&breakBounds is Bounds exact){min=exact.min;max=exact.max;cornerFrame=Matrix4x4.identity;}
            for (int i = 0; i < 8; i++)
            {
                var point = cornerFrame.MultiplyPoint3x4(new Vector3((i&1)==0?min.x:max.x,(i&2)==0?min.y:max.y,(i&4)==0?min.z:max.z));
                if (!Finite(point)) throw new InvalidOperationException("Nonfinite weapon mesh bounds");
                if (!hasBounds) { bounds = new Bounds(point,Vector3.zero); hasBounds = true; } else bounds.Encapsulate(point);
            }
            parts.Add(new Part { Source = source, Mesh = mesh, Matrix = matrix, Skin=skin, Snapshot=snapshot, InitialBounds=mesh.bounds });
            if(breakHere)breakPart=parts[^1];
            Bootstrap.Write("WEAPON VISUAL source=" + source.name + " type=" + source.GetIl2CppType().Name + " vertices=" + mesh.vertexCount + " sourceScale=" + source.transform.lossyScale);
        }
        if(profile=="prop")
        {
            propGrip=PropFitPolicy.GripKey(weapon.identifier,weapon.name);
            if(ControlledPropGrip)bounds=FlattenPropParts(parts);
        }
        if (!hasBounds || parts.Count == 0 || bounds.size.z < .0001f || bounds.size.z > 1000)
            throw new InvalidOperationException("No usable weapon mesh in barrel frame");
        var minBound = bounds.min; var maxBound = bounds.max;
        var fit = WeaponGeometry.Fit(new System.Numerics.Vector3(minBound.x,minBound.y,minBound.z),
            new System.Numerics.Vector3(maxBound.x,maxBound.y,maxBound.z),profile,bazookaTrimmed?WeaponGeometry.BazookaScale:float.NaN);
        var propRotation=Quaternion.identity;
        if(profile=="prop")
        {
            string label=(weapon.name+" "+weapon.identifier).ToLowerInvariant();
            bool chair=label.Contains("chair")||label.Contains("stool");IsChair=chair;
            propGrip=PropFitPolicy.GripKey(weapon.identifier,weapon.name);
            float extent=Math.Max(bounds.size.x,Math.Max(bounds.size.y,bounds.size.z));
            float scale=PropFitPolicy.Extent(label)/extent;
            // Grip the top rail or nearest rim, never the centre of the dish.
            var handle=chair?new Vector3(bounds.center.x,maxBound.y-bounds.size.y*.045f,minBound.z+bounds.size.z*.10f)
                :new Vector3(bounds.center.x,bounds.center.y,minBound.z+bounds.size.z*.045f);
            propRotation=chair?Quaternion.Euler(215,0,0):Quaternion.identity;
            var fingerOffset=chair?new Vector3(0,-.014f,.025f):new Vector3(0,-.011f,.065f);
            if(ControlledPropGrip)
            {
                var configured=PropGripGeometry.Fit(propGrip,new System.Numerics.Vector3(minBound.x,minBound.y,minBound.z),new System.Numerics.Vector3(maxBound.x,maxBound.y,maxBound.z));
                handle=new Vector3(configured.handle.X,configured.handle.Y,configured.handle.Z);
                if(chair)
                {
                    PropMeshData(parts,Matrix4x4.identity,out var v,out var t);
                    if(PropSurfaceGeometry.ChairRail(v,t,out var rail,out float railThickness)){handle=new Vector3(rail.X,rail.Y,rail.Z);PropGripThickness=railThickness*scale;}
                    else throw new InvalidOperationException("Chair top rail surface unavailable");
                }
                var q=configured.rotation;propRotation=new Quaternion(q.X,q.Y,q.Z,q.W);
                fingerOffset=new Vector3(configured.contact.X,configured.contact.Y,configured.contact.Z);
                PropGripContact=fingerOffset;
                if(propGrip.Contains("ashtray"))PropGripThickness=bounds.size.z*scale;
            }
            var translation=fingerOffset-(propRotation*(handle*scale));
            var tip=translation+propRotation*(bounds.center*scale);
            fit=new WeaponFit(scale,new System.Numerics.Vector3(translation.x,translation.y,translation.z),new System.Numerics.Vector3(tip.x,tip.y,tip.z));
        }
        MuzzleOffset = new Vector3(fit.Muzzle.X,fit.Muzzle.Y,fit.Muzzle.Z);
        fitScale=fit.Scale;
        if(!EquipmentProfile.Manual(profile))ContactShape=ContactSolver.Box(fit.Point(new System.Numerics.Vector3(minBound.x,minBound.y,minBound.z)),fit.Point(new System.Numerics.Vector3(maxBound.x,maxBound.y,maxBound.z)));
        fitMatrix=Matrix4x4.TRS(new Vector3(fit.Translation.X,fit.Translation.Y,fit.Translation.Z),propRotation,Vector3.one*fit.Scale);
        ItemCenter=fitMatrix.MultiplyPoint3x4(bounds.center);
        if(profile=="prop"&&IsChair)
        {
            PropMeshData(parts,fitMatrix,out var chairPoints,out var chairTriangles);
            try
            {
                var watch=System.Diagnostics.Stopwatch.StartNew();
                ChairSurface=ChairGripSurface.Measure(chairPoints,chairTriangles,new System.Numerics.Vector3(PropGripContact.x,PropGripContact.y,PropGripContact.z));
                Bootstrap.Write("CHAIR SURFACE measured sections="+ChairSurface.Count+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F1"));
            }
            catch(InvalidOperationException ex){Bootstrap.Warn("CHAIR SURFACE keeping central rail fit: "+ex.Message);}
        }
        if(profile=="prop"&&propGrip.Contains("ashtray"))
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();
            MeasureAshtrayRim(parts);float rimMs=(float)watch.Elapsed.TotalMilliseconds;watch.Restart();
            // 0.1.81: sections of the real plate around the rim contact for the
            // whole-hand grip (same solver as the chair board).
            PropMeshData(parts,fitMatrix,out var trayPoints,out var trayTriangles);
            try
            {
                ChairSurface=ChairGripSurface.Measure(trayPoints,trayTriangles,new System.Numerics.Vector3(PropGripContact.x,PropGripContact.y,PropGripContact.z),.004f);
                Bootstrap.Write("ASHTRAY SURFACE measured sections="+ChairSurface.Count+" rail=["+(ChairSurface.MinX*1000).ToString("F0")+".."+(ChairSurface.MaxX*1000).ToString("F0")+"] rimMs="+rimMs.ToString("F1")+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F1"));
            }
            catch(InvalidOperationException ex){ChairSurface=null;Bootstrap.Warn("ASHTRAY SURFACE keeping rim pinch: "+ex.Message);}
        }
        // One compact section dump per prop model and session, so a grip can
        // be reproduced offline from the log.
        if(ChairSurface!=null&&dumpedSections.Add(propGrip))
        {
            float mid=(ChairSurface.MinX+ChairSurface.MaxX)*.5f;
            Bootstrap.Write("PROP SECTIONS "+propGrip+" rail=["+(ChairSurface.MinX*1000).ToString("F0")+".."+(ChairSurface.MaxX*1000).ToString("F0")+"]"
                +ChairSurface.Describe(new[]{-.048f,-.032f,-.016f,0f,.016f,.032f,.048f}.Select(x=>mid+x).ToArray()));
        }
        root = new GameObject("XIII VR Weapon Visual " + profile); root.SetActive(false);
        if(profile=="prop"||profile=="revolver")
        {
            var a=fitMatrix.MultiplyPoint3x4(minBound);var b=fitMatrix.MultiplyPoint3x4(maxBound);
            PropCenter=fitMatrix.MultiplyPoint3x4(bounds.center);
            PropRadius=Math.Clamp(Math.Min(bounds.size.x,Math.Min(bounds.size.y,bounds.size.z))*fit.Scale*.5f,.022f,.15f);
            // Build contact from occupied mesh cells, not a solid box spanning chair legs.
            var points=new List<Vector3>();
            foreach(var part in parts)
            {
                var map=fitMatrix*part.Matrix;
                try{foreach(var vertex in part.Mesh.vertices)points.Add(map.MultiplyPoint3x4(vertex));}
                catch(Exception ex)
                {Bootstrap.Warn("PROP collision vertices unavailable: "+ex.Message);var box=part.Mesh.bounds;
                    for(int i=0;i<8;i++)points.Add(map.MultiplyPoint3x4(new Vector3((i&1)==0?box.min.x:box.max.x,(i&2)==0?box.min.y:box.max.y,(i&4)==0?box.min.z:box.max.z)));}
            }
            // Bound query cost as well as preserving the open gaps between legs.
            var cells=new Dictionary<(int,int,int),Vector3>();float cell=profile=="revolver"?.016f:.075f;
            for(int pass=0;pass<8;pass++)
            {cells.Clear();foreach(var point in points){var key=((int)Math.Floor(point.x/cell),(int)Math.Floor(point.y/cell),(int)Math.Floor(point.z/cell));
                if(!cells.ContainsKey(key))cells[key]=new Vector3((key.Item1+.5f)*cell,(key.Item2+.5f)*cell,(key.Item3+.5f)*cell);}
                if(cells.Count<=(profile=="revolver"?128:64))break;cell*=1.18f;}
            var contacts=new List<ContactSphere>();foreach(var point in cells.Values)contacts.Add(new ContactSphere(ContactWorld.V(point),cell*.72f));
            ContactShape=contacts.Count>0?contacts.ToArray():ContactSolver.Box(ContactWorld.V(Vector3.Min(a,b)),ContactWorld.V(Vector3.Max(a,b)));
            LongGripOrigin=null;LongHeadEnd=null;
            if(profile=="prop"&&PropFitPolicy.LongHandle(propGrip)&&points.Count>16)MeasureLongHandle(points);
        }
        // 0.1.117: guns without a hand-made contact shape (M16, crossbow, Uzi,
        // M60, bazooka...) collided as a few fat spheres around their whole
        // bounding box (up to ~20 cm across). Use the occupied mesh cells
        // instead, like props: the gun collides where it actually has metal.
        if(EquipmentProfile.TightContact(profile))
        {
            try
            {
                var tight=MeshCellShape(parts,.035f,96,out float cell);
                if(tight!=null){ContactShape=tight;Bootstrap.Write("WEAPON CONTACT "+profile+" mesh cells="+tight.Length+" cell="+cell.ToString("F3")+" radius="+(cell*.72f).ToString("F3"));}
            }
            catch(Exception ex){Bootstrap.Warn("WEAPON CONTACT "+profile+" keeping box shape: "+ex.Message);}
        }
        // 0.1.122: the muzzle flash at the gun's own muzzle point (the native
        // muzzle transform, the origin of the barrel frame), not at the top
        // front of the bounding box: on the M60 (box and sight above the
        // barrel) the flash hung in the air above the barrel.
        var native=fitMatrix.MultiplyPoint3x4(Vector3.zero);
        EffectMuzzle=EquipmentProfile.RequiresMuzzle(profile)&&MuzzleMath.Plausible(ContactWorld.V(native),ContactWorld.V(MuzzleOffset))?native:MuzzleOffset;
        // 0.1.194: the silenced pistol's flash at the silencer's front.
        if(attachmentFront is Vector3 end){var tip=fitMatrix.MultiplyPoint3x4(end);if(Finite(tip)&&tip.z>EffectMuzzle.z&&tip.z-EffectMuzzle.z<.15f)EffectMuzzle=new Vector3(EffectMuzzle.x,EffectMuzzle.y,tip.z);}
        if(EffectMuzzle!=MuzzleOffset)Bootstrap.Write("MUZZLE point "+profile+" native="+native.ToString("F3")+" (aim point "+MuzzleOffset.ToString("F3")+")");
        var muzzlePoint=new GameObject("XIII VR effect muzzle");
        muzzlePoint.transform.SetParent(root.transform,false); muzzlePoint.transform.localPosition=EffectMuzzle;
        MuzzleAnchor=muzzlePoint.transform;
        var geometry = new GameObject("Geometry"); geometry.transform.SetParent(root.transform,false);
        geometry.transform.localPosition = new Vector3(fit.Translation.X,fit.Translation.Y,fit.Translation.Z);
        geometry.transform.localScale = Vector3.one*fit.Scale;geometry.transform.localRotation=propRotation;
        geometryRoot=geometry.transform;geometryMirrored=false;
        foreach (var part in parts)
        {
            var go = new GameObject(part.Source.name + " VR"); go.layer = 0;
            go.transform.SetParent(geometry.transform,false);
            var m = part.Matrix;
            go.transform.localPosition = new Vector3(m.m03,m.m13,m.m23);
            go.transform.localRotation = m.rotation; go.transform.localScale = m.lossyScale;
            var filter = go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>();
            var renderer = go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>();
            if (filter == null || renderer == null) throw new InvalidOperationException("Could not create render-only weapon mesh");
            filter.sharedMesh = part.Mesh; renderer.sharedMaterials = part.Source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            part.Copy=go.transform; part.Filter=filter; animatedParts.Add(part);
            if(part.Skin!=null) part.Mechanism=new WeaponMechanism(part.Skin,profile);
            // 0.1.195: the Uzi's top handle drawn back with its bolt.
            if(profile=="uzi"&&part.Mechanism!=null&&part.Snapshot!=null)PrepareKnob(part);
        }
        if(profile!="prop"){TryEnableAnimation(weapon);TryBuildFlash();}
        Bootstrap.Write("WEAPON VISUAL built profile=" + profile + " parts=" + parts.Count + " canonical="+AuthoredGripAvailable+" rawSize=" + bounds.size.ToString("F6") + " fittedLength=" + (bounds.size.z*fit.Scale) + " muzzleOffset=" + MuzzleOffset);
    }
    // The native first-person pose holds a broom in the middle. Find the
    // handle end (the thin end; the brush/mop head is the wide one) and the
    // stick axis, so the hand can hold it near the end.
    private void MeasureLongHandle(List<Vector3> points)
    {
        float zMin=float.PositiveInfinity,zMax=float.NegativeInfinity;
        foreach(var p in points){zMin=Math.Min(zMin,p.z);zMax=Math.Max(zMax,p.z);}
        float length=zMax-zMin;if(length<.5f)return;float window=length*.12f;
        float Spread(bool low)
        {
            float x0=float.PositiveInfinity,x1=float.NegativeInfinity,y0=float.PositiveInfinity,y1=float.NegativeInfinity;
            foreach(var p in points){if(low?p.z>zMin+window:p.z<zMax-window)continue;x0=Math.Min(x0,p.x);x1=Math.Max(x1,p.x);y0=Math.Min(y0,p.y);y1=Math.Max(y1,p.y);}
            return float.IsFinite(x0)?Math.Max(x1-x0,y1-y0):float.PositiveInfinity;
        }
        float lowSpread=Spread(true),highSpread=Spread(false);
        bool handleLow=lowSpread<=highSpread;float end=handleLow?zMin:zMax;
        Vector3 Centroid(float from,float to){var sum=Vector3.zero;int n=0;foreach(var p in points)if(p.z>=from&&p.z<=to){sum+=p;n++;}return n>0?sum/n:new Vector3(float.NaN,0,0);}
        var cEnd=handleLow?Centroid(zMin,zMin+window):Centroid(zMax-window,zMax);
        var cMid=Centroid(zMin+length*.30f,zMin+length*.70f);
        // A one-segment stick has no vertices mid-way: use the head end.
        if(float.IsNaN(cMid.x))cMid=handleLow?Centroid(zMax-window,zMax):Centroid(zMin,zMin+window);
        if(float.IsNaN(cEnd.x)||float.IsNaN(cMid.x))return;
        var axis=(cMid-cEnd).normalized;if(Math.Abs(axis.z)<.8f)return;
        var endPoint=cEnd-axis*((cEnd.z-end)/axis.z);
        LongGripOrigin=endPoint+axis*.15f;LongGripAxis=axis;LongHeadEnd=endPoint+axis*(length/Math.Max(.8f,Math.Abs(axis.z)));
        Bootstrap.Write("PROP LONG HANDLE "+propGrip+" handleEnd="+(handleLow?"min":"max")+" spread="+lowSpread.ToString("F3")+"/"+highSpread.ToString("F3")+" grip="+LongGripOrigin.Value.ToString("F3")+" axis="+axis.ToString("F3")+" length="+length.ToString("F2"));
    }
    // 0.1.146: the collision of this gun held as a still copy (the second gun
    // in the other hand): its mesh cells, 2.5 cm and up (at most 80), each
    // round its own points; the gun's own shape if the mesh cannot be read.
    private ContactSphere[]? heldShape;private bool heldShapeTried;
    internal ContactSphere[]? HeldShape()
    {
        if(heldShapeTried)return heldShape;
        heldShapeTried=true;
        try
        {
            heldShape=MeshCellShape(animatedParts,.025f,80,out float cell);
            if(heldShape!=null)Bootstrap.Write("WEAPON CONTACT "+Profile+" held copy: mesh cells="+heldShape.Length+" cell="+cell.ToString("F3"));
        }
        catch(Exception ex){heldShape=null;Bootstrap.Warn("WEAPON CONTACT "+Profile+" held copy: "+ex.Message);}
        heldShape??=ContactShape??(HolsterLayout.Firearm(Profile)?ContactSolver.Weapon(Profile):null);
        return heldShape;
    }
    // Occupied cells of the fitted mesh, grown until at most maxCells remain;
    // one sphere per cell (radius 0.72 cell). Null when there are no vertices.
    private ContactSphere[]? MeshCellShape(List<Part> parts,float start,int maxCells,out float cell)
    {
        var points=new List<System.Numerics.Vector3>();
        foreach(var part in parts)
        {
            var map=fitMatrix*part.Matrix;
            foreach(var vertex in part.Mesh.vertices){var q=map.MultiplyPoint3x4(vertex);if(Finite(q))points.Add(new System.Numerics.Vector3(q.x,q.y,q.z));}
        }
        return ContactSolver.Cells(points,start,maxCells,out cell);
    }
    private void MeasureAshtrayRim(List<Part> parts)
    {
        try
        {
            PropMeshData(parts,fitMatrix,out var vertices,out var triangles);
            if(AshtrayRimGeometry.TryMeasure(vertices,triangles,PropGripContact.x,out var edge,out float thickness))
            {
                PropGripContact=new Vector3(edge.X,edge.Y,edge.Z);PropGripThickness=thickness;
                Bootstrap.Write("ASHTRAY RIM mesh surface edge="+PropGripContact.ToString("F6")+" thickness="+thickness.ToString("F6"));
            }
            else Bootstrap.Warn("ASHTRAY RIM surface sample unavailable; using fitted bounds");
        }
        catch(Exception ex){Bootstrap.Warn("ASHTRAY RIM sample: "+ex.Message);}
    }
    internal bool PrepareReload()
    {
        if(reloadProbeDone||Time.realtimeSinceStartup<nextReloadProbe)return reloadMesh!=null;reloadProbeDone=true;
        try
        {
            foreach(var part in animatedParts)
            {
                if(part.Snapshot==null)continue;
                Bootstrap.Write("RELOAD MESH probe profile="+Profile+" readable="+part.Snapshot.Original.isReadable+" vertices="+part.Mesh.vertexCount);
                var bones=part.Snapshot.Bones;var boneNames=new string?[bones.Length];
                for(int i=0;i<bones.Length;i++)boneNames[i]=bones[i]!=null?bones[i].name:null;
                // 0.1.117: bone roles by name, including the M16 and the crossbow's bolt.
                var found=ReloadBones.Find(Profile,boneNames);int mag=found.Ammo,bolt=found.Bolt,receiver=found.Receiver,weaponRoot=found.Root;
                Bootstrap.Write("RELOAD MESH bones "+Profile+" root="+Name(weaponRoot)+" ammunition="+Name(mag)+" bolt="+Name(bolt));
                string Name(int i)=>i>=0&&i<boneNames.Length?boneNames[i]??"?":"none";
                if(mag<0){Bootstrap.Warn("RELOAD MESH magazine/shell bone absent: "+Profile);continue;}
                var weights=part.Snapshot.Original.boneWeights;if(weights.Length!=part.Mesh.vertexCount)throw new InvalidOperationException("Reload weights="+weights.Length+" vertices="+part.Mesh.vertexCount);
                // A crossbow bolt may carry the line/string bones as children:
                // only the bolt itself (and bones named as part of it) is ammunition.
                bool arrow=EquipmentProfile.Arrow(Profile);
                // 0.1.164: decided once per bone. Asked per vertex (four times
                // each, with the bone's name read from the game every time),
                // it took 40-90 ms whenever a gun was taken (a hitch each time).
                var ammoBone=new bool[bones.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    if(bones[i]==null)continue;string lower=(boneNames[i]??"").ToLowerInvariant();
                    ammoBone[i]=i==mag||bones[i].IsChildOf(bones[mag])&&(!arrow||lower.Contains("arrow"))||ReloadBones.AmmunitionPart(Profile,lower);
                }
                bool Matches(int i)=>i>=0&&i<ammoBone.Length&&ammoBone[i];
                if(arrow)
                {
                    var restore=new List<int>();for(int i=0;i<bones.Length;i++)if(Matches(i))restore.Add(i);
                    arrowBones=restore.ToArray();arrowReference=weaponRoot;arrowPart=part;
                    var names=new string[arrowBones.Length];for(int k=0;k<names.Length;k++)names[k]=boneNames[arrowBones[k]]??"";
                    arrowRelation=LoadedAtBuild?part.Snapshot.LoadedRelation(weaponRoot,arrowBones):null;string from="the loaded crossbow when taken";
                    if(arrowRelation!=null)SaveBolt(names,arrowRelation);
                    else{arrowRelation=LoadBolt(names);from=arrowRelation!=null?"the saved placement":"the rig's bind pose (no loaded crossbow seen yet)";}
                    Bootstrap.Write("CROSSBOW bolt bones="+arrowBones.Length+" held relative to "+Name(weaponRoot)+"; rail placement from "+from+" (loaded when taken="+LoadedAtBuild+")");
                }
                var selected=new bool[weights.Length];
                for(int i=0;i<weights.Length;i++){var w=weights[i];selected[i]=(Matches(w.boneIndex0)?w.weight0:0)+(Matches(w.boneIndex1)?w.weight1:0)+(Matches(w.boneIndex2)?w.weight2:0)+(Matches(w.boneIndex3)?w.weight3:0)>.5f;}
                var attachment=new Mesh();
                try
                {
                    if(arrow&&arrowRelation!=null&&weaponRoot>=0)part.Snapshot.BakeRelation(attachment,part.Snapshot.Initial??part.Snapshot.Live,weaponRoot,arrowBones!,arrowRelation);
                    else part.Snapshot.BakeAttachment(attachment,weaponRoot);
                    reloadMesh=new ReloadMesh(attachment,part.Source.sharedMaterials,fitMatrix*part.Matrix,selected,EquipmentProfile.Arrow(Profile)?.95f:.55f);
                    if(found.Cover>=0)PrepareCover(part,bones,found.Cover,attachment,weights);
                    // 0.1.123: the string, drawn back by the bolt.
                    if(arrow&&weaponRoot>=0)
                    {
                        try{PrepareString(part,boneNames,weaponRoot);}
                        catch(Exception ex){stringBones=null;Bootstrap.Warn("CROSSBOW string stays the game's: "+ex.Message);}
                    }
                    if(bolt>=0)
                    {
                        try
                        {
                            var set=new HashSet<int>();for(int i=0;i<bones.Length;i++)if(bones[i]!=null&&(i==bolt||bones[i].IsChildOf(bones[bolt])))set.Add(i);
                            if(BoltShape!=null)UnityEngine.Object.Destroy(BoltShape);
                            BoltShape=ExtractPart(attachment,fitMatrix*part.Matrix,Weighted(weights,set),true,out var boltCenter,"XIII bolt outline source "+Profile);BoltShapeCenter=boltCenter;
                        }
                        catch(Exception ex){BoltShape=null;Bootstrap.Warn("RELOAD OUTLINE bolt shape unavailable: "+ex.Message);}
                    }
                }
                finally{UnityEngine.Object.Destroy(attachment);}
                reloadGrip=ReloadGripMath.Fit(Profile,ContactWorld.V(reloadMesh.Min),ContactWorld.V(reloadMesh.Max),reloadMesh.GripPoints);
                ReloadGripGeometry.Set(Profile,reloadGrip);
                // The socket entrance is the exposed bottom of the installed
                // magazine, not its feed end buried inside the receiver.
                ReloadPort=reloadMesh.Center+new Vector3(0,reloadMesh.Min.y+.010f,0);
                if(EquipmentProfile.RifleMagazine(Profile))
                {
                    // An AK enters the receiver at the feed lips, not at the
                    // removed magazine's bottom. Average only its upper band.
                    var sum=Vector3.zero;int count=0;
                    foreach(var vertex in reloadMesh.GripPoints)if(vertex.Y>reloadMesh.Max.y-.008f){sum+=ContactWorld.U(vertex);count++;}
                    if(count>0)ReloadPort=reloadMesh.Center+sum/count-Vector3.up*.010f;
                }
                InsertAxis=EquipmentProfile.SingleRound(Profile)?Vector3.forward:Vector3.up;
                // 0.1.117: a crossbow bolt is pushed forward along the rail
                // until its point reaches where the loaded bolt's point sits.
                // 0.1.122: it is laid on the rail at the muzzle end and drawn
                // back until its rear reaches the string (ArrowRear).
                if(EquipmentProfile.Arrow(Profile))ReloadPort=ArrowRear;
                // 0.1.119: the M60 box (with its belt) is pushed up into place at its centre.
                if(EquipmentProfile.Lidded(Profile))ReloadPort=reloadMesh.Center;
                reloadHint=new ReloadHint(root!.transform);
                ReloadBolt=bolt>=0?GameWorldToFitted.MultiplyPoint3x4(bones[bolt].position):new Vector3(0,.02f,Profile=="shotgun"?.22f:0);
                if(Profile=="shotgun")
                {
                    // The spare animated shell rests by the stock. Use the
                    // tube/receiver bone, never that shell's parked position.
                    var port=receiver>=0?GameWorldToFitted.MultiplyPoint3x4(bones[receiver].position):new Vector3(0,-.035f,ReloadBolt.z-.18f);
                    port.z=Math.Clamp(port.z,ReloadBolt.z-.19f,ReloadBolt.z-.085f);
                    port.y=Math.Min(port.y,-.025f);ReloadPort=port;
                }
                // The shotgun shell is an animation prop embedded in the skin.
                // Extract it for the hand, then always remove it from the gun.
                {
                    magazinePart=part;magazineKept=new int[part.Mesh.subMeshCount][];magazineFull=new int[part.Mesh.subMeshCount][];
                    for(int sub=0;sub<magazineKept.Length;sub++)
                    {
                        int[] triangles=part.Mesh.GetTriangles(sub);magazineFull[sub]=triangles;var keep=new List<int>();
                        for(int i=0;i+2<triangles.Length;i+=3)if(!(selected[triangles[i]]&&selected[triangles[i+1]]&&selected[triangles[i+2]]))
                        {keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);}
                        magazineKept[sub]=keep.ToArray();
                    }
                }
                Bootstrap.Write("MANUAL RELOAD native mesh="+Profile+" port="+ReloadPort.ToString("F4")+" bolt="+ReloadBolt.ToString("F4")+" ammoMin="+reloadGrip.Min+" ammoMax="+reloadGrip.Max+" feed="+reloadGrip.Forward+" tip="+reloadGrip.Tip);return true;
            }
            if(EquipmentProfile.ManualFallback(Profile)){ReloadUnavailable=true;Bootstrap.Warn("MANUAL RELOAD no separable native ammunition mesh for "+Profile+"; the game's own reload stays in use");}
            else Bootstrap.Warn("MANUAL RELOAD no separable native ammunition mesh for "+Profile+"; manual mode blocks automatic reload; extraction requires investigation");
        }
        catch(Exception ex)
        {
            reloadHint?.Dispose();reloadHint=null;reloadMesh?.Dispose();reloadMesh=null;reloadProbeDone=false;nextReloadProbe=Time.realtimeSinceStartup+2;
            // 0.1.117: the newer manual guns give up after three failures and keep the game's reload.
            if(EquipmentProfile.ManualFallback(Profile)&&++reloadFailures>=3){reloadProbeDone=true;ReloadUnavailable=true;Bootstrap.Warn("MANUAL RELOAD "+Profile+" unavailable after "+reloadFailures+" attempts; the game's own reload stays in use: "+ex.Message);}
            else Bootstrap.Warn("MANUAL RELOAD mesh unavailable; retry scheduled: "+ex.Message);
        }
        return false;
    }
    private float nextReloadProbe;
    internal void AutoMagazine(float age)
    {
        if(reloadMesh==null)return;
        bool active=age>=0&&age<.80f;
        if(magazineHidden!=active){magazineHidden=active;animatedFrame=-1;}
        if(!active){reloadMesh.Hide();return;}
        // Guns and wrists remain tracked. Only the detached magazine travels.
        float offset=age<.30f?age/.30f*.22f:(1-Mathf.Clamp01((age-.30f)/.50f))*.22f;
        reloadMesh.Pose(FittedToWorld.MultiplyPoint3x4(reloadMesh.Center-Vector3.up*offset),FittedToWorld.rotation);
    }
    internal void ReloadPose(bool holding,bool magOut,bool hint,float rack,Vector3 hand,Quaternion rotation,bool empty=false)
    {
        slideLocked=empty&&Profile=="pistol";
        bool hide=magOut&&Profile!="shotgun";
        if(magazineHidden!=hide||Math.Abs(manualRack-rack)>.0005f){magazineHidden=hide;manualRack=rack;animatedFrame=-1;}
        // GloveVisual owns the final collision-constrained pose, once per frame.
        // Reposing from raw tracking in each eye/camera callback bypassed it.
        if(!holding)reloadMesh?.Hide();
        reloadHint?.Show(Profile!="shotgun"&&hint,MagazineCenter);
    }
    // 0.1.133: right: held by the right hand (the reload of a gun in the left
    // hand), the left hand's hold mirrored.
    private static System.Numerics.Vector3 Held(System.Numerics.Vector3 v,bool right)=>right?new(-v.X,v.Y,v.Z):v;
    private static Quaternion Held(System.Numerics.Quaternion q,bool right)=>right?new Quaternion(q.X,-q.Y,-q.Z,q.W):new Quaternion(q.X,q.Y,q.Z,q.W);
    internal void PoseAmmunition(Vector3 hand,Quaternion rotation,bool right=false)
    {
        if(reloadMesh==null||reloadGrip==null)return;
        reloadMesh.Pose(hand+rotation*ContactWorld.U(Held(reloadGrip.Position,right)),rotation*Held(reloadGrip.Rotation,right));
    }
    internal Vector3 AmmunitionTip(Vector3 hand,Quaternion rotation,bool right=false)=>hand+rotation*ContactWorld.U(Held(reloadGrip?.Tip??System.Numerics.Vector3.Zero,right));
    internal Vector3 AmmunitionForward(Quaternion rotation,bool right=false)=>rotation*ContactWorld.U(Held(reloadGrip?.Forward??System.Numerics.Vector3.UnitZ,right));
    internal void AmmunitionPose(Vector3 hand,Quaternion rotation,out Vector3 position,out Quaternion q,bool right=false)
    {
        position=hand+rotation*ContactWorld.U(Held(reloadGrip?.Position??System.Numerics.Vector3.Zero,right));
        q=rotation*Held(reloadGrip?.Rotation??System.Numerics.Quaternion.Identity,right);
    }
    private void MaskMagazine(Part part)
    {
        if(part!=magazinePart||magazineKept==null||magazineFull==null)return;
        var indices=magazineHidden||Profile=="shotgun"?magazineKept:magazineFull;
        for(int sub=0;sub<indices.Length;sub++)part.Spare!.SetTriangles(indices[sub],sub,false,0);
    }
    private static int Lod(string name)
    {
        int at = name.IndexOf("lod",StringComparison.Ordinal);
        return at >= 0 && at+3 < name.Length && char.IsDigit(name[at+3]) ? name[at+3]-'0' : -1;
    }
    private static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
    private void TryEnableAnimation(Equipable weapon)
    {
        try
        {
            if(animationOwned)return;animationOwned=true;
            foreach(var part in animatedParts)
            {
                if(part.Skin==null) continue;
                part.WasOffscreen=part.Skin.updateWhenOffscreen;
                if(part.Spare==null){part.Spare=new Mesh(); baked.Add(part.Spare);}
                part.Skin.updateWhenOffscreen=true;
            }
            weaponAnimation=weapon.GetComponent(Il2CppType.Of<PlayMagic.Weapons.AnimationComponent>())?.TryCast<PlayMagic.Weapons.AnimationComponent>();
            if(weaponAnimation!=null)
            {
                oldOffscreen=weaponAnimation.updateWhenOffscreen; weaponAnimation.updateWhenOffscreen=true;
                TrackAnimator(weaponAnimation.weaponAnimator,true);
            }
            foreach(var component in weapon.GetComponentsInParent(Il2CppType.Of<Animator>(),true))
            {
                var animator=component.TryCast<Animator>(); if(animator==null) continue;
                TrackAnimator(animator,false);
            }
            foreach(var component in weapon.GetComponentsInChildren(Il2CppType.Of<Animator>(),true)) TrackAnimator(component.TryCast<Animator>(),false);
            Bootstrap.Write("WEAPON ANIMATION late-render snapshots; native animators="+animators.Count+" directWeaponAnimator="+(weaponAnimation!=null && weaponAnimation.weaponAnimator!=null));
        }
        catch(Exception ex) { animationFailed=true; RestoreAnimation(); Bootstrap.Warn("WEAPON ANIMATION static fallback; recoil remains active. "+ex.Message); }
    }
    private void TrackAnimator(Animator? animator,bool enable)
    {
        if(animator==null) return;
        foreach(var a in animators) if(a.animator==animator) return;
        animators.Add((animator,animator.cullingMode,animator.enabled));
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        if(enable) animator.enabled=true;
        Bootstrap.Write("WEAPON ANIMATOR bound="+animator.name+" enabled="+animator.enabled+" layers="+animator.layerCount);
    }
    private void RefreshAnimation()
    {
        if(sourceRoot==null || animatedFrame==Time.frameCount || (animationFailed&&Time.realtimeSinceStartup<animationRetryAt))return;
        animationFailed=false;
        animatedFrame=Time.frameCount;
        try
        {
            // Cancel parent/body motion but retain internal slide/bolt/magazine
            // deformation. Keep the fitted scale fixed; never re-fit every frame.
            var frame=(sourceRoot.localToWorldMatrix*rootToBarrel).inverse;
            bool still=HeldStill;if(still)FindRocket();
            TickCover();
            foreach(var part in animatedParts)
            {
                if(part.Source==null || part.Copy==null || part.Filter==null) continue;
                // 0.1.229: the bazooka held still in its fitted frame (HeldStill).
                var matrix=StableGun||still?part.Matrix:frame*part.Source.localToWorldMatrix;
                Vector3 p=new(matrix.m03,matrix.m13,matrix.m23),scale=matrix.lossyScale;
                var initial=new Vector3(part.Matrix.m03,part.Matrix.m13,part.Matrix.m23);
                float travel=Math.Max(.1f,part.InitialBounds.size.magnitude*part.Matrix.lossyScale.magnitude*2);
                if(!Finite(p) || !Finite(scale) || (p-initial).magnitude>travel || scale.magnitude>part.Matrix.lossyScale.magnitude*3)
                    throw new InvalidOperationException("Animated weapon transform outside its fitted bounds");
                if(part.Skin!=null && part.Spare!=null)
                {
                    bool owned=false;
                    if(part.Snapshot!=null)
                    {
                        part.Snapshot.Invalidate();
                        // 0.1.194: the double-barrelled shotgun held still, its barrels opening by hand.
                        if(StableGun&&BreakAction&&part==breakPart)owned=BakeBreak(part);
                        if(!owned&&still&&part.Snapshot.Initial!=null)
                        {
                            bool rocketHere=part==rocketPart&&rocketMask!=null;
                            part.Snapshot.BakeStill(part.Spare,rocketHere?rocketMask:null,rocketHere?rocketReference:-1);owned=true;
                        }
                        if(!owned&&(StableGun||ManualMode&&(ReloadAvailable||Profile=="shotgun")))
                        {
                            float cycle=Profile=="shotgun"?0:MechanismMath.Travel(Profile)*MechanismMath.Cycle(Time.realtimeSinceStartup-lastShot,Profile);
                            float mechanismTravel=Math.Max(manualRack,slideLocked?.045f:cycle);
                            owned=part.Mechanism?.BakeOwned(part.Snapshot,part.Spare,matrix,fitScale,mechanismTravel,StableGun)==true;
                        }
                        // 0.1.120: the crossbow is baked live (its bind pose hangs it
                        // straight down) with only its bolt put back on the rail.
                        if(!owned&&ManualMode&&part==arrowPart&&arrowBones!=null&&arrowReference>=0)part.Snapshot.BakeRestored(part.Spare,arrowReference,arrowBones,arrowRelation,stringBones,StringPose());
                        else if(!owned)part.Snapshot.Bake(part.Spare,StableGun||ManualMode&&Profile=="shotgun");
                    }
                    else part.Skin.BakeMesh(part.Spare);
                    if(!owned)part.Mechanism?.Apply(part.Spare,matrix,fitScale,ManualMode&&Profile=="shotgun"?float.NegativeInfinity:Time.realtimeSinceStartup-lastShot,Time.realtimeSinceStartup,manualRack);
                    var bounds=part.Spare.bounds; var baseline=part.InitialBounds;
                    float span=Math.Max(.001f,baseline.size.magnitude);
                    if(part.Spare.vertexCount!=part.Mesh.vertexCount || !Finite(bounds.center) || !Finite(bounds.size)
                        || bounds.size.magnitude>span*3 || (bounds.center-baseline.center).magnitude>span*2)
                        throw new InvalidOperationException("Animated mesh outside its fitted bounds");
                    MaskMagazine(part);
                    var old=part.Mesh; part.Mesh=part.Spare; part.Spare=old; part.Filter.sharedMesh=part.Mesh;
                }
                part.Copy.localPosition=p; part.Copy.localRotation=matrix.rotation; part.Copy.localScale=scale;
            }
            if(Time.realtimeSinceStartup>=nextAnimationReport)
            { nextAnimationReport=Time.realtimeSinceStartup+10; Bootstrap.Write("WEAPON ANIMATION live snapshot frame="+animatedFrame+" parts="+animatedParts.Count); }
        }
        catch(Exception ex)
        {
            animationFailed=true;animationRetryAt=Time.realtimeSinceStartup+.5f;
            if(Time.realtimeSinceStartup>=nextAnimationReport)
            {nextAnimationReport=Time.realtimeSinceStartup+5;Bootstrap.Warn("WEAPON ANIMATION temporary snapshot failure; retry scheduled. "+ex.Message);}
        }
    }
    private void RestoreAnimation()
    {
        if(!animationOwned)return;animationOwned=false;
        try { if(weaponAnimation!=null) weaponAnimation.updateWhenOffscreen=oldOffscreen; } catch(Exception ex) { Bootstrap.Warn("Weapon animation flag restore: "+ex.Message); }
        foreach(var part in animatedParts)
        {
            try { if(part.Skin!=null && part.Spare!=null) part.Skin.updateWhenOffscreen=part.WasOffscreen; }
            catch(Exception ex) { Bootstrap.Warn("Weapon skin culling restore: "+ex.Message); }
        }
        foreach(var entry in animators)
        {
            try { if(entry.animator!=null) { entry.animator.cullingMode=entry.mode; entry.animator.enabled=entry.enabled; } }
            catch(Exception ex) { Bootstrap.Warn("Weapon animator culling restore: "+ex.Message); }
        }
        animators.Clear();
    }
    private void TryBuildFlash()
    {
        try
        {
            flash=GameObject.CreatePrimitive(PrimitiveType.Sphere); flash.name="XIII VR muzzle flash";
            flash.SetActive(false); flash.transform.SetParent(root!.transform,false);
            flash.transform.localPosition=EffectMuzzle+Vector3.forward*.025f;
            flash.transform.localScale=new Vector3(.022f,.022f,.07f);
            var collider=flash.GetComponent(Il2CppType.Of<Collider>())?.TryCast<Collider>();
            if(collider!=null) { collider.enabled=false; UnityEngine.Object.Destroy(collider); }
            var shader=Shader.Find("Unlit/Color")??Shader.Find("Standard")??Shader.Find("Legacy Shaders/Diffuse");
            var renderer=flash.GetComponent(Il2CppType.Of<Renderer>())?.TryCast<Renderer>();
            if(shader==null || renderer==null) throw new InvalidOperationException("Muzzle flash shader unavailable");
            flashMaterial=new Material(shader); flashMaterial.color=new Color(1f,.65f,.12f,1f);
            if(flashMaterial.HasProperty("_EmissionColor")){flashMaterial.EnableKeyword("_EMISSION");flashMaterial.SetColor("_EmissionColor",new Color(3f,1.4f,.15f,1));}
            renderer.material=flashMaterial; renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        catch(Exception ex)
        { if(flash!=null) UnityEngine.Object.Destroy(flash); flash=null; Bootstrap.Warn("Muzzle flash unavailable; recoil remains active. "+ex.Message); }
    }
    internal void Pose(Vector3 position,Quaternion rotation,bool firing,bool rendering)
    {
        if (root == null) return;
        if(rendering&&Profile!="prop") RefreshAnimation();
        if(Profile=="shotgun")TickCasings();
        if(Profile=="prop")foreach(var part in animatedParts)
        {if(part.Source==null)continue;if(!propSources.ContainsKey(part.Source))propSources[part.Source]=part.Source.enabled;part.Source.enabled=false;}
        root.transform.SetPositionAndRotation(position,rotation); root.SetActive(true);
        if(flash!=null) flash.SetActive(firing && Time.realtimeSinceStartup>=nativeFlashUntil);
        if(rendering)TickScope();
    }
    internal bool Matches(Equipable item,Transform muzzle)
    {
        if(sourceRoot==null||sourceRoot!=item.transform||sourceMuzzle==null||sourceMuzzle!=muzzle||root==null)return false;
        foreach(var part in animatedParts)
            if(part.Source==null||part.Snapshot!=null&&part.Skin!.sharedMesh!=part.Snapshot.Original)return false;
        return true;
    }
    internal void Suspend(){ReloadPose(false,false,false,0,Vector3.zero,Quaternion.identity);Hide();RestoreAnimation();}
    internal void Resume(Equipable item)
    {
        createdAt=Time.realtimeSinceStartup;animatedFrame=-1;animationRetryAt=0;
        ResetCycle();nativeFlashUntil=0;foreach(var snapshot in snapshots)snapshot.Invalidate();
        if(Profile!="prop")TryEnableAnimation(item);
    }
    internal void Hide() { if (root != null) root.SetActive(false);RestoreProps(); }
    // 0.1.153: a thing
    // in the left hand is held as the right hand holds it, mirrored - the hand
    // was mirrored, the thing was not (a chair's board, a broom's stick off the
    // middle lay beside the fingers). Now its mesh is mirrored with the hand,
    // across the same plane (x = 0 of the fitted frame).
    private Transform? geometryRoot;private bool geometryMirrored;
    internal void MirrorGeometry(bool on)
    {
        if(geometryRoot==null||on==geometryMirrored)return;
        geometryMirrored=on;
        var t=geometryRoot;var p=t.localPosition;var q=t.localRotation;var s=t.localScale;
        t.localPosition=new Vector3(-p.x,p.y,p.z);t.localRotation=new Quaternion(q.x,-q.y,-q.z,q.w);t.localScale=new Vector3(-s.x,s.y,s.z);
    }
    internal bool GeometryMirrored=>geometryMirrored;
    // 0.1.154: its shape where it is drawn (mirrored in the left hand).
    private ContactSphere[]? mirroredShape,mirroredFrom;
    internal ContactSphere[]? ActiveMeleeShape
    {
        get
        {
            var shape=ContactShape;if(!geometryMirrored||shape==null)return shape;
            if(!ReferenceEquals(shape,mirroredFrom)||mirroredShape==null)
            {
                mirroredFrom=shape;mirroredShape=new ContactSphere[shape.Length];
                for(int i=0;i<shape.Length;i++){var o=shape[i].Offset;mirroredShape[i]=shape[i] with {Offset=new System.Numerics.Vector3(-o.X,o.Y,o.Z)};}
            }
            return mirroredShape;
        }
    }
    private void RestoreProps()
    {foreach(var pair in propSources)if(pair.Key!=null)pair.Key.enabled=pair.Value;propSources.Clear();}
    public void Dispose()
    {
        RestoreProps();DisposeCylinder();DisposeScope();
        foreach(var m in stillTemp)if(m!=null)UnityEngine.Object.Destroy(m);stillTemp.Clear();
        reloadHint?.Dispose();reloadHint=null;reloadMesh?.Dispose();reloadMesh=null;
        if(BoltShape!=null)UnityEngine.Object.Destroy(BoltShape);BoltShape=null;if(CoverShape!=null)UnityEngine.Object.Destroy(CoverShape);CoverShape=null;
        RestoreAnimation(); animatedParts.Clear(); sourceRoot=null;
        if (root != null) UnityEngine.Object.Destroy(root); root = null; MuzzleAnchor=null;
        if(flashMaterial!=null) UnityEngine.Object.Destroy(flashMaterial); flashMaterial=null; flash=null;
        foreach (var mesh in baked) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        baked.Clear();foreach(var snapshot in snapshots)snapshot.Dispose();snapshots.Clear();
    }
}
