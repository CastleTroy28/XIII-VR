using System;
namespace XiiiXR;
// 0.1.109: the lockpick is held like a small screwdriver in the player's
// reference photo: the hand closed in a fist, the handle lying along the
// hand on the thumb side of the curled index finger, the thumb pressing on
// top of it near the front of the handle, the rear of the handle in the palm
// and the pick pointing forward (where the controller points).
internal static class ScrewdriverGrip
{
    internal const string Profile="screwdriver";
    // The thumb/index hold the handle this far behind its front end.
    internal const float FrontInset=.012f;
    // Point on the handle (along its axis) under the thumb, given the handle's
    // extent: near the front, never behind its middle.
    internal static float HoldPoint(float start,float end)
    {
        if(!float.IsFinite(start)||!float.IsFinite(end)||end<=start)return float.IsFinite(end)?end:0;
        return Math.Clamp(end-FrontInset,(start+end)*.5f,end);
    }
}
