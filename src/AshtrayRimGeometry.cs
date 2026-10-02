using System;using System.Collections.Generic;using System.Numerics;
namespace XiiiXR;
// Find the two actual faces at a purchase point just inside the rear rim.
// A mesh AABB and the gap between guessed fingertips are not contact surfaces.
internal static class AshtrayRimGeometry
{
    internal const float Purchase=.008f;
    internal static bool SegmentClear(Vector3 a,Vector3 b,Vector3 edge,float thickness,float radius)
    {
        // Open interior of a tray slab extended forward from its rear rim.
        // Skin-pad endpoints may touch a face; phalanges retain a small radius.
        float lo=0,hi=1;
        bool Clip(float p,float d,float min,float max)
        {
            if(Math.Abs(d)<1e-8f)return p>min&&p<max;
            float t0=(min-p)/d,t1=(max-p)/d;if(t0>t1)(t0,t1)=(t1,t0);
            lo=Math.Max(lo,t0);hi=Math.Min(hi,t1);return hi-lo>1e-6f;
        }
        return !Clip(a.Y,b.Y-a.Y,edge.Y-thickness*.5f-radius+.0002f,edge.Y+thickness*.5f+radius-.0002f)
            ||!Clip(a.Z,b.Z-a.Z,edge.Z-radius+.0002f,float.PositiveInfinity);
    }
    internal static bool TryMeasure(Vector3[] vertices,int[] triangles,float x,out Vector3 edge,out float thickness)
    {
        edge=Vector3.Zero;thickness=0;var section=PropSurfaceGeometry.Section(vertices,triangles,x);
        if(section.Count==0)return false;
        float near=float.PositiveInfinity,far=float.NegativeInfinity;
        foreach(var s in section){near=Math.Min(near,Math.Min(s.a.Z,s.b.Z));far=Math.Max(far,Math.Max(s.a.Z,s.b.Z));}
        if(!float.IsFinite(near)||far-near<Purchase*2)return false;
        float z=near+Purchase;
        if(!PropSurfaceGeometry.Faces(section,z,out float low,out float high))return false;
        thickness=high-low;
        // Imported dishes can be single-sided or thinner than four millimetres.
        // Keep their measured face, with a small virtual purchase thickness.
        if(!float.IsFinite(thickness)||thickness<0||thickness>.065f)return false;
        thickness=Math.Max(.004f,thickness);
        edge=new Vector3(x,(low+high)*.5f,near);return true;
    }
}
