using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// Small owned meshes in metres (triangles in palette colours, mirrored for the left hand).
// 0.1.254: the watch is no longer built here (WatchModelMath: the 3D watch).
internal sealed class HandMeshGeometry
{
    internal readonly List<Vector3> Vertices=new();
    internal readonly List<Vector4> Colors=new();
    internal readonly List<int> Triangles=new();
    internal readonly List<int> MaterialSlots=new();
    internal readonly List<Vector4> Palette=new();
    private readonly bool right;
    internal HandMeshGeometry(bool isRight){right=isRight;}
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
}
