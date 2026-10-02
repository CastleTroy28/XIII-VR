using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// The grappling hook in VR (0.1.84-0.1.86).
// - On the rope: left stick up/down climbs/descends (native Up/Down input),
//   right stick forward/back swings (LocomotionDriver), L3 or right B lets go.
// - The game hangs the hook on the WRIST BONE of its own left arm ("fake
//   arms"). That arm is hidden; the hook keeps the same authored attachment
//   on the VR left hand's wrist, so the cable starts from the VR hand, and
//   the VR hand copies the game's finger pose around it.
internal sealed class GrappleVr : IDisposable
{
    internal static GrappleVr? Current;
    private readonly CameraRig rig;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.grapple");
    private GrapplingHookController? controller;
    private Transform? player;private float nextFind,nextRumble;
    private readonly Dictionary<int,(Renderer renderer,bool enabled)> hidden=new();
    private bool wasActive,wasAttached,reported;
    // Hook placement relative to the wrist bone, and one renderer's placement
    // relative to the hook root (the preview mesh is built in that space).
    private Matrix4x4? hookFromWrist;private readonly Dictionary<string,Matrix4x4> bodyFromHook=new();
    private IntPtr hookInstance;
    internal bool DeviceShown {get;private set;}
    // Left hand pose while holding the hook: the game's own grasp when its
    // animation closes the hand, otherwise a pistol grip around the handle.
    private bool grasp;
    internal string HandProfile=>grasp?"grapple":"dual_pistol";
    // 0.1.195: the hand holding it (0 left, 1 right).
    // In the right hand its placement is the left hand's mirrored across the
    // hand (the hand roots are mirror images, as for held items).
    internal int Side;
    internal string HandProfileFor(bool right)=>right?(grasp?"grapple":"pistol"):HandProfile;
    private Transform? leftHand,rightHand;
    internal void Hands(Transform? left,Transform? right){if(left!=null)leftHand=left;if(right!=null)rightHand=right;}
    // 0.1.197: the game's left wrist bone is itself
    // mirrored, so mirroring its matrix and reading the turn back out of it
    // turned the hook round. The right hand now holds the mirror image of the
    // hook as it is drawn in the left hand (its place, turn and size in the
    // left hand's root), mirrored across the hand (a true mirror image).
    private Vector3 leftAt;private Quaternion leftTurn=Quaternion.identity;private Vector3 leftSize=Vector3.one;private bool leftKnown,mirrorReported;
    internal readonly struct Placed{internal readonly Vector3 Position;internal readonly Quaternion Rotation;internal readonly Vector3 Scale;internal Placed(Vector3 p,Quaternion q,Vector3 s){Position=p;Rotation=q;Scale=s;}}
    private bool LeftPlaced(Transform? hand,out Placed placed)
    {
        placed=default;var native=WeaponHands.Current?.LeftNative;
        if(hookFromWrist==null||native==null||!native.TryWristWorld(out var wrist))return false;
        var m=Place(wrist,hookFromWrist.Value,hand);placed=new Placed(m.GetColumn(3),m.rotation,m.lossyScale);
        if(hand!=null)
        {
            var inverse=Quaternion.Inverse(hand.rotation);
            leftAt=inverse*(placed.Position-hand.position);leftTurn=inverse*placed.Rotation;leftSize=placed.Scale;leftKnown=true;
        }
        return true;
    }
    // The hook's placement in the hand `side` (world), and that hand.
    private bool Placement(int side,out Placed placed,out Transform? hand)
    {
        placed=default;hand=side==0?leftHand:rightHand;
        if(side==0)return LeftPlaced(hand,out placed);
        if(hand==null||!leftKnown&&(leftHand==null||!LeftPlaced(leftHand,out _)))return false;
        var mirror=HandTools.MirrorAcrossHand(new System.Numerics.Vector3(leftAt.x,leftAt.y,leftAt.z),new System.Numerics.Quaternion(leftTurn.x,leftTurn.y,leftTurn.z,leftTurn.w),new System.Numerics.Vector3(leftSize.x,leftSize.y,leftSize.z));
        var at=new Vector3(mirror.position.X,mirror.position.Y,mirror.position.Z);var turn=new Quaternion(mirror.rotation.X,mirror.rotation.Y,mirror.rotation.Z,mirror.rotation.W);
        placed=new Placed(hand.position+hand.rotation*at,hand.rotation*turn,new Vector3(mirror.scale.X,mirror.scale.Y,mirror.scale.Z));
        if(!mirrorReported){mirrorReported=true;Bootstrap.Write("GRAPPLE right hand: the left hand's hook mirrored across the hand (left: at "+leftAt.ToString("F3")+" turn "+leftTurn.eulerAngles.ToString("F0")+" size "+leftSize.ToString("F2")+"; right: at "+at.ToString("F3")+" turn "+turn.eulerAngles.ToString("F0")+")");}
        return true;
    }
    private GameObject? previewHolder,preview;
    // 0.1.142: the game's
    // rope speed (and how fast the body follows a changing rope) times the
    // VR config GrappleClimbSpeed; set again whenever the game resets it.
    private IntPtr climbController;private float baseClimb,appliedClimb=-1,baseFollow,appliedFollow=-1;
    internal static float ClimbScale=>GrappleMath.ClimbScale(QualityOptions.GrappleClimbSpeed?.Value??GrappleMath.DefaultClimbScale);
    private void ApplyClimbSpeed()
    {
        var c=controller;if(c==null)return;
        try
        {
            float scale=ClimbScale;
            if(c.Pointer!=climbController){climbController=c.Pointer;baseClimb=c.upwardsDownwardsVelocity;appliedClimb=-1;baseFollow=-1;appliedFollow=-1;}
            float v=c.upwardsDownwardsVelocity;if(appliedClimb>=0&&Math.Abs(v-appliedClimb)>1e-4f)baseClimb=v;
            if(baseClimb>0&&float.IsFinite(baseClimb)&&Math.Abs(v-baseClimb*scale)>1e-4f)
            {
                c.upwardsDownwardsVelocity=appliedClimb=baseClimb*scale;
                Bootstrap.Write("GRAPPLE rope climb speed "+baseClimb.ToString("F2")+" x"+scale.ToString("F1")+" = "+appliedClimb.ToString("F2"));
            }
            var body=c.characterController;
            if(body!=null)
            {
                float f=body.rappelAccelerationWhileExtendingOrRetracting;
                if(baseFollow<0||appliedFollow>=0&&Math.Abs(f-appliedFollow)>1e-4f)baseFollow=f;
                if(baseFollow>0&&float.IsFinite(baseFollow)&&Math.Abs(f-baseFollow*scale)>1e-4f){body.rappelAccelerationWhileExtendingOrRetracting=appliedFollow=baseFollow*scale;}
            }
        }
        catch(Exception ex){if(climbController!=IntPtr.Zero){climbController=IntPtr.Zero;Bootstrap.Warn("GRAPPLE climb speed: "+ex.Message);}}
    }
    internal GrappleVr(CameraRig camera)
    {
        rig=camera;Current=this;
        try
        {
            patches.Patch(AccessTools.DeclaredMethod(typeof(GrapplingHookController),"GrapplingHookUpInput"),postfix:new HarmonyMethod(typeof(GrappleVr),nameof(Up)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(GrapplingHookController),"GrapplingHookDownInput"),postfix:new HarmonyMethod(typeof(GrappleVr),nameof(Down)));
        }
        catch(Exception ex){Bootstrap.Warn("GRAPPLE input patch unavailable: "+ex.Message);}
    }
    internal bool Active{get{try{return controller!=null&&(controller.isUsingGrapplingHook||controller.isAttachedToGrapplePoint);}catch{return false;}}}
    internal bool OnRope{get{try{return controller!=null&&controller.isAttachedToGrapplePoint;}catch{return false;}}}
    private static bool Mine(GrapplingHookController c)=>Current?.controller!=null&&Current.controller.Pointer==c.Pointer;
    // 0.1.197: the hook fired from the right hand mirrors the
    // rope: the right stick climbs, the left stick swings, R3 lets go.
    internal bool RightHanded=>Side==1;
    internal static bool RopeRightHanded=>Current?.RightHanded==true&&Current.OnRope;
    private StickSample ClimbStick=>RightHanded?rig.RightStick:rig.LeftStick;
    private static void Up(GrapplingHookController __instance,ref bool __result)
    {
        var g=Current;if(g==null||!Mine(__instance))return;
        var stick=g.ClimbStick;if(stick.Valid)__result=stick.Value.Y>.5f;
    }
    private static void Down(GrapplingHookController __instance,ref bool __result)
    {
        var g=Current;if(g==null||!Mine(__instance))return;
        var stick=g.ClimbStick;if(stick.Valid)__result=stick.Value.Y<-.5f;
    }
    internal void Tick(Transform? root)
    {
        try
        {
            if(root!=player){Restore();HidePreview();controller=null;player=root;nextFind=0;hookFromWrist=null;bodyFromHook.Clear();hookInstance=IntPtr.Zero;DestroyPreview();leftKnown=false;mirrorReported=false;}
            if(root==null)return;
            if(controller==null&&Time.realtimeSinceStartup>=nextFind)
            {
                nextFind=Time.realtimeSinceStartup+1;
                foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<GrapplingHookController>(),true))
                {var g=c.TryCast<GrapplingHookController>();if(g!=null){controller=g;Bootstrap.Write("GRAPPLE controller bound "+g.name);Prefab();break;}}
            }
            ApplyClimbSpeed();
            // 0.1.185: the hand's copy of the hook made ready as soon as the game's hook is known (not when first shown).
            if(controller!=null&&preview==null&&!previewFailed&&hookFromWrist!=null)
            {try{BuildPreview();}catch(Exception ex){previewFailed=true;DestroyPreview();Bootstrap.Warn("GRAPPLE preview: "+ex.Message);}}
            bool active=Active,attached=OnRope;
            // 0.1.90: light pulses in the left controller while climbing or
            // descending the rope.
            if(attached&&Time.realtimeSinceStartup>=nextRumble)
            {
                var stick=rig.LeftStick;
                if(stick.Valid&&Math.Abs(stick.Value.Y)>.5f){nextRumble=Time.realtimeSinceStartup+.09f;rig.RopeHaptics();}
            }
            if(active!=wasActive||attached!=wasAttached)
            {
                Bootstrap.Write("GRAPPLE state using="+active+" attached="+attached);
                if(!active){Restore();DeviceShown=false;grasp=false;}
                wasActive=active;wasAttached=attached;
            }
        }
        catch(Exception ex){controller=null;Restore();Bootstrap.Warn("GRAPPLE tick: "+ex.Message);}
    }
    // Authored attachment from the equip prefab, before the hook is ever used.
    private void Prefab()
    {
        try
        {
            // 0.1.88: the game spawns its hook under the left wrist bone at
            // start; its local placement is the authored one (the prefab root
            // itself is identity, which put the preview across the hand).
            var instance=controller?.grappleHookInstance;var prefab=instance??controller?.grappleHookEquipPrefab;if(prefab==null)return;
            var t=prefab.transform;hookFromWrist=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);
            if(instance!=null)hookInstance=instance.Pointer;
            Bodies(t);Bootstrap.Write("GRAPPLE "+(instance!=null?"instance ":"prefab ")+prefab.name+" parent="+(t.parent!=null?t.parent.name:"none")+" local="+t.localPosition.ToString("F3")+" rot="+t.localRotation.eulerAngles.ToString("F0")+" scale="+t.localScale.ToString("F3")+" bodies="+bodyFromHook.Count);
        }
        catch(Exception ex){Bootstrap.Warn("GRAPPLE prefab: "+ex.Message);}
    }
    private void Bodies(Transform hook)
    {
        var inverse=hook.worldToLocalMatrix;
        foreach(var c in hook.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {var r=c.TryCast<Renderer>();if(r!=null)bodyFromHook[r.name]=inverse*r.transform.localToWorldMatrix;}
    }
    // World placement for the preview mesh (built in renderer `body` space)
    // so it sits in the VR left hand exactly as the game's hook will.
    internal bool TryPreviewBody(string body,out Matrix4x4 world)
    {
        world=Matrix4x4.identity;
        var native=WeaponHands.Current?.LeftNative;
        if(hookFromWrist==null||!bodyFromHook.TryGetValue(body,out var rel)||native==null||!native.TryWristWorld(out var wrist))return false;
        world=wrist*hookFromWrist.Value*rel;return true;
    }
    // A render-only copy of the game's hook, placed on the VR left wrist the
    // same way as the real one on the rope. Built inside an inactive parent,
    // with every non-render component removed before it is ever enabled, so
    // no game script on it runs.
    // 0.1.88: the game's wrist attachment left the trigger ~3 cm past the VR
    // index finger; slide the hook along the knuckle line toward the little
    // finger (left hand: -X of the hand root) so the index rests on it.
    // 0.1.91: adjustable in the VR settings (mm, hand frame).
    internal static Vector3 GripShift=>QualityOptions.GrappleAlong==null?new Vector3(.010f,0,.020f):new Vector3(QualityOptions.GrappleAlong.Value,QualityOptions.GrappleUp.Value,QualityOptions.GrappleForward.Value)*.001f;
    private static Matrix4x4 Place(Matrix4x4 wrist,Matrix4x4 hookFromWrist,Transform? hand)
    {
        var m=wrist*hookFromWrist;
        if(hand!=null){var p=(Vector3)m.GetColumn(3)+hand.rotation*GripShift;m.SetColumn(3,new Vector4(p.x,p.y,p.z,1));}
        return m;
    }
    internal bool ShowPreview(Transform? hand=null,int side=0)
    {
        if(hand!=null){lastHand=hand;if(side==0)leftHand=hand;else rightHand=hand;}
        Side=side;
        try
        {
            if(controller==null||hookFromWrist==null||!Placement(side,out var m,out _)){HidePreview();return false;}
            if(preview==null&&!BuildPreview()){return false;}
            var t=preview!.transform;
            t.SetPositionAndRotation(m.Position,m.Rotation);t.localScale=m.Scale;
            if(!preview.activeSelf)preview.SetActive(true);
            return true;
        }
        catch(Exception ex){Bootstrap.Warn("GRAPPLE preview: "+ex.Message);DestroyPreview();previewFailed=true;return false;}
    }
    private bool previewFailed;
    private bool BuildPreview()
    {
        if(previewFailed)return false;
        var source=controller?.grappleHookInstance??controller?.grappleHookEquipPrefab;if(source==null)return false;
        previewHolder=new GameObject("XIII VR grapple preview");previewHolder.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(previewHolder);
        preview=UnityEngine.Object.Instantiate(source,previewHolder.transform);preview.name="XIII VR grapple preview mesh";
        int removed=0;
        for(int pass=0;pass<4;pass++)
        {
            bool left=false;
            foreach(var c in preview.GetComponentsInChildren(Il2CppType.Of<Component>(),true))
            {
                if(c==null||c.TryCast<Transform>()!=null||c.TryCast<MeshFilter>()!=null||c.TryCast<MeshRenderer>()!=null||c.TryCast<SkinnedMeshRenderer>()!=null)continue;
                try{UnityEngine.Object.DestroyImmediate(c);removed++;}catch{left=true;}
            }
            if(!left)break;
        }
        int shown=0;
        foreach(var c in preview.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var r=c.TryCast<Renderer>();if(r==null)continue;
            r.enabled=true;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;shown++;
            var skin=r.TryCast<SkinnedMeshRenderer>();if(skin!=null)skin.updateWhenOffscreen=true;
        }
        foreach(var c in preview.GetComponentsInChildren(Il2CppType.Of<Transform>(),true)){var t=c.TryCast<Transform>();if(t!=null)t.gameObject.SetActive(true);}
        preview.SetActive(false);previewHolder.SetActive(true);
        Bootstrap.Write("GRAPPLE preview built from "+source.name+" renderers="+shown+" removedComponents="+removed);
        return true;
    }
    // 0.1.90: the hook in the left hand is solid for the right hand and its
    // weapon (ContactRig peer shape): spheres along each part's long axis.
    private ContactSphere[]? deviceShape;private IntPtr shapeFor;
    internal bool TryDeviceShape(out ContactSphere[] shape,out ContactPose pose)
    {
        shape=Array.Empty<ContactSphere>();pose=default;
        try
        {
            Transform? t=DeviceShown?controller?.grappleHookInstance?.transform:preview!=null&&preview.activeInHierarchy?preview.transform:null;
            if(t==null)return false;
            if(deviceShape==null||shapeFor!=t.Pointer)
            {
                deviceShape=BuildShape(t);shapeFor=t.Pointer;float far=0,big=0;
                foreach(var sp in deviceShape){far=Math.Max(far,sp.Offset.Length());big=Math.Max(big,sp.Radius);}
                Bootstrap.Write("GRAPPLE collision spheres="+deviceShape.Length+" farthest="+far.ToString("F3")+" maxRadius="+big.ToString("F3")+" scale="+t.lossyScale.ToString("F3"));
            }
            var q=t.rotation;pose=new ContactPose(ContactWorld.V(t.position),new System.Numerics.Quaternion(q.x,q.y,q.z,q.w));
            shape=deviceShape;return shape.Length>0;
        }
        catch(Exception ex){Bootstrap.Warn("GRAPPLE collision: "+ex.Message);deviceShape=Array.Empty<ContactSphere>();return false;}
    }
    // The same spheres in the left hand root's frame, so the hand + hook stop
    // together at walls (ContactRig adds them to the left hand shape).
    private Transform? lastHand;private ContactSphere[]? handShape;private int handShapeFrame=-1;
    internal bool TryHandShape(out ContactSphere[] shape)
    {
        shape=Array.Empty<ContactSphere>();
        if(handShapeFrame==Time.frameCount&&handShape!=null){shape=handShape;return shape.Length>0;}
        if(lastHand==null||!TryDeviceShape(out var device,out var pose))return false;
        var inverse=Quaternion.Inverse(lastHand.rotation);var origin=lastHand.position;var list=new ContactSphere[device.Length];
        for(int i=0;i<device.Length;i++){var w=ContactWorld.U(pose.Point(device[i].Offset));list[i]=new ContactSphere(ContactWorld.V(inverse*(w-origin)),device[i].Radius);}
        handShape=list;handShapeFrame=Time.frameCount;shape=list;return true;
    }
    private static ContactSphere[] BuildShape(Transform root)
    {
        var parts=new List<ContactSphere>();var inverse=Quaternion.Inverse(root.rotation);
        foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var r=c.TryCast<Renderer>();if(r==null)continue;
            Bounds local;var skin=r.TryCast<SkinnedMeshRenderer>();
            if(skin!=null)local=skin.localBounds;
            else{var mesh=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh;if(mesh==null)continue;local=mesh.bounds;}
            // SkinnedMeshRenderer.localBounds are in the ROOT BONE's space.
            var m=(skin?.rootBone!=null?skin.rootBone:r.transform).localToWorldMatrix;bool first=true;Vector3 min=default,max=default;
            for(int i=0;i<8;i++)
            {
                var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var p=inverse*(m.MultiplyPoint3x4(corner)-root.position);
                if(first){min=max=p;first=false;}else{min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            }
            var size=max-min;if(size.magnitude>.6f||size.magnitude<.01f)continue;
            int axis=size.x>=size.y&&size.x>=size.z?0:size.y>=size.z?1:2;
            float along=size[axis],other=Math.Max(Math.Min(size[(axis+1)%3],size[(axis+2)%3]),Math.Max(size[(axis+1)%3],size[(axis+2)%3])*.6f);
            float radius=Math.Clamp(other*.45f,.012f,.045f);int n=Math.Max(1,(int)MathF.Ceiling(along/(radius*1.4f)));
            var center=(min+max)*.5f;var dir=Vector3.zero;dir[axis]=1;
            for(int j=0;j<n;j++){var o=center+dir*(along*((j+.5f)/n-.5f));parts.Add(new ContactSphere(ContactWorld.V(o),radius));}
        }
        return parts.ToArray();
    }
    internal void HidePreview(){if(preview!=null&&preview.activeSelf)preview.SetActive(false);}
    private void DestroyPreview(){if(previewHolder!=null)UnityEngine.Object.Destroy(previewHolder);previewHolder=null;preview=null;}
    // Render pose pass, after animation: move the game's hook to the VR hand.
    internal void Render(Transform? left,Transform? right=null)
    {
        Hands(left,right);
        var holding=Side==0?left:right;if(holding!=null)lastHand=holding;
        if(!Active||controller==null){DeviceShown=false;return;}
        try
        {
            var cable=controller.cableComponentGameObject;var end=controller.endGameObject;
            var fake=controller.playerFakeArmsAnimationControl;
            var instance=controller.grappleHookInstance;
            if(instance!=null&&instance.Pointer!=hookInstance)
            {
                Bootstrap.Write("GRAPPLE instance local="+instance.transform.localPosition.ToString("F3")+" rot="+instance.transform.localRotation.eulerAngles.ToString("F0"));
                // Read the authored local placement before this class moves it.
                hookInstance=instance.Pointer;var t=instance.transform;
                hookFromWrist=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);Bodies(t);
            }
            if(fake!=null)Hide(fake.transform,instance,cable,end);
            DeviceShown=false;
            if(instance!=null&&holding!=null&&hookFromWrist!=null&&Placement(Side,out var m,out _))
            {
                var t=instance.transform;var want=m.Scale;
                // 0.1.197: exactly where the ready copy is (its parent, the game's left wrist, mirrors).
                var local=(t.parent!=null?t.parent.worldToLocalMatrix:Matrix4x4.identity)*Matrix4x4.TRS(m.Position,m.Rotation,want);
                var d=ItemPlacement.Decompose(new System.Numerics.Vector3(local.m00,local.m10,local.m20),new System.Numerics.Vector3(local.m01,local.m11,local.m21),new System.Numerics.Vector3(local.m02,local.m12,local.m22));
                if(d!=null){t.localPosition=local.GetColumn(3);t.localRotation=new Quaternion(d.Value.rotation.X,d.Value.rotation.Y,d.Value.rotation.Z,d.Value.rotation.W);t.localScale=new Vector3(d.Value.scale.X,d.Value.scale.Y,d.Value.scale.Z);}
                // 0.1.185: the game's own hook is
                // not drawn at first (switched off, or its arm still folded
                // away to nothing) - the hand held nothing meanwhile. Now the
                // hand keeps the ready copy until the game's hook is really drawn there.
                DeviceShown=Drawn(instance,t,m.Position,want);
                // The game's own hand pose around the hook, for the VR hand.
                // (0.1.88: the game's own left hand is open around the hook, so
                // its pose is not copied; the VR hand holds it as a pistol.)
            }
            else if(instance!=null)Hide(instance.transform,null,cable,end);
            foreach(var entry in hidden.Values)if(entry.renderer!=null)entry.renderer.enabled=false;
            if(!reported)
            {
                reported=true;
                Bootstrap.Write("GRAPPLE visuals: hidden="+hidden.Count+" deviceInLeftHand="+DeviceShown+" instance="+Path(instance?.transform)+" cable="+Path(cable?.transform)
                    +" fake="+Path(fake?.transform));
            }
        }
        catch(Exception ex){Bootstrap.Warn("GRAPPLE render: "+ex.Message);}
    }
    private static float Safe(float v)=>Math.Abs(v)<1e-5f?1:v;
    private readonly List<Renderer> instanceRenderers=new();private IntPtr renderersFor;private bool notDrawnReported;
    private bool Drawn(GameObject instance,Transform t,Vector3 at,Vector3 scale)
    {
        string why="";
        try
        {
            if(!instance.activeInHierarchy)why="switched off";
            else if((t.position-at).sqrMagnitude>.05f*.05f)why="not where it was put";
            else if(t.lossyScale.sqrMagnitude<scale.sqrMagnitude*.25f)why="folded away (scale "+t.lossyScale.magnitude.ToString("F2")+")";
            else
            {
                if(renderersFor!=instance.Pointer)
                {
                    renderersFor=instance.Pointer;instanceRenderers.Clear();
                    foreach(var c in instance.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true)){var r=c.TryCast<Renderer>();if(r!=null&&r.TryCast<ParticleSystemRenderer>()==null)instanceRenderers.Add(r);}
                }
                bool any=false;foreach(var r in instanceRenderers)if(r!=null&&r.enabled&&r.gameObject.activeInHierarchy){any=true;break;}
                if(!any)why="not drawn by the game";
            }
        }
        catch(Exception ex){why=ex.Message;}
        if(why.Length==0){notDrawnReported=false;return true;}
        if(!notDrawnReported){notDrawnReported=true;Bootstrap.Write("GRAPPLE the game's hook is "+why+" yet: the hand keeps the ready copy");}
        return false;
    }
    private void Hide(Transform root,GameObject? keep,GameObject? cable,GameObject? end)
    {
        foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var r=c.TryCast<Renderer>();if(r==null)continue;
            var t=r.transform;
            if(keep!=null&&t.IsChildOf(keep.transform)||cable!=null&&t.IsChildOf(cable.transform)||end!=null&&t.IsChildOf(end.transform))continue;
            if(r.TryCast<ParticleSystemRenderer>()!=null)continue;
            if(r.GetComponentInParent(Il2CppType.Of<RopeTubeRendererController>())!=null)continue;
            string n=t.name.ToLowerInvariant();if(n.Contains("cable")||n.Contains("rope"))continue;
            int id=r.GetInstanceID();if(!hidden.ContainsKey(id))hidden.Add(id,(r,r.enabled));
        }
    }
    private static string Path(Transform? t)
    {
        if(t==null)return "none";var s=t.name;
        for(var p=t.parent;p!=null&&s.Length<160;p=p.parent)s=p.name+"/"+s;return s;
    }
    private void Restore()
    {
        foreach(var entry in hidden.Values)if(entry.renderer!=null)entry.renderer.enabled=entry.enabled;
        hidden.Clear();reported=false;
    }
    public void Dispose(){Restore();DestroyPreview();patches.UnpatchSelf();if(Current==this)Current=null;}
}
