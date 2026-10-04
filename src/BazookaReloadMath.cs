using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.215: the bazooka reloaded by hand (WeaponHands.Bazooka): a rocket taken
// from the belt pouch with the grip (0.1.215: the trigger) of the hand not holding the handle,
// its tail put into the front of the tube.
internal static class BazookaReloadMath
{
    // How near the tail must come to the tube's mouth (m), and how parallel
    // the rocket must lie to the tube (cosine).
    internal const float MouthReach=.13f,Parallel=.6f;
    // A rocket can be taken: the tube is empty, rockets are left, the hand is
    // free and at the pouch, and its grip (0.1.221; was its trigger) went down now.
    internal static bool Take(bool empty,int reserve,bool free,bool atPouch,bool pressed)=>empty&&reserve!=0&&free&&atPouch&&pressed;
    // The rocket goes in: its tail at the mouth, pointing the way the tube does.
    internal static bool Insert(Vector3 tail,Vector3 rocketForward,Vector3 mouth,Vector3 tubeForward)
    {
        if(!Finite(tail)||!Finite(mouth)||rocketForward.LengthSquared()<1e-8f||tubeForward.LengthSquared()<1e-8f)return false;
        return Vector3.Distance(tail,mouth)<=MouthReach&&Vector3.Dot(Vector3.Normalize(rocketForward),Vector3.Normalize(tubeForward))>=Parallel;
    }
    // The aim dot's radius: about a third of a degree seen from the head.
    internal static float DotRadius(float distance)=>float.IsFinite(distance)?Math.Clamp(distance*.006f,.025f,.4f):.025f;
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
