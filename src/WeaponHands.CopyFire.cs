using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.130: both hands fire at once. The game has one weapon in play; a
// weapon held in the other hand fires through its own fire component (the
// game's projectile, damage, ammunition and sound), aimed along the weapon in
// that hand. Another weapon of an owned kind (taken from the ground) fires
// through the owned weapon with its own magazine. If the game refuses such a
// shot for a kind of weapon, that weapon's trigger brings it into play
// instead (as in 0.1.128).
internal sealed partial class WeaponHands
{
    private readonly float[] copyNextShot=new float[2];
    private static readonly HashSet<string> copyFireRefused=new(),copyFireReported=new();
    private bool copyShooting,copyLaunched;private FireComponent? copyShotFire;private Equipable? copyShotWeapon;
    private Vector3 copyShotOrigin,copyShotForward,copyShotUp;private Transform? copyShotAnchor;
    // The muzzle of each kind in the fitted frame (from the game's weapon when it was in play).
    private static readonly Dictionary<string,Vector3> muzzleOffsets=new();
    private static bool AutomaticKind(string p)=>p is "uzi" or "ak47" or "m16" or "m60" or "rifle" or "heavy";
    private static Vector3 DefaultMuzzle(string p)=>p=="pistol"?new(0,.045f,.14f):p=="revolver"?new(0,.045f,.18f):p=="shotgun"?new(0,.04f,.62f):new(0,.04f,.5f);
    internal static FireComponent? FireOf(Equipable e)
    {
        var script=e.primaryFireUsageComponentScript;var candidate=script==null?null:script.TryCast<FireComponent>();
        if(candidate!=null&&candidate.TryCast<DualWieldComponent>()==null&&candidate.projectileOrigin!=null)return candidate;
        foreach(var c in e.GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<FireComponent>(),true))
        {var f=c.TryCast<FireComponent>();if(f!=null&&f.TryCast<DualWieldComponent>()==null&&f.projectileOrigin!=null&&f.TryCast<ThrowingComponent>()==null)return f;}
        return null;
    }
    private static float ShotInterval(Equipable w,bool automatic)
    {
        float rate=0;try{rate=w.CurrentEquipableParameters?.fireRate??0;}catch(Exception){}
        float interval=!float.IsFinite(rate)||rate<=0?(automatic?.1f:.15f):rate>30?60f/rate:1f/rate;
        return Math.Clamp(interval,automatic?.05f:.1f,1.5f);
    }
    private void RefuseCopyFire(string p,string why)
    {
        if(copyFireRefused.Add(p))Bootstrap.Write("COPY FIRE "+p+": the game does not fire it outside play ("+why+"); its trigger brings it into play instead");
    }
    // True when the trigger was used here (a shot, the fire rate, an empty
    // magazine); false: bring the weapon into play instead.
    private bool CopyFire(int s,HolsterCopy copy,bool down,bool held,float now)
    {
        string p=copyProfile[s];int key=copyKey[s];bool loose=BodyHolsters.IsLoose(key);bool automatic=AutomaticKind(p);
        if(copyFireRefused.Contains(p))return loose;   // another one of a kind never fires by itself
        if(!down&&!automatic)return true;
        if(now<copyNextShot[s]||holsters==null||!copy.Shown)return true;
        var w=holsters.WeaponOf(key);var f=w==null?null:FireOf(w);
        if(w==null||f==null){RefuseCopyFire(p,"no fire component");return loose;}
        var a=f.ammoManagementComponent;
        if(a==null){RefuseCopyFire(p,"no ammunition component");return loose;}
        bool infinite=false;try{infinite=a.ammoPool!=null&&a.ammoPool.IsInfinite;}catch(Exception){}
        int magazine=loose?holsters.LooseMagazine(key):a.PrimaryMagazineAmmoCount;
        if(magazine<=0)
        {
            // 0.1.131: empty: it reloads by itself (it stays in this hand).
            if(down)StartHandReload(s);
            return true;
        }
        if(HandReloading(s))return true;
        // 0.1.185: a shotgun copy fires again once pumped (as the game's shotgun).
        if(CopyUnpumped(s,down))return true;
        var muzzleLocal=muzzleOffsets.TryGetValue(p,out var mo)?mo:DefaultMuzzle(p);
        var rotation=copy.Rotation;
        copyShotOrigin=copy.Position+rotation*muzzleLocal;copyShotForward=rotation*Vector3.forward;copyShotUp=rotation*Vector3.up;
        copyShotAnchor=copy.Anchor(muzzleLocal);
        int saved=a.PrimaryMagazineAmmoCount,secondary=a.SecondaryMagazineAmmoCount;
        if(loose)a.SetAmmo(magazine,secondary);
        int before=a.PrimaryMagazineAmmoCount;
        f._ProjectileOriginPositionAfterIK_k__BackingField=copyShotOrigin;f._ProjectileOriginForwardAfterIK_k__BackingField=copyShotForward;f._ProjectileOriginUpAfterIK_k__BackingField=copyShotUp;
        copyShooting=true;copyLaunched=false;copyShotFire=f;copyShotWeapon=w;copyShotProfile=p;
        string? error=null;
        try
        {
            f.canFire=true;
            var auto=f.TryCast<AutoFireComponent>();
            if(auto!=null)auto.FireBullet();else f.ExecuteFireTask();
        }
        catch(Exception ex){error=ex.Message;}
        finally{copyShooting=false;}
        int after=before;
        try
        {
            after=a.PrimaryMagazineAmmoCount;
            // The game launched it without counting the round: count it once.
            if(copyLaunched&&after>=before&&!infinite){after=before-1;a.SetAmmo(after,a.SecondaryMagazineAmmoCount);}
            if(loose){holsters.SetLooseMagazine(key,after);a.SetAmmo(saved,a.SecondaryMagazineAmmoCount);}
        }
        catch(Exception ex){error??=ex.Message;}
        if(!copyLaunched)
        {
            // Nothing left the barrel: put back what the game may have taken.
            try{if(!loose&&after<before)a.SetAmmo(before,a.SecondaryMagazineAmmoCount);}catch(Exception){}
            RefuseCopyFire(p,error??"no projectile");return loose;
        }
        copyNextShot[s]=now+ShotInterval(w,automatic);CopyShotPumped(s);
        if(s==0)rig.LeftShotHaptics();else rig.ShotHaptics(p,false);
        // 0.1.132: its sound, the kick and the slide/bolt cycling back (locked
        // back on a pistol's last round).
        CopyShotSound(s,w,copyShotOrigin,now);
        copy.Shot(now);copy.LockBack(after<=0&&p is "pistol");
        if(copyFireReported.Add(p))Bootstrap.Write("COPY FIRE "+Side(s)+" hand "+p+(loose?" (another one)":"")+": fired by itself while the game's weapon is "+(weapon!=null?profile:"none")+" (both hands fire at once); magazine "+before+" -> "+after);
        return true;
    }
    // 0.1.131: a weapon in a hand that is not the game's right-hand weapon
    // (a copy, another one of a kind, the game's weapon in the left hand)
    // reloads by itself: its trigger on an empty magazine (or the right B for
    // the left hand's weapon) fills it from the reserve after a moment (the
    // game's own reload needs its arms animation, which a gun in the left hand
    // never plays). The right hand's own weapon keeps the hand reload.
    private readonly float[] handReloadAt={-1,-1};private readonly int[] handReloadKey={-1,-1};private readonly bool[] reserveEmptyReported=new bool[2];
    internal const float HandReloadSeconds=1.6f;
    private AmmoManagementComponent? HandAmmo(int s,out bool loose,out int key)
    {
        loose=false;key=-1;
        if(copyKey[s]>=0&&holsters!=null){key=copyKey[s];loose=BodyHolsters.IsLoose(key);var w=holsters.WeaponOf(key);return w==null?null:FireOf(w)?.ammoManagementComponent;}
        if(s==0&&PrimaryLeft&&weapon!=null){key=(int)weapon.slot;return ammo;}
        // 0.1.133: the right hand's gun while the left hand holds a weapon of its own.
        if(s==1&&!PrimaryLeft&&weapon!=null&&HasTrackedWeapon){key=(int)weapon.slot;return ammo;}
        return null;
    }
    internal bool HandReloading(int s)=>handReloadAt[s]>0;
    // 0.1.136: only with the automatic reload (VR SETTINGS / config
    // ManualReload off), or for kinds that have no hand reload: with the
    // manual reload a weapon in a hand while the other hand holds one is not
    // reloaded by itself - free the other hand and reload it by hand.
    internal static bool AutoHandReload(string p)=>!WeaponOptions.ManualReload.Value||!(EquipmentProfile.Manual(p)||p=="revolver");
    private readonly bool[] manualEmptyReported=new bool[2];
    // 0.1.142: asked for with the hand's own reload button (left Y / right
    // B): reloaded by itself even with the manual reload (the other hand is
    // not free to do it).
    private void StartHandReload(int s,bool asked=false)
    {
        // 0.1.183: a pistol whose hands are both full reloads against the chest (ChestReload).
        if(handReloadAt[s]>0||ChestReloads(s))return;
        var a=HandAmmo(s,out bool loose,out int key);if(a==null)return;
        string p=copyKey[s]>=0?copyProfile[s]:profile;
        if(!asked&&!AutoHandReload(p))
        {
            rig.PunchHaptics(s==1);
            if(!manualEmptyReported[s]){manualEmptyReported[s]=true;Bootstrap.Write("HANDS "+Side(s)+" hand "+p+" is empty; the manual reload is on: it is not reloaded by itself (free the other hand and reload it by hand, or press "+(s==0?"the left Y":"the right B")+")");}
            return;
        }
        manualEmptyReported[s]=false;
        try
        {
            int max=Math.Max(1,a.MaxPrimaryMagazineAmmoCount),have=loose?holsters!.LooseMagazine(key):a.PrimaryMagazineAmmoCount;
            if(have>=max)return;
            bool infinite=a.ammoPool!=null&&a.ammoPool.IsInfinite;
            if(!infinite&&a.PrimaryReserveAmmoCount<=0)
            {
                rig.PunchHaptics(s==1);
                if(!reserveEmptyReported[s]){reserveEmptyReported[s]=true;Bootstrap.Write("HANDS "+Side(s)+" hand "+p+": no rounds left in the reserve");}
                return;
            }
            reserveEmptyReported[s]=false;handReloadAt[s]=Time.realtimeSinceStartup+HandReloadSeconds;handReloadKey[s]=key;
            reloadAudio??=new ReloadAudio();reloadAudio.PlayCue(p,0);rig.PunchHaptics(s==1);
            Bootstrap.Write("HANDS "+Side(s)+" hand "+p+(loose?" (another one)":"")+": reloading by itself ("+have+"/"+max+")");
        }
        catch(Exception ex){Bootstrap.Warn("HANDS reload: "+ex.Message);}
    }
    private void TickHandReload()
    {
        float now=Time.realtimeSinceStartup;
        for(int s=0;s<2;s++)
        {
            if(handReloadAt[s]<0||now<handReloadAt[s])continue;
            handReloadAt[s]=-1;
            var a=HandAmmo(s,out bool loose,out int key);if(a==null||key!=handReloadKey[s])continue;
            string p=copyKey[s]>=0?copyProfile[s]:profile;
            try
            {
                int max=Math.Max(1,a.MaxPrimaryMagazineAmmoCount),have=loose?holsters!.LooseMagazine(key):a.PrimaryMagazineAmmoCount,need=max-have;if(need<=0)continue;
                int got=a.ammoPool.IsInfinite?need:a.ammoPool.TryRemoveAmmo(a.primaryAmmoType,need);
                if(loose)holsters!.SetLooseMagazine(key,have+got);else a.SetAmmo(have+got,a.SecondaryMagazineAmmoCount);
                // 0.1.183: the magazine in, the slide forward (also a pistol's
                // magazine dropped against the chest before).
                KnownHandState(s)?.QuickLoad();
                if(copyKey[s]>=0&&holsters?.CopyOf(copyKey[s]) is HolsterCopy reloaded){reloaded.LockBack(false);reloaded.MagazineOut=false;}
                try{a.TriggerEventAmmoPoolChanged();}catch(Exception){}
                NotifyAmmo();rig.PunchHaptics(s==1);reloadAudio??=new ReloadAudio();reloadAudio.PlayCue(p,1);
                if(copyKey[s]<0){reload.ObserveRounds(have+got);ReleaseNativeFire("hand reload");}
                Bootstrap.Write("HANDS "+Side(s)+" hand "+p+" reloaded: "+(have+got)+"/"+max);
            }
            catch(Exception ex){Bootstrap.Warn("HANDS reload: "+ex.Message);}
        }
    }
    // 0.1.132: the shot sound of a weapon firing beside the game's one. The
    // game releases a put-away weapon's sound instances, so its own fire
    // sound stays silent: its fire event (with its weapon parameter) is
    // played here. A looping event is stopped after the last shot.
    private sealed class ShotEvent{internal string Path="";internal string Parameter="";internal float Value;}
    private static readonly Dictionary<IntPtr,ShotEvent?> shotEvents=new();
    private readonly FMOD.Studio.EventInstance[] copyLoop=new FMOD.Studio.EventInstance[2];private readonly bool[] copyLoopLive=new bool[2];private readonly float[] copyLoopStopAt=new float[2];
    private static ShotEvent? ShotEventOf(Equipable w)
    {
        if(shotEvents.TryGetValue(w.Pointer,out var cached))return cached;
        ShotEvent? e=null;
        try
        {
            var audio=w.GetComponentInChildren(Il2CppInterop.Runtime.Il2CppType.Of<AudioComponent>(),true)?.TryCast<AudioComponent>();
            if(audio!=null)
            {
                string path=!string.IsNullOrEmpty(audio.fireEqpEvent2D)?audio.fireEqpEvent2D:audio.firegEqpEvent3D??"";
                if(path.Length>0)
                {
                    e=new ShotEvent{Path=path};
                    var subs=audio.audioSubComponents;
                    if(subs!=null)for(int i=0;i<subs.Count;i++)
                    {
                        var sub=subs[i];if(sub==null)continue;
                        if(sub.TryCast<SingleFireSound>()==null&&sub.TryCast<AutoFireSound>()==null&&sub.TryCast<BurstFireSound>()==null)continue;
                        e.Parameter=BaseAudioSubComponent.eqpFmodParam??"";e.Value=sub.eqpFmodParamValue;break;
                    }
                }
            }
            Bootstrap.Write("COPY FIRE sound of "+w.name+": "+(e==null?"none found":e.Path+(e.Parameter.Length>0?" "+e.Parameter+"="+e.Value:"")));
        }
        catch(Exception ex){Bootstrap.Warn("COPY FIRE sound of "+w.name+": "+ex.Message);e=null;}
        shotEvents[w.Pointer]=e;return e;
    }
    private void CopyShotSound(int s,Equipable w,Vector3 at,float now)
    {
        var e=ShotEventOf(w);if(e==null)return;
        try
        {
            if(copyLoopLive[s]){copyLoop[s].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);copyLoop[s].release();copyLoopLive[s]=false;}
            var instance=FMODUnity.RuntimeManager.CreateInstance(e.Path);
            instance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(at));
            if(e.Parameter.Length>0)instance.setParameterByName(e.Parameter,e.Value,false);
            instance.start();
            bool once=true;
            try{if(instance.getDescription(out var d)==FMOD.RESULT.OK)d.isOneshot(out once);}catch(Exception){}
            if(once){instance.release();return;}
            copyLoop[s]=instance;copyLoopLive[s]=true;copyLoopStopAt[s]=now+.3f;
        }
        catch(Exception ex){Bootstrap.Warn("COPY FIRE sound: "+ex.Message);shotEvents[w.Pointer]=null;}
    }
    private void TickCopySound()
    {
        float now=Time.realtimeSinceStartup;
        for(int s=0;s<2;s++)
        {
            if(!copyLoopLive[s]||now<copyLoopStopAt[s])continue;
            copyLoopLive[s]=false;
            try{copyLoop[s].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);copyLoop[s].release();}catch(Exception){}
        }
    }
    private void WriteCopyMuzzle(FireComponent f)
    {
        f._ProjectileOriginPositionAfterIK_k__BackingField=copyShotOrigin;f._ProjectileOriginForwardAfterIK_k__BackingField=copyShotForward;f._ProjectileOriginUpAfterIK_k__BackingField=copyShotUp;
    }
    private bool CopyShotBy(FireComponent? f)=>copyShooting&&f!=null&&copyShotFire!=null&&f.Pointer==copyShotFire.Pointer;
}
