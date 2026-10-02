using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.161: the game's broom on the floor has no physics body - put where
// the hand let go of it, it stayed there, standing. It now falls: from how
// it was held, it turns level (its long side along the floor) and drops onto
// the floor below, as a thing falls (gravity), in a moment.
internal static class LayFlatMath
{
    internal const float Gravity=9.81f;
    // The longest side of a box: 0 x, 1 y, 2 z.
    internal static int LongAxis(Vector3 size)=>size.X>=size.Y&&size.X>=size.Z?0:size.Y>=size.Z?1:2;
    internal static Vector3 Axis(int i)=>i==0?Vector3.UnitX:i==1?Vector3.UnitY:Vector3.UnitZ;
    // The turn that takes direction a to direction b (both non-zero), the shortest way.
    internal static Quaternion FromTo(Vector3 a,Vector3 b)
    {
        if(a.LengthSquared()<1e-12f||b.LengthSquared()<1e-12f)return Quaternion.Identity;
        a=Vector3.Normalize(a);b=Vector3.Normalize(b);
        float d=Math.Clamp(Vector3.Dot(a,b),-1,1);
        if(d>.999999f)return Quaternion.Identity;
        if(d<-.999999f)
        {
            var side=Vector3.Cross(a,Vector3.UnitY);if(side.LengthSquared()<1e-6f)side=Vector3.Cross(a,Vector3.UnitX);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(side),MathF.PI);
        }
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(a,b)),MathF.Acos(d));
    }
    // Level: the thing turned (the shortest way) so that its long side
    // (localLong, in its own frame) lies flat along heading (flattened; when
    // there is none, along its own flattened long side).
    internal static Quaternion Level(Quaternion current,Vector3 localLong,Vector3 heading)
    {
        var along=Vector3.Transform(localLong,current);
        var flat=new Vector3(heading.X,0,heading.Z);
        if(flat.LengthSquared()<1e-6f)flat=new Vector3(along.X,0,along.Z);
        if(flat.LengthSquared()<1e-6f)flat=Vector3.UnitX;
        return Quaternion.Normalize(FromTo(along,flat)*current);
    }
    // Where its root goes for the box (min..max about the root, in its own
    // frame, world units) turned by q to lie on floorY with its middle over (x, z).
    internal static Vector3 Rest(Vector3 min,Vector3 max,Quaternion q,float floorY,float x,float z)
    {
        var middle=Vector3.Transform((min+max)*.5f,q);float lowest=float.PositiveInfinity;
        for(int i=0;i<8;i++)
        {
            var c=new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z);
            lowest=Math.Min(lowest,Vector3.Transform(c,q).Y);
        }
        return new Vector3(x-middle.X,floorY-lowest,z-middle.Z);
    }
    // How much of a fall of `height` metres is done t seconds after letting go (0..1).
    internal static float Fall(float t,float height)
    {
        if(!float.IsFinite(t)||!float.IsFinite(height)||height<=1e-3f)return 1;
        return Math.Clamp(.5f*Gravity*t*t/height,0,1);
    }
    // Its head (the heavier end): the lower end while it stood; lying, the end
    // nearer its own origin (a broom's origin is at its bristles). +1: the
    // positive end of its long side.
    internal static int HeadEnd(float upOfPositiveEnd,float minAlong,float maxAlong)
    {
        if(Math.Abs(upOfPositiveEnd)>.5f)return upOfPositiveEnd>0?-1:1;
        return Math.Abs(maxAlong)<Math.Abs(minAlong)?1:-1;
    }
}
