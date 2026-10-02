using System;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.123: after its bolt was shot the crossbow did not fire again after a
// hand reload until another weapon was taken and the crossbow back. The game
// waits after an emptying shot for its own reload (which the hand reload
// replaces and blocks) before its fire component lets the gun start again;
// switching weapons ended that wait. Now, once ammunition is in the gun by
// hand (and again on a trigger press that the game would refuse), the wait
// is ended the way the game's own reload/unequip ends it.
internal sealed partial class WeaponHands
{
    private float nextFireRelease;private bool releasingFire;
    private void ReleaseNativeFire(string why)
    {
        var f=fire;var w=weapon;var a=ammo;
        if(releasingFire||f==null||w==null||a==null||!ManualEnabled)return;
        // On a trigger press only after the game's reload was refused (never
        // to cut a gun's own time between shots).
        if(why=="trigger"&&!nativeReloadRefused)return;
        releasingFire=true;
        try
        {
            if(a.PrimaryMagazineAmmoCount<=0)return;
            if(StartAllowed(f)){nativeReloadRefused=false;return;}
            float now=Time.realtimeSinceStartup;bool report=now>=nextFireRelease;if(report)nextFireRelease=now+2;
            string before=FireState(f,a);string how="none";
            try{f.CancelWaitAfterFire(w);how="wait ended";}catch(Exception ex){if(report)Bootstrap.Warn("FIRE STATE end wait: "+ex.Message);}
            if(!StartAllowed(f)){try{f.OnUnequipOrReload(w);how+=", reset as on reload";}catch(Exception ex){if(report)Bootstrap.Warn("FIRE STATE reset: "+ex.Message);}}
            if(!StartAllowed(f)&&!f.canFire){f.canFire=true;how+=", fire allowed";}
            bool ready=StartAllowed(f);if(ready)nativeReloadRefused=false;
            if(report)Bootstrap.Write("FIRE STATE "+profile+" after "+why+": "+before+" -> "+FireState(f,a)+" ("+how+") ready="+ready);
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextFireRelease){nextFireRelease=Time.realtimeSinceStartup+10;Bootstrap.Warn("FIRE STATE release: "+ex.Message);}}
        finally{releasingFire=false;}
    }
    // 0.1.124: after the bolt was inserted the crossbow only made its shot
    // sound: no bolt left it. Its game reload had been asked for after the
    // shot and refused (the hands reload); the game's arms still waited in
    // that reload, so the shot's launch (driven by the arms' animation) never
    // came. The pistols' hand reload always cancelled the game's reload when
    // the magazine came out, the crossbow's never did. Now every hand reload
    // cancels it (taking a round, inserting), and the arms are released.
    private void ResetNativeReload(string why)
    {
        var a=ammo;var w=weapon;if(a==null||w==null)return;
        // Never the M16 grenade launcher's own (native) reload.
        if(Time.realtimeSinceStartup-secondaryReloadAt<4)return;
        string before=ArmsState();
        try{a.CancelReload();}catch(Exception ex){Bootstrap.Warn("FIRE STATE cancel reload: "+ex.Message);}
        var arms=Arms;
        try{if(arms!=null&&(arms.isReloading||arms.IsReloadAnimationPlaying()))arms.CheckReloadCancel(w);}catch(Exception ex){Bootstrap.Warn("FIRE STATE arms: "+ex.Message);}
        float now=Time.realtimeSinceStartup;
        if(now>=nextResetReport){nextResetReport=now+2;Bootstrap.Write("FIRE STATE "+profile+" "+why+": game reload cancelled; arms "+before+" -> "+ArmsState()+(fire!=null?"; fire "+FireState(fire,a):""));}
    }
    private float nextResetReport;
    private PlayerArmsAnimationControl? Arms
    {get{try{return ammo?.playerArmsAnimationControl??fire?.playerArmsAnimationControl??inventory?.playerArmsAnimationControl;}catch(Exception){return null;}}}
    private string ArmsState()
    {
        try{var a=Arms;return a==null?"none":"reloading="+a.isReloading+" animation="+a.IsReloadAnimationPlaying()+" trigger="+a.Reload_AnimTrigger+" cancelled="+a.reloadCanceled;}
        catch(Exception ex){return "unreadable ("+ex.Message+")";}
    }
    // A trigger press on a loaded gun that launched nothing within half a
    // second (after a hand reload): reported and the game's state released.
    private float stallPressAt=-1;private int stallShots;private int shotsLaunched;private float lastLaunchAt=-10;
    internal void NoteLaunch(){shotsLaunched++;lastLaunchAt=Time.realtimeSinceStartup;stallPressAt=-1;}
    private void NotePress()
    {
        if(ammo==null||!ManualReady)return;
        try{if(ammo.PrimaryMagazineAmmoCount<=0)return;}catch(Exception){return;}
        if(!nativeReloadRefused&&!EquipmentProfile.SingleRound(profile))return;
        if(Time.realtimeSinceStartup-lastLaunchAt<1.2f)return;
        stallPressAt=Time.realtimeSinceStartup;stallShots=shotsLaunched;
    }
    private void TickStall()
    {
        if(stallPressAt<0||Time.realtimeSinceStartup-stallPressAt<.5f)return;
        stallPressAt=-1;if(shotsLaunched!=stallShots||fire==null||ammo==null||weapon==null)return;
        Bootstrap.Write("FIRE STALL "+profile+": trigger pressed with "+ammo.PrimaryMagazineAmmoCount+" loaded, nothing launched; "+FireState(fire,ammo)+"; arms "+ArmsState());
        ResetNativeReload("stall");
        try{fire.CancelWaitAfterFire(weapon);fire.OnUnequipOrReload(weapon);}catch(Exception ex){Bootstrap.Warn("FIRE STALL reset: "+ex.Message);}
        Bootstrap.Write("FIRE STALL "+profile+" after reset: "+FireState(fire,ammo)+"; arms "+ArmsState()+"; ready="+StartAllowed(fire));
        RequestRedraw("a trigger press that launched nothing");
    }
    // 0.1.125: the crossbow still launched nothing after a hand reload: its
    // fire component started (the shot sound) and then waited for the shot
    // that the gun's own animation launches - the gun's animator stayed in
    // its "empty" state (only the game's reload brings it back), and from
    // there no shot is played. Taking another weapon and the crossbow again
    // fixed it: the gun's animator starts over when it is drawn. So after the
    // bolt is inserted the crossbow is drawn again, instantly (fists, then
    // the crossbow, no draw animation), usually within the same frame. Any
    // other gun that still stalls after a hand reload gets the same.
    private int redrawSlot=-1,redrawStage;private float redrawAt;private bool redrawHeld;
    // 0.1.127: while the game draws the gun again, the VR gun stays bound and
    // drawn in the hand (it disappeared for about a second).
    internal bool Redrawing=>redrawStage!=0&&weapon!=null&&(int)weapon.slot==redrawSlot;
    private void RequestRedraw(string why)
    {
        if(inventory==null||weapon==null||redrawStage!=0||DualActive)return;
        var slot=weapon.slot;int loaded=ammo?.PrimaryMagazineAmmoCount??-1;
        try
        {
            if(!inventory.TrySelectSlot(PlayerEquipableInventory.ActiveEquipmentSlot.Fist,true,true,true,false,true)){Bootstrap.Write("FIRE STATE "+profile+" redraw refused by the game");return;}
            redrawSlot=(int)slot;redrawAt=Time.realtimeSinceStartup;redrawStage=1;redrawHeld=gripState.Owned;redrawLoaded=loaded;
            Bootstrap.Write("FIRE STATE "+profile+" drawn again after "+why+" (like a weapon switch, without animation) loaded="+loaded);
            TickRedraw();
        }
        catch(Exception ex){redrawStage=0;Bootstrap.Warn("FIRE STATE redraw: "+ex.Message);}
    }
    private int redrawLoaded;
    private void TickRedraw()
    {
        if(redrawStage==0||inventory==null)return;
        if(Time.realtimeSinceStartup-redrawAt>2){redrawStage=0;Bootstrap.Warn("FIRE STATE redraw did not complete");return;}
        try
        {
            if(redrawStage==1)
            {
                var current=inventory.currentEquipable;
                if(inventory.isInTransit||current==null||current.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Fist)return;
                if(redrawHeld)gripTakeAt=Time.realtimeSinceStartup;
                bool ok=inventory.TrySelectSlot((PlayerEquipableInventory.ActiveEquipmentSlot)redrawSlot,true,true,true,false,true);
                redrawStage=ok?2:0;if(!ok)Bootstrap.Warn("FIRE STATE redraw: the game refused the weapon back");
                if(!ok)return;
            }
            var back=inventory.currentEquipable;
            if(redrawStage==2&&back!=null&&(int)back.slot==redrawSlot&&!inventory.isInTransit)
            {
                redrawStage=0;
                var a=back.primaryFireUsageComponentScript?.TryCast<FireComponent>()?.ammoManagementComponent??ammo;
                int now=a?.PrimaryMagazineAmmoCount??-1;
                if(a!=null&&redrawLoaded>0&&now!=redrawLoaded){a.SetAmmo(redrawLoaded,a.SecondaryMagazineAmmoCount);Bootstrap.Warn("FIRE STATE redraw changed the magazine "+now+" -> restored "+redrawLoaded);}
                Bootstrap.Write("FIRE STATE "+EquipmentProfile.ForSlot(redrawSlot)+" back in the hand, loaded="+(a?.PrimaryMagazineAmmoCount??-1)+" ready="+(back.primaryFireUsageComponentScript?.TryCast<FireComponent>() is FireComponent f?StartAllowed(f):true));
            }
        }
        catch(Exception ex){redrawStage=0;Bootstrap.Warn("FIRE STATE redraw: "+ex.Message);}
    }
    private static bool StartAllowed(FireComponent f){try{return f.CanStart();}catch(Exception){return true;}}
    private static string FireState(FireComponent f,AmmoManagementComponent a)
    {
        try{return "canFire="+f.canFire+" active="+f.isActive+" waiting="+(f.waitAfterFireRoutine!=null)+" shots="+f.shotsFired+"/"+f.shotLimit+" once="+f.fireOnce+" ammo="+a.CurrentAmmoState+" reloadFlag="+a.triggerPrimaryReload;}
        catch(Exception ex){return "state unreadable ("+ex.Message+")";}
    }
}
