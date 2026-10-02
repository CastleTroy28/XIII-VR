using System;using System.Linq;using System.Numerics;
namespace XiiiXR;
internal static class KeyGripGeometry
{
    internal static bool PinchProfile(string profile)=>profile=="key"||profile=="card";
    // pick: a lockpick — handle (the wider end) in the pinch, pick forward.
    internal static (Matrix4x4 fit,Vector3 edge,float thickness,Vector3 tip) Fit(Vector3[] vertices,int[] triangles,bool card,bool pick=false)
    {
        if(vertices.Length<3)throw new InvalidOperationException("Key/card mesh missing");
        var min=vertices.Aggregate(Vector3.Min);var max=vertices.Aggregate(Vector3.Max);var size=max-min;
        float[] extent={size.X,size.Y,size.Z};var axes=new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ};
        int along=Array.IndexOf(extent,extent.Max()),thin=Array.IndexOf(extent,extent.Min());
        if(along==thin||extent[along]<1e-6f)throw new InvalidOperationException("Key/card mesh has no usable axes");
        int wide=3-along-thin;var center=(min+max)*.5f;
        float Width(bool high)
        {
            float low=float.PositiveInfinity,top=float.NegativeInfinity;
            foreach(var p in vertices)
            {
                float z=Vector3.Dot(p-center,axes[along]);if((high?z:-z)<extent[along]*.2f)continue;
                float x=Vector3.Dot(p,axes[wide]);low=Math.Min(low,x);top=Math.Max(top,x);
            }
            return top-low;
        }
        // The bow is wider than the blade. Keep the bow towards the wrist.
        var forward=axes[along]*(!card&&Width(true)>Width(false)?-1:1);
        var normal=axes[thin];var across=Vector3.Cross(normal,forward);
        var turn=new Matrix4x4(across.X,normal.X,forward.X,0,across.Y,normal.Y,forward.Y,0,across.Z,normal.Z,forward.Z,0,0,0,0,1);
        float length=card?.09f:pick?.15f:.10f;
        var fit=Matrix4x4.CreateTranslation(-center)*turn*Matrix4x4.CreateScale(length/extent[along])*Matrix4x4.CreateTranslation(0,0,length*.5f);
        var points=vertices.Select(p=>Vector3.Transform(p,fit)).ToArray();
        var target=new Vector3(0,0,card?.008f:pick?PickHandleMiddle(points,triangles,length):.012f);var contact=Vector3.Zero;float best=float.PositiveInfinity;
        for(int i=0;i+2<triangles.Length;i+=3)
        {
            var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];
            var n=Vector3.Cross(b-a,c-a);if(n.LengthSquared()<1e-14f||Math.Abs(Vector3.Normalize(n).Y)<.6f)continue;
            var p=Closest(target,a,b,c);float distance=Vector3.DistanceSquared(p,target);
            if(distance<best){best=distance;contact=p;}
        }
        if(!float.IsFinite(best))throw new InvalidOperationException("Key/card has no pinch surface");
        // Find both faces at that occupied point. A hole in a key bow is not a grip.
        float lowY=float.PositiveInfinity,highY=float.NegativeInfinity;
        for(int i=0;i+2<triangles.Length;i+=3)
        {
            var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];
            float det=(b.X-a.X)*(c.Z-a.Z)-(b.Z-a.Z)*(c.X-a.X);if(Math.Abs(det)<1e-12f)continue;
            float u=((contact.X-a.X)*(c.Z-a.Z)-(contact.Z-a.Z)*(c.X-a.X))/det;
            float v=((b.X-a.X)*(contact.Z-a.Z)-(b.Z-a.Z)*(contact.X-a.X))/det;
            if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
            float y=a.Y+(b.Y-a.Y)*u+(c.Y-a.Y)*v;lowY=Math.Min(lowY,y);highY=Math.Max(highY,y);
        }
        contact.Y=(lowY+highY)*.5f;float thickness=Math.Clamp(highY-lowY,.004f,.025f);
        return (fit,contact-Vector3.UnitZ*AshtrayRimGeometry.Purchase,thickness,new Vector3(0,0,length));
    }
    // 0.1.105: pinch a lockpick in the middle of its handle (the thick part at
    // the wrist end), not at its very end.
    internal static float PickHandleMiddle(Vector3[] points,int[] triangles,float length)
    {
        var (start,end)=PickHandle(points,triangles,length);
        if(!float.IsFinite(start))return .035f;
        return Math.Clamp((start+end)*.5f,.02f,.07f);
    }
    // 0.1.109: extent of the thick handle along the fitted lockpick (Z).
    internal static (float start,float end) PickHandle(Vector3[] points,int[] triangles,float length)
    {
        const float bin=.005f;int n=Math.Max(1,(int)Math.Ceiling(length/bin));var width=new float[n];
        // Triangles cover every bin they span (a cylinder side has vertices only at its ends).
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            var a=points[triangles[t]];var b=points[triangles[t+1]];var c=points[triangles[t+2]];
            float r=Math.Max(MathF.Sqrt(a.X*a.X+a.Y*a.Y),Math.Max(MathF.Sqrt(b.X*b.X+b.Y*b.Y),MathF.Sqrt(c.X*c.X+c.Y*c.Y)));
            int i0=Math.Clamp((int)(Math.Min(a.Z,Math.Min(b.Z,c.Z))/bin),0,n-1),i1=Math.Clamp((int)(Math.Max(a.Z,Math.Max(b.Z,c.Z))/bin),0,n-1);
            for(int i=i0;i<=i1;i++)width[i]=Math.Max(width[i],r);
        }
        float max=width.Max();if(!(max>0))return (float.NaN,float.NaN);
        int start=-1,end=n;
        for(int i=0;i<n;i++){if(start<0){if(width[i]>=.7f*max)start=i;}else if(width[i]<.5f*max){end=i;break;}}
        if(start<0)return (float.NaN,float.NaN);
        return (start*bin,end*bin);
    }
    private static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
        if(d1<=0&&d2<=0)return a;
        var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
        float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
        var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
        float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
        float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
        float denom=1/(va+vb+vc);return a+ab*(vb*denom)+ac*(vc*denom);
    }
}
