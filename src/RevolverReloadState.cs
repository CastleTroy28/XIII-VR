using System;
namespace XiiiXR;
internal enum RevolverAction {None,Open,Empty,Take,Drop,Load,Close}
// Input-independent, one transaction per transition. No ammunition is created.
internal sealed class RevolverReloadState
{
    internal bool Open,Holding,Emptied;
    internal int HeldRounds;
    private float tipTime,closingUntil,quietUntil=float.NegativeInfinity;
    private bool swung;
    // 0.1.241: the cylinder stays open until the player shuts it: B again, or a
    // flick of the gun hand - measured in the room by the caller (not against
    // the head: looking down at the pouch shut it), never while the other hand
    // is at the pouch or holds rounds, nor for a moment after a step of the
    // reload (the arm still moving from it).
    internal const float QuietAfterStep=.6f;
    internal RevolverAction Step(float now,float dt,bool reload,bool takeDown,bool takeHeld,bool pouch,bool atCylinder,float muzzleUp,float rightVelocity)
    {
        if(!float.IsFinite(now)||!float.IsFinite(dt))return RevolverAction.None;
        if(!Open){if(reload)return RevolverAction.Open;return RevolverAction.None;}
        if(reload&&!Holding){swung=false;return RevolverAction.Close;}
        if(!Emptied)
        {
            tipTime=muzzleUp>.65f?tipTime+Math.Clamp(dt,0,.05f):0;
            if(tipTime>.18f){quietUntil=now+QuietAfterStep;return RevolverAction.Empty;}
        }
        if(Holding)
        {
            swung=false;
            if(!takeHeld){quietUntil=now+QuietAfterStep;return RevolverAction.Drop;}
            if(Emptied&&atCylinder){quietUntil=now+QuietAfterStep;return RevolverAction.Load;}
            return RevolverAction.None;
        }
        if(Emptied&&takeDown&&pouch){quietUntil=now+QuietAfterStep;return RevolverAction.Take;}
        if(pouch||now<quietUntil){swung=false;return RevolverAction.None;}
        if(rightVelocity>.65f){swung=true;closingUntil=now+.35f;}
        if(swung&&now<=closingUntil&&rightVelocity<.10f){swung=false;return RevolverAction.Close;}
        if(now>closingUntil)swung=false;
        return RevolverAction.None;
    }
    internal void Applied(RevolverAction action,int rounds=0)
    {
        switch(action){case RevolverAction.Open:Open=true;Emptied=false;tipTime=0;swung=false;quietUntil=float.NegativeInfinity;break;
        case RevolverAction.Empty:Emptied=true;tipTime=0;break;
        case RevolverAction.Take:Holding=true;HeldRounds=Math.Max(0,rounds);break;
        case RevolverAction.Drop:Holding=false;HeldRounds=0;break;
        case RevolverAction.Load:Holding=false;HeldRounds=0;Emptied=false;tipTime=-.6f;break;
        case RevolverAction.Close:Open=false;swung=false;tipTime=0;break;}
    }
}
