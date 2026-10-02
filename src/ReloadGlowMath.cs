namespace XiiiXR;
// 0.1.120: which part of the gun glows during a hand reload (what to take next).
//  magazine guns: empty -> magazine; out -> belt pouch; in -> bolt
//  shotgun / crossbow: empty -> pouch; pump when needed
//  M60: empty -> cover; open -> ammunition box; box off -> pouch; new box
//       on -> cover (close); closed -> charging handle
// 0.1.123: nothing while ammunition is in the hand (where it goes is plain;
// the ghost of the magazine at its place is gone).
internal enum GlowTarget{None,Magazine,Pouch,Bolt,Cover}
internal static class ReloadGlowMath
{
    internal static GlowTarget Target(bool holding,int heldRounds,bool installed,bool needsRack,bool coverOpen,int rounds,bool lidded,bool single)
    {
        if(holding)return GlowTarget.None;
        if(lidded&&coverOpen)return installed?(needsRack?GlowTarget.Cover:GlowTarget.Magazine):GlowTarget.Pouch;
        if(!installed)return GlowTarget.Pouch;
        if(rounds<=0)return lidded?GlowTarget.Cover:single?GlowTarget.Pouch:GlowTarget.Magazine;
        if(needsRack)return GlowTarget.Bolt;
        return GlowTarget.None;
    }
}
