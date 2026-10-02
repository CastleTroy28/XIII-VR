using System;
using System.Collections.Generic;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.183: pistols in both hands reload each with its own hand.
// The reload button of the hand holding the pistol (left Y / right B) drops
// its magazine (the rounds left in it go back to the reserve; the slide stays
// back when it was empty); striking the pistol's grip against the chest puts
// a full magazine in at once and lets the slide go forward. Each hand by
// itself, both at the same time too. It is the reload of any pistol whose
// other hand is full (a weapon of its own, or a hostage in the left hand),
// and of another pistol held as a copy; a pistol alone with the other hand
// free keeps the belt reload. With the manual reload on (VR SETTINGS);
// without it pistols reload by themselves as before.
internal sealed partial class WeaponHands
{
    private readonly ChestStrike[] chestStrike={new(),new()};
    private readonly int[] chestKey={-1,-1};
    private readonly float[] chestChamberAt={-1,-1};
    private readonly bool[] chestEmptyReported=new bool[2],chestActive=new bool[2];
    private readonly Dictionary<IntPtr,AmmoManagementComponent> chestAmmo=new();
    private ReloadGlow? chestGlow;private bool chestGlowFailed;
    private float chestYaw;private bool chestYawSet;
    private string chestLabel="";private bool chestGameShown;
    // The game's pistol is in hand s (held by its handle).
    private bool GamePistolIn(int s)
    {
        if(weapon==null||!EquipmentProfile.ChestMagazine(profile)||ammo==null||foreEndOnly||inventory==null)return false;
        int key=GameKey;return key>=0&&key==(int)weapon.slot&&GameSide(key)==s&&ManualEnabled;
    }
    // Hand o holds a weapon (its own, or the game's by the handle or the
    // fore-end) or carries a body (the left hand).
    private bool HandFull(int o)
    {
        if(copyKey[o]>=0||SupportsCopy(o==1))return true;
        if(o==0&&GripCarry.Current?.HidesLeft==true)return true;
        int key=GameKey;if(weapon==null||key<0||key!=(int)weapon.slot)return false;
        int g=GameSide(key);if(g<0)return false;
        return foreEndOnly?1-g==o:g==o;
    }
    internal bool ChestReloads(int s)
    {
        if(s<0||s>1||!enabled||!WeaponOptions.ManualReload.Value)return false;
        // 0.1.194: two pistols of the game (both hands full) reload each against the chest.
        if(DualActive)return DualChest(s);
        if(copyKey[s]>=0)return EquipmentProfile.ChestMagazine(copyProfile[s])&&!copyForeEnd[s];
        return GamePistolIn(s)&&HandFull(1-s);
    }
    // The game's own pistol reloads against the chest (its belt reload and the game's reload are off).
    internal bool ChestGame
    {
        get
        {
            try{if(weapon==null)return false;int g=GameSide(GameKey);return g>=0&&copyKey[g]<0&&ChestReloads(g);}
            catch(Exception){return false;}
        }
    }
    // What the watch shows for the reload: the chest reload's step, or the belt reload's.
    internal string WatchReloadLabel=>chestLabel.Length>0?chestLabel:chestGameShown?"":ReloadLabel;
    // The reload state of the pistol in hand s: the game's own, an owned
    // weapon's (kept with it when it comes into play), or another one's.
    private ManualReloadState? ChestState(int s)=>DualActive&&s==0?DualLeftState():copyKey[s]<0?reload:CopyState(s,true);
    // Only an existing state (a weapon reloaded by itself).
    private ManualReloadState? KnownHandState(int s)=>copyKey[s]<0?(weapon!=null?reload:null):CopyState(s,false);
    // 0.1.185: the reload state of the weapon held as a copy in hand s (made
    // when asked): an owned weapon's (kept with it when it comes into play),
    // or another one's of a kind.
    private ManualReloadState? CopyState(int s,bool create)
    {
        int key=copyKey[s];if(key<0||holsters==null)return null;string p=copyProfile[s];
        if(BodyHolsters.IsLoose(key))
        {
            if(!looseReloadStates.TryGetValue(key,out var l)){if(!create)return null;l=ReloadStateFor(p);looseReloadStates[key]=l;}
            return l;
        }
        var w=holsters.WeaponOf(key);if(w==null)return null;
        int id=w.GetInstanceID();
        if(!reloadStates.TryGetValue(id,out var st)){if(!create)return null;st=ReloadStateFor(p);reloadStates[id]=st;}
        return st;
    }
    private AmmoManagementComponent? ChestAmmo(int s,out bool loose,out int key)
    {
        loose=false;key=-1;
        if(DualActive&&s==0)return DualLeftAmmo(out key);
        if(copyKey[s]<0){if(weapon==null)return null;key=(int)weapon.slot;return ammo;}
        if(holsters==null)return null;
        key=copyKey[s];loose=BodyHolsters.IsLoose(key);
        var w=holsters.WeaponOf(key);if(w==null)return null;
        if(chestAmmo.TryGetValue(w.Pointer,out var cached))return cached;
        var a=FireOf(w)?.ammoManagementComponent;if(a!=null)chestAmmo[w.Pointer]=a;
        return a;
    }
    private int ChestRounds(int s,AmmoManagementComponent a,bool loose,int key)=>loose?holsters!.LooseMagazine(key):a.PrimaryMagazineAmmoCount;
    private void SetChestRounds(int s,AmmoManagementComponent a,bool loose,int key,int count)
    {
        if(loose){holsters!.SetLooseMagazine(key,count);return;}
        if(copyKey[s]<0&&ammo!=null&&a.Pointer==ammo.Pointer){SetMagazine(count);return;}
        a.triggerPrimaryReload=false;a.SetAmmo(count,a.SecondaryMagazineAmmoCount);
        if(a.PrimaryMagazineAmmoCount!=count)throw new InvalidOperationException("the magazine took "+a.PrimaryMagazineAmmoCount+", not "+count);
    }
    private static bool Infinite(AmmoManagementComponent a){try{return a.ammoPool!=null&&a.ammoPool.IsInfinite;}catch(Exception){return false;}}
    private static void ChestRefund(AmmoManagementComponent a,int rounds)
    {
        if(rounds<=0||Infinite(a))return;
        try{a.ammoPool.AddAmmo(a.primaryAmmoType,rounds);}catch(Exception ex){Bootstrap.Warn("CHEST RELOAD rounds back to the reserve: "+ex.Message);}
    }
    private static void ChestNotify(AmmoManagementComponent a){try{a.TriggerEventAmmoPoolChanged();}catch(Exception){}}
    // Where the pistol's grip ends (the bottom of its magazine), world.
    private bool ChestButt(int s,out Vector3 butt)
    {
        butt=Vector3.zero;
        if(copyKey[s]>=0){var c=holsters?.CopyOf(copyKey[s]);if(c==null||!c.Shown)return false;butt=c.ButtWorld;return true;}
        if(DualActive&&s==0)return DualLeftButt(out butt);
        if(visual==null||!poseValid)return false;
        var local=visual.ReloadAvailable?visual.ReloadPort:HandleSided(NativeGrip(true,Vector3.zero))+HolsterCopy.ButtBelowGrip;
        butt=visual.FittedToWorld.MultiplyPoint3x4(local);return true;
    }
    private System.Numerics.Vector3 ChestLocal(Vector3 world)=>ChestReloadMath.Local(ToN(world),ToN(rig.HeadPosition),chestYaw);
    private void TickChestReload()
    {
        float now=Time.realtimeSinceStartup;
        if(ChestReloadMath.Heading(ToN(rig.HeadRotation*Vector3.forward),out float yaw)){chestYaw=yaw;chestYawSet=true;}
        bool anyOut=false;string label="";
        for(int s=0;s<2;s++)
        {
            if(chestChamberAt[s]>0&&now>=chestChamberAt[s])
            {
                chestChamberAt[s]=-1;reloadAudio??=new ReloadAudio();reloadAudio.PlayCue(ChestProfile(s),2);rig.ResistanceHaptics(.8f,s==1);
            }
            chestActive[s]=ChestReloads(s);
            if(!chestActive[s]){chestStrike[s].Reset();chestKey[s]=-1;continue;}
            var state=ChestState(s);var a=ChestAmmo(s,out bool loose,out int key);
            if(state==null||a==null){chestStrike[s].Reset();continue;}
            if(key!=chestKey[s]){chestKey[s]=key;chestStrike[s].Reset();chestEmptyReported[s]=false;}
            int rounds;bool infinite=Infinite(a);int reserve;
            try{rounds=ChestRounds(s,a,loose,key);reserve=a.PrimaryReserveAmmoCount;}
            catch(Exception ex){if(copyKey[s]>=0)chestAmmo.Clear();if(now>=nextChestError){nextChestError=now+10;Bootstrap.Warn("CHEST RELOAD "+Side(s)+" hand: "+ex.Message);}continue;}
            // 0.1.194: two pistols of the game: each one's slide and magazine as they are.
            if(DualActive)state.ObserveRounds(rounds);
            // A copy: its slide back on the last round, its magazine drawn as it is.
            else if(copyKey[s]>=0)
            {
                state.ObserveRounds(rounds);
                var copy=holsters?.CopyOf(key);
                if(copy!=null){copy.MagazineOut=!state.Installed;if(copy.HasCycle)copy.LockBack(state.SlideLocked);}
            }
            bool busy=HandReloading(s)||copyKey[s]<0&&inventory!.isInTransit||DualActive&&DualSwitching;
            var controls=s==0?rig.LeftControls:rig.RightControls;
            if(!busy&&controls.Valid&&(controls.Down&HandControls.B)!=0)
            {
                if(state.Installed)ChestEject(s,state,a,loose,key,rounds,now);
                else rig.ResistanceHaptics(.3f,s==1);
            }
            // Waiting for a strike: the magazine out, or one in with the slide back.
            bool waiting=!state.Installed||state.NeedsRack&&rounds>0;
            if(!waiting)chestStrike[s].Reset();
            else if(!busy&&chestYawSet)
            {
                if(!ChestButt(s,out var butt))chestStrike[s].Reset();
                else if(chestStrike[s].Step(ChestLocal(butt),now))
                {
                    ChestInsert(s,state,a,loose,key,now);
                    try{rounds=ChestRounds(s,a,loose,key);reserve=a.PrimaryReserveAmmoCount;}catch(Exception){}
                }
            }
            bool loaded=infinite||reserve>0;
            if(!state.Installed){if(loaded){anyOut=true;label="CHEST";}}
            else if(state.NeedsRack&&rounds>0)label="CHEST";
            else if(rounds<=0&&loaded&&label.Length==0)label="DROP";
        }
        chestLabel=label;chestGameShown=ChestGame;
        if(!anyOut||!chestYawSet){chestGlow?.Hide();return;}
        if(chestGlowFailed)return;
        try{chestGlow??=new ReloadGlow();var at=ChestReloadMath.World(ChestReloadMath.GlowPoint,ToN(rig.HeadPosition),chestYaw);chestGlow.Show(new Vector3(at.X,at.Y,at.Z),.025f);}
        catch(Exception ex){chestGlowFailed=true;chestGlow=null;Bootstrap.Warn("CHEST RELOAD glow unavailable: "+ex.Message);}
    }
    private float nextChestError;
    private void ChestEject(int s,ManualReloadState state,AmmoManagementComponent a,bool loose,int key,int rounds,float now)
    {
        bool game=copyKey[s]<0;
        try
        {
            if(game)a.CancelReload();
            SetChestRounds(s,a,loose,key,0);
        }
        catch(Exception ex){Bootstrap.Warn("CHEST RELOAD "+Side(s)+" hand magazine out: "+ex.Message);return;}
        state.Detach(rounds,false);ChestRefund(a,rounds);ChestNotify(a);
        chestStrike[s].Reset();chestEmptyReported[s]=false;
        var at=rig.HeadPosition;
        if(game)
        {
            if(DualActive&&s==0)DropDualLeft(ref at);
            else if(visual!=null){at=visual.FittedToWorld.MultiplyPoint3x4(visual.MagazineCenter);Drop(at,visual.FittedToWorld.rotation,Vector3.down*.35f);}
        }
        else if(holsters?.CopyOf(key) is HolsterCopy copy)
        {
            copy.MagazineOut=true;if(copy.HasCycle)copy.LockBack(state.SlideLocked);at=copy.MagazineWorld;
            if(copy.MagazineTemplate!=null)
            {
                try{drops??=new ReloadDrops();drops.Add(copy.MagazineTemplate,at,copy.Rotation,Vector3.down*.35f);}
                catch(Exception ex){Bootstrap.Warn("CHEST RELOAD dropped magazine: "+ex.Message);}
            }
        }
        reloadAudio??=new ReloadAudio();reloadAudio.Play(ReloadAction.DropInstalled,ChestProfile(s),at,rig.HeadPosition,rig.HeadRotation);
        rig.ReloadHaptics(ReloadAction.DropInstalled,s==0);
        Bootstrap.Write("CHEST RELOAD "+Side(s)+" hand "+ChestProfile(s)+(loose?" (another one)":DualActive?" (one of the game's two pistols)":game?" (the game's)":"")+": magazine out, "+rounds+" rounds back to the reserve"+(state.SlideLocked?", the slide stays back":"")+"; strike the grip against the chest for a full one");
    }
    private void ChestInsert(int s,ManualReloadState state,AmmoManagementComponent a,bool loose,int key,float now)
    {
        bool game=copyKey[s]<0;var copy=game?null:holsters?.CopyOf(key);
        if(state.Installed)
        {
            // A magazine already in, the slide back: the strike lets it go forward.
            state.QuickLoad();if(copy!=null&&copy.HasCycle)copy.LockBack(false);
            if(DualActive)ReleaseDualFire(s,"chest slide");
            else if(game){DisarmFireTrigger();ReleaseNativeFire("chest slide");}
            reloadAudio??=new ReloadAudio();reloadAudio.PlayCue(ChestProfile(s),2);rig.PunchHaptics(s==1);
            Bootstrap.Write("CHEST RELOAD "+Side(s)+" hand "+ChestProfile(s)+" struck against the chest: the slide forward");
            return;
        }
        bool infinite=Infinite(a);int have,max,request;
        try{have=ChestRounds(s,a,loose,key);max=Math.Max(1,a.MaxPrimaryMagazineAmmoCount);request=infinite?max:Math.Min(max,Math.Max(0,a.PrimaryReserveAmmoCount));}
        catch(Exception ex){Bootstrap.Warn("CHEST RELOAD "+Side(s)+" hand: "+ex.Message);return;}
        int got=0;
        if(request>0){try{got=infinite?request:a.ammoPool.TryRemoveAmmo(a.primaryAmmoType,request);}catch(Exception ex){Bootstrap.Warn("CHEST RELOAD reserve: "+ex.Message);got=0;}}
        if(got<=0)
        {
            rig.ResistanceHaptics(.35f,s==1);
            if(!chestEmptyReported[s]){chestEmptyReported[s]=true;Bootstrap.Write("CHEST RELOAD "+Side(s)+" hand "+ChestProfile(s)+": no rounds left in the reserve; the magazine stays out");}
            return;
        }
        try
        {
            if(DualActive)a.CancelReload();
            else if(game)ResetNativeReload("chest");
            SetChestRounds(s,a,loose,key,got);
        }
        catch(Exception ex){ChestRefund(a,got);Bootstrap.Warn("CHEST RELOAD "+Side(s)+" hand magazine in: "+ex.Message);return;}
        ChestRefund(a,have);state.QuickLoad();ChestNotify(a);
        if(copy!=null){copy.MagazineOut=false;if(copy.HasCycle)copy.LockBack(false);}
        if(DualActive)ReleaseDualFire(s,"chest insert");
        else if(game){DisarmFireTrigger();ReleaseNativeFire("chest insert");}
        reloadAudio??=new ReloadAudio();reloadAudio.PlayCue(ChestProfile(s),1);chestChamberAt[s]=now+ChamberDelay;
        rig.PunchHaptics(s==1);chestEmptyReported[s]=false;
        Bootstrap.Write("CHEST RELOAD "+Side(s)+" hand "+ChestProfile(s)+(loose?" (another one)":DualActive?" (one of the game's two pistols)":game?" (the game's)":"")+" struck against the chest: magazine in "+got+"/"+max+", the "+(ChestProfile(s)=="uzi"?"bolt":"slide")+" forward");
    }
    internal const float ChamberDelay=.12f;
    // The gun reloaded against the chest in hand s.
    private string ChestProfile(int s)=>DualActive?"pistol":copyKey[s]>=0?copyProfile[s]:profile;
    private void HideChest(){chestGlow?.Hide();chestLabel="";chestGameShown=false;}
    private void ResetChest()
    {
        HideChest();chestAmmo.Clear();
        for(int s=0;s<2;s++){chestStrike[s].Reset();chestKey[s]=-1;chestChamberAt[s]=-1;chestActive[s]=false;}
    }
    private void DisposeChest(){ResetChest();chestGlow?.Dispose();chestGlow=null;}
}
