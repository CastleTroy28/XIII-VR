using System;
using UnityEngine;
namespace XiiiXR;
internal sealed partial class WeaponHands
{
    private RevolverReloadState revolver=new();
    private Vector3 previousRevolverPosition;
    private bool revolverPositionValid;
    private bool RevolverReady=>profile=="revolver"&&WeaponOptions.ManualReload.Value&&visual?.CylinderReady==true;
    // 0.1.142: the revolver in the left hand reloads by hand too (it was only
    // the right hand's: in the left it only vibrated): the left Y opens it,
    // the right hand takes rounds from the pouch and puts them in, a flick of
    // the left hand closes it.
    // 0.1.241: it stays open until shut: B (Y) again, or the flick.
    private bool LeftRevolverManual=>PrimaryLeft&&RevolverReady&&copyKey[1]<0&&!foreEndOnly;
    private bool revolverMirrored;
    private void TickRevolver()
    {
        bool mirrored=PrimaryLeft;
        if(mirrored!=revolverMirrored){revolverMirrored=mirrored;CancelRevolver();if(revolver.Open)revolver.Applied(RevolverAction.Close);}
        if(foreEndOnly||(mirrored?!LeftRevolverManual||GripCarry.Current?.HoldingBody==true:GripCarry.Current?.HidesLeft==true)){CancelRevolver();return;}
        if(!RevolverReady||visual==null||ammo==null)return;
        if(!CanControl(playerId)||!rig.SampleWorldHands(out var left,out var right,out var valid)||!valid){CancelRevolver();return;}
        // The hand that loads (the other one) and the hand with the gun.
        var loading=mirrored?right:left;var gun=mirrored?left:right;
        var loadControls=mirrored?rig.RightControls:rig.LeftControls;var gunControls=mirrored?rig.LeftControls:rig.RightControls;
        var lp=CameraRig.UnityPosition(loading);var lq=GloveVisual.Rotation(loading,mirrored);
        if(revolver.Holding&&ContactRig.Current?.ResolveHand(mirrored,false,ref lp,ref lq)==false)return;
        // 0.1.241: the gun hand's motion in the room (the tracked controller).
        // It was measured against the head: looking down at the pouch moved the
        // head, and the cylinder shut by itself while the rounds were taken.
        Vector3 rp;Quaternion gunTurn;
        if(rig.SampleTrackedHand(!mirrored,out var tracked)){rp=CameraRig.UnityPosition(tracked);gunTurn=CameraRig.UnityRotation(tracked);}
        else{rp=CameraRig.UnityPosition(gun);gunTurn=CameraRig.UnityRotation(gun);}
        float dt=Time.unscaledDeltaTime;
        var velocity=revolverPositionValid&&dt>.0001f&&dt<.1f?(rp-previousRevolverPosition)/dt:Vector3.zero;
        previousRevolverPosition=rp;revolverPositionValid=true;
        // To the right of the hand holding it (level), in the same room space.
        var axis=gunTurn*Vector3.right;axis.y=0;axis=axis.sqrMagnitude>1e-4f?axis.normalized:Vector3.right;
        var socket=visual.FittedToWorld.MultiplyPoint3x4(visual.CylinderSocket);
        var fit=ReloadGripGeometry.Get("revolver");
        var tipLocal=fit==null?new Vector3(0,-.035f,.108f):ContactWorld.U(fit.Tip);if(mirrored)tipLocal.x=-tipLocal.x;
        var tip=lp+lq*tipLocal;
        bool aligned=Vector3.Dot(lq*Vector3.forward,visual.FittedToWorld.MultiplyVector(Vector3.forward))>.7f;
        // The flick that closes it: to the right with the right hand; either
        // way with the left.
        float flick=Vector3.Dot(velocity,axis);if(mirrored)flick=Math.Abs(flick);
        // Opened with the B of the hand holding it (right B / left Y).
        var a=revolver.Step(Time.realtimeSinceStartup,dt,gunControls.Valid&&(gunControls.Down&HandControls.B)!=0,
            (loadControls.Down&AmmoButton)!=0,(loadControls.Held&AmmoButton)!=0,
            pouch?.Near(lp)==true,aligned&&Vector3.Distance(tip,socket)<.065f,Vector3.Dot(aimForward,Vector3.up),flick);
        if(a==RevolverAction.None)return;
        int held=0;
        switch(a)
        {
            case RevolverAction.Open:ammo.CancelReload();StopOwnedFire();ReleaseSupport();break;
            case RevolverAction.Empty:int remaining=ammo.PrimaryMagazineAmmoCount;SetMagazine(0);Refund(remaining);visual.EjectCasings(ammo.MaxPrimaryMagazineAmmoCount,reloadAudio);break;
            case RevolverAction.Take:
                ReleaseSupport();
                int count=ammo.ammoPool.IsInfinite?ammo.MaxPrimaryMagazineAmmoCount:Math.Min(ammo.MaxPrimaryMagazineAmmoCount,Math.Max(0,ammo.PrimaryReserveAmmoCount));
                held=ammo.ammoPool.IsInfinite?count:ammo.ammoPool.TryRemoveAmmo(ammo.primaryAmmoType,count);NotifyAmmo();break;
            case RevolverAction.Drop:Refund(revolver.HeldRounds);break;
            case RevolverAction.Load:SetMagazine(revolver.HeldRounds);break;
            case RevolverAction.Close:DisarmFireTrigger();break;
        }
        heldAmmoFrame=-10;ContactRig.Current?.ResetHand(mirrored);
        revolver.Applied(a,held);rig.ReloadHaptics(a==RevolverAction.Open?ReloadAction.DropInstalled:a==RevolverAction.Load?ReloadAction.Insert:a==RevolverAction.Close?ReloadAction.Chamber:ReloadAction.TakeSupply);
        reloadAudio??=new ReloadAudio();
        reloadAudio.PlayCue("revolver",a==RevolverAction.Open?0:a==RevolverAction.Empty?4:a==RevolverAction.Close?2:a==RevolverAction.Load?1:-1);
        Bootstrap.Write("REVOLVER action="+a+(mirrored?" (in the left hand; the right hand loads)":"")+" magazine="+ammo.PrimaryMagazineAmmoCount+" reserve="+ammo.PrimaryReserveAmmoCount);
    }
    // The rounds in the loading hand (the right one for the revolver in the left).
    private void RenderRevolver(PoseValue left,PoseValue right)
    {
        if(!RevolverReady)return;bool mirrored=PrimaryLeft;var loading=mirrored?right:left;
        visual?.PoseCylinder(revolver.Open,revolver.Holding,Time.frameCount-heldAmmoFrame<=2?heldAmmoHand:CameraRig.UnityPosition(loading),Time.frameCount-heldAmmoFrame<=2?heldAmmoRotation:GloveVisual.Rotation(loading,mirrored),revolver.HeldRounds,revolver.Emptied);
    }
    private void CancelRevolver()
    {if(revolver.Holding){Refund(revolver.HeldRounds);revolver.Applied(RevolverAction.Drop);}revolverPositionValid=false;}
}
