using System;
using PlayMagic.Weapons;
using UnityEngine;
using Slot=PlayerEquipableInventory.ActiveEquipmentSlot;
namespace XiiiXR;
// 0.1.124: weapons held by the grip (VR SETTINGS "Weapon in hand": hold
// grip / toggle with grip / always). Let go at a body place: it hangs there;
// let go elsewhere: it drops to the floor (still in the wheel). An empty hand
// takes a weapon from its body place or from the floor with the grip (0.1.128:
// either hand, see WeaponHands.Hands). The game's own weapon is switched
// instantly (no draw animation); with empty hands the game holds the fists.
internal sealed partial class WeaponHands
{
    private BodyHolsters? holsters;
    private readonly WeaponGripState gripState=new();
    private int gripBoundId;private float gripTakeAt=-10;private bool nearHolster;
    private float nextHolsterReport;
    // Put away just now: the game may take a few frames to switch to the
    // fists; meanwhile the weapon is neither drawn in the hand nor fired.
    private int putAwayKey=-1;private float putAwayAt=-10;
    internal bool HolsterHidesCurrent=>weapon!=null&&putAwayKey==(int)weapon.slot&&Time.realtimeSinceStartup-putAwayAt<1.5f;
    // 0.1.133: a weapon just let go is still the game's current one for a few
    // frames; it is no longer in a hand (another hand taking it, or taking
    // another weapon, must not see it there).
    private bool Leaving(int key)=>key>=0&&key==putAwayKey&&Time.realtimeSinceStartup-putAwayAt<1.5f;
    private int GameKey{get{int k=CurrentKey;return Leaving(k)?-1:k;}}
    private float autoPromoteAt;
    internal static WeaponGripMode GripMode=>(WeaponGripMode)Math.Clamp(QualityOptions.WeaponGrip?.Value??0,0,2);
    internal static bool LeftHanded=>QualityOptions.LeftHanded?.Value==true;
    // 0.1.146: a gun the
    // game brings into play (the wheel, a pickup) comes to the left hand -
    // unless the right hand took it itself, or the left hand is busy (a gun
    // copy, the grappling hook, an item, a door or a body in it).
    private int rightChosenKey=-1;private float rightChosenAt;
    private void RightHandHas(int key){if(leftGameKey==key)leftGameKey=-1;rightChosenKey=key;rightChosenAt=Time.realtimeSinceStartup;}
    // 0.1.151: the thing comes to the hand whose grip took it.
    private static int thingSide=-1;private static float thingSideAt=-10;
    internal static void ThingTakenBy(int side){thingSide=side;thingSideAt=Time.realtimeSinceStartup;}
    private bool ThingToPressingHand(int currentKey,bool leaving,float now)
    {
        if(thingSide<0)return false;
        if(now-thingSideAt>4){thingSide=-1;return false;}
        if(currentKey!=(int)Slot.Enviromental||leaving||DualActive||inventory==null||inventory.isInTransit)return false;
        int side=thingSide;thingSide=-1;
        if(side==0)
        {
            if(copyKey[0]>=0||foreEndOnly||GameUiControls.Current?.LeftItemHeld==true||GrappleVr.Current?.DeviceShown==true&&GrappleVr.Current.Side==0||GripCarry.Current?.HidesLeft==true)return false;
            if(leftGameKey!=currentKey){leftGameKey=currentKey;leftGameSince=now;leftTriggerLocked=true;}
        }
        else RightHandHas(currentKey);
        Bootstrap.Write("HANDS "+(inventory.currentEquipable?.identifier??"the thing")+" is held in the "+Side(side)+" hand (its grip took it)"+(side==0?"; the right hand's hold mirrored":""));
        return true;
    }
    private void LeftHandedDraw(int currentKey,bool leaving,float now)
    {
        if(rightChosenKey>=0&&currentKey!=rightChosenKey&&now-rightChosenAt>3)rightChosenKey=-1;
        if(ThingToPressingHand(currentKey,leaving,now))return;
        if(!LeftHanded||leaving||currentKey<0||currentKey==leftGameKey||currentKey==rightChosenKey||DualActive||inventory==null||inventory.isInTransit)return;
        string p=EquipmentProfile.ForSlot(currentKey);
        // 0.1.150: the
        // game's things in the hand (bottles, chairs, the ashtray, a broom...) too.
        bool thing=currentKey==(int)Slot.Enviromental;
        if(!HolsterLayout.Firearm(p)&&!thing||copyKey[0]>=0||foreEndOnly)return;
        if(GameUiControls.Current?.LeftItemHeld==true||GrappleVr.Current?.DeviceShown==true&&GrappleVr.Current.Side==0||GripCarry.Current?.HidesLeft==true||InteractionDriver.Current?.HandOccupied(false)==true)return;
        leftGameKey=currentKey;leftGameSince=now;leftTriggerLocked=true;
        Bootstrap.Write(thing?"HANDS left-handed: "+(inventory.currentEquipable?.identifier??"the thing")+" is held in the left hand (the right hand's hold mirrored)"
            :"HANDS left-handed: "+p+" comes to the left hand (the left trigger fires it, the right hand reloads it)");
    }
    // The empty right hand is at a body weapon (the grip there takes it, not a
    // door, prop or pickup).
    internal bool RightNearHolster=>nearHolster&&MainSide==1;
    // 0.1.150: the hand that takes things (the left one for a left-hander) is at a body weapon.
    internal bool MainNearHolster=>nearHolster;
    internal static int MainSide=>LeftHanded?0:1;
    private WeaponPointing? pointing;
    internal bool PointedWeapon=>pointing!=null&&pointing.Target!=WeaponPointing.Kind.None;
    // 0.1.171: this hand points at (or is at) a weapon, or did a moment ago: its grip press is for that weapon.
    // 0.1.179: the layers the player's shots stop at (walls, doors): what a weapon is not taken through.
    internal int ShotMask{get{try{int m=handler!=null?handler.raycastCollisionMask:0;return m!=0?m:~0;}catch(Exception){return ~0;}}}
    internal bool PointedWeaponBy(bool right)=>pointing!=null&&pointing.Aims(right?1:0,Time.realtimeSinceStartup);
    // 0.1.186: hand s may take a medkit from the other forearm: free for a
    // weapon (nothing in it, not along the other hand's weapon, no door, prop
    // or body), not at a weapon on the body and not pointing at one (the
    // grip there is for that weapon).
    internal bool HandTakesItem(int s,Vector3 hand)
    {
        if(!HandFreeForWeapon(s,hand,true,CurrentKey))return false;
        if(GripMode!=WeaponGripMode.Always&&holsters!=null&&holsters.TryGrab(hand,out _,out _))return false;
        return !PointedWeaponBy(s==1);
    }
    private bool TakePending=>weapon==null&&Time.realtimeSinceStartup-gripTakeAt<.6f;
    // Wheel choice confirmed with the grip: the weapon is held by it.
    internal void TakenByGrip(){gripTakeAt=Time.realtimeSinceStartup;}
    private static bool Releasable(string p)=>HolsterLayout.Firearm(p)||p is "knife" or "grenade";
    internal static Transform? FindMuzzle(Equipable e,string profile)
    {
        var script=e.primaryFireUsageComponentScript;var candidate=script==null?null:script.TryCast<FireComponent>();
        if(candidate==null||candidate.TryCast<DualWieldComponent>()!=null||candidate.projectileOrigin==null)
        {
            foreach(var c in e.GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<FireComponent>(),true))
            {var f=c.TryCast<FireComponent>();if(f!=null&&f.TryCast<DualWieldComponent>()==null&&f.projectileOrigin!=null){candidate=f;break;}}
        }
        var origin=candidate?.projectileOrigin;
        if(origin==null&&EquipmentProfile.RequiresMuzzle(profile))return null;
        return origin??e.transform;
    }
    // 0.1.165: which part of a long holster step took the time (PERF SLOW holsters):
    // taking a gun from the floor stopped the game for 30-50 ms in the log.
    private readonly StepClock holsterClock=new("holsters");private float nextMissReport;
    // The weapon in a hand now (not one being let go; one being drawn again).
    private int HandKeyNow()
    {
        int k=CurrentKey;float now=Time.realtimeSinceStartup;
        bool leaving=k==putAwayKey&&now-putAwayAt<1.5f||LeftThrowBusy||Redrawing;
        return Redrawing?redrawSlot:leaving?-1:k;
    }
    private void TickHolsters(){var clock=holsterClock;clock.Begin();try{TickHolstersCore(clock);}finally{clock.End();}}
    private void TickHolstersCore(StepClock clock)
    {
        var mode=GripMode;
        if(mode==WeaponGripMode.Always&&holsters!=null&&(copyKey[0]>=0||copyKey[1]>=0||leftGameKey>=0||foreEndOnly))
        {
            for(int s=0;s<2;s++)if(copyKey[s]>=0){holsters.Return(copyKey[s],copyProfile[s]);ClearCopy(s,"the old way: always in the hand");}
            leftGameKey=-1;foreEndOnly=false;
        }
        if(mode==WeaponGripMode.Always||inventory==null||playerRoot==null||!CanControl(playerId)||!rig.SampleWorldHands(out var l,out var r,out bool leftValid))
        {
            // 0.1.185: while the player cannot play (the mission's opening, a
            // cutscene, a menu) the weapons owned are made ready for the hands.
            if(mode!=WeaponGripMode.Always&&inventory!=null&&playerRoot!=null&&!disposed)
            {
                holsters??=new BodyHolsters();
                try{holsters.Sync(inventory,HandKeyNow(),copyKey[0],copyKey[1]);holsters.BuildMissing(true);}
                catch(Exception ex){if(Time.realtimeSinceStartup>=nextHolsterReport){nextHolsterReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("HOLSTER inventory: "+ex.Message);}}
            }
            holsters?.HideAll();pointing?.Hide();nearHolster=false;RightAtLeftGrenade=false;return;
        }
        holsters??=new BodyHolsters();WorldStats.Holsters=holsters.Count;
        float now=Time.realtimeSinceStartup;
        var hands=handPos;hands[0]=CameraRig.UnityPosition(l);hands[1]=CameraRig.UnityPosition(r);
        for(int s=0;s<2;s++)
        {
            float dt=now-handAtTime[s];bool ok=(s==1||leftValid)&&dt>.0001f&&dt<.1f;
            handVelocity[s]=ok?Vector3.ClampMagnitude((hands[s]-handAt[s])/dt,HolsterCopy.MaxThrowSpeed):Vector3.zero;
            var turn=CameraRig.UnityRotation(s==0?l:r);
            var w=ok?ThrowMath.Spin(ContactWorld.V(turn*Vector3.forward),ContactWorld.V(turn*Vector3.up),ContactWorld.V(handTurnAt[s]*Vector3.forward),ContactWorld.V(handTurnAt[s]*Vector3.up),dt):System.Numerics.Vector3.Zero;
            // Smoothed over a few frames (tracking jitter).
            handSpin[s]=ok?Vector3.Lerp(handSpin[s],Vector3.ClampMagnitude(ContactWorld.U(w),HolsterCopy.MaxSpin),.5f):Vector3.zero;
            handAt[s]=hands[s];handAtTime[s]=now;handTurnAt[s]=turn;
        }
        RightAtLeftGrenade=false;
        var current=inventory.currentEquipable;int currentKey=current!=null?(int)current.slot:-1;
        // The left hand's game weapon was replaced some other way (the wheel, the game).
        // 0.1.138: not while the gun is being drawn again (after a bolt went
        // in, or a stall): the game holds the fists for a moment, the gun
        // stays in the left hand (it came back in the right one and fell).
        if(leftGameKey>=0&&currentKey!=leftGameKey&&redrawStage==0&&now-leftGameSince>3)leftGameKey=-1;
        bool leaving=currentKey==putAwayKey&&now-putAwayAt<1.5f||LeftThrowBusy||Redrawing;
        LeftHandedDraw(currentKey,leaving,now);
        // Being drawn again: still the hand's weapon (no body copy, no build).
        int handKey=Redrawing?redrawSlot:leaving?-1:currentKey;
        clock.Mark("start");
        try{holsters.Sync(inventory,handKey,copyKey[0],copyKey[1]);clock.Mark("sync");holsters.BuildMissing();clock.Mark("build copies");}
        catch(Exception ex){if(now>=nextHolsterReport){nextHolsterReport=now+10;Bootstrap.Warn("HOLSTER inventory: "+ex.Message);}}
        for(int s=0;s<2;s++)if(copyKey[s]>=0&&!(s==0&&LeftThrowBusy)&&!holsters.Has(copyKey[s]))ClearCopy(s,"no longer owned");
        // 0.1.128: a hand's copy that the game switched to is held as the game's weapon.
        PromoteCopies();clock.Mark("promote");
        TickPickupTake(now);clock.Mark("pickup take");
        // 0.1.145: a snatched gun that never reached the hand keeps nothing.
        for(int s=0;s<2;s++)if(snatchKeep[s]&&now-snatchKeepAt[s]>3&&copyKey[s]<0&&GameSide(currentKey)!=s&&pickupTakeKey<0)snatchKeep[s]=false;
        // 0.1.130: a take while the game was switching weapons: switched now.
        for(int s=0;s<2;s++)
        {
            if(deferredTake[s]<0)continue;
            if(copyKey[s]!=deferredTake[s]||now-deferredAt[s]>4){deferredTake[s]=-1;continue;}
            if(inventory.isInTransit||GameSide(currentKey)>=0&&currentKey!=putAwayKey)continue;
            // 0.1.133: the game has not switched away from it yet.
            if(currentKey==deferredTake[s]&&Leaving(currentKey))continue;
            int key=deferredTake[s];deferredTake[s]=-1;bool ok;
            try{ok=inventory.HasEquipableInSlot((PlayerEquipableInventory.ActiveEquipmentSlot)key)&&inventory.TrySelectSlot((PlayerEquipableInventory.ActiveEquipmentSlot)key,true,true,true,false,true);}
            catch(Exception ex){ok=false;Bootstrap.Warn("HOLSTER deferred take: "+ex.Message);}
            if(!ok)continue;
            if(s==0){leftGameKey=key;leftGameSince=now;}else RightHandHas(key);
            gripTakeAt=now;putAwayKey=-1;Bootstrap.Write("HOLSTER "+Side(s)+" hand: "+EquipmentProfile.ForSlot(key)+" comes into play now (caught while the game was switching)");
        }
        // 0.1.133: no weapon of the game's in the hands: a hand's copy comes
        // into play (its scope, its hand hold, its reload) - it was left a copy
        // when it was taken while the other hand's weapon was being let go.
        // 0.1.134: also another weapon of an owned kind (it becomes the owned one).
        if(!leaving&&!inventory.isInTransit&&GameSide(currentKey)<0&&!LeftThrowBusy&&!Redrawing&&now>=autoPromoteAt)
            for(int s=1;s>=0;s--)
            {
                if(copyKey[s]<0||deferredTake[s]>=0||copyForeEnd[s]||!HolsterLayout.Firearm(copyProfile[s]))continue;
                autoPromoteAt=now+1;
                if(BodyHolsters.IsLoose(copyKey[s])&&!SwapLoose(s)){autoPromoteAt=now+3;continue;}
                Bootstrap.Write("HANDS no game weapon in the hands: "+copyProfile[s]+" in the "+Side(s)+" hand comes into play");
                if(!SwitchTo(s))autoPromoteAt=now+5;
                break;
            }
        // A new game weapon in the hand: held by the grip if the grip took it.
        if(weapon!=null&&weapon.GetInstanceID()!=gripBoundId)
        {
            gripBoundId=weapon.GetInstanceID();int side=PrimaryLeft?0:1;
            bool byGrip=now-gripTakeAt<2.5f&&GripInput(side).held;gripState.Took(byGrip);gripTakeAt=-10;foreEndOnly=false;ClearClub();
            if(side==0)leftTriggerLocked=true;
            if(Releasable(profile)&&!LeftThrowBusy)Bootstrap.Write("HOLSTER in the "+Side(side)+" hand "+profile+(byGrip?" (held by the grip)":" (grip to hold, let go to put away)")+" mode="+mode+" body: "+holsters.Describe());
        }
        if(weapon==null){gripBoundId=0;foreEndOnly=false;barrelHand=-1;}
        hinted[0]=hinted[1]=false;
        var busy=handBusy;busy[0]=busy[1]=false;
        // 0.1.141: a hand holding an enemy's gun takes nothing else.
        for(int s=0;s<2;s++)if(NpcHitReactions.Current?.Grabbing(s==1)==true)busy[s]=true;
        int gameSide=GameSide(currentKey);
        // 0.1.130: the hand that just let go may still catch it (floor/air only).
        int catching=-1;
        // 0.1.214: the next knife at once after a throw (KnifeRetake).
        if(!leaving&&gameSide>=0)KnifeRetake(gameSide,hands[gameSide]);
        if(leaving){int side=LeftThrowBusy?0:putAwaySide;busy[side]=true;if(!LeftThrowBusy&&!Redrawing)catching=side;}
        else if(weapon!=null&&gameSide>=0&&Releasable(profile)&&!inventory.isInTransit)TickGameWeapon(gameSide,hands,l,r,mode,busy);
        else if(gameSide>=0)busy[gameSide]=true;
        clock.Mark("game weapon");
        // Copies in the hands.
        for(int s=0;s<2;s++)if(copyKey[s]>=0&&(s==1||leftValid)){busy[s]=true;TickCopy(s,hands[s],mode,now);}
        clock.Mark("copies");
        // Empty hands take weapons from the body, the floor or the other hand.
        bool wasNear=nearHolster;nearHolster=false;bool took=false;
        for(int s=1;s>=0;s--)
        {
            if(putAwayFrame==Time.frameCount)break;   // the press that let go does not take again
            bool catchOnly=busy[s]&&catching==s;
            if(busy[s]&&!catchOnly||!HandFreeForWeapon(s,hands[s],s==1||leftValid,currentKey))continue;
            int o=1-s;var other=copyKey[o]>=0?holsters.CopyOf(copyKey[o]):null;
            // 0.1.131: a long gun is passed hand to hand at its handle (the
            // other hand); along its length the grip holds its fore-end.
            bool atOther=!catchOnly&&other!=null&&HolsterLayout.Firearm(copyProfile[o])
                &&(HandRoles.ForeEnd(copyProfile[o])&&!copyForeEnd[o]?Vector3.Distance(hands[s],hands[o])<HolsterLayout.GrabRadius:other.Distance(hands[s])<HolsterLayout.GrabRadius);
            int key=-1;bool floor=false;
            bool near=atOther||holsters.TryGrab(hands[s],out key,out floor);
            if(catchOnly&&!floor)near=false;
            if(s==MainSide){nearHolster=near;if(near&&!wasNear)rig.PunchHaptics(s==1);}
            // 0.1.169: a grip press near a body place that took nothing: how far off it was.
            if(!near&&GripInput(s).down&&Time.realtimeSinceStartup>=nextMissReport&&holsters.NearestShown(hands[s],out var missed,out float off)&&off<.35f)
            {nextMissReport=Time.realtimeSinceStartup+2;Bootstrap.Write("HOLSTER "+Side(s)+" grip took nothing: "+missed+" "+off.ToString("F2")+" m away (takes within "+HolsterLayout.GrabRadiusOf(missed).ToString("F2")+" m)");}
            if(!near||!GripInput(s).down)continue;
            pointing?.Hide();took=true;busy[s]=true;if(s==MainSide)nearHolster=false;
            if(atOther)HandOver(o,s);else TakeWeapon(key,s,floor?"the floor":holsters.PlaceOf(key).ToString());
            break;
        }
        for(int s=0;s<2;s++)if(!hinted[s])holsters.HideHint(s==0);
        clock.Mark(took?"take":"grab");
        if(took)return;
        // 0.1.125: a weapon lying in the world, pointed at with either hand.
        bool rightFree=!busy[1]&&!(nearHolster&&MainSide==1)&&HandFreeForWeapon(1,hands[1],true,currentKey);
        bool leftFree=!busy[0]&&!(nearHolster&&MainSide==0)&&leftValid&&HandFreeForWeapon(0,hands[0],true,currentKey);
        if(!rightFree&&!leftFree){pointing?.Hide();return;}
        pointing??=new WeaponPointing();
        try
        {
            pointing.Scan(rig.HeadPosition,rig.PlayerRoot,ShotMask);clock.Mark("point search");
            bool rightPress=rightFree&&GripInput(1).down,leftPress=leftFree&&GripInput(0).down;
            int ticks=pointing.Tick(holsters,rightFree?r:null,leftFree?l:null,QualityOptions.Hints.Value,rightPress,leftPress);clock.Mark("point outline");
            // 0.1.171: a short tick in the hand that comes onto a weapon (with or without the outline).
            for(int s=1;s>=0;s--)if((ticks&(1<<s))!=0)rig.ResistanceHaptics(.55f,s==1);
            // Each hand's press takes that hand's weapon (right first).
            for(int s=1;s>=0;s--)
            {
                if(!(s==1?rightPress:leftPress))continue;
                if(pointing.Takeable(s,holsters,out var pick)){TakePointed(s,pick);clock.Mark("take pointed");break;}
                if(now>=nextPointMiss){string miss=pointing.Miss(s);if(miss.Length>0){nextPointMiss=now+2;Bootstrap.Write("POINT GRAB "+Side(s)+" grip took nothing: nearest "+miss);}}
            }
        }
        catch(Exception ex){pointing.Hide();if(now>=nextHolsterReport){nextHolsterReport=now+10;Bootstrap.Warn("POINT GRAB: "+ex.Message);}}
    }
    private float nextPointMiss;
    private void TakePointed(int s,WeaponPointing.Pick pick)
    {
        var p=pointing;if(p==null||inventory==null)return;
        if(pick.Kind==WeaponPointing.Kind.Copy)
        {
            int key=pick.Key;p.Clear();Bootstrap.Write("POINT GRAB "+Side(s)+" hand takes "+(holsters?.ProfileOf(key)??EquipmentProfile.ForSlot(key))+" from the floor ("+pick.How+")");
            TakeWeapon(key,s,"the floor ("+pick.How+")");
            return;
        }
        var pickup=pick.Pickup;if(pickup==null)return;
        Bootstrap.Write("POINT GRAB "+Side(s)+" hand takes "+pickup.name+" ("+pick.How+")");
        if(PickUpInto(pickup,s)){p.Forget(pickup);p.Clear();}
    }
    // A game weapon pickup into hand s (pointed at, or an enemy's gun that
    // hand made it let go of, 0.1.141); false when the game refused it.
    internal bool PickUpInto(WeaponPickup pickup,int s)
    {
        if(inventory==null||holsters==null)return false;bool left=s==0;
        var actor=InteractionDriver.Current?.Actor;
        if(actor==null){Bootstrap.Write("POINT GRAB no interaction actor yet");return false;}
        int slot=-1;try{var e=pickup.weaponPrefab;if(e!=null)slot=(int)e.slot;}catch(Exception){}
        // 0.1.130: another weapon of a kind already owned: taken as another
        // weapon (its rounds in its magazine); the owned one stays where it is.
        if(slot>=0&&TakeAnother(pickup,slot,s))return true;
        // 0.1.128: the new weapon comes to the pointing hand as the game's
        // weapon; the other hand's weapon stays there as a copy (0.1.130: also
        // one hanging by its fore-end in the other hand).
        int current=GameKey,demoted=-1,demotedSide=1-s;
        if((GameSide(current)==1-s||foreEndOnly&&GameSide(current)==s)&&HolsterLayout.Firearm(EquipmentProfile.ForSlot(current))&&DemoteGame())demoted=current;
        int oldLeft=leftGameKey;float oldSince=leftGameSince;
        if(s==0&&slot>=0){leftGameKey=slot;leftGameSince=Time.realtimeSinceStartup;}else if(s==1&&slot>=0)RightHandHas(slot);
        bool ok=false;
        deliberatePickup=true;
        try{ok=pickup.TryPickupItem(actor);}catch(Exception ex){Bootstrap.Warn("POINT GRAB pickup: "+ex.Message);}
        finally{deliberatePickup=false;}
        Bootstrap.Write("POINT GRAB "+Side(s)+" hand picked up "+pickup.name+" ok="+ok+(demoted>=0?" (the "+Side(1-s)+" hand keeps "+EquipmentProfile.ForSlot(demoted)+" as a copy)":""));
        if(!ok){if(demoted>=0)UndoDemote(demotedSide,demoted);leftGameKey=oldLeft;leftGameSince=oldSince;return false;}
        gripTakeAt=Time.realtimeSinceStartup;rig.PunchHaptics(!left);
        // 0.1.140: it goes to this hand (the game only puts it in the
        // inventory; a free body place took it first).
        if(slot>=0&&HolsterLayout.Firearm(EquipmentProfile.ForSlot(slot))){pickupTakeKey=slot;pickupTakeSide=s;pickupTakeAt=Time.realtimeSinceStartup;holsters.Rush(slot);}
        return true;
    }
    private int pickupTakeKey=-1,pickupTakeSide;private float pickupTakeAt;
    // 0.1.145: a gun taken from an enemy's hands stays in the
    // hand when that grip lets go; the next press and release puts it away as usual.
    private readonly bool[] snatchKeep=new bool[2];private readonly float[] snatchKeepAt=new float[2];
    internal void KeepSnatched(int s){snatchKeep[s]=true;snatchKeepAt[s]=Time.realtimeSinceStartup;}
    private bool KeptSnatched(int s,WeaponGripState state,string p)
    {
        if(!snatchKeep[s])return false;
        snatchKeep[s]=false;state.Took(false);
        Bootstrap.Write("HANDS "+Side(s)+" hand keeps the snatched "+p+" (the grip that took it let go; press and let go of the grip to put it away)");
        return true;
    }
    // Once the picked-up weapon is owned (its copy built): into the hand that took it.
    private void TickPickupTake(float now)
    {
        if(pickupTakeKey<0||inventory==null||holsters==null)return;
        int key=pickupTakeKey,s=pickupTakeSide;string p=EquipmentProfile.ForSlot(key);
        if(now-pickupTakeAt>2.5f){pickupTakeKey=-1;Bootstrap.Write("HOLSTER picked-up "+p+" did not reach the "+Side(s)+" hand (not owned in time)");return;}
        if(inventory.isInTransit||!holsters.Has(key)||holsters.CopyOf(key)==null)return;
        pickupTakeKey=-1;
        if(CurrentKey==key){Bootstrap.Write("HOLSTER picked-up "+p+": the game drew it itself");return;}
        if(copyKey[s]>=0||GameSide(CurrentKey)==s&&!Leaving(CurrentKey)){Bootstrap.Write("HOLSTER picked-up "+p+" stays on the body: the "+Side(s)+" hand is holding something");return;}
        TakeWeapon(key,s,"the ground (picked up)");
        Bootstrap.Write("HOLSTER picked-up "+p+" comes to the "+Side(s)+" hand first (the body place only when you hang it there)");
    }
    // 0.1.130: a ground weapon of an owned kind becomes another weapon in the
    // hand (a copy of the owned one's look, its own magazine).
    private bool TakeAnother(WeaponPickup pickup,int slot,int s)
    {
        if(inventory==null||holsters==null)return false;
        string p=EquipmentProfile.ForSlot(slot);
        try
        {
            if(!HolsterLayout.Firearm(p)||!inventory.HasEquipableInSlot((PlayerEquipableInventory.ActiveEquipmentSlot)slot))return false;
            // The owned one in play has no still copy yet: made from it now.
            if(holsters.CopyOf(slot)==null&&weapon!=null&&(int)weapon.slot==slot&&visual!=null)
            {try{holsters.Store(slot,HolsterCopy.From(visual,NativeGrip(true,Vector3.zero),false));}catch(Exception ex){Bootstrap.Warn("POINT GRAB copy of the owned "+p+": "+ex.Message);}}
            if(holsters.CopyOf(slot)==null)return false;
            // 0.1.142: at most two others of a kind; a third one gives its rounds only.
            // 0.1.200: one kept on the body gives way (its rounds back to the reserve) when none lies on the floor.
            if(!holsters.MakeRoomForLoose(slot,out int freed))
            {
                var pool0=inventory.playerAmmo;
                if(!pickup.ammoWasTaken&&pickup.weaponPrefab!=null&&pool0!=null){pickup.AddWeaponAmmoToPool(pickup.weaponPrefab,pool0);pickup.ammoWasTaken=true;NotifyAmmo();}
                rig.PunchHaptics(s==1);
                Bootstrap.Write("POINT GRAB "+Side(s)+" hand: already "+BodyHolsters.MaxLoose+" other "+p+" besides the owned one, all in hands; its rounds are taken, the weapon stays");
                return true;
            }
            var owned=holsters.WeaponOf(slot);var f=owned==null?null:FireOf(owned);var a=f?.ammoManagementComponent;if(a==null)return false;
            if(freed>0&&!a.ammoPool.IsInfinite){try{a.ammoPool.AddAmmo(a.primaryAmmoType,freed);}catch(Exception ex){Bootstrap.Warn("POINT GRAB rounds of the one given way: "+ex.Message);}}
            var pool=inventory.playerAmmo;
            if(!pickup.ammoWasTaken&&pickup.weaponPrefab!=null&&pool!=null){pickup.AddWeaponAmmoToPool(pickup.weaponPrefab,pool);pickup.ammoWasTaken=true;}
            int max=Math.Max(1,a.MaxPrimaryMagazineAmmoCount);
            int magazine=a.ammoPool.IsInfinite?max:a.ammoPool.TryRemoveAmmo(a.primaryAmmoType,max);
            int key=holsters.AddLoose(slot,magazine);if(key<0)return false;
            try{pickup.ToggleEnabled(false,false);}catch(Exception){}
            try{if(pickup.gameObject.activeSelf)pickup.gameObject.SetActive(false);}catch(Exception){}
            try{if(playerRoot!=null)pickup.PlayPickupSound(playerRoot.gameObject);}catch(Exception){}
            NotifyAmmo();
            if(!TakeCopy(key,s,"the ground",false))return false;
            gripTakeAt=Time.realtimeSinceStartup;
            Bootstrap.Write("POINT GRAB "+Side(s)+" hand took another "+p+" from "+pickup.name+" (magazine "+magazine+"/"+max+"); the owned "+p+" stays where it is");
            return true;
        }
        catch(Exception ex){Bootstrap.Warn("POINT GRAB another "+p+": "+ex.Message);return false;}
    }
    // Before drawing: the body copies follow the head exactly.
    private void RenderHolsters()
    {
        TickPins();
        if(holsters==null)return;
        // 0.1.234: while the weapon places are moved (VR SETTINGS, the game paused) the weapons on the body follow them.
        bool editing=QualityMenu.EditingPlaces&&PauseMenuControl.HackGameIsPaused&&!rig.Scripted;
        if(GripMode==WeaponGripMode.Always||!editing&&!CanControl(playerId)){holsters.HideAll();return;}
        try{holsters.Render(rig.HeadPosition,rig.HeadRotation,LeftHanded,rig.PlayerRoot);if(!editing)RenderHandCopies();}
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextHolsterReport){nextHolsterReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("HOLSTER draw: "+ex.Message);}holsters.HideAll();}
    }
    private void PutAway(int key,Vector3 hand,Vector3 velocity,int side)
    {
        if(inventory==null||holsters==null||visual==null)return;
        if(DualActive){if(Time.realtimeSinceStartup>=nextHolsterReport){nextHolsterReport=Time.realtimeSinceStartup+5;Bootstrap.Write("HOLSTER two pistols stay in the hands (not put away)");}return;}
        CancelReloadGesture();StopOwnedFire();ReleaseSupport();
        var place=holsters.NearestPlace(key,profile,hand);
        bool drop=place==HolsterSlot.None&&HolsterLayout.Firearm(profile);
        HolsterCopy? copy=null;
        try{copy=HolsterCopy.From(visual,NativeGrip(true,Vector3.zero),HolsterLayout.CenterPlaced(profile),HeldPoseOf(profile));}
        catch(Exception ex){Bootstrap.Warn("HOLSTER copy from the hand: "+ex.Message);}
        var from=visual.FittedToWorld;var position=(Vector3)from.GetColumn(3);var rotation=from.rotation;
        bool switched;
        try{switched=inventory.TrySelectSlot(PlayerEquipableInventory.ActiveEquipmentSlot.Fist,true,true,true,false,true);}
        catch(Exception ex){switched=false;Bootstrap.Warn("HOLSTER fists: "+ex.Message);}
        if(!switched){copy?.Dispose();Bootstrap.Write("HOLSTER the game kept "+profile+" in the hand (switch refused)");gripState.Took(false);foreEndOnly=false;return;}
        if(copy!=null)holsters.Store(key,copy);
        string by=foreEndOnly?" (from the fore-end)":"";
        visual.Hide();poseValid=false;putAwayKey=key;putAwayAt=Time.realtimeSinceStartup;putAwaySide=side;putAwayFrame=Time.frameCount;leftGameKey=-1;foreEndOnly=false;
        if(place!=HolsterSlot.None){holsters.Hang(key,profile,place);Bootstrap.Write("HOLSTER "+Side(side)+" hand hung "+profile+" on "+place+by);}
        else if(drop&&copy!=null&&holsters.Drop(key,position,rotation,velocity,handSpin[side],hand)){Bootstrap.Write("HOLSTER "+Side(side)+" hand let go of "+profile+by+": on the floor (still in the wheel)");}
        else{var s=holsters.Return(key,profile);Bootstrap.Write("HOLSTER "+profile+" back to "+s);}
        rig.PunchHaptics(side==1);
    }
    // The game's weapon to a hand; the copy is shown in that hand while the game switches.
    private void Take(int key,int side,string from)
    {
        if(inventory==null||holsters==null)return;
        var slot=(PlayerEquipableInventory.ActiveEquipmentSlot)key;string p=EquipmentProfile.ForSlot(key);
        // 0.1.185: no copy yet: made now, so the weapon is in the hand at once (not after the game's switch).
        if(HolsterLayout.Firearm(p)&&holsters.CopyOf(key)==null)holsters.BuildNow(key);
        bool shown=HolsterLayout.Firearm(p)&&TakeCopy(key,side,from,true);
        // 0.1.130: while the game is still switching (a weapon just let go and
        // caught again) the copy is held and the switch follows.
        // 0.1.133: also when it is the weapon just let go (the game still
        // switching away from it): it comes into play once that switch ends.
        bool deferred=shown&&(inventory.isInTransit&&CurrentKey!=key||CurrentKey==key&&Leaving(key));
        bool ok;
        try{ok=deferred||inventory.HasEquipableInSlot(slot)&&(CurrentKey==key||inventory.TrySelectSlot(slot,true,true,true,false,true));}
        catch(Exception ex){ok=false;Bootstrap.Warn("HOLSTER take: "+ex.Message);}
        if(!ok){Bootstrap.Write("HOLSTER the game refused "+p+(shown?" (held as a still copy)":""));return;}
        if(deferred){deferredTake[side]=key;deferredAt[side]=Time.realtimeSinceStartup;Bootstrap.Write("HOLSTER "+Side(side)+" hand caught "+p+" from "+from+" (comes into play when the game's switch ends)");return;}
        if(!shown)holsters.Taken(key,p);
        if(side==0){leftGameKey=key;leftGameSince=Time.realtimeSinceStartup;}else RightHandHas(key);
        gripTakeAt=Time.realtimeSinceStartup;nearHolster=false;putAwayKey=-1;rig.PunchHaptics(side==1);
        Bootstrap.Write("HOLSTER "+Side(side)+" hand took "+p+" from "+from);
    }
    // 0.1.129: pulled grenade pins in the fingers, then falling.
    private void TickPins()
    {
        if(pins==null||pins.Count==0)return;
        try
        {
            if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)){pins.Tick(rig.PlayerRoot,Matrix4x4.identity,Matrix4x4.identity,Vector3.zero,Vector3.zero,false);return;}
            pins.Tick(rig.PlayerRoot,Matrix4x4.TRS(CameraRig.UnityPosition(l),GloveVisual.Rotation(l,false),Vector3.one),Matrix4x4.TRS(CameraRig.UnityPosition(r),GloveVisual.Rotation(r,true),Vector3.one),handVelocity[0],handVelocity[1],leftValid);
        }
        catch(Exception ex){pins.Clear();if(Time.realtimeSinceStartup>=nextHolsterReport){nextHolsterReport=Time.realtimeSinceStartup+10;Bootstrap.Warn("GRENADE pins: "+ex.Message);}}
    }
    private void ClearHolsters()
    {
        holsters?.Dispose();holsters=null;pointing?.Dispose();pointing=null;pins?.Clear();emptyAfterThrowAt=-1;looseReloadStates.Clear();
        for(int s=0;s<2;s++){copyKey[s]=-1;copyProfile[s]="";copyGrip[s].Reset();}
        leftGameKey=-1;foreEndOnly=false;ClearClub();copyClub[0]=copyClub[1]=false;leftGrenadeGesture.Reset();leftThrowPending=false;leftRestoreAt=-1;gripState.Reset();gripBoundId=0;nearHolster=false;putAwayKey=-1;
    }
}
