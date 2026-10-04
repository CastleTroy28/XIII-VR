using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.216: a hard punch of either fist in an enemy's back knocks him out at
// once (as the game's own takedown from behind does); a touch, a light blow,
// a blow from the side or the front, or one with a weapon or a thing in the
// hand, is an ordinary punch.
internal static class KnockoutMath
{
    // Behind him: he faces away from the player's head within 60 degrees of
    // straight away, the head within reach.
    internal const float MinAway=.5f,MaxDistance=2f;
    // A swing with force (the hand's top speed of the stroke, m/s); a touch
    // or a jab is slower.
    internal const float HardSpeed=3f;
    internal static bool FromBehind(Vector3 npcForward,Vector3 npcPosition,Vector3 head)
    {
        var f=new Vector2(npcForward.X,npcForward.Z);var to=new Vector2(npcPosition.X-head.X,npcPosition.Z-head.Z);
        float distance=to.Length();
        if(!float.IsFinite(distance)||distance>MaxDistance||distance<1e-3f||!(f.LengthSquared()>1e-6f))return false;
        return Vector2.Dot(Vector2.Normalize(f),to/distance)>=MinAway;
    }
    internal static bool Hard(float speed)=>float.IsFinite(speed)&&speed>=HardSpeed;
    internal static bool Knocks(bool fist,bool behind,bool hard,bool conscious,bool held)=>fist&&behind&&hard&&conscious&&!held;
}
