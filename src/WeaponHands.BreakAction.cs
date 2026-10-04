using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.194: the double-barrelled shotgun's hand reload. The B of the
// hand holding it (Y for the left hand) opens it: the barrels drop and only
// the fired cases fly out, an unfired shell stays in its chamber. A shell is
// taken from the belt with the other hand's trigger (held) and pushed into an
// open empty chamber; letting go of the trigger drops it. A small flick of the
// gun up and down swings the barrels shut by their weight (or B again). Open,
// it does not fire. With the manual reload on (VR SETTINGS).
internal sealed partial class WeaponHands
{
    private readonly Dictionary<int,BreakActionState> breakStates=new();
    private float breakTickAt=-1,breakPrevAt=-1,breakPrevPitch;private Vector3 breakPrevGun;
    private BreakActionState? CurrentBreak
    {
        get
        {
            if(weapon==null||visual?.BreakAction!=true)return null;
            int id=weapon.GetInstanceID();
            if(!breakStates.TryGetValue(id,out var st))
            {
                st=new BreakActionState();int rounds=0;try{rounds=ammo?.PrimaryMagazineAmmoCount??0;}catch(Exception){}
                st.Load(rounds);breakStates[id]=st;
                Bootstrap.Write("BREAK ACTION "+weapon.name+" by hand: "+rounds+" loaded; B/Y opens (fired cases out, unfired stay), a shell from the belt into an open chamber, a flick up and down (or B/Y) shuts it");
            }
            return st;
        }
    }
    internal bool BreakReady=>ManualReady&&visual?.BreakAction==true&&CurrentBreak!=null;
    internal bool BreakBlocksFire=>BreakReady&&CurrentBreak!.BlocksFire;
    internal bool BreakHolding=>BreakReady&&CurrentBreak!.Holding;
    // 0.1.221: open with an empty chamber and no shell in hand: the grip at the belt takes one.
    private bool BreakWantsShell(Vector3 hand)=>CurrentBreak is BreakActionState st&&st.Open&&!st.Holding&&st.AnyEmpty&&pouch!=null&&pouch.NearShell(hand);
    // A double-barrelled gun (the game's hunting shotgun) by its name.
    internal static bool BreakGun(Equipable? w)
    {
        if(w==null)return false;
        try{var n=(w.identifier??"")+" "+w.name;return n.Contains("hunting",StringComparison.OrdinalIgnoreCase);}catch(Exception){return false;}
    }
    // A shotgun held as a copy that breaks open (it is not pumped).
    private bool CopyBreaks(int s)=>holsters!=null&&copyKey[s]>=0&&BreakGun(holsters.WeaponOf(copyKey[s]));
    private string BreakLabel
    {
        get
        {
            var st=CurrentBreak;if(st==null)return "";
            if(st.Open)return st.Holding?"INSERT":st.AnyEmpty?"POUCH":"CLOSE";
            return st.LiveRounds==0?"OPEN":"";
        }
    }
    // The chamber the held shell goes into: open, empty, nearest to its tip.
    private int BreakTarget(Vector3 tipFitted,out float distance)
    {
        distance=float.PositiveInfinity;int best=-1;var st=CurrentBreak;if(st==null||visual==null)return -1;
        for(int c=0;c<2;c++)if(st.Chambers[c]==BreakChamber.Empty){float d=Vector3.Distance(tipFitted,visual.BreakMouth(c));if(d<distance){distance=d;best=c;}}
        return best;
    }
    private bool BreakAccess(Vector3 probe,out Vector3 point,out Vector3 axis)
    {
        point=Vector3.zero;axis=Vector3.forward;var st=CurrentBreak;var v=visual;
        if(st==null||v==null||!st.Open||!st.Holding)return false;
        var m=v.FittedToWorld;int c=BreakTarget(m.inverse.MultiplyPoint3x4(probe),out _);if(c<0)return false;
        point=m.MultiplyPoint3x4(v.BreakMouth(c));axis=m.MultiplyVector(v.BreakAxis).normalized;
        return (probe-point).sqrMagnitude<.30f*.30f;
    }
    internal const float BreakInsertReach=.05f;
    // 0.1.241: no flick shuts the gun for a moment after a shell is taken, let go or put in.
    internal const float BreakQuietAfterStep=.6f;private float breakQuietUntil=float.NegativeInfinity;
    private void TickBreak(bool mirrored,Vector3 hand,Quaternion rotation,Vector3 world,HandControls gun,HandControls loader,PoseValue gunPose,float now)
    {
        var st=CurrentBreak;var v=visual;var a=ammo;if(st==null||v==null||a==null)return;
        int fired=st.Observe(a.PrimaryMagazineAmmoCount);
        if(fired>0)Bootstrap.Write("BREAK ACTION shot: "+fired+" barrel"+(fired>1?"s":"")+" fired; chambers right="+st.Chambers[0]+" left="+st.Chambers[1]);
        var m=v.FittedToWorld;
        // The gun hand's button: open (the fired cases fly out) or shut.
        if((gun.Down&HandControls.B)!=0)
        {
            if(!st.Open)
            {
                var ejected=st.OpenGun();
                foreach(int c in ejected)EjectBreakCase(c);
                ResetNativeReload("open");
                reloadAudio?.Play(ReloadAction.RackBack,profile,m.MultiplyPoint3x4(v.BreakMouth(0)),rig.HeadPosition,rig.HeadRotation);
                rig.ReloadHaptics(ReloadAction.RackBack,!mirrored);
                Bootstrap.Write("BREAK ACTION opened: "+ejected.Length+" fired case"+(ejected.Length==1?"":"s")+" out, "+st.LiveRounds+" unfired stay; chambers right="+st.Chambers[0]+" left="+st.Chambers[1]);
            }
            else if(st.CloseGun())BreakClosed("B",mirrored);
        }
        // The flick that shuts it.
        // 0.1.241: the gun hand's rise in the room (not moved by walking, a crouch or the head).
        var gunAt=rig.SampleTrackedHand(!mirrored,out var trackedGun)?CameraRig.UnityPosition(trackedGun):CameraRig.UnityPosition(gunPose);var forward=m.MultiplyVector(Vector3.forward).normalized;
        float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;float dt=now-breakPrevAt;
        if(breakPrevAt>0&&dt>.0001f&&dt<.1f)
        {
            float up=(gunAt.y-breakPrevGun.y)/dt,raise=(pitch-breakPrevPitch)/dt;
            // Not while the other hand is at the belt, holds a shell, or has just put one in.
            bool quiet=st.Holding||now<breakQuietUntil||pouch!=null&&pouch.NearShell(world);
            if(st.Flick(now,up,raise,quiet))BreakClosed("a flick up and down (the barrels swung shut by their weight)",mirrored);
        }
        breakPrevAt=now;breakPrevGun=gunAt;breakPrevPitch=pitch;
        // 0.1.221: the other hand's grip (was its trigger) takes and holds a shell.
        bool down=(loader.Down&AmmoButton)!=0,held=(loader.Held&AmmoButton)!=0;
        // A shell from the belt (the other hand's grip at it).
        if(!st.Holding&&down&&pouch!=null&&pouch.NearShell(world))
        {
            bool infinite=false;try{infinite=a.ammoPool.IsInfinite;}catch(Exception){}
            int got=0;
            try{if(infinite)got=1;else if(a.PrimaryReserveAmmoCount>0)got=a.ammoPool.TryRemoveAmmo(a.primaryAmmoType,1);}catch(Exception ex){Bootstrap.Warn("BREAK ACTION shell from the belt: "+ex.Message);}
            if(got>0&&st.Take())
            {
                breakQuietUntil=now+BreakQuietAfterStep;
                NotifyAmmo();heldAmmoFrame=-10;ContactRig.Current?.ResetHand(mirrored);
                reloadAudio?.Play(ReloadAction.TakeSupply,profile,world,rig.HeadPosition,rig.HeadRotation);rig.ReloadHaptics(ReloadAction.TakeSupply,mirrored);
            }
            else rig.ResistanceHaptics(.3f,mirrored);
        }
        else if(st.Holding&&!held)
        {
            // Let go of: the shell back to the reserve, drawn falling.
            v.AmmunitionPose(hand,rotation,out var at,out var turn,mirrored);
            Refund(st.LetGo());Drop(at,turn,throwVelocity);heldAmmoFrame=-10;ContactRig.Current?.ResetHand(mirrored);breakQuietUntil=now+BreakQuietAfterStep;
        }
        // Pushed into an open empty chamber (along its barrel).
        if(st.Holding&&st.Open&&st.Swing>.75f)
        {
            var inverse=m.inverse;
            var tip=inverse.MultiplyPoint3x4(v.AmmunitionTip(hand,rotation,mirrored));
            var along=inverse.MultiplyVector(v.AmmunitionForward(rotation,mirrored)).normalized;
            int c=BreakTarget(tip,out float distance);
            if(c>=0&&distance<BreakInsertReach&&Vector3.Dot(along,v.BreakAxis)>.35f&&st.Insert(c))
            {
                breakQuietUntil=now+BreakQuietAfterStep;
                try{SetMagazine(st.LiveRounds);}catch(Exception ex){Bootstrap.Warn("BREAK ACTION chamber: "+ex.Message);}
                heldAmmoFrame=-10;ContactRig.Current?.ResetHand(mirrored);
                reloadAudio?.Play(ReloadAction.Insert,profile,m.MultiplyPoint3x4(v.BreakMouth(c)),rig.HeadPosition,rig.HeadRotation);
                rig.ReloadHaptics(ReloadAction.Insert,mirrored);
                Bootstrap.Write("BREAK ACTION shell into the "+(c==0?"right":"left")+" chamber; loaded="+st.LiveRounds+" reserve="+a.PrimaryReserveAmmoCount);
            }
        }
        if(st.BlocksFire)StopOwnedFire();
    }
    private void BreakClosed(string how,bool mirrored)
    {
        var st=CurrentBreak;var v=visual;if(st==null||v==null)return;
        try{if(ammo!=null&&ammo.PrimaryMagazineAmmoCount!=st.LiveRounds)SetMagazine(st.LiveRounds);}catch(Exception){}
        DisarmFireTrigger();ReleaseNativeFire("closed");
        reloadAudio?.Play(ReloadAction.Chamber,profile,v.FittedToWorld.MultiplyPoint3x4(v.BreakMouth(0)),rig.HeadPosition,rig.HeadRotation);
        rig.ReloadHaptics(ReloadAction.Chamber,!mirrored);
        Bootstrap.Write("BREAK ACTION shut by "+how+"; loaded="+st.LiveRounds);
    }
    // A fired case out of chamber c: back over the shoulder, falling.
    private void EjectBreakCase(int c)
    {
        var v=visual;if(v?.Ammunition==null)return;
        try
        {
            var m=v.FittedToWorld;var at=m.MultiplyPoint3x4(v.BreakMouth(c));
            var velocity=m.MultiplyVector(-v.BreakAxis*1.4f+Vector3.up*1.1f+Vector3.right*(c==0?.25f:-.25f));
            drops??=new ReloadDrops();drops.Add(v.Ammunition,at,m.rotation,velocity);
        }
        catch(Exception ex){Bootstrap.Warn("BREAK ACTION fired case: "+ex.Message);}
    }
    // Every frame: the barrels' swing, the chambers' shells, the hints.
    private void RenderBreak(PoseValue left)
    {
        var st=CurrentBreak;var v=visual;if(st==null||v==null)return;
        float now=Time.realtimeSinceStartup;
        if(ammo!=null)st.Observe(ammo.PrimaryMagazineAmmoCount);
        if(breakTickAt>0)st.Tick(Math.Min(.1f,now-breakTickAt));breakTickAt=now;
        v.ReloadPose(st.Holding,false,false,0,CameraRig.UnityPosition(left),GloveVisual.Rotation(left,false));
        v.PoseBreak(st.Swing,st.Chambers[0]!=BreakChamber.Empty,st.Chambers[1]!=BreakChamber.Empty);
        if(foreEndOnly||PrimaryLeft&&!MirroredManual){HideHints();return;}
        try
        {
            var m=v.FittedToWorld;
            if(st.Open&&st.Holding)
            {
                int c=-1;float best=float.PositiveInfinity;
                for(int i=0;i<2;i++)if(st.Chambers[i]==BreakChamber.Empty){float d=(m.MultiplyPoint3x4(v.BreakMouth(i))-rig.HeadPosition).sqrMagnitude;if(c<0||d<best){c=i;best=d;}}
                if(c<0){HideHints();return;}
                outline?.Hide();glow??=new ReloadGlow();glow.Show(m.MultiplyPoint3x4(v.BreakMouth(c)),.014f);return;
            }
            bool rounds=false;try{rounds=ammo!=null&&(ammo.ammoPool.IsInfinite||ammo.PrimaryReserveAmmoCount>0);}catch(Exception){}
            if(st.Open&&st.AnyEmpty&&rounds&&pouch!=null&&pouch.Shown)
            {
                bool drawn=false;
                if(pouch.Shape!=null&&!outlineFailed){try{outline??=new ReloadOutline();drawn=outline.Show(pouch.Shape,pouch.ShapeToWorld);}catch(Exception ex){outlineFailed=true;outline?.Dispose();outline=null;Bootstrap.Warn("RELOAD OUTLINE unavailable (small glow instead): "+ex.Message);}}
                if(drawn){glow?.Hide();return;}
                outline?.Hide();glow??=new ReloadGlow();glow.Show(pouch.Center,.02f);return;
            }
            if(!st.Open&&st.LiveRounds==0&&rounds)
            {
                outline?.Hide();glow??=new ReloadGlow();glow.Show(m.MultiplyPoint3x4((v.BreakMouth(0)+v.BreakMouth(1))*.5f),.018f);return;
            }
            HideHints();
        }
        catch(Exception ex){HideHints();if(now>=nextGlowError){nextGlowError=now+10;Bootstrap.Warn("Break action hint: "+ex.Message);}}
    }
    private void CancelBreak()
    {
        var st=CurrentBreak;if(st==null||!st.Holding)return;
        Refund(st.LetGo());
    }
}
