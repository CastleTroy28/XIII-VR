using System;
namespace XiiiXR;
// 0.1.220: picking a lock takes the game's own time (its lockpick's
// lockpickTime, the hold the game asks for with a button), counted down on
// the game's lockpicking HUD from the moment the pick is turned in the lock.
// Pulled out of the lock (or put away) it stops; turned again it starts over.
internal static class LockpickTimerMath
{
    internal const float DefaultTime=3f,MinTime=.5f,MaxTime=20f;
    // Farther than this from the lock the pick is out of it (the turn is accepted within .32 m).
    internal const float PullOut=.36f;
    internal static float Time(float configured)=>float.IsFinite(configured)&&configured>0?Math.Clamp(configured,MinTime,MaxTime):DefaultTime;
    internal static float Remaining(float started,float time,float now)=>float.IsFinite(started)&&float.IsFinite(now)?Math.Max(0,started+time-now):time;
    internal static bool PulledOut(float distance)=>!float.IsFinite(distance)||distance>PullOut;
}
