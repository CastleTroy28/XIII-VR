using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// 0.1.222: a bottle (or another throwable thing) grabbed and thrown in one
// motion. The game takes a moment to put a thing taken by the grip into the
// hand (its draw animation), and the throw only started to watch the grip
// once it was there: a grip let go in a swing before that did not count, the
// thing stayed in the hand, and it took a second grip press to throw it. That
// swing now throws it as soon as the game has put it in the hand ("Weapon in
// hand: hold grip"; a grip let go without a swing still drops it).
internal sealed partial class WeaponHands
{
    private readonly GripLetGo[] letGo={new(),new()};
    private int lateKey=-1,lateSide=-1;private float lateUntil;
    // Every frame (SampleSwings): each grip's press, and how it was let go.
    private void NoteGrips(float now)
    {
        for(int s=0;s<2;s++)
        {
            var c=s==1?rig.RightControls:rig.LeftControls;if(!c.Valid)continue;
            if((c.Down&HandControls.Grip)!=0)letGo[s].Press(now);
            if((c.Up&HandControls.Grip)==0)continue;
            bool threw;Vector3 direction;float speed=0;string why;
            if(QualityOptions.ThrowArc?.Value==true){threw=AimThrow(s,out direction);why="aimed";}
            else threw=SwingThrow(s,out direction,out speed,out why);
            letGo[s].LetGo(now,threw,ToN(direction),speed,why);
        }
    }
    // The thing came to hand s (fresh) with its grip already open: the press
    // that took it was let go in a swing while the game was putting it there.
    private void NoteLateThrow(int s,int key,bool fresh,bool held,WeaponGripMode mode,float now)
    {
        if(held){if(lateKey==key)lateKey=-1;return;}
        if(!fresh||mode!=WeaponGripMode.Hold||!letGo[s].SwungSincePress(now))return;
        lateKey=key;lateSide=s;lateUntil=letGo[s].LetGoAt+GripLetGo.ThrowWindow;
        Bootstrap.Write("PROP THROW the grip that took "+(weapon!=null?weapon.identifier:"it")+" let go in a swing "+(now-letGo[s].LetGoAt).ToString("F2")+" s ago, before the game had it in the hand: thrown with that swing");
    }
    // The swing kept for it: thrown once the game can (false: not now).
    private bool LateThrow(int s,int key,Vector3 origin,float native,float now)
    {
        if(lateKey!=key||lateSide!=s||propThrow==null||visual==null)return false;
        if(now>lateUntil){lateKey=-1;letGo[s].Spend();Bootstrap.Write("PROP THROW the game never had it ready for the swing (it stays in the hand)");return false;}
        if(!propThrow.CanStart())return true;   // the game is not ready yet: wait (and nothing else)
        lateKey=-1;var swing=letGo[s];swing.Spend();throwHeldKey[s]=-1;
        if(throwAiming){throwAiming=false;throwRoot?.SetActive(false);}
        bool aimed=swing.Why=="aimed";var direction=ToU(swing.Direction);
        throwRotation=visual.FittedToWorld.rotation;
        GripCarry.Current?.Forget(weapon!);
        throwOrigin=origin+direction*.09f;
        throwLaunch=direction*(aimed?native:native*Math.Clamp(swing.Speed/3f,.8f,1.35f));
        LaunchProp((s==0?"left ":"")+(aimed?"aimed, grip let go while drawn":"grip let go while drawn handSpeed="+swing.Speed.ToString("F2")),s==1);
        return true;
    }
    // GripCarry (hold grip: let go, it is dropped): not while a swing let go
    // of before the thing was in the hand is still to throw it.
    internal bool AwaitsThrow(Equipable item)
    {
        try
        {
            if(item==null||weapon==null||weapon.Pointer!=item.Pointer||profile!="prop"||GripMode!=WeaponGripMode.Hold)return false;
            int key=weapon.GetInstanceID();float now=Time.realtimeSinceStartup;
            if(lateKey==key)return now<=lateUntil;
            int s=PrimaryLeft?0:1;
            // Watched by the throw this frame (it ticks first): it has dealt with that swing.
            if(throwHeldKey[s]==key&&throwSeenFrame[s]>=Time.frameCount-1||!letGo[s].SwungSincePress(now))return false;
            return (propThrow!=null&&propThrow.baseEquipable!=null&&propThrow.baseEquipable.Pointer==weapon.Pointer)||weapon.GetComponent(Il2CppType.Of<ThrowingComponent>())!=null;
        }
        catch(Exception){return false;}
    }
}
