using System;
using UnityEngine;
namespace XiiiXR;
// One rigid root owns the native hand, closed watch and display mesh.
// It is sampled once before culling, then stays fixed for all cameras/eyes.
internal sealed class GloveVisual : IDisposable
{
    private GameObject? root;
    private GameObject? wearable,bandRoot;
    private RigidMeshVisual? band;
    private RigidMeshVisual? watch,display;
    private NativeHandVisual? native;
    private readonly bool right;
    private WristFit fit=WristFit.Default;
    private float faceLift;
    private int posedFrame=-1;
    private string primary="-",secondary="-",language="";
    private float health=1,armor=1,nextError,curl,triggerCurl;
    internal Transform? Attachment=>root==null?null:root.transform;
    internal bool Valid=>root!=null;
    internal bool Visible=>root!=null && root.activeSelf && native?.Valid==true;
    internal GloveVisual(bool rightHand)
    {
        right=rightHand;
        try
        {
            root=new GameObject(right?"XIII right native hand and watch":"XIII left native hand and watch");root.layer=0;root.SetActive(false);
            wearable=new GameObject("XIII single wristband");wearable.layer=root.layer;wearable.transform.SetParent(root.transform,false);
            bandRoot=new GameObject("XIII skin aligned wrist strap");bandRoot.transform.SetParent(root.transform,false);
            band=new RigidMeshVisual(bandRoot.transform,"XIII fitted wrist strap");
            watch=new RigidMeshVisual(wearable.transform,"XIII closed watch case");watch.Set(HandMeshGeometry.BuildWatch(right));
            display=new RigidMeshVisual(wearable.transform,"XIII rigid watch digits",true);
            display.Set(WatchFaceGeometry.Build(AmmoFace,primary,secondary,health,armor,AmmoFace&&!right));
            Bootstrap.Write("WATCH rigid mesh display, same parent/layer as case; no Canvas; closed underside.");
        }
        catch{Dispose();throw;}
    }
    internal void BindNative(Transform? player)
    {
        if(root==null || player==null || native?.BelongsTo(player)==true)return;
        native?.Dispose();native=null;
        try
        {
            native=NativeHandVisual.Create(root.transform,player,right);WeaponHands.Current?.RegisterHand(native,right);
            fit=native.Wrist;faceLift=fit.RadiusY+.0025f-.040f;
            watch?.Set(HandMeshGeometry.BuildWatch(right,fit.RadiusX,fit.RadiusY,false));
            band?.Set(HandMeshGeometry.BuildBand(right,fit.RadiusX,fit.RadiusY,false));RefreshDisplay();
        }
        catch(Exception ex){Report(ex);}
    }
    internal void Readout(string main,string extra,float hp=1,float ap=1)
    {
        main=WatchFaceGeometry.Clean(main);extra=WatchFaceGeometry.Clean(extra);
        hp=float.IsFinite(hp)?Mathf.Clamp01(hp):0;ap=float.IsFinite(ap)?Mathf.Clamp01(ap):0;
        hp=MathF.Round(hp*100)/100;ap=MathF.Round(ap*100)/100;
        string code=UiLanguage.Code;
        if(main==primary && extra==secondary && hp==health && ap==armor&&language==code)return;language=code;
        primary=main;secondary=extra;health=hp;armor=ap;
        RefreshDisplay();
    }
    // 0.1.146: which readout this wrist's watch shows (the ammunition one on
    // the weapon hand: the right one, the left one for a left-hander).
    private bool ammoFace;private bool ammoFaceSet;
    internal bool AmmoFace
    {
        get=>ammoFaceSet?ammoFace:right;
        set{if(ammoFaceSet&&ammoFace==value)return;ammoFace=value;ammoFaceSet=true;primary=secondary="-";RefreshDisplay();}
    }
    private void RefreshDisplay()
    {
        var geometry=WatchFaceGeometry.Build(AmmoFace,primary,secondary,health,armor,AmmoFace&&!right);
        for(int i=0;i<geometry.Vertices.Count;i++)geometry.Vertices[i]+=System.Numerics.Vector3.UnitY*faceLift;
        display?.Set(geometry);
    }
    internal void Pose(PoseValue pose,float gripTarget,float triggerTarget)
    {
        if(root==null || posedFrame==Time.frameCount)return;
        posedFrame=Time.frameCount;
        // 0.1.98: a hand holding an NPC body stays closed around it.
        if(BodyGrabState.Holding(right)){gripTarget=Math.Max(gripTarget,.9f);triggerTarget=1;}
        var position=CameraRig.UnityPosition(pose);var rotation=Rotation(pose,right);float size=1;bool held=false;
        Vector3 carryPosition=Vector3.zero,carryElbow=Vector3.zero;Quaternion carryRotation=Quaternion.identity;
        bool carrying=GripCarry.Current?.TryCarryHand(right,out carryPosition,out carryRotation,out carryElbow)==true;
        if(carrying){position=carryPosition;rotation=carryRotation;held=true;gripTarget=.82f;triggerTarget=.82f;WeaponHands.Current?.NoteSupportGlove(right,"the hand carrying something");}
        // 0.1.197: riding the zipline, the hand that held the hook is not drawn (only the free hand is there).
        if(!carrying&&ZiplineVr.Current?.HidesHand(right)==true){Hide();return;}
        bool rocketHeld=false;
        try
        {
            var weapons=WeaponHands.Current;
            if(!carrying&&native!=null && weapons!=null)
            {
                weapons.RegisterHand(native,right);
                held=weapons.TryPoseHand(native,right,out var gripPosition,out var gripRotation,out size);
                if(held){position=gripPosition;rotation=gripRotation;}
                // 0.1.198: the zipline hook held by its handle: the hand as on a pistol.
                else if(GameUiControls.Current?.Items.TryToolHand(right,pose,out var toolPosition,out var toolRotation)==true){position=toolPosition;rotation=toolRotation;}
                // 0.1.217: the bazooka's rocket taken from the pouch: the same hold, closed round its motor tube.
                else if(weapons.TryRocketHand(right,pose,out var rocketPosition,out var rocketRotation)){position=rocketPosition;rotation=rocketRotation;rocketHeld=true;}
                // 0.1.227: why the bazooka's support hand is not on its front grip (the log, once).
                if(!held&&!rocketHeld)weapons.NoteSupportGlove(right,"the glove drawn free at the controller");
            }
            if(held)HandImpact.Clear(right);
            else
            {
                var recoil=HandImpact.Offset(right,Time.realtimeSinceStartup);
                position+=rotation*new Vector3(recoil.X,recoil.Y,recoil.Z);
            }
            // 0.1.150: keys, cards, lockpicks and medkits in the taking hand (the left one for a left-hander).
            bool main=right!=WeaponHands.LeftHanded;
            bool keyHeld=!carrying&&main&&InteractionDriver.Current?.KeyActive==true;
            if(keyHeld){held=true;size=1;}
            // 0.1.103: a lockpick taken from the wheel is pinched like a key.
            bool pickHeld=main&&!held&&GameUiControls.Current?.Items.PinchHeld==true;
            if(pickHeld){held=true;size=1;}
            // 0.1.88: the grappling hook stays gripped in the left hand without
            // holding the controller grip (the fingers followed the controller).
            // (Also on the rope: the wheel item may be cleared by the game's
            // scripted rope state while the hook is still in this hand.)
            // 0.1.195: in either hand (the grappling or zipline hook), and on the zipline cable.
            bool gadgetHeld=!held&&(rocketHeld||GameUiControls.Current?.ItemHeldOn(right)==true||GrappleVr.Current?.DeviceShown==true&&GrappleVr.Current.Side==(right?1:0));
            if(gadgetHeld){held=true;size=1;}
            if(!carrying&&ContactRig.Current?.ResolveHand(right,held&&!keyHeld&&!gadgetHeld&&!pickHeld,ref position,ref rotation,!held&&gripTarget>.6f)==false)
            {if(held)WeaponHands.Current?.NoteSupportGlove(right,"the glove hidden (the gun not clear of the world)");root.SetActive(false);return;}
            // 0.1.146: a hand laying a bolt along the crossbow rests on its rail.
            if(!carrying)weapons?.RestOnRail(right,ref position,rotation);
            if(native!=null)
            {
                float blend=1-MathF.Exp(-18*Math.Clamp(Time.unscaledDeltaTime,0,.05f));
                curl+=(gripTarget-curl)*blend;triggerCurl+=(triggerTarget-triggerCurl)*blend;
                var elbow=native.ForearmRest;
                var rig=CameraRig.Current;
                if(rig!=null)
                {
                    var headOrigin=rig.HeadPosition;var yaw=Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0);
                    var wrist=position+rotation*new Vector3(0,0,NativeHandMesh.WristZ);
                    var shoulder=headOrigin+yaw*new Vector3(right?.19f:-.19f,-.20f,0);
                    var pole=yaw*new Vector3(right?1:-1,-1,-.35f);
                    var target=ArmIkMath.Elbow(ToN(shoulder),ToN(wrist),ToN(pole),elbow.Length()*(held?size:1));
                    var relative=Quaternion.Inverse(rotation)*(new Vector3(target.X,target.Y,target.Z)-wrist);
                    elbow=ToN(relative)/(held?size:1);
                }
                if(carrying)
                {
                    var wrist=position+rotation*new Vector3(0,0,NativeHandMesh.WristZ);
                    elbow=ToN(Quaternion.Inverse(rotation)*(carryElbow-wrist));
                }
                long timer=FramePerformance.Begin();
                // 0.1.186: also a medkit taken from a forearm, in the hand that took it.
                bool itemHeld=main&&GameUiControls.Current?.PendingConsumable==true||GameUiControls.Current?.Items.ArmHeld(right)==true;
                string profile=itemHeld?GameUiControls.Current!.Items.GripProfile:weapons?.HandProfile??"";
                // 0.1.128: a still copy in this hand, or the game's weapon in the left hand (mirrored holds).
                if(held&&!itemHeld&&weapons?.HandProfileFor(right) is string sided)profile=sided;
                if(keyHeld){var interaction=InteractionDriver.Current!;profile=interaction.KeyGripProfile;interaction.PrepareKeyHand(native);}
                // 0.1.198: the zipline hook's handle in the closed hand.
                // 0.1.226: the rocket's thicker tube with the fingers and the thumb opened round it.
                if(gadgetHeld)profile=rocketHeld&&weapons!=null?weapons.RocketGripProfile(right):GameUiControls.Current?.Items.Kind==HandToolKind.Zipline?WeaponHands.LongHandleGrip:GrappleVr.Current?.HandProfileFor(right)??"gadget";
                if(pickHeld){profile=ScrewdriverGrip.Profile;GameUiControls.Current!.Items.PrepareHand(native);}
                if(!right&&weapons?.LeftPistolVisible==true)profile="dual_pistol";
                // 0.1.133: the hand holding a magazine (the right one for a gun in the left hand).
                if(weapons?.ReloadHandHolding(right)==true)profile="reload_"+weapons.Profile;
                if(carrying)profile="prop_carry_neck"; // both index fingers curl; no firearm trigger pose
                try{native.Refresh(held,held?size:1,curl,triggerCurl,profile,elbow);}
                finally{FramePerformance.End(timer,0);}
            }
        }
        catch(Exception ex){native?.Dispose();native=null;Report(ex);}
        WeaponHands.Current?.NoteHandDrawn(right,held,position);
        root.transform.SetPositionAndRotation(position,rotation);root.transform.localScale=Vector3.one;
        if(!carrying&&right!=WeaponHands.LeftHanded)InteractionDriver.Current?.RenderKey(root.transform);
        if(!carrying)WeaponHands.Current?.PoseHeldAmmunition(right,position,rotation);
        if(rocketHeld)WeaponHands.Current?.PoseHeldRocket(right,position,rotation);
        if(wearable!=null)
        {
            var swing=native?.ForearmSwing??System.Numerics.Quaternion.Identity;
            var mount=WatchMountMath.BandPose(fit,swing,held?size:1);
            var strap=WatchMountMath.BandPose(fit,swing,held?size:1);
            if(bandRoot!=null){bandRoot.transform.localScale=Vector3.one*(held?size:1);
                bandRoot.transform.localPosition=new Vector3(strap.position.X,strap.position.Y,strap.position.Z);
                bandRoot.transform.localRotation=new Quaternion(strap.rotation.X,strap.rotation.Y,strap.rotation.Z,strap.rotation.W);}
            wearable.transform.localScale=Vector3.one*(held?size:1);
            wearable.transform.localRotation=new Quaternion(mount.rotation.X,mount.rotation.Y,mount.rotation.Z,mount.rotation.W);
            wearable.transform.localPosition=new Vector3(mount.position.X,mount.position.Y,mount.position.Z);
        }
        // Digits inherit this exact transform. Never place/reproject them as UI.
        var displayRoot=wearable!.transform;
        var o=HandMeshGeometry.ScreenPosition;var p=displayRoot.TransformPoint(new Vector3(o.X,o.Y+faceLift,o.Z));
        var head=CameraRig.Current?.HeadPosition ?? p;
        display?.Show(Vector3.Dot(displayRoot.up,(head-p).normalized)>.03f && Vector3.Distance(p,head)<1.3f);
        root.SetActive(true);
    }
    private static System.Numerics.Vector3 ToN(Vector3 p)=>new(p.x,p.y,p.z);
    internal static Quaternion Rotation(PoseValue pose,bool rightHand)=>rightHand?ControllerAim.Rotation(pose):CameraRig.UnityRotation(pose)*Quaternion.Euler(45,0,0);
    internal void InvalidatePose(){posedFrame=-1;native?.ResetRenderPose();}
    // 0.1.186: the forearm's cut as drawn this frame (ArmMedkits).
    internal bool TryCropEnd(out Vector3 center,out Vector3 outward,out Vector3 up)
    {
        center=outward=up=Vector3.zero;
        return Visible&&native!=null&&native.TryCropWorld(out center,out outward,out up);
    }
    internal void Hide(){if(root!=null&&root.activeSelf){root.SetActive(false);InvalidatePose();}ContactRig.Current?.ResetHand(right);HandImpact.Clear(right);}
    private void Report(Exception ex)
    {if(Time.realtimeSinceStartup<nextError)return;nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("NATIVE HAND "+(right?"R":"L")+" unavailable; watch remains active: "+ex.Message);}
    public void Dispose()
    {
        native?.Dispose();native=null;band?.Dispose();band=null;display?.Dispose();display=null;watch?.Dispose();watch=null;
        if(root!=null)UnityEngine.Object.Destroy(root);root=null;
    }
}
