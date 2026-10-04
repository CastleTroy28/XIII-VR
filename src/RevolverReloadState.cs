using System;
namespace XiiiXR;
internal enum RevolverAction {None,Open,Empty,Take,Drop,Load,Close}
// Input-independent, one transaction per transition. No ammunition is created.
internal sealed class RevolverReloadState
{
    internal bool Open,Holding,Emptied;
    internal int HeldRounds;
    private float tipTime,closingUntil;
    private bool swung;
    internal RevolverAction Step(float now,float dt,bool reload,bool takeDown,bool takeHeld,bool pouch,bool atCylinder,float muzzleUp,float rightVelocity)
    {
        if(!float.IsFinite(now)||!float.IsFinite(dt))return RevolverAction.None;
        if(!Open){if(reload)return RevolverAction.Open;return RevolverAction.None;}
        if(!Emptied)
        {
            tipTime=muzzleUp>.65f?tipTime+Math.Clamp(dt,0,.05f):0;
            if(tipTime>.18f)return RevolverAction.Empty;
        }
        if(Holding){if(!takeHeld)return RevolverAction.Drop;if(Emptied&&atCylinder)return RevolverAction.Load;return RevolverAction.None;}
        if(Emptied&&takeDown&&pouch)return RevolverAction.Take;
        if(rightVelocity>.65f){swung=true;closingUntil=now+.35f;}
        if(swung&&now<=closingUntil&&rightVelocity<.10f){swung=false;return RevolverAction.Close;}
        if(now>closingUntil)swung=false;
        return RevolverAction.None;
    }
    internal void Applied(RevolverAction action,int rounds=0)
    {
        switch(action){case RevolverAction.Open:Open=true;Emptied=false;tipTime=0;swung=false;break;
        case RevolverAction.Empty:Emptied=true;tipTime=0;break;
        case RevolverAction.Take:Holding=true;HeldRounds=Math.Max(0,rounds);break;
        case RevolverAction.Drop:Holding=false;HeldRounds=0;break;
        case RevolverAction.Load:Holding=false;HeldRounds=0;Emptied=false;tipTime=-.6f;break;
        case RevolverAction.Close:Open=false;swung=false;tipTime=0;break;}
    }
}
