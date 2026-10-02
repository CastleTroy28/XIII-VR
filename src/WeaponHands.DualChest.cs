using System;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.194: the
// game's own two pistols (DualWieldComponent), with the hand reload on, are
// reloaded each by its own hand like pistols in both hands (ChestReload):
// the left Y / right B drops that pistol's magazine, its grip struck against
// the chest puts a full one in. The game's reload of both is off then.
internal sealed partial class WeaponHands
{
    // 0.1.194: the left pistol of the game's two (a key of its own for the strike).
    private const int DualLeftKey=100000;
    private bool DualChest(int s)=>DualActive&&(s==1?ammo!=null&&visual!=null:leftFire?.ammoManagementComponent!=null&&leftVisual!=null&&leftWeapon!=null);
    internal bool DualChestOn=>enabled&&WeaponOptions.ManualReload.Value&&DualActive;
    private ManualReloadState? DualLeftState()
    {
        if(leftWeapon==null)return null;int id=leftWeapon.GetInstanceID();
        if(!reloadStates.TryGetValue(id,out var st)){st=ReloadStateFor("pistol");reloadStates[id]=st;}
        return st;
    }
    // 0.1.194: each of the game's two pistols shows its own magazine (out or
    // in) and its slide (back on the last round); none fires with it out.
    private void PoseDualChest()
    {
        if(!DualChestOn)return;
        var left=DualLeftState();
        try{if(left!=null&&leftFire?.ammoManagementComponent!=null)left.ObserveRounds(leftFire.ammoManagementComponent.PrimaryMagazineAmmoCount);}catch(Exception){}
        try{if(ammo!=null)reload.ObserveRounds(ammo.PrimaryMagazineAmmoCount);}catch(Exception){}
        if(left!=null)leftVisual?.ReloadPose(false,!left.Installed,false,left.VisualRackTravel,Vector3.zero,Quaternion.identity,left.SlideLocked);
        visual?.ReloadPose(false,!reload.Installed,false,reload.VisualRackTravel,Vector3.zero,Quaternion.identity,reload.SlideLocked);
    }
    // After a magazine went in: the pistol's fire no longer waits for the game's own reload.
    private void ReleaseDualFire(int s,string why)
    {
        var f=s==0?leftFire:fire;var w=s==0?leftWeapon:weapon;var a=f?.ammoManagementComponent??(s==1?ammo:null);
        if(f==null||w==null||a==null)return;
        try
        {
            if(a.PrimaryMagazineAmmoCount<=0||StartAllowed(f))return;
            try{f.CancelWaitAfterFire(w);}catch(Exception){}
            if(!StartAllowed(f))try{f.OnUnequipOrReload(w);}catch(Exception){}
            if(!StartAllowed(f)&&!f.canFire)f.canFire=true;
            Bootstrap.Write("FIRE STATE "+Side(s)+" pistol of two after "+why+": ready="+StartAllowed(f));
        }
        catch(Exception ex){Bootstrap.Warn("FIRE STATE "+Side(s)+" pistol of two: "+ex.Message);}
    }
    private bool DualBlocksFire(bool left)
    {
        if(!DualChestOn)return false;
        var st=left?DualLeftState():reload;return st!=null&&st.BlocksFire;
    }
    private AmmoManagementComponent? DualLeftAmmo(out int key)
    {
        key=weapon==null?-1:DualLeftKey+(int)weapon.slot;
        return weapon==null?null:leftFire?.ammoManagementComponent;
    }
    private bool DualLeftButt(out Vector3 butt)
    {
        butt=Vector3.zero;
        if(leftVisual==null||!leftPoseValid||!leftVisual.ReloadAvailable)return false;
        butt=leftVisual.FittedToWorld.MultiplyPoint3x4(leftVisual.ReloadPort);return true;
    }
    private bool DualSwitching{get{try{return dual?.dualWieldStateChanging==true;}catch(Exception){return false;}}}
    private void DropDualLeft(ref Vector3 at)
    {
        var v=leftVisual;if(v==null)return;
        at=v.FittedToWorld.MultiplyPoint3x4(v.MagazineCenter);
        if(v.Ammunition==null)return;
        try{drops??=new ReloadDrops();drops.Add(v.Ammunition,at,v.FittedToWorld.rotation,Vector3.down*.35f);}
        catch(Exception ex){Bootstrap.Warn("CHEST RELOAD left pistol's dropped magazine: "+ex.Message);}
    }
}
