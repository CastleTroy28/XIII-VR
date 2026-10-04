using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.215: the bazooka reloads by hand (with "Manual reload" on). Empty, the
// hand not holding its handle takes a rocket from the belt pouch with its
// trigger and puts the rocket's tail into the front of the tube: the rocket
// goes in with the game's own reload sound, one from the reserve. Letting go
// of the trigger anywhere else puts it back in the pouch. The game's own
// reload (B, or after a shot) is not used then. Loaded, a red dot marks where
// the rocket will hit: the bazooka's sight on the model is not where it
// flies.
internal sealed partial class WeaponHands
{
    private int rocketSide=-1;private RocketVisual? rocket;private IntPtr rocketFor;
    private readonly ThrowMarker aimDot=new("XIII bazooka aim dot",true);
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> aimHits=new(24);
    private ReloadGlow? bazookaGlow;private float nextBazookaError;
    private bool BazookaManual=>WeaponOptions.ManualReload.Value&&profile=="bazooka"&&weapon!=null&&ammo!=null&&!DualActive;
    internal bool RocketIn(int side)=>rocketSide==side;
    private int BazookaReserve{get{try{return ammo==null?0:ammo.ammoPool.IsInfinite?-1:Math.Max(0,ammo.PrimaryReserveAmmoCount);}catch(Exception){return 0;}}}
    private void TickBazooka()
    {
        try{BazookaHoldReport();}catch(Exception){}
        try{TickBazookaCore();}
        catch(Exception ex){PutRocketBack("an error");HideBazooka();if(Time.realtimeSinceStartup>=nextBazookaError){nextBazookaError=Time.realtimeSinceStartup+10;Bootstrap.Warn("BAZOOKA hand reload: "+ex.Message);}}
    }
    private void TickBazookaCore()
    {
        // 0.1.217: with the hand reload the rocket in the tube is drawn by the mod (loaded: in it, empty: none).
        if(profile=="bazooka"&&visual!=null&&ammo!=null)visual.RocketInTube(BazookaManual,ammo.PrimaryMagazineAmmoCount>0);
        if(profile!="bazooka"||weapon==null||ammo==null||visual==null||!CanControl(playerId)||inventory==null||inventory.isInTransit||!poseValid)
        {PutRocketBack(profile!="bazooka"?"the bazooka left the hand":"");HideBazooka();return;}
        bool empty=ammo.PrimaryMagazineAmmoCount<=0;
        // Loaded: where the rocket hits.
        if(!empty){PutRocketBack("already loaded");bazookaGlow?.Hide();ShowAimDot();return;}
        aimDot.Hide();
        if(!BazookaManual){PutRocketBack("");bazookaGlow?.Hide();return;}
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)){PutRocketBack("no tracking");return;}
        int s=PrimaryLeft?1:0;var pose=s==0?l:r;var input=s==0?rig.LeftControls:rig.RightControls;
        if(s==0&&!leftValid||!input.Valid){PutRocketBack("no tracking");return;}
        var hand=CameraRig.UnityPosition(pose);
        if(rocketSide<0)
        {
            bool free=copyKey[s]<0&&!(barrelHand==s)&&!SupportHeld&&GameUiControls.Current?.ItemHeldOn(s==1)!=true&&InteractionDriver.Current?.HandOccupied(s==1)!=true&&GripCarry.Current?.HidesHand(s==1)!=true;
            bool atPouch=pouch!=null&&pouch.Shown&&pouch.Near(hand);
            // 0.1.221: the grip takes it (was the trigger).
            if(BazookaReloadMath.Take(true,BazookaReserve,free,atPouch,(input.Down&AmmoButton)!=0))TakeRocket(s);
            else ShowBazookaGlow(pouch!=null&&pouch.Shown?pouch.Center:(Vector3?)null);
            if(rocketSide<0)return;
        }
        // 0.1.217: in the closed fist round its motor tube, as a stick: its tail below the
        // little finger, its warhead above the thumb (the glove draws it there: PoseHeldRocket).
        RocketHold(s==1,pose,out var handAt,out var handTurn);
        var rootAt=handAt+handTurn*rocketAt;var rootTurn=handTurn*rocketTurn;
        rocket!.PoseRoot(rootAt,rootTurn);
        var forward=rootTurn*Vector3.forward;
        var muzzle=BazookaMouth(out var tube);
        ShowBazookaGlow(muzzle);
        var tail=rootAt+rootTurn*rocket.Tail;
        if(BazookaReloadMath.Insert(ToN(tail),ToN(forward),ToN(muzzle),ToN(tube))){InsertRocket(s,muzzle);return;}
        if((input.Held&AmmoButton)==0)PutRocketBack("let go away from the tube");
    }
    private void TakeRocket(int s)
    {
        var w=weapon!;
        if(rocket==null||!rocket.Valid||rocketFor!=w.Pointer)
        {
            rocket?.Dispose();
            GameObject? prefab=null;
            try{var rc=fire?.TryCast<RocketProjectileComponent>()??w.GetComponentInChildren(Il2CppType.Of<RocketProjectileComponent>(),true)?.TryCast<RocketProjectileComponent>();prefab=rc?.projectileUsedByPlayer??rc?.projectile??fire?.projectile;}
            catch(Exception){}
            rocket=RocketVisual.Create(prefab);rocketFor=w.Pointer;
            Bootstrap.Write("BAZOOKA rocket for the hand: "+rocket.Source+" length="+rocket.Length.ToString("F2"));
        }
        rocketSide=s;rig.PunchHaptics(s==1);StopOwnedFire();
        Bootstrap.Write("BAZOOKA rocket taken from the pouch by the "+Side(s)+" hand (reserve "+(BazookaReserve<0?"unlimited":BazookaReserve.ToString())+"): its tail into the front of the tube");
    }
    private void PutRocketBack(string why)
    {
        if(rocketSide<0)return;
        rocket?.Hide();int s=rocketSide;rocketSide=-1;
        if(why.Length>0)Bootstrap.Write("BAZOOKA rocket back in the pouch ("+why+")");
        if(why=="let go away from the tube")rig.PunchHaptics(s==1);
    }
    private void InsertRocket(int s,Vector3 mouth)
    {
        var a=ammo!;
        int got=a.ammoPool.IsInfinite?1:a.ammoPool.TryRemoveAmmo(a.primaryAmmoType,1);
        if(got<=0){PutRocketBack("no rocket left");return;}
        rocket?.Hide();rocketSide=-1;
        ResetNativeReload("rocket in");SetMagazine(Math.Max(1,a.PrimaryMagazineAmmoCount+got));
        nativeReloadRefused=true;ReleaseNativeFire("rocket in");
        // The game's own reload sound.
        bool sound=false;
        try
        {
            var audio=weapon!.GetComponent(Il2CppType.Of<AudioComponent>())?.TryCast<AudioComponent>();
            if(audio?.audioSubComponents!=null)foreach(var sub in audio.audioSubComponents){var reloadSound=sub?.TryCast<ReloadSound>();if(reloadSound!=null){reloadSound.FireSound(weapon);sound=true;break;}}
        }
        catch(Exception ex){Bootstrap.Warn("BAZOOKA reload sound: "+ex.Message);}
        rig.ReloadHaptics(ReloadAction.Insert,s==1);bazookaGlow?.Hide();
        Bootstrap.Write("BAZOOKA rocket in by the "+Side(s)+" hand: magazine="+a.PrimaryMagazineAmmoCount+" reserve="+a.PrimaryReserveAmmoCount+(sound?" (the game's reload sound)":" (no reload sound found)"));
    }
    // The tube's mouth and the way the tube points. 0.1.217: the front of the
    // tube itself (from its mesh), not the aim point well ahead of it.
    private Vector3 BazookaMouth(out Vector3 forward)
    {
        var frame=visual!.FittedToWorld;
        forward=frame.MultiplyVector(Vector3.forward).normalized;
        if(!(forward.sqrMagnitude>.5f))forward=aimForward.sqrMagnitude>1e-6f?aimForward.normalized:Vector3.forward;
        return visual.TubeMouth is Vector3 mouth?frame.MultiplyPoint3x4(mouth):aimPosition;
    }
    // 0.1.217: the rocket in the hand: the hand as on a pistol's grip (at the
    // controller), the rocket's motor tube along that grip's line deep in the
    // closed fingers (the zipline hook's handle is held the same way). Its
    // root in the hand's frame: rocketAt / rocketTurn.
    private Vector3 rocketAt;private Quaternion rocketTurn=Quaternion.identity;
    // 0.1.226: the fingers and the thumb opened round the rocket's motor tube
    // (thicker than the hook's handle; the thumb went into it). 0.1.227: the
    // tube along the knuckles through the middle of the circle those fingers
    // lie on, each finger closed onto it and the thumb wrapped round it clear
    // of it (FingerPoseMath.FitGrip) - 0.1.226 kept it deep in the palm, where
    // the hook's handle lies, and the opened fingers stood off the tube.
    private float rocketCurl=FingerPoseMath.HeldCurl;private System.Numerics.Vector3 rocketChannel,rocketLittle;private bool rocketChannelValid;private string rocketCurlFor="",rocketProfile="";
    internal const string RocketPrefix="prop_rocket@";
    private void RocketWrap(bool right)
    {
        if(rocket==null)return;var native=right?rightNative:leftNative;
        string key=(right?"R":"L")+rocket.TubeRadius.ToString("F4")+(native!=null?"n":"");
        if(key==rocketCurlFor)return;rocketCurlFor=key;rocketProfile="";
        rocketChannelValid=native!=null&&native.GripFit(RocketPrefix,rocket.TubeRadius,rocket.Length*.15f,false,out rocketCurl,out rocketChannel,out rocketLittle,out rocketProfile);
        if(!rocketChannelValid){rocketCurl=native!=null?native.WrapCurlThick(rocket.TubeRadius,out _):FingerPoseMath.HeldCurl;rocketChannel=default;rocketProfile="";}
        var fist=RocketFist();
        Bootstrap.Write("BAZOOKA rocket in the "+(right?"right":"left")+" hand: its tube "+(rocket.TubeRadius*200).ToString("F1")+" cm thick, fingers closed "+rocketCurl.ToString("F2")+" (a stick's "+FingerPoseMath.HeldCurl.ToString("F2")+")"
            +(fist is Vector3 f?", along the knuckles through the middle of their curl "+(f*100).ToString("F1")+" cm in the hand; "+native!.GripFitReport(rocketProfile):", its line where the hook's handle lies (the hand's fingers not measured)"));
    }
    // The rocket's line in the hand (the hand's frame as drawn, at full size).
    private Vector3? RocketFist()=>rocketChannelValid?ToU(BazookaTubeMath.DrawnChannel(rocketChannel,1)):null;
    internal string RocketGripProfile(bool right){RocketWrap(right);return rocketProfile.Length>0?rocketProfile:Math.Abs(rocketCurl-FingerPoseMath.HeldCurl)>.005f?FingerPoseMath.WrapProfile(rocketCurl):LongHandleGrip;}
    private void RocketHold(bool right,PoseValue pose,out Vector3 handAt,out Quaternion hand)
    {
        var bar=BazookaTubeMath.HoldBar(ToN(rocket!.Middle),rocket.Length);
        RocketWrap(right);var fist=RocketFist();
        // The tube along the fingers' line, its warhead toward the thumb.
        Vector3? along=fist!=null?-ToU(rocketLittle):null;
        if(TryToolHold(right,pose,bar,out handAt,out hand,out rocketAt,out rocketTurn,fist,along))return;
        // No pistol hold read yet: the hand as the controller holds it.
        hand=HandAim(pose,right);handAt=CameraRig.UnityPosition(pose);
        var contact=fist??LongHandleContact(right?rightNative:leftNative,1)+FistShift;
        var placed=GripBarMath.Hold(bar,along is Vector3 a?ToN(a):ClubMath.GripLine,ClubMath.GripForward,ToN(contact));
        rocketTurn=new Quaternion(placed.rotation.X,placed.rotation.Y,placed.rotation.Z,placed.rotation.W);rocketAt=new Vector3(placed.position.X,placed.position.Y,placed.position.Z);
    }
    // The glove of the hand holding the rocket: posed as on a pistol, closed round it.
    internal bool TryRocketHand(bool right,PoseValue pose,out Vector3 position,out Quaternion rotation)
    {
        position=Vector3.zero;rotation=Quaternion.identity;
        if(rocketSide!=(right?1:0)||rocket==null||!rocket.Valid)return false;
        try{RocketHold(right,pose,out position,out rotation);return true;}catch(Exception){return false;}
    }
    // The rocket in that glove as drawn (after its collisions).
    internal void PoseHeldRocket(bool right,Vector3 hand,Quaternion rotation)
    {
        if(rocketSide!=(right?1:0)||rocket==null||!rocket.Valid)return;
        rocket.PoseRoot(hand+rotation*rocketAt,rotation*rocketTurn);
    }
    private void ShowBazookaGlow(Vector3? at)
    {
        if(at is not Vector3 p){bazookaGlow?.Hide();return;}
        bazookaGlow??=new ReloadGlow();bazookaGlow.Show(p,.03f);
    }
    // Where the rocket will hit: the game launches it at the point its
    // aim ray meets (the muzzle along the tube, HitPoint).
    private void ShowAimDot()
    {
        if(handler==null||!(aimForward.sqrMagnitude>1e-6f)){aimDot.Hide();return;}
        float range=fire!=null&&fire.maxRange>0&&float.IsFinite(fire.maxRange)?fire.maxRange:300;
        int count=Physics.RaycastNonAlloc(aimPosition,aimForward,aimHits,range,handler.raycastCollisionMask,QueryTriggerInteraction.Ignore);
        float best=float.PositiveInfinity;Vector3 point=default,normal=Vector3.up;bool found=false;
        for(int i=0;i<count&&i<aimHits.Length;i++)
        {
            var hit=aimHits[i];var c=hit.collider;
            if(c==null||!(hit.distance>0)||hit.distance>=best)continue;
            if(playerRoot!=null&&c.transform.IsChildOf(playerRoot))continue;
            best=hit.distance;point=hit.point;normal=hit.normal;found=true;
        }
        if(!found){aimDot.Hide();return;}
        aimDot.Show(point,normal,BazookaReloadMath.DotRadius(Vector3.Distance(rig.HeadPosition,point)));
    }
    private void HideBazooka(){aimDot.Hide();bazookaGlow?.Hide();}
    private void DisposeBazooka(){rocket?.Dispose();rocket=null;rocketSide=-1;aimDot.Dispose();bazookaGlow?.Dispose();bazookaGlow=null;}
}
