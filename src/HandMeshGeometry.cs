using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// Closed watch geometry, in metres. Hand meshes come from the game.
internal sealed class HandMeshGeometry
{
    internal readonly List<Vector3> Vertices=new();
    internal readonly List<Vector4> Colors=new();
    internal readonly List<int> Triangles=new();
    internal readonly List<int> MaterialSlots=new();
    internal readonly List<Vector4> Palette=new();
    internal const float ScreenScale=.0002f, FaceSize=.72f;
    internal static readonly Vector3 ScreenPosition=new(0,.0392f,-.11f);
    private readonly bool right;
    private static readonly Vector4 Rubber=new(.085f,.088f,.092f,1),Seam=new(.16f,.165f,.17f,1);
    internal HandMeshGeometry(bool isRight){right=isRight;}
    internal static HandMeshGeometry BuildWatch(bool right,float radiusX=.036f,float radiusY=.040f,bool includeStrap=true)
    {var m=new HandMeshGeometry(right);m.Watch(right,radiusX,radiusY,includeStrap);return m;}
    internal static HandMeshGeometry BuildBand(bool right,float rx,float ry,bool native)
    {var m=new HandMeshGeometry(right);m.LinkedBand(rx,ry,native?2:0,native?6:16);return m;}
    private void Watch(bool right,float rx,float ry,bool includeStrap)
    {
        // Keep the native left strap when present; only replace its dead case.
        // The other wrist gets a compact band around its sampled skin section.
        if(includeStrap)LinkedBand(rx,ry,0,16);
        
        float lift=ry+.0025f-.040f;
        var accent=new Vector4(.46f,.39f,.24f,1);
        Case(new Vector3(0,.032f+lift,-.11f),.078f*FaceSize,.060f*FaceSize,.005f,.009f*FaceSize,Seam);
        Case(new Vector3(0,.037f+lift,-.11f),.071f*FaceSize,.052f*FaceSize,.001f,.007f*FaceSize,accent);
        Case(new Vector3(0,.038f+lift,-.11f),.067f*FaceSize,.048f*FaceSize,.0005f,.005f*FaceSize,new(.19f,.21f,.19f,1));
    }
    private void LinkedBand(float rx,float ry,int first,int last)
    {
        // Closed three-column links, shared wrist cross-section with the case.
        // Fine separators reveal a solid dark backing, never missing geometry.
        Ring(-.11f,.022f,rx+.0015f,ry+.0015f,rx,ry,Rubber,first,last);
        for(int i=first;i<last;i++)
        {
            Ring(-.1175f,.007f,rx+.0025f,ry+.0025f,rx+.0014f,ry+.0014f,Seam,i,i+1,.025f);
            Ring(-.11f,.007f,rx+.003f,ry+.003f,rx+.0014f,ry+.0014f,new Vector4(.115f,.12f,.125f,1),i,i+1,.025f);
            Ring(-.1025f,.007f,rx+.0025f,ry+.0025f,rx+.0014f,ry+.0014f,Seam,i,i+1,.025f);
        }
    }
    private Vector3 Mirror(Vector3 p)=>right?p:new Vector3(-p.X,p.Y,p.Z);
    internal void Tri(Vector3 a,Vector3 b,Vector3 c,Vector4 color)
    {
        a=Mirror(a);b=Mirror(b);c=Mirror(c);
        var normal=Vector3.Cross(b-a,c-a);if(normal.LengthSquared()<1e-18f)return;
        float shade=.62f+.38f*MathF.Abs(Vector3.Dot(Vector3.Normalize(normal),Vector3.Normalize(new Vector3(-.3f,1,.2f))));
        var tint=new Vector4(color.X*shade,color.Y*shade,color.Z*shade,color.W);
        int start=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);
        int slot=Palette.IndexOf(color);if(slot<0){slot=Palette.Count;Palette.Add(color);}MaterialSlots.Add(slot);
        Colors.Add(tint);Colors.Add(tint);Colors.Add(tint);
        Triangles.Add(start);Triangles.Add(start+(right?1:2));Triangles.Add(start+(right?2:1));
    }
    internal void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector4 color,bool reverse=false)
    {if(reverse){Tri(a,c,b,color);Tri(a,d,c,color);}else{Tri(a,b,c,color);Tri(a,c,d,color);}}
    private void Ring(float z,float width,float rx,float ry,float ix,float iy,Vector4 color,int first=0,int last=16,float inset=0)
    {
        Vector3 P(int s,bool inner,bool back)=>new((inner?ix:rx)*MathF.Cos((s+(s==first?inset:s==last?-inset:0))*MathF.PI/8),(inner?iy:ry)*MathF.Sin((s+(s==first?inset:s==last?-inset:0))*MathF.PI/8)-.008f,z+(back?-1:1)*width*.5f);
        if(first!=0||last!=16)
        {
            Quad(P(first,false,false),P(first,true,false),P(first,true,true),P(first,false,true),color);
            Quad(P(last,false,true),P(last,true,true),P(last,true,false),P(last,false,false),color);
        }
        for(int i=first;i<last;i++)
        {
            Quad(P(i,false,false),P(i,false,true),P(i+1,false,true),P(i+1,false,false),color);
            Quad(P(i,true,true),P(i,true,false),P(i+1,true,false),P(i+1,true,true),color);
            Quad(P(i,false,false),P(i+1,false,false),P(i+1,true,false),P(i,true,false),color);
            Quad(P(i,false,true),P(i,true,true),P(i+1,true,true),P(i+1,false,true),color);
        }
    }
    private void Case(Vector3 p,float width,float length,float height,float bevel,Vector4 color)
    {
        float x=width/2,z=length/2;
        var corners=new[]{new Vector3(-x+bevel,0,-z),new Vector3(x-bevel,0,-z),new Vector3(x,0,-z+bevel),new Vector3(x,0,z-bevel),new Vector3(x-bevel,0,z),new Vector3(-x+bevel,0,z),new Vector3(-x,0,z-bevel),new Vector3(-x,0,-z+bevel)};
        var up=Vector3.UnitY*height;
        for(int i=0;i<8;i++)
        {
            var a=p+corners[i];var b=p+corners[(i+1)%8];
            Quad(a,b,b+up,a+up,color,true);Tri(p+up,b+up,a+up,color);
            Tri(p,a,b,color); // Underside, outward normal -Y; never an open shell.
        }
    }
}
