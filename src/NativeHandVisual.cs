using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;
namespace XiiiXR;
// Borrow the player's actual skin/materials. Only owned render meshes are moved.
internal sealed partial class NativeHandVisual : IDisposable
{
    private GameObject? root;
    private SkinnedMeshRenderer? source;
    private Transform? wrist;
    private Mesh? sourceMesh;
    private NativeHandMesh? clipped;
    private Matrix4x4 restFrame,restWrist;
    private int wristIndex=-1;
    private float nextGripReport;
    private FingerPoseMath? fingers;
    private int[] skinDrivers=Array.Empty<int>();private bool[] handBones=Array.Empty<bool>();
    private System.Numerics.Matrix4x4[] canonicalRest=Array.Empty<System.Numerics.Matrix4x4>();
    private readonly Dictionary<string,System.Numerics.Matrix4x4[]> skinPoses=new();
    private readonly Dictionary<int,int[]> rimPadVertices=new();
    private readonly Dictionary<string,(Vector3 p,Quaternion q,float size,System.Numerics.Matrix4x4[] pose)> bindings=new();
    private readonly GripPoseStability gripStability=new();
    private string activeGripKey="";
    private bool rightHand;
    private PlayMagic.CustomCharacterController? character;
    private PlayerArmsAnimationControl? armControl;
    private float nextFistCapture;
    private Matrix4x4[] neutralBones=Array.Empty<Matrix4x4>();
    internal NVector ForearmRest { get; private set; }
    // 0.1.186: the middle of the forearm's cut, in the rest frame (the wrist at the origin).
    internal NVector CropRest { get; private set; }
    internal WristFit Wrist {get;private set;}=WristFit.Default;
    internal bool NativeBand {get;private set;}
    internal bool GripPoseReady { get; private set; }
    private NVector[] restPoints=Array.Empty<NVector>();
    private NVector[] restNormals=Array.Empty<NVector>();
    private int sourceCount;
    private Matrix4x4[] posed=Array.Empty<Matrix4x4>();
    private NVector[] points=Array.Empty<NVector>(),normals=Array.Empty<NVector>(),posedPoints=Array.Empty<NVector>(),posedNormals=Array.Empty<NVector>();
    private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> output=new(0),outNormals=new(0);
    private float[] weights=Array.Empty<float>();
    private string lastProfile="?";
    private int lastGrip=-1,lastTrigger=-1,lastRevision=-1;
    private float nextBake;
    private bool shapeReady;
    private System.Numerics.Vector4[] posedTangents=Array.Empty<System.Numerics.Vector4>();
    private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector4> outTangents=new(0);
    private System.Numerics.Quaternion lastSwing=System.Numerics.Quaternion.Identity;
    internal System.Numerics.Quaternion ForearmSwing=>lastSwing;
    private readonly System.Numerics.Quaternion[] bends=new System.Numerics.Quaternion[65];
    internal bool Valid=>root!=null && RenderResourcesValid && source!=null && wrist!=null && source.sharedMesh==sourceMesh
        &&character!=null&&character.CurrentFpsRigReference!=null&&character.CurrentFpsRigReference.m_fpsMesh==source;
    internal bool BelongsTo(Transform player)=>Valid && source!.transform.IsChildOf(player);
    // 0.1.245: the hand only, cut just behind the watch (VR SETTINGS "Forearms").
    internal bool HandsOnly{get;private set;}
    internal static NativeHandVisual Create(Transform parent,Transform player,bool right,bool handsOnly=false)
    {
        var result=new NativeHandVisual{HandsOnly=handsOnly};
        try{result.Build(parent,player,right);return result;}catch{result.Dispose();throw;}
    }
    private void Build(Transform parent,Transform player,bool right)
    {
        // The game can retain a previous outfit and a fake left-arm rig under
        // the same player. Mesh size/name/activeInHierarchy cannot identify it.
        rightHand=right;
        character=player.GetComponent(Il2CppType.Of<PlayMagic.CustomCharacterController>())?.TryCast<PlayMagic.CustomCharacterController>();
        source=character?.CurrentFpsRigReference?.m_fpsMesh;
        armControl=character?.armsController;
        if(source==null||source.sharedMesh==null||!source.transform.IsChildOf(player))
            throw new InvalidOperationException("Current first-person outfit skin not ready");
        snapshot=new NativeSkinSnapshot(source);var bones=snapshot.Bones;var original=snapshot.Original;sourceMesh=original;
        string side=right?"R":"L";int oi=-1,ei=-1,thumb=-1;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null)
        {
            string name=bones[i].name;
            if(name==side+"_Arm_WristSHJnt")wristIndex=i;
            if(name==(right?"L":"R")+"_Arm_WristSHJnt")oi=i;
            if(name==side+"_Arm_ElbowSHJnt")ei=i;
        }
        if(wristIndex<0 || oi<0 || ei<0)throw new InvalidOperationException("Native wrist/elbow bones missing: "+string.Join(",",bones.Where(b=>b!=null).Select(b=>b.name)));
        wrist=bones[wristIndex];restWrist=snapshot.Bind[wristIndex].inverse;
        Vector3 Rest(int i)=>snapshot.Bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
        var fingers=new List<NVector>();float thumbLength=0;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null && bones[i]!=wrist && bones[i].IsChildOf(wrist))
        {
            if(bones[i].name.IndexOf("thumb",StringComparison.OrdinalIgnoreCase)<0)fingers.Add(N(Rest(i)));
            else
            {
                float length=(Rest(i)-Rest(wristIndex)).sqrMagnitude;
                if(length>thumbLength){thumbLength=length;thumb=i;}
            }
        }
        if(thumb<0)throw new InvalidOperationException("Native thumb chain not identified");
        restFrame=U(NativeHandMath.Frame(N(Rest(wristIndex)),N(Rest(ei)),N(Rest(thumb)),fingers.ToArray(),right));
        ForearmRest=N(restFrame.MultiplyPoint3x4(Rest(ei)));
        neutralBones=snapshot.Bind.Select(b=>b.inverse).ToArray();
        canonicalRest=neutralBones.Select(b=>N(restFrame*b)).ToArray();
        this.fingers=new FingerPoseMath(bones.Select(b=>b.name).ToArray(),canonicalRest,right);
        skinDrivers=new int[bones.Length];Array.Fill(skinDrivers,-1);handBones=new bool[bones.Length];
        var driven=new Dictionary<int,int>();
        for(int i=0;i<bones.Length;i++)
        {
            string name=bones[i].name;
            if(name.StartsWith(side+"_Finger_",StringComparison.Ordinal)||name.StartsWith(side+"_Thumb_",StringComparison.Ordinal))
                if(name.EndsWith("_01SHJnt",StringComparison.Ordinal)||name.EndsWith("_02SHJnt",StringComparison.Ordinal)||name.EndsWith("_03SHJnt",StringComparison.Ordinal))driven[bones[i].GetInstanceID()]=i;
        }
        int helpers=0;
        for(int i=0;i<bones.Length;i++)
        {
            handBones[i]=bones[i]==wrist||bones[i].IsChildOf(wrist);
            if(!handBones[i])continue;
            for(var t=bones[i];t!=null&&t!=wrist;t=t.parent)
                if(driven.TryGetValue(t.GetInstanceID(),out int driver)){skinDrivers[i]=driver;if(driver!=i)helpers++;break;}
        }
        Bootstrap.Write("HAND SKIN hierarchy source="+source.name+" side="+side+" bones="+handBones.Count(x=>x)+" helpers="+helpers);
        var bake=EnsureBakeTarget();snapshot.Bake(bake,true);
        sourceCount=bake.vertexCount;
        if(sourceCount<30||sourceCount>200000)throw new InvalidOperationException("Unexpected native arm vertex count "+sourceCount);
        var vertices=bake.vertices;var normals=bake.normals;var uv=bake.uv;
        if(uv.Length!=sourceCount||normals.Length!=sourceCount)throw new InvalidOperationException("Native arm UVs/normals unavailable");
        restPoints=new NVector[sourceCount];restNormals=new NVector[sourceCount];var coords=new NVector2[sourceCount];
        var normalFrame=restFrame.inverse.transpose;
        for(int i=0;i<sourceCount;i++)
        {restPoints[i]=N(restFrame.MultiplyPoint3x4(vertices[i]));restNormals[i]=N(normalFrame.MultiplyVector(normals[i]).normalized);coords[i]=new NVector2(uv[i].x,uv[i].y);}
        var triangles=new int[bake.subMeshCount][];
        for(int i=0;i<triangles.Length;i++)triangles[i]=bake.GetTriangles(i);
        if(restFrame.determinant<0)foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3)(sub[i+1],sub[i+2])=(sub[i+2],sub[i+1]);
        bool[]? eligible=null;string selection="bone weights";
        if(original.isReadable)
        {
            var weights=original.boneWeights;
            if(weights.Length==sourceCount)
            {
                // Sample distal skin once for the outfit-specific ashtray pinch.
                // Helper bones inherit their named finger ancestor's deformation.
                var clouds=this.fingers.RimDistalBones.ToDictionary(b=>b,b=>new List<int>());
                for(int i=0;i<sourceCount;i++)
                {
                    var w=weights[i];
                    foreach(var cloud in clouds)
                    {
                        int distal=cloud.Key;
                        bool Follows(int b)=>b>=0&&b<skinDrivers.Length&&(b==distal||skinDrivers[b]==distal);
                        float weight=(Follows(w.boneIndex0)?w.weight0:0)+(Follows(w.boneIndex1)?w.weight1:0)
                            +(Follows(w.boneIndex2)?w.weight2:0)+(Follows(w.boneIndex3)?w.weight3:0);
                        if(weight>=.9f)cloud.Value.Add(i);
                    }
                }
                foreach(var cloud in clouds)
                {
                    var selected=this.fingers.SetRimPadCloud(cloud.Key,cloud.Value.Select(i=>restPoints[i]).ToArray());
                    if(selected.Length>0)rimPadVertices[cloud.Key]=selected.Select(i=>cloud.Value[i]).ToArray();
                }
                bool IsSide(int b)=>b>=0&&b<bones.Length&&bones[b]!=null && (bones[b]==wrist || bones[b].IsChildOf(wrist) || bones[b].name.StartsWith(side+"_Arm_",StringComparison.Ordinal));
                eligible=new bool[sourceCount];
                for(int i=0;i<sourceCount;i++)
                {
                    var w=weights[i];float amount=(IsSide(w.boneIndex0)?w.weight0:0)+(IsSide(w.boneIndex1)?w.weight1:0)+(IsSide(w.boneIndex2)?w.weight2:0)+(IsSide(w.boneIndex3)?w.weight3:0);
                    eligible[i]=amount>=.45f;
                }
                if(eligible.Count(x=>x)<30)eligible=null;
            }
        }
        if(eligible==null)
        {
            selection="neutral connected islands";
            eligible=NativeHandMesh.ConnectedOwnership(restPoints,triangles,NVector.Zero,N(restFrame.MultiplyPoint3x4(Rest(oi))));
        }
        var candidates=restPoints.Where((p,i)=>eligible[i]).ToArray();
        var min=candidates.Length>0?candidates.Aggregate(NVector.Min):NVector.Zero;
        var max=candidates.Length>0?candidates.Aggregate(NVector.Max):NVector.Zero;
        Bootstrap.Write("NATIVE HAND PROBE "+side+" neutralSnapshot=true source="+source.name+" readable="+original.isReadable
            +" sourceScale="+source.transform.lossyScale.ToString("F6")+" vertices="+sourceCount+" eligible="+candidates.Length
            +" selection="+selection+" min="+min.ToString("F6")+" max="+max.ToString("F6")+" joints="+string.Join(",",bones.Where(b=>b!=null&&b.IsChildOf(wrist)).Select(b=>b.name)));
        float mountDistance=.052f;bool[] accessoryPoints=new bool[sourceCount];
        void RemoveStaticWatch()
        {
            var accessory=NativeBraceletMath.Select(restPoints,triangles,eligible,true);
            if(!right&&accessory.Count(x=>x)>=12)
            {NativeBand=true;accessoryPoints=accessory;mountDistance=WristFitMath.AccessoryDistance(restPoints,accessory,ForearmRest);}
            int removed=0;for(int i=0;i<eligible.Length;i++)if(accessory[i]){eligible[i]=false;removed++;}
            Bootstrap.Write("WATCH removed static native accessory from owned "+side+" mesh vertices="+removed);
        }
        RemoveStaticWatch();
        float cutZ=NativeHandMesh.ForearmCut(ForearmRest.Z,mountDistance,HandsOnly);
        try{clipped=NativeHandMesh.Build(restPoints,coords,triangles,eligible,cutZ,.14f);}
        catch(InvalidOperationException) when(selection=="bone weights")
        {
            // Readable meshes can expose an empty/partial legacy weight array.
            // Use spatially separated neutral islands, never posed touching hands.
            eligible=NativeHandMesh.ConnectedOwnership(restPoints,triangles,NVector.Zero,N(restFrame.MultiplyPoint3x4(Rest(oi))));
            RemoveStaticWatch();
            selection="neutral connected islands after legacy-weight crop failed";
            cutZ=NativeHandMesh.ForearmCut(ForearmRest.Z,mountDistance,HandsOnly);
            clipped=NativeHandMesh.Build(restPoints,coords,triangles,eligible,cutZ,.14f);
        }
        try{Wrist=WristFitMath.Fit(restPoints,triangles,eligible,ForearmRest,mountDistance);if(NativeBand)Wrist=WristFitMath.AttachToNativeCase(Wrist,restPoints,triangles,eligible,accessoryPoints);Bootstrap.Write("WRIST FIT "+side+" nativeBand="+NativeBand+" mount="+mountDistance+" center="+Wrist.Center+" radii="+Wrist.RadiusX+","+Wrist.RadiusY+" nativeCasePoint="+Wrist.DisplayPoint+" nativeCaseNormal="+Wrist.DisplayNormal);}catch(Exception ex){Bootstrap.Warn("WRIST FIT default: "+ex.Message);}
        mesh=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};mesh.name="XIII native "+side+" hand cropped";mesh.indexFormat=IndexFormat.UInt32;mesh.MarkDynamic();
        root=new GameObject("XIII original "+side+" hand");root.layer=parent.gameObject.layer;root.transform.SetParent(parent,false);
        root.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!.sharedMesh=mesh;
        var renderer=root.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
        renderer.sharedMaterials=source.sharedMaterials;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        mesh.vertices=new Vector3[clipped.Points.Count];mesh.uv=clipped.Points.Select(p=>new Vector2(p.UV.X,p.UV.Y)).ToArray();mesh.subMeshCount=clipped.Submeshes.Count;
        var originalColors=bake.colors;
        if(originalColors.Length==sourceCount)
            mesh.colors=clipped.Points.Select(p=>originalColors[p.A]*p.Mix.X+originalColors[p.B]*p.Mix.Y+originalColors[p.C]*p.Mix.Z).ToArray();
        for(int i=0;i<clipped.Submeshes.Count;i++)mesh.SetTriangles(clipped.Submeshes[i],i,false,0);
        NativeFistSampler.Capture(player,source,snapshot,restFrame,restWrist,wristIndex,this.fingers);
        points=new NVector[sourceCount];this.normals=new NVector[sourceCount];posed=new Matrix4x4[neutralBones.Length];
        posedPoints=new NVector[clipped.Points.Count];posedNormals=new NVector[clipped.Points.Count];
        output=new(clipped.Points.Count);outNormals=new(clipped.Points.Count);
        posedTangents=new System.Numerics.Vector4[clipped.Points.Count];outTangents=new(clipped.Points.Count);
        weights=clipped.Points.Select(p=>ArmIkMath.ForearmWeight(p.Rest.Z)).ToArray();
        // 0.1.186: the middle of the forearm's cut (where a small medkit is carried).
        var cap=clipped.Points.Where(p=>p.Cap).Select(p=>p.Rest).ToArray();
        CropRest=cap.Length>=3?cap.Aggregate(NVector.Zero,(sum,p)=>sum+p)/cap.Length:ArmMedkitMath.AlongForearm(ForearmRest,cutZ);
        Bootstrap.Write("NATIVE HAND "+side+" forearm cut at "+CropRest.ToString()+" (rim points "+cap.Length+(HandsOnly?"; the hand only: cut behind the watch, its band "+mountDistance.ToString("F3")+" m from the wrist":"")+")");
        Refresh(false,1,0,0,"",ForearmRest);
        Bootstrap.Write("NATIVE HAND "+side+" bound source="+source.name+" vertices="+clipped.Points.Count+" selection="+selection+" materials="+string.Join(",",source.sharedMaterials.Where(m=>m!=null).Select(m=>m.name)));
    }
    private Matrix4x4 Frame()
    {
        snapshot!.Sample();
        return U(NativeHandMath.AnimatedFrame(N(restFrame),N(restWrist),N(snapshot.Live[wristIndex])));
    }
    internal Vector3 PropRimContact(string profile,float thickness){EnsurePropPads();return fingers==null?new Vector3(0,-.044f,.073f):U(fingers.RimContact(profile,thickness));}
    // Hand orientation in the chair's fitted frame (backrest rake).
    internal Quaternion PropChairHandRotation
    {
        get{var q=fingers==null?System.Numerics.Quaternion.Identity:fingers.ChairRotation;return new Quaternion(q.X,q.Y,q.Z,q.W);}
    }
    internal Quaternion PropRimHandRotation
    {
        get{var q=fingers==null?System.Numerics.Quaternion.Identity:System.Numerics.Quaternion.Inverse(fingers.RimRotation);return new Quaternion(q.X,q.Y,q.Z,q.W);}
    }
    internal string PropRimReport=>fingers==null?"unavailable":"skinPads="+fingers.RimMeasuredPads+" pinchErrorMm="+(fingers.RimPinchError*1000).ToString("F2")+" handClear="+fingers.RimClear+" chairPadErrorMm="+(fingers.ChairContactError*1000).ToString("F2")+" chairPenetrationMm="+(fingers.ChairPenetrationDepth*1000).ToString("F2")+" "+fingers.ChairReport;
    internal string HandRestReport=>fingers==null?"unavailable":fingers.RestReport();
    // 0.1.86: world matrix of this hand's native wrist bone as the VR hand
    // renders it (canonical rest wrist under the owned hand mesh root).
    // Items the game attaches to that bone keep their authored placement.
    internal bool TryWristWorld(out Matrix4x4 world)
    {
        world=Matrix4x4.identity;
        if(!Valid||root==null||!root.activeInHierarchy||wristIndex<0||wristIndex>=canonicalRest.Length)return false;
        world=root.transform.localToWorldMatrix*Matrix4x4.Translate(new Vector3(0,0,NativeHandMesh.WristZ))*U(canonicalRest[wristIndex]);
        return true;
    }
    // 0.1.197: a thing held by another rig of the same skeleton (the game's
    // fake arm holding the zipline hook), in this hand's own frame; and back
    // from that frame to where this hand is drawn.
    internal bool TryCanonicalFromRig(Transform rigRoot,Matrix4x4 world,out Matrix4x4 canonical,out float wristDistance)
    {
        canonical=Matrix4x4.identity;wristDistance=float.PositiveInfinity;
        if(!Valid||snapshot==null||wristIndex<0||snapshot.Bones[wristIndex]==null)return false;
        var name=snapshot.Bones[wristIndex].name;Transform? liveWrist=null;
        foreach(var c in rigRoot.GetComponentsInChildren(Il2CppType.Of<Transform>(),true)){var t=c.TryCast<Transform>();if(t!=null&&t.name==name){liveWrist=t;break;}}
        if(liveWrist==null)return false;
        var frame=U(NativeHandMath.AnimatedFrame(N(restFrame),N(restWrist),N(liveWrist.localToWorldMatrix)));
        canonical=frame*world;wristDistance=Vector3.Distance(liveWrist.position,(Vector3)world.GetColumn(3));
        return float.IsFinite(canonical.m03+canonical.m13+canonical.m23);
    }
    internal bool TryCanonicalWorld(Matrix4x4 canonical,out Matrix4x4 world)
    {
        world=Matrix4x4.identity;
        if(!Valid||root==null||!root.activeInHierarchy)return false;
        world=root.transform.localToWorldMatrix*Matrix4x4.Translate(new Vector3(0,0,NativeHandMesh.WristZ))*canonical;
        return true;
    }
    // Copy the finger pose of another rig of the same skeleton (the game's
    // "fake arms" holding the grappling hook) as this hand's grip profile.
    internal bool CaptureFromRig(Transform rigRoot,string profile)
    {
        if(!Valid||fingers==null||snapshot==null||wristIndex<0)return false;
        var bones=snapshot.Bones;var map=new Dictionary<string,Transform>();
        foreach(var c in rigRoot.GetComponentsInChildren(Il2CppType.Of<Transform>(),true))
        {var t=c.TryCast<Transform>();if(t!=null&&!map.ContainsKey(t.name))map[t.name]=t;}
        if(bones[wristIndex]==null||!map.TryGetValue(bones[wristIndex].name,out var liveWrist))return false;
        var frame=U(NativeHandMath.AnimatedFrame(N(restFrame),N(restWrist),N(liveWrist.localToWorldMatrix)));
        var pose=new System.Numerics.Matrix4x4[bones.Length];int found=0;
        for(int i=0;i<bones.Length;i++)
        {
            if(bones[i]!=null&&map.TryGetValue(bones[i].name,out var t)){pose[i]=N(frame*t.localToWorldMatrix);found++;}
            else pose[i]=canonicalRest[i];
        }
        bool ok=fingers.Capture(profile,pose,false,true,true);// 0.1.88: only a real grasp
        Bootstrap.Write("GRAPPLE hand pose capture profile="+profile+" bones="+found+" ok="+ok+(ok?"":" reason="+fingers.CaptureFailure));
        return ok;
    }
    // 0.1.186: the forearm's cut as drawn now (the forearm swung toward the
    // elbow): its middle, the way out of it (toward the elbow), the hand's up.
    internal bool TryCropWorld(out Vector3 center,out Vector3 outward,out Vector3 up)
    {
        center=outward=up=Vector3.zero;
        if(!Valid||root==null||!root.activeInHierarchy||CropRest.LengthSquared()<1e-8f)return false;
        var t=root.transform;var bend=lastSwing;
        center=t.TransformPoint(U(NVector.Transform(CropRest,bend)+new NVector(0,0,NativeHandMesh.WristZ)));
        outward=t.TransformDirection(U(NVector.Transform(-NVector.UnitZ,bend))).normalized;
        up=t.TransformDirection(U(NVector.Transform(NVector.UnitY,bend))).normalized;
        return float.IsFinite(center.x)&&float.IsFinite(center.y)&&float.IsFinite(center.z);
    }
    internal void InvalidateSample()=>snapshot?.Invalidate();
    internal void ResetGrips(){bindings.Clear();activeGripKey="";gripStability.Reset();skinPoses.Clear();fingers?.ResetGrips();GripPoseReady=false;ResetRenderPose();}
    internal void ResetRenderPose(){snapshot?.Invalidate();shapeReady=false;nextBake=0;lastSwing=System.Numerics.Quaternion.Identity;}
    internal bool TryWeaponGrip(WeaponVisual weapon,out Vector3 position,out Quaternion rotation,out float size,bool renderSample=true)
    {
        position=Vector3.zero;rotation=Quaternion.identity;size=1;
        if(!Valid || !weapon.AuthoredGripAvailable)return false;
        try
        {
            string key=weapon.AnchorKey,profile=weapon.GripProfile;
            if(bindings.TryGetValue(key,out var saved))
            {
                if(activeGripKey!=key)
                {fingers!.Capture(profile,saved.pose,true,false,true);skinPoses[profile]=saved.pose;activeGripKey=key;}
                GripPoseReady=true;position=saved.p;rotation=saved.q;size=saved.size;return true;
            }
            if(activeGripKey!=key)
            {
                // Pose and wrist attachment are one sample, keyed by the actual
                // visual. A new ashtray must not reuse a previous draw's fingers.
                activeGripKey=key;fingers!.Forget(profile);skinPoses.Remove(profile);gripStability.Reset();
            }
            var frame=Frame();
            var matrix=weapon.GameWorldToFitted*source!.localToWorldMatrix*frame.inverse;
            bool valid=NativeHandMath.TryGrip(N(matrix),out var p,out var q,out size);
            position=U(p);rotation=new Quaternion(q.X,q.Y,q.Z,q.W);
            GripPoseReady=false;
            bool settled=weapon.ReadyForGripCapture&&armControl!=null&&!armControl.weaponSwitchInProgress
                &&armControl.inventory!=null&&!armControl.inventory.isInTransit
                &&!armControl.isReloading&&!armControl.isFiring&&!armControl.propAnimationActive
                &&!armControl.keyAnimationActive&&GripCarry.Current?.HidesLeft!=true;
            var animator=armControl?.animator;
            if(animator==null||!animator.isActiveAndEnabled)settled=false;
            else for(int layer=0;layer<animator.layerCount;layer++)if(animator.IsInTransition(layer))settled=false;
            if(renderSample&&valid)
            {
                var complete=snapshot!.Live.Select(b=>N(frame*b)).ToArray();
                if(gripStability.Observe(key,N(matrix),complete,handBones,Time.realtimeSinceStartup,Time.frameCount,settled))
                {
                    bool grasp=rightHand&&(profile=="pistol"||profile=="revolver"||profile=="shotgun"||profile=="ak47"||profile=="uzi"||profile=="m16"||profile=="sniper"||profile=="m60"||profile.StartsWith("prop",StringComparison.Ordinal));
                    GripPoseReady=fingers!.Capture(profile,complete,true,grasp,true);
                    if(GripPoseReady)
                    {
                        if(bindings.Count>=32)bindings.Clear();
                        skinPoses[profile]=complete;bindings[key]=(position,rotation,size,complete);
                        Bootstrap.Write("NATIVE GRIP paired stable pose source="+source!.name+" wrist="+wrist!.name+" profile="+profile);
                    }
                    else if(Time.realtimeSinceStartup>=nextGripReport){nextGripReport=Time.realtimeSinceStartup+5;Bootstrap.Warn("NATIVE GRIP using procedural grasp; waiting for closed native pose source="+source!.name+" profile="+profile+" reason="+fingers.CaptureFailure);}
                }
            }
            // A flat/culled native pose is not a reason to detach the VR hand.
            // Keep the measured wrist and the existing procedural equipment curl
            // until a stable closed native pose becomes available.
            if(!valid && Time.realtimeSinceStartup>=nextGripReport)
            {nextGripReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("NATIVE GRIP rejected "+wrist!.name+" scale="+matrix.lossyScale.ToString("F6")+" wrist="+matrix.MultiplyPoint3x4(Vector3.zero).ToString("F6"));}
            return valid;
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextGripReport)
            {nextGripReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("NATIVE GRIP temporarily unavailable: "+ex.Message);}
            return false; // A visual hand binding must never disable firing.
        }
    }
    internal void Refresh(bool held,float size,float grip,float trigger,string profile,NVector elbow)
    {
        if(!Valid||mesh==null||clipped==null)return;
        // A managed reference in this IL2CPP plugin is not a Unity asset root.
        // Recover a lost scratch mesh before the cached-pose early-out, even
        // when the controller/profile did not change during the flashback.
        var bake=EnsureBakeTarget();
        // Scale only the owned hand around its wrist. The case/digits retain the
        // unchanged parent, dimensions and once-per-frame rendering path.
        root!.transform.localScale=Vector3.one*size;
        root.transform.localPosition=new Vector3(0,0,NativeHandMesh.WristZ*(1-size));
        if(!held&&!fingers!.Has("fists")&&Time.realtimeSinceStartup>=nextFistCapture)
        {
            nextFistCapture=Time.realtimeSinceStartup+.5f;
            try
            {
                var inventory=armControl?.inventory;
                if(inventory!=null&&!inventory.isInTransit&&inventory.currentEquipable!=null
                    &&inventory.currentEquipable.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist
                    &&armControl!.desiredArmState==PlayerArmsAnimationControl.ArmStates.Weapon_Fists&&!armControl.weaponSwitchInProgress)
                {
                    var frame=Frame();
                    if(fingers.Capture("fists",snapshot!.Live.Select(b=>N(frame*b)).ToArray()))Bootstrap.Write("NATIVE FIST captured live pose wrist="+wrist!.name);
                }
            }
            catch(Exception ex){if(Time.realtimeSinceStartup>=nextGripReport){nextGripReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("NATIVE FIST live sample: "+ex.Message);}}
        }
        string requested=held||profile.StartsWith("medkit_",StringComparison.Ordinal)||profile.StartsWith("reload_",StringComparison.Ordinal)?profile:"";
        int g=(int)MathF.Round(Math.Clamp(grip,0,1)*20),t=(int)MathF.Round(Math.Clamp(trigger,0,1)*20);
        if(fingers!.Has(held?profile:"fists")){if(held)g=20;t=0;}
        if(requested.Contains("ashtray")||KeyGripGeometry.PinchProfile(requested)||FingerPoseMath.ChairProfile(requested)||requested==ScrewdriverGrip.Profile){g=20;t=0;}
        bool dirty=!shapeReady||lastProfile!=requested||lastGrip!=g||lastTrigger!=t||lastRevision!=fingers!.Revision;
        // Bake only changed poses, capped at 30 Hz. Rigid hand tracking still
        // follows every display frame, with no extra lag while walking.
        bool changed=dirty && (!shapeReady||Time.realtimeSinceStartup>=nextBake||lastProfile!=requested);
        if(changed)
        {
            long timer=FramePerformance.Begin();
            try
            {
                var canonical=fingers!.Pose(g/20f,t/20f,requested);
                skinPoses.TryGetValue(requested,out var capturedSkin);
                bool fittedGrip=requested.Contains("ashtray")||KeyGripGeometry.PinchProfile(requested)||FingerPoseMath.ChairProfile(requested);
                canonical=HandSkinPose.Complete(canonicalRest,canonical,skinDrivers,handBones,held&&!fittedGrip?capturedSkin:null);
                var inv=restFrame.inverse;
                for(int i=0;i<posed.Length;i++)posed[i]=inv*U(canonical[i]);
                snapshot!.BakePose(bake,posed);
                var nframe=restFrame.inverse.transpose;var v=bake.vertices;var n=bake.normals;
                // Pure managed Numerics in the vertex loops: no per-vertex Unity
                // matrix/vector native calls or temporary full-size arrays.
                var pm=N(restFrame);var nm=N(nframe);var tangent=bake.tangents;float handedness=restFrame.determinant<0?-1:1;
                for(int i=0;i<sourceCount;i++)
                {points[i]=NVector.Transform(N(v[i]),pm);normals[i]=NVector.Normalize(NVector.TransformNormal(N(n[i]),nm));}
                if(fittedGrip)
                {
                    var expected=fingers.RimPads(canonical);int digit=0;float error=0;
                    foreach(int bone in fingers.RimDistalBones)
                    {
                        if(rimPadVertices.TryGetValue(bone,out var ids))
                        {
                            var actual=ids.Aggregate(NVector.Zero,(sum,i)=>sum+points[i])/ids.Length+NVector.UnitZ*NativeHandMesh.WristZ;
                            error=Math.Max(error,NVector.Distance(actual,expected[digit]));
                        }
                        digit++;
                    }
                    Bootstrap.Write("PINCH SKIN profile="+requested+" renderedPadErrorMm="+(rimPadVertices.Count==0?"unavailable":(error*1000).ToString("F2"))+" "+PropRimReport);
                }
                for(int i=0;i<posedPoints.Length;i++)
                {
                    var p=clipped.Points[i];posedPoints[i]=NativeHandMesh.Evaluate(p,points);
                    var ns=p.Rest.Z<0?restNormals:normals;
                    posedNormals[i]=p.Cap?-NVector.UnitZ:NVector.Normalize(ns[p.A]*p.Mix.X+ns[p.B]*p.Mix.Y+ns[p.C]*p.Mix.Z);
                    NVector tangentDirection=NVector.UnitX;float sign=1;
                    if(!p.Cap&&tangent.Length==sourceCount)
                    {
                        var ta=tangent[p.A];var tb=tangent[p.B];var tc=tangent[p.C];
                        tangentDirection=NVector.TransformNormal(new NVector(ta.x,ta.y,ta.z)*p.Mix.X+new NVector(tb.x,tb.y,tb.z)*p.Mix.Y+new NVector(tc.x,tc.y,tc.z)*p.Mix.Z,pm);
                        sign=ta.w*handedness;
                    }
                    tangentDirection-=posedNormals[i]*NVector.Dot(tangentDirection,posedNormals[i]);
                    if(tangentDirection.LengthSquared()<1e-8f)tangentDirection=NVector.Cross(posedNormals[i],NVector.UnitZ);
                    if(tangentDirection.LengthSquared()<1e-8f)tangentDirection=NVector.UnitX;
                    posedTangents[i]=new System.Numerics.Vector4(NVector.Normalize(tangentDirection),sign);
                }
                shapeReady=true;lastProfile=requested;lastGrip=g;lastTrigger=t;lastRevision=fingers.Revision;
                nextBake=Time.realtimeSinceStartup+1f/30;
            }
            finally{FramePerformance.End(timer,2);}
        }
        var desiredSwing=ArmIkMath.ForearmSwing(ForearmRest,elbow);
        var swing=System.Numerics.Quaternion.Slerp(lastSwing,desiredSwing,1-MathF.Exp(-14*Math.Clamp(Time.unscaledDeltaTime,0,.05f)));
        if(!changed && Math.Abs(System.Numerics.Quaternion.Dot(swing,lastSwing))>.999999f)return;
        lastSwing=swing;
        for(int i=0;i<bends.Length;i++)bends[i]=System.Numerics.Quaternion.Slerp(System.Numerics.Quaternion.Identity,swing,i/(float)(bends.Length-1));
        var pivot=new NVector(0,0,NativeHandMesh.WristZ);
        for(int i=0;i<output.Length;i++)
        {
            var bend=bends[(int)MathF.Round(weights[i]*(bends.Length-1))];
            output[i]=U(NVector.Transform(posedPoints[i]-pivot,bend)+pivot);
            outNormals[i]=U(NVector.Transform(posedNormals[i],bend));
            var t0=posedTangents[i];var rotated=NVector.Transform(new NVector(t0.X,t0.Y,t0.Z),bend);
            outTangents[i]=new Vector4(rotated.X,rotated.Y,rotated.Z,t0.W);
        }
        mesh.vertices=output;mesh.normals=outNormals;mesh.tangents=outTangents;
        mesh.bounds=new Bounds(new Vector3(0,0,-.10f),new Vector3(.8f,.8f,1f));
    }
    internal static System.Numerics.Matrix4x4 N(Matrix4x4 m)=>new(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
    internal static Matrix4x4 U(System.Numerics.Matrix4x4 m)=>new(new Vector4(m.M11,m.M12,m.M13,m.M14),new Vector4(m.M21,m.M22,m.M23,m.M24),new Vector4(m.M31,m.M32,m.M33,m.M34),new Vector4(m.M41,m.M42,m.M43,m.M44));
    private static NVector N(Vector3 p)=>new(p.x,p.y,p.z);
    private static Vector3 U(NVector p)=>new(p.X,p.Y,p.Z);
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
        DisposeSkinResources();
        // Source textures, materials, skeleton and renderer remain game-owned.
        source=null;sourceMesh=null;wrist=null;clipped=null;
    }
}
