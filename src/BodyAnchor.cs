using System;
using System.Numerics;
namespace XiiiXR;
// The desktop camera is deliberately not an input. Its animations, pitch and
// temporary render pose must never feed back into the tracking origin.
internal sealed class BodyAnchor
{
    private bool initialized;
    private Quaternion previousYaw;
    private Vector3 previousRoot;
    internal Vector3 TurnOffset { get; private set; }
    internal void Reset() { initialized=false; TurnOffset=Vector3.Zero; }
    // Only the displacement actually accepted by CharacterController.Move is
    // consumed. Camera/hand world positions stay unchanged by body alignment.
    internal void ConsumeBodyMove(Vector3 actual)
    { if(float.IsFinite(actual.X)&&float.IsFinite(actual.Z)) TurnOffset-=new Vector3(actual.X,0,actual.Z); }
    internal Vector3 BodyGap(Quaternion yaw,Vector3 relativeHead)
    {return TurnOffset+Vector3.Transform(new Vector3(relativeHead.X,0,relativeHead.Z),PoseMath.Yaw(yaw));}
    internal PoseValue Sample(Vector3 root,Quaternion body,float eyeHeight,Vector3 relativeHead)
    {
        var yaw=PoseMath.Yaw(body);
        if(!initialized || Vector3.DistanceSquared(root,previousRoot)>25)
        { TurnOffset=Vector3.Zero; previousYaw=yaw; initialized=true; }
        // Turn about the current HMD, including room-scale displacement. Reading
        // twice in a frame adds zero once the body's yaw has been consumed.
        var horizontal=new Vector3(relativeHead.X,0,relativeHead.Z);
        TurnOffset+=Vector3.Transform(horizontal,previousYaw)-Vector3.Transform(horizontal,yaw);
        previousYaw=yaw; previousRoot=root;
        return new PoseValue(root+Vector3.UnitY*eyeHeight+TurnOffset,yaw);
    }
}
