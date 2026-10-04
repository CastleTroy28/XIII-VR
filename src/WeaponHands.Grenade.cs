using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.118: hand grenade — left trigger at the grenade pulls the pin (a click
// of the game's fuse sound, both controllers pulse); holding the right
// trigger is holding the lever; letting it go throws the grenade with the
// hand's velocity (GrenadeThrow). The game's projectile, damage and fuse are
// used through the same launch path as thrown props (ThrowingComponent +
// PropLaunched sets position and velocity). The game's own button throw is
// not used while the grenade is in hand.
internal sealed partial class WeaponHands
{
    private readonly GrenadeThrow grenadeGesture=new();
    private ThrowingComponent? grenadeThrow;
    private float grenadeCheckAt=-1,grenadeHiddenUntil;private int grenadeCountBefore=-1;
    // 0.1.126: the grenade's own ammunition, kept for the count check (the
    // right hand's weapon may be back by then, after a left-hand throw).
    private AmmoManagementComponent? grenadeAmmo;
    // 0.1.129: pulled pins (drawn in the hand, then dropped); the fuse sound
    // plays only its first moment (the pin's click), no ticking; after a
    // right-hand throw the hand is empty (the next grenade stays on the chest).
    private GrenadePins? pins;
    private FMOD.Studio.EventInstance clickInstance;private bool clickLive;private float clickStopAt=-1;
    private float emptyAfterThrowAt=-1,thrownAt=-10;
    // 0.1.149: what was thrown (a grenade, or a knife let go of by the grip).
    private PlayerEquipableInventory.ActiveEquipmentSlot emptyAfterThrowSlot=PlayerEquipableInventory.ActiveEquipmentSlot.Grenade;
    private bool reportedFuseEvent;
    internal bool GrenadePinPulled=>profile=="grenade"&&grenadeGesture.PinPulled;
    private bool GrenadeOwned=>profile=="grenade"&&grenadeThrow!=null;
    private void TickGrenade()
    {
        CheckGrenadeAmmo();
        if(profile!="grenade"||weapon==null){if(grenadeThrow!=null)ResetGrenade("weapon changed");if(LeftThrowBusy)TickLeftThrow();return;}
        if(!CanControl(playerId)||inventory?.isInTransit==true){if(LeftThrowBusy)leftWaitWhy=inventory?.isInTransit==true?"the game still drawing its grenade":"the game not taking input";return;}
        if(grenadeThrow==null||grenadeThrow.baseEquipable==null||grenadeThrow.baseEquipable.Pointer!=weapon.Pointer)
        {
            if(grenadeThrow!=null)ResetGrenade("next grenade");
            grenadeThrow=weapon.GetComponent(Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>();
            if(grenadeThrow!=null)
                Bootstrap.Write("GRENADE bound "+weapon.identifier+" detonation="+grenadeThrow.detonationType+" delay="+grenadeThrow.explosionDelay+" canBeDelayed="+grenadeThrow.canBeDelayed
                    +" pinAfterDelay="+grenadeThrow.pinPulledAfterDelay+" newControls="+grenadeThrow.useNewThrowControls+" count="+GrenadeCount());
            else{Bootstrap.Warn("GRENADE no ThrowingComponent on "+weapon.identifier+"; the game's own throw stays in use");return;}
        }
        // 0.1.126: selected only to throw the left hand's grenade.
        if(LeftThrowBusy){TickLeftThrow();return;}
        RenderPose();if(!poseValid||visual==null)return;
        if(!rig.SampleRightRelative(out var local,out var toWorld)||!rig.SampleWorldHands(out var left,out _,out bool leftValid))return;
        var center=visual.FittedToWorld.MultiplyPoint3x4(visual.ItemCenter);
        var fingers=CameraRig.UnityPosition(left)+GloveVisual.Rotation(left,false)*new Vector3(0,-.02f,.06f);
        bool leftFree=leftValid&&rig.LeftControls.Valid&&!LeftReloadHolding&&!LeftHoldsWeapon&&GripCarry.Current?.HidesLeft!=true;
        if(grenadeGesture.PullPin(leftFree&&(rig.LeftControls.Down&HandControls.Trigger)!=0,ToN(fingers),ToN(center)))
        {
            // 0.1.133: the game's own pin state is set only for the launch (a
            // pulled pin in the game's hands runs its own fuse and sound).
            PinClick(grenadeThrow,center);
            rig.PunchHaptics(false);rig.PunchHaptics(true);
            // The pin comes out of the grenade into the left fingers.
            try
            {
                var pinMesh=new Mesh{name="XIII grenade pin"};
                if(visual.BakePin(pinMesh,true,out var mats,out var pinWorld,out var pinCenter))
                {pins??=new GrenadePins();pins.Add(pinMesh,mats,pinWorld,pinCenter,Matrix4x4.TRS(CameraRig.UnityPosition(left),GloveVisual.Rotation(left,false),Vector3.one),false);}
                else UnityEngine.Object.Destroy(pinMesh);
            }
            catch(Exception ex){Bootstrap.Warn("GRENADE pin visual: "+ex.Message);}
            visual.PinPulled=true;
            Bootstrap.Write("GRENADE pin pulled; "+(GripMode==WeaponGripMode.Always?"hold the right trigger":"hold the right grip")+", swing and let go");
        }
        visual.PinPulled=grenadeGesture.PinPulled;
        // 0.1.126: with the grip holding weapons the grip is the lever (let go
        // to throw); the old way it is the right trigger.
        ulong lever=GripMode==WeaponGripMode.Always?HandControls.Trigger:HandControls.Grip;
        bool leverHeld=rig.RightControls.Valid&&(rig.RightControls.Held&lever)!=0;
        var result=grenadeGesture.Sample(Time.realtimeSinceStartup,leverHeld,ToN(local));
        // 0.1.214: where it lands, while the pin is out and the lever held.
        if(!result.Throw&&grenadeGesture.PinPulled&&leverHeld)AimGrenadeLanding(1,grenadeGesture,center,toWorld,weapon);
        if(!result.Throw)return;
        var velocity=LivelyThrow(AimedThrow(toWorld*new Vector3(result.Velocity.X,result.Velocity.Y,result.Velocity.Z)),result.HandSpeed);
        throwOrigin=center;throwLaunch=velocity;throwRotation=visual.FittedToWorld.rotation;throwSpin=ThrowSpin(1);
        StopClick();StopGameFuse(grenadeThrow);
        grenadeAmmo=ammo;grenadeCountBefore=GrenadeCount();grenadeCheckAt=Time.realtimeSinceStartup+1f;grenadeHiddenUntil=Time.realtimeSinceStartup+.5f;
        propThrow=grenadeThrow;
        try{grenadeThrow.pinPulled=true;}catch(Exception){}
        lastThrown=grenadeThrow;lastThrownAt=Time.realtimeSinceStartup;
        LaunchProp("grenade handSpeed="+result.HandSpeed.ToString("F2")+" speed="+velocity.magnitude.ToString("F2"));
        SettleAfterLaunch(grenadeThrow,"right hand");
        // 0.1.129: the right hand is empty after the throw.
        if(GripMode!=WeaponGripMode.Always){thrownAt=Time.realtimeSinceStartup;emptyAfterThrowAt=thrownAt+.35f;emptyAfterThrowSlot=PlayerEquipableInventory.ActiveEquipmentSlot.Grenade;gripState.Reset();}
    }
    // 0.1.138: thrown as briskly as a thrown prop (the hand's speed alone,
    // against the game's double gravity, dropped it a few metres away): a
    // real swing flies at the game's throwing speed (scaled by how hard the
    // swing was), in the hand's direction; a gentle toss stays gentle.
    private Vector3 LivelyThrow(Vector3 velocity,float handSpeed)
    {
        float native=-1;try{native=weapon!.CurrentEquipableParameters.primaryProjectileSpeed;}catch(Exception){}
        var v=GrenadeThrow.Lively(ToN(velocity),handSpeed,ThrowTrajectory.Speed(native));
        if(!reportedLively){reportedLively=true;Bootstrap.Write("GRENADE throw speed: the game's "+native.ToString("F1")+" m/s (used "+ThrowTrajectory.Speed(native).ToString("F1")+"), hand "+handSpeed.ToString("F2")+" -> "+v.Length().ToString("F2")+" m/s");}
        return new Vector3(v.X,v.Y,v.Z);
    }
    private bool reportedLively;
    // 0.1.159: the swing's heading turned part of the way toward where the head looks (GrenadeThrow.TowardLook).
    private Vector3 AimedThrow(Vector3 velocity)
    {
        var look=rig.HeadRotation*Vector3.forward;
        var v=GrenadeThrow.TowardLook(ToN(velocity),ToN(look));
        return new Vector3(v.X,v.Y,v.Z);
    }
    // The hand's turn as it let go (the grenade tumbles on), none in the old
    // always-in-the-hand mode (not measured there).
    private Vector3 ThrowSpin(int side)=>GripMode==WeaponGripMode.Always?Vector3.zero:handSpin[side];
    // 0.1.138: the thrown grenade's own fuse sound (tick, tick, ... bang) is
    // silent until it explodes; the bang is heard.
    // 0.1.140: it ticked on and on after the bang: a few seconds after it the
    // grenade's fuse sound is stopped (GrenadeFuseLedger), and a sweep stops
    // any other instance of the grenade's fuse events still playing while
    // none of the player's grenades is live (enemies' live grenades are kept).
    private static readonly System.Collections.Generic.Dictionary<IntPtr,float> quietFuses=new();
    private static readonly GrenadeFuseLedger fuseLedger=new();
    private static readonly System.Collections.Generic.HashSet<string> fuseEvents=new(StringComparer.OrdinalIgnoreCase);
    private static float fuseSweepUntil=-1,nextFuseSweep;private static int fuseReports;
    internal const float StraySeconds=9f;
    private static readonly System.Collections.Generic.Dictionary<IntPtr,float> fuseFirstSeen=new();
    private static bool reportedFuse,reportedBlast;
    private static void NoteFuseEvent(string? path){if(!string.IsNullOrEmpty(path)&&fuseEvents.Count<8)fuseEvents.Add(path!);}
    private static IntPtr FuseHandle(DetonatingProjectile d){try{return d.explosionInst.handle;}catch(Exception){return IntPtr.Zero;}}
    private void QuietFuse(Projectile p)
    {
        DetonatingProjectile? d=null;try{d=p.TryCast<DetonatingProjectile>();}catch(Exception){}
        if(d==null)return;
        float now=Time.realtimeSinceStartup;
        if(quietFuses.Count>32)foreach(var k in new System.Collections.Generic.List<IntPtr>(quietFuses.Keys))if(quietFuses[k]<now)quietFuses.Remove(k);
        float delay=5;try{if(d.explosionDelay>0)delay=d.explosionDelay;}catch(Exception){}
        quietFuses[d.Pointer]=now+delay+3;
        bool playing=FuseVolume(d,0);
        string ev="";try{ev=d.explosionEvent;}catch(Exception){}
        NoteFuseEvent(ev);fuseLedger.Launch(d.Pointer,FuseHandle(d),now,delay);fuseSweepUntil=Math.Max(fuseSweepUntil,now+delay+60);
        if(!reportedFuse){reportedFuse=true;bool impact=false;try{impact=d.explodeOnImpact;}catch(Exception){}
            Bootstrap.Write("GRENADE fuse sound "+ev+" playing="+playing+" delay="+delay.ToString("F1")+" onImpact="+impact+": silent until the explosion, stopped "+GrenadeFuseLedger.Tail.ToString("F1")+" s after it");}
    }
    private static bool FuseVolume(DetonatingProjectile d,float volume)
    {
        try{var i=d.explosionInst;if(i.handle==IntPtr.Zero)return false;i.setVolume(volume);return true;}catch(Exception){return false;}
    }
    private static void GrenadeSoundStarted(DetonatingProjectile __instance)
    {
        if(__instance!=null&&quietFuses.TryGetValue(__instance.Pointer,out float until)&&Time.realtimeSinceStartup<until)FuseVolume(__instance,0);
    }
    private static void GrenadeExploding(DetonatingProjectile __instance)
    {
        if(__instance==null)return;
        var handle=FuseHandle(__instance);
        bool ours=fuseLedger.Exploded(__instance.Pointer,handle,Time.realtimeSinceStartup);
        if(quietFuses.Remove(__instance.Pointer))FuseVolume(__instance,1);
        if(ours&&!reportedBlast){reportedBlast=true;Bootstrap.Write("GRENADE exploded: its fuse sound "+(handle!=IntPtr.Zero?"plays (the bang)":"is gone")+"; stopped in "+GrenadeFuseLedger.Tail.ToString("F1")+" s");}
    }
    // Every frame (cheap): the due fuse sounds stopped; twice a second, while
    // a grenade was thrown in the last minute and none of the player's is
    // live, any stray instance of the fuse events.
    private void TickFuses()
    {
        float now=Time.realtimeSinceStartup;
        // 0.1.140: the game's own in-hand fuse sound is never wanted (the pin's
        // click is ours): the game starts it for the throw (the pin is set for
        // the launch) and stops it only in its own second launch, which is
        // blocked now - so it ticked on for ever. Stopped whenever it runs.
        SilenceGameFuse(grenadeThrow);if(lastThrown!=null&&now-lastThrownAt<15)SilenceGameFuse(lastThrown);else lastThrown=null;
        try
        {
            if(fuseLedger.Count>0)
            {
                var due=fuseLedger.Due(now);
                if(due.Count>0){int n=0;foreach(var h in due)if(StopFuse(h))n++;if(n>0&&fuseReports++<20)Bootstrap.Write("GRENADE fuse sound stopped after the bang ("+n+" instance"+(n>1?"s":"")+")");}
            }
            if(fuseEvents.Count==0||now>fuseSweepUntil||now<nextFuseSweep)return;
            nextFuseSweep=now+.5f;
            if(now<fuseLedger.LiveUntil||grenadeGesture.PinPulled||leftGrenadeGesture.PinPulled||clickLive)return;
            var playing=new System.Collections.Generic.List<(FMOD.Studio.EventInstance inst,string path)>();
            var seen=new System.Collections.Generic.HashSet<IntPtr>();
            foreach(var path in fuseEvents)
            {
                var desc=FMODUnity.RuntimeManager.GetEventDescription(path);
                if(!desc.isValid()||desc.getInstanceList(out var list)!=FMOD.RESULT.OK||list==null)continue;
                for(int i=0;i<list.Length;i++)
                {
                    var inst=list[i];if(inst.handle==IntPtr.Zero)continue;
                    if(inst.getPlaybackState(out var state)!=FMOD.RESULT.OK||state==FMOD.Studio.PLAYBACK_STATE.STOPPED)continue;
                    // Only one seen playing longer than any fuse and its bang
                    // (an enemy's grenade just thrown or just exploded is younger;
                    // a looping tick restarts its timeline, so the time it was
                    // first seen counts).
                    seen.Add(inst.handle);
                    if(!fuseFirstSeen.TryGetValue(inst.handle,out float first)){fuseFirstSeen[inst.handle]=now;continue;}
                    if(now-first<StraySeconds)continue;
                    playing.Add((inst,path));
                }
            }
            foreach(var gone in new System.Collections.Generic.List<IntPtr>(fuseFirstSeen.Keys))if(!seen.Contains(gone))fuseFirstSeen.Remove(gone);
            if(playing.Count==0)return;
            // Live grenades of others (enemies) keep their fuse.
            var keep=new System.Collections.Generic.HashSet<IntPtr>();
            foreach(var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<DetonatingProjectile>()))
            {
                var d=o.TryCast<DetonatingProjectile>();if(d==null||!d.isActiveAndEnabled)continue;
                if(fuseLedger.Known(d.Pointer)&&!fuseLedger.Live(d.Pointer,now))continue;
                var h=FuseHandle(d);if(h!=IntPtr.Zero)keep.Add(h);
            }
            int stopped=0;string names="";
            foreach(var (inst,path) in playing)
            {
                if(keep.Contains(inst.handle))continue;
                try{inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);inst.release();stopped++;if(names.IndexOf(path,StringComparison.Ordinal)<0)names+=(names.Length>0?", ":"")+path;}catch(Exception){}
            }
            if(stopped>0&&fuseReports++<20)Bootstrap.Write("GRENADE stray fuse sound stopped: "+stopped+" instance"+(stopped>1?"s":"")+" of "+names+" still playing with no live grenade of the player");
        }
        catch(Exception ex){fuseSweepUntil=-1;Bootstrap.Warn("GRENADE fuse sweep: "+ex.Message);}
    }
    private ThrowingComponent? lastThrown;private float lastThrownAt=-10;private static bool reportedGameFuse;
    private static void SilenceGameFuse(ThrowingComponent? t)
    {
        if(t==null)return;
        try
        {
            var i=t.m_timerInst;if(i.handle==IntPtr.Zero)return;
            if(i.getPlaybackState(out var state)==FMOD.RESULT.OK&&state==FMOD.Studio.PLAYBACK_STATE.STOPPED)return;
            i.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);i.release();t.m_timerInst=default;
            if(!reportedGameFuse){reportedGameFuse=true;Bootstrap.Write("GRENADE the game's own fuse sound was running in the hand (started for the throw): stopped");}
        }
        catch(Exception){}
    }
    private static bool StopFuse(IntPtr handle)
    {
        if(handle==IntPtr.Zero)return false;
        try
        {
            var inst=new FMOD.Studio.EventInstance{handle=handle};
            if(inst.getPlaybackState(out var state)!=FMOD.RESULT.OK||state==FMOD.Studio.PLAYBACK_STATE.STOPPED)return false;
            inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);return true;
        }
        catch(Exception){return false;}
    }
    // 0.1.133: the pin's click is a short instance of the game's fuse event
    // of our own, cut off at once (IMMEDIATE). The game's own fuse sound is
    // not started any more: stopped after its click it ran on to its
    // explosion (the second blast heard on every throw).
    // 0.1.138: 0.2 s (was 0.3: a tick could still be heard after the click).
    internal const float ClickSeconds=.2f;
    private void PinClick(ThrowingComponent? t,Vector3 at)
    {
        if(t==null)return;
        try
        {
            string path=t.m_timerEvent??"";NoteFuseEvent(path);
            if(!reportedFuseEvent){reportedFuseEvent=true;Bootstrap.Write("GRENADE pin click event="+path+" (own instance, cut off after "+ClickSeconds.ToString("F1")+" s; the game's fuse sound is not used)");}
            StopClick();
            if(path.Length==0)return;
            var i=FMODUnity.RuntimeManager.CreateInstance(path);
            i.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(at));
            i.start();clickInstance=i;clickLive=true;clickStopAt=Time.realtimeSinceStartup+ClickSeconds;
        }
        catch(Exception ex){Bootstrap.Warn("GRENADE pin sound: "+ex.Message);}
    }
    private void StopClick()
    {
        clickStopAt=-1;if(!clickLive)return;clickLive=false;
        try{clickInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);clickInstance.release();}catch(Exception){}
    }
    // The game's own fuse sound, if it plays: cut off at once (no explosion at its end).
    private static void StopGameFuse(ThrowingComponent? t)
    {
        if(t==null)return;
        try{var i=t.m_timerInst;if(i.handle!=IntPtr.Zero){i.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);i.release();t.m_timerInst=default;}}catch(Exception){}
    }
    // After a launch: the thrown grenade has its own fuse; the one in the
    // game's hands must not keep a pulled pin (its own cooking timer and fuse
    // sound, or a live grenade dropped when the hand is emptied).
    private bool reportedThrowState;
    private ThrowingComponent? settleThrow;private float settleAt=-1;private string settleWho="";
    // The game launches a moment after the release; settled once it has.
    private void SettleAfterLaunch(ThrowingComponent? t,string who){settleThrow=t;settleAt=Time.realtimeSinceStartup;settleWho=who;}
    private void SettleThrown(ThrowingComponent? t,string who)
    {
        if(t==null)return;
        try
        {
            if(!reportedThrowState){reportedThrowState=true;Bootstrap.Write("GRENADE after the launch ("+who+"): pinPulled="+t.pinPulled+" startTimer="+t.startTimer+" startedTimer="+t.startedTimer+" timer="+t.weaponTimer.ToString("F2")+" detonating="+t.isDetonatingProjectile+" fuseSound="+(t.m_timerInst.handle!=IntPtr.Zero));}
            t.pinPulled=false;t.startTimer=false;t.startedTimer=false;
        }
        catch(Exception ex){Bootstrap.Warn("GRENADE after the launch: "+ex.Message);}
        StopGameFuse(t);
    }
    // Every frame: the click stops before the ticking; the hand empties after
    // a throw (the game draws the next grenade, or another weapon after the last).
    private void TickGrenadeAfter()
    {
        TickFuses();
        float now=Time.realtimeSinceStartup;
        if(clickStopAt>0&&now>=clickStopAt)StopClick();
        if(settleThrow!=null&&(launchUntil<=0||now-settleAt>1.5f)){var t=settleThrow;settleThrow=null;SettleThrown(t,settleWho);}
        if(emptyAfterThrowAt<0||now<emptyAfterThrowAt)return;
        if(now-thrownAt>3||gripTakeAt>thrownAt||inventory==null){emptyAfterThrowAt=-1;return;}
        if(inventory.isInTransit)return;
        var current=inventory.currentEquipable;
        if(current==null||current.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist){emptyAfterThrowAt=-1;return;}
        bool ok;
        if(settleThrow!=null&&launchUntil>0)return;   // not launched yet
        if(current.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Grenade)SettleThrown(grenadeThrow??current.GetComponent(Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>(),"hand emptied");
        try{ok=inventory.TrySelectSlot(PlayerEquipableInventory.ActiveEquipmentSlot.Fist,true,true,true,false,true);}
        catch(Exception ex){ok=false;Bootstrap.Warn("GRENADE empty hand: "+ex.Message);}
        if(!ok)return;
        emptyAfterThrowAt=-1;
        bool knife=emptyAfterThrowSlot==PlayerEquipableInventory.ActiveEquipmentSlot.Knife;
        Bootstrap.Write((knife?"KNIFE":"GRENADE")+" thrown: the right hand is empty (the game had drawn "+EquipmentProfile.ForSlot((int)current.slot)+"); left on the chest: "+Math.Max(0,holsters?.CountOf((int)emptyAfterThrowSlot)??0));
    }
    private void ResetGrenade(string why)
    {
        if(grenadeGesture.PinPulled)Bootstrap.Write("GRENADE pin state cleared ("+why+")");
        try{if(grenadeThrow!=null)grenadeThrow.pinPulled=false;}catch(Exception){}
        StopGameFuse(grenadeThrow);
        if(visual!=null&&profile=="grenade")visual.PinPulled=false;
        grenadeGesture.Reset();grenadeThrow=null;
    }
    private int GrenadeCount()
    {
        var a=grenadeAmmo??ammo;
        try{return a==null?-1:a.PrimaryMagazineAmmoCount+Math.Max(0,a.PrimaryReserveAmmoCount);}catch(Exception){return -1;}
    }
    // The game should take one grenade per throw. If it did not (this launch
    // path skips its throw animation), take it once, from the reserve first.
    private void CheckGrenadeAmmo()
    {
        if(grenadeCheckAt<0||Time.realtimeSinceStartup<grenadeCheckAt)return;
        grenadeCheckAt=-1;int before=grenadeCountBefore,after=GrenadeCount();grenadeCountBefore=-1;
        var g=grenadeAmmo??ammo;
        try
        {
            if(before<=0||after<0||g==null)return;
            if(after<before){Bootstrap.Write("GRENADE count "+before+" -> "+after+" (game)");return;}
            if(g.ammoPool.IsInfinite){Bootstrap.Write("GRENADE count unchanged (infinite ammo)");return;}
            if(g.PrimaryReserveAmmoCount>0)g.ammoPool.TryRemoveAmmo(g.primaryAmmoType,1);
            else g.SetAmmo(Math.Max(0,g.PrimaryMagazineAmmoCount-1),g.SecondaryMagazineAmmoCount);
            try{g.TriggerEventAmmoPoolChanged();}catch(Exception){}
            Bootstrap.Write("GRENADE count "+before+" -> "+GrenadeCount()+" (taken by the mod)");
        }
        catch(Exception ex){Bootstrap.Warn("GRENADE count: "+ex.Message);}
        finally{grenadeAmmo=null;}
    }
}
