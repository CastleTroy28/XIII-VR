using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.195: the gadgets held in a hand from the weapon wheel: the grappling
// hook (fired at grapple points) and the zipline hook (rides a cable).
internal enum HandToolKind{None,Grapple,Zipline}
internal static class HandTools
{
    // The zipline hook's inventory slot is "Zipline" (16); the grappling hook
    // is "Gadget" (33) and is told by its name (0.1.84 log).
    internal static HandToolKind Kind(int slot,string? label)
    {
        var n=(label??"").ToLowerInvariant();
        if(slot==16||n.Contains("zipline"))return HandToolKind.Zipline;
        if(n.Contains("grappl"))return HandToolKind.Grapple;
        return HandToolKind.None;
    }
    // The other hand close enough to the holding hand to take the tool over.
    internal const float PassReach=.17f;
    internal static bool Reaches(Vector3 hand,Vector3 holder)=>float.IsFinite(hand.X+hand.Y+hand.Z+holder.X+holder.Y+holder.Z)&&Vector3.Distance(hand,holder)<=PassReach;
    // 0.1.197: a thing's placement in one hand's root (x across the hand) as
    // the other hand's mirror image: the position's x negated, the turn
    // mirrored, and the thing itself mirrored (negative x scale), as items
    // held in the left hand are the right hand's mirrored.
    internal static (Vector3 position,Quaternion rotation,Vector3 scale) MirrorAcrossHand(Vector3 position,Quaternion rotation,Vector3 scale)
        =>(new Vector3(-position.X,position.Y,position.Z),new Quaternion(rotation.X,-rotation.Y,-rotation.Z,rotation.W),new Vector3(-scale.X,scale.Y,scale.Z));
}
