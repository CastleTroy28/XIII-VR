using System;
using System.Numerics;
namespace XiiiXR;
internal static class WatchMountMath
{
    internal static (Vector3 position,Quaternion rotation) BandPose(WristFit fit,Quaternion swing,float size)
    {
        var bend=Quaternion.Slerp(Quaternion.Identity,swing,MathF.Round(ArmIkMath.ForearmWeight(fit.Center.Z)*64)/64);
        var q=Quaternion.Normalize(bend*fit.Rotation);
        return(new Vector3(0,0,NativeHandMesh.WristZ)+(Vector3.Transform(fit.Center,bend)-Vector3.Transform(new Vector3(0,-.008f,-.11f),q))*size,q);
    }
    internal static (Vector3 position,Quaternion rotation) Pose(WristFit fit,Quaternion swing,float size)
    {
        var top=fit.DisplayPoint??(fit.Center+Vector3.Transform(Vector3.UnitY*(fit.RadiusY+.0005f),fit.Rotation));
        var up=fit.DisplayNormal??Vector3.Transform(Vector3.UnitY,fit.Rotation);
        var z=Vector3.Transform(Vector3.UnitZ,fit.Rotation);
        var x=Vector3.Normalize(Vector3.Cross(up,z));z=Vector3.Cross(x,up);
        var rest=Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X,x.Y,x.Z,0,up.X,up.Y,up.Z,0,z.X,z.Y,z.Z,0,0,0,0,1));
        var bend=Quaternion.Slerp(Quaternion.Identity,swing,MathF.Round(ArmIkMath.ForearmWeight(top.Z)*64)/64);
        var q=Quaternion.Normalize(bend*rest);
        // Exact underside of the existing case geometry, before display lift.
        var underside=new Vector3(0,.032f+fit.RadiusY+.0025f-.040f,-.11f);
        return(new Vector3(0,0,NativeHandMesh.WristZ)+(Vector3.Transform(top,bend)-Vector3.Transform(underside,q))*size,q);
    }
}
