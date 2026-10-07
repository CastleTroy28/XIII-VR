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
    // 0.1.245: where the forearm is cut (rest frame: the wrist joint at 0,
    // the elbow toward -Z). With the forearms shown: 2.5 cm short of the elbow
    // (as before). The hands only (VR SETTINGS "Forearms"): just behind the
    // watch - its band's middle `mount` back from the wrist and some 3 cm wide
    // with its case - at 8.5 cm at the least.
    internal const float HandsOnlyCut=-.085f,BehindWatch=.032f;
    internal static float ForearmCut(float elbowZ,float mount,bool handsOnly)
    {
        float longCut=Math.Clamp(float.IsFinite(elbowZ)?elbowZ-.025f:-.25f,-.35f,-.16f);
        if(!handsOnly)return longCut;
        float m=float.IsFinite(mount)&&mount>0?mount:.052f;
        return Math.Max(longCut,Math.Min(HandsOnlyCut,-(m+BehindWatch)));
    }
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
        internal Point AsCap(Vector2 uv)=>new(A,B,C,Mix,Rest,uv,true);
        internal static Point Lerp(Point a,Point b,float t)=>new(a.A,a.B,a.C,Vector3.Lerp(a.Mix,b.Mix,t),Vector3.Lerp(a.Rest,b.Rest,t),Vector2.Lerp(a.UV,b.UV,t));
    }
    internal static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    internal static NativeHandMesh Build(Vector3[] rest,Vector2[] uv,int[][] triangles,bool[] eligible,float cut=Cut,float radius=.09f)
    {
        if(rest.Length==0 || uv.Length!=rest.Length || eligible.Length!=rest.Length || rest.Any(p=>!Finite(p)))throw new ArgumentException("Invalid native hand input");
        var result=new NativeHandMesh();
        var outputs=new List<List<int>>();var rims=new List<List<int>>();
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
            outputs.Add(output);rims.Add(rim);
        }
        result.CloseCut(outputs,rims);
        foreach(var output in outputs)result.Submeshes.Add(output.ToArray());
        if(result.Submeshes.Sum(x=>x.Length)<30)throw new InvalidOperationException("Too little native hand geometry after wrist crop");
        result.Compact();
        return result;
    }
    // The rim point whose texture point and colour the whole cap has (null: no cap).
    internal Point? CapSource { get; private set; }
    // 0.1.250: the cut closed by one flat cap over the whole cross-section (the
    // outline around every point cut, of every material), in the material cut
    // most, one texture point all over it (that of the cut's outer edge). The
    // cap was a fan of the rim points sorted around their middle, with their
    // own texture points, per material: through a sleeve of layers (an outer
    // cloth and its lining or a glove's cuff, as on the military outfit) the
    // inner and outer points alternated - a jagged star, its inward triangles
    // turned away (gaps) and the texture smeared across its whole atlas between
    // them (streaks).
    private void CloseCut(List<List<int>> outputs,List<List<int>> rims)
    {
        int most=-1;for(int i=0;i<rims.Count;i++)if(rims[i].Count>=3&&(most<0||rims[i].Count>rims[most].Count))most=i;
        if(most<0)return;
        var unique=new List<int>();
        foreach(var rim in rims)foreach(int n in rim)if(!unique.Any(k=>Vector3.DistanceSquared(Points[k].Rest,Points[n].Rest)<1e-10f))unique.Add(n);
        var outline=Outline(unique.Select(n=>Points[n].Rest).ToArray());
        if(outline.Count<3)return;
        var own=new HashSet<int>(rims[most]);
        var edge=outline.Select(i=>unique[i]).Where(own.Contains).ToList();
        var source=Points[CapPoint(edge.Count>0?edge:rims[most],Points)];
        CapSource=source;
        int start=Points.Count;
        // Around clockwise, seen from the hand (the order the cap always had: its face toward the elbow).
        for(int i=outline.Count-1;i>=0;i--)Points.Add(Points[unique[outline[i]]].AsCap(source.UV));
        for(int n=1;n+1<outline.Count;n++)AddTriangle(this,outputs[most],start,start+n,start+n+1);
    }
    // The convex outline of points across the cut (their X and Y), counter-clockwise; indices into points.
    internal static List<int> Outline(Vector3[] points)
    {
        var order=Enumerable.Range(0,points.Length).Where(i=>Finite(points[i])).OrderBy(i=>points[i].X).ThenBy(i=>points[i].Y).ToArray();
        var hull=new List<int>();
        if(order.Length<3)return hull;
        // A left turn of more than 1e-4 radians (points along an edge, within rounding of it, are left out).
        bool Left(int o,int a,int b)
        {
            float ax=points[a].X-points[o].X,ay=points[a].Y-points[o].Y,bx=points[b].X-points[o].X,by=points[b].Y-points[o].Y;
            return ax*by-ay*bx>1e-4f*MathF.Sqrt((ax*ax+ay*ay)*(bx*bx+by*by));
        }
        foreach(int i in order){while(hull.Count>=2&&!Left(hull[^2],hull[^1],i))hull.RemoveAt(hull.Count-1);hull.Add(i);}
        int lower=hull.Count+1;
        for(int k=order.Length-2;k>=0;k--){int i=order[k];while(hull.Count>=lower&&!Left(hull[^2],hull[^1],i))hull.RemoveAt(hull.Count-1);hull.Add(i);}
        hull.RemoveAt(hull.Count-1);
        return hull.Count>=3?hull:new List<int>();
    }
    // The point whose texture point is nearest the middle (median) of theirs: on the cloth most of the edge shows.
    internal static int CapPoint(IReadOnlyList<int> candidates,IReadOnlyList<Point> points)
    {
        float Median(IEnumerable<float> v){var s=v.OrderBy(x=>x).ToArray();return s.Length==0?0:s.Length%2==1?s[s.Length/2]:(s[s.Length/2-1]+s[s.Length/2])*.5f;}
        var middle=new Vector2(Median(candidates.Select(i=>points[i].UV.X)),Median(candidates.Select(i=>points[i].UV.Y)));
        int best=candidates[0];float nearest=float.PositiveInfinity;
        foreach(int i in candidates){float d=Vector2.DistanceSquared(points[i].UV,middle);if(d<nearest){nearest=d;best=i;}}
        return best;
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
