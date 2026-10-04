using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.149. The hand's path in tracking space (walking and turning
// with the stick add nothing): at the moment the grip opens, the hand's
// fastest forward velocity of the last moments is the throw - fast enough and
// forward, else the hand simply let go.
internal sealed class SwingRelease
{
    internal const float MinSpeed=1.3f,PeakWindow=.14f,VelocityWindow=.035f,AimBlend=.3f,AimDot=.25f,LookDot=.3f;
    private readonly float[] times=new float[48];private readonly Vector3[] points=new Vector3[48];
    private int count,head;
    internal void Reset(){count=0;head=0;}
    internal void Sample(float now,Vector3 position)
    {
        if(!float.IsFinite(now)||!Finite(position)){Reset();return;}
        if(count>0&&now<=times[(head+times.Length-1)%times.Length])return;
        times[head]=now;points[head]=position;head=(head+1)%times.Length;count=Math.Min(count+1,times.Length);
    }
    // The hand's velocity over the last few samples (m/s; zero without history).
    internal Vector3 Velocity(float now)
    {
        if(count<2)return Vector3.Zero;
        int a=(head+times.Length-1)%times.Length;
        for(int j=1;j<count;j++)
        {
            int b=(head+times.Length-1-j)%times.Length;float dt=times[a]-times[b];
            if(dt>=VelocityWindow)return (points[a]-points[b])/dt;
        }
        return Vector3.Zero;
    }
    // The grip opened now: a throw along the hand's fastest forward velocity
    // of the last moments (a little toward where the controller points), if
    // fast enough and forward - forward of the controller, or of the look.
    internal KnifeThrowGesture.Result Release(float now,Vector3 aim,Vector3 look)
    {
        if(!float.IsFinite(now)||!Finite(aim)||aim.LengthSquared()<1e-6f)return new KnifeThrowGesture.Result(false,true,Vector3.Zero,0,"no aim");
        aim=Vector3.Normalize(aim);
        bool lookValid=Finite(look)&&look.LengthSquared()>1e-6f;if(lookValid)look=Vector3.Normalize(look);
        Vector3 best=Vector3.Zero;float bestScore=float.NegativeInfinity;
        for(int i=0;i<count;i++)
        {
            int a=(head+times.Length-1-i)%times.Length;if(now-times[a]>PeakWindow)break;
            for(int j=i+1;j<count;j++)
            {
                int b=(head+times.Length-1-j)%times.Length;float dt=times[a]-times[b];
                if(dt<VelocityWindow)continue;
                if(dt>VelocityWindow*3)break;
                var v=(points[a]-points[b])/dt;
                float score=Math.Max(Vector3.Dot(v,aim),lookValid?Vector3.Dot(v,look):float.NegativeInfinity);
                if(score>bestScore){bestScore=score;best=v;}
                break;
            }
        }
        float speed=best.Length();
        if(!float.IsFinite(bestScore)||speed<MinSpeed)return new KnifeThrowGesture.Result(false,true,Vector3.Zero,speed,"slow");
        var direction=Vector3.Normalize(best);
        if(Vector3.Dot(direction,aim)<AimDot&&!(lookValid&&Vector3.Dot(direction,look)>=LookDot))return new KnifeThrowGesture.Result(false,true,Vector3.Zero,speed,"not forward");
        direction=Vector3.Normalize(direction*(1-AimBlend)+aim*AimBlend);
        return new KnifeThrowGesture.Result(true,false,direction,speed,"");
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
internal enum ThrowGripStep{None,Throw,Release}
// Holding a throwable thing by the grip ("Weapon in hand" setting):
//  - hold the grip: held while the grip is held; letting go throws it (a
//    swing) or lets it go (put back / dropped);
//  - toggle: the press that took it keeps it; the next press holds it the
//    same way until its grip opens (a swing throws, else it is let go);
//  - always in the hand: a press, a swing and letting go throws; letting go
//    without a swing keeps it.
internal sealed class ThrowGrip
{
    private bool armed;
    internal bool Armed=>armed;
    internal void Took(bool byGrip,WeaponGripMode mode){armed=byGrip&&mode==WeaponGripMode.Hold;}
    internal void Reset(){armed=false;}
    // swing: the release was a throw (asked only when the grip opens).
    internal ThrowGripStep Step(WeaponGripMode mode,bool held,bool down,Func<bool> swing)
    {
        if(held)
        {
            if(mode==WeaponGripMode.Hold||down)armed=true;
            return ThrowGripStep.None;
        }
        if(!armed)return ThrowGripStep.None;
        armed=false;
        if(swing())return ThrowGripStep.Throw;
        return mode==WeaponGripMode.Always?ThrowGripStep.None:ThrowGripStep.Release;
    }
}
// 0.1.222: one hand's last grip press and how it was let go (a throw: its
// direction and speed). A thing taken by a grip press comes into the hand
// only once the game has drawn it (a moment); a grip let go in a swing
// before that still throws it (SwungSincePress), as soon as it is there.
internal sealed class GripLetGo
{
    internal const float PressWindow=3f,ThrowWindow=1.5f;
    internal float PressedAt{get;private set;}=-10;
    internal float LetGoAt{get;private set;}=-10;
    internal bool Threw{get;private set;}
    internal Vector3 Direction{get;private set;}
    internal float Speed{get;private set;}
    internal string Why{get;private set;}="";
    internal void Press(float now){if(float.IsFinite(now))PressedAt=now;}
    internal void LetGo(float now,bool threw,Vector3 direction,float speed,string why)
    {
        if(!float.IsFinite(now))return;
        LetGoAt=now;Threw=threw&&float.IsFinite(direction.X)&&float.IsFinite(direction.Y)&&float.IsFinite(direction.Z)&&direction.LengthSquared()>1e-6f;
        Direction=Threw?Vector3.Normalize(direction):Vector3.Zero;Speed=float.IsFinite(speed)?Math.Max(0,speed):0;Why=why??"";
    }
    // The last press was let go in a throw, both lately (the press within
    // PressWindow, the let-go within ThrowWindow).
    internal bool SwungSincePress(float now)=>Threw&&float.IsFinite(now)&&LetGoAt>=PressedAt&&LetGoAt<=now&&now-PressedAt<=PressWindow&&now-LetGoAt<=ThrowWindow;
    internal void Spend(){Threw=false;}
}
