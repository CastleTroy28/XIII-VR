using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
// Crop a render-only snapshot while preserving source vertex interpolation and UVs.
// No source mesh, arm bone or animation state is modified.
internal sealed class NativeHandMesh
{
    internal const float WristZ=-.065f, Cut=-.055f;
    internal readonly List<Point> Points=new();
    internal readonly List<int[]> Submeshes=new();
    internal readonly struct Point
    {
        internal readonly int A,B,C;
        internal readonly Vector3 Mix,Rest;
        internal readonly Vector2 UV;
        internal readonly bool Cap;
        internal Point(int a,int b,int c,Vector3 mix,Vector3 rest,Vector2 uv,bool cap=false)
        {A=a;B=b;C=c;Mix=mix;Rest=rest;UV=uv;Cap=cap;}
        internal Point AsCap()=>new(A,B,C,Mix,Rest,UV,true);
        internal static Point Lerp(Point a,Point b,float t)=>new(a.A,a.B,a.C,Vector3.Lerp(a.Mix,b.Mix,t),Vector3.Lerp(a.Rest,b.Rest,t),Vector2.Lerp(a.UV,b.UV,t));
    }
    internal static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    internal static NativeHandMesh Build(Vector3[] rest,Vector2[] uv,int[][] triangles,bool[] eligible,float cut=Cut,float radius=.09f)
    {
        if(rest.Length==0 || uv.Length!=rest.Length || eligible.Length!=rest.Length || rest.Any(p=>!Finite(p)))throw new ArgumentException("Invalid native hand input");
        var result=new NativeHandMesh();
        foreach(var indices in triangles)
        {
            var output=new List<int>();var rim=new List<int>();
            for(int i=0;i+2<indices.Length;i+=3)
            {
                int a=indices[i],b=indices[i+1],c=indices[i+2];
                if(a<0||b<0||c<0||a>=rest.Length||b>=rest.Length||c>=rest.Length)throw new ArgumentException("Invalid native triangle index");
                if(!eligible[a]||!eligible[b]||!eligible[c])continue;
                var poly=new List<Point>{new(a,b,c,Vector3.UnitX,rest[a],uv[a]),new(a,b,c,Vector3.UnitY,rest[b],uv[b]),new(a,b,c,Vector3.UnitZ,rest[c],uv[c])};
                foreach(var plane in new[]{new Vector4(0,0,1,-cut),new Vector4(0,0,-1,.23f),new Vector4(1,0,0,radius),new Vector4(-1,0,0,radius),new Vector4(0,1,0,radius),new Vector4(0,-1,0,radius)})
                {
                    if(poly.Count==0)break;var next=new List<Point>();var prev=poly[^1];float dp=Distance(prev.Rest,plane);
                    foreach(var current in poly)
                    {
                        float dc=Distance(current.Rest,plane);
                        if((dc>=0)!=(dp>=0))next.Add(Point.Lerp(prev,current,dp/(dp-dc)));
                        if(dc>=0)next.Add(current);prev=current;dp=dc;
                    }
                    poly=next;
                }
                if(poly.Count<3)continue;
                int start=result.Points.Count;result.Points.AddRange(poly);
                for(int n=0;n<poly.Count;n++)if(Math.Abs(poly[n].Rest.Z-cut)<1e-5f)rim.Add(start+n);
                for(int n=1;n+1<poly.Count;n++)AddTriangle(result,output,start,start+n,start+n+1);
            }
            // Convex wrist cross-section. Duplicate rim vertices to give the cap
            // its own -Z normal while preserving native texture coordinates.
            var unique=new List<int>();
            foreach(int n in rim)if(!unique.Any(k=>Vector3.DistanceSquared(result.Points[k].Rest,result.Points[n].Rest)<1e-10f))unique.Add(n);
            if(unique.Count>=3)
            {
                var center=Vector3.Zero;foreach(int n in unique)center+=result.Points[n].Rest;center/=unique.Count;
                unique.Sort((a,b)=>MathF.Atan2(result.Points[b].Rest.Y-center.Y,result.Points[b].Rest.X-center.X).CompareTo(MathF.Atan2(result.Points[a].Rest.Y-center.Y,result.Points[a].Rest.X-center.X)));
                int start=result.Points.Count;foreach(int n in unique)result.Points.Add(result.Points[n].AsCap());
                for(int n=1;n+1<unique.Count;n++)AddTriangle(result,output,start,start+n,start+n+1);
            }
            result.Submeshes.Add(output.ToArray());
        }
        if(result.Submeshes.Sum(x=>x.Length)<30)throw new InvalidOperationException("Too little native hand geometry after wrist crop");
        result.Compact();
        return result;
    }
    private void Compact()
    {
        var unique=new List<Point>();var indices=new int[Points.Count];
        var lookup=new Dictionary<(int,bool),int>();
        for(int i=0;i<Points.Count;i++)
        {
            var p=Points[i];int source=p.Mix.X==1?p.A:p.Mix.Y==1?p.B:p.Mix.Z==1?p.C:-1;
            if(source>=0&&lookup.TryGetValue((source,p.Cap),out int found)){indices[i]=found;continue;}
            indices[i]=unique.Count;unique.Add(p);if(source>=0)lookup[(source,p.Cap)]=indices[i];
        }
        foreach(var sub in Submeshes)for(int i=0;i<sub.Length;i++)sub[i]=indices[sub[i]];
        Points.Clear();Points.AddRange(unique);
    }
    private static float Distance(Vector3 p,Vector4 plane)=>p.X*plane.X+p.Y*plane.Y+p.Z*plane.Z+plane.W;
    private static void AddTriangle(NativeHandMesh m,List<int> indices,int a,int b,int c)
    {
        if(Vector3.Cross(m.Points[b].Rest-m.Points[a].Rest,m.Points[c].Rest-m.Points[a].Rest).LengthSquared()<1e-16f)return;
        indices.Add(a);indices.Add(b);indices.Add(c);
    }
    internal static Vector3 Evaluate(Point p,Vector3[] animated)
    {
        var pose=animated[p.A]*p.Mix.X+animated[p.B]*p.Mix.Y+animated[p.C]*p.Mix.Z;
        if(!Finite(pose)||pose.Length()>.4f)throw new InvalidOperationException("Native hand deformation out of bounds");
        // Freeze the short cuff in its rest shape. Only fingers/palm inherit the
        // game's internal hand animation, never its elbow or wrist translation.
        float t=Math.Clamp((p.Rest.Z+.008f)/.032f,0,1);t=t*t*(3-2*t);
        return Vector3.Lerp(p.Rest,pose,t)+new Vector3(0,0,WristZ);
    }
    internal static bool[] ConnectedOwnership(Vector3[] meshVertices,int[][] triangles,Vector3 wrist,Vector3 other)
    {
        // Fallback when CPU bone weights were stripped. Distinct native arm
        // mesh islands are assigned as wholes, preventing the other hand from
        // being copied merely because the player has brought the hands together.
        int[] parent=Enumerable.Range(0,meshVertices.Length).ToArray();
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        // UV/normal seams duplicate indices at the same surface point. Weld
        // connectivity only; original UVs, normals and render vertices stay intact.
        var welded=new Dictionary<(int,int,int),int>();
        for(int i=0;i<meshVertices.Length;i++)
        {
            var p=meshVertices[i];var key=((int)MathF.Round(p.X*100000),(int)MathF.Round(p.Y*100000),(int)MathF.Round(p.Z*100000));
            if(welded.TryGetValue(key,out int otherIndex))parent[Find(i)]=Find(otherIndex);else welded[key]=i;
        }
        foreach(var sub in triangles)for(int i=0;i+2<sub.Length;i+=3){parent[Find(sub[i+1])]=Find(sub[i]);parent[Find(sub[i+2])]=Find(sub[i]);}
        var distances=new Dictionary<int,(float own,float other)>();
        for(int i=0;i<parent.Length;i++)
        {
            int key=Find(i);if(!distances.TryGetValue(key,out var d))d=(float.PositiveInfinity,float.PositiveInfinity);
            d.own=Math.Min(d.own,Vector3.DistanceSquared(meshVertices[i],wrist));d.other=Math.Min(d.other,Vector3.DistanceSquared(meshVertices[i],other));distances[key]=d;
        }
        if(distances.Count<2)throw new InvalidOperationException("Arm mesh is connected and has no readable bone weights; cannot safely separate hands");
        return Enumerable.Range(0,parent.Length).Select(i=>distances[Find(i)].own<distances[Find(i)].other).ToArray();
    }
}
