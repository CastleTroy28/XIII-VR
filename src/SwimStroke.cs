using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.108: arm strokes in water. Spreading the hands apart, or pushing both
// away from the head, faster than a slow reach builds a forward swim that
// fades out ~1.5 s after the last stroke. Physical tracking space only:
// swimming/turning in the game cannot create a stroke.
internal sealed class SwimStroke
{
    internal const float Threshold=.6f;   // m/s
    private const float Gain=2.5f,Decay=.8f,MaxBoost=1.2f;
    private Vector3 lastLeft,lastRight,lastHead;private bool has;private float boost,nextPulse;
    internal float Power {get;private set;}
    internal float Forward=>Math.Min(1,boost);
    internal bool Active=>Power>Threshold;
    internal void Reset(){has=false;boost=0;Power=0;}
    // Returns the vibration strength to send now (0: none).
    internal float Sample(Vector3 left,Vector3 right,Vector3 head,float dt,float now)
    {
        Power=0;
        if(!Finite(left)||!Finite(right)||!Finite(head)||!(dt>0)||dt>.1f||!float.IsFinite(now)){has=false;return 0;}
        if(!has){lastLeft=left;lastRight=right;lastHead=head;has=true;boost=Math.Max(0,boost-Decay*dt);return 0;}
        float spread=(Vector3.Distance(left,right)-Vector3.Distance(lastLeft,lastRight))/dt;
        float awayLeft=(Vector3.Distance(left,head)-Vector3.Distance(lastLeft,lastHead))/dt;
        float awayRight=(Vector3.Distance(right,head)-Vector3.Distance(lastRight,lastHead))/dt;
        lastLeft=left;lastRight=right;lastHead=head;
        Power=Math.Max(spread,Math.Min(awayLeft,awayRight));
        if(!float.IsFinite(Power))Power=0;
        if(Power>Threshold)boost=Math.Min(MaxBoost,boost+(Power-Threshold)*Gain*dt);
        else boost=Math.Max(0,boost-Decay*dt);
        if(Power>Threshold&&now>=nextPulse){nextPulse=now+.07f;return Math.Clamp((Power-Threshold)/1.2f,.25f,.8f);}
        return 0;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
// 0.1.110: under water, swimming forward (stick or arm strokes) goes where
// the head looks: forward input splits into horizontal (cos pitch) and
// vertical (sin pitch) at the native swim speed.
internal static class SwimGaze
{
    internal const float Deadband=5;
    internal static float Horizontal(float forward,float pitchDegrees)
        =>forward>0&&float.IsFinite(pitchDegrees)?forward*MathF.Cos(Math.Clamp(pitchDegrees,-90,90)*MathF.PI/180):forward;
    // NaN: leave the native vertical speed alone.
    internal static float Vertical(float forward,float pitchDegrees,float swimSpeed)
    {
        if(!(forward>.05f)||!float.IsFinite(pitchDegrees)||Math.Abs(pitchDegrees)<Deadband||!(swimSpeed>0))return float.NaN;
        return Math.Clamp(forward,0,1)*swimSpeed*MathF.Sin(Math.Clamp(pitchDegrees,-90,90)*MathF.PI/180);
    }
}
// 0.1.108/0.1.110: swimming up/down from the stick (right stick up/down),
// and at the surface from the head: moving forward while looking down dives.
// Under water the swim follows the gaze directly (SwimGaze).
internal sealed class SwimVertical
{
    internal const float Enter=25,Leave=20,Forward=.3f;
    private bool lookUp,lookDown;
    internal ActionEdge Up,Down;
    internal void Reset(){lookUp=lookDown=false;Up=new ActionEdge(false,Up.Held);Down=new ActionEdge(false,Down.Held);}
    internal void Sample(bool stickUp,bool stickDown,float forward,float pitchDegrees,bool submerged)
    {
        if(!float.IsFinite(pitchDegrees))pitchDegrees=0;if(!float.IsFinite(forward))forward=0;
        lookUp=lookUp?pitchDegrees>Leave:pitchDegrees>Enter;
        lookDown=lookDown?pitchDegrees< -Leave:pitchDegrees< -Enter;
        bool moving=forward>Forward;
        bool up=stickUp;
        bool down=stickDown||!submerged&&moving&&lookDown&&!stickUp;
        Up=new ActionEdge(up,Up.Held);Down=new ActionEdge(down,Down.Held);
    }
}
