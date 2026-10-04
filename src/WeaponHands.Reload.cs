using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private ManualReloadState reload=new();
    private readonly Dictionary<int,ManualReloadState> reloadStates=new();
    private AmmoManagementComponent? ammo;
    private int reloadAction;
    private AmmoPouch? pouch;
    private ReloadDrops? drops;private WeaponImpactAudio? impactAudio;
    private ReloadAudio? reloadAudio;
    private Vector3 heldAmmoHand;private Quaternion heldAmmoRotation;private int heldAmmoFrame=-10;
    private Vector3 previousLeft,throwVelocity;
    private float previousLeftAt;
    private bool ManualEnabled=>WeaponOptions.ManualReload.Value&&!DualActive&&(EquipmentProfile.Manual(profile)||profile=="revolver"&&visual?.CylinderReady==true)&&ammo!=null
        &&!(EquipmentProfile.ManualFallback(profile)&&visual?.ReloadUnavailable==true);
    private ManualReloadState NewReloadState()=>ReloadStateFor(profile);
    private static ManualReloadState ReloadStateFor(string p)=>new(p=="shotgun",EquipmentProfile.RifleMagazine(p),EquipmentProfile.Arrow(p),EquipmentProfile.Lidded(p));
    // 0.1.134: the hand reload state of each other weapon of an owned kind
    // (traded with the owned weapon's when it comes into play).
    private readonly Dictionary<int,ManualReloadState> looseReloadStates=new();
    // 0.1.194: the double-barrelled shotgun reloads by hand even without the game's shell mesh for the hand.
    private bool ManualReady=>ManualEnabled&&(visual?.ReloadAvailable==true||visual?.BreakAction==true);
    // 0.1.133: the hand that reloads: the left one for a gun in the right
    // hand; the right one (mirrored) for the game's gun in the left hand.
    internal bool ReloadRight=>PrimaryLeft;
    // The game's gun in the left hand reloads by hand (its magazine, bolt,
    // pouch with the right hand) when the manual reload is on for it.
    private bool MirroredManual=>PrimaryLeft&&ManualReady&&copyKey[1]<0;
    // 0.1.221: the button of the other hand that takes magazines, rounds and
    // rockets from the belt and holds them (its grip; it was its trigger).
    internal const ulong AmmoButton=HandControls.Grip;
    // The reloading hand at the belt while the gun in play wants rounds from
    // it: its grip takes them, not a weapon from the left belt's holster place.
    private bool PouchTakes(int s,Vector3 hand)
    {
        try
        {
            if(pouch==null||!pouch.Shown||weapon==null||ammo==null||(s==1)!=ReloadRight)return false;
            int rounds=ammo.PrimaryMagazineAmmoCount,capacity=ammo.MaxPrimaryMagazineAmmoCount;
            bool reserve=ammo.ammoPool.IsInfinite||ammo.PrimaryReserveAmmoCount>0;
            if(!reserve)return false;
            if(BazookaManual)return rounds<=0&&rocketSide<0&&pouch.Near(hand);
            if(BreakReady)return BreakWantsShell(hand);
            if(RevolverReady)return revolver.Open&&revolver.Emptied&&!revolver.Holding&&pouch.Near(hand);
            if(!ManualReady)return false;
            return reload.WantsSupply(rounds,capacity)&&(profile=="shotgun"?pouch.NearShell(hand):pouch.Near(hand));
        }
        catch(Exception){return false;}
    }
    private int pouchFrame=-1;private readonly bool[] pouchNow=new bool[2];
    private bool PouchTakesNow(int s)
    {
        if(pouchFrame!=Time.frameCount)
        {
            pouchFrame=Time.frameCount;pouchNow[0]=pouchNow[1]=false;
            if(pouch!=null&&pouch.Shown&&rig.SampleWorldHands(out var l,out var r,out bool lv))
            {pouchNow[1]=PouchTakes(1,CameraRig.UnityPosition(r));if(lv)pouchNow[0]=PouchTakes(0,CameraRig.UnityPosition(l));}
        }
        return pouchNow[s];
    }
    // 0.1.223: the M60's open cover as a solid thing for the hands (CoverPush).
    private readonly CoverPush coverPush=new();
    private static readonly Vector3 PalmPoint=new(0,-.03f,.05f);
    private ReloadAction PushCover(PoseValue l,PoseValue r,bool leftValid)
    {
        if(visual==null)return ReloadAction.None;
        if(!reload.CoverOpen){coverPush.Reset();visual.CoverPushDegrees=null;return ReloadAction.None;}
        if(reload.Holding||!visual.CoverFrame(out var hinge,out var edge)){visual.CoverPushDegrees=float.IsNaN(coverPush.Opening)?null:coverPush.Opening;return ReloadAction.None;}
        var inverse=visual.FittedToWorld.inverse;bool shut=false;
        for(int s=0;s<2;s++)
        {
            if(s==0&&!leftValid)continue;var pose=s==1?r:l;
            var palm=CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,s==1)*PalmPoint;
            if(coverPush.Step(s,ToN(inverse.MultiplyPoint3x4(palm)),ToN(hinge),ToN(edge),WeaponVisual.CoverOpenDegrees))shut=true;
        }
        visual.CoverPushDegrees=float.IsNaN(coverPush.Opening)?null:coverPush.Opening;
        if(!shut)return ReloadAction.None;
        var action=reload.PushCoverShut();
        if(action!=ReloadAction.None)Bootstrap.Write("M60 COVER pressed shut by hand");
        return action;
    }
    internal bool ReloadHandHolding(bool right)=>right==ReloadRight&&(BreakHolding||reload.Holding&&ManualReady&&!BreakReady||RevolverReady&&revolver.Holding);
    private bool lastReloadRight;
    // The trigger that fires the gun must not fire as the magazine goes in.
    private void DisarmFireTrigger(){if(PrimaryLeft)leftTriggerLocked=true;else rig.DisarmTrigger();}
    internal bool TryReloadAccess(Vector3 hand,Quaternion rotation,out Vector3 point,out Vector3 axis)
    {
        point=Vector3.zero;axis=Vector3.up;
        if(RevolverReady&&revolver.Open&&visual!=null)
        {point=visual.FittedToWorld.MultiplyPoint3x4(visual.CylinderSocket);axis=visual.FittedToWorld.MultiplyVector(Vector3.forward).normalized;return (hand-point).sqrMagnitude<.32f*.32f;}
        if(!ManualReady||visual==null)return false;
        // 0.1.194: the double-barrelled shotgun's open chamber.
        if(BreakReady)return BreakAccess(visual.AmmunitionTip(hand,rotation,ReloadRight),out point,out axis);
        // 0.1.223: the M60's box goes in at its own place.
        var local=reload.Holding&&!reload.DiscardOnly?(EquipmentProfile.Lidded(profile)?visual.MagazineCenter:visual.ReloadPort):reload.Hint?visual.MagazineCenter:reload.NeedsRack?visual.ReloadBolt-Vector3.forward*reload.VisualRackTravel:Vector3.positiveInfinity;
        point=visual.FittedToWorld.MultiplyPoint3x4(local);
        axis=visual.FittedToWorld.MultiplyVector(visual.InsertAxis).normalized;
        bool right=ReloadRight;
        var probe=reload.Holding?visual.AmmunitionTip(hand,rotation,right):hand;
        return (probe-point).sqrMagnitude<.30f*.30f&&(!reload.Holding||Vector3.Dot(visual.AmmunitionForward(rotation,right),axis)>.45f);
    }
    internal bool LeftReloadHolding=>ReloadHandHolding(false);
    internal bool ReloadContactFree=>ManualReady&&EquipmentProfile.TightContact(profile)&&(reload.Active||reload.Holding);
    internal string ReloadLabel=>BazookaManual&&ammo!=null&&ammo.PrimaryMagazineAmmoCount<=0?(rocketSide>=0?"INSERT":"POUCH"):!ManualReady||foreEndOnly||PrimaryLeft&&!MirroredManual?"":BreakReady?BreakLabel:reload.CoverOpen&&!reload.Holding&&reload.Installed?"COVER":reload.Holding?(reload.DiscardOnly?"DROP":"INSERT"):reload.Hint?"GRAB":!reload.Installed||EquipmentProfile.Arrow(profile)&&ammo!=null&&ammo.PrimaryMagazineAmmoCount<=0?"POUCH":reload.NeedsRack?"RACK":"";
    private void BindReload()
    {
        if(weapon==null)return;
        if(profile=="revolver"&&WeaponOptions.ManualReload.Value)visual?.PrepareCylinder();
        int id=weapon.GetInstanceID();
        if(!reloadStates.TryGetValue(id,out var state)){state=NewReloadState();reloadStates.Add(id,state);}
        reload=state;previousManual=ManualEnabled;nativeReloadRefused=false;if(visual!=null){visual.ManualMode=ManualEnabled;visual.StableGun=DualActive||ManualEnabled&&profile=="shotgun";}
        if(ManualEnabled){if(profile=="revolver")visual?.PrepareCylinder();else visual?.PrepareReload();reloadAudio??=new ReloadAudio();}
    }
    private bool previousManual;
    private void TickReloadProps()
    {
        if(profile=="revolver"&&WeaponOptions.ManualReload.Value)visual?.PrepareCylinder();
        if(ManualEnabled&&profile!="revolver"&&visual?.ReloadAvailable==false)visual.PrepareReload();
        if(weapon!=null&&previousManual!=ManualEnabled)
        {
            CancelRevolver();revolver=new RevolverReloadState();visual?.PoseCylinder(false,false,Vector3.zero,Quaternion.identity);
            CancelReloadGesture();reload=NewReloadState();reloadStates[weapon.GetInstanceID()]=reload;
            previousManual=ManualEnabled;visual?.ReloadPose(false,false,false,0,Vector3.zero,Quaternion.identity);
            if(ManualEnabled){if(profile=="revolver")visual?.PrepareCylinder();else visual?.PrepareReload();reloadAudio??=new ReloadAudio();}
            if(visual!=null){visual.ManualMode=ManualEnabled;visual.StableGun=DualActive||ManualEnabled&&profile=="shotgun";}
            rig.DisarmTrigger();
        }
        impactAudio??=new WeaponImpactAudio();
        drops?.Tick(rig.PlayerRoot);reloadAudio?.Tick(rig.HeadPosition,rig.HeadRotation);
        bool show=enabled&&rig.PlayerRoot!=null&&!rig.Scripted;
        if(pouch!=null&&!pouch.Valid){pouch.Dispose();pouch=null;}
        // 0.1.135: the pouch stays on the left of the belt whichever hand reloads.
        if(show){pouch??=new AmmoPouch();pouch.Pose(rig.HeadPosition,rig.HeadRotation,LeftHanded);
            int shells=inventory?.playerAmmo==null?0:inventory.playerAmmo.GetAmmoCount(ActorAmmoPool.AmmoType.Shotgun_12GBuckshot);
            if(inventory?.playerAmmo?.AmmoPool?.IsInfinite==true)shells=8;pouch.SetShellCount(shells);}
        else pouch?.Hide();
    }
    private void TickReload()
    {
        // 0.1.133: the right hand reloads the game's gun in the left hand
        // (mirrored); a gesture in progress ends when the gun changes hands.
        bool mirrored=ReloadRight;int rs=mirrored?1:0;
        if(mirrored!=lastReloadRight){lastReloadRight=mirrored;if(reload.Holding||reload.Active||BreakHolding)CancelReloadGesture();}
        if(mirrored?GripCarry.Current?.HoldingBody==true:GripCarry.Current?.HidesLeft==true){CancelReloadGesture();return;}
        // 0.1.136: a shotgun hanging by its fore-end is pumped with that hand.
        if(foreEndOnly&&!Clubbed&&profile=="shotgun"&&ManualReady&&!BreakReady&&weapon!=null&&visual!=null&&ammo!=null){if(reload.Holding)CancelReloadGesture();TickOneHandPump();return;}
        pumpActive=false;
        // 0.1.126: a hand holding a weapon of its own does not reload.
        // 0.1.128: a gun hanging by its fore-end reloads the game's way.
        if(copyKey[rs]>=0||foreEndOnly){if(reload.Holding||BreakHolding)CancelReloadGesture();return;}
        if(!ManualReady||weapon==null||visual==null||ammo==null)return;
        reload.ObserveRounds(ammo.PrimaryMagazineAmmoCount);
        if(!CanControl(playerId)||!rig.LeftControls.Valid||!rig.RightControls.Valid||inventory!.isInTransit)
        {CancelReloadGesture();return;}
        if(!rig.SampleWorldHands(out var l,out var r,out bool validLeft)||!validLeft){CancelReloadGesture();return;}
        var hp=mirrored?r:l;
        var world=CameraRig.UnityPosition(hp);var rotation=GloveVisual.Rotation(hp,mirrored);
        float now=Time.realtimeSinceStartup,dt=now-previousLeftAt;
        throwVelocity=dt>.0001f&&dt<.1f?Vector3.ClampMagnitude((world-previousLeft)/dt,5):Vector3.zero;
        previousLeft=world;previousLeftAt=now;
        // The B of the hand holding the gun (tap: magazine out; hold: take
        // it): the right B, or the left Y for the gun in the left hand
        // (0.1.142; it was the right B either way); the reloading
        // hand's grip takes and holds the magazine or rounds (0.1.221: was
        // its trigger), its trigger racks a bolt.
        var right=mirrored?rig.LeftControls:rig.RightControls;var left=mirrored?rig.RightControls:rig.LeftControls;
        // Resolve this tracking sample, not last frame's world-space glove pose.
        // Locomotion and support release can move the receiver between frames.
        var interactionHand=world;var interactionRotation=rotation;
        if((reload.Holding||BreakHolding)&&ContactRig.Current?.ResolveHand(mirrored,false,ref interactionHand,ref interactionRotation)==false)return;
        // 0.1.194: the double-barrelled shotgun breaks open instead.
        if(BreakReady){TickBreak(mirrored,interactionHand,interactionRotation,world,right,left,mirrored?l:r,now);return;}
        var local=visual.FittedToWorld.inverse.MultiplyPoint3x4(interactionHand+interactionRotation*new Vector3(0,-.025f,.015f));
        var tip=visual.FittedToWorld.inverse.MultiplyPoint3x4(visual.AmmunitionTip(interactionHand,interactionRotation,mirrored));
        var heldForward=visual.FittedToWorld.inverse.MultiplyVector(visual.AmmunitionForward(interactionRotation,mirrored)).normalized;
        bool aligned=Vector3.Dot(heldForward,visual.InsertAxis)>.50f;
        // 0.1.122: the crossbow bolt is tracked by its rear end along the rail.
        bool bolt=EquipmentProfile.Arrow(profile);float railLength=bolt?visual.ArrowLength:0;
        if(bolt){tip-=heldForward*railLength;aligned=Vector3.Dot(heldForward,visual.InsertAxis)>.80f;}
        bool wasOnRail=reload.OnRail;float railBefore=reload.RailOffset;
        // 0.1.223: the M60's open cover pressed shut by either hand.
        var pushed=PushCover(l,r,validLeft);
        var action=pushed!=ReloadAction.None?pushed:reload.Step(now,(right.Down&HandControls.B)!=0,(right.Held&HandControls.B)!=0,(right.Up&HandControls.B)!=0,
            (left.Down&HandControls.Trigger)!=0,(left.Held&HandControls.Trigger)!=0,pouch!=null&&(profile=="shotgun"?pouch.NearShell(world):pouch.Near(world)),
            ToN(local),ToN(visual.MagazineCenter),ToN(visual.ReloadPort),ToN(visual.ReloadBolt),ToN(visual.InsertAxis),
            ammo.PrimaryMagazineAmmoCount,ammo.MaxPrimaryMagazineAmmoCount,(left.Held&HandControls.Grip)!=0,ToN(tip),aligned,
            visual.CoverGrab is Vector3 cover?ToN(cover):null,railLength,(left.Down&AmmoButton)!=0,(left.Held&AmmoButton)!=0);
        RailHaptics(wasOnRail,railBefore,dt);
        if(reload.BlocksFire){StopOwnedFire();if(profile!="shotgun"||reload.Holding)ReleaseSupport();}
        if(action==ReloadAction.None)return;
        if(action==ReloadAction.TakeSupply||action==ReloadAction.DropHeld||action==ReloadAction.Insert||action==ReloadAction.TakeInstalled)
        {heldAmmoFrame=-10;ContactRig.Current?.ResetHand(mirrored);}
        // No native reload animation/coroutine. Magazine writes and reserve
        // transactions happen exactly once, on the physical state transition.
        try
        {
            switch(action)
            {
                case ReloadAction.DropInstalled:
                case ReloadAction.TakeInstalled:
                    int old=ammo.PrimaryMagazineAmmoCount;
                    ammo.CancelReload();SetMagazine(0);
                    reload.Detach(old,action==ReloadAction.TakeInstalled);
                    if(action==ReloadAction.DropInstalled){Refund(old);Drop(visual.FittedToWorld.MultiplyPoint3x4(visual.MagazineCenter),visual.FittedToWorld.rotation,Vector3.down*.35f);}
                    break;
                case ReloadAction.TakeSupply:
                    // 0.1.124: the game's own (refused) reload is cancelled.
                    ResetNativeReload("take");
                    int capacity=EquipmentProfile.SingleRound(profile)?1:ammo.MaxPrimaryMagazineAmmoCount;
                    int request=ammo.ammoPool.IsInfinite?capacity:Math.Min(capacity,Math.Max(0,ammo.PrimaryReserveAmmoCount));
                    if(EquipmentProfile.SingleRound(profile)&&request==0)break;
                    int obtained=ammo.ammoPool.IsInfinite?request:ammo.ammoPool.TryRemoveAmmo(ammo.primaryAmmoType,request);
                    reload.Supply(obtained);NotifyAmmo();ReleaseSupport();break;
                case ReloadAction.DropHeld:
                    visual.AmmunitionPose(interactionHand,interactionRotation,out var dropPoint,out var dropRotation,mirrored);
                    Refund(reload.ReturnHeld());Drop(dropPoint,dropRotation,throwVelocity);break;
                case ReloadAction.Insert:
                    ResetNativeReload("insert");
                    int before=ammo.PrimaryMagazineAmmoCount;
                    int total=EquipmentProfile.SingleRound(profile)?before+reload.HeldRounds:reload.HeldRounds;
                    SetMagazine(total);reload.Inserted(before);DisarmFireTrigger();
                    // 0.1.125: the crossbow's animator only leaves its empty state when drawn again.
                    if(EquipmentProfile.Arrow(profile))redrawAfterAction=true;
                    break;
                case ReloadAction.Chamber:
                    DisarmFireTrigger();break;
                case ReloadAction.RackBack:break;
                case ReloadAction.OpenCover:case ReloadAction.CloseCover:DisarmFireTrigger();break;
            }
            if(profile=="shotgun"&&action==ReloadAction.RackBack&&reload.TakeSpentCase())visual.EjectShell(reloadAudio);
            // 0.1.123: end the game's wait for its own (blocked) reload.
            if(action==ReloadAction.Insert||action==ReloadAction.Chamber)ReleaseNativeFire(action.ToString());
            reloadAudio?.Play(action,profile,visual.FittedToWorld.MultiplyPoint3x4(visual.ReloadBolt),rig.HeadPosition,rig.HeadRotation);
            rig.ReloadHaptics(action,mirrored);
            Bootstrap.Write("PHYSICAL RELOAD "+profile+(mirrored?" (right hand, the gun in the left)":"")+" action="+action+" installed="+reload.Installed+" rack="+reload.NeedsRack+" magazine="+ammo.PrimaryMagazineAmmoCount+" reserve="+ammo.PrimaryReserveAmmoCount+" held="+reload.HeldRounds);
            if(redrawAfterAction){redrawAfterAction=false;RequestRedraw("the bolt was inserted");}
        }
        catch(Exception ex){CancelReloadGesture();Bootstrap.Warn("Physical reload transaction: "+ex.Message);}
    }
    // 0.1.136: the shotgun hanging by its fore-end in one hand (caught there
    // after a toss, or the handle let go): a jerk of that hand towards the
    // stock and back pumps it (InertialPump: the gun's own weight and
    // inertia move it along the pump in the hand).
    private readonly InertialPump oneHandPump=new();private bool pumpActive;
    private void TickOneHandPump()
    {
        int hand=PrimaryLeft?1:0;   // the handle side is the game's side; the fore-end hand is the other
        if(!CanControl(playerId)||inventory!.isInTransit||!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||hand==0&&!leftValid){pumpActive=false;return;}
        if(!reload.NeedsRack&&!reload.Pulled){pumpActive=false;return;}
        float now=Time.realtimeSinceStartup;
        var at=CameraRig.UnityPosition(hand==0?l:r);var axis=visual!.FittedToWorld.MultiplyVector(Vector3.forward);
        // 0.1.138: from a two-hand hold the pump hand was still racking by the
        // grip (after a shot it takes the pump at once), and the gun's inertia
        // was ignored: only a gun tossed up and caught by the pump could be
        // pumped with one hand.
        if(!pumpActive)
        {
            pumpActive=true;bool wasRacking=reload.Racking;reload.EndRacking();
            oneHandPump.Reset(reload.Pulled?reload.FullTravel:reload.RackTravel);
            Bootstrap.Write("ONE-HAND PUMP shotgun hangs by its pump in the "+(hand==0?"left":"right")+" hand"+(wasRacking?" (was racked by the grip)":"")+": jerk down and back up; travel="+reload.RackTravel.ToString("F3"));
        }
        float travel=oneHandPump.Step(ToN(at),ToN(axis),now,reload.FullTravel,new System.Numerics.Vector3(0,-9.81f,0));
        var action=reload.InertialRack(travel);
        if(action==ReloadAction.None)return;
        if(action==ReloadAction.RackBack&&reload.TakeSpentCase())visual.EjectShell(reloadAudio);
        if(action==ReloadAction.Chamber)ReleaseNativeFire("pump");
        reloadAudio?.Play(action,profile,visual.FittedToWorld.MultiplyPoint3x4(visual.ReloadBolt),rig.HeadPosition,rig.HeadRotation);
        rig.ReloadHaptics(action,hand==1);
        Bootstrap.Write("ONE-HAND PUMP shotgun "+action+" by the "+(hand==0?"left":"right")+" hand (a jerk: the gun's weight moved it along the pump)");
    }
    private void SetMagazine(int count)
    {
        ammo!.triggerPrimaryReload=false;ammo.triggerSecondaryReload=false;
        ammo.SetAmmo(count,ammo.SecondaryMagazineAmmoCount);
        if(ammo.PrimaryMagazineAmmoCount!=count)throw new InvalidOperationException("Native magazine rejected count "+count);
        // Cosmetic notification must not cause a second ammunition transaction.
        NotifyAmmo();
    }
    private void NotifyAmmo()
    {try{ammo?.TriggerEventAmmoPoolChanged();}catch(Exception ex){Bootstrap.Warn("Reload HUD event: "+ex.Message);}}
    private void Drop(Vector3 position,Quaternion rotation,Vector3 velocity)
    {
        if(visual?.Ammunition==null)return;
        try{drops??=new ReloadDrops();drops.Add(visual.Ammunition,position,rotation,velocity);}
        catch(Exception ex){Bootstrap.Warn("Discarded magazine visual: "+ex.Message);}
    }
    // 0.1.146:
    // the hand drawing a bolt back along the crossbow's rail rests on the
    // rail - it is drawn where the bolt's rear lies on the rail line, not in
    // the stock under it (nor beside it).
    internal void RestOnRail(bool right,ref Vector3 hand,Quaternion rotation)
    {
        if(!reload.OnRail||!ManualReady||!ReloadHandHolding(right)||!EquipmentProfile.Arrow(profile)||visual==null)return;
        try
        {
            var m=visual.FittedToWorld;var inverse=m.inverse;
            var forward=inverse.MultiplyVector(visual.AmmunitionForward(rotation,right)).normalized;
            var rear=inverse.MultiplyPoint3x4(visual.AmmunitionTip(hand,rotation,right))-forward*visual.ArrowLength;
            var axis=visual.InsertAxis.normalized;var d=rear-visual.ReloadPort;
            var across=d-axis*Vector3.Dot(d,axis);
            if(!(across.sqrMagnitude<=(ManualReloadState.RailSink+.05f)*(ManualReloadState.RailSink+.05f)))return;
            hand-=m.MultiplyVector(across);
        }
        catch(Exception){}
    }
    internal void PoseHeldAmmunition(bool right,Vector3 hand,Quaternion rotation)
    {
        if(!ReloadHandHolding(right))return;
        heldAmmoHand=hand;heldAmmoRotation=rotation;heldAmmoFrame=Time.frameCount;
        // 0.1.122: a crossbow bolt on the rail lies on the rail.
        if(reload.OnRail&&EquipmentProfile.Arrow(profile))visual?.PoseArrowOnRail(reload.RailOffset);
        else visual?.PoseAmmunition(hand,rotation,right);
    }
    // Resistance while the bolt is drawn back: a click when it is laid on the
    // rail, then a buzz that grows with the drawing speed.
    private float nextRailHaptic;
    private void RailHaptics(bool wasOnRail,float before,float dt)
    {
        if(!reload.OnRail)return;
        float now=Time.realtimeSinceStartup;
        if(!wasOnRail){rig.ResistanceHaptics(.55f,ReloadRight);nextRailHaptic=now+.05f;Bootstrap.Write("CROSSBOW bolt laid on the rail at "+reload.RailOffset.ToString("F2")+" m from the string");return;}
        if(now<nextRailHaptic)return;
        float speed=dt>.0001f&&dt<.1f?Math.Max(0,before-reload.RailOffset)/dt:0;
        rig.ResistanceHaptics(RailMath.Resistance(speed),ReloadRight);nextRailHaptic=now+.04f;
    }
    private void RenderReload(PoseValue left)
    {
        if(!ManualReady){HideHints();return;}
        if(BreakReady){RenderBreak(left);return;}
        if(ammo!=null)reload.ObserveRounds(ammo.PrimaryMagazineAmmoCount);
        // 0.1.117: an empty crossbow shows no bolt on its rail.
        bool empty=!reload.Installed||EquipmentProfile.Arrow(profile)&&ammo!=null&&ammo.PrimaryMagazineAmmoCount<=0;
        visual?.ReloadPose(reload.Holding,empty,reload.Hint,reload.VisualRackTravel,CameraRig.UnityPosition(left),GloveVisual.Rotation(left,false),reload.SlideLocked);
        // 0.1.123: the crossbow string: cocked while loaded, carried back by the
        // bolt drawn along the rail, let go after the shot.
        if(visual!=null&&EquipmentProfile.Arrow(profile))visual.StringDraw=reload.Holding&&reload.OnRail?visual.StringDrawFor(reload.RailOffset):empty?0:1;
        if(visual!=null)visual.CoverOpen=reload.CoverOpen;
        // 0.1.183: the chest reload has its own glow (both hands full).
        if(foreEndOnly||PrimaryLeft&&!MirroredManual||ChestGame)HideHints();else UpdateGlow();
    }
    // 0.1.120: the faint pulsing glow on what to take next.
    // 0.1.122: a blinking white outline of that part (ReloadOutline): the
    // installed magazine/box/bolt, the pouch, the charging handle/slide, the
    // M60 cover. A small white glow only where no part shape exists.
    // 0.1.123: no hint where the held magazine goes.
    private ReloadGlow? glow;private ReloadOutline? outline;private bool outlineFailed;
    private void HideHints(){glow?.Hide();outline?.Hide();}
    private void UpdateGlow()
    {
        try
        {
            var v=visual;var a=ammo;
            if(!ManualReady||v==null||a==null){HideHints();return;}
            var target=ReloadGlowMath.Target(reload.Holding,reload.HeldRounds,reload.Installed,reload.NeedsRack,reload.CoverOpen,a.PrimaryMagazineAmmoCount,
                EquipmentProfile.Lidded(profile),EquipmentProfile.SingleRound(profile));
            var m=v.FittedToWorld;Vector3 at;Mesh? shape=null;Matrix4x4 place=Matrix4x4.identity;
            switch(target)
            {
                case GlowTarget.Magazine:at=v.MagazineCenter;shape=v.Ammunition?.Shape;place=m*Matrix4x4.Translate(v.MagazineCenter);at=m.MultiplyPoint3x4(at);break;
                case GlowTarget.Pouch:if(pouch==null||!pouch.Shown){HideHints();return;}at=pouch.Center;shape=pouch.Shape;place=pouch.ShapeToWorld;break;
                case GlowTarget.Bolt:
                    at=m.MultiplyPoint3x4(v.ReloadBolt-Vector3.forward*reload.VisualRackTravel);
                    if(v.BoltShape!=null){shape=v.BoltShape;place=m*Matrix4x4.Translate(v.BoltShapeCenter-Vector3.forward*reload.VisualRackTravel);}
                    break;
                case GlowTarget.Cover:
                    var c=reload.CoverOpen?v.CoverGrab:v.CoverMiddle;if(c==null){HideHints();return;}
                    at=m.MultiplyPoint3x4(c.Value);
                    if(v.CoverShape!=null){shape=v.CoverShape;place=m*v.CoverShapeToFit;}
                    break;
                default:HideHints();return;
            }
            bool drawn=false;
            if(shape!=null&&!outlineFailed)
            {
                try{outline??=new ReloadOutline();drawn=outline.Show(shape,place);}
                catch(Exception ex){outlineFailed=true;outline?.Dispose();outline=null;Bootstrap.Warn("RELOAD OUTLINE unavailable (small glow instead): "+ex.Message);}
            }
            if(drawn){glow?.Hide();return;}
            outline?.Hide();glow??=new ReloadGlow();glow.Show(at,.02f);
        }
        catch(Exception ex){HideHints();if(Time.realtimeSinceStartup>=nextGlowError){nextGlowError=Time.realtimeSinceStartup+10;Bootstrap.Warn("Reload glow: "+ex.Message);}}
    }
    private float nextGlowError;
    private void CancelReloadGesture()
    {
        HideHints();
        if(BreakReady)CancelBreak();
        int refund=reload.Suspend();previousLeftAt=0;heldAmmoFrame=-10;
        Refund(refund);
        visual?.ReloadPose(false,!reload.Installed,false,reload.VisualRackTravel,Vector3.zero,Quaternion.identity,reload.SlideLocked);
        if(visual!=null&&EquipmentProfile.Arrow(profile)&&ammo!=null)visual.StringDraw=reload.Installed&&ammo.PrimaryMagazineAmmoCount>0?1:0;
    }
    private void Refund(int refund)
    {
        if(refund>0&&ammo!=null&&!ammo.ammoPool.IsInfinite)
        {
            try{ammo.ammoPool.AddAmmo(ammo.primaryAmmoType,refund);NotifyAmmo();}
            catch(Exception ex){Bootstrap.Warn("Unable to return uninserted ammunition: "+ex.Message);}
        }
        
    }
    private static bool SelectedShotSound(Equipable equipable)
    {
        var c=Current;
        // 0.1.132: a weapon firing beside the game's one gets its sound from CopyShotSound.
        if(c!=null&&c.copyShooting)return false;
        if(c==null||c.profile!="shotgun"||c.weapon==null||equipable==null||c.weapon.Pointer!=equipable.Pointer||!c.CanControl(c.playerId))return true;
        // 0.1.194: the double-barrelled shotgun keeps its own shot (no pump in it).
        if(BreakGun(c.weapon))return true;
        c.reloadAudio??=new ReloadAudio();
        // Suppress the native shot+pump event only after the replacement started.
        return !c.reloadAudio.PlayCue("shotgun",6);
    }
    private static bool CopyNativeSound()=>Current?.copyShooting!=true;
    private static bool AllowReloadSound(Equipable equipable)
    {
        try
        {
            var c=Current;
            if(c==null||!c.CanControl(c.playerId)||c.weapon==null||equipable==null||c.weapon.Pointer!=equipable.Pointer)return true;
            if(c.profile=="shotgun")
            {
                c.reloadAudio??=new ReloadAudio();
                if(!c.ManualReady)c.reloadAudio.PlayCue("shotgun",4);
                return false;
            }
            // The grenade launcher's own reload keeps its native sound.
            if(Time.realtimeSinceStartup-c.secondaryReloadAt<4)return true;
            return !c.ManualReady&&!c.RevolverReady;
        }
        catch{return true;}
    }
    // 0.1.122: only the primary (magazine) reload is the hands' job. The M16's
    // grenade launcher reloads natively (StartReload(false)); blocking it left
    // the launcher empty after its first grenade (sound, no shot).
    private float secondaryReloadAt=-10;
    // 0.1.123: the game asked for its own magazine reload and was refused
    // (the hands reload); it may hold the gun back until that reload ends.
    private bool nativeReloadRefused,redrawAfterAction;
    private static bool AllowStartReload(AmmoManagementComponent __instance,bool isPrimary)
    {
        if(isPrimary)
        {
            bool allow=AllowStockReload(__instance);
            if(!allow&&Current!=null)Current.nativeReloadRefused=true;
            return allow;
        }
        var c=Current;if(c!=null&&c.ammo!=null&&c.ammo.Pointer==__instance.Pointer)c.secondaryReloadAt=Time.realtimeSinceStartup;
        return true;
    }
    private static bool AllowStockReload(AmmoManagementComponent __instance)
    {
        var c=Current;
        // If extraction is unavailable, do not silently substitute automatic
        // reload for B. Report the asset failure and allow config opt-out.
        // 0.1.183: a pistol reloaded against the chest never reloads the game's way.
        // 0.1.194: neither of the game's two pistols reloads the game's way while they reload against the chest.
        if(c!=null&&c.DualChestOn&&c.OwnsAmmo(__instance))return false;
        // 0.1.215: the bazooka reloaded by hand (a rocket from the pouch) never reloads the game's way.
        if(c!=null&&c.BazookaManual&&c.ammo!=null&&c.ammo.Pointer==__instance.Pointer)return false;
        return c==null||!c.ManualEnabled||c.PrimaryLeft&&!c.MirroredManual&&!c.ChestGame||c.ammo!.Pointer!=__instance.Pointer;
    }

}
