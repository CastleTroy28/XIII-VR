using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
using Slot=PlayerEquipableInventory.ActiveEquipmentSlot;
namespace XiiiXR;
// 0.1.126: two hands with weapons ("juggling").
// 0.1.128: either hand holds the game's own weapon (the one that fires) by
// its handle and fires it with its own trigger; the other hand can hold its
// fore-end. Letting go of the handle while the other hand holds the fore-end
// leaves the weapon hanging there by the fore-end; the grip at the handle
// takes it again. A left-hand hold is the right-hand one mirrored across the
// weapon's middle (the weapon itself is not mirrored). A hand takes a weapon
// as the game's weapon when the other hand has none; otherwise it holds a
// still copy whose trigger brings it into play (the game switches weapons,
// about a second) - the other hand's weapon then stays there as the copy.
// The game only has one weapon in play at a time.
// Let go (as set for the grip) at a body place: it hangs there; elsewhere: it
// drops to the floor. The watch shows both hands' rounds: right / left.
// A grenade in the left hand: the right hand at it + right trigger pulls the
// pin; swing and let go of the left grip to throw it.
internal sealed partial class WeaponHands
{
    // Index 0: the left hand, 1: the right hand.
    private readonly int[] copyKey={-1,-1};
    private readonly string[] copyProfile={"",""};
    private readonly WeaponGripState[] copyGrip={new(),new()};
    private readonly bool[] copySnap=new bool[2],gameSnap=new bool[2],hinted=new bool[2];
    private readonly Vector3[] handAt=new Vector3[2],handVelocity=new Vector3[2],handPos=new Vector3[2];private readonly bool[] handBusy=new bool[2];
    private readonly float[] handAtTime=new float[2];
    // 0.1.135: how fast each hand turns (world, rad/s), for a spin throw.
    private readonly Vector3[] handSpin=new Vector3[2];private readonly Quaternion[] handTurnAt={Quaternion.identity,Quaternion.identity};
    // The slot of the game's weapon that the left hand holds.
    private int leftGameKey=-1;private float leftGameSince;
    // The left trigger fires only after it was let go once in the left hand.
    private bool leftTriggerLocked;
    // Held only by the fore-end (the handle let go): where the weapon is
    // relative to the controller of the hand holding the fore-end.
    private bool foreEndOnly;private Vector3 foreEndOffset;private Quaternion foreEndRotation=Quaternion.identity;
    private int putAwaySide=1;private float nextHandsReport;private int putAwayFrame=-1;
    // 0.1.130: a copy held by its fore-end (the game's weapon became a copy
    // while it hung by the fore-end): where it is relative to that controller.
    private readonly bool[] copyForeEnd=new bool[2];
    // A copy's trigger fires once let go after taking it; a take deferred
    // while the game was switching weapons.
    private readonly bool[] copyTriggerLocked=new bool[2];private readonly int[] deferredTake={-1,-1};private readonly float[] deferredAt=new float[2];
    private readonly Vector3[] copyHoldOffset=new Vector3[2];private readonly Quaternion[] copyHoldRotation={Quaternion.identity,Quaternion.identity};
    // The left native hand's fore-end grip of each weapon kind (fitted space).
    private static readonly Dictionary<string,(Vector3 point,Quaternion rotation,float size)> foreEndGrips=new();
    // The right hand's native grip of each weapon kind (fitted space), for
    // still copies held in a hand.
    private static readonly Dictionary<string,(Vector3 point,Quaternion rotation,float size)> handleGrips=new();
    // 0.1.152: the holds kept between games (HandGripMemory).
    private static bool gripsLoaded,gripsDirty;private static float gripsSaveAt;
    private static string GripFile=>System.IO.Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-hand-grips.txt");
    private static void LoadGrips()
    {
        if(gripsLoaded)return;gripsLoaded=true;
        // 0.1.159:
        // a left-hander never holds the game's grenade in the right hand, so its
        // hold was never seen and the grenade copy was laid in the closed hand by
        // a guess (GrenadeDefault) - the wrong way up. The game's own hold of its
        // grenade is always the same (the right hand's, in every log): it is known
        // from the start; the left hand holds the copy with it, mirrored (as the
        // knife). A hold seen during a throw is not kept for it.
        handleGrips["grenade"]=GrenadeHold;
        try
        {
            if(!System.IO.File.Exists(GripFile))return;int n=0;
            foreach(var e in HandGripMemory.Read(System.IO.File.ReadAllLines(GripFile)))
                if(!handleGrips.ContainsKey(e.Key)){handleGrips[e.Key]=(new Vector3(e.Value.point.X,e.Value.point.Y,e.Value.point.Z),new Quaternion(e.Value.rotation.X,e.Value.rotation.Y,e.Value.rotation.Z,e.Value.rotation.W),e.Value.size);n++;}
            Bootstrap.Write("HAND GRIPS "+n+" weapon holds read from the last games (a copy in the other hand is held with them): "+string.Join(", ",handleGrips.Keys));
        }
        catch(Exception ex){Bootstrap.Warn("HAND GRIPS unreadable: "+ex.Message);}
    }
    // The game's right hand on its grenade (NATIVE GRIP bound weapon=grenade side=R, the same in every game).
    private static (Vector3 point,Quaternion rotation,float size) GrenadeHold=>(new Vector3(-.009505f,-.02143f,.47469f),Quaternion.Euler(0f,91.886f,191.906f),.79368f);
    private static void RememberGrip(string profile,Vector3 point,Quaternion rotation,float size)
    {
        if(profile=="grenade")return;
        bool known=handleGrips.TryGetValue(profile,out var old);
        handleGrips[profile]=(point,rotation,size);
        if(HandGripMemory.Changed(known,new System.Numerics.Vector3(old.point.x,old.point.y,old.point.z),new System.Numerics.Vector3(point.x,point.y,point.z))){if(!gripsDirty)gripsSaveAt=Time.realtimeSinceStartup+10;gripsDirty=true;}
    }
    private static void SaveGrips()
    {
        if(!gripsDirty||Time.realtimeSinceStartup<gripsSaveAt)return;gripsDirty=false;
        try
        {
            var lines=new List<string>();
            foreach(var e in handleGrips)lines.Add(HandGripMemory.Format(e.Key,new System.Numerics.Vector3(e.Value.point.x,e.Value.point.y,e.Value.point.z),new System.Numerics.Quaternion(e.Value.rotation.x,e.Value.rotation.y,e.Value.rotation.z,e.Value.rotation.w),e.Value.size));
            System.IO.File.WriteAllLines(GripFile,lines);
        }
        catch(Exception ex){Bootstrap.Warn("HAND GRIPS not saved: "+ex.Message);}
    }
    // 0.1.152: a knife copy is held like the game's knife in the right hand (the left hand mirrored), once that hold is known.
    // 0.1.153: a knife or a grenade copy held as the game's own one is held
    // by the right hand (0.1.152: the knife only) - once that hold is known.
    private static readonly Dictionary<string,Matrix4x4[]?[]> heldPoses=new();private float nextHeldPose;
    internal static Matrix4x4[]?[]? HeldPoseOf(string p)=>heldPoses.TryGetValue(p,out var x)?x:null;
    private static bool KnifeHold(string p)=>(p=="knife"||p=="grenade")&&handleGrips.ContainsKey(p);
    // 0.1.156: a grenade copy whose right-hand hold was never seen (a
    // left-hander rarely holds the game's grenade in the right hand) is held
    // in the closed hand: its middle where the curled fingers close, the hand
    // on the controller, the fingers round it (not the pointing hand).
    private static bool GrenadeDefault(string p)=>p=="grenade"&&!handleGrips.ContainsKey(p);
    private static Quaternion GrenadeTurn(bool right)=>Quaternion.Euler(0,right?-90:90,0);
    private Vector3 GrenadeContact(int s)
    {
        var native=s==1?rightNative:leftNative;var fallback=new Vector3(0,-.025f,.073f);
        try{return native==null?fallback:native.PropPowerContact(s==1?"grenade":FingerPoseMath.MirrorPrefix+"grenade",.06f);}catch(Exception){return fallback;}
    }
    private static bool HeldCopy(string p)=>HolsterLayout.Firearm(p)||KnifeHold(p)||GrenadeDefault(p);
    private static string Side(int s)=>s==0?"left":"right";
    private static Quaternion MirrorQ(Quaternion q)=>new(q.x,-q.y,-q.z,q.w);
    // The game's weapon is in the left hand.
    internal bool PrimaryLeft=>weapon!=null&&leftGameKey>=0&&(int)weapon.slot==leftGameKey&&!DualActive;
    // 0.1.133: mirrored across the weapon's own middle (WeaponVisual.MirrorCenterX).
    private static readonly Dictionary<string,float> mirrorCenters=new();
    private static float MirrorCenter(string p)=>mirrorCenters.TryGetValue(p,out var c)?c:0;
    private static Vector3 MirrorAcross(string p,Vector3 v)=>new(2*MirrorCenter(p)-v.x,v.y,v.z);
    private Vector3 Sided(Vector3 v)=>PrimaryLeft?MirrorAcross(profile,v):v;
    // 0.1.130: the left hand's grip of a handle: mirrored, and the weapon a
    // little to the right in that hand (the VR SETTINGS / config left pistol
    // offset, as the second pistol always had), so it lies in the palm.
    private static float LeftHandleOffset{get{try{return Math.Clamp(WeaponOptions.LeftPistolOffset.Value,-.04f,.04f);}catch(Exception){return .015f;}}}
    // 0.1.135: long guns are mirrored across the middle of their handle and
    // held without the pistol's extra shift (they hung beside the left hand).
    // 0.1.142: handguns mirrored across the measured handle middle with the
    // shift that keeps the pistol where 0.1.139 had it (LeftHoldMath).
    internal static Vector3 LeftHandle(string p,Vector3 v)=>new(LeftHoldMath.HandleX(v.x,MirrorCenter(p),mirrorCenters.ContainsKey(p),LeftHoldMath.Handgun(p),LeftHandleOffset),v.y,v.z);
    private Vector3 HandleSided(Vector3 v)=>PrimaryLeft?LeftHandle(profile,v):v;
    // 0.1.142: the left Y reloads the left hand's gun (not the next weapon).
    internal bool LeftYReloads=>PrimaryLeft&&weapon!=null&&HolsterLayout.Firearm(profile)||copyKey[0]>=0&&HolsterLayout.Firearm(copyProfile[0]);
    private void TickLeftCopyReload()
    {
        if(copyKey[0]<0||!HolsterLayout.Firearm(copyProfile[0])||copyForeEnd[0]||!rig.LeftControls.Valid||(rig.LeftControls.Down&HandControls.B)==0)return;
        StartHandReload(0,true);
    }
    private int CurrentKey=>inventory?.currentEquipable is Equipable c?(int)c.slot:-1;
    // The hand with the game's weapon (not the fists): 0 left, 1 right, -1 none.
    private int GameSide(int key)
    {
        if(key<0||key==(int)Slot.Fist)return -1;
        return leftGameKey>=0&&key==leftGameKey&&!DualActive?0:1;
    }
    private bool HandleHeld(int s,int key)=>GameSide(key)==s&&!foreEndOnly;
    private bool ForeEndHeld(int s,int key)=>GameSide(key)==1-s&&weapon!=null&&(SupportHeld||foreEndOnly||barrelHand==s);
    internal bool LeftHoldsWeapon=>copyKey[0]>=0||HandleHeld(0,CurrentKey);
    internal bool HandHoldsWeapon(bool right)
    {
        int s=right?1:0;if(copyKey[s]>=0||SupportsCopy(right)||!right&&LeftPistolVisible)return true;
        return HasTrackedWeapon&&HandleHeld(s,CurrentKey);
    }
    internal bool CopyInHand(bool right)=>copyKey[right?1:0]>=0||SupportsCopy(right);
    // 0.1.130: the hand that swings (hits with) a weapon: the one moving the
    // game's weapon (by the handle, or by the fore-end when it hangs there) or
    // holding a copy. A hand only holding the fore-end of a weapon held by the
    // other hand does not punch.
    internal bool MeleeHand(bool right)
    {
        int s=right?1:0;
        if(copyKey[s]>=0)return HolsterLayout.Firearm(copyProfile[s]);
        int g=GameSide(CurrentKey);if(g<0||weapon==null)return right;
        return (foreEndOnly?1-g:g)==s;
    }
    internal bool PunchBlocked(bool right)=>!MeleeHand(right)&&(SupportsCopy(right)||(right?ForeEndHeld(1,CurrentKey):OffhandOccupied)
        // 0.1.221: a hand holding a magazine, rounds or a rocket by its grip does not punch with it.
        ||ReloadHandHolding(right)||rocketSide==(right?1:0)||PouchTakesNow(right?1:0)
        // 0.1.223: nor a hand working a bolt with its grip.
        ||right==ReloadRight&&ManualReady&&reload.Racking);
    internal bool RightAtLeftGrenade{get;private set;}
    // The free hand collides with the game's weapon held by the other hand.
    // 0.1.196: not the hand holding its barrel.
    internal bool FreeHandCollides(bool right)=>right==PrimaryLeft&&barrelHand!=(right?1:0);
    // 0.1.160 (for a player with a gun stock: holding a rifle by the right
    // grip, the left hand could not reach its fore-end without taking the
    // controller off the stock): a two-handed gun held in one hand is turned
    // toward the other hand (to the right in the right hand, to the left in the
    // left hand) about the hand, by the VR setting (0-45 degrees). With both
    // hands on it, the hands aim it as before.
    internal static Quaternion GunAim(PoseValue pose,bool right,string profile)
    {
        var aim=HandAim(pose,right);
        if(!HandRoles.ForeEnd(profile))return aim;
        int turn=QualityMenu.GunTurnDegrees;if(turn==0)return aim;
        return aim*Quaternion.Euler(0,right?turn:-turn,0);
    }
    internal static Quaternion HandAim(PoseValue pose,bool right)
    {
        var t=new System.Numerics.Vector3(WeaponOptions.Pitch.Value,WeaponOptions.Yaw.Value,WeaponOptions.Roll.Value);
        if(right)return ControllerAim.Rotation(pose)*Quaternion.Euler(t.X,t.Y,t.Z);
        var m=HandMirror.Trim(t);return ControllerAim.Mirrored(pose)*Quaternion.Euler(m.X,m.Y,m.Z);
    }
    private (bool held,bool down) GripInput(int s)
    {
        var c=s==0?rig.LeftControls:rig.RightControls;
        return (c.Valid&&(c.Held&HandControls.Grip)!=0,c.Valid&&(c.Down&HandControls.Grip)!=0);
    }
    private HandControls TriggerControls=>PrimaryLeft?rig.LeftControls:rig.RightControls;
    private HandControls SupportControls=>PrimaryLeft?rig.RightControls:rig.LeftControls;
    // The hand along the game weapon's length (held by the other hand): the
    // grip there takes its fore-end, not a body weapon.
    private bool AtGameWeapon(Vector3 hand)
    {
        if(weapon==null||!poseValid||visual==null)return false;
        var a=visual.FittedToWorld.MultiplyPoint3x4(PrimaryHandPoint());var b=aimPosition;var ab=b-a;
        float t=ab.sqrMagnitude>1e-8f?Mathf.Clamp01(Vector3.Dot(hand-a,ab)/ab.sqrMagnitude):0;
        return Vector3.Distance(hand,a+ab*t)<.16f;
    }
    private bool NearHandle(Vector3 hand)
    {
        if(weapon==null||visual==null)return false;
        return Vector3.Distance(hand,visual.FittedToWorld.MultiplyPoint3x4(PrimaryHandPoint()))<HolsterLayout.GrabRadius+.03f;
    }
    // 0.1.130: also while the game switches weapons (a weapon tossed up is
    // caught at once) and, while the game's weapon hangs by its fore-end in the
    // other hand, the free hand takes another weapon.
    private bool HandFreeForWeapon(int s,Vector3 hand,bool valid,int key)
    {
        var c=s==0?rig.LeftControls:rig.RightControls;
        if(!valid||!c.Valid||copyKey[s]>=0||CopySupported(1-s)||DualActive||LeftThrowBusy||MountedGunVr.HidesHands||inventory==null||rocketSide==s)return false;
        // 0.1.221: at the belt while the gun wants rounds, the grip takes them (not a weapon there).
        if(PouchTakes(s,hand))return false;
        bool leftJustNow=key==putAwayKey&&Time.realtimeSinceStartup-putAwayAt<1.5f;
        int g=leftJustNow?-1:GameSide(key);
        if(g==s&&!foreEndOnly)return false;
        if(g==1-s&&(SupportHeld||foreEndOnly||AtGameWeapon(hand)))return false;
        if(InteractionDriver.Current?.HandOccupied(s==1)==true)return false;
        if(s==1)return GripCarry.Current?.HidesHand(true)!=true&&!ReloadHandHolding(true)&&GameUiControls.Current?.RightItemHeld!=true;
        return !LeftReloadHolding&&!LeftPistolVisible&&GripCarry.Current?.HidesLeft!=true&&GameUiControls.Current?.LeftItemHeld!=true;
    }
    // ---- The game's weapon in a hand ----
    private void TickGameWeapon(int s,Vector3[] hands,PoseValue l,PoseValue r,WeaponGripMode mode,bool[] busy)
    {
        if(holsters==null||weapon==null)return;
        int o=1-s,key=(int)weapon.slot;busy[s]=!foreEndOnly;
        var (held,down)=GripInput(s);
        if(foreEndOnly)
        {
            busy[o]=true;hinted[o]=true;
            bool snap=holsters.Hint(key,profile,hands[o],out _,o==0);
            if(snap&&!gameSnap[o])rig.PunchHaptics(o==1);gameSnap[o]=snap;
            if(!GripInput(o).held){PutAway(key,hands[o],handVelocity[o],o);return;}
            if(down&&NearHandle(hands[s]))
            {
                busy[s]=true;bool wasClub=Clubbed;foreEndOnly=false;ClearClub();gripState.Took(true);if(s==0)leftTriggerLocked=true;else rig.DisarmTrigger();rig.PunchHaptics(s==1);
                Bootstrap.Write("HANDS "+Side(s)+" hand took "+profile+" by the handle again"+(wasClub?" (from the club hold: it shoots again)":""));
                return;
            }
            // 0.1.196: the other hand's grip at the barrel's front takes it as a club
            // (from the club hand, or from the hand holding its fore-end).
            if(down&&ClubMath.Clubbable(profile)&&HandFreeForBarrel(s)&&AtBarrelFront(hands[s],out float taken))
            {busy[s]=true;StartClub(s,taken,Clubbed?"taken over from the "+Side(o)+" hand":"taken from the fore-end in the "+Side(o)+" hand");return;}
            return;
        }
        hinted[s]=true;
        bool near=holsters.Hint(key,profile,hands[s],out _,s==0);
        if(near&&!gameSnap[s])rig.PunchHaptics(s==1);gameSnap[s]=near;
        if(SupportHeld)busy[o]=true;
        // 0.1.196: the other hand at the barrel's front holds the barrel.
        TrackBarrelHand(o,hands[o]);if(barrelHand==o)busy[o]=true;
        // A grenade with its pin out is thrown by letting go (TickGrenade).
        if(profile=="grenade"&&grenadeGesture.PinPulled){gripState.Reset();return;}
        // 0.1.149: the knife: held by the grip, thrown by letting go in a swing.
        if(profile=="knife"){KnifeGrip(s,hands[s],held,down,mode,key);return;}
        if(!gripState.Step(mode,held,down))return;
        if(KeptSnatched(s,gripState,profile))return;
        // 0.1.196: the handle let go while the other hand holds the barrel's front: a club in that hand.
        if(barrelHand==o&&GripInput(o).held&&ClubMath.Clubbable(profile)&&visual!=null){busy[o]=true;StartClub(o,barrelAlong,"the "+Side(s)+" hand let go of the handle");return;}
        if(SupportHeld&&HandRoles.ForeEnd(profile)&&GripInput(o).held&&visual!=null)
        {
            var pose=o==0?l:r;var frame=visual.FittedToWorld;
            var rel=HandMirror.Relative(ToN(CameraRig.UnityPosition(pose)),ToN(CameraRig.UnityRotation(pose)),ToN((Vector3)frame.GetColumn(3)),ToN(frame.rotation));
            foreEndOffset=new Vector3(rel.offset.X,rel.offset.Y,rel.offset.Z);foreEndRotation=new Quaternion(rel.rotation.X,rel.rotation.Y,rel.rotation.Z,rel.rotation.W);
            foreEndOnly=true;ClearClub();StopOwnedFire();busy[o]=true;rig.PunchHaptics(o==1);
            Bootstrap.Write("HANDS "+profile+" let go by the "+Side(s)+" hand: it hangs by its fore-end in the "+Side(o)+" hand (grip at the handle takes it again)");
            return;
        }
        PutAway(key,hands[s],handVelocity[s],s);
    }
    // The game's weapon becomes a still copy in the hand holding it.
    private bool DemoteGame()
    {
        if(inventory==null||holsters==null||weapon==null||visual==null||DualActive||!HolsterLayout.Firearm(profile))return false;
        int key=(int)weapon.slot,s=PrimaryLeft?0:1;string p=profile;
        // 0.1.130: hanging by its fore-end: the copy stays in that hand, by the fore-end.
        bool byForeEnd=foreEndOnly;if(byForeEnd)s=1-s;
        if(copyKey[s]>=0)return false;
        HolsterCopy? copy;
        try{copy=HolsterCopy.From(visual,NativeGrip(true,Vector3.zero),false);}
        catch(Exception ex){Bootstrap.Warn("HANDS copy of the game's "+p+": "+ex.Message);return false;}
        KeepCopyClub(s,byForeEnd);bool asClub=copyClub[s];
        CancelReloadGesture();StopOwnedFire();ReleaseSupport();foreEndOnly=false;ClearClub();
        holsters.Store(key,copy);holsters.Hold(key);
        copyKey[s]=key;copyProfile[s]=p;copyGrip[s].Took(byForeEnd||gripState.Owned);copySnap[s]=false;
        copyForeEnd[s]=byForeEnd;copyHoldOffset[s]=foreEndOffset;copyHoldRotation[s]=foreEndRotation;copyTriggerLocked[s]=true;
        if(byForeEnd)Bootstrap.Write("HANDS "+p+" stays in the "+Side(s)+" hand "+(asClub?"by its barrel (a club)":"by its fore-end")+", as a copy");
        if(s==0)leftGameKey=-1;
        visual.Hide();poseValid=false;putAwayKey=key;putAwayAt=Time.realtimeSinceStartup;putAwaySide=s;
        return true;
    }
    private void UndoDemote(int s,int key)
    {
        bool byForeEnd=copyKey[s]==key&&copyForeEnd[s];bool asClub=byForeEnd&&copyClub[s];float along=copyClubAlong[s];
        if(copyKey[s]==key){copyKey[s]=-1;copyProfile[s]="";copyForeEnd[s]=false;copyClub[s]=false;}
        holsters?.Taken(key,EquipmentProfile.ForSlot(key));putAwayKey=-1;
        // Back as it was (hanging by the fore-end: the handle side is the other hand).
        if(byForeEnd){foreEndOnly=true;foreEndOffset=copyHoldOffset[s];foreEndRotation=copyHoldRotation[s];ClearClub();if(asClub){club=true;clubAlong=along;}}
        int handle=byForeEnd?1-s:s;
        if(handle==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;}else RightHandHas(key);
    }
    // A hand's copy is the game's weapon now: that hand holds it by the handle.
    private void PromoteCopies()
    {
        if(weapon==null||inventory==null||inventory.isInTransit||HolsterHidesCurrent)return;
        int key=(int)weapon.slot;
        for(int s=0;s<2;s++)
        {
            if(copyKey[s]!=key)continue;
            string p=copyProfile[s];bool owned=copyGrip[s].Owned||GripInput(s).held;
            // 0.1.136: held by its fore-end: in play hanging by the fore-end in
            // this hand (the other hand's grip at the handle takes it).
            if(copyForeEnd[s])
            {
                int handle=1-s;bool asClub=copyClub[s];float along=copyClubAlong[s];
                copyKey[s]=-1;copyProfile[s]="";copyForeEnd[s]=false;copyClub[s]=false;copySupport[s].Release();
                if(handle==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;leftTriggerLocked=true;}else RightHandHas(key);
                gripState.Took(owned);gripBoundId=weapon.GetInstanceID();ReleaseSupport();
                foreEndOnly=true;foreEndOffset=copyHoldOffset[s];foreEndRotation=copyHoldRotation[s];ClearClub();if(asClub){club=true;clubAlong=along;}
                holsters?.Taken(key,p);
                Bootstrap.Write("HANDS "+Side(s)+" hand holds "+p+(asClub?" by its barrel (a club)":" by its fore-end")+" in play (the "+Side(handle)+" hand's grip at the handle takes it)");
                continue;
            }
            copyKey[s]=-1;copyProfile[s]="";copySupport[s].Release();if(s==0)leftGrenadeGesture.Reset();
            if(s==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;leftTriggerLocked=true;}else RightHandHas(key);
            gripState.Took(owned);gripBoundId=weapon.GetInstanceID();foreEndOnly=false;ReleaseSupport();
            holsters?.Taken(key,p);
            Bootstrap.Write("HANDS "+Side(s)+" hand: "+p+" is the game's weapon now (the "+Side(s)+" trigger fires it)");
        }
    }
    // An empty hand takes a weapon from the body or the floor.
    private void TakeWeapon(int key,int s,string from)
    {
        if(inventory==null||holsters==null)return;
        // 0.1.136: a long gun caught (or taken from the floor) nearer its
        // fore-end than its handle hangs by the fore-end in that hand.
        var fore=from=="the floor"?ForeEndCatch(key,s):null;
        TakeWeaponAt(key,s,from);
        if(fore is Vector3 f&&copyKey[s]==key)HoldByForeEnd(s,f);
    }
    private void TakeWeaponAt(int key,int s,string from)
    {
        if(inventory==null||holsters==null)return;
        // 0.1.130: another weapon of an owned kind is always held as a copy.
        if(BodyHolsters.IsLoose(key)){TakeCopy(key,s,from,false);return;}
        // 0.1.133: a weapon just let go (still the game's for a few frames) is
        // not in a hand any more.
        string p=EquipmentProfile.ForSlot(key);int current=GameKey;
        // The game's weapon hangs by its fore-end in the other hand: it stays
        // there (as a copy) and this hand's weapon is the game's.
        if(foreEndOnly&&GameSide(current)==s)
        {
            if(HandRoles.Take(p,s==1,"")==HandTake.Game&&DemoteGame()){Take(key,s,from);return;}
            TakeCopy(key,s,from,false);return;
        }
        string otherGame=GameSide(current)==1-s?EquipmentProfile.ForSlot(current):"";
        switch(HandRoles.Take(p,s==1,otherGame))
        {
            case HandTake.Copy:TakeCopy(key,s,from,false);return;
            case HandTake.Game:
                // The other hand's weapon stays there, as a still copy.
                if(otherGame.Length>0&&!DemoteGame()){Bootstrap.Write("HANDS the "+Side(1-s)+" hand keeps "+otherGame+" in play");return;}
                Take(key,s,from);return;
            default:Bootstrap.Write("HANDS the "+Side(s)+" hand does not hold "+p);return;
        }
    }
    // 0.1.136: the fore-end point of a long gun in the fitted frame, for the
    // hand on that side (the game's left-hand hold; mirrored for the right).
    internal static bool PlausibleForeEnd(Vector3 p)=>Math.Abs(p.x)<.1f&&Math.Abs(p.y)<.15f&&p.z>-.2f&&p.z<.8f;
    private static Vector3 ForeEndPoint(string p,HolsterCopy copy,bool right)
    {
        var point=foreEndGrips.TryGetValue(p,out var f)&&PlausibleForeEnd(f.point)?f.point:new Vector3(0,-.03f,copy.Grip.z+.30f);
        return right?MirrorAcross(p,point):point;
    }
    // Where the fore-end is (fitted frame) when the hand is nearer to it than
    // to the handle of a long gun lying on the floor or flying; else null.
    private Vector3? ForeEndCatch(int key,int s)
    {
        if(holsters==null)return null;
        string p=holsters.ProfileOf(key);var copy=holsters.CopyOf(key);
        if(!HandRoles.ForeEnd(p)||copy==null||!copy.OnFloor||!copy.Shown)return null;
        var point=ForeEndPoint(p,copy,s==1);var hand=handPos[s];
        float toFore=Vector3.Distance(hand,copy.Position+copy.Rotation*point),toHandle=Vector3.Distance(hand,copy.GripWorld);
        return toFore<toHandle?point:null;
    }
    // The copy just taken into hand s hangs by its fore-end there, turned as
    // it was caught; the fore-end comes to the hand.
    private void HoldByForeEnd(int s,Vector3 forePoint)
    {
        var copy=holsters?.CopyOf(copyKey[s]);if(copy==null)return;
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||s==0&&!leftValid)return;
        var pose=s==0?l:r;var at=CameraRig.UnityPosition(pose);var turn=CameraRig.UnityRotation(pose);
        var rotation=copy.Rotation;var root=copy.Position+(at-(copy.Position+rotation*forePoint));
        var rel=HandMirror.Relative(ToN(at),ToN(turn),ToN(root),ToN(rotation));
        copyForeEnd[s]=true;copyClub[s]=false;copySupport[s].Release();
        copyHoldOffset[s]=new Vector3(rel.offset.X,rel.offset.Y,rel.offset.Z);copyHoldRotation[s]=new Quaternion(rel.rotation.X,rel.rotation.Y,rel.rotation.Z,rel.rotation.W);
        Bootstrap.Write("HOLSTER "+Side(s)+" hand caught "+copyProfile[s]+" by its fore-end (a jerk of the hand pumps a shotgun)");
    }
    private bool TakeCopy(int key,int s,string from,bool quiet)
    {
        var copy=holsters?.TakeHand(key);string p=holsters?.ProfileOf(key)??EquipmentProfile.ForSlot(key);
        if(copy==null){if(!quiet)Bootstrap.Write("HOLSTER "+Side(s)+" hand: no still copy of "+p+" yet");return false;}
        copyKey[s]=key;copyProfile[s]=p;copyGrip[s].Took(true);copySnap[s]=false;copyForeEnd[s]=false;copyTriggerLocked[s]=true;autoPromoteAt=0;
        if(s==0){leftGrenadeGesture.Reset();if(reload.Holding)CancelReloadGesture();}
        rig.PunchHaptics(s==1);
        if(!quiet)Bootstrap.Write("HOLSTER "+Side(s)+" hand took "+p+(BodyHolsters.IsLoose(key)?" (another one)":"")+" from "+from+" (held while the game's weapon is elsewhere: it fires by itself); game weapon: "+(weapon!=null?profile+" in the "+Side(PrimaryLeft?0:1)+" hand":"none"));
        return true;
    }
    private void PutAwayCopy(int s,Vector3 hand)
    {
        if(holsters==null||copyKey[s]<0)return;
        int key=copyKey[s];string p=copyProfile[s];var copy=holsters.CopyOf(key);putAwayFrame=Time.frameCount;
        // Let go while the game was still switching to it: back to the fists.
        if(!BodyHolsters.IsLoose(key)&&CurrentKey==key&&inventory!=null)
        {
            try{inventory.TrySelectSlot(Slot.Fist,true,true,true,false,true);}catch(Exception ex){Bootstrap.Warn("HANDS fists: "+ex.Message);}
            if(s==0)leftGameKey=-1;putAwayKey=key;putAwayAt=Time.realtimeSinceStartup;putAwaySide=s;
        }
        var place=holsters.NearestPlace(key,p,hand);
        if(place!=HolsterSlot.None){holsters.Hang(key,p,place);Bootstrap.Write("HOLSTER "+Side(s)+" hand hung "+p+" on "+place);}
        else if(HolsterLayout.Firearm(p)&&copy!=null&&holsters.Drop(key,copy.Position,copy.Rotation,handVelocity[s],handSpin[s],hand))Bootstrap.Write("HOLSTER "+Side(s)+" hand let go of "+p+": on the floor (still in the wheel)");
        else{var slot=holsters.Return(key,p);Bootstrap.Write("HOLSTER "+Side(s)+" hand: "+p+" back to "+slot);}
        copyKey[s]=-1;copyProfile[s]="";copyForeEnd[s]=false;deferredTake[s]=-1;copySupport[s].Release();if(s==0)leftGrenadeGesture.Reset();rig.PunchHaptics(s==1);
    }
    // An empty hand takes the other hand's copy.
    private void HandOver(int from,int to)
    {
        int key=copyKey[from];if(key<0)return;string p=copyProfile[from];
        copyKey[from]=-1;copyProfile[from]="";copyForeEnd[from]=false;copySupport[from].Release();copySupport[to].Release();if(from==0)leftGrenadeGesture.Reset();
        copyKey[to]=key;copyProfile[to]=p;copyGrip[to].Took(true);copySnap[to]=false;copyForeEnd[to]=false;copyTriggerLocked[to]=true;rig.PunchHaptics(to==1);
        Bootstrap.Write("HOLSTER "+Side(to)+" hand takes "+p+" from the "+Side(from)+" hand (by the handle)");
        if(BodyHolsters.IsLoose(key))return;
        if(CurrentKey==key){if(to==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;}else RightHandHas(key);return;}
        // No game weapon in the hands: it comes into play in its new hand.
        if(GameSide(CurrentKey)<0&&HolsterLayout.Firearm(p))SwitchTo(to);
    }
    // 0.1.134: another weapon of an owned kind (from the ground) in a hand
    // becomes the owned one, so that it can come into play like any weapon
    // (its hand reload, underbarrel, the game's own shot): the two trade
    // places, magazines and reload states. Not while the owned one is in the
    // other hand.
    private bool SwapLoose(int s)
    {
        int key=copyKey[s];if(!BodyHolsters.IsLoose(key)||holsters==null||inventory==null)return false;
        int owner=holsters.OwnerOf(key);if(owner<0||copyKey[1-s]==owner||GameKey==owner)return false;
        var w=holsters.WeaponOf(owner);var a=w==null?null:FireOf(w)?.ammoManagementComponent;if(w==null||a==null)return false;
        if(HandReloading(s))return false;
        int ownedMagazine,looseMagazine=holsters.LooseMagazine(key);
        try{ownedMagazine=a.PrimaryMagazineAmmoCount;}catch(Exception){return false;}
        if(!holsters.SwapWithOwner(key))return false;
        try{a.SetAmmo(looseMagazine,a.SecondaryMagazineAmmoCount);}
        catch(Exception ex){holsters.SwapWithOwner(key);Bootstrap.Warn("HANDS another "+copyProfile[s]+": "+ex.Message);return false;}
        holsters.SetLooseMagazine(key,ownedMagazine);
        int id=w.GetInstanceID();
        reloadStates.TryGetValue(id,out var ownedState);looseReloadStates.TryGetValue(key,out var looseState);
        reloadStates[id]=looseState??ReloadStateFor(copyProfile[s]);
        if(ownedState!=null)looseReloadStates[key]=ownedState;else looseReloadStates.Remove(key);
        copyKey[s]=owner;NotifyAmmo();
        Bootstrap.Write("HANDS "+Side(s)+" hand: the other "+copyProfile[s]+" becomes the owned one (magazine "+looseMagazine+"); the owned one takes its place (magazine "+ownedMagazine+")");
        return true;
    }
    // A copy's trigger: it comes into play (the other hand's weapon becomes the copy).
    private bool SwitchTo(int s)
    {
        if(inventory==null||holsters==null)return false;
        int key=copyKey[s];if(key<0||BodyHolsters.IsLoose(key)||copyForeEnd[s])return false;string p=copyProfile[s];
        int current=CurrentKey;if(current==key||inventory.isInTransit)return current==key;
        string otherGame=GameSide(current)==1-s?EquipmentProfile.ForSlot(current):"";
        if(!HandRoles.Switch(p,otherGame))
        {
            if(Time.realtimeSinceStartup>=nextHandsReport){nextHandsReport=Time.realtimeSinceStartup+3;Bootstrap.Write("HANDS "+p+" stays a copy: the "+Side(1-s)+" hand's "+otherGame+" is in play (throw it or put it away first)");}
            return false;
        }
        int demoted=-1;
        if(otherGame.Length>0){if(!DemoteGame())return false;demoted=current;}
        bool ok;
        try{ok=inventory.HasEquipableInSlot((Slot)key)&&inventory.TrySelectSlot((Slot)key,true,true,true,false,true);}
        catch(Exception ex){ok=false;Bootstrap.Warn("HANDS switch: "+ex.Message);}
        if(!ok){if(demoted>=0)UndoDemote(1-s,demoted);Bootstrap.Write("HANDS the game refused "+p);return false;}
        if(s==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;}else RightHandHas(key);
        gripTakeAt=Time.realtimeSinceStartup;rig.PunchHaptics(s==1);
        Bootstrap.Write("HANDS "+Side(s)+" trigger: "+p+" comes into play"+(demoted>=0?"; the "+Side(1-s)+" hand keeps "+EquipmentProfile.ForSlot(demoted)+" as a copy":""));
        return true;
    }
    private void TickCopy(int s,Vector3 hand,WeaponGripMode mode,float now)
    {
        if(holsters==null)return;
        var copy=holsters.CopyOf(copyKey[s]);if(copy==null){ClearCopy(s,"its copy is gone");return;}
        var (held,down)=GripInput(s);
        if(s==0&&copyProfile[0]=="grenade"){TickLeftGrenade(copy,hand,held,down,mode,now);return;}
        // 0.1.149: a knife in this hand: thrown by letting go of its grip in a swing.
        if(copyProfile[s]=="knife"){TickCopyKnife(s,hand,held,down,mode);return;}
        hinted[s]=true;
        bool snap=holsters.Hint(copyKey[s],copyProfile[s],hand,out _,s==0);
        if(snap&&!copySnap[s])rig.PunchHaptics(s==1);copySnap[s]=snap;
        if(copyGrip[s].Step(mode,held,down)&&!KeptSnatched(s,copyGrip[s],copyProfile[s])){PutAwayCopy(s,hand);return;}
        TickCopySupport(s,copy);
        // 0.1.185: a shotgun copy hanging by its pump is pumped with that hand.
        TickCopyPump(s,copy,now);
        if(ScopeRaised(s,copy,now))return;
        // 0.1.130: its trigger fires it by itself (both hands at once); if the
        // game refuses that for this kind, the trigger brings it into play.
        var c=s==0?rig.LeftControls:rig.RightControls;
        bool tDown=c.Valid&&(c.Down&HandControls.Trigger)!=0,tHeld=c.Valid&&(c.Held&HandControls.Trigger)!=0;
        if(copyTriggerLocked[s]){if(tHeld)return;copyTriggerLocked[s]=false;}
        if(copyForeEnd[s]||!HolsterLayout.Firearm(copyProfile[s])||!(tDown||tHeld))return;
        if(!CopyFire(s,copy,tDown,tHeld,now)&&tDown)SwitchTo(s);
    }
    // 0.1.133: a scoped weapon held as a copy (the other hand's weapon in
    // play) comes into play when its scope is raised to the eye: the scope's
    // picture is drawn for the game's weapon only.
    private readonly float[] scopeSwitchAt=new float[2];
    private bool ScopeRaised(int s,HolsterCopy copy,float now)
    {
        string p=copyProfile[s];
        if(!EquipmentProfile.Scoped(p)||copyForeEnd[s]||now<scopeSwitchAt[s]||!copy.Shown||!WeaponVisual.ScopeLenses.TryGetValue(p,out var lens))return false;
        var rotation=copy.Rotation;var at=copy.Position+rotation*lens;var head=rig.HeadPosition;
        if(Vector3.Distance(at,head)>ScopeRaiseDistance||!ScopeGeometry.Viewing(ContactWorld.V(at),ContactWorld.V(head),ContactWorld.V(rotation*Vector3.forward)))return false;
        scopeSwitchAt[s]=now+2;
        if(BodyHolsters.IsLoose(copyKey[s])&&!SwapLoose(s))return false;
        Bootstrap.Write("HANDS "+p+" raised to the eye in the "+Side(s)+" hand: it comes into play (its scope)");
        SwitchTo(s);return true;
    }
    internal const float ScopeRaiseDistance=.15f;
    private void ClearCopy(int s,string? why)
    {
        if(copyKey[s]<0)return;
        if(why!=null)Bootstrap.Write("HOLSTER "+Side(s)+" hand empty ("+why+")");
        copyKey[s]=-1;copyProfile[s]="";copyForeEnd[s]=false;deferredTake[s]=-1;copySupport[s].Release();if(s==0)leftGrenadeGesture.Reset();
    }
    // 0.1.131: a weapon held as a copy with both hands: the other hand's grip
    // at its fore-end (as for the game's weapon; grip radius from the settings).
    private readonly GripMath[] copySupport={new(),new()};
    private bool CopySupported(int s)=>copyKey[s]>=0&&!copyForeEnd[s]&&copySupport[s].Held;
    internal bool SupportsCopy(bool right)=>CopySupported(right?0:1);
    private static float CopyGripRadius{get{try{return Math.Clamp(WeaponOptions.GripRadius.Value,.05f,.3f);}catch(Exception){return .18f;}}}
    // The fore-end point relative to the handle point (fitted frame, this hand's side).
    private bool CopySupportOffset(int s,HolsterCopy copy,out Vector3 handle,out Vector3 support)
    {
        var p=copyProfile[s];
        handle=handleGrips.TryGetValue(p,out var g)?g.point:copy.Grip;
        support=foreEndGrips.TryGetValue(p,out var f)?f.point:handle+new Vector3(0,-.035f,.26f);
        if(s==0){handle=LeftHandle(p,handle);support=MirrorAcross(p,support);}
        return true;
    }
    private void TickCopySupport(int s,HolsterCopy copy)
    {
        if(!HolsterLayout.Firearm(copyProfile[s])||!HandRoles.ForeEnd(copyProfile[s])||copyForeEnd[s]){copySupport[s].Release();return;}
        int o=1-s;
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)){copySupport[s].Release();return;}
        bool free=copyKey[o]<0&&GameSide(CurrentKey)!=o&&(o==1?!ReloadHandHolding(true):leftValid&&!LeftReloadHolding&&!LeftPistolVisible&&GripCarry.Current?.HidesLeft!=true)&&InteractionDriver.Current?.HandOccupied(o==1)!=true&&GameUiControls.Current?.ItemHeldOn(o==1)!=true;
        var hp=s==0?l:r;var op=o==0?l:r;
        var aim=GunAim(hp,s==1,copyProfile[s]);var hand=CameraRig.UnityPosition(hp);
        CopySupportOffset(s,copy,out var handle,out var support);
        var socket=hand+aim*(support-handle);
        var (oHeld,oDown)=GripInput(o);bool was=copySupport[s].Held;
        copySupport[s].Solve(ToN(hand),ToN(aim),ToN(CameraRig.UnityPosition(op)),ToN(socket),free,oDown,oHeld,CopyGripRadius);
        if(!copySupport[s].Held)return;
        handBusy[o]=true;
        if(!was){rig.PunchHaptics(o==1);Bootstrap.Write("HANDS "+copyProfile[s]+" in the "+Side(s)+" hand: the "+Side(o)+" hand holds its fore-end");}
    }
    // Where a hand holds its copy (at the controller, the left hand mirrored).
    private bool CopyHandPose(int s,HolsterCopy copy,out Vector3 hand,out Quaternion aim,out Vector3 point)
    {
        hand=point=Vector3.zero;aim=Quaternion.identity;
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||s==0&&!leftValid)return false;
        var pose=s==0?l:r;
        if(copyForeEnd[s])
        {
            var held=HandMirror.Apply(ToN(CameraRig.UnityPosition(pose)),ToN(CameraRig.UnityRotation(pose)),ToN(copyHoldOffset[s]),ToN(copyHoldRotation[s]));
            hand=new Vector3(held.position.X,held.position.Y,held.position.Z);aim=new Quaternion(held.rotation.X,held.rotation.Y,held.rotation.Z,held.rotation.W);point=Vector3.zero;
            hand+=CopyPumpShift(s,aim);
            return CopyCollides(s,copy,ref hand,ref aim,point);
        }
        var p=copyProfile[s];
        aim=GunAim(pose,s==1,p);hand=CameraRig.UnityPosition(pose);
        if(GrenadeDefault(p)){var rot=GloveVisual.Rotation(pose,s==1);aim=rot*GrenadeTurn(s==1);point=copy.Middle-Quaternion.Inverse(aim)*(rot*GrenadeContact(s));return true;}
        if(HolsterLayout.CenterPlaced(p)&&!KnifeHold(p)){point=copy.Grip;hand+=aim*new Vector3(0,-.02f,.045f);return true;}
        point=handleGrips.TryGetValue(p,out var g)?g.point:copy.Grip;
        if(s==0)point=LeftHandle(p,point);
        // 0.1.153: the game's own knife and
        // grenade follow the hand (the hand stays on the controller, the knife
        // turns about the wrist); the copy did the opposite - it followed the
        // controller and turned the hand sideways by the game's hold. Now the
        // same as the game's: the hand on the controller, the copy about it.
        if(KnifeHold(p)){aim=GloveVisual.Rotation(pose,s==1)*Quaternion.Inverse(s==1?g.rotation:MirrorQ(g.rotation));return true;}
        if(CopySupported(s))
        {
            int o=1-s;CopySupportOffset(s,copy,out var handle,out var support);
            var socket=hand+aim*(support-handle);
            var q=copySupport[s].Solve(ToN(hand),ToN(aim),ToN(CameraRig.UnityPosition(o==0?l:r)),ToN(socket),o==1||leftValid,false,GripInput(o).held,CopyGripRadius);
            aim=new Quaternion(q.X,q.Y,q.Z,q.W);
        }
        return CopyCollides(s,copy,ref hand,ref aim,point);
    }
    // 0.1.146: the copy in hand s stopped by the world and by the gun in the
    // other hand (ContactRig.ResolveCopy), once a frame; the hand (and the
    // other hand on its fore-end) follows the stopped copy.
    private readonly int[] copyContactFrame={-1,-1};private readonly Vector3[] copyContactShift=new Vector3[2];
    private readonly Quaternion[] copyContactTurn={Quaternion.identity,Quaternion.identity};private readonly bool[] copyContactOk={true,true};
    private bool CopyCollides(int s,HolsterCopy copy,ref Vector3 hand,ref Quaternion aim,Vector3 point)
    {
        if(!HolsterLayout.Firearm(copyProfile[s])||ContactRig.Current==null)return true;
        var root=hand-aim*point;
        if(copyContactFrame[s]!=Time.frameCount)
        {
            copyContactFrame[s]=Time.frameCount;
            var p=root;var q=aim;
            copyContactOk[s]=ContactRig.Current.ResolveCopy(s,copyProfile[s],copy.Shape,ref p,ref q);
            copyContactShift[s]=p-root;copyContactTurn[s]=q*Quaternion.Inverse(aim);
        }
        if(!copyContactOk[s])return false;
        // Turned about the copy's own origin, then moved: as resolved.
        aim=copyContactTurn[s]*aim;root+=copyContactShift[s];
        hand=root+aim*point;
        return true;
    }
    // Before drawing: the copies at their hands.
    private void RenderHandCopies()
    {
        if(holsters==null)return;
        for(int s=0;s<2;s++)
        {
            if(copyKey[s]<0){ContactRig.Current?.ResetCopy(s);continue;}var copy=holsters.CopyOf(copyKey[s]);if(copy==null){ContactRig.Current?.ResetCopy(s);continue;}
            if(!CanControl(playerId)||MountedGunVr.HidesHands||!CopyHandPose(s,copy,out var hand,out var aim,out var point)){copy.Hide();ContactRig.Current?.ResetCopy(s);continue;}
            // 0.1.132: a shot kicks it back and up about the hand; the slide cycles.
            copy.Animate(Time.realtimeSinceStartup,out float kick,out float pitch);
            if(kick>0||pitch>0){var pivot=hand;aim*=Quaternion.Euler(-pitch,0,0);copy.PoseAt(pivot-aim*Vector3.forward*kick,aim,point);continue;}
            copy.PoseAt(hand,aim,point);
        }
    }
    // The hand holding a copy wraps its handle like the game's own hold.
    private bool TryPoseCopyHand(bool right,out Vector3 position,out Quaternion rotation,out float size)
    {
        position=Vector3.zero;rotation=Quaternion.identity;size=1;int s=right?1:0;
        // 0.1.131: this hand on the fore-end of the other hand's copy.
        if(copyKey[s]<0&&SupportsCopy(right)&&holsters!=null&&CanControl(playerId))
        {
            int c=1-s;var held=holsters.CopyOf(copyKey[c]);
            if(held==null||!foreEndGrips.TryGetValue(copyProfile[c],out var fe)||!CopyHandPose(c,held,out var at,out var turn,out var handlePoint))return false;
            var root=at-turn*handlePoint;
            position=root+turn*(c==1?fe.point:MirrorAcross(copyProfile[c],fe.point));rotation=turn*(c==1?fe.rotation:MirrorQ(fe.rotation));size=fe.size;return true;
        }
        if(copyKey[s]<0||holsters==null||!HeldCopy(copyProfile[s])||!CanControl(playerId))return false;
        var copy=holsters.CopyOf(copyKey[s]);if(copy==null)return false;
        if(GrenadeDefault(copyProfile[s])){if(!CopyHandPose(s,copy,out var at,out var turn,out _))return false;position=at;rotation=turn*Quaternion.Inverse(GrenadeTurn(right));size=1;return true;}
        // 0.1.196: a club copy: the hand closed round its barrel, as held.
        if(CopyClub(s)){if(!CopyHandPose(s,copy,out var clubRoot,out var clubTurn,out _))return false;position=clubRoot+clubTurn*copyClubPoint[s];rotation=clubTurn*copyClubTurn[s];size=copyClubSize[s];return true;}
        if(copyForeEnd[s])
        {
            // By the fore-end: the left hand's own support hold (the right hand's mirrored).
            if(!foreEndGrips.TryGetValue(copyProfile[s],out var f)||!CopyHandPose(s,copy,out var root,out var turn,out _))return false;
            position=root+turn*(right?MirrorAcross(copyProfile[s],f.point):f.point);rotation=turn*(right?MirrorQ(f.rotation):f.rotation);size=f.size;return true;
        }
        if(!handleGrips.TryGetValue(copyProfile[s],out var g)||!CopyHandPose(s,copy,out var hand,out var aim,out _))return false;
        position=hand;rotation=aim*(right?g.rotation:MirrorQ(g.rotation));size=g.size;return true;
    }
    // The fingers: the left hand's hold is the right one mirrored, and the
    // right hand on a left-held weapon's fore-end is the left support hold mirrored.
    internal string? HandProfileFor(bool right)
    {
        int s=right?1:0;
        // 0.1.196: the fist closed round a gun's barrel (a club).
        if(Clubbing(right))return ClubHandProfile(right);
        // 0.1.219: a bazooka grip: the fingers closed round it.
        if(copyKey[s]<0&&BazookaGripHand(right,out string bazookaGrip))return bazookaGrip;
        // 0.1.150: a long stick in the left hand (a left-hander): the left hand
        // at its end holds it as the right hand would, mirrored; the right hand
        // further up holds it with its own hold (the left's support hold mirrored).
        if(PrimaryLeft&&copyKey[s]<0&&weapon!=null&&visual?.LongGripOrigin!=null)
        {
            if(!right)return longGripClosed[1]?LongHandleGrip:FingerPoseMath.MirrorPrefix+HandProfile;
            return longGripClosed[0]?LongHandleGrip:longSupportMirrored?HandProfile:FingerPoseMath.MirrorPrefix+HandProfile;
        }
        // 0.1.143: a stick laid in the closed hand (shovel): fingers round it.
        if(copyKey[s]<0&&weapon!=null&&visual?.LongGripOrigin!=null&&longGripClosed[s])return LongHandleGrip;
        if(copyKey[s]>=0&&HeldCopy(copyProfile[s]))return right!=copyForeEnd[s]?copyProfile[s]:FingerPoseMath.MirrorPrefix+copyProfile[s];
        if(SupportsCopy(right))return right?FingerPoseMath.MirrorPrefix+copyProfile[0]:copyProfile[1];
        if(PrimaryLeft&&weapon!=null)return FingerPoseMath.MirrorPrefix+HandProfile;
        // 0.1.142: the shovel's other hand holds its stick like the right hand, mirrored.
        if(!right&&longSupportMirrored&&visual?.LongGripOrigin!=null&&weapon!=null)return FingerPoseMath.MirrorPrefix+HandProfile;
        return null;
    }
    // Rounds for the watch: right / left.
    internal bool JuggleAmmo(string gameMagazine,string gameReserve,out string magazine,out string reserve)
    {
        magazine=gameMagazine;reserve=gameReserve;
        bool left=PrimaryLeft;
        if(copyKey[0]<0&&copyKey[1]<0&&!left)return false;
        var m=new string[2];var r=new string[2];
        for(int s=0;s<2;s++)
        {
            m[s]=r[s]="-";
            if(weapon!=null&&HasTrackedWeapon&&(left?0:1)==s){m[s]=gameMagazine;r[s]=gameReserve;continue;}
            if(copyKey[s]<0||!HolsterLayout.Firearm(copyProfile[s])||holsters==null)continue;
            bool loose=BodyHolsters.IsLoose(copyKey[s]);
            var w=holsters.WeaponOf(copyKey[s]);if(w==null)continue;
            try
            {
                var a=w.GetComponent(Il2CppInterop.Runtime.Il2CppType.Of<AmmoManagementComponent>())?.TryCast<AmmoManagementComponent>()
                    ??w.GetComponentInChildren(Il2CppInterop.Runtime.Il2CppType.Of<AmmoManagementComponent>(),true)?.TryCast<AmmoManagementComponent>();
                if(a==null)continue;
                m[s]=(loose?holsters.LooseMagazine(copyKey[s]):a.PrimaryMagazineAmmoCount).ToString();r[s]=a.ammoPool!=null&&a.ammoPool.IsInfinite?"∞":a.PrimaryReserveAmmoCount.ToString();
            }
            catch(Exception){}
        }
        // 0.1.146: a left-hander's watch (on the left wrist) lists the left hand first.
        if(LeftHanded){magazine=m[0]+"/"+m[1];reserve=r[0]+"/"+r[1];}
        else{magazine=m[1]+"/"+m[0];reserve=r[1]+"/"+r[0];}
        return true;
    }
    // 0.1.128: the gun in the left hand reloads when its magazine is empty
    // (0.1.131: by itself, see StartHandReload).
    private void LeftHandReload()
    {
        try{if(ammo==null||ammo.PrimaryMagazineAmmoCount>0)return;}catch(Exception){return;}
        StartHandReload(0);
    }
    // ---- A grenade in the left hand ----
    private readonly GrenadeThrow leftGrenadeGesture=new();
    private bool leftThrowPending;private Vector3 leftThrowOrigin,leftThrowVelocity;private Quaternion leftThrowRotation;
    // 0.1.149: the throw waiting for the game's weapon is a knife's (from hand leftThrowSide).
    private bool leftThrowKnife;private int leftThrowSide;
    private float leftThrowAt=-10,leftRestoreAt=-1;private int leftRestoreKey=-1;private float leftThrowSpeed;private Vector3 leftThrowSpin;
    private bool LeftThrowBusy=>leftThrowPending||leftRestoreAt>0;
    private string leftWaitWhy="";private bool leftThrowOnBody;private Vector3 leftThrowLocal,leftThrowLocalVelocity;private Quaternion leftThrowLocalRotation=Quaternion.identity;
    private void TickLeftGrenade(HolsterCopy copy,Vector3 hand,bool held,bool down,WeaponGripMode mode,float now)
    {
        var center=copy.CenterWorld;
        if(rig.SampleWorldHands(out _,out var r,out _))
        {
            var fingers=CameraRig.UnityPosition(r)+GloveVisual.Rotation(r,true)*new Vector3(0,-.02f,.06f);
            RightAtLeftGrenade=!leftGrenadeGesture.PinPulled&&Vector3.Distance(fingers,center)<GrenadeThrow.PinReach;
            if(leftGrenadeGesture.PullPin(rig.RightControls.Valid&&(rig.RightControls.Down&HandControls.Trigger)!=0,ToN(fingers),ToN(center)))
            {
                rig.PunchHaptics(true);rig.PunchHaptics(false);rig.DisarmTrigger();RightAtLeftGrenade=false;
                // 0.1.129: the pin comes out into the right fingers; its click.
                try
                {
                    if(copy.TakePinMesh(out var pinMesh,out var mats,out var pinWorld,out var pinCenter)&&pinMesh!=null)
                    {pins??=new GrenadePins();pins.Add(pinMesh,mats,pinWorld,pinCenter,Matrix4x4.TRS(CameraRig.UnityPosition(r),GloveVisual.Rotation(r,true),Vector3.one),true);}
                }
                catch(Exception ex){Bootstrap.Warn("GRENADE (left hand) pin visual: "+ex.Message);}
                copy.PinPulled=true;
                try{PinClick(holsters?.WeaponOf(copyKey[0])?.GetComponent(Il2CppInterop.Runtime.Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>(),center);}catch(Exception){}
                Bootstrap.Write("GRENADE (left hand) pin pulled; swing and let go of the left grip");
            }
        }
        if(!rig.SampleLeftRelative(out var local,out var toWorld))return;
        var result=leftGrenadeGesture.Sample(now,held,ToN(local));
        if(!result.Throw&&leftGrenadeGesture.PinPulled&&held)AimGrenadeLanding(0,leftGrenadeGesture,center,toWorld,holsters?.WeaponOf(copyKey[0]));
        if(result.Throw){StartLeftThrow(center,toWorld*new Vector3(result.Velocity.X,result.Velocity.Y,result.Velocity.Z),copy.Rotation,result.HandSpeed);return;}
        if(leftGrenadeGesture.PinPulled){copyGrip[0].Reset();return;}
        if(copyGrip[0].Step(mode,held,down))PutAwayCopy(0,hand);
    }
    // The game throws only its selected grenade: it is selected (instantly)
    // for the launch from where the left hand let go, then the right hand's
    // weapon comes back.
    private void StartLeftThrow(Vector3 origin,Vector3 velocity,Quaternion rotation,float handSpeed)
    {
        if(inventory==null||holsters==null)return;
        holsters.CopyOf(copyKey[0])?.Hide();
        var current=inventory.currentEquipable;
        leftRestoreKey=current!=null?(int)current.slot:(int)Slot.Fist;
        int key=copyKey[0];copyKey[0]=-1;copyProfile[0]="";holsters.Return(key,"grenade");
        velocity=AimedThrow(velocity);leftWaitWhy="";
        leftThrowOrigin=origin;leftThrowVelocity=velocity;leftThrowRotation=rotation;leftThrowSpeed=handSpeed;leftThrowAt=Time.realtimeSinceStartup;leftThrowSpin=ThrowSpin(0);
        // 0.1.159: kept
        // on the body; the game's grenade comes a moment later, and the throw
        // still leaves from where the hand let go of it, the way it went
        // (the player may have walked or turned meanwhile).
        var body=rig.PlayerRoot;leftThrowOnBody=body!=null;
        if(body!=null){leftThrowLocal=body.InverseTransformPoint(origin);leftThrowLocalVelocity=body.InverseTransformDirection(velocity);leftThrowLocalRotation=Quaternion.Inverse(body.rotation)*rotation;}
        bool ok;
        try{ok=current!=null&&current.slot==Slot.Grenade||inventory.TrySelectSlot(Slot.Grenade,true,true,true,false,true);}
        catch(Exception ex){ok=false;Bootstrap.Warn("GRENADE (left hand) select: "+ex.Message);}
        if(!ok){Bootstrap.Write("GRENADE (left hand) the game refused its grenade; not thrown");return;}
        leftThrowPending=true;grenadeHiddenUntil=Time.realtimeSinceStartup+1.5f;
        Bootstrap.Write("GRENADE (left hand) let go: handSpeed="+handSpeed.ToString("F2")+" speed="+velocity.magnitude.ToString("F2"));
    }
    // From TickGrenade once the game's grenade is bound.
    private void TickLeftThrow()
    {
        float now=Time.realtimeSinceStartup;
        if(leftThrowPending)
        {
            if(now-leftThrowAt>1.5f){leftThrowPending=false;leftRestoreAt=now;Bootstrap.Warn((leftThrowKnife?"KNIFE":"GRENADE")+" ("+Side(leftThrowKnife?leftThrowSide:0)+" hand) the game's "+(leftThrowKnife?"knife":"grenade")+" did not "+(leftThrowKnife&&knifeLaunchedAt<leftThrowAt&&profile=="knife"?"leave":"come")+"; not thrown"+(leftWaitWhy.Length>0?" (waited: "+leftWaitWhy+")":""));leftThrowKnife=false;return;}
            if(leftThrowKnife){TickCopyKnifeThrow(now);return;}
            if(profile!="grenade"||grenadeThrow==null||inventory==null||inventory.isInTransit){leftWaitWhy=profile!="grenade"?"the game's grenade not in its hand yet ("+profile+")":grenadeThrow==null?"its throw not bound":"the game still drawing its grenade";return;}
            bool can=false;try{can=grenadeThrow.CanStart();}catch(Exception){}
            if(!can){leftWaitWhy="the game's throw not ready";return;}
            try{grenadeThrow.pinPulled=true;}catch(Exception){}
            var origin=leftThrowOrigin;var velocity=leftThrowVelocity;var rotation=leftThrowRotation;var body=rig.PlayerRoot;
            if(leftThrowOnBody&&body!=null){origin=body.TransformPoint(leftThrowLocal);velocity=body.TransformDirection(leftThrowLocalVelocity);rotation=body.rotation*leftThrowLocalRotation;}
            throwOrigin=origin;throwLaunch=LivelyThrow(velocity,leftThrowSpeed);throwRotation=rotation;throwSpin=leftThrowSpin;
            grenadeAmmo=ammo;grenadeCountBefore=GrenadeCount();grenadeCheckAt=now+1f;grenadeHiddenUntil=now+.6f;
            propThrow=grenadeThrow;lastThrown=grenadeThrow;lastThrownAt=now;
            LaunchProp("left grenade handSpeed="+leftThrowSpeed.ToString("F2")+" speed="+throwLaunch.magnitude.ToString("F2")+", "+(now-leftThrowAt).ToString("F2")+" s after the grip was let go"+(leftWaitWhy.Length>0?" (waited: "+leftWaitWhy+")":"")+", from where it was let go"+(leftThrowOnBody?" (kept on the body)":""),false);
            SettleAfterLaunch(grenadeThrow,"left hand");
            leftThrowPending=false;leftRestoreAt=now+.4f;
            return;
        }
        if(leftRestoreAt>0&&now>=leftRestoreAt)
        {
            leftRestoreAt=-1;
            if(inventory==null||leftRestoreKey<0||leftRestoreKey==(int)Slot.Grenade)return;
            if(settleThrow!=null){var t=settleThrow;settleThrow=null;SettleThrown(t,settleWho);}
            try{inventory.TrySelectSlot((Slot)leftRestoreKey,true,true,true,false,true);}
            catch(Exception ex){Bootstrap.Warn("GRENADE (left hand) restore: "+ex.Message);}
            Bootstrap.Write("GRENADE (left hand) thrown; right hand back to "+EquipmentProfile.ForSlot(leftRestoreKey));
        }
    }
}
