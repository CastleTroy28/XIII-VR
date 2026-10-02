using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.146: when two held guns come to touch
// (0: the left copy, 1: the right copy, 2: the game's weapon), how fast their
// touching points met. Each gun's last two resolved poses give the speed of
// the very point where they touch (a swing turns a long gun: its muzzle moves
// faster than its handle).
internal sealed class GunKnock
{
    // Touching within Contact (m); apart again beyond Release; knocks slower
    // than MinSpeed are silent; at most one knock every MinGap seconds
    // (0.1.147: .25 s, apart again beyond 2 cm: a steady touch ticked).
    internal const float Contact=.004f,Release=.02f,MinSpeed=.3f,FullSpeed=2.5f,MinGap=.25f;
    private readonly ContactPose[] current=new ContactPose[3],previous=new ContactPose[3];
    private readonly float[] currentAt={-1,-1,-1},previousAt={-1,-1,-1};
    private float lastKnock=-10;
    internal bool Touching{get;private set;}
    internal int Reports{get;set;}
    internal void Moved(int i,ContactPose pose,float now)
    {
        if(i<0||i>2||!float.IsFinite(now))return;
        if(currentAt[i]>=0&&now>currentAt[i]){previous[i]=current[i];previousAt[i]=currentAt[i];}
        else if(currentAt[i]<0){previousAt[i]=-1;}
        current[i]=pose;currentAt[i]=now;
    }
    internal void Forget(int i){if(i<0||i>2)return;currentAt[i]=previousAt[i]=-1;}
    internal void Apart(){Touching=false;}
    // The speed of a gun's point (world) from its last two poses (0 unknown).
    internal Vector3 PointVelocity(int i,Vector3 point)
    {
        if(i<0||i>2||currentAt[i]<0||previousAt[i]<0)return Vector3.Zero;
        float dt=currentAt[i]-previousAt[i];if(!(dt>1e-4f)||dt>.2f)return Vector3.Zero;
        var c=current[i];var local=Vector3.Transform(point-c.Position,Quaternion.Inverse(c.Rotation));
        var before=previous[i].Point(local);
        var v=(point-before)/dt;
        return float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)?v:Vector3.Zero;
    }
    // The knock's speed when the two came to touch now (0: no knock).
    internal float Step(bool touching,int a,int b,Vector3 point,float now)
    {
        if(!touching){Touching=false;return 0;}
        if(Touching)return 0;
        Touching=true;
        float speed=(PointVelocity(a,point)-PointVelocity(b,point)).Length();
        if(!float.IsFinite(speed)||speed<MinSpeed||now-lastKnock<MinGap)return 0;
        lastKnock=now;return speed;
    }
    // Loudness of a knock (0.2 quiet tap .. 0.8).
    internal static float Volume(float speed)=>!float.IsFinite(speed)||speed<MinSpeed?0:Math.Clamp(.2f+.6f*(speed-MinSpeed)/(FullSpeed-MinSpeed),.2f,.8f);
}
