using System;
using System.Numerics;
namespace XiiiXR;
// Rigid native case: long edge across the palm, thickness below it. A SINGLE
// scale preserves every angle and proportion; fingers adapt to the measured box.
internal static class MedkitGeometry
{
    internal const float PalmFace=-.023f,FarEdge=.048f;
    private static readonly Vector3[] sizes={new(.14f,.035f,.085f),new(.18f,.055f,.11f)};
    internal static int Revision{get;private set;}
    internal static Vector3 Size(bool large)=>sizes[large?1:0];
    internal static Vector3 Center(bool large)
    {var size=Size(large);return new Vector3(0,PalmFace-size.Y*.5f,FarEdge-size.Z*.5f);}
    // Conservative finger capsules against the rigid case; canonical hand
    // coordinates start at the wrist, while the displayed root adds WristZ.
    internal static bool SegmentClear(Vector3 a,Vector3 b,bool large,float radius=.009f)
    {
        var center=Center(large)-new Vector3(0,0,NativeHandMesh.WristZ);
        var half=Size(large)*.5f+new Vector3(radius);var lo=center-half;var hi=center+half;
        var delta=b-a;float enter=0,exit=1;
        for(int i=0;i<3;i++)
        {
            float p=i==0?a.X:i==1?a.Y:a.Z,d=i==0?delta.X:i==1?delta.Y:delta.Z;
            float low=i==0?lo.X:i==1?lo.Y:lo.Z,high=i==0?hi.X:i==1?hi.Y:hi.Z;
            if(Math.Abs(d)<1e-8f){if(p<low||p>high)return true;continue;}
            float x=(low-p)/d,y=(high-p)/d;if(x>y)(x,y)=(y,x);
            enter=Math.Max(enter,x);exit=Math.Min(exit,y);if(enter>exit)return true;
        }
        return false;
    }
    internal static Matrix4x4 Fit(Vector3 center,Vector3 extent,bool large)
    {
        if(!float.IsFinite(extent.LengthSquared())||Math.Min(extent.X,Math.Min(extent.Y,extent.Z))<.00001f)
            throw new ArgumentException("Degenerate medkit bounds");
        var lengths=new[]{extent.X,extent.Y,extent.Z};var axes=new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ};
        int[] order={0,1,2};Array.Sort(order,(a,b)=>lengths[a].CompareTo(lengths[b]));
        var y=axes[order[0]];var x=axes[order[2]];var z=Vector3.Cross(x,y);
        var orientation=new Matrix4x4(x.X,y.X,z.X,0,x.Y,y.Y,z.Y,0,x.Z,y.Z,z.Z,0,0,0,0,1);
        float scale=(large?.18f:.14f)/lengths[order[2]];
        var size=new Vector3(lengths[order[2]],lengths[order[0]],lengths[order[1]])*scale;
        int slot=large?1:0;if(Vector3.Distance(sizes[slot],size)>.0001f){sizes[slot]=size;Revision++;}
        return Matrix4x4.CreateTranslation(-center)*orientation*Matrix4x4.CreateScale(scale)*Matrix4x4.CreateTranslation(Center(large));
    }
}
