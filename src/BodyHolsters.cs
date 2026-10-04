using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.124: the player's weapons on the body. Every owned weapon that is not
// in the hand hangs on its body place (HolsterAssignment) as a still copy
// (HolsterCopy): taken from the hand when it is put away, or built once from
// the game's own weapon for weapons not yet held. A weapon let go away from
// the body lies on the floor (still in the wheel) until taken again.
internal sealed class BodyHolsters:IDisposable
{
    private sealed class Entry
    {
        internal IntPtr Pointer;internal Equipable? Weapon;internal string Profile="";internal HolsterCopy? Copy;
        // InHand: the game's weapon (in a hand); Held: a still copy in a hand.
        internal int Attempts;internal float NextBuild;internal bool InHand,Held;
        // 0.1.129: grenades/knives: how many are left; a second copy on the
        // body place while the copy itself is in the left hand.
        internal AmmoManagementComponent? Ammo;internal bool AmmoSearched;internal HolsterCopy? Twin;
        // 0.1.130: another weapon of an owned kind (taken from the ground):
        // Owner is the owned weapon's slot (it fires through that weapon, with
        // its own magazine). Shared: on the body place of a weapon of the same
        // kind (drawn when that one is not there). ShownAt: where it is on the
        // body now (None: in a hand, on the floor or behind another).
        internal int Owner=-1;internal int Magazine;internal HolsterSlot Shared=HolsterSlot.None,ShownAt=HolsterSlot.None;
        internal void DropCopies(){Twin?.Dispose();Twin=null;Copy?.Dispose();Copy=null;}
    }
    internal const int LooseBase=1000;
    internal static bool IsLoose(int key)=>key>=LooseBase;
    private int nextLoose;
    private readonly HashSet<HolsterSlot> activeSlots=new();
    private int Group(int key,Entry e)=>e.Owner>=0?e.Owner:key;
    // 0.1.129: kinds that come several at a time (one taken, the rest stay on the body place).
    internal static bool Stacked(string profile)=>profile is "grenade" or "knife";
    // How many of a stacked kind are left (-1: unknown); 1 for the others.
    private static int Left(Entry e)
    {
        if(!Stacked(e.Profile))return 1;
        var w=e.Weapon;if(w==null)return -1;
        try
        {
            if(!e.AmmoSearched)
            {
                e.AmmoSearched=true;
                e.Ammo=w.primaryFireUsageComponentScript?.TryCast<FireComponent>()?.ammoManagementComponent
                    ??w.GetComponentInChildren(Il2CppInterop.Runtime.Il2CppType.Of<AmmoManagementComponent>(),true)?.TryCast<AmmoManagementComponent>();
            }
            var a=e.Ammo;if(a==null)return -1;
            return Math.Max(0,a.PrimaryMagazineAmmoCount)+Math.Max(0,a.PrimaryReserveAmmoCount);
        }
        catch(Exception){return -1;}
    }
    internal int CountOf(int key)=>entries.TryGetValue(key,out var e)?Left(e):-1;
    private readonly Dictionary<int,Entry> entries=new();
    private readonly HolsterAssignment places=new();
    private readonly ContactWorld world=new();
    private ReloadGlow? glow,leftGlow;
    private float nextSync,nextBuild;
    private Quaternion torsoYaw=Quaternion.identity;private Vector3 head;
    private bool leftHanded;
    internal int Count=>entries.Count;
    // Torso frame from the head: the head's heading (not while looking
    // straight down), yaw only.
    // 0.1.234: the heading shared with the weapon places editor (HolsterPlaces.Torso).
    private void Torso(Vector3 headPosition,Quaternion headRotation)
    {
        var f=headRotation*Vector3.forward;
        torsoYaw=Quaternion.Euler(0,HolsterPlaces.Torso(new N(f.x,f.y,f.z)),0);
        head=headPosition;
    }
    private Vector3 World(N local)=>head+torsoYaw*new Vector3(local.X,local.Y,local.Z);
    private HolsterPose PoseOf(HolsterSlot slot)=>HolsterLayout.Pose(slot,leftHanded);
    private Vector3 Reach(HolsterSlot slot)=>World(PoseOf(slot).Reach);
    // The owned weapons (every second): new ones get a place in the order
    // they are seen; weapons gone from the inventory are forgotten.
    internal void Sync(PlayerEquipableInventory inventory,int inHand,int heldLeft=-1,int heldRight=-1)
    {
        foreach(var pair in entries)
        {
            var e=pair.Value;e.Held=pair.Key==heldLeft||pair.Key==heldRight;bool now0=pair.Key==inHand&&!e.Held;
            // Came to the hand some other way (the wheel): off the floor, back
            // to a body place for later.
            if(now0&&!e.InHand){e.Copy?.Hide();if(e.Copy?.OnFloor==true){e.Copy.Lift();places.Assign(pair.Key,e.Profile);}}
            e.InHand=now0;
        }
        float now=Time.realtimeSinceStartup;
        // 0.1.185: a weapon just picked up is looked for every frame.
        bool rushing=rushKey>=0&&now<rushUntil&&!entries.ContainsKey(rushKey);
        if(now<nextSync&&!rushing)return;nextSync=now+1;
        var seen=new HashSet<int>();
        var list=inventory.GetCurrentEquipablesInInventory();
        if(list!=null)for(int i=0;i<list.Count;i++)
        {
            var w=list[i];if(w==null)continue;int key=(int)w.slot;string profile=EquipmentProfile.ForSlot(key);
            if(!HolsterLayout.Kind(profile))continue;
            seen.Add(key);
            if(!entries.TryGetValue(key,out var e)){e=new Entry{Profile=profile,InHand=key==inHand};entries[key]=e;var s=places.Assign(key,profile);Bootstrap.Write("HOLSTER owned "+profile+" place="+s);}
            if(e.Pointer!=w.Pointer){e.Pointer=w.Pointer;e.Weapon=w;e.DropCopies();e.Attempts=0;e.Ammo=null;e.AmmoSearched=false;}
        }
        Replace();
        foreach(var key in new List<int>(entries.Keys))
        {
            var e=entries[key];
            // 0.1.130: extra weapons stay while their kind is owned.
            if(e.Owner>=0){if(seen.Contains(e.Owner)&&entries.TryGetValue(e.Owner,out var o)){e.Weapon=o.Weapon;continue;}}
            else if(seen.Contains(key))continue;
            e.DropCopies();entries.Remove(key);places.Forget(key);
        }
    }
    // 0.1.146: a place switched off in VR SETTINGS: its weapon moves to a
    // place still on (none free: out of sight, still in the wheel); a weapon
    // kept out of sight gets a place again when one is switched back on.
    private void Replace()
    {
        foreach(var pair in entries)
        {
            var e=pair.Value;int key=pair.Key;
            if(e.Owner>=0||e.InHand||e.Held||e.Copy?.OnFloor==true)continue;
            var slot=places.SlotOf(key);
            if(slot!=HolsterSlot.None&&Allowed(e.Profile,slot))continue;
            if(slot!=HolsterSlot.None)places.Release(key);
            var to=places.Assign(key,e.Profile);
            if(slot!=HolsterSlot.None||to!=HolsterSlot.None)Bootstrap.Write("HOLSTER "+e.Profile+(slot!=HolsterSlot.None?" leaves "+slot+" (switched off)":"")+" -> "+(to==HolsterSlot.None?"out of sight (no place on; still in the wheel)":to.ToString()));
        }
    }
    private static bool Allowed(string profile,HolsterSlot slot){foreach(var c in HolsterLayout.Candidates(profile))if(c==slot)return true;return false;}
    // 0.1.130: another weapon of an owned kind, taken from the ground: a copy of
    // the owned one's still copy, with its own magazine. -1 if none can be made.
    internal int AddLoose(int owner,int magazine)
    {
        if(!entries.TryGetValue(owner,out var o)||o.Copy==null||o.Owner>=0)return -1;
        var clone=o.Copy.Clone();if(clone==null)return -1;
        int key=LooseBase+nextLoose++;
        entries[key]=new Entry{Weapon=o.Weapon,Profile=o.Profile,Copy=clone,Owner=owner,Magazine=magazine,Pointer=o.Pointer};
        Bootstrap.Write("HOLSTER another "+o.Profile+" (key "+key+", magazine "+magazine+"); the owned one stays where it is");
        return key;
    }
    internal int OwnerOf(int key)=>entries.TryGetValue(key,out var e)?e.Owner:-1;
    // 0.1.134: another weapon of an owned kind comes into play: the owned
    // weapon takes its place (in the hand) and it takes the owned one's (on
    // the body, on the floor, shared); the two look the same. Magazines are
    // traded by the caller. Not while the owned one is in a hand.
    internal bool SwapWithOwner(int loose)
    {
        if(!entries.TryGetValue(loose,out var l)||l.Owner<0||!entries.TryGetValue(l.Owner,out var o)||o.InHand||o.Held||l.Copy==null||o.Copy==null)return false;
        places.Swap(loose,l.Owner);
        (l.Copy,o.Copy)=(o.Copy,l.Copy);(l.Twin,o.Twin)=(o.Twin,l.Twin);
        (l.InHand,o.InHand)=(o.InHand,l.InHand);(l.Held,o.Held)=(o.Held,l.Held);
        (l.Shared,o.Shared)=(o.Shared,l.Shared);(l.ShownAt,o.ShownAt)=(o.ShownAt,l.ShownAt);
        return true;
    }
    internal string ProfileOf(int key)=>entries.TryGetValue(key,out var e)?e.Profile:EquipmentProfile.ForSlot(key);
    internal int LooseMagazine(int key)=>entries.TryGetValue(key,out var e)?e.Magazine:0;
    internal void SetLooseMagazine(int key,int value){if(entries.TryGetValue(key,out var e))e.Magazine=Math.Max(0,value);}
    internal int LooseCount(int owner){int n=0;foreach(var e in entries.Values)if(e.Owner==owner)n++;return n;}
    // A still copy for a weapon never held yet (one build every 1.5 s).
    // 0.1.185: a weapon
    // just picked up is built at once, the frame the game has set it up (it
    // waited for the next inventory look, up to 1 s, then for the next build,
    // up to 1.5 s, and a weapon the game had not set up yet waited 5 s more);
    // while the player cannot play (the mission's opening, a cutscene, a menu)
    // the weapons owned are built every 0.3 s; and a kind built once is kept
    // for the whole game: any later one of that kind is copied from it at once.
    private int rushKey=-1;private float rushUntil;
    internal void Rush(int key){rushKey=key;rushUntil=Time.realtimeSinceStartup+3;nextSync=0;}
    private static readonly Dictionary<string,HolsterCopy> kept=new();
    internal static int KeptKinds=>kept.Count;
    internal void BuildMissing(bool quick=false)
    {
        float now=Time.realtimeSinceStartup;
        if(rushKey>=0)
        {
            if(now>=rushUntil)rushKey=-1;
            else if(entries.TryGetValue(rushKey,out var r))
            {
                if(r.Copy!=null||r.Owner>=0||r.InHand||r.Weapon==null){rushKey=-1;}
                else
                {
                    if(FromKept(r)||Ready(r)&&Build(r)||r.Attempts>=3)rushKey=-1;
                    return;
                }
            }
        }
        if(now<nextBuild)return;
        foreach(var e in entries.Values)
        {
            if(e.Copy!=null||e.Owner>=0||e.InHand||e.Held||e.Weapon==null||e.Attempts>=3||now<e.NextBuild)continue;
            // 0.1.128: also for weapons out of sight (a hand may take them as a copy).
            if(FromKept(e)){nextBuild=now+.1f;return;}
            nextBuild=now+(quick?.3f:1.5f);
            // Not set up by the game yet: looked at again soon, not counted as a try.
            if(!Ready(e)){e.NextBuild=now+.5f;return;}
            e.NextBuild=now+5;Build(e);
            return;
        }
    }
    // 0.1.185: a weapon taken from its body place before its copy was built: built now (shown in the hand at once).
    internal bool BuildNow(int key)
    {
        if(!entries.TryGetValue(key,out var e)||e.Copy!=null)return e?.Copy!=null;
        if(e.Owner>=0||e.Weapon==null||e.Attempts>=3)return false;
        return FromKept(e)||Ready(e)&&Build(e);
    }
    private static bool Ready(Entry e){try{return e.Weapon!=null&&e.Weapon.isEquipableSetUp;}catch(Exception){return false;}}
    private bool Build(Entry e)
    {
        e.Attempts++;
        WeaponVisual? visual=null;
        try
        {
            var muzzle=WeaponHands.FindMuzzle(e.Weapon!,e.Profile);if(muzzle==null)throw new InvalidOperationException("no muzzle");
            visual=WeaponVisual.Create(e.Weapon!,muzzle,e.Profile);
            e.Copy=HolsterCopy.From(visual,Vector3.zero,HolsterLayout.CenterPlaced(e.Profile),WeaponHands.HeldPoseOf(e.Profile));
            Bootstrap.Write("HOLSTER copy built for "+e.Profile);
            Keep(e);
            return true;
        }
        catch(Exception ex){Bootstrap.Warn("HOLSTER copy of "+e.Profile+" not built (attempt "+e.Attempts+"): "+ex.Message);return false;}
        finally{visual?.Dispose();}
    }
    // Firearms only (a knife's or a grenade's copy carries its pin and its stack).
    private static void Keep(Entry e)
    {
        if(e.Copy==null||!HolsterLayout.Firearm(e.Profile)||kept.ContainsKey(e.Profile))return;
        try{var c=e.Copy.Clone();if(c!=null){kept[e.Profile]=c;Bootstrap.Write("HOLSTER "+e.Profile+" kept for the whole game (the next one of its kind is ready at once)");}}
        catch(Exception ex){Bootstrap.Warn("HOLSTER "+e.Profile+" not kept: "+ex.Message);}
    }
    private static bool FromKept(Entry e)
    {
        if(!kept.TryGetValue(e.Profile,out var k))return false;
        try
        {
            if(!k.Intact){k.Dispose();kept.Remove(e.Profile);Bootstrap.Write("HOLSTER kept "+e.Profile+" lost its meshes (a level unloaded them): built again");return false;}
            var c=k.Clone();if(c==null)return false;
            e.Copy=c;Bootstrap.Write("HOLSTER copy for "+e.Profile+" from the kept one (at once)");return true;
        }
        catch(Exception ex){kept.Remove(e.Profile);Bootstrap.Warn("HOLSTER kept "+e.Profile+": "+ex.Message);return false;}
    }
    // The copy taken from the hand as it was put away.
    internal void Store(int key,HolsterCopy copy)
    {
        if(!entries.TryGetValue(key,out var e)){copy.Dispose();return;}
        e.DropCopies();e.Copy=copy;
    }
    internal HolsterSlot PlaceOf(int key)=>places.SlotOf(key);
    // The free place of this weapon's kind nearest to the hand, within reach
    // (0.1.142: each place its own reach, further over the shoulders). No
    // longer the place of another weapon of the same kind (0.1.130 shared it:
    // the player took an M16 from his shoulder and another one was still
    // there); an owned weapon may take the place of another one of a kind
    // taken from the ground (that one drops).
    internal HolsterSlot NearestPlace(int key,string profile,Vector3 hand)=>NearestWithin(key,profile,hand,false);
    private HolsterSlot NearestWithin(int key,string profile,Vector3 hand,bool hint)
    {
        bool owned=!entries.TryGetValue(key,out var me)||me.Owner<0;
        var best=HolsterSlot.None;float bestD=float.PositiveInfinity;
        foreach(var c in HolsterLayout.Candidates(profile))
        {
            int holder=places.Holder(c,key);
            if(holder>=0&&!(owned&&(IsExtra(holder)||MoveTo(holder,c)!=HolsterSlot.None)))continue;
            float d=Vector3.Distance(hand,Reach(c));
            if(!float.IsFinite(d)||d>(hint?HolsterLayout.HintRadiusOf(c):HolsterLayout.SnapRadiusOf(c))||d>=bestD)continue;
            bestD=d;best=c;
        }
        return best;
    }
    private bool IsExtra(int key)=>entries.TryGetValue(key,out var e)&&e.Owner>=0;
    // 0.1.142: a weapon hung on the place of another owned one moves
    // that one to a free place of its kind (none free: this place is not
    // offered).
    private HolsterSlot MoveTo(int holder,HolsterSlot from)
    {
        if(!entries.TryGetValue(holder,out var h)||h.Owner>=0)return HolsterSlot.None;
        foreach(var c in HolsterLayout.Candidates(h.Profile))if(c!=from&&!places.Occupied(c,holder))return c;
        return HolsterSlot.None;
    }
    // Another weapon of a kind on a place an owned weapon takes: it drops.
    private void Bump(int key)
    {
        if(!entries.TryGetValue(key,out var e))return;
        places.Release(key);e.Shared=HolsterSlot.None;
        if(e.Copy!=null&&!e.InHand&&!e.Held){e.Copy.Drop(e.Copy.Position,e.Copy.Rotation,Vector3.zero);Bootstrap.Write("HOLSTER another "+e.Profile+" makes room on the body: it drops");}
    }
    private void Settle(int key,Entry e,HolsterSlot slot)
    {
        e.Shared=HolsterSlot.None;
        if(slot!=HolsterSlot.None)
        {
            int holder=places.Holder(slot,key);
            if(holder>=0&&e.Owner<0&&IsExtra(holder))Bump(holder);
            else if(holder>=0&&e.Owner<0&&MoveTo(holder,slot) is var to&&to!=HolsterSlot.None&&places.Place(holder,ProfileOf(holder),to))
                Bootstrap.Write("HOLSTER "+ProfileOf(holder)+" moves from "+slot+" to "+to+" ("+e.Profile+" hangs there)");
            if(places.Place(key,e.Profile,slot))return;
        }
        if(places.SlotOf(key)!=HolsterSlot.None&&slot==HolsterSlot.None)return;
        if(places.Assign(key,e.Profile)!=HolsterSlot.None)return;
        // An owned weapon with every place of its kind taken: one taken by
        // another weapon of a kind is given up for it.
        if(e.Owner<0)foreach(var c in HolsterLayout.Candidates(e.Profile))
        {
            int holder=places.Holder(c,key);
            if(holder>=0&&IsExtra(holder)){Bump(holder);if(places.Place(key,e.Profile,c))return;}
        }
        places.Release(key);
    }
    // 0.1.142: another weapon of a kind is not kept out of sight on the body:
    // with no free place it lies on the floor; left far behind it is gone.
    private void Discard(int key)
    {
        if(!entries.TryGetValue(key,out var e))return;
        e.DropCopies();entries.Remove(key);places.Forget(key);
    }
    // At most this many other weapons of one kind (a third one taken from the
    // ground replaces one lying on the floor).
    internal const int MaxLoose=2;
    // 0.1.200: one on the body gives way too (its rounds back to the reserve: freedRounds).
    internal bool MakeRoomForLoose(int owner,out int freedRounds)
    {
        freedRounds=0;
        var loose=new List<(int key,bool floor,bool hand)>();
        foreach(var pair in entries)if(pair.Value.Owner==owner)loose.Add((pair.Key,pair.Value.Copy?.OnFloor==true,pair.Value.InHand||pair.Value.Held));
        int free=HolsterLayout.LooseToFree(loose,MaxLoose);
        if(free==-1)return true;
        if(free<0||!entries.TryGetValue(free,out var e))return false;
        bool floor=e.Copy?.OnFloor==true;var p=e.Profile;
        if(!floor)freedRounds=Math.Max(0,e.Magazine);
        Discard(free);
        Bootstrap.Write(floor?"HOLSTER another "+p+" on the floor is let go (at most "+MaxLoose+" others of a kind)"
            :"HOLSTER another "+p+" on the body gives way to the one taken (at most "+MaxLoose+" others of a kind); its "+freedRounds+" rounds go back to the reserve");
        return true;
    }
    internal void Hang(int key,string profile,HolsterSlot slot)
    {
        if(!entries.TryGetValue(key,out var e)){places.Place(key,profile,slot);return;}
        Settle(key,e,slot);
        e.InHand=false;e.Held=false;e.Copy?.Lift();if(e.Copy!=null)e.Copy.PinPulled=false;
    }
    // Back to its own place (or the first free one) without hanging by hand.
    internal HolsterSlot Return(int key,string profile)
    {
        if(!entries.TryGetValue(key,out var e))return places.Assign(key,profile);
        if(places.SlotOf(key)==HolsterSlot.None)Settle(key,e,HolsterSlot.None);
        e.InHand=false;e.Held=false;e.Copy?.Lift();if(e.Copy!=null)e.Copy.PinPulled=false;
        var s=places.SlotOf(key);
        if(s==HolsterSlot.None&&e.Owner>=0&&e.Copy!=null)
        {e.Copy.Drop(e.Copy.Position,e.Copy.Rotation,Vector3.zero);Bootstrap.Write("HOLSTER another "+e.Profile+": no free place on the body, it drops");}
        return s;
    }
    // 0.1.135: with the hand's turn and where the hand was (a spin throw).
    internal bool Drop(int key,Vector3 position,Quaternion rotation,Vector3 velocity,Vector3 spin=default,Vector3? hand=null)
    {
        if(!entries.TryGetValue(key,out var e)||e.Copy==null)return false;
        places.Release(key);e.Shared=HolsterSlot.None;e.InHand=false;e.Held=false;
        if(hand is Vector3 h)e.Copy.Drop(position,rotation,velocity,spin,h);else e.Copy.Drop(position,rotation,velocity);
        return true;
    }
    internal void Taken(int key,string profile)
    {
        if(!entries.TryGetValue(key,out var e))return;
        e.InHand=true;e.Held=false;e.Copy?.Hide();
        if(e.Copy?.OnFloor==true){e.Copy.Lift();Settle(key,e,HolsterSlot.None);}
    }
    // A weapon the empty right hand can take here: on its body place (drawn
    // or not) or lying on the floor.
    // 0.1.169: the nearest place with a weapon on it and how far the hand is (a grip press that took nothing).
    internal bool NearestShown(Vector3 hand,out HolsterSlot slot,out float distance)
    {
        slot=HolsterSlot.None;distance=float.PositiveInfinity;
        foreach(var e in entries.Values)
        {
            if(e.InHand||e.Held||Left(e)==0||e.Copy!=null&&e.Copy.OnFloor)continue;
            var s=e.ShownAt;if(s==HolsterSlot.None)continue;
            float d=Vector3.Distance(hand,Reach(s));if(d<distance){distance=d;slot=s;}
        }
        return slot!=HolsterSlot.None;
    }
    internal bool TryGrab(Vector3 hand,out int key,out bool floor)
    {
        key=-1;floor=false;float best=float.PositiveInfinity;
        foreach(var pair in entries)
        {
            var e=pair.Value;if(e.InHand||e.Held||Left(e)==0)continue;
            if(e.Copy!=null&&e.Copy.OnFloor)
            {
                float d=e.Copy.Distance(hand);
                if(d<HolsterLayout.FloorGrabRadius&&d<best){best=d;key=pair.Key;floor=true;}
                continue;
            }
            // Of several weapons on one place, the one there now.
            var slot=e.ShownAt;if(slot==HolsterSlot.None)continue;
            float r=Vector3.Distance(hand,Reach(slot));
            if(r<HolsterLayout.GrabRadiusOf(slot)&&r<best){best=r;key=pair.Key;floor=false;}
        }
        return key>=0;
    }
    // Shown while a weapon is in the hand: where it can be hung.
    internal bool Hint(int key,string profile,Vector3 hand,out HolsterSlot slot,bool left=false)
    {
        slot=NearestWithin(key,profile,hand,true);
        if(slot==HolsterSlot.None){HideHint(left);return false;}
        float d=Vector3.Distance(hand,Reach(slot));bool snap=d<=HolsterLayout.SnapRadiusOf(slot);
        try
        {
            if(left){leftGlow??=new ReloadGlow();leftGlow.Show(Reach(slot),snap?.045f:.03f);}
            else{glow??=new ReloadGlow();glow.Show(Reach(slot),snap?.045f:.03f);}
        }
        catch(Exception ex){if(left)leftGlow=null;else glow=null;Bootstrap.Warn("HOLSTER hint unavailable: "+ex.Message);}
        return snap;
    }
    internal void HideHint(bool left=false){if(left)leftGlow?.Hide();else glow?.Hide();}
    // 0.1.126: a hand holds a body copy (0.1.128: either hand).
    internal bool Has(int key)=>entries.ContainsKey(key);
    internal HolsterCopy? CopyOf(int key)=>entries.TryGetValue(key,out var e)?e.Copy:null;
    internal Equipable? WeaponOf(int key)=>entries.TryGetValue(key,out var e)?e.Weapon:null;
    internal HolsterCopy? TakeHand(int key)
    {
        if(!entries.TryGetValue(key,out var e)||e.Copy==null)return null;
        if(e.Copy.OnFloor){e.Copy.Lift();Settle(key,e,HolsterSlot.None);}
        e.Copy.PinPulled=false;e.Held=true;e.InHand=false;return e.Copy;
    }
    // 0.1.128: the game's weapon became a still copy in the hand holding it.
    internal void Hold(int key){if(entries.TryGetValue(key,out var e)){e.Held=true;e.InHand=false;}}
    // Every frame (and once more before drawing): the copies on the body,
    // the fall of dropped ones.
    internal void Render(Vector3 headPosition,Quaternion headRotation,bool left,Transform? player)
    {
        leftHanded=left;Torso(headPosition,headRotation);
        world.Player=player;float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        // 0.1.130: which places have a weapon on them now (own place first, then
        // shared ones: one weapon drawn per place).
        activeSlots.Clear();
        foreach(var pair in entries)
        {
            var e=pair.Value;e.ShownAt=HolsterSlot.None;
            var own=places.SlotOf(pair.Key);
            if(own==HolsterSlot.None||e.InHand||e.Held||e.Copy!=null&&e.Copy.OnFloor||Left(e)==0)continue;
            e.ShownAt=own;activeSlots.Add(own);
        }
        foreach(var pair in entries)
        {
            var e=pair.Value;
            if(e.Shared==HolsterSlot.None||places.SlotOf(pair.Key)!=HolsterSlot.None||e.InHand||e.Held||e.Copy==null||e.Copy.OnFloor)continue;
            if(!places.Occupied(e.Shared,pair.Key)&&places.Place(pair.Key,e.Profile,e.Shared)){e.ShownAt=e.Shared;e.Shared=HolsterSlot.None;activeSlots.Add(e.ShownAt);continue;}
            if(activeSlots.Contains(e.Shared))continue;
            e.ShownAt=e.Shared;activeSlots.Add(e.Shared);
        }
        foreach(var pair in entries)
        {
            var e=pair.Value;var c=e.Copy;if(c==null)continue;
            if(c.OnFloor)
            {
                c.Fall(world,dt);
                // Far behind: it comes back to its place on the body.
                if(Vector3.Distance(c.Position,head)>40)
                {
                    // 0.1.142: another one of a kind left behind is gone (it came back to the body).
                    if(e.Owner>=0){gone??=new List<int>();gone.Add(pair.Key);continue;}
                    Return(pair.Key,e.Profile);Bootstrap.Write("HOLSTER "+e.Profile+" left far behind: back on the body");
                }
                continue;
            }
            var slot=e.InHand||e.Held?places.SlotOf(pair.Key):e.ShownAt;
            // 0.1.129: grenades/knives: while one is in a hand, the rest stay on
            // the body place (none left: the place is empty).
            int count=Left(e);bool stacked=Stacked(e.Profile);
            var shown=e.Held?(stacked&&count>1?e.Twin??=c.Twin():null):c;
            if(shown!=e.Twin)e.Twin?.Hide();
            if(shown==null)continue;   // the copy is posed in a hand
            if(slot==HolsterSlot.None||!HolsterLayout.Visible(slot)||count==0||e.InHand&&!(stacked&&count>1)){shown.Hide();continue;}
            var pose=PoseOf(slot);var q=HolsterLayout.Rotation(pose);
            shown.PoseGrip(World(pose.Grip),torsoYaw*new Quaternion(q.X,q.Y,q.Z,q.W));
        }
        if(gone!=null){foreach(var key in gone){var p=ProfileOf(key);Discard(key);Bootstrap.Write("HOLSTER another "+p+" left far behind: gone");}gone.Clear();}
    }
    private List<int>? gone;
    internal IEnumerable<(int key,HolsterCopy copy)> FloorCopies()
    {
        foreach(var pair in entries)if(!pair.Value.InHand&&!pair.Value.Held&&pair.Value.Copy!=null&&pair.Value.Copy.OnFloor&&pair.Value.Copy.Shown)yield return (pair.Key,pair.Value.Copy);
    }
    internal void HideAll(){foreach(var e in entries.Values){if(e.Copy!=null&&!e.Copy.OnFloor)e.Copy.Hide();e.Twin?.Hide();}glow?.Hide();leftGlow?.Hide();}
    internal string Describe()
    {
        var parts=new List<string>();
        foreach(var pair in entries)parts.Add(pair.Value.Profile+(pair.Value.Owner>=0?"(extra)":"")+"="+(pair.Value.InHand?"hand":pair.Value.Held?"held copy":pair.Value.Copy?.OnFloor==true?"floor":pair.Value.Shared!=HolsterSlot.None?pair.Value.Shared+"(shared)":places.SlotOf(pair.Key).ToString())+(pair.Value.Copy==null?"(no copy)":""));
        return string.Join(" ",parts);
    }
    public void Dispose()
    {
        foreach(var e in entries.Values)e.DropCopies();entries.Clear();places.Clear();
        glow?.Dispose();glow=null;leftGlow?.Dispose();leftGlow=null;world.Dispose();
    }
}
