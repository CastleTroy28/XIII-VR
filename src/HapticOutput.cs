using System;
using UnityEngine.XR;
namespace XiiiXR;
// Legacy compatibility only. Valve XR provider currently maps this call to
// TriggerHapticPulse, so it is not independent confirmation of vibration.
// SendHapticImpulse's duration is not implemented by every provider, so the
// caller still owns the short, nonblocking pulse envelope.
internal sealed class HapticOutput
{
    private readonly bool[] unavailable=new bool[2],reported=new bool[2];
    internal bool Send(bool right,ushort strength)
    {
        int i=right?1:0;if(unavailable[i])return false;
        try
        {
            var device=InputDevices.GetDeviceAtXRNode(right?XRNode.RightHand:XRNode.LeftHand);
            bool accepted=device.isValid&&device.SendHapticImpulse(0,Math.Clamp(strength/3999f,0,1),.012f);
            if(!reported[i]){reported[i]=true;Bootstrap.Write("HAPTIC XR side="+(right?"R":"L")+" accepted="+accepted+" device="+device.name+"; API acceptance is not hardware confirmation");}
            return accepted;
        }
        catch(Exception ex){unavailable[i]=true;Bootstrap.Warn("HAPTIC XR side="+i+" fallback to legacy: "+ex.Message);return false;}
    }
    internal void Reset(){Array.Clear(unavailable,0,2);Array.Clear(reported,0,2);}
}
