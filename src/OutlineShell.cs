using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.122: the reload hint is a white outline around the part to take (not
// a yellow ball): a slightly larger copy of the part turned inside out. Only
// its inner (back) faces are drawn, so it shows as a rim around the part.
// Normals of points at the same place are averaged, so hard edges do not
// split the rim.
internal static class OutlineShell
{
    internal static (Vector3[] vertices,int[] triangles) Build(IReadOnlyList<Vector3> points,IReadOnlyList<Vector3>? normals,IReadOnlyList<int> triangles,float thickness)
    {
        int n=points.Count;
        if(n==0||triangles.Count<3)return (Array.Empty<Vector3>(),Array.Empty<int>());
        var center=Vector3.Zero;int finite=0;
        foreach(var p in points)if(Finite(p)){center+=p;finite++;}
        center=finite>0?center/finite:Vector3.Zero;
        var sums=new Dictionary<(long,long,long),Vector3>();
        const float grid=1e-4f;
        (long,long,long) Key(Vector3 p)=>((long)MathF.Round(p.X/grid),(long)MathF.Round(p.Y/grid),(long)MathF.Round(p.Z/grid));
        Vector3 Normal(int i)
        {
            var v=normals!=null&&i<normals.Count?normals[i]:Vector3.Zero;
            if(!Finite(v)||v.LengthSquared()<1e-8f)v=points[i]-center;
            return v.LengthSquared()>1e-12f?Vector3.Normalize(v):Vector3.UnitY;
        }
        for(int i=0;i<n;i++){if(!Finite(points[i]))continue;var k=Key(points[i]);sums[k]=(sums.TryGetValue(k,out var s)?s:Vector3.Zero)+Normal(i);}
        var vertices=new Vector3[n];
        for(int i=0;i<n;i++)
        {
            var p=points[i];if(!Finite(p)){vertices[i]=center;continue;}
            var sum=sums[Key(p)];var dir=sum.LengthSquared()>1e-10f?Vector3.Normalize(sum):Normal(i);
            vertices[i]=p+dir*thickness;
        }
        var shell=new List<int>(triangles.Count);
        for(int t=0;t+2<triangles.Count;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
            if(a<0||b<0||c<0||a>=n||b>=n||c>=n)continue;
            shell.Add(a);shell.Add(c);shell.Add(b); // reversed: the inside shows
        }
        return (vertices,shell.ToArray());
    }
    // Brightness of the blinking white outline (Dim..1 over ~0.8 s).
    internal const float Hertz=1.25f,Dim=.45f; // 0.1.123: never darker than light grey
    internal static float Blink(float time)=>Dim+(1-Dim)*(.5f+.5f*MathF.Sin(time*Hertz*2*MathF.PI));
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
