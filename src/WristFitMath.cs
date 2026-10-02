using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
internal readonly record struct WristFit(Vector3 Center,Quaternion Rotation,float RadiusX,float RadiusY,Vector3? DisplayPoint=null,Vector3? DisplayNormal=null)
{
    internal static WristFit Default=>new(new Vector3(0,-.008f,-.045f),Quaternion.Identity,.034f,.038f);
}
internal static class WristFitMath
{
    // Intersect the actual neutral skin with a plane across the forearm.
    // Accessories are excluded before this call. No guessed ring centre/size.
    internal static WristFit Fit(Vector3[] points,int[][] triangles,bool[] eligible,Vector3 elbow,float distance=.052f)
    {
        var forward=-Vector3.Normalize(elbow);
        var up=Vector3.UnitY-forward*Vector3.Dot(Vector3.UnitY,forward);
        if(up.LengthSquared()<1e-6f)throw new InvalidOperationException("Invalid wrist cross-section frame");
        up=Vector3.Normalize(up);var x=Vector3.Normalize(Vector3.Cross(up,forward));up=Vector3.Cross(forward,x);
        distance=Math.Clamp(distance,.006f,.10f);var planePoint=-forward*distance;
        var crossings=new List<Vector2>();
        foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3)
        {
            int a=sub[i],b=sub[i+1],c=sub[i+2];if(!eligible[a]||!eligible[b]||!eligible[c])continue;
            Edge(points[a],points[b]);Edge(points[b],points[c]);Edge(points[c],points[a]);
        }
        void Edge(Vector3 a,Vector3 b)
        {
            float da=Vector3.Dot(a-planePoint,forward),db=Vector3.Dot(b-planePoint,forward);
            if((da>0)==(db>0)||Math.Abs(da-db)<1e-7f)return;
            var p=Vector3.Lerp(a,b,da/(da-db))-planePoint;
            var v=new Vector2(Vector3.Dot(p,x),Vector3.Dot(p,up));
            if(v.LengthSquared()<.01f)crossings.Add(v);
        }
        if(crossings.Count<8)throw new InvalidOperationException("Native wrist section missing");
        var min=new Vector2(float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity);
        foreach(var p in crossings){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        var center=(min+max)*.5f;var radius=(max-min)*.5f;
        if(radius.X<.008f||radius.Y<.008f||radius.X>.055f||radius.Y>.055f)throw new InvalidOperationException("Native wrist section out of bounds");
        // Ellipse enclosing sampled skin, then 1.5 mm clearance. Bounding radii
        // alone can cut through asymmetric corners of a non-elliptical wrist.
        float expansion=1;
        foreach(var p in crossings){var d=p-center;expansion=Math.Max(expansion,MathF.Sqrt(d.X*d.X/(radius.X*radius.X)+d.Y*d.Y/(radius.Y*radius.Y)));}
        radius*=Math.Min(expansion,1.3f);
        var rotation=Quaternion.CreateFromRotationMatrix(new Matrix4x4(x.X,x.Y,x.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1));
        return new WristFit(planePoint+x*center.X+up*center.Y,rotation,radius.X+.0015f,radius.Y+.0015f);
    }
    internal static WristFit AttachToNativeCase(WristFit fit,Vector3[] points,int[][] triangles,bool[] skin,bool[] accessory)
    {
        Vector3 center=Vector3.Zero;int count=0;
        for(int i=0;i<points.Length;i++)if(accessory[i]){center+=points[i];count++;}
        if(count<12)return fit;
        center/=count;
        var forward=Vector3.Transform(Vector3.UnitZ,fit.Rotation);
        var outward=center-fit.Center;outward-=forward*Vector3.Dot(outward,forward);
        if(outward.LengthSquared()<.000001f)return fit;
        outward=Vector3.Normalize(outward);
        // The case may sit off-centre on an asymmetric wrist. Its centroid
        // points toward the correct side, but is not its face normal (the old
        // radial estimate rolled a display by about 18 degrees).
        var radial=outward;var faceNormal=Vector3.Zero;float areaSum=0;
        foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3)
        {
            int a=sub[i],b=sub[i+1],c=sub[i+2];if(!accessory[a]||!accessory[b]||!accessory[c])continue;
            var cross=Vector3.Cross(points[b]-points[a],points[c]-points[a]);float area=cross.Length();
            if(area<1e-9f)continue;
            var normal=cross/area;
            if(Vector3.Dot(normal,radial)<.75f||Vector3.Dot((points[a]+points[b]+points[c])/3-center,radial)<-.001f)continue;
            // Broad flat case faces dominate bevel and strap triangles.
            float weight=area*area;faceNormal+=normal*weight;areaSum+=weight;
        }
        if(areaSum>1e-12f&&faceNormal.LengthSquared()>1e-20f)
        {outward=Vector3.Normalize(faceNormal);}
        var origin=center-outward*Vector3.Dot(center-fit.Center,outward);
        float nearest=float.PositiveInfinity;
        foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3)
        {
            int a=sub[i],b=sub[i+1],c=sub[i+2];if(!skin[a]||!skin[b]||!skin[c])continue;
            // Intersect the real skin underneath the removed stock watch, not
            // an enclosing ellipse that can float several mm above it.
            var e1=points[b]-points[a];var e2=points[c]-points[a];var h=Vector3.Cross(outward,e2);float det=Vector3.Dot(e1,h);
            if(Math.Abs(det)<1e-10f)continue;
            var t=origin-points[a];float u=Vector3.Dot(t,h)/det;if(u<0||u>1)continue;
            var q=Vector3.Cross(t,e1);float v=Vector3.Dot(outward,q)/det;if(v<0||u+v>1)continue;
            float d=Vector3.Dot(e2,q)/det;if(d>.004f&&d<.08f)nearest=Math.Min(nearest,d);
        }
        return float.IsFinite(nearest)?fit with{DisplayPoint=origin+outward*(nearest+.0002f),DisplayNormal=outward}:fit;
    }
    internal static float AccessoryDistance(Vector3[] points,bool[] accessory,Vector3 elbow)
    {
        var axis=Vector3.Normalize(elbow);float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int i=0;i<points.Length;i++)if(accessory[i])
        {float d=Vector3.Dot(points[i],axis);min=Math.Min(min,d);max=Math.Max(max,d);}
        return float.IsFinite(min)&&float.IsFinite(max)?Math.Clamp((min+max)*.5f,.006f,.10f):.052f;
    }
}
