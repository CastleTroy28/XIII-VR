using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.255: a model in a few flat tones (its points' tones, one material a tone), as the mod's own models are
// drawn (the belt): the triangles between tones cut into cuts x cuts, each in the tone nearest the colour there
// (the colours blend across them as the points'), their edges' points shared with their neighbours.
internal readonly struct ToneMesh
{
    internal readonly Vector3[] Points,Normals;
    internal readonly int[][] Tones;      // the triangles of each tone
    internal readonly Vector4[] Palette;  // 0..1
    internal ToneMesh(Vector3[] points,Vector3[] normals,int[][] tones,Vector4[] palette){Points=points;Normals=normals;Tones=tones;Palette=palette;}
    internal int TriangleCount{get{int n=0;foreach(var t in Tones)n+=t.Length/3;return n;}}
}
internal sealed class ToneMeshBuilder
{
    private readonly List<Vector3> points=new(),normals=new();
    private readonly List<int>[] lists;
    private readonly byte[] palette;
    private readonly Dictionary<long,int> edges=new();
    private readonly List<Vector3> colours=new();
    internal ToneMeshBuilder(byte[] palette){this.palette=palette;lists=new List<int>[palette.Length/3];for(int i=0;i<lists.Length;i++)lists[i]=new List<int>();}
    internal int Point(Vector3 p,Vector3 n,int tone)
    {points.Add(p);normals.Add(n.LengthSquared()>1e-12f?Vector3.Normalize(n):Vector3.UnitY);colours.Add(Colour(tone));return points.Count-1;}
    private Vector3 Colour(int tone)=>new(palette[tone*3],palette[tone*3+1],palette[tone*3+2]);
    private int Nearest(Vector3 c)
    {
        int best=0;float bestD=float.MaxValue;
        for(int t=0;t<lists.Length;t++){float d=Vector3.DistanceSquared(c,Colour(t));if(d<bestD){bestD=d;best=t;}}
        return best;
    }
    internal void Flat(int a,int b,int c,int tone){lists[tone].Add(a);lists[tone].Add(b);lists[tone].Add(c);}
    // A triangle of points (their tones by their colours): in one tone, or cut where its corners differ.
    internal void Blended(int a,int b,int c,int cuts)
    {
        int ta=Nearest(colours[a]),tb=Nearest(colours[b]),tc=Nearest(colours[c]);
        if(ta==tb&&tb==tc||cuts<2){Flat(a,b,c,tb==tc?tb:ta);return;}
        int n=cuts;var grid=new int[n+1,n+1];
        int EdgePoint(int p,int q,int k)
        {
            if(p>q){(p,q)=(q,p);k=n-k;}
            long key=((long)p*1048576+q)*16+k;
            if(edges.TryGetValue(key,out int i))return i;
            float t=(float)k/n;
            points.Add(Vector3.Lerp(points[p],points[q],t));normals.Add(Unit(Vector3.Lerp(normals[p],normals[q],t),normals[p]));colours.Add(Vector3.Lerp(colours[p],colours[q],t));
            edges[key]=points.Count-1;return points.Count-1;
        }
        for(int i=0;i<=n;i++)for(int j=0;i+j<=n;j++)
        {
            int index;
            if(i==0&&j==0)index=a;else if(i==n)index=b;else if(j==n)index=c;
            else if(j==0)index=EdgePoint(a,b,i);else if(i==0)index=EdgePoint(a,c,j);else if(i+j==n)index=EdgePoint(b,c,j);
            else
            {
                float u=(float)i/n,v=(float)j/n;
                points.Add(points[a]+(points[b]-points[a])*u+(points[c]-points[a])*v);
                normals.Add(Unit(normals[a]*(1-u-v)+normals[b]*u+normals[c]*v,normals[a]));
                colours.Add(colours[a]*(1-u-v)+colours[b]*u+colours[c]*v);
                index=points.Count-1;
            }
            grid[i,j]=index;
        }
        for(int i=0;i<n;i++)for(int j=0;i+j<n;j++)
        {
            Add(grid[i,j],grid[i+1,j],grid[i,j+1]);
            if(i+j<n-1)Add(grid[i+1,j],grid[i+1,j+1],grid[i,j+1]);
        }
        void Add(int p,int q,int r)=>Flat(p,q,r,Nearest((colours[p]+colours[q]+colours[r])/3));
    }
    private static Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>1e-12f?Vector3.Normalize(v):fallback;
    internal ToneMesh Build()
    {
        var tones=new int[lists.Length][];for(int t=0;t<lists.Length;t++)tones[t]=lists[t].ToArray();
        var pal=new Vector4[lists.Length];for(int t=0;t<lists.Length;t++){var c=Colour(t)/255f;pal[t]=new Vector4(c,1);}
        return new ToneMesh(points.ToArray(),normals.ToArray(),tones,pal);
    }
}
