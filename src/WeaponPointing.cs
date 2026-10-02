using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.125: a weapon lying in the world (one the player let go, or a game
// weapon pickup) is taken by pointing a hand at it and pressing that hand's
// grip; with the interaction hints on, the pointed weapon has a white
// outline. The weapon goes to the right hand (the game has one weapon hand).
internal sealed class WeaponPointing:IDisposable
{
    internal enum Kind{None,Copy,Pickup}
    internal Kind Target{get;private set;}
    internal int TargetKey{get;private set;}=-1;
    internal WeaponPickup? TargetPickup{get;private set;}
    internal bool TargetLeft{get;private set;}
    private readonly List<WeaponPickup> pickups=new();private float nextScan;
    private readonly Dictionary<IntPtr,(Mesh? mesh,Transform? frame,bool baked)> shapes=new();
    private ReloadOutline? outline;private ReloadGlow? glow;private bool outlineFailed;
    private string lastTarget="";
    // 0.1.162: the game's weapon pickups, kept between rare scene searches
    // (every 30 s since 0.1.164, and after a scene change); one the game spawns (an enemy's
    // dropped gun) is handed over by its hook at once. Those near the player
    // are picked from the kept ones every half second.
    private readonly SceneFind<WeaponPickup> all=new("weapon-pickups",30,found=>
    {
        foreach(var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<WeaponPickup>()))
        {var p=o.TryCast<WeaponPickup>();if(p!=null&&p.gameObject.scene.IsValid()&&!p.isAIOnlyWeapon)found.Add(p);}
    });
    private bool fresh;
    internal WeaponPointing()
    {
        SceneHooks.Pickup=p=>{try{if(!p.isAIOnlyWeapon){all.Add(p);fresh=true;}}catch(Exception){}};
    }
    // Game weapon pickups near the player.
    internal void Scan(Vector3 head,Transform? player=null,int solid=~0)
    {
        this.head=head;this.player=player;this.solid=solid==0?~0:solid;
        bool searched;
        try{searched=all.Refresh();}
        catch(Exception ex){Bootstrap.Warn("POINT GRAB pickup search: "+ex.Message);pickups.Clear();return;}
        float now=Time.realtimeSinceStartup;
        if(!searched&&!fresh&&now<nextScan)return;
        nextScan=now+.5f;fresh=false;
        pickups.Clear();all.Prune();WorldStats.Pickups=all.Items.Count;
        foreach(var p in all.Items)
        {
            try
            {
                if(p==null||!p.isActiveAndEnabled||p.wasPicked)continue;
                if((p.transform.position-head).sqrMagnitude>15*15)continue;
                if(!pickups.Exists(k=>k.Pointer==p.Pointer))pickups.Add(p);
            }
            catch(Exception){}
        }
    }
    // 0.1.171: each hand has its own target; a weapon the hand is at (reaching
    // down to it, within 12 cm of it or 30 cm of its middle) is taken without
    // pointing; the cone is wider; a press a moment after the aim slipped off
    // (0.4 s) still takes what was pointed at; a press with nothing pointed
    // looks again in a cone almost twice as wide; the hand gets a short tick
    // when it comes onto a weapon (so it works the same without the outline).
    internal const float ReachBounds=.12f,ReachMiddle=.30f,Grace=.4f,PressWiden=1.8f;
    private struct Aim
    {
        internal Kind Kind;internal int Key;internal WeaponPickup? Pickup;internal HolsterCopy? Copy;internal IntPtr Id;
        internal Vector3 At;internal float Score,Seen;internal bool Reach,Wide;
        internal bool Same(in Aim o)=>Kind!=Kind.None&&Kind==o.Kind&&(Kind==Kind.Copy?Key==o.Key:Id==o.Id);
    }
    private readonly Aim[] aim=new Aim[2],recent=new Aim[2];
    private readonly Vector3[] handAt=new Vector3[2],handDir=new Vector3[2];private readonly bool[] handSeen=new bool[2];
    private readonly List<(WeaponPickup p,Vector3 center,Bounds bounds,bool hasBounds)> near=new();
    // What a hand's press takes now: its target, or the one it pointed at a moment ago.
    internal readonly struct Pick
    {
        internal readonly Kind Kind;internal readonly int Key;internal readonly WeaponPickup? Pickup;internal readonly string How;
        internal Pick(Kind kind,int key,WeaponPickup? pickup,string how){Kind=kind;Key=key;Pickup=pickup;How=how;}
    }
    internal bool Aims(int side,float now)=>aim[side].Kind!=Kind.None||recent[side].Kind!=Kind.None&&now-recent[side].Seen<=Grace;
    // Picks each hand's weapon and shows the outline of the best one; returns
    // the hands (bit 0 left, bit 1 right) that just came onto a weapon.
    internal int Tick(BodyHolsters? holsters,PoseValue? right,PoseValue? left,bool hints,bool rightPress=false,bool leftPress=false)
    {
        float now=Time.realtimeSinceStartup;int ticks=0;
        near.Clear();
        for(int i=pickups.Count-1;i>=0;i--)
        {
            var p=pickups[i];
            try
            {
                if(p==null||!p.isActiveAndEnabled||p.wasPicked){pickups.RemoveAt(i);continue;}
                var center=Center(p,out var bounds,out bool has);near.Add((p,center,bounds,has));
            }
            catch(Exception){pickups.RemoveAt(i);}
        }
        for(int s=1;s>=0;s--)
        {
            var hand=s==1?right:left;bool press=s==1?rightPress:leftPress;
            var before=aim[s];aim[s]=default;
            if(hand is not PoseValue h){handSeen[s]=false;continue;}
            // 0.1.140: the left hand points the way the right does (its
            // controller's aim, mirrored); the glove's forward was off.
            var o=CameraRig.UnityPosition(h);var d=(s==0?ControllerAim.Mirrored(h):ControllerAim.Rotation(h))*Vector3.forward;
            handAt[s]=o;handDir[s]=d;handSeen[s]=true;
            var best=Best(s,holsters,o,d,before,1);
            bool remembered=recent[s].Kind!=Kind.None&&now-recent[s].Seen<=Grace;
            if(best.Kind==Kind.None&&press&&!remembered){best=Best(s,holsters,o,d,before,PressWiden);best.Wide=best.Kind!=Kind.None;}
            if(best.Kind!=Kind.None)
            {
                if(!before.Same(best)&&!(remembered&&recent[s].Same(best)))ticks|=1<<s;
                best.Seen=now;recent[s]=best;
            }
            aim[s]=best;
        }
        // The outline: the better of the two hands' targets.
        int side=aim[1].Kind!=Kind.None&&(aim[0].Kind==Kind.None||aim[1].Score<=aim[0].Score)?1:aim[0].Kind!=Kind.None?0:-1;
        var shown=side>=0?aim[side]:default;
        Target=shown.Kind;TargetKey=shown.Kind==Kind.Copy?shown.Key:-1;TargetPickup=shown.Pickup;TargetLeft=side==0;
        string name=Target==Kind.Copy?"floor "+shown.Copy?.Profile:Target==Kind.Pickup?shown.Pickup?.name??"":"";
        if(name!=lastTarget){lastTarget=name;if(name.Length>0)Bootstrap.Write("POINT GRAB "+(TargetLeft?"left":"right")+" hand "+(shown.Reach?"is at ":"points at ")+name+(shown.Wide?" (wider look on the press)":""));}
        if(!hints||Target==Kind.None){HideVisuals();return ticks;}
        bool drawn=false;
        if(!outlineFailed)
        {
            try
            {
                Mesh? mesh=null;var m=Matrix4x4.identity;
                if(shown.Copy!=null)shown.Copy.OutlineSource(out mesh,out m);else if(shown.Pickup!=null)PickupShape(shown.Pickup,out mesh,out m);
                if(mesh!=null){outline??=new ReloadOutline();drawn=outline.Show(mesh,m);}
            }
            catch(Exception ex){outlineFailed=true;outline?.Dispose();outline=null;Bootstrap.Warn("POINT GRAB outline unavailable (small glow instead): "+ex.Message);}
        }
        if(drawn){glow?.Hide();return ticks;}
        outline?.Hide();
        try{glow??=new ReloadGlow();glow.Show(shown.At,.04f);}catch(Exception){glow=null;}
        return ticks;
    }
    // The best weapon for one hand: one it is at wins, else the one nearest its pointing line.
    private Aim Best(int side,BodyHolsters? holsters,Vector3 o,Vector3 d,in Aim before,float widen)
    {
        var best=new Aim{Score=float.PositiveInfinity};
        // The hand's last target counts as nearer the pointing line (Sticky).
        // 0.1.179: only a weapon with no wall or door between (Visible).
        if(holsters!=null)foreach(var (key,c) in holsters.FloorCopies())
        {
            var center=c.CenterWorld;float reach=c.Distance(o);float s;bool at=reach<ReachMiddle;
            if(at)s=-2+reach;
            else{s=PointingMath.Score(ContactWorld.V(o),ContactWorld.V(d),ContactWorld.V(center),PointingMath.MinDistance,PointingMath.MaxDistance,widen);if(before.Kind==Kind.Copy&&before.Key==key)s/=PointingMath.Sticky;}
            if(s<best.Score&&Visible(side,-1-key,"floor "+c.Profile,c.RootTransform,center,center+Vector3.up*.06f,o,at))best=new Aim{Kind=Kind.Copy,Key=key,Copy=c,At=center,Score=s,Reach=at};
        }
        foreach(var (p,center,bounds,has) in near)
        {
            try
            {
                float middle=(center-o).magnitude;bool at=middle<=ReachMiddle||has&&bounds.SqrDistance(o)<=ReachBounds*ReachBounds;float s;
                if(at)s=-2+Math.Min(middle,has?Mathf.Sqrt(bounds.SqrDistance(o)):middle);
                else{s=PointingMath.Score(ContactWorld.V(o),ContactWorld.V(d),ContactWorld.V(center),.10f,PointingMath.MaxDistance,widen);if(before.Kind==Kind.Pickup&&before.Id==p.Pointer)s/=PointingMath.Sticky;}
                var top=has?new Vector3(center.x,bounds.max.y+.03f,center.z):center+Vector3.up*.08f;
                if(s<best.Score&&Visible(side,p.Pointer.ToInt64(),p.name,p.transform,center,top,o,at))best=new Aim{Kind=Kind.Pickup,Pickup=p,Id=p.Pointer,Key=-1,At=center,Score=s,Reach=at};
            }
            catch(Exception){}
        }
        if(float.IsPositiveInfinity(best.Score))best=default;
        return best;
    }
    // 0.1.179: nothing
    // solid between (PointingMath.Seen/Blocks); the answer kept 0.15 s.
    // solid: the layers the player's shots stop at (the game's own mask), else
    // all; a door blocks whatever its layer.
    private Vector3 head;private Transform? player;private int solid=~0;
    private readonly Il2CppStructArray<RaycastHit> sightHits=new(32);
    private readonly Dictionary<(int side,bool reach,long id),(float until,bool seen)> sight=new();
    private readonly Dictionary<IntPtr,int> people=new();
    private readonly Dictionary<long,float> blockedReported=new();
    private string blocker="";
    internal const float SightKeep=.15f;
    private bool Visible(int side,long id,string name,Transform? own,Vector3 center,Vector3 top,Vector3 hand,bool reach)
    {
        float now=Time.realtimeSinceStartup;
        if(sight.TryGetValue((side,reach,id),out var kept)&&now<kept.until)return kept.seen;
        if(sight.Count>64)sight.Clear();
        blocker="";
        bool headSees=Clear(head,center,own)||Clear(head,top,own);
        string wall=blocker;
        bool seen=headSees;
        if(reach)
        {
            bool handSees=Clear(hand,center,own)||Clear(hand,top,own);if(wall.Length==0)wall=blocker;
            bool headHand=headSees||Clear(head,hand,null);if(wall.Length==0)wall=blocker;
            seen=PointingMath.Seen(true,headSees,headHand,handSees);
        }
        sight[(side,reach,id)]=(now+SightKeep,seen);
        if(!seen&&(!blockedReported.TryGetValue(id,out float next)||now>=next))
        {
            if(blockedReported.Count>64)blockedReported.Clear();
            blockedReported[id]=now+5;
            Bootstrap.Write("POINT GRAB "+(side==1?"right":"left")+" hand: "+name+" is behind "+(wall.Length>0?wall:"a wall")+(reach?" (the hand is at it)":"")+": not taken through it");
        }
        return seen;
    }
    private bool Clear(Vector3 from,Vector3 to,Transform? own)
    {
        var d=to-from;float length=d.magnitude;if(!(length>.02f))return true;
        int n=Math.Min(Physics.RaycastNonAlloc(from,d/length,sightHits,length,~0,QueryTriggerInteraction.Ignore),sightHits.Length);
        Transform? heldRight=null,heldLeft=null,carried=null;bool heldRead=false;
        for(int i=0;i<n;i++)
        {
            var h=sightHits[i];var c=h.collider;if(c==null||!c.enabled)continue;
            var t=c.transform;var body=c.attachedRigidbody;
            if(!PointingMath.Blocks(h.distance,length,own!=null&&t.IsChildOf(own),player!=null&&t.IsChildOf(player),body!=null&&!body.isKinematic,false))continue;
            if(!heldRead){heldRead=true;try{heldRight=InteractionDriver.Current?.HeldRoot(true);heldLeft=InteractionDriver.Current?.HeldRoot(false);carried=GripCarry.Current?.BodyRoot;}catch(Exception){}}
            if(heldRight!=null&&t.IsChildOf(heldRight)||heldLeft!=null&&t.IsChildOf(heldLeft)||carried!=null&&t.IsChildOf(carried))continue;
            // A person never blocks; a door always does; the rest when the player's shots stop there.
            int kind=Class(c);if(kind==Person||kind==Other&&(solid&(1<<c.gameObject.layer))==0)continue;
            blocker=c.name;return false;
        }
        return true;
    }
    private const int Person=0,Door=1,Other=2;
    private int Class(Collider c)
    {
        var key=c.Pointer;if(people.TryGetValue(key,out int known))return known;
        if(people.Count>512)people.Clear();
        int kind=Other;
        try
        {
            if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null)kind=Person;
            else{int up=0;for(var t=c.transform;t!=null&&up<6;t=t.parent,up++)if(t.name.IndexOf("door",StringComparison.OrdinalIgnoreCase)>=0){kind=Door;break;}}
        }
        catch(Exception){}
        people[key]=kind;return kind;
    }
    // What hand s takes with its press now (its target, or the one it pointed at a moment ago, still lying there).
    internal bool Takeable(int s,BodyHolsters? holsters,out Pick pick)
    {
        pick=default;float now=Time.realtimeSinceStartup;
        var a=aim[s];string how=a.Reach?"reached":a.Wide?"pointed (wider look)":"pointed";
        if(a.Kind==Kind.None){a=recent[s];how="pointed a moment before the press";if(a.Kind==Kind.None||now-a.Seen>Grace)return false;}
        if(a.Kind==Kind.Copy){var c=holsters?.CopyOf(a.Key);if(c==null||!c.OnFloor)return false;}
        else{try{if(a.Pickup==null||!a.Pickup.isActiveAndEnabled||a.Pickup.wasPicked)return false;}catch(Exception){return false;}}
        pick=new Pick(a.Kind,a.Key,a.Pickup,how);return true;
    }
    // A press that took nothing: the nearest weapon lying there and how far off the aim it was.
    internal string Miss(int s)
    {
        if(!handSeen[s])return "";
        var o=handAt[s];var d=handDir[s].normalized;string best="";float bestAngle=float.MaxValue;
        foreach(var (p,center,_,_) in near)
        {
            try
            {
                var v=center-o;float dist=v.magnitude;if(dist>4||dist<1e-3f)continue;
                float angle=Vector3.Angle(d,v);if(angle>=bestAngle)continue;
                bestAngle=angle;best=p.name+" "+dist.ToString("F2")+" m away, "+angle.ToString("F0")+" degrees off the aim";
            }
            catch(Exception){}
        }
        return best;
    }
    // The middle of the pickup's largest shown part, and that part's box.
    private static Vector3 Center(WeaponPickup p,out Bounds box,out bool has)
    {
        Vector3 c=p.transform.position;float size=-1;box=default;has=false;
        foreach(var r in p.GetComponentsInChildren(Il2CppType.Of<Renderer>(),false))
        {var x=r.TryCast<Renderer>();if(x==null||!x.enabled)continue;var b=x.bounds;float s=b.size.sqrMagnitude;if(s>size){size=s;c=b.center;box=b;has=true;}}
        return c;
    }
    // The pickup's largest readable mesh (a skinned one baked once).
    // 0.1.165: baked once per kind of weapon (its game mesh), not per pickup:
    // the outline built from it (ReloadOutline, cached per mesh) took 30-50 ms,
    // and each new pickup pointed at built it again.
    private readonly Dictionary<int,Mesh> bakedByKind=new();
    private void PickupShape(WeaponPickup p,out Mesh? mesh,out Matrix4x4 toWorld)
    {
        mesh=null;toWorld=Matrix4x4.identity;
        if(!shapes.TryGetValue(p.Pointer,out var shape))
        {
            shape=(null,null,false);int best=0;
            foreach(var c in p.GetComponentsInChildren(Il2CppType.Of<Renderer>(),false))
            {
                var r=c.TryCast<Renderer>();if(r==null||!r.enabled)continue;
                var skin=r.TryCast<SkinnedMeshRenderer>();
                if(skin!=null&&skin.sharedMesh!=null&&skin.sharedMesh.vertexCount>best)
                {
                    int kind=skin.sharedMesh.GetInstanceID();
                    if(!bakedByKind.TryGetValue(kind,out var baked)||baked==null){baked=new Mesh(){hideFlags=HideFlags.DontUnloadUnusedAsset};skin.BakeMesh(baked);bakedByKind[kind]=baked;}
                    shape=(baked,skin.transform,true);best=skin.sharedMesh.vertexCount;continue;
                }
                var filter=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>();var m=filter?.sharedMesh;
                if(m!=null&&m.isReadable&&m.vertexCount>best){shape=(m,r.transform,false);best=m.vertexCount;}
            }
            shapes[p.Pointer]=shape;
            if(shapes.Count>256)shapes.Clear();
        }
        if(shape.mesh==null||shape.frame==null)return;
        mesh=shape.mesh;
        // A baked skin is in the renderer's frame without its scale.
        toWorld=shape.baked?Matrix4x4.TRS(shape.frame.position,shape.frame.rotation,Vector3.one):shape.frame.localToWorldMatrix;
    }
    private void HideVisuals(){outline?.Hide();glow?.Hide();}
    internal void Hide(){HideVisuals();Target=Kind.None;TargetPickup=null;TargetKey=-1;aim[0]=aim[1]=default;}
    // After a take: nothing is remembered for the press grace either.
    internal void Clear(){Hide();recent[0]=recent[1]=default;}
    internal void Forget(WeaponPickup p){pickups.Remove(p);}
    public void Dispose()
    {
        outline?.Dispose();outline=null;glow?.Dispose();glow=null;
        foreach(var m in bakedByKind.Values)if(m!=null)UnityEngine.Object.Destroy(m);bakedByKind.Clear();shapes.Clear();pickups.Clear();sight.Clear();people.Clear();
    }
}
