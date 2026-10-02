using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.148: how an
// enemy's fingers curl into a fist. Worked out on the hand's rest (bind) pose,
// where the fingers lie flat: the back of the hand from the fingers' line and
// the knuckles (the thumb tells which side is the index), each finger's hinge across it,
// and how far a joint is bent now against its rest direction.
internal static class FistCurlMath
{
    // Degrees from the flat hand: a finger's knuckle, middle and end joint;
    // the thumb's three (it folds over the fingers, less).
    // 0.1.153: a tighter fist.
    internal static readonly float[] Finger={88,100,68},Thumb={22,38,42};
    // The back of the hand (unit; zero if it cannot be told) from the wrist,
    // the fingers' first joints and the thumb's end.
    internal static Vector3 Back(Vector3 wrist,Vector3[] knuckles,Vector3 thumb,bool right)
    {
        var forward=Vector3.Zero;
        foreach(var k in knuckles){var d=k-wrist;if(Finite(d)&&d.LengthSquared()>1e-12f)forward+=Vector3.Normalize(d);}
        if(forward.LengthSquared()<1e-8f)return Vector3.Zero;
        forward=Vector3.Normalize(forward);
        // Across the knuckles, from the one by the thumb (the index) to the
        // farthest (the little finger): the thumb only tells the side, its
        // own slant towards the palm does not tilt the hand.
        int near=-1,far=-1;float nearest=float.PositiveInfinity,farthest=-1;
        for(int i=0;i<knuckles.Length;i++)
        {
            float d=Vector3.DistanceSquared(knuckles[i],thumb);if(!float.IsFinite(d))continue;
            if(d<nearest){nearest=d;near=i;}
            if(d>farthest){farthest=d;far=i;}
        }
        if(near<0||far<0||near==far)return Vector3.Zero;
        var up=Vector3.Cross(forward,knuckles[far]-knuckles[near])*(right?1:-1);
        up-=forward*Vector3.Dot(up,forward);
        return !Finite(up)||up.LengthSquared()<1e-12f?Vector3.Zero:Vector3.Normalize(up);
    }
    // The axis a finger segment turns about to curl towards the palm (a
    // positive turn about it curls).
    internal static Vector3 Hinge(Vector3 segment,Vector3 back)
    {
        var h=Vector3.Cross(segment,-back);
        return !Finite(h)||h.LengthSquared()<1e-12f?Vector3.Zero:Vector3.Normalize(h);
    }
    // How far (degrees, + towards the palm) a segment is bent now from its rest direction, about the hinge.
    internal static float Bend(Vector3 rest,Vector3 now,Vector3 hinge)
    {
        rest-=hinge*Vector3.Dot(rest,hinge);now-=hinge*Vector3.Dot(now,hinge);
        if(!Finite(rest)||!Finite(now)||rest.LengthSquared()<1e-12f||now.LengthSquared()<1e-12f)return 0;
        rest=Vector3.Normalize(rest);now=Vector3.Normalize(now);
        float a=MathF.Acos(Math.Clamp(Vector3.Dot(rest,now),-1,1))*180/MathF.PI;
        return Vector3.Dot(Vector3.Cross(rest,now),hinge)<0?-a:a;
    }
    // How much more to turn a joint (degrees): from where the animation left
    // it to the fist, by the weight (0 open .. 1 clenched).
    internal static float Turn(float bend,float target,float weight)
    {
        if(!float.IsFinite(bend))bend=0;
        weight=float.IsFinite(weight)?Math.Clamp(weight,0,1):0;
        return Math.Clamp(target-bend,-40,140)*weight;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
