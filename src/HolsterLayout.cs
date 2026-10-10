using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.124: where the player's weapons hang on the body while not in the hand.
// Torso frame (from the head: x right, y up, z forward, yaw only), metres
// from the eyes. Names are for a right-hander; for a left-hander the whole
// layout is mirrored (BeltRight is then the left hip, the belly rifle points
// right, and so on).
//  - pistols / revolvers / Uzi: on the belt at the sides (muzzle down), then
//    under the arms (muzzle back);
//  - assault rifles, M60, shotgun, speargun: across the belly above the belt
//    (muzzle to the left); the shotgun (and bazooka) also behind the right
//    shoulder (not drawn);
//  - SVD / crossbow: on the back over the left shoulder;
//  0.1.140: every long gun (crossbow, bazooka, assault rifles,
//  M60, shotgun, speargun, SVD) can hang on the belly and on the back over
//  either shoulder; the one over the right shoulder is drawn now (the mirror
//  of the left one, a little further back so the two cross).
//  - throwing knife: left of the chest; grenade: right of the chest.
internal enum HolsterSlot{None=-1,BeltRight,BeltLeft,ArmpitLeft,ArmpitRight,Belly,LeftShoulder,RightBack,ChestLeft,ChestRight}
// Grip: where the weapon's grip sits; Forward: the muzzle direction; Up: the
// top of the weapon; Reach: where the hand takes it from (the grip itself,
// except over the shoulders).
internal readonly record struct HolsterPose(Vector3 Grip,Vector3 Forward,Vector3 Up,Vector3 Reach);
internal static class HolsterLayout
{
    internal const float GrabRadius=.12f,SnapRadius=.16f,HintRadius=.30f,FloorGrabRadius=.20f;
    // 0.1.142: the places
    // over the shoulders take (and hang) a weapon from further away.
    internal const float ShoulderGrabRadius=.22f,ShoulderSnapRadius=.26f,ShoulderHintRadius=.36f;
    // 0.1.169: the left belt place (the side the reloading hand takes rounds
    // from; mirrored for a left-hander) takes from further away and hangs 3 cm
    // further back. 0.1.256: both belt places hang outside the belt's rows of
    // shells (the pouch is in front now).
    internal const float BeltLeftGrabRadius=.17f,BeltLeftSnapRadius=.20f;
    // 0.1.214: the knife and grenade places on the chest take from further
    // away (the next knife after a throw was hard to find by feel).
    internal const float ChestGrabRadius=.17f,ChestSnapRadius=.20f;
    internal static bool OverShoulder(HolsterSlot slot)=>slot is HolsterSlot.LeftShoulder or HolsterSlot.RightBack;
    internal static bool OnChest(HolsterSlot slot)=>slot is HolsterSlot.ChestLeft or HolsterSlot.ChestRight;
    internal static float GrabRadiusOf(HolsterSlot slot)=>OverShoulder(slot)?ShoulderGrabRadius:slot==HolsterSlot.BeltLeft?BeltLeftGrabRadius:OnChest(slot)?ChestGrabRadius:GrabRadius;
    internal static float SnapRadiusOf(HolsterSlot slot)=>OverShoulder(slot)?ShoulderSnapRadius:slot==HolsterSlot.BeltLeft?BeltLeftSnapRadius:OnChest(slot)?ChestSnapRadius:SnapRadius;
    internal static float HintRadiusOf(HolsterSlot slot)=>OverShoulder(slot)?ShoulderHintRadius:HintRadius;
    private static readonly HolsterSlot[] Small={HolsterSlot.BeltRight,HolsterSlot.BeltLeft,HolsterSlot.ArmpitLeft,HolsterSlot.ArmpitRight};
    private static readonly HolsterSlot[] Rifle={HolsterSlot.Belly,HolsterSlot.RightBack,HolsterSlot.LeftShoulder};
    private static readonly HolsterSlot[] Shotgun={HolsterSlot.Belly,HolsterSlot.RightBack,HolsterSlot.LeftShoulder};
    private static readonly HolsterSlot[] Back={HolsterSlot.RightBack,HolsterSlot.LeftShoulder,HolsterSlot.Belly};
    private static readonly HolsterSlot[] Shoulder={HolsterSlot.LeftShoulder,HolsterSlot.RightBack,HolsterSlot.Belly};
    private static readonly HolsterSlot[] Knife={HolsterSlot.ChestLeft};
    private static readonly HolsterSlot[] Grenade={HolsterSlot.ChestRight};
    // 0.1.146: the pistol places on the
    // belt and under the arms can each be switched off (VR SETTINGS).
    internal static Func<bool> BeltPistols=()=>true,ArmpitPistols=()=>true;
    private static readonly HolsterSlot[] SmallBelt={HolsterSlot.BeltRight,HolsterSlot.BeltLeft},SmallArmpit={HolsterSlot.ArmpitLeft,HolsterSlot.ArmpitRight};
    private static IReadOnlyList<HolsterSlot> SmallPlaces()
    {
        bool belt=BeltPistols(),armpit=ArmpitPistols();
        return belt&&armpit?Small:belt?SmallBelt:armpit?SmallArmpit:Array.Empty<HolsterSlot>();
    }
    // A kind of weapon that has body places at all (even with them switched off).
    internal static bool Kind(string profile)=>All(profile).Count>0;
    private static IReadOnlyList<HolsterSlot> All(string profile)=>profile is "pistol" or "revolver" or "uzi"?Small:Candidates(profile);
    // In order of preference: a newly owned weapon takes the first free one.
    internal static IReadOnlyList<HolsterSlot> Candidates(string profile)=>profile switch
    {
        "pistol" or "revolver" or "uzi"=>SmallPlaces(),
        "ak47" or "m16" or "m60" or "rifle"=>Rifle,
        "shotgun" or "heavy"=>Shotgun,
        "bazooka"=>Back,
        "sniper" or "crossbow"=>Shoulder,
        "knife"=>Knife,
        "grenade"=>Grenade,
        _=>Array.Empty<HolsterSlot>()
    };
    // Weapons the grip takes and puts away (the knife and grenade hang on the
    // chest and are taken from there, but stay in the hand until thrown).
    internal static bool Firearm(string profile)=>profile is "pistol" or "revolver" or "uzi" or "ak47" or "m16" or "m60" or "rifle" or "shotgun" or "heavy" or "bazooka" or "sniper" or "crossbow";
    // Placed on its body place by its middle (not by a grip).
    internal static bool CenterPlaced(string profile)=>profile is "knife" or "grenade";
    internal static bool Visible(HolsterSlot slot)=>slot!=HolsterSlot.None;
    // 0.1.140: the places a long gun can hang on.
    internal static bool LongGunPlace(HolsterSlot slot)=>slot is HolsterSlot.Belly or HolsterSlot.LeftShoulder or HolsterSlot.RightBack;
    // 0.1.234: how far the player moved each place (HolsterPlaces; right-hander frame).
    internal static Func<HolsterSlot,Vector3> Offset=_=>Vector3.Zero;
    internal static HolsterPose Pose(HolsterSlot slot,bool leftHanded)=>Pose(slot,leftHanded,true);
    internal static HolsterPose Pose(HolsterSlot slot,bool leftHanded,bool moved)
    {
        var p=slot switch
        {
            // 0.1.125: the belt pistols 12 cm higher (they hung below the belt).
            // 0.1.256: 5 and 2 cm further out, outside the shells on the hips.
            HolsterSlot.BeltRight=>new HolsterPose(new(.25f,-.50f,0),-Vector3.UnitY,Vector3.UnitZ,new(.25f,-.45f,0)),
            HolsterSlot.BeltLeft=>new HolsterPose(new(-.25f,-.50f,-.03f),-Vector3.UnitY,Vector3.UnitZ,new(-.25f,-.45f,-.03f)),
            HolsterSlot.ArmpitLeft=>new HolsterPose(new(-.13f,-.34f,.03f),-Vector3.UnitZ,Vector3.UnitY,new(-.13f,-.34f,.03f)),
            HolsterSlot.ArmpitRight=>new HolsterPose(new(.13f,-.34f,.03f),-Vector3.UnitZ,Vector3.UnitY,new(.13f,-.34f,.03f)),
            // 0.1.256: 10 cm further forward, in front of the belt's pouch.
            HolsterSlot.Belly=>new HolsterPose(new(.10f,-.47f,.27f),-Vector3.UnitX,Vector3.UnitY,new(.10f,-.47f,.27f)),
            HolsterSlot.LeftShoulder=>new HolsterPose(new(.05f,-.55f,-.19f),Vector3.Normalize(new(-.35f,1,0)),-Vector3.UnitZ,new(-.14f,-.12f,-.12f)),
            HolsterSlot.RightBack=>new HolsterPose(new(-.05f,-.55f,-.23f),Vector3.Normalize(new(.35f,1,0)),-Vector3.UnitZ,new(.14f,-.12f,-.12f)),
            // Knife and grenade by their middle, on the chest above the rifle
            // (0.1.127: 10 cm higher, they went through the belly rifle).
            HolsterSlot.ChestLeft=>new HolsterPose(new(-.10f,-.22f,.15f),-Vector3.UnitY,Vector3.UnitZ,new(-.10f,-.22f,.15f)),
            HolsterSlot.ChestRight=>new HolsterPose(new(.10f,-.22f,.15f),-Vector3.UnitY,Vector3.UnitZ,new(.10f,-.22f,.15f)),
            _=>new HolsterPose(Vector3.Zero,Vector3.UnitZ,Vector3.UnitY,Vector3.Zero)
        };
        if(moved&&slot!=HolsterSlot.None)
        {
            var o=Offset(slot);
            if(float.IsFinite(o.X)&&float.IsFinite(o.Y)&&float.IsFinite(o.Z)&&o.LengthSquared()>0)p=new HolsterPose(p.Grip+o,p.Forward,p.Up,p.Reach+o);
        }
        if(!leftHanded)return p;
        static Vector3 M(Vector3 v)=>new(-v.X,v.Y,v.Z);
        return new HolsterPose(M(p.Grip),M(p.Forward),M(p.Up),M(p.Reach));
    }
    // The weapon's rotation in the torso frame (Forward = +Z, Up = +Y).
    internal static Quaternion Rotation(HolsterPose p)
    {
        var f=Vector3.Normalize(p.Forward);var u=p.Up-f*Vector3.Dot(p.Up,f);
        u=u.LengthSquared()>1e-8f?Vector3.Normalize(u):Math.Abs(f.Y)<.9f?Vector3.UnitY:Vector3.UnitZ;
        var r=Vector3.Cross(u,f);
        // Columns (right, up, forward) as a rotation matrix.
        var m=new Matrix4x4(r.X,r.Y,r.Z,0,u.X,u.Y,u.Z,0,f.X,f.Y,f.Z,0,0,0,0,1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }
    // 0.1.200: which other weapon of a kind
    // gives way to one more taken from the ground: one lying on the floor
    // first, else the longest kept one on the body; never one in a hand.
    // -1: there is room (fewer than max); -2: all of them are in hands.
    internal static int LooseToFree(IReadOnlyList<(int key,bool floor,bool hand)> loose,int max)
    {
        if(loose.Count<max)return -1;
        int best=-2;bool bestFloor=false;
        foreach(var l in loose)
        {
            if(l.hand)continue;
            if(best<0||l.floor&&!bestFloor||l.floor==bestFloor&&l.key<best){best=l.key;bestFloor=l.floor;}
        }
        return best;
    }
}
// 0.1.124: which body place each owned weapon has. A newly seen weapon takes
// the first free place for its kind, in the order the weapons were owned (the
// mission's starting weapons first); with none free it is kept out of sight
// (still in the wheel). The player can hang a weapon on any free place of
// its kind by hand; a weapon dropped on the floor gives its place up.
internal sealed class HolsterAssignment
{
    private readonly Dictionary<int,HolsterSlot> slots=new();
    internal int Count=>slots.Count;
    internal HolsterSlot SlotOf(int key)=>slots.TryGetValue(key,out var s)?s:HolsterSlot.None;
    internal bool Known(int key)=>slots.ContainsKey(key);
    // 0.1.142: the weapon on a place (-1: none).
    internal int Holder(HolsterSlot slot,int except)
    {
        if(slot==HolsterSlot.None)return -1;
        foreach(var e in slots)if(e.Key!=except&&e.Value==slot)return e.Key;
        return -1;
    }
    internal bool Occupied(HolsterSlot slot,int except)
    {
        if(slot==HolsterSlot.None)return false;
        foreach(var e in slots)if(e.Key!=except&&e.Value==slot)return true;
        return false;
    }
    // Keeps an existing place; otherwise the first free one (or None).
    internal HolsterSlot Assign(int key,string profile)
    {
        if(slots.TryGetValue(key,out var s)&&s!=HolsterSlot.None)return s;
        s=HolsterSlot.None;
        foreach(var c in HolsterLayout.Candidates(profile))if(!Occupied(c,key)){s=c;break;}
        slots[key]=s;return s;
    }
    internal bool Place(int key,string profile,HolsterSlot slot)
    {
        if(slot==HolsterSlot.None||Occupied(slot,key))return false;
        bool fits=false;foreach(var c in HolsterLayout.Candidates(profile))if(c==slot)fits=true;
        if(!fits)return false;
        slots[key]=slot;return true;
    }
    // Dropped on the floor: the place is free for others (and taken again,
    // if still free, when this weapon comes back).
    internal void Release(int key){if(slots.ContainsKey(key))slots[key]=HolsterSlot.None;}
    internal void Forget(int key)=>slots.Remove(key);
    internal void Clear()=>slots.Clear();
    // 0.1.134: two weapons of one kind trade places (another one taken from
    // the ground comes into play as the owned one).
    internal void Swap(int a,int b)
    {
        bool ka=slots.TryGetValue(a,out var sa),kb=slots.TryGetValue(b,out var sb);
        if(kb)slots[a]=sb;else slots.Remove(a);
        if(ka)slots[b]=sa;else slots.Remove(b);
    }
    internal IEnumerable<int> Keys=>slots.Keys;
    // The free (or own) place of this kind nearest to a point, within reach.
    internal HolsterSlot Nearest(int key,string profile,Func<HolsterSlot,float> distance,float radius)
    {
        var best=HolsterSlot.None;float bestD=radius;
        foreach(var c in HolsterLayout.Candidates(profile))
        {
            if(Occupied(c,key))continue;
            float d=distance(c);if(float.IsFinite(d)&&d<=bestD){bestD=d;best=c;}
        }
        return best;
    }
}
// 0.1.124: how the right grip holds a weapon.
//  Hold: in the hand while the grip is held (a weapon chosen in the wheel
//  without the grip stays until the grip has been pressed and let go);
//  Toggle: a grip press takes it, the next press puts it away;
//  Always: the old way (always in the hand, no body places).
internal enum WeaponGripMode{Hold=0,Toggle=1,Always=2}
internal sealed class WeaponGripState
{
    private bool owned,pressTook;
    internal bool Owned=>owned;
    // A weapon came to the hand; byGrip: taken with the grip held (body place,
    // floor, wheel + grip).
    internal void Took(bool byGrip){owned=byGrip;pressTook=byGrip;}
    internal void Reset(){owned=pressTook=false;}
    // True when the weapon is to leave the hand now.
    internal bool Step(WeaponGripMode mode,bool held,bool down)
    {
        if(mode==WeaponGripMode.Always){owned=pressTook=false;return false;}
        if(mode==WeaponGripMode.Hold)
        {
            if(held){owned=true;return false;}
            if(owned){owned=false;return true;}
            return false;
        }
        if(!held)pressTook=false;
        if(down&&!pressTook){pressTook=true;return true;}
        return false;
    }
}
// 0.1.128: either hand holds a weapon by its handle, the other can hold its
// fore-end. A weapon in the left hand is held like in the right hand,
// mirrored across the weapon's vertical middle plane (the weapon itself is not
// mirrored: the hand is on its other side).
internal static class HandMirror
{
    internal static Vector3 Point(Vector3 v)=>new(-v.X,v.Y,v.Z);
    // 0.1.133: mirrored across the weapon's own middle (fitted x = center).
    internal static Vector3 Point(Vector3 v,float center)=>new(2*center-v.X,v.Y,v.Z);
    internal const float MaxCenter=.03f;
    // The middle of a weapon's left/right bone pairs (median of the pairs'
    // middles); 0 (its box's middle) without pairs; at most 3 cm off.
    internal static float Center(IReadOnlyList<float> pairMiddles)
    {
        var v=new List<float>();foreach(var m in pairMiddles)if(float.IsFinite(m))v.Add(m);
        if(v.Count==0)return 0;
        v.Sort();float c=v.Count%2==1?v[v.Count/2]:(v[v.Count/2-1]+v[v.Count/2])*.5f;
        return Math.Clamp(c,-MaxCenter,MaxCenter);
    }
    // 0.1.135: the middle across of the part a hand holds, from the x of the
    // weapon's points around the palm (5 %..95 % of them: stray points of the
    // trigger guard or receiver do not count); NaN with too few points.
    internal const int MinHandlePoints=20;
    internal static float HandleMiddle(List<float> xs)
    {
        var v=new List<float>();foreach(var x in xs)if(float.IsFinite(x))v.Add(x);
        if(v.Count<MinHandlePoints)return float.NaN;
        v.Sort();float lo=v[(int)(v.Count*.05f)],hi=v[Math.Min(v.Count-1,(int)(v.Count*.95f))];
        return Math.Clamp((lo+hi)*.5f,-MaxCenter,MaxCenter);
    }
    // The name of a bone's pair on the other side ("left" <-> "right",
    // any case), or null.
    internal static string? PairName(string name)
    {
        int i=name.IndexOf("left",StringComparison.OrdinalIgnoreCase);
        if(i<0)return null;
        string right=char.IsUpper(name[i])?"Right":"right";
        return name.Substring(0,i)+right+name.Substring(i+4);
    }
    internal static Quaternion Rotation(Quaternion q)=>new(q.X,-q.Y,-q.Z,q.W);
    // 0.1.140: how far a weapon's own level (its left/right bone pairs, seen
    // from behind: x right, y up) is turned about its barrel, in degrees
    // (+ = counter-clockwise: the right one higher); median of the pairs at
    // least 2 cm apart; NaN without such pairs.
    internal static float PairRoll(IReadOnlyList<(Vector3 left,Vector3 right)> pairs)
    {
        var a=new List<float>();
        foreach(var (l,r) in pairs)
        {
            var d=r-l;if(d.X<0)d=-d;
            if(!float.IsFinite(d.X+d.Y)||Math.Abs(d.X)<.02f)continue;
            a.Add(MathF.Atan2(d.Y,d.X)*180/MathF.PI);
        }
        if(a.Count==0)return float.NaN;
        a.Sort();return a.Count%2==1?a[a.Count/2]:(a[a.Count/2-1]+a[a.Count/2])*.5f;
    }
    // Euler (pitch, yaw, roll) weapon trim of the right hand, for the left hand.
    internal static Vector3 Trim(Vector3 degrees)=>new(degrees.X,-degrees.Y,-degrees.Z);
    // The weapon keeps its place relative to the hand that holds its fore-end.
    internal static (Vector3 offset,Quaternion rotation) Relative(Vector3 hand,Quaternion handRotation,Vector3 weapon,Quaternion weaponRotation)
    {
        var inverse=Quaternion.Inverse(Quaternion.Normalize(handRotation));
        return (Vector3.Transform(weapon-hand,inverse),Quaternion.Normalize(inverse*weaponRotation));
    }
    internal static (Vector3 position,Quaternion rotation) Apply(Vector3 hand,Quaternion handRotation,Vector3 offset,Quaternion rotation)
    {
        var q=Quaternion.Normalize(handRotation);
        return (hand+Vector3.Transform(offset,q),Quaternion.Normalize(q*rotation));
    }
}
internal enum HandTake{Refuse,Game,Copy}
internal static class HandRoles
{
    // Hangs by its fore-end in the other hand when the handle is let go
    // (pistols and revolvers have no fore-end: they are let go).
    internal static bool ForeEnd(string profile)=>HolsterLayout.Firearm(profile)&&profile is not ("pistol" or "revolver");
    // How an empty hand takes a weapon: the game's own weapon (it fires) when
    // the other hand has no game weapon; otherwise a still copy that comes into
    // play with its trigger. The knife and grenade are thrown by the right hand
    // as the game's weapon; the left hand holds them as a copy.
    internal static HandTake Take(string profile,bool rightHand,string otherGame)
    {
        if(!HolsterLayout.Kind(profile))return HandTake.Refuse;
        // 0.1.156: the left hand takes the knife as
        // the game's own when the right hand has no game weapon - held and
        // thrown at once like the right hand's (a copy only beside another weapon).
        if(profile=="knife"&&!rightHand&&otherGame.Length==0)return HandTake.Game;
        if(profile is "knife" or "grenade")return rightHand&&(otherGame.Length==0||HolsterLayout.Firearm(otherGame))?HandTake.Game:HandTake.Copy;
        if(!HolsterLayout.Firearm(profile))return HandTake.Refuse;
        return otherGame.Length==0?HandTake.Game:HandTake.Copy;
    }
    // A copy's trigger brings it into play: the other hand's game weapon (if
    // any) becomes a copy in that hand. Not while it is a knife or grenade.
    internal static bool Switch(string copy,string otherGame)=>HolsterLayout.Firearm(copy)&&(otherGame.Length==0||HolsterLayout.Firearm(otherGame));
}
// 0.1.125: taking a weapon from a distance by pointing a hand at it (a cone
// that widens with the distance; the one nearest the pointing line wins,
// nearer ones slightly preferred).
internal static class PointingMath
{
    // 0.1.140: a wider cone: 14 cm + 12 cm per metre (was 10 + 7).
    // 0.1.171 (still long aiming, with either hand): 18 cm + 16 cm per metre;
    // a press with nothing pointed looks in a cone wider still (widen).
    internal const float MinDistance=.25f,MaxDistance=3.5f,Base=.18f,Spread=.16f;
    // The weapon already pointed at stays the target unless another scores
    // clearly better (no flicker between two weapons lying side by side).
    internal const float Sticky=1.6f;
    // 0.1.179: a
    // weapon is taken only with nothing solid between. Pointed at: the head
    // sees it (its middle or just above it). Reached (the hand at it): the
    // hand sees it, and the hand is on the player's side of any wall (the
    // head sees the hand) or the head sees the weapon.
    internal static bool Seen(bool reach,bool headSeesWeapon,bool headSeesHand,bool handSeesWeapon)
        =>reach?handSeesWeapon&&(headSeesHand||headSeesWeapon):headSeesWeapon;
    // A hit on the line blocks unless it is the weapon's own part, the
    // player (or what he holds), a loose moving thing, a person (a body lying
    // on its gun), or the surface the weapon lies on (within SightTouch of it).
    internal const float SightTouch=.04f;
    internal static bool Blocks(float hitDistance,float length,bool own,bool player,bool loose,bool person)
        =>!own&&!player&&!loose&&!person&&length-hitDistance>SightTouch;
    // Lower is better; +infinity when the target is not pointed at.
    internal static float Score(Vector3 origin,Vector3 direction,Vector3 target,float min=MinDistance,float max=MaxDistance,float widen=1)
    {
        float l=direction.Length();if(!(l>1e-5f)||!float.IsFinite(l))return float.PositiveInfinity;
        var d=direction/l;var v=target-origin;float along=Vector3.Dot(v,d);
        if(!float.IsFinite(along)||along<min||along>max)return float.PositiveInfinity;
        float perp=(v-d*along).Length(),cone=(Base+along*Spread)*Math.Clamp(widen,1,3);
        return perp<=cone?perp/cone+along*.05f:float.PositiveInfinity;
    }
}
