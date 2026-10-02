using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.195. The handle is not rigged: its
// mesh pieces are found around the rigged part it sits on (the seed): the
// small, separate pieces of the mesh (vertices welded by position, joined by
// triangles) that touch the seed's surroundings and belong to no other rigged
// part. The gun's big pieces (its body, its top cover) are too large to pass.
internal static class KnobMath
{
    internal const float Weld=1e-4f;
    internal static int[] Select(Vector3[] points,int[] triangles,bool[] seed,bool[] excluded,float reach,float maxSize)
    {
        int n=points.Length;if(n==0||seed.Length!=n||excluded.Length!=n)return Array.Empty<int>();
        var c=Vector3.Zero;int seeds=0;for(int i=0;i<n;i++)if(seed[i]){c+=points[i];seeds++;}
        if(seeds==0)return Array.Empty<int>();c/=seeds;
        // Weld by position, then join along triangle edges.
        var key=new Dictionary<(long,long,long),int>();var rep=new int[n];
        for(int i=0;i<n;i++)
        {
            var p=points[i];var k=((long)MathF.Round(p.X/Weld),(long)MathF.Round(p.Y/Weld),(long)MathF.Round(p.Z/Weld));
            if(!key.TryGetValue(k,out int r)){r=i;key[k]=i;}rep[i]=r;
        }
        var parent=new int[n];for(int i=0;i<n;i++)parent[i]=i;
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        void Join(int a,int b){a=Find(rep[a]);b=Find(rep[b]);if(a!=b)parent[a]=b;}
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],d=triangles[t+2];
            if((uint)a>=(uint)n||(uint)b>=(uint)n||(uint)d>=(uint)n)continue;
            Join(a,b);Join(b,d);
        }
        var lo=new Dictionary<int,Vector3>();var hi=new Dictionary<int,Vector3>();var near=new HashSet<int>();var bad=new HashSet<int>();
        for(int i=0;i<n;i++)
        {
            int g=Find(rep[i]);var p=points[i];
            if(!float.IsFinite(p.X+p.Y+p.Z)){bad.Add(g);continue;}
            if(lo.TryGetValue(g,out var l)){lo[g]=Vector3.Min(l,p);hi[g]=Vector3.Max(hi[g],p);}else{lo[g]=p;hi[g]=p;}
            if(excluded[i])bad.Add(g);
            if(MathF.Abs(p.X-c.X)<reach&&MathF.Abs(p.Z-c.Z)<reach*1.5f&&p.Y>c.Y-reach*.6f)near.Add(g);
        }
        var chosen=new HashSet<int>();
        foreach(int g in near)
        {
            if(bad.Contains(g))continue;var size=hi[g]-lo[g];
            if(size.X>maxSize||size.Y>maxSize||size.Z>maxSize)continue;
            chosen.Add(g);
        }
        var list=new List<int>();
        for(int i=0;i<n;i++)if(!seed[i]&&chosen.Contains(Find(rep[i])))list.Add(i);
        // A handle is a small share of the gun; more means the mesh is one welded piece.
        return list.Count>n/4?Array.Empty<int>():list.ToArray();
    }
    // 0.1.197: the handle is what stands up out
    // of the gun's top around the rigged bolt part: the points within `half`
    // across and `reach` along of it (between the bolt bone and the bolt part)
    // that are higher than the top just in front of and behind it (the lower
    // of the two, so a sight on one side does not hide it), with the whole
    // small pieces they belong to. None if fewer than MinTop such points.
    internal const int MinTop=3;
    internal static int[] SelectTop(Vector3[] points,int[] triangles,bool[] seed,bool[] excluded,Vector3 bone,float half,float reach,float maxSize,out float top)
    {
        top=float.NaN;int n=points.Length;
        if(n==0||seed.Length!=n||excluded.Length!=n||!float.IsFinite(bone.X+bone.Y+bone.Z))return Array.Empty<int>();
        var c=bone;int seeds=0;var sum=Vector3.Zero;for(int i=0;i<n;i++)if(seed[i]&&float.IsFinite(points[i].X+points[i].Y+points[i].Z)){sum+=points[i];seeds++;}
        float z0=bone.Z,z1=bone.Z;if(seeds>0){var sc=sum/seeds;z0=Math.Min(z0,sc.Z);z1=Math.Max(z1,sc.Z);}
        z0-=reach;z1+=reach;
        float front=float.NegativeInfinity,back=float.NegativeInfinity;
        for(int i=0;i<n;i++)
        {
            var p=points[i];if(!float.IsFinite(p.X+p.Y+p.Z)||excluded[i]||seed[i]||MathF.Abs(p.X-c.X)>half*.8f)continue;
            if(p.Z>z1&&p.Z<=z1+reach)front=Math.Max(front,p.Y);
            else if(p.Z<z0&&p.Z>=z0-reach)back=Math.Max(back,p.Y);
        }
        top=float.IsFinite(front)&&float.IsFinite(back)?Math.Min(front,back):float.IsFinite(front)?front:float.IsFinite(back)?back:c.Y;
        var candidate=new bool[n];int count=0;
        for(int i=0;i<n;i++)
        {
            var p=points[i];if(!float.IsFinite(p.X+p.Y+p.Z)||excluded[i]||seed[i])continue;
            if(MathF.Abs(p.X-c.X)<=half&&p.Z>=z0&&p.Z<=z1&&p.Y>top+.002f){candidate[i]=true;count++;}
        }
        if(count<MinTop)return Array.Empty<int>();
        // The whole small pieces they belong to (welded by position, joined by triangles).
        var key=new Dictionary<(long,long,long),int>();var rep=new int[n];
        for(int i=0;i<n;i++)
        {
            var p=points[i];var k=((long)MathF.Round(p.X/Weld),(long)MathF.Round(p.Y/Weld),(long)MathF.Round(p.Z/Weld));
            if(!key.TryGetValue(k,out int r)){r=i;key[k]=i;}rep[i]=r;
        }
        var parent=new int[n];for(int i=0;i<n;i++)parent[i]=i;
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],d=triangles[t+2];
            if((uint)a>=(uint)n||(uint)b>=(uint)n||(uint)d>=(uint)n)continue;
            int ra=Find(rep[a]),rb=Find(rep[b]),rd=Find(rep[d]);if(ra!=rb)parent[ra]=rb;rb=Find(rb);if(rb!=rd)parent[rd]=rb;
        }
        var lo=new Dictionary<int,Vector3>();var hi=new Dictionary<int,Vector3>();var bad=new HashSet<int>();var touched=new HashSet<int>();
        for(int i=0;i<n;i++)
        {
            int g=Find(rep[i]);var p=points[i];
            if(!float.IsFinite(p.X+p.Y+p.Z)){bad.Add(g);continue;}
            if(lo.TryGetValue(g,out var l)){lo[g]=Vector3.Min(l,p);hi[g]=Vector3.Max(hi[g],p);}else{lo[g]=p;hi[g]=p;}
            if(excluded[i])bad.Add(g);
            if(candidate[i])touched.Add(g);
        }
        var whole=new HashSet<int>();
        foreach(int g in touched){if(bad.Contains(g))continue;var size=hi[g]-lo[g];if(size.X<=maxSize&&size.Y<=maxSize&&size.Z<=maxSize)whole.Add(g);}
        var list=new List<int>();
        for(int i=0;i<n;i++)if(!seed[i]&&(candidate[i]||whole.Contains(Find(rep[i]))))list.Add(i);
        return list.Count>n/4?Array.Empty<int>():list.ToArray();
    }
}