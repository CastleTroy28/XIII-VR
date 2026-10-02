using System;
namespace XiiiXR;
// 0.1.84: on the grappling rope the left stick climbs, the right stick
// swings, L3 or the right B lets go. 0.1.197: the hook fired from
// the right hand mirrors it: the right stick climbs, the left stick swings,
// R3 lets go (the left Y stays the reload of the gun in the left hand).
internal static class RopeRelease
{
    internal static bool Pressed(bool rightHanded,ActionEdge l3,HandControls left,HandControls right,int phase)
    {
        ulong Bits(HandControls c)=>!c.Valid?0:phase==0?c.Held:phase==1?c.Down:c.Up;
        if(rightHanded)return (Bits(right)&HandControls.Stick)!=0;
        bool click=phase==0?l3.Held:phase==1?l3.Down:l3.Up;
        return click||(Bits(right)&HandControls.B)!=0;
    }
}
