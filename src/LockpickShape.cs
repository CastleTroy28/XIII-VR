using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.104/0.1.105: the native lockpick model also contains the tension wrench
// (a thin bar across or beside the pick). Keep only the pick itself.
//  - pick axis: principal axis of the whole model (the pick is the longest);
//  - a LONG piece belongs to the pick only if it runs along that axis and is
//    centred on it; any other long piece is the wrench;
//  - a short piece (handle ring, cap, hook, wrench band) goes with whichever
//    is nearer: the pick axis or a wrench bar.
internal static class LockpickShape
{
    internal sealed class Piece
    {
        internal int Part;internal List<int> Triangles=new();internal List<Vector3> Points=new();
        internal Vector3 Centroid,Axis;internal float Low,High;
        internal float Length=>High-Low;
        internal Vector3 A=>Centroid+Axis*Low;internal Vector3 B=>Centroid+Axis*High;
    }
    internal static List<int[]?> Filter(IReadOnlyList<(Vector3[] vertices,int[] triangles)> parts,out string report)
    {
        var pieces=new List<Piece>();
        for(int p=0;p<parts.Count;p++)pieces.AddRange(Split(p,parts[p].vertices,parts[p].triangles));
        var result=new List<int[]?>();for(int p=0;p<parts.Count;p++)result.Add(null);
        if(pieces.Count<2){report="single piece kept";return result;}
        var all=new List<Vector3>();foreach(var piece in pieces)all.AddRange(piece.Points);
        Principal(all,out var center,out var axis,out float lo,out float hi);
        float pickLength=hi-lo;if(!(pickLength>1e-6f)){report="degenerate";return result;}
        // The pick's own line: through the longest piece that runs along the axis.
        Piece? main=null;foreach(var piece in pieces)if(Math.Abs(Vector3.Dot(piece.Axis,axis))>.9f&&(main==null||piece.Length>main.Length))main=piece;
        var origin=main!=null?main.Centroid:center;var dir=main!=null&&main.Length>.3f*pickLength?main.Axis:axis;
        float Radial(Vector3 p){var d=p-origin;return (d-dir*Vector3.Dot(d,dir)).Length();}
        var drop=new bool[pieces.Count];var wrenches=new List<Piece>();
        for(int i=0;i<pieces.Count;i++)
        {
            var piece=pieces[i];if(piece.Length<=.2f*pickLength)continue;
            bool along=Math.Abs(Vector3.Dot(piece.Axis,dir))>.9f;
            if(!along||Radial(piece.Centroid)>.06f*pickLength){drop[i]=true;wrenches.Add(piece);}
        }
        if(wrenches.Count>0)
            for(int i=0;i<pieces.Count;i++)
            {
                if(drop[i])continue;var c=pieces[i].Centroid;float toPick=Radial(c);
                foreach(var w in wrenches)if(Segment(c,w.A,w.B)<toPick){drop[i]=true;break;}
            }
        int dropped=0;foreach(bool d in drop)if(d)dropped++;
        var lines=new List<string>();
        for(int i=0;i<pieces.Count&&i<24;i++)
            lines.Add(i+":"+pieces[i].Points.Count+"v L="+(pieces[i].Length/pickLength).ToString("F2")+" dot="+Math.Abs(Vector3.Dot(pieces[i].Axis,dir)).ToString("F2")+" r="+(Radial(pieces[i].Centroid)/pickLength).ToString("F3")+(drop[i]?" DROP":""));
        report=(dropped==0?"no wrench found":"dropped "+dropped+" of "+pieces.Count+" pieces (wrench bars "+wrenches.Count+")")+"; pieces "+string.Join(", ",lines);
        if(dropped==0)return result;
        for(int p=0;p<parts.Count;p++)
        {
            var kept=new List<int>();bool changed=false;
            for(int i=0;i<pieces.Count;i++)if(pieces[i].Part==p){if(!drop[i])kept.AddRange(pieces[i].Triangles);else changed=true;}
            if(changed)result[p]=kept.ToArray();
        }
        return result;
    }
    private static float Segment(Vector3 p,Vector3 a,Vector3 b)
    {
        var ab=b-a;float t=ab.LengthSquared()<1e-12f?0:Math.Clamp(Vector3.Dot(p-a,ab)/ab.LengthSquared(),0,1);
        return (p-(a+ab*t)).Length();
    }
    // Principal axis by power iteration on the covariance.
    internal static void Principal(List<Vector3> points,out Vector3 center,out Vector3 axis,out float low,out float high)
    {
        center=Vector3.Zero;foreach(var p in points)center+=p;center/=Math.Max(1,points.Count);
        float xx=0,xy=0,xz=0,yy=0,yz=0,zz=0;
        foreach(var p in points){var d=p-center;xx+=d.X*d.X;xy+=d.X*d.Y;xz+=d.X*d.Z;yy+=d.Y*d.Y;yz+=d.Y*d.Z;zz+=d.Z*d.Z;}
        // Start from the largest-variance box axis (converges fast, never orthogonal to the answer by accident).
        axis=xx>=yy&&xx>=zz?Vector3.UnitX:yy>=zz?Vector3.UnitY:Vector3.UnitZ;axis=Vector3.Normalize(axis+new Vector3(.01f,.02f,.03f));
        for(int i=0;i<40;i++)
        {
            var next=new Vector3(xx*axis.X+xy*axis.Y+xz*axis.Z,xy*axis.X+yy*axis.Y+yz*axis.Z,xz*axis.X+yz*axis.Y+zz*axis.Z);
            if(next.LengthSquared()<1e-20f)break;axis=Vector3.Normalize(next);
        }
        low=float.MaxValue;high=float.MinValue;
        foreach(var p in points){float t=Vector3.Dot(p-center,axis);low=Math.Min(low,t);high=Math.Max(high,t);}
        if(points.Count==0){low=high=0;}
    }
    // Connected pieces of one mesh; vertices at the same position are welded
    // (UV/normal seams split a mesh into several vertex copies).
    internal static List<Piece> Split(int part,Vector3[] vertices,int[] triangles)
    {
        var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);
        foreach(var v in vertices){min=Vector3.Min(min,v);max=Vector3.Max(max,v);}
        float cell=Math.Max(1e-6f,(max-min).Length()*1e-5f);
        var weld=new Dictionary<(long,long,long),int>();var id=new int[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];var key=((long)Math.Round(v.X/cell),(long)Math.Round(v.Y/cell),(long)Math.Round(v.Z/cell));
            if(!weld.TryGetValue(key,out int w)){w=weld.Count;weld[key]=w;}id[i]=w;
        }
        var parent=new int[weld.Count];for(int i=0;i<parent.Length;i++)parent[i]=i;
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            int a=Find(id[triangles[t]]),b=Find(id[triangles[t+1]]);parent[b]=a;
            int c=Find(id[triangles[t+2]]);parent[c]=Find(a);
        }
        var byRoot=new Dictionary<int,Piece>();var used=new Dictionary<int,HashSet<int>>();
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            int root=Find(id[triangles[t]]);
            if(!byRoot.TryGetValue(root,out var piece)){piece=new Piece{Part=part};byRoot[root]=piece;used[root]=new HashSet<int>();}
            for(int k=0;k<3;k++){int index=triangles[t+k];piece.Triangles.Add(index);if(used[root].Add(id[index]))piece.Points.Add(vertices[index]);}
        }
        foreach(var piece in byRoot.Values){Principal(piece.Points,out piece.Centroid,out piece.Axis,out piece.Low,out piece.High);}
        return new List<Piece>(byRoot.Values);
    }
}
