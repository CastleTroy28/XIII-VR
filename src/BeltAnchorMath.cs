using System;
using System.Numerics;
namespace XiiiXR;
internal static class BeltAnchorMath
{
    // With no pelvis tracker, horizontal HMD heading estimates body yaw.
    // Pitch/roll never rotate the pouch; both room and stick yaw are included.
    // 0.1.133: right: the same place on the right side of the belt.
    internal static (Vector3 position,Quaternion rotation) Pose(Vector3 head,Quaternion heading,bool right=false)
    {
        var forward=Vector3.Transform(Vector3.UnitZ,heading);forward.Y=0;
        float yaw=forward.LengthSquared()<1e-6f?0:MathF.Atan2(forward.X,forward.Z);
        var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
        return(head+Vector3.Transform(new Vector3(right?.19f:-.19f,-.57f,.15f),rotation),rotation);
    }
}
