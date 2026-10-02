using System;using System.Collections.Generic;using System.Linq;using System.Numerics;
namespace XiiiXR;
// Sections of the rendered triangles, not a corner of their bounding box.
internal static class PropSurfaceGeometry
{
    internal static List<(Vector3 a,Vector3 b)> Section(Vector3[] vertices,int[] triangles,float x)
    {
        var section=new List<(Vector3 a,Vector3 b)>();
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            var cut=new List<Vector3>(3);
            for(int j=0;j<3;j++)
            {
                int ia=triangles[t+j],ib=triangles[t+(j+1)%3];
                if(ia<0||ib<0||ia>=vertices.Length||ib>=vertices.Length)return new();
                var a=vertices[ia];var b=vertices[ib];float dx=b.X-a.X;
                if(Math.Abs(a.X-x)<1e-6f)Add(a);
                if(Math.Abs(dx)>1e-8f){float u=(x-a.X)/dx;if(u>0&&u<1)Add(Vector3.Lerp(a,b,u));}
            }
            if(cut.Count==2)section.Add((cut[0],cut[1]));
            else if(cut.Count==3)for(int j=0;j<3;j++)section.Add((cut[j],cut[(j+1)%3]));
            void Add(Vector3 p){foreach(var old in cut)if(Vector3.DistanceSquared(old,p)<1e-12f)return;cut.Add(p);}
        }
        return section;
    }
    internal static bool Faces(List<(Vector3 a,Vector3 b)> section,float z,out float low,out float high)
    {
        low=float.PositiveInfinity;high=float.NegativeInfinity;
        foreach(var s in section)
        {
            float dz=s.b.Z-s.a.Z;
            if(Math.Abs(dz)<1e-8f)continue;
            float u=(z-s.a.Z)/dz;if(u<0||u>1)continue;
            float y=s.a.Y+(s.b.Y-s.a.Y)*u;low=Math.Min(low,y);high=Math.Max(high,y);
        }
        return float.IsFinite(low)&&float.IsFinite(high);
    }
    internal static bool ChairRail(Vector3[] vertices,int[] triangles,out Vector3 center)
    {
        return ChairRail(vertices,triangles,out center,out _);
    }
    internal static bool ChairRail(Vector3[] vertices,int[] triangles,out Vector3 center,out float thickness)
    {
        thickness=0;center=Vector3.Zero;if(vertices.Length==0)return false;
        var min=vertices[0];var max=min;foreach(var p in vertices){min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        float x=(min.X+max.X)*.5f;var section=Section(vertices,triangles,x);
        float top=float.NegativeInfinity,bottom=float.PositiveInfinity;
        foreach(var s in section){top=Math.Max(top,Math.Max(s.a.Z,s.b.Z));bottom=Math.Min(bottom,Math.Min(s.a.Z,s.b.Z));}
        if(!float.IsFinite(top)||top-bottom<1e-5f)return false;
        // Stay inside the top rail, even when the seat is much deeper than it.
        float z=top-(top-bottom)*.018f;
        if(!Faces(section,z,out float low,out float high))return false;
        center=new Vector3(x,(low+high)*.5f,z);thickness=high-low;return thickness>1e-5f;
    }
}
// Small, immutable section table measured from the rendered chair mesh, in
// fitted item metres. Built once per visual; no mesh access in the pose loop.
internal sealed class ChairGripSurface
{
    private readonly Vector4[] sections;
    // 0.1.77: the actual section outlines near the rail, relative to the
    // contact, as (y,z) segments. Used to test finger/palm points against the
    // real chair (rail, gaps, cross rails, stiles) instead of a guessed slab.
    private readonly float[] cutX;
    private readonly (Vector2 a,Vector2 b)[][] cuts;
    internal int Count=>sections.Length;
    // Extent of the measured rail along the chair's width, relative to the contact.
    internal float MinX=>sections.Length==0?0:sections.Min(r=>r.X);
    internal float MaxX=>sections.Length==0?0:sections.Max(r=>r.X);
    // Per section: indices of its segments by 5mm band of Z, so a point test
    // looks at a handful of segments instead of the whole outline.
    private readonly int[][][] bands;
    private const float BandZ0=-.10f,BandSize=.005f;private const int BandCount=80;
    private ChairGripSurface(Vector4[] values,float[] xs,(Vector2,Vector2)[][] outlines)
    {sections=values;cutX=xs;cuts=outlines;bands=outlines.Select(Band).ToArray();}
    private static int[][] Band((Vector2 a,Vector2 b)[] outline)
    {
        var lists=new List<int>[BandCount];for(int i=0;i<BandCount;i++)lists[i]=new List<int>();
        for(int s=0;s<outline.Length;s++)
        {
            float z0=Math.Min(outline[s].a.Y,outline[s].b.Y),z1=Math.Max(outline[s].a.Y,outline[s].b.Y);
            int i0=Math.Clamp((int)MathF.Floor((z0-BandZ0)/BandSize),0,BandCount-1),i1=Math.Clamp((int)MathF.Floor((z1-BandZ0)/BandSize),0,BandCount-1);
            for(int i=i0;i<=i1;i++)lists[i].Add(s);
        }
        return lists.Select(l=>l.ToArray()).ToArray();
    }
    // Signed distance from the real section: negative inside the chair
    // (depth to the nearest face), positive outside, capped at `reach`.
    // Distance is measured in the section plane, in any direction.
    internal float Signed(Vector3 p,float reach)
    {
        int c=OutlineIndex(p.X);if(c<0)return reach;
        var outline=cuts[c];var band=bands[c];float y=p.Y,z=p.Z;
        bool inside=false;int b=(int)MathF.Floor((z-BandZ0)/BandSize);
        if(b>=0&&b<BandCount)
        {
            int up=0,down=0;
            foreach(int s in band[b])
            {
                var (a,e)=outline[s];if((a.Y<=z)==(e.Y<=z))continue;
                float yy=a.X+(e.X-a.X)*(z-a.Y)/(e.Y-a.Y);if(yy>y)up++;else down++;
            }
            inside=(up&1)==1&&(down&1)==1;
        }
        float best=reach*reach;
        int b0=Math.Max(0,(int)MathF.Floor((z-reach-BandZ0)/BandSize)),b1=Math.Min(BandCount-1,(int)MathF.Floor((z+reach-BandZ0)/BandSize));
        for(int i=b0;i<=b1;i++)foreach(int s in band[i])
        {
            var (a,e)=outline[s];float dy=e.X-a.X,dz=e.Y-a.Y,len=dy*dy+dz*dz;
            float u=len>1e-12f?Math.Clamp(((y-a.X)*dy+(z-a.Y)*dz)/len,0,1):0;
            float ry=a.X+dy*u-y,rz=a.Y+dz*u-z;best=Math.Min(best,ry*ry+rz*rz);
        }
        float d=MathF.Sqrt(best);return inside?-d:Math.Min(d,reach);
    }
    // The same chair seen from the other side: 180 degrees about the
    // contact's Z axis (x and y mirrored), for a grip with the fingers on
    // the opposite face.
    internal ChairGripSurface Flipped()
    {
        var rows=sections.Select(r=>new Vector4(-r.X,-r.Z,-r.Y,r.W)).OrderBy(r=>r.X).ToArray();
        var order=Enumerable.Range(0,cutX.Length).OrderBy(i=>-cutX[i]).ToArray();
        return new ChairGripSurface(rows,order.Select(i=>-cutX[i]).ToArray(),
            order.Select(i=>cuts[i].Select(s=>(new Vector2(-s.a.X,s.a.Y),new Vector2(-s.b.X,s.b.Y))).ToArray()).ToArray());
    }
    // Compact section dump (mm) for offline reproduction from a log.
    internal string Describe(float[] xs,float zMin=-.03f,float zMax=.16f,float yMax=.07f)
    {
        var b=new System.Text.StringBuilder();
        foreach(float x in xs)
        {
            int c=OutlineIndex(x);if(c<0)continue;
            b.Append(" |x=").Append((cutX[c]*1000).ToString("F0",System.Globalization.CultureInfo.InvariantCulture)).Append(':');
            foreach(var (a,e) in cuts[c])
            {
                if(Math.Max(a.Y,e.Y)<zMin||Math.Min(a.Y,e.Y)>zMax||Math.Min(Math.Abs(a.X),Math.Abs(e.X))>yMax)continue;
                b.Append(' ').Append(F(a.X)).Append(',').Append(F(a.Y)).Append(',').Append(F(e.X)).Append(',').Append(F(e.Y));
            }
        }
        return b.ToString();
        static string F(float v)=>(v*1000).ToString("F1",System.Globalization.CultureInfo.InvariantCulture);
    }
    // step: spacing of the section cuts across the rail (0.1.85: 4mm for the
    // thin, slightly yawed ashtray shell, 8mm for chairs).
    internal static ChairGripSurface Measure(Vector3[] vertices,int[] triangles,Vector3 contact,float step=.008f)
    {
        var rows=new List<Vector4>();var xs=new List<float>();var outlines=new List<(Vector2,Vector2)[]>();
        int cuts=(int)MathF.Round(.16f/step);
        for(int i=0;i<=cuts;i++)
        {
            float x=contact.X-.08f+i*step;
            var section=PropSurfaceGeometry.Section(vertices,triangles,x);
            var outline=new List<(Vector2,Vector2)>();
            // Cut slightly off the sample X: a plane through a vertex row (the
            // symmetric centre of a chair) yields doubled edges and breaks the
            // inside/outside parity.
            foreach(var s in PropSurfaceGeometry.Section(vertices,triangles,x+.000137f))
            {
                float z0=Math.Min(s.a.Z,s.b.Z)-contact.Z,z1=Math.Max(s.a.Z,s.b.Z)-contact.Z;
                if(z1<-.10f||z0>.30f)continue;
                outline.Add((new Vector2(s.a.Y-contact.Y,s.a.Z-contact.Z),new Vector2(s.b.Y-contact.Y,s.b.Z-contact.Z)));
            }
            xs.Add(x-contact.X);outlines.Add(outline.ToArray());
            float edge=float.PositiveInfinity;
            // A section crosses the seat/legs as well as the backrest. The
            // globally rearmost edge can belong to another piece 20cm away.
            // Search only the rail already selected by ChairRail, in metres.
            foreach(var s in section)
            {
                var a=s.a;var b=s.b;float dy=b.Y-a.Y,lo=0,hi=1;
                if(Math.Abs(dy)<1e-8f){if(Math.Abs(a.Y-contact.Y)>.055f)continue;}
                else
                {
                    float u=(contact.Y-.055f-a.Y)/dy,v=(contact.Y+.055f-a.Y)/dy;
                    lo=Math.Max(0,Math.Min(u,v));hi=Math.Min(1,Math.Max(u,v));if(lo>hi)continue;
                }
                float z0=Math.Min(Vector3.Lerp(a,b,lo).Z,Vector3.Lerp(a,b,hi).Z);
                if(Math.Abs(z0-contact.Z)<=.05f)edge=Math.Min(edge,z0);
            }
            // Feet extend +Z in the fitted mesh. Curl each finger over the
            // occupied back edge at its own X, including curved/sloping rails.
            if(!float.IsFinite(edge))continue;
            // 0.1.81: a thin lip (ashtray) may cross less than 1mm at 8mm;
            // look a little further in before giving up on this X.
            foreach(float inset in new[]{.008f,.016f,.024f})
            {
                float z=edge+inset;
                float low=float.PositiveInfinity,high=float.NegativeInfinity;
                foreach(var s in section)
                {
                    float dz=s.b.Z-s.a.Z;if(Math.Abs(dz)<1e-8f)continue;
                    float u=(z-s.a.Z)/dz;if(u<0||u>1)continue;
                    float y=s.a.Y+(s.b.Y-s.a.Y)*u;if(Math.Abs(y-contact.Y)>.055f)continue;
                    low=Math.Min(low,y);high=Math.Max(high,y);
                }
                if(!float.IsFinite(low)||!float.IsFinite(high)||high-low<.0003f||high-low>.065f)continue;
                // Row W keeps the edge at W-8mm whatever depth measured the faces.
                rows.Add(new Vector4(x-contact.X,low-contact.Y,high-contact.Y,edge+.008f-contact.Z));break;
            }
        }
        if(rows.Count<3)throw new InvalidOperationException("Chair local rail contact sections unavailable");
        return new ChairGripSurface(rows.ToArray(),xs.ToArray(),outlines.ToArray());
    }
    internal Vector3 Pad(float x,bool thumb)
    {
        var s=Row(x);
        return new Vector3(s.X,thumb?s.Z:s.Y,s.W);
    }
    private Vector4 Row(float x)
    {
        var a=sections[0];var b=a;
        for(int i=1;i<sections.Length;i++){b=sections[i];if(x<=b.X)break;a=b;}
        float t=b.X-a.X>1e-6f?Math.Clamp((x-a.X)/(b.X-a.X),0,1):0;
        return Vector4.Lerp(a,b,t);
    }
    // Near (top) edge of the rail at this X, relative to the contact.
    internal float Edge(float x)=>Row(x).W-.008f;
    private int OutlineIndex(float x)
    {
        if(cuts.Length==0)return -1;
        int best=0;for(int i=1;i<cutX.Length;i++)if(Math.Abs(cutX[i]-x)<Math.Abs(cutX[best]-x))best=i;
        return Math.Abs(cutX[best]-x)<=.012f?best:-1;
    }
    private (Vector2 a,Vector2 b)[]? Outline(float x){int i=OutlineIndex(x);return i<0?null:cuts[i];}
    // Rail faces crossed by the line z=const near the contact, at this X.
    internal bool Faces(float x,float z,out float low,out float high)
    {
        low=float.PositiveInfinity;high=float.NegativeInfinity;
        var outline=Outline(x);if(outline==null)return false;
        foreach(var (a,b) in outline)
        {
            if((a.Y<=z)==(b.Y<=z))continue;
            float y=a.X+(b.X-a.X)*(z-a.Y)/(b.Y-a.Y);if(Math.Abs(y)>.055f)continue;
            low=Math.Min(low,y);high=Math.Max(high,y);
        }
        return float.IsFinite(low)&&high-low>.001f;
    }
    // Point inside the real chair section (both vertical rays cross an odd
    // number of surfaces). Depth: distance to the nearest face along Y.
    internal bool Inside(Vector3 p,out float depth)
    {
        depth=0;var outline=Outline(p.X);if(outline==null)return false;
        int up=0,down=0;float toUp=float.PositiveInfinity,toDown=float.PositiveInfinity;
        foreach(var (a,b) in outline)
        {
            if((a.Y<=p.Z)==(b.Y<=p.Z))continue;
            float y=a.X+(b.X-a.X)*(p.Z-a.Y)/(b.Y-a.Y);
            if(y>p.Y){up++;toUp=Math.Min(toUp,y-p.Y);}else{down++;toDown=Math.Min(toDown,p.Y-y);}
        }
        if(up%2==0||down%2==0)return false;
        depth=Math.Min(toUp,toDown);return true;
    }
}
