using System;
using System.Numerics;
namespace XiiiXR;
// Throwing-knife gesture, in tracking space (locomotion and stick turns do not
// add hand speed). Hold the trigger, draw the hand back, snap it forward and
// let go of the trigger during the snap: the knife leaves along the hand's
// peak forward velocity, gently blended with where the controller points.
// A slow release, or letting go while still pulling back, throws nothing.
internal sealed class KnifeThrowGesture
{
    internal const float MinSpeed=1.3f,MinHold=.08f,PeakWindow=.14f,VelocityWindow=.035f,AimBlend=.3f;
    internal readonly struct Result
    {
        internal readonly bool Throw,Cancelled;internal readonly Vector3 Direction;internal readonly float Speed;internal readonly string Reason;
        internal Result(bool thrown,bool cancelled,Vector3 direction,float speed,string reason){Throw=thrown;Cancelled=cancelled;Direction=direction;Speed=speed;Reason=reason;}
    }
    private readonly float[] times=new float[48];private readonly Vector3[] points=new Vector3[48];
    // Starts as "held": a trigger already down when the knife appears must be
    // released before it can arm.
    private int count,head;private bool armed,wasHeld=true;private float armedAt;
    internal bool Armed=>armed;
    internal void Reset(){count=0;head=0;armed=false;wasHeld=true;}
    internal Result Sample(float now,bool held,Vector3 position,Vector3 aim)
    {
        if(!float.IsFinite(now)||!Finite(position)||!Finite(aim)||aim.LengthSquared()<1e-6f){Reset();return default;}
        if(count>0&&now<=times[(head+times.Length-1)%times.Length]){wasHeld=held;return default;}
        times[head]=now;points[head]=position;head=(head+1)%times.Length;count=Math.Min(count+1,times.Length);
        // Only a fresh press arms: a trigger still held from before (menu,
        // weapon switch, startup) must be released first.
        if(held&&!wasHeld){armed=true;armedAt=now;}
        wasHeld=held;
        if(held||!armed)return default;
        armed=false;
        aim=Vector3.Normalize(aim);
        if(now-armedAt<MinHold)return new Result(false,true,Vector3.Zero,0,"tap");
        // Peak forward velocity over the last moments before the release:
        // players tend to open the trigger slightly after the fastest point.
        Vector3 best=Vector3.Zero;float bestForward=float.NegativeInfinity;
        for(int i=0;i<count;i++)
        {
            int a=(head+times.Length-1-i)%times.Length;if(now-times[a]>PeakWindow)break;
            // Velocity over ~35ms ending at sample a.
            for(int j=i+1;j<count;j++)
            {
                int b=(head+times.Length-1-j)%times.Length;float dt=times[a]-times[b];
                if(dt<VelocityWindow)continue;
                if(dt>VelocityWindow*3)break;
                var v=(points[a]-points[b])/dt;float forward=Vector3.Dot(v,aim);
                if(forward>bestForward){bestForward=forward;best=v;}
                break;
            }
        }
        float speed=best.Length();
        if(!float.IsFinite(bestForward)||speed<MinSpeed)return new Result(false,true,Vector3.Zero,speed,"slow");
        var direction=Vector3.Normalize(best);
        // Released while pulling back or swinging sideways: not a throw.
        if(Vector3.Dot(direction,aim)<.25f)return new Result(false,true,Vector3.Zero,speed,"not forward");
        direction=Vector3.Normalize(direction*(1-AimBlend)+aim*AimBlend);
        return new Result(true,false,direction,speed,"");
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
