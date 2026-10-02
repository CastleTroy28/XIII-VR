using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
namespace XiiiXR;
// 0.1.124: a still, render-only copy of one of the player's weapons: on its
// body place, or lying on the floor after it was let go. No collider; the
// fall to the floor is a swept kinematic drop (like dropped magazines).
internal sealed class HolsterCopy:IDisposable
{
    private GameObject? root;private readonly List<Mesh> meshes=new();
    private readonly List<(Transform part,Mesh mesh)> parts=new();
    internal Vector3 Middle{get;private set;}
    internal Vector3 CenterWorld=>root!=null?root.transform.TransformPoint(Middle):Vector3.zero;
    internal Transform? RootTransform=>root!=null?root.transform:null;
    // 0.1.125: the largest readable part for the white outline of a pointed
    // weapon lying on the floor.
    internal bool OutlineSource(out Mesh? mesh,out Matrix4x4 toWorld)
    {
        mesh=null;toWorld=Matrix4x4.identity;int best=0;
        foreach(var (part,m) in parts)
        {
            if(part==null||m==null||!m.isReadable||m.vertexCount<=best)continue;
            best=m.vertexCount;mesh=m;toWorld=part.localToWorldMatrix;
        }
        return mesh!=null;
    }
    // Fitted frame: +Z towards the muzzle; Grip is where the hand holds it.
    internal Vector3 Grip{get;private set;}
    // 0.1.146: its collision in a hand (fitted space, like the weapon's own):
    // the occupied cells of its mesh, as close to the model as they go.
    internal ContactSphere[]? Shape{get;private set;}
    internal Vector3 Rear{get;private set;}
    internal Vector3 Front{get;private set;}
    internal string Profile{get;}
    internal bool Shown=>root!=null&&root.activeSelf;
    // 0.1.185: every part still has its mesh (a level unloading its assets
    // takes the game's own meshes from a copy kept across levels).
    internal bool Intact
    {
        get
        {
            if(root==null||parts.Count==0)return false;
            foreach(var (part,mesh) in parts)if(part==null||mesh==null)return false;
            return true;
        }
    }
    internal Vector3 Position=>root!=null?root.transform.position:Vector3.zero;
    internal Quaternion Rotation=>root!=null?root.transform.rotation:Quaternion.identity;
    // Floor state.
    internal bool OnFloor{get;private set;}
    internal bool Resting{get;private set;}
    private Vector3 velocity,spin;private float fallStarted,soundAt=-1;private int bounces;
    // 0.1.161:
    // let go with the gun already in the floor (the hand at the floor), the
    // fall began under it and nothing stopped it; and a floor the game made
    // for walking only (collider_player) is skipped by the fall's sweep. Its
    // start is brought out to the hand's side of what is between them, and
    // it never goes below the floor found under it when let go.
    private Vector3 dropHand,floorAt;private bool checkStart,floorChecked;private float floorY=float.NaN;
    // 0.1.174: the water's bullet-splash plane counted as a
    // floor. Now it does not; the water's height is kept, and under it the
    // gun sinks slowly (water drag) to the bottom.
    private float waterY=float.NaN;private bool wasUnder;
    private static Il2CppStructArray<RaycastHit>? floorHits;
    private HolsterCopy(string profile){Profile=profile;}
    // 0.1.129: a grenade copy: the part holding its pin, and meshes of the
    // grenade without its pin and of the pin alone (for the hand pulling it).
    private MeshFilter? pinFilter;private Mesh? withPin,pinless,pinOnly;private Material[] pinMaterials=Array.Empty<Material>();private Matrix4x4 pinLocal=Matrix4x4.identity;private Vector3 pinCenter;
    private bool pinPulled,twin;
    // 0.1.132: the slide/bolt: closed (the still) and cycled back (a shot, or
    // locked back when empty); a small kick of the whole weapon on a shot.
    private MeshFilter? cycleFilter;private Mesh? closedMesh,openMesh;
    private float shotAt=float.NegativeInfinity;private bool lockedBack;
    internal bool HasCycle=>cycleFilter!=null&&closedMesh!=null&&openMesh!=null;
    internal void Shot(float now){shotAt=now;}
    internal void LockBack(bool locked){lockedBack=locked;}
    internal const float CycleSeconds=.07f,KickSeconds=.12f;
    // Every frame before drawing.
    internal void Animate(float now,out float kick,out float pitch)
    {
        float age=now-shotAt;
        float k=age>=0&&age<KickSeconds?1-age/KickSeconds:0;kick=.02f*k*k;pitch=4f*k*k;
        if(cycleFilter==null||closedMesh==null||openMesh==null)return;
        var want=lockedBack||age>=0&&age<CycleSeconds?openMesh:closedMesh;
        if(cycleFilter.sharedMesh!=want)cycleFilter.sharedMesh=want;
    }
    // 0.1.183: a pistol reloaded with its own hand (both hands full): its
    // magazine out - the magazine's triangles left out of the drawn meshes
    // (the slide meshes, or the part's own copy); a copy of the magazine to
    // drop; where the magazine sits and the butt of the grip (fitted frame).
    private readonly List<Mesh> magazineMeshes=new();private int[][]? magazineKept,magazineFull;private bool magazineOut;
    internal ReloadMesh? MagazineTemplate{get;private set;}
    internal Vector3 MagazineCenter{get;private set;}
    internal Vector3 Butt{get;private set;}
    internal bool HasMagazine=>magazineMeshes.Count>0&&magazineKept!=null&&magazineFull!=null;
    internal Vector3 ButtWorld=>root!=null?root.transform.TransformPoint(Butt):Vector3.zero;
    internal Vector3 MagazineWorld=>root!=null?root.transform.TransformPoint(MagazineCenter):Vector3.zero;
    internal bool MagazineOut
    {
        get=>magazineOut;
        set{if(magazineOut==value)return;magazineOut=value;ShowMagazine();}
    }
    private void ShowMagazine()
    {
        var indices=magazineOut?magazineKept:magazineFull;if(indices==null)return;
        foreach(var m in magazineMeshes)
        {
            if(m==null||m.subMeshCount!=indices.Length)continue;
            try{for(int sub=0;sub<indices.Length;sub++)m.SetTriangles(indices[sub],sub,false,0);}
            catch(Exception ex){Bootstrap.Warn("HOLSTER "+Profile+" magazine "+(magazineOut?"out":"in")+": "+ex.Message);}
        }
    }
    private static bool Holds(List<Mesh> list,Mesh? mesh){if(mesh==null)return false;foreach(var m in list)if(m==mesh)return true;return false;}
    internal bool HasPin=>pinFilter!=null&&pinless!=null;
    internal bool PinPulled
    {
        get=>pinPulled;
        set{if(pinFilter==null||pinless==null||withPin==null||pinPulled==value)return;pinPulled=value;pinFilter.sharedMesh=value?pinless:withPin;}
    }
    // A mesh of the pin alone (owned by the caller) and where it is drawn now.
    internal bool TakePinMesh(out Mesh? mesh,out Material[] materials,out Matrix4x4 world,out Vector3 center)
    {
        mesh=null;materials=pinMaterials;world=Matrix4x4.identity;center=pinCenter;
        if(pinOnly==null||root==null)return false;
        var m=UnityEngine.Object.Instantiate(pinOnly).TryCast<Mesh>();if(m==null)return false;
        m.name="XIII grenade pin";mesh=m;world=root.transform.localToWorldMatrix*pinLocal;return true;
    }
    // 0.1.130: an independent copy (its own meshes): another weapon of this kind.
    internal HolsterCopy? Clone()
    {
        if(root==null)return null;
        var t=new HolsterCopy(Profile){Grip=Grip,Middle=Middle,Rear=Rear,Front=Front,Shape=Shape,MagazineCenter=MagazineCenter,Butt=Butt};
        t.magazineKept=magazineKept;t.magazineFull=magazineFull;t.magazineOut=magazineOut;
        var go=UnityEngine.Object.Instantiate(root).TryCast<GameObject>();if(go==null)return null;
        go.name="XIII body weapon extra "+Profile;UnityEngine.Object.DontDestroyOnLoad(go);go.SetActive(false);t.root=go;
        try
        {
            foreach(var c in go.GetComponentsInChildren(Il2CppType.Of<MeshFilter>(),true))
            {
                var f=c.TryCast<MeshFilter>();if(f==null||f.sharedMesh==null)continue;
                bool owned=false;foreach(var m in meshes)if(m==f.sharedMesh)owned=true;
                bool cycled=closedMesh!=null&&openMesh!=null&&(f.sharedMesh==closedMesh||f.sharedMesh==openMesh);
                if(cycled)
                {
                    var a=UnityEngine.Object.Instantiate(closedMesh!).TryCast<Mesh>();var b=UnityEngine.Object.Instantiate(openMesh!).TryCast<Mesh>();
                    if(a!=null&&b!=null)
                    {
                        a.hideFlags=b.hideFlags=HideFlags.DontUnloadUnusedAsset;t.meshes.Add(a);t.meshes.Add(b);f.sharedMesh=a;t.cycleFilter=f;t.closedMesh=a;t.openMesh=b;
                        if(Holds(magazineMeshes,closedMesh)){t.magazineMeshes.Add(a);t.magazineMeshes.Add(b);}
                    }
                }
                else if(owned)
                {
                    var source=f.sharedMesh;var m=UnityEngine.Object.Instantiate(source).TryCast<Mesh>();
                    if(m!=null){m.hideFlags=HideFlags.DontUnloadUnusedAsset;t.meshes.Add(m);f.sharedMesh=m;if(Holds(magazineMeshes,source))t.magazineMeshes.Add(m);}
                }
                t.parts.Add((f.transform,f.sharedMesh));
            }
            // 0.1.183: its own magazine to drop.
            if(MagazineTemplate!=null){try{t.MagazineTemplate=MagazineTemplate.Copy().Keep();}catch(Exception ex){Bootstrap.Warn("HOLSTER another "+Profile+": no magazine to drop ("+ex.Message+")");}}
        }
        catch{t.Dispose();throw;}
        return t;
    }
    // 0.1.130: a point on the weapon (its muzzle) that effects can follow.
    private Transform? anchor;
    internal Transform? Anchor(Vector3 local)
    {
        if(root==null)return null;
        if(anchor==null){var go=new GameObject("XIII copy muzzle");go.transform.SetParent(root.transform,false);anchor=go.transform;}
        anchor.localPosition=local;anchor.localRotation=Quaternion.identity;return anchor;
    }
    internal Vector3 RearWorld=>root!=null?root.transform.TransformPoint(Rear):Vector3.zero;
    internal Vector3 FrontWorld=>root!=null?root.transform.TransformPoint(Front):Vector3.zero;
    // 0.1.129: a second copy on the body place while this one is in a hand
    // (more grenades or knives left). Shares this copy's meshes: the owner
    // disposes it before this one.
    internal HolsterCopy? Twin()
    {
        if(root==null)return null;
        var t=new HolsterCopy(Profile){twin=true,Grip=Grip,Middle=Middle,Rear=Rear,Front=Front,Shape=Shape};
        var go=UnityEngine.Object.Instantiate(root).TryCast<GameObject>();if(go==null)return null;
        go.name="XIII body weapon twin "+Profile;UnityEngine.Object.DontDestroyOnLoad(go);go.SetActive(false);t.root=go;
        if(pinFilter!=null&&withPin!=null)
            foreach(var c in go.GetComponentsInChildren(Il2CppType.Of<MeshFilter>(),true)){var f=c.TryCast<MeshFilter>();if(f!=null&&(f.sharedMesh==pinless||f.sharedMesh==withPin))f.sharedMesh=withPin;}
        return t;
    }
    // center: placed by the middle of the item (knife, grenade: their fitted
    // origin lies far from the item itself, so they hung below the belt).
    internal static HolsterCopy From(WeaponVisual visual,Vector3 grip,bool center=false,Matrix4x4[]?[]? poses=null)
    {
        var copy=new HolsterCopy(visual.Profile);
        try
        {
            // 0.1.183: a pistol's magazine (its hand drops it; struck against the chest a new one goes in).
            // Only with the manual reload (a copy is built again each time it is put away).
            // 0.1.194: the Uzi's too.
            bool pistol=EquipmentProfile.ChestMagazine(visual.Profile);
            if(pistol&&!visual.ReloadAvailable&&WeaponOptions.ManualReload.Value)visual.PrepareReload();
            copy.root=new GameObject("XIII VR body weapon "+visual.Profile);copy.root.SetActive(false);UnityEngine.Object.DontDestroyOnLoad(copy.root);
            bool any=false;var min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);var max=-min;
            var pinSource=visual.HasPin?visual.PinSourceMesh:null;
            foreach(var (source,baked,materials,local,magazine) in visual.StillParts(poses))
            {
                var mesh=source;
                if(baked){mesh=UnityEngine.Object.Instantiate(source).TryCast<Mesh>()!;mesh.hideFlags=HideFlags.DontUnloadUnusedAsset;mesh.name="XIII still "+visual.Profile;copy.meshes.Add(mesh);}
                var go=new GameObject("part");go.transform.SetParent(copy.root.transform,false);copy.parts.Add((go.transform,mesh));
                go.transform.localPosition=local.GetColumn(3);go.transform.localRotation=local.rotation;go.transform.localScale=local.lossyScale;
                var filter=go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!;filter.sharedMesh=mesh;
                // 0.1.132: the slide/bolt closed in the still, and a cycled-back mesh for shots.
                if(baked&&copy.cycleFilter==null&&visual.MechanismTravel>0)
                {
                    try
                    {
                        var closed=new Mesh{name="XIII still "+visual.Profile+" closed",hideFlags=HideFlags.DontUnloadUnusedAsset};
                        var open=new Mesh{name="XIII still "+visual.Profile+" cycled",hideFlags=HideFlags.DontUnloadUnusedAsset};
                        if(visual.BakeStillMechanism(source,closed,0)&&visual.BakeStillMechanism(source,open,visual.MechanismTravel))
                        {copy.meshes.Add(closed);copy.meshes.Add(open);copy.cycleFilter=filter;copy.closedMesh=closed;copy.openMesh=open;filter.sharedMesh=closed;mesh=closed;copy.parts[copy.parts.Count-1]=(go.transform,closed);}
                        else{UnityEngine.Object.Destroy(closed);UnityEngine.Object.Destroy(open);}
                    }
                    catch(Exception ex){Bootstrap.Warn("HOLSTER still "+visual.Profile+" slide: "+ex.Message);}
                }
                if(pistol&&magazine&&copy.magazineMeshes.Count==0&&visual.MagazineKept!=null&&visual.MagazineFull!=null)
                {
                    if(copy.cycleFilter==filter&&copy.closedMesh!=null&&copy.openMesh!=null){copy.magazineMeshes.Add(copy.closedMesh);copy.magazineMeshes.Add(copy.openMesh);}
                    else if(baked)copy.magazineMeshes.Add(mesh);
                    if(copy.magazineMeshes.Count>0){copy.magazineKept=visual.MagazineKept;copy.magazineFull=visual.MagazineFull;copy.ShowMagazine();}
                }
                if(pinSource!=null&&source==pinSource&&copy.pinFilter==null)
                {
                    try
                    {
                        var without=new Mesh{name="XIII still grenade without pin",hideFlags=HideFlags.DontUnloadUnusedAsset};copy.meshes.Add(without);
                        var alone=new Mesh{name="XIII still grenade pin",hideFlags=HideFlags.DontUnloadUnusedAsset};copy.meshes.Add(alone);
                        if(visual.BakePin(without,false,out _,out _,out _)&&visual.BakePin(alone,true,out var pinMats,out _,out var pinMiddle))
                        {copy.pinFilter=filter;copy.withPin=mesh;copy.pinless=without;copy.pinOnly=alone;copy.pinMaterials=pinMats;copy.pinLocal=local;copy.pinCenter=pinMiddle;}
                    }
                    catch(Exception ex){Bootstrap.Warn("GRENADE copy without its pin: "+ex.Message);}
                }
                var r=go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
                r.sharedMaterials=materials;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
                var b=mesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var p=local.MultiplyPoint3x4(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));
                    min=Vector3.Min(min,p);max=Vector3.Max(max,p);any=true;
                }
            }
            if(!any)throw new InvalidOperationException("no mesh to copy");
            var c=(min+max)*.5f;copy.Grip=center?c:grip;copy.Middle=c;copy.Rear=new Vector3(c.x,c.y,min.z);copy.Front=new Vector3(c.x,c.y,max.z);
            if(!center)copy.Shape=visual.HeldShape();
            if(pistol)
            {
                // The bottom of the magazine (the butt); without the game's
                // magazine, under the hand at the grip.
                if(visual.ReloadAvailable){copy.MagazineCenter=visual.MagazineCenter;copy.Butt=visual.ReloadPort;}
                else{copy.Butt=copy.Grip+ButtBelowGrip;copy.MagazineCenter=copy.Grip+ButtBelowGrip*.5f;}
                try{if(visual.Ammunition!=null)copy.MagazineTemplate=visual.Ammunition.Copy().Keep();}
                catch(Exception ex){Bootstrap.Warn("HOLSTER "+visual.Profile+": no magazine to drop ("+ex.Message+")");}
            }
            return copy;
        }
        catch{copy.Dispose();throw;}
    }
    // A pistol's butt from where the hand holds it (fitted frame), when the
    // game's magazine could not be found.
    internal static readonly Vector3 ButtBelowGrip=new(0,-.075f,-.02f);
    // The copy placed with its grip at a point.
    internal void PoseGrip(Vector3 grip,Quaternion rotation)=>Pose(grip-rotation*Grip,rotation);
    // 0.1.128: held in a hand at a given point of the weapon (the left hand
    // holds it at its mirrored grip).
    internal void PoseAt(Vector3 hand,Quaternion rotation,Vector3 point)=>Pose(hand-rotation*point,rotation);
    internal void Pose(Vector3 position,Quaternion rotation)
    {
        if(root==null)return;
        root.transform.SetPositionAndRotation(position,rotation);
        if(!root.activeSelf)root.SetActive(true);
    }
    internal void Hide(){if(root!=null&&root.activeSelf)root.SetActive(false);}
    // Distance from a point to the copy's length (rear to muzzle), world.
    internal float Distance(Vector3 point)
    {
        if(root==null)return float.PositiveInfinity;
        var a=root.transform.TransformPoint(Rear);var b=root.transform.TransformPoint(Front);var ab=b-a;
        float t=ab.sqrMagnitude>1e-8f?Mathf.Clamp01(Vector3.Dot(point-a,ab)/ab.sqrMagnitude):0;
        return Vector3.Distance(point,a+ab*t);
    }
    internal Vector3 GripWorld=>root!=null?root.transform.TransformPoint(Grip):Vector3.zero;
    // Let go away from the body: flies, turns, bounces and comes to rest
    // lying on its side.
    // 0.1.135: it keeps the hand's turn (a flick of the wrist spins it) and
    // its middle flies with the hand's speed plus that turn (it was thrown
    // straight and fell without turning).
    internal const float MaxThrowSpeed=12,MaxSpin=25;
    internal void Drop(Vector3 position,Quaternion rotation,Vector3 throwVelocity)=>Drop(position,rotation,throwVelocity,Vector3.zero,Vector3.zero,false);
    internal void Drop(Vector3 position,Quaternion rotation,Vector3 handVelocity,Vector3 handSpin,Vector3 hand,bool fromHand=true)
    {
        OnFloor=true;Resting=false;fallStarted=Time.realtimeSinceStartup;bounces=0;npcStruck=false;Pose(position,rotation);
        dropHand=hand;checkStart=fromHand;floorChecked=false;floorY=float.NaN;waterY=float.NaN;wasUnder=false;
        spin=Vector3.ClampMagnitude(handSpin,MaxSpin);
        var middle=position+rotation*((Rear+Front)*.5f);
        velocity=Vector3.ClampMagnitude(handVelocity+(fromHand?Vector3.Cross(spin,middle-hand):Vector3.zero),MaxThrowSpeed);
    }
    internal Vector3 Spin=>spin;
    // 0.1.141: a weapon thrown into an NPC hits it (a little damage, it reacts
    // where it was hit), once a throw, and bounces off it. The handler (the
    // punch driver) says whether that collider took the hit.
    internal static Func<Collider,RaycastHit,Vector3,string,bool>? StruckNpc;
    internal const float NpcStrikeSpeed=2.5f;
    private bool npcStruck;
    private static Il2CppStructArray<RaycastHit>? npcHits;
    private static readonly List<(float distance,int index)> npcOrder=new();
    // 0.1.174: only the
    // gun's middle was tested, and a long gun's ends pass through a body the
    // middle misses; its two ends are tested too.
    private bool StrikeNpc(Vector3 center,Vector3 step,float reach)
    {
        if(StrikeNpcFrom(center,step,reach))return true;
        if(root==null)return false;
        return StrikeNpcFrom(RearWorld,step,reach)||StrikeNpcFrom(FrontWorld,step,reach);
    }
    private bool StrikeNpcFrom(Vector3 center,Vector3 step,float reach)
    {
        float length=step.magnitude;if(length<1e-4f||StruckNpc==null)return false;
        var dir=step/length;
        npcHits??=new Il2CppStructArray<RaycastHit>(32);
        int found=Physics.SphereCastNonAlloc(center-dir*.03f,.06f,dir,npcHits,reach+.03f,~0,QueryTriggerInteraction.Collide);
        if(found<=0)return false;
        npcOrder.Clear();
        for(int i=0;i<found&&i<npcHits.Length;i++)
        {
            var c=npcHits[i].collider;if(c==null||!c.enabled)continue;
            if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())==null)continue;
            npcOrder.Add((npcHits[i].distance,i));
        }
        npcOrder.Sort((a,b)=>a.distance.CompareTo(b.distance));
        foreach(var (_,i) in npcOrder)
        {
            var hit=npcHits[i];
            if(hit.distance<=0||!float.IsFinite(hit.point.sqrMagnitude)||hit.point==Vector3.zero){hit.point=center;hit.normal=-dir;}
            if(!StruckNpc(hit.collider,hit,velocity,Profile))continue;
            npcStruck=true;
            var n=hit.normal.sqrMagnitude>1e-6f?hit.normal.normalized:-dir;
            if(Vector3.Dot(n,dir)>0)n=-n;
            // It glances off, slowed, and falls.
            velocity=Vector3.Reflect(velocity,n)*.3f;spin*=.5f;
            return true;
        }
        return false;
    }
    internal void Lift(){OnFloor=false;Resting=false;velocity=Vector3.zero;spin=Vector3.zero;}
    // The first surface along a ray that the thing stops at (not the player,
    // not a trigger, not a loose thing; walking-only floors count when level).
    private static bool Surface(Vector3 from,Vector3 direction,float length,Transform? player,out RaycastHit found)=>Surface(from,direction,length,player,out found,out _);
    private static bool Surface(Vector3 from,Vector3 direction,float length,Transform? player,out RaycastHit found,out float water)
    {
        found=default;water=float.NaN;floorHits??=new Il2CppStructArray<RaycastHit>(32);
        int n=Physics.RaycastNonAlloc(from,direction,floorHits,length,~0,QueryTriggerInteraction.Ignore);float best=float.PositiveInfinity,nearestWater=float.PositiveInfinity;
        for(int i=0;i<Math.Min(n,floorHits.Length);i++)
        {
            var h=floorHits[i];var c=h.collider;if(c==null||!c.enabled)continue;
            if(player!=null&&c.transform.IsChildOf(player))continue;
            if(IsWater(c)){if(h.distance<nearestWater){nearestWater=h.distance;water=h.point.y;}continue;}
            if(c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic)continue;
            if(ContactFilter.NonPhysicalName(c.name)||c.transform.parent!=null&&ContactFilter.NonPhysicalName(c.transform.parent.name))
            {if(h.normal.y<.7f||c.name.StartsWith("XIII ",StringComparison.OrdinalIgnoreCase))continue;}
            if(h.distance<best){best=h.distance;found=h;}
        }
        return float.IsFinite(best);
    }
    private void FloorUnder(Vector3 center,Transform? player)
    {
        floorAt=center;floorY=Surface(center+Vector3.up*.05f,Vector3.down,60,player,out var floor,out float water)?floor.point.y:float.NaN;
        if(float.IsFinite(water))waterY=water;
        // Already under the water: its surface above.
        else if(!float.IsFinite(waterY)){Surface(center,Vector3.up,30,player,out _,out float above);if(float.IsFinite(above))waterY=above;}
    }
    // The water: its bullet-splash plane (the game's WaterProjectileDetection) and its swim volumes.
    internal static bool IsWater(Collider c)
    {
        string n=c.name;
        if(n.StartsWith("projectileDetection",StringComparison.OrdinalIgnoreCase)||n.IndexOf("SwimVolume",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("swim_volume",StringComparison.OrdinalIgnoreCase)>=0)return true;
        try{return c.GetComponentInParent(Il2CppType.Of<WaterProjectileDetection>())!=null;}catch(Exception){return false;}
    }
    private void CheckFloor(Transform? player,ref Vector3 center)
    {
        floorChecked=true;
        try
        {
            if(checkStart)
            {
                var d=center-dropHand;float length=d.magnitude;
                if(length>.02f&&length<2&&Surface(dropHand,d/length,length,player,out var between))
                {
                    var moved=between.point+between.normal*.04f;
                    Bootstrap.Write("HOLSTER "+Profile+" let go inside "+between.collider.name+": brought out to the hand's side ("+(moved-center).magnitude.ToString("F2")+" m)");
                    center=moved;
                }
            }
            FloorUnder(center,player);
        }
        catch(Exception ex){Bootstrap.Warn("HOLSTER floor check: "+ex.Message);}
    }
    internal void Fall(ContactWorld world,float dt)
    {
        if(root==null||!OnFloor||Resting)return;
        var t=root.transform;var local=(Rear+Front)*.5f;var center=t.TransformPoint(local);
        if(!floorChecked)
        {
            var before=center;CheckFloor(world.Player,ref center);
            if(center!=before){Pose(center-t.rotation*local,t.rotation);velocity*=.3f;}
        }
        // 0.1.174: under the water it sinks slowly: drag on the motion and the turn, little weight.
        bool under=float.IsFinite(waterY)&&center.y<waterY;
        if(under)
        {
            if(!wasUnder){wasUnder=true;fallStarted=Time.realtimeSinceStartup;Bootstrap.Write("HOLSTER "+Profile+" went into the water: it sinks");}
            float drag=Mathf.Exp(-3.5f*dt);velocity*=drag;spin*=drag;
            velocity+=Vector3.down*(9.81f*.3f*dt);if(velocity.y< -1.2f)velocity.y=-1.2f;
        }
        else velocity+=Vector3.down*(9.81f*dt);
        var end=center+velocity*dt;
        float f=world.Sweep(ContactWorld.V(center),ContactWorld.V(end),.03f,out var normal);
        // 0.1.141: an NPC before the wall (or the floor) takes the hit.
        if(!npcStruck&&StruckNpc!=null&&velocity.sqrMagnitude>NpcStrikeSpeed*NpcStrikeSpeed)
        {
            try
            {
                // 0.1.174: the whole step (the middle may be stopped by something the ends pass).
                if(StrikeNpc(center,end-center,(end-center).magnitude))
                {
                    end=center+velocity*dt;f=world.Sweep(ContactWorld.V(center),ContactWorld.V(end),.03f,out normal);
                }
            }
            catch(Exception ex){npcStruck=true;Bootstrap.Warn("THROWN weapon vs NPC: "+ex.Message);}
        }
        var moved=Vector3.Lerp(center,end,f);
        var rotation=t.rotation;
        // Turning in the air (a little air drag on the turn).
        spin*=Mathf.Exp(-.25f*dt);
        float turn=spin.magnitude*Mathf.Rad2Deg*dt;
        if(turn>1e-4f)rotation=Quaternion.AngleAxis(turn,spin.normalized)*rotation;
        // 0.1.174: never left hanging in the air after the time runs out: put on the floor under it.
        bool settle=Time.realtimeSinceStartup-fallStarted>(under?40:6);
        if(settle&&float.IsFinite(floorY)&&moved.y>floorY+.05f)moved.y=floorY+.03f;
        // Never below the floor found under it when let go (found again when
        // it has slid or bounced off: off a table it falls to the floor).
        if(floorChecked&&new Vector2(moved.x-floorAt.x,moved.z-floorAt.z).sqrMagnitude>.25f*.25f)
        {try{FloorUnder(center,world.Player);}catch(Exception){floorY=float.NaN;}}
        if(float.IsFinite(floorY)&&moved.y<floorY+.02f&&velocity.y<=0)
        {
            float into=-velocity.y,now=Time.realtimeSinceStartup;
            if(f>=1&&now-soundAt>=WeaponImpactMath.MinGap){soundAt=now;WeaponImpactAudio.Current?.Hit(moved,Vector3.up,into,Profile);}
            moved.y=floorY+.03f;f=0;settle=true;
        }
        if(f<1&&!settle)
        {
            var n=ContactWorld.U(normal);if(n.sqrMagnitude<1e-6f)n=Vector3.up;n.Normalize();
            bool leaving=Vector3.Dot(velocity,n)>=0;
            // 0.1.137: its sound on the surface it lands on.
            float into=-Vector3.Dot(velocity,n),now=Time.realtimeSinceStartup;
            if(!leaving&&now-soundAt>=WeaponImpactMath.MinGap){soundAt=now;WeaponImpactAudio.Current?.Hit(moved,n,into,Profile);}
            var bounce=ThrowMath.Bounce(ContactWorld.V(velocity),ContactWorld.V(spin),ContactWorld.V(n),bounces);
            if(bounce.bounced)
            {
                velocity=ContactWorld.U(bounce.velocity);spin=ContactWorld.U(bounce.spin);if(!leaving)bounces++;
                moved+=n*.01f;
            }
            // 0.1.174: stopped
            // against a wall or a body (not a floor) it slides down along it.
            else if(n.y>=.5f)settle=true;
            else{float vn=Vector3.Dot(velocity,n);velocity=(velocity-n*Math.Min(0,vn))*.5f;spin*=.5f;moved+=n*.01f;}
            if(settle)moved+=n*.02f;
        }
        if(settle)
        {
            // Lying on its side: the muzzle stays where it pointed, flat, on
            // the side nearer to how it came down.
            var forward=rotation*Vector3.forward;forward.y=0;if(forward.sqrMagnitude<1e-4f)forward=rotation*Vector3.up;forward.y=0;
            if(forward.sqrMagnitude<1e-4f)forward=Vector3.forward;
            var up=Vector3.Cross(Vector3.up,forward.normalized);if(Vector3.Dot(up,rotation*Vector3.up)<0)up=-up;
            rotation=Quaternion.LookRotation(forward.normalized,up);
            Resting=true;velocity=Vector3.zero;spin=Vector3.zero;
        }
        Pose(moved-rotation*local,rotation);
    }
    public void Dispose()
    {
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
        if(!twin)foreach(var m in meshes)if(m!=null)UnityEngine.Object.Destroy(m);
        meshes.Clear();parts.Clear();pinFilter=null;withPin=pinless=pinOnly=null;anchor=null;cycleFilter=null;closedMesh=openMesh=null;
        magazineMeshes.Clear();MagazineTemplate?.Dispose();MagazineTemplate=null;
    }
}
