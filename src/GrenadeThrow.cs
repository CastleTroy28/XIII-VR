using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.118: hand grenade: bring the left hand to the grenade
// and press the left trigger to pull the pin; hold the right trigger (the
// lever), swing and let go — the grenade leaves the hand with the hand's own
// velocity ("by inertia"), in whatever direction the hand was moving.
// Positions are in tracking space, so walking or turning with the stick does
// not add to the throw. Without the pin nothing is thrown; a tap shorter than
// MinHold is ignored.
internal sealed class GrenadeThrow
{
    internal const float PinReach=.16f,MinHold=.10f,PeakWindow=.12f,VelocityWindow=.04f,Boost=1.35f,MaxSpeed=24f;
    internal readonly struct Result
    {
        internal readonly bool Throw;internal readonly Vector3 Velocity;internal readonly float HandSpeed;
        internal Result(Vector3 velocity,float hand){Throw=true;Velocity=velocity;HandSpeed=hand;}
    }
    private readonly float[] times=new float[48];private readonly Vector3[] points=new Vector3[48];
    private int count,head;private bool armed,wasHeld=true;private float armedAt;
    internal bool PinPulled{get;private set;}
    internal bool Armed=>armed;
    internal void Reset(){count=0;head=0;armed=false;wasHeld=true;PinPulled=false;}
    // Left trigger pressed with the left fingers at the grenade.
    internal bool PullPin(bool leftTriggerDown,Vector3 leftFingers,Vector3 grenade)
    {
        if(PinPulled||!leftTriggerDown||!Finite(leftFingers)||!Finite(grenade))return false;
        if(Vector3.Distance(leftFingers,grenade)>PinReach)return false;
        PinPulled=true;return true;
    }
    internal Result Sample(float now,bool rightHeld,Vector3 position)
    {
        if(!float.IsFinite(now)||!Finite(position)){count=0;head=0;armed=false;return default;}
        if(count>0&&now<=times[(head+times.Length-1)%times.Length]){wasHeld=rightHeld;return default;}
        times[head]=now;points[head]=position;head=(head+1)%times.Length;count=Math.Min(count+1,times.Length);
        // The lever is held once the pin is out and the right trigger is down
        // (pressed before or after pulling the pin).
        if(PinPulled&&rightHeld&&!armed){armed=true;armedAt=now;}
        wasHeld=rightHeld;
        if(rightHeld||!armed)return default;
        armed=false;
        if(now-armedAt<MinHold)return default;
        PinPulled=false;
        if(!Fastest(now,out var velocity,out float bestSpeed))return new Result(Vector3.Zero,0);   // no history: it simply drops
        return new Result(velocity,bestSpeed);
    }
    // 0.1.214: the throw letting go now would make (the landing mark), the
    // gesture left as it is.
    internal bool Peek(float now,out Vector3 velocity,out float handSpeed)=>Fastest(now,out velocity,out handSpeed);
    // Fastest hand velocity over ~40 ms windows ending in the last 120 ms:
    // the trigger tends to open just after the fastest point of the swing.
    private bool Fastest(float now,out Vector3 velocity,out float handSpeed)
    {
        Vector3 best=Vector3.Zero;float bestSpeed=-1;
        for(int i=0;i<count;i++)
        {
            int a=(head+times.Length-1-i)%times.Length;if(now-times[a]>PeakWindow)break;
            for(int j=i+1;j<count;j++)
            {
                int b=(head+times.Length-1-j)%times.Length;float dt=times[a]-times[b];
                if(dt<VelocityWindow)continue;
                if(dt>VelocityWindow*3)break;
                var v=(points[a]-points[b])/dt;float s=v.Length();
                if(s>bestSpeed){bestSpeed=s;best=v;}
                break;
            }
        }
        handSpeed=Math.Max(0,bestSpeed);velocity=Vector3.Zero;
        if(bestSpeed<0)return false;
        velocity=best*Boost;float speed=velocity.Length();
        if(speed>MaxSpeed)velocity*=MaxSpeed/speed;
        return true;
    }
    // 0.1.138: as briskly as a thrown prop. The hand's own speed against the
    // game's double gravity (20 m/s^2) dropped the grenade a few metres away,
    // "sluggish". A real swing (hand at LivelyFull m/s or more) leaves at the
    // game's throwing speed, scaled like a prop throw (0.8..1.35 by the
    // swing); a gentle toss (hand under LivelyFrom) keeps the hand's speed;
    // in between, blended. The direction is always the hand's.
    internal const float LivelyFrom=1f,LivelyFull=2.5f;
    internal static Vector3 Lively(Vector3 velocity,float handSpeed,float native)
    {
        float speed=velocity.Length();
        if(!(speed>1e-4f)||!float.IsFinite(speed)||!(native>0)||!float.IsFinite(native)||!float.IsFinite(handSpeed))return velocity;
        float w=Math.Clamp((handSpeed-LivelyFrom)/(LivelyFull-LivelyFrom),0,1);
        float target=native*Math.Clamp(handSpeed/3f,.8f,1.35f);
        float s=speed+(Math.Max(speed,target)-speed)*w;
        s=Math.Min(s,MaxSpeed);
        return velocity/speed*s;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    // 0.1.159: a
    // swing's fastest moment often runs a little sideways (the arm's arc). Its
    // heading is turned part of the way (LookBlend) toward where the head
    // looks, when within LookWithin degrees of it; its rise or fall and its
    // speed stay the hand's.
    internal const float LookBlend=.35f,LookWithin=75f;
    internal static Vector3 TowardLook(Vector3 velocity,Vector3 look)
    {
        var flat=new Vector2(velocity.X,velocity.Z);var aim=new Vector2(look.X,look.Z);
        float speed=flat.Length(),aimLength=aim.Length();
        if(!(speed>1e-4f)||!(aimLength>1e-4f)||!Finite(velocity)||!Finite(look))return velocity;
        float from=MathF.Atan2(flat.X,flat.Y),to=MathF.Atan2(aim.X,aim.Y);
        float d=to-from;while(d>MathF.PI)d-=2*MathF.PI;while(d<-MathF.PI)d+=2*MathF.PI;
        if(MathF.Abs(d)>LookWithin*MathF.PI/180f)return velocity;
        float heading=from+d*LookBlend;
        return new Vector3(MathF.Sin(heading)*speed,velocity.Y,MathF.Cos(heading)*speed);
    }
}
