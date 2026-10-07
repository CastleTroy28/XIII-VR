using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private DualWieldComponent? dual;
    private Equipable? leftWeapon;
    private FireComponent? leftFire;
    private WeaponVisual? leftVisual;
    private readonly Dictionary<int,(Renderer renderer,bool enabled)> dualHidden=new();
    private Vector3 leftDesiredTip;
    private Vector3 leftAimPosition,leftAimForward,leftAimUp,leftHandPosition;
    private Quaternion leftHandRotation;
    private float dualReloadStarted=-100;
    private bool dualLeftOwned,dualRightOwned,leftShooting,leftPoseValid;
    private int dualInputFrame=-1;
    private float dualRetry,leftFlashUntil;
    internal bool DualActive=>profile=="pistol"&&dual!=null&&dual.IsDualWieldActive();
    internal bool LeftPistolVisible=>DualActive&&leftPoseValid;
    internal bool DualAmmo(out string magazines,out string reserve)
    {
        magazines=reserve="";if(!DualActive||ammo==null)return false;
        int left=leftFire?.ammoManagementComponent?.PrimaryMagazineAmmoCount??ammo.DualWieldedMagazineAmmoCount;
        magazines=ammo.PrimaryMagazineAmmoCount+" / "+left;reserve=ammo.PrimaryReserveAmmoCount.ToString();return true;
    }
    private void BindDual()=>dual=profile=="pistol"?weapon?.GetComponent(Il2CppType.Of<DualWieldComponent>())?.TryCast<DualWieldComponent>():null;
    private void TickDual()
    {
        if(!DualActive){ClearDualVisual();return;}
        var selected=dual!.DualWieldEquipable;
        if(selected==null)return;
        if(leftWeapon!=selected&&Time.realtimeSinceStartup>=dualRetry)
        {
            ClearDualVisual();dualRetry=Time.realtimeSinceStartup+1;
            var single=selected.GetComponent(Il2CppType.Of<SingleFireComponent>())?.TryCast<FireComponent>();
            if(single?.projectileOrigin==null)return;
            try{leftVisual=WeaponVisual.Create(weapon!,muzzle!,"pistol");leftVisual.MatchPrimaryFit(visual!);leftWeapon=selected;leftFire=single;leftVisual.StableGun=true;leftVisual.PrepareReload();
            visual!.StableGun=true;visual.PrepareReload();}
            catch(Exception ex){Bootstrap.Warn("LEFT PISTOL bind retry: "+ex.Message);return;}
            // FakeArms is a native animation rig. Keep its scripts alive, hide
            // its renderers; only the existing VR skin renders the left hand.
            var fake=dual.fakeArmsController?.fakeArms;
            if(fake!=null)HideDualRenderers(fake.transform);
            HideDualRenderers(selected.transform);
            Bootstrap.Write("DUAL VR bound independent left pistol; native fake arms hidden");
        }
        foreach(var entry in dualHidden.Values)if(entry.renderer!=null)entry.renderer.enabled=false;
        if(!CanControl(playerId)||inventory!.isInTransit||leftFire==null){StopDualFire();leftVisual?.Hide();leftPoseValid=false;ContactRig.Current?.ResetLeftGun();return;}
        RenderLeftPistol(false);
        if(dualInputFrame==Time.frameCount)return;dualInputFrame=Time.frameCount;
        FireHand(fire,rig.RightControls,false,ref dualRightOwned);
        FireHand(leftFire,rig.LeftControls,true,ref dualLeftOwned);
        // 0.1.194: with the hand reload on, each pistol reloads against the chest (TickChestReload).
        if(!DualChestOn&&(rig.RightControls.Down&HandControls.B)!=0)
        {
            if(ammo?.CanReload()==true)ammo.StartReload(true);
            var other=leftFire.ammoManagementComponent;if(other?.CanReload()==true)other.StartReload(true);
        }
    }
    private void HideDualRenderers(Transform root)
    {
        foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Renderer>(),true))
        {
            var r=c.TryCast<Renderer>();if(r==null||r.TryCast<ParticleSystemRenderer>()!=null)continue;
            int id=r.GetInstanceID();if(!dualHidden.ContainsKey(id))dualHidden.Add(id,(r,r.enabled));r.enabled=false;
        }
    }
    private void FireHand(FireComponent? component,HandControls input,bool left,ref bool owned)
    {
        if(component==null)return;
        bool held=input.Valid&&(input.Held&HandControls.Trigger)!=0;
        if(owned&&!held){component.End(PlayerEquipableHandler.UsageType.primary);owned=false;}
        if(!input.Valid||(input.Down&HandControls.Trigger)==0||left&&!leftPoseValid)return;
        if(DualBlocksFire(left)){rig.ResistanceHaptics(.25f,!left);return;}
        leftShooting=left;
        try{WriteMuzzle(component);if(component.CanStart()){component.Begin(PlayerEquipableHandler.UsageType.primary);owned=true;}}
        finally{leftShooting=false;}
    }
    private void RenderLeftPistol(bool rendering)
    {
        if(!DualActive||leftVisual==null||leftFire==null||!CanControl(playerId)
            ||!rig.SampleWorldHands(out var left,out _,out bool valid)||!valid){leftVisual?.Hide();leftPoseValid=false;ContactRig.Current?.ResetLeftGun();return;}
        // Mirror the already fitted primary handle, not the native fake arm's
        // world-space pose. Both guns use the same barrel-forward convention.
        foreach(var e in dualHidden.Values)if(e.renderer!=null)e.renderer.enabled=false;
        var q=ControllerAim.Rotation(left)*Quaternion.Euler(WeaponOptions.Pitch.Value,WeaponOptions.Yaw.Value,WeaponOptions.Roll.Value);
        var gripPoint=NativeGrip(true,Vector3.zero);gripPoint.x=-gripPoint.x-Math.Clamp(WeaponOptions.LeftPistolOffset.Value,-.04f,.04f);
        var primary=gripValid[1]?gripRotations[1]:Quaternion.Euler(0,0,-85);
        var handLocal=new Quaternion(primary.x,-primary.y,-primary.z,primary.w);
        leftHandPosition=CameraRig.UnityPosition(left);leftHandRotation=q*handLocal;
        var p=leftHandPosition-q*gripPoint;
        leftDesiredTip=p+q*leftVisual.MuzzleOffset;
        if(ContactRig.Current?.ResolveLeftGun(ref p,ref q,leftVisual.ContactShape)==false){leftVisual.Hide();leftPoseValid=false;return;}
        leftHandPosition=p+q*gripPoint;leftHandRotation=q*handLocal;
        leftVisual.Pose(p,q,Time.realtimeSinceStartup<leftFlashUntil,rendering);
        leftAimPosition=p+q*leftVisual.MuzzleOffset;leftAimForward=q*Vector3.forward;leftAimUp=q*Vector3.up;
        leftPoseValid=true;WriteMuzzle(leftFire);
        if(rendering)ShowGunDot(leftGunDot,leftAimPosition,leftAimForward);
        if(DualChestOn)PoseDualChest();
        else{leftVisual.AutoMagazine(Time.realtimeSinceStartup-dualReloadStarted);
        visual?.AutoMagazine(Time.realtimeSinceStartup-dualReloadStarted);}
    }
    private static void BeginFireTask(FireComponent __instance,out bool __state)
    {
        var c=Current;__state=c?.leftShooting??false;
        if(c==null||c.CopyShotBy(__instance)||!c.Owns(__instance))return;
        c.RenderPose();c.leftShooting=c.IsLeft(__instance);c.WriteMuzzle(__instance);
    }
    private static Exception? EndFireTask(Exception? __exception,bool __state)
    {if(Current!=null)Current.leftShooting=__state;return __exception;}
    private static void DualReload(AmmoManagementComponent __instance)
    {
        var c=Current;if(c==null||!c.DualActive||!c.OwnsAmmo(__instance))return;
        c.dualReloadStarted=Time.realtimeSinceStartup;
    }
    private bool OwnsAmmo(AmmoManagementComponent a)=>ammo!=null&&a.Pointer==ammo.Pointer||leftFire?.ammoManagementComponent!=null&&a.Pointer==leftFire.ammoManagementComponent.Pointer;
    private static void AimLaunch(FireComponent __instance,Vector3 projectilePosition,ref Vector3 hitPoint)
    {
        var c=Current;
        if(c!=null&&c.CopyShotBy(__instance)){hitPoint=projectilePosition+c.copyShotForward*Math.Max(1,__instance.maxRange);return;}
        if(c==null||!c.Owns(__instance)||!c.IsLeft(__instance)||!c.leftPoseValid)return;
        // Native spread remains native; only its central aim is controller-owned.
        hitPoint=projectilePosition+c.leftAimForward*Math.Max(1,__instance.maxRange);
    }
    private bool IsLeft(FireComponent c)=>leftWeapon!=null&&c.baseEquipable!=null&&c.baseEquipable.Pointer==leftWeapon.Pointer;
    private void StopDualFire()
    {
        if(dualLeftOwned){dualLeftOwned=false;leftFire?.End(PlayerEquipableHandler.UsageType.primary);}
        if(dualRightOwned){dualRightOwned=false;fire?.End(PlayerEquipableHandler.UsageType.primary);}
    }
    private void ClearDualVisual()
    {
        // TickDual also calls this for single weapons. Do not reset the active
        // manual magazine or pump every frame when there is no dual state.
        if(leftVisual==null&&leftWeapon==null&&leftFire==null&&dualHidden.Count==0
            &&!dualLeftOwned&&!dualRightOwned&&!leftPoseValid)return;
        ContactRig.Current?.ResetLeftGun();
        StopDualFire();dualReloadStarted=-100;if(visual!=null){visual.StableGun=ManualEnabled&&profile=="shotgun";visual.AutoMagazine(100);}
        leftVisual?.Dispose();leftVisual=null;leftFire=null;leftWeapon=null;leftPoseValid=false;
        foreach(var e in dualHidden.Values)if(e.renderer!=null)e.renderer.enabled=e.enabled;
        dualHidden.Clear();
    }
}
