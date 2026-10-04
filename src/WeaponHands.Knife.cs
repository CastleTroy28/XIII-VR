using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
using Slot=PlayerEquipableInventory.ActiveEquipmentSlot;
namespace XiiiXR;
// Throwing knives. 0.1.149: held by the grip (ThrowGrip, as the "Weapon in
// hand" setting says); the grip opening during a swing throws it along the
// hand's fastest forward motion (SwingRelease); opening it without a swing, or
// at the knife's place on the chest, puts it back there. The game's own throw
// (ammo, sound, damage) runs once on that release; only its origin and
// direction come from the tracked hand. The right hand throws the game's
// knife; a knife held in the left hand (a copy) is thrown by selecting the
// game's knife for that one throw, then the right hand's weapon comes back.
// With "Throwing: hold + flight path" (VR settings) the line of flight is
// drawn while the grip is held and letting go throws along the controller.
internal sealed partial class WeaponHands
{
    private readonly SwingRelease[] swings={new(),new()};
    private readonly ThrowGrip[] throwGrips={new(),new()};
    private readonly int[] throwHeldKey={-1,-1},throwSeenFrame={-10,-10};
    private int knifePressFrame=-1;
    private Vector3 knifeOrigin,knifeDirection;
    private float knifeLaunchUntil,knifeLaunchedAt=-10,knifeReleasedAt=-10,knifeHiddenUntil;
    private bool knifeByGrip;
    // 0.1.214: a throw let go while the game's knife was not ready yet (its
    // last throw still playing, or drawn again): pressed once it is.
    private float knifePressDue=-1;
    // Which hand threw the knife last (the one that takes the next one at once).
    private int knifeThrowSide=1;
    private bool KnifeLaunching=>profile=="knife"&&Time.realtimeSinceStartup<knifeLaunchUntil;
    // Every frame (before the hands are handled): each hand's path, for a
    // throw when its grip opens.
    private void SampleSwings()
    {
        float now=Time.realtimeSinceStartup;
        if(rig.SampleRightRelative(out var r,out _))swings[1].Sample(now,ToN(r));else swings[1].Reset();
        if(rig.SampleLeftRelative(out var l,out _))swings[0].Sample(now,ToN(l));else swings[0].Reset();
        NoteGrips(now);
    }
    // Hand s let go: a throw (world direction, hand speed), or why not.
    private bool SwingThrow(int s,out Vector3 direction,out float speed,out string why)
    {
        direction=default;speed=0;why="no tracking";
        Quaternion toWorld;
        bool ok=s==1?rig.SampleRightRelative(out _,out toWorld):rig.SampleLeftRelative(out _,out toWorld);
        if(!ok||!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||s==0&&!leftValid)return false;
        var aim=ControllerAim.Rotation(s==1?r:l)*Vector3.forward;
        var inverse=Quaternion.Inverse(toWorld);
        var result=swings[s].Release(Time.realtimeSinceStartup,ToN(inverse*aim),ToN(inverse*(rig.HeadRotation*Vector3.forward)));
        speed=result.Speed;why=result.Reason;
        if(!result.Throw)return false;
        direction=(toWorld*new Vector3(result.Direction.X,result.Direction.Y,result.Direction.Z)).normalized;
        return true;
    }
    // The flight-line option: along where the controller points.
    private bool AimThrow(int s,out Vector3 direction)
    {
        direction=default;
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||s==0&&!leftValid)return false;
        direction=ControllerAim.Rotation(s==1?r:l)*Vector3.forward;return true;
    }
    private Vector3 HandPoint(int s){return rig.SampleWorldHands(out var l,out var r,out _)?CameraRig.UnityPosition(s==1?r:l):rig.HeadPosition;}
    // A throwable thing came to hand s (another one, or after frames without
    // it): held by the grip if the grip is holding it now.
    // true: fresh (0.1.222).
    private bool ThrowHeld(int s,int key,WeaponGripMode mode,bool held)
    {
        int f=Time.frameCount;bool fresh=throwHeldKey[s]!=key||throwSeenFrame[s]<f-1;
        if(fresh)throwGrips[s].Took(held,mode);
        throwHeldKey[s]=key;throwSeenFrame[s]=f;return fresh;
    }
    // ---- The game's knife in the right hand (from TickGameWeapon) ----
    private void KnifeGrip(int s,Vector3 hand,bool held,bool down,WeaponGripMode mode,int key)
    {
        if(knifeByGrip&&Time.realtimeSinceStartup-knifeReleasedAt<1.5f)return;   // being thrown
        ThrowHeld(s,weapon!=null?weapon.GetInstanceID():key,mode,held);
        bool arc=QualityOptions.ThrowArc.Value;
        // 0.1.214: where it lands (not at its place on the chest: letting go there puts it back).
        if(held&&throwGrips[s].Armed&&holsters!=null&&holsters.NearestPlace(key,"knife",hand)==HolsterSlot.None)AimKnifeLanding(s,hand,arc);
        if(arc&&held&&throwGrips[s].Armed&&AimThrow(s,out var line))DrawThrowPreview(hand+line*.12f,line*30,false,1);
        else if(knifeAiming){knifeAiming=false;throwRoot?.SetActive(false);}
        if(arc&&held&&throwGrips[s].Armed)knifeAiming=true;
        Vector3 direction=default;float speed=0;string why="";
        var step=throwGrips[s].Step(mode,held,down,()=>
        {
            if(holsters!=null&&holsters.NearestPlace(key,"knife",hand)!=HolsterSlot.None){why="at its place";return false;}
            if(arc){speed=0;why="aimed";return AimThrow(s,out direction);}
            return SwingThrow(s,out direction,out speed,out why);
        });
        if(step==ThrowGripStep.None)return;
        if(knifeAiming){knifeAiming=false;throwRoot?.SetActive(false);}
        gripState.Reset();throwHeldKey[s]=-1;
        if(step==ThrowGripStep.Throw){ThrowGameKnife(s,hand,direction,speed,arc);return;}
        Bootstrap.Write("KNIFE let go without a throw ("+why+(speed>0?" handSpeed="+speed.ToString("F2"):"")+"): back to its place");
        PutAway(key,hand,handVelocity[s],s);
    }
    private void ThrowGameKnife(int s,Vector3 hand,Vector3 direction,float speed,bool aimed)
    {
        float now=Time.realtimeSinceStartup;
        knifeDirection=direction.normalized;knifeOrigin=hand+knifeDirection*.12f;knifeThrowSide=s;
        knifeReleasedAt=now;knifeByGrip=true;knifeHiddenUntil=now+1.5f;
        rig.PunchHaptics(s==1);
        string how="KNIFE THROW "+(aimed?"aimed ":"")+"grip let go"+(aimed?"":" handSpeed="+speed.ToString("F2"))+" direction="+knifeDirection.ToString("F2");
        // 0.1.214: the next knife can be in the hand before the game has
        // finished its last throw: the press waits for the game's knife.
        if(!KnifeReady(out string busy))
        {
            knifePressDue=now+KnifeRetakeMath.PressWait;knifeLaunchUntil=knifePressDue+.75f;knifeHiddenUntil=knifePressDue+1.5f;
            Bootstrap.Write(how+"; the game's knife not ready ("+busy+"): pressed once it is");
            FreezeKnifeLanding();
            return;
        }
        knifeLaunchUntil=now+.75f;
        // Native input is polled during the next frames: Down once, held
        // briefly, then Up, exactly like a quick physical press.
        knifePressFrame=Time.frameCount+1;
        Bootstrap.Write(how+"; native throw pressed");
        FreezeKnifeLanding();
    }
    // The game's knife can start a throw now (else why not).
    private bool KnifeReady(out string why)
    {
        why="";
        if(weapon==null||profile!="knife"){why="no knife";return false;}
        if(inventory?.isInTransit==true){why="being drawn";return false;}
        try{var t=weapon.GetComponent(Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>();if(t!=null&&!t.CanStart()){why="its last throw still playing";return false;}}
        catch(Exception){}
        return true;
    }
    // 0.1.214: after a throw the next knife comes at once. The game keeps its
    // knife selected (the next of the stack) while it plays its throw; the
    // hand waited for it to be emptied (TickGrenadeAfter) and the knife drawn
    // again, over a second. Once the thrown knife has left, a grip press of
    // the hand that threw it at the knife's place on the chest holds the knife
    // the game still has in hand.
    private void KnifeRetake(int s,Vector3 hand)
    {
        if(profile!="knife"||weapon==null||holsters==null||GripMode==WeaponGripMode.Always||s!=knifeThrowSide)return;
        bool thrown=KnifeRetakeMath.Thrown(knifeByGrip,knifeReleasedAt,knifeLaunchedAt,emptyAfterThrowAt>0&&emptyAfterThrowSlot==Slot.Knife);
        if(!thrown||!GripInput(s).down)return;
        int key=(int)weapon.slot;
        bool at=holsters.NearestPlace(key,"knife",hand)!=HolsterSlot.None;int left=holsters.CountOf(key);
        if(!KnifeRetakeMath.Retake(thrown,true,at,left))return;
        float now=Time.realtimeSinceStartup;
        knifeByGrip=false;knifeReleasedAt=-10;knifeHiddenUntil=0;knifeLaunchUntil=0;knifePressDue=-1;
        emptyAfterThrowAt=-1;gripTakeAt=now;throwHeldKey[s]=-1;
        rig.PunchHaptics(s==1);
        Bootstrap.Write("KNIFE the next one taken from the chest at once ("+(left>=0?left+" left":"count unknown")+(inventory?.isInTransit==true?"; the game still drawing it":"")+")");
    }
    // ---- A knife held as a copy (the left hand, or a hand whose game weapon is elsewhere) ----
    private void TickCopyKnife(int s,Vector3 hand,bool held,bool down,WeaponGripMode mode)
    {
        if(holsters==null)return;
        hinted[s]=true;
        bool snap=holsters.Hint(copyKey[s],copyProfile[s],hand,out _,s==0);
        if(snap&&!copySnap[s])rig.PunchHaptics(s==1);copySnap[s]=snap;
        ThrowHeld(s,1000+copyKey[s],mode,held);
        bool arc=QualityOptions.ThrowArc.Value;
        if(held&&throwGrips[s].Armed&&!snap)AimKnifeLanding(s,hand,arc);
        Vector3 direction=default;float speed=0;string why="";
        var step=throwGrips[s].Step(mode,held,down,()=>
        {
            if(snap){why="at its place";return false;}
            if(arc){why="aimed";return AimThrow(s,out direction);}
            return SwingThrow(s,out direction,out speed,out why);
        });
        if(step==ThrowGripStep.Throw){StartCopyKnifeThrow(s,hand,direction,speed);return;}
        if(step==ThrowGripStep.Release){Bootstrap.Write("KNIFE ("+Side(s)+" hand) let go without a throw ("+why+"): back to its place");PutAwayCopy(s,hand);throwHeldKey[s]=-1;}
    }
    // The game throws only its selected knife: selected (instantly) for this
    // throw from where the hand let go, then the right hand's weapon comes back.
    private void StartCopyKnifeThrow(int s,Vector3 hand,Vector3 direction,float speed)
    {
        if(inventory==null||holsters==null)return;
        holsters.CopyOf(copyKey[s])?.Hide();
        var current=inventory.currentEquipable;
        leftRestoreKey=current!=null?(int)current.slot:(int)Slot.Fist;
        int key=copyKey[s];copyKey[s]=-1;copyProfile[s]="";throwHeldKey[s]=-1;holsters.Return(key,"knife");
        float now=Time.realtimeSinceStartup;
        leftThrowOrigin=hand+direction.normalized*.12f;leftThrowVelocity=direction.normalized*Math.Max(speed,.1f);leftThrowAt=now;leftThrowKnife=true;leftThrowSide=s;knifeThrowSide=s;
        FreezeLanding(s,leftThrowOrigin,direction.normalized*KnifeFlightSpeed,KnifeGravity);
        // 0.1.156: kept on the body - the game's knife comes a moment later; the
        // throw still leaves from where the hand let go of it (the player may
        // have walked or turned meanwhile), not from the game's right hand.
        var body=rig.PlayerRoot;copyThrowBody=body!=null;
        if(body!=null){copyThrowLocal=body.InverseTransformPoint(leftThrowOrigin);copyThrowLocalDirection=body.InverseTransformDirection(leftThrowVelocity.normalized);}
        bool ok;
        try{ok=current!=null&&current.slot==Slot.Knife||inventory.TrySelectSlot(Slot.Knife,true,true,true,false,true);}
        catch(Exception ex){ok=false;Bootstrap.Warn("KNIFE ("+Side(s)+" hand) select: "+ex.Message);}
        if(!ok){leftThrowKnife=false;Bootstrap.Write("KNIFE ("+Side(s)+" hand) the game refused its knife; not thrown");return;}
        leftThrowPending=true;knifeHiddenUntil=now+1.5f;rig.PunchHaptics(s==1);
        Bootstrap.Write("KNIFE THROW ("+Side(s)+" hand) grip let go handSpeed="+speed.ToString("F2")+" direction="+direction.normalized.ToString("F2")+"; the game's knife selected for the throw");
    }
    // From TickLeftThrow while a copy's knife throw waits for the game's knife.
    private bool copyThrowBody;private Vector3 copyThrowLocal,copyThrowLocalDirection;
    private void TickCopyKnifeThrow(float now)
    {
        if(profile!="knife"||weapon==null||inventory==null||inventory.isInTransit)return;
        if(knifeLaunchedAt>leftThrowAt){leftThrowPending=false;leftThrowKnife=false;leftRestoreAt=now+.4f;knifeHiddenUntil=Math.Max(knifeHiddenUntil,now+.9f);return;}
        if(knifePressFrame>=0||KnifeLaunching)return;
        try{var t=weapon.GetComponent(Il2CppType.Of<ThrowingComponent>())?.TryCast<ThrowingComponent>();if(t!=null&&!t.CanStart())return;}catch(Exception){}
        knifeOrigin=leftThrowOrigin;knifeDirection=leftThrowVelocity.normalized;
        var body=rig.PlayerRoot;
        if(copyThrowBody&&body!=null){knifeOrigin=body.TransformPoint(copyThrowLocal);knifeDirection=body.TransformDirection(copyThrowLocalDirection).normalized;}
        // Until the game's knife leaves (its throw may take longer than a quick press's 0.75 s).
        knifeLaunchUntil=Math.Max(now+.75f,leftThrowAt+1.6f);knifeByGrip=false;
        knifePressFrame=Time.frameCount+1;if(leftThrowSide==0)leftShotUntil=now+1.6f;
        Bootstrap.Write("KNIFE THROW ("+Side(leftThrowSide)+" hand) native throw pressed");
    }
    // Every frame: the native press window, the flight-line option's line
    // hidden, a throw that never happened.
    private void TickKnifeThrow()
    {
        if(knifePressFrame>=0&&Time.frameCount>knifePressFrame+10)knifePressFrame=-1;
        float now=Time.realtimeSinceStartup;
        if(knifePressDue>0)
        {
            if(profile!="knife"||now>knifePressDue){knifePressDue=-1;Bootstrap.Write("KNIFE THROW not pressed: the game's knife never got ready");knifeByGrip=false;knifeReleasedAt=-10;knifeHiddenUntil=0;knifeLaunchUntil=0;}
            else if(KnifeReady(out _))
            {
                Bootstrap.Write("KNIFE THROW native throw pressed "+(now-knifeReleasedAt).ToString("F2")+" s after the grip let go (the game's knife was not ready)");
                knifePressDue=-1;knifePressFrame=Time.frameCount+1;knifeReleasedAt=now;knifeLaunchUntil=now+.75f;knifeHiddenUntil=now+1.5f;
            }
            else{TickKnifeFlight();return;}
        }
        TickKnifeFlight();
        if(knifeByGrip&&knifeReleasedAt>0&&now-knifeReleasedAt>1.5f)
        {
            if(knifeLaunchedAt<knifeReleasedAt)Bootstrap.Write("KNIFE THROW the game did not throw the knife (it stays in the hand)");
            knifeByGrip=false;knifeReleasedAt=-10;knifeHiddenUntil=0;
        }
        if(profile!="knife"&&knifeAiming){knifeAiming=false;throwRoot?.SetActive(false);}
        // "Weapon in hand: always" (no body places): the knife is thrown the same way here.
        if(GripMode==WeaponGripMode.Always&&profile=="knife"&&weapon!=null&&!LeftThrowBusy&&inventory?.isInTransit==false&&CanControl(playerId))
        {
            var c=rig.RightControls;
            if(c.Valid)KnifeGrip(1,HandPoint(1),(c.Held&HandControls.Grip)!=0,(c.Down&HandControls.Grip)!=0,WeaponGripMode.Always,(int)weapon.slot);
        }
    }
    private bool knifeAiming;
    private bool KnifeButton(int phase)
    {
        if(knifePressFrame<0)return false;
        int f=Time.frameCount;
        return phase switch{1=>f==knifePressFrame,0=>f>=knifePressFrame&&f<knifePressFrame+3,_=>f==knifePressFrame+3};
    }
    // Native aim/hit queries follow the controller (or the throw) rather than
    // the knife model, whose pinch lies along the fingers.
    private void KnifeAim(PoseValue right)
    {
        var controller=ControllerAim.Rotation(right);
        aimForward=KnifeLaunching?knifeDirection:controller*Vector3.forward;
        aimPosition=KnifeLaunching?knifeOrigin:CameraRig.UnityPosition(right)+aimForward*.12f;
        aimUp=controller*Vector3.up;
    }
    private bool KnifeLaunch(ThrowingComponent component,Projectile projectile,ref Vector3 hitPoint,ref bool missTarget)
    {
        if(!KnifeLaunching||weapon==null||projectile==null||component.baseEquipable==null||component.baseEquipable.Pointer!=weapon.Pointer)return false;
        projectile.transform.SetPositionAndRotation(knifeOrigin,Quaternion.LookRotation(knifeDirection,Vector3.up));
        hitPoint=knifeOrigin+knifeDirection*Math.Max(50,component.maxRange);missTarget=false;
        // Aim the one launch only; the next native throw needs a new gesture.
        float now=Time.realtimeSinceStartup;
        knifeLaunchUntil=now+.05f;knifeLaunchedAt=now;WatchKnifeFlight(projectile);
        // 0.1.149: thrown by letting go - the hand is empty now (the next knife stays on the chest).
        if(knifeByGrip&&GripMode!=WeaponGripMode.Always){thrownAt=now;emptyAfterThrowAt=now+.3f;emptyAfterThrowSlot=Slot.Knife;}
        Bootstrap.Write("KNIFE THROW launched origin="+knifeOrigin.ToString("F2")+" direction="+knifeDirection.ToString("F2")+" native speed/spin/damage kept");
        return true;
    }
}
