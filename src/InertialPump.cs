using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.136: a pump shotgun held by its fore-end only, pumped with one hand:
// the pump stays in the hand and the rest of the gun (receiver, stock) is
// free to slide along the gun's axis. Gravity and the hand's own
// acceleration (the other way: the gun lags behind a jerk) push it; friction
// holds it until the push is strong enough. A jerk of the hand towards the
// stock opens it (the gun's weight and inertia pull it forward, off the
// pump), a jerk back closes it.
// 0.1.138: a gun held level (let go at the handle from a two-hand hold) is
// pumped by the same jerk down and back up as a gun held muzzle up: the
// hand's acceleration is taken along the gun's axis tilted up towards the
// vertical (not at all for a gun already pointing up). Gravity still acts
// along the real axis only.
internal sealed class InertialPump
{
    // StaticFriction above gravity: tilting the gun alone never moves it.
    internal const float StaticFriction=10.5f,KineticFriction=2f,HandGain=1.6f,MaxAcceleration=150f;
    // 0.1.138: the gun's weight across the pump presses it on the pump: more
    // friction the more level the gun is (a level gun no longer slides on
    // after a small bump).
    internal const float NormalStatic=.3f,NormalKinetic=.5f;
    // How far the pump is back (0: closed .. full).
    internal float Travel{get;private set;}
    private float speed,lastTime=-1;private Vector3 lastPosition,lastVelocity,acceleration;private int samples;
    internal void Reset(float travel){Travel=Math.Max(0,travel);speed=0;samples=0;lastTime=-1;acceleration=Vector3.Zero;}
    // hand: where the hand holding the fore-end is (world); axis: the gun's
    // forward (stock to muzzle, world); gravity: world, m/s^2.
    internal float Step(Vector3 hand,Vector3 axis,float now,float full,Vector3 gravity)
    {
        float dt=lastTime<0?0:now-lastTime;lastTime=now;
        if(!(full>0)||axis.LengthSquared()<1e-8f||!float.IsFinite(hand.X+hand.Y+hand.Z))return Travel;
        axis=Vector3.Normalize(axis);
        if(samples==0||!(dt>1e-4f)||dt>.1f){lastPosition=hand;lastVelocity=Vector3.Zero;acceleration=Vector3.Zero;samples=1;return Travel;}
        var velocity=(hand-lastPosition)/dt;
        if(samples>=2)
        {
            var a=(velocity-lastVelocity)/dt;
            if(a.Length()>MaxAcceleration)a=Vector3.Normalize(a)*MaxAcceleration;
            acceleration=Vector3.Lerp(acceleration,a,.6f);
        }
        lastPosition=hand;lastVelocity=velocity;samples++;
        if(samples<3)return Travel;
        // Along the axis, relative to the pump in the hand.
        float along=Vector3.Dot(gravity,axis),across=(gravity-axis*along).Length();
        float drive=along-HandGain*Vector3.Dot(acceleration,JerkAxis(axis,gravity));
        float holds=StaticFriction+NormalStatic*across,slides=KineticFriction+NormalKinetic*across;
        if(speed==0)
        {
            if(Math.Abs(drive)<=holds)return Travel;
            if(drive>0&&Travel>=full||drive<0&&Travel<=0)return Travel;
        }
        float direction=speed!=0?Math.Sign(speed):Math.Sign(drive);
        float next=speed+(drive-slides*direction)*dt;
        if(speed!=0&&Math.Sign(next)!=Math.Sign(speed)&&Math.Abs(drive)<=holds)next=0;
        speed=next;Travel+=speed*dt;
        if(Travel<=0){Travel=0;if(speed<0)speed=0;}
        if(Travel>=full){Travel=full;if(speed>0)speed=0;}
        return Travel;
    }
    internal const float Upright=.9f;
    // The direction along which the hand's jerk moves the gun: the axis plus
    // up times (Upright - its own upness): a level gun tilted about 42 deg,
    // a gun pointing up (dot with up >= Upright) as it is.
    internal static Vector3 JerkAxis(Vector3 axis,Vector3 gravity)
    {
        if(gravity.LengthSquared()<1e-8f)return axis;
        var up=-Vector3.Normalize(gravity);float lift=Upright-Vector3.Dot(axis,up);
        if(!(lift>0))return axis;
        var d=axis+up*lift;float l=d.Length();
        return l>1e-5f?d/l:up;
    }
}
