using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.198: the
// hook's handle - its rubber grip, a thick round bar at the far end of its
// frame - is found in its mesh: of the mesh's separate pieces (points welded
// by position, joined by triangles) the thickest long round one of a handle's
// size for the whole hook. The hand holds that bar as it holds a pistol's grip,
// the rest of the hook pointing forward from the fist (as a pistol's barrel).
internal static class GripBarMath
{
    internal readonly struct Bar
    {
        internal readonly Vector3 Center,Axis,Toward;internal readonly float Length,Thickness;
        internal Bar(Vector3 center,Vector3 axis,Vector3 toward,float length,float thickness){Center=center;Axis=axis;Toward=toward;Length=length;Thickness=thickness;}
    }
    internal const float Weld=1e-4f;
    // points/triangles: the whole hook (one frame). Null: no such bar.
    // groups: the triangles of each of its materials (submeshes), optional: a
    // rubber grip of its own material is a piece of its own even where it
    // touches the metal frame (points at the same place are welded).
    internal static Bar? Find(Vector3[] points,int[] triangles,IReadOnlyList<int[]>? groups=null)
    {
        int n=points.Length;if(n<12)return null;
        var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);var sum=Vector3.Zero;int count=0;
        foreach(var p in points){if(!Finite(p))continue;lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);sum+=p;count++;}
        if(count<12)return null;
        var size=hi-lo;float whole=Math.Max(size.X,Math.Max(size.Y,size.Z));if(!(whole>1e-5f))return null;
        var middle=sum/count;
        Bar? best=null;float bestScore=0;
        foreach(var piece in Candidates(points,triangles,groups))
        {
            if(piece.Count<12)continue;
            if(!Shape(points,piece,out var center,out var axis,out float length,out float wide,out float narrow))continue;
            if(!Handle(length,wide,narrow,whole))continue;
            float score=length*wide*wide;
            if(score<=bestScore)continue;
            var toMiddle=middle-center;var toward=toMiddle-axis*Vector3.Dot(toMiddle,axis);
            if(!Beside(toMiddle,axis))continue;
            bestScore=score;best=new Bar(center,Signed(axis),Vector3.Normalize(toward),length,wide);
        }
        return best;
    }
    // A handle: long and round, from a fifth to most of the hook's size, a fist thick.
    // 0.1.198 (the game's hook, screenshots): its frame's arms are
    // flat bars (3.1 x 1.9 cm, as long as the grip and thicker) and one of
    // them was taken for the handle; the rubber grip is round (as thick
    // across both ways, at least 4/5).
    internal const float Round=.8f,Side=.7f;
    internal static bool Handle(float length,float wide,float narrow,float whole)=>
        length>=whole*.18f&&length<=whole*.85f&&wide>=whole*.04f&&wide<=whole*.22f&&narrow>=wide*Round&&length>=wide*2.2f;
    // The rest of the hook is beside the handle (as a pistol's barrel is
    // beside its grip, not along it): the way from the handle's middle to the
    // hook's middle mostly square to the handle (a frame's arm, the shaft to
    // the claw point along it).
    internal static bool Beside(Vector3 toMiddle,Vector3 axis)
    {
        float all=toMiddle.Length();if(!(all>1e-5f)||!float.IsFinite(all))return false;
        var across=toMiddle-axis*Vector3.Dot(toMiddle,axis);
        return across.Length()>=all*Side;
    }
    // The axis's sign is the mesh's own, not the spread's: it points along
    // the mesh axis it is nearest to (+X, +Y or +Z), the same for every load.
    internal static Vector3 Signed(Vector3 axis)
    {
        float ax=Math.Abs(axis.X),ay=Math.Abs(axis.Y),az=Math.Abs(axis.Z);
        float main=ax>=ay&&ax>=az?axis.X:ay>=az?axis.Y:axis.Z;
        return main<0?-axis:axis;
    }
    // The hook's pieces: of the whole mesh, then of each of its materials.
    internal static List<List<int>> Candidates(Vector3[] points,int[] triangles,IReadOnlyList<int[]>? groups)
    {
        var all=Pieces(points,triangles);
        if(groups==null||groups.Count<2)return all;
        foreach(var g in groups)
        {
            if(g==null||g.Length<3)continue;
            var used=new bool[points.Length];foreach(int i in g)if((uint)i<(uint)points.Length)used[i]=true;
            foreach(var piece in Pieces(points,g)){var own=piece.FindAll(i=>used[i]);if(own.Count>=12)all.Add(own);}
        }
        return all;
    }
    // For the log: the biggest pieces' sizes (length x wide x narrow, m).
    internal static string Report(Vector3[] points,int[] triangles,IReadOnlyList<int[]>? groups,int most=6)
    {
        var sizes=new List<(int count,float length,float wide,float narrow)>();
        foreach(var piece in Candidates(points,triangles,groups))
        {
            if(piece.Count<12||!Shape(points,piece,out _,out _,out float length,out float wide,out float narrow))continue;
            sizes.Add((piece.Count,length,wide,narrow));
        }
        sizes.Sort((a,b)=>b.count.CompareTo(a.count));
        var parts=new List<string>();var inv=System.Globalization.CultureInfo.InvariantCulture;
        for(int i=0;i<sizes.Count&&i<most;i++)parts.Add(sizes[i].count+"v "+sizes[i].length.ToString("F3",inv)+"x"+sizes[i].wide.ToString("F3",inv)+"x"+sizes[i].narrow.ToString("F3",inv));
        return sizes.Count+" pieces"+(parts.Count>0?" ("+string.Join(", ",parts)+")":"");
    }
    // A gadget is fitted to 20 cm (its longest side); the hook held by its
    // handle is drawn at its own size again (fitted: the fit's scale), its
    // longest side at most 40 cm and at least 10 cm.
    internal const float FittedLength=.20f,MaxLength=.40f,MinLength=.10f;
    internal static float TrueScale(float fitted)=>float.IsFinite(fitted)&&fitted>1e-4f?Math.Clamp(1/fitted,MinLength/FittedLength,MaxLength/FittedLength):1;
    // The bar of a hook drawn `scale` times its fitted size.
    internal static Bar Scaled(Bar bar,float scale)=>new(bar.Center*scale,bar.Axis,bar.Toward,bar.Length*scale,bar.Thickness*scale);
    // A piece's long axis (its points' main spread), its middle and its sizes along and across.
    internal static bool Shape(Vector3[] points,List<int> piece,out Vector3 center,out Vector3 axis,out float length,out float wide,out float narrow)
    {
        center=axis=Vector3.Zero;length=wide=narrow=0;
        var mean=Vector3.Zero;int count=0;foreach(int i in piece){var p=points[i];if(!Finite(p))continue;mean+=p;count++;}
        if(count<3)return false;mean/=count;
        float xx=0,xy=0,xz=0,yy=0,yz=0,zz=0;
        foreach(int i in piece){var d=points[i]-mean;if(!Finite(d))continue;xx+=d.X*d.X;xy+=d.X*d.Y;xz+=d.X*d.Z;yy+=d.Y*d.Y;yz+=d.Y*d.Z;zz+=d.Z*d.Z;}
        Vector3 Apply(Vector3 v)=>new(xx*v.X+xy*v.Y+xz*v.Z,xy*v.X+yy*v.Y+yz*v.Z,xz*v.X+yz*v.Y+zz*v.Z);
        var e1=Principal(Apply,new Vector3(.577f,.577f,.578f),null);if(e1==null)return false;
        var seed=Math.Abs(e1.Value.X)<.9f?Vector3.UnitX:Vector3.UnitY;seed-=e1.Value*Vector3.Dot(seed,e1.Value);
        var e2=Principal(v=>{var a=Apply(v);return a-e1.Value*Vector3.Dot(a,e1.Value);},Vector3.Normalize(seed),e1);if(e2==null)return false;
        var e3=Vector3.Normalize(Vector3.Cross(e1.Value,e2.Value));
        float a0=float.MaxValue,a1=float.MinValue,b0=float.MaxValue,b1=float.MinValue,c0=float.MaxValue,c1=float.MinValue;
        foreach(int i in piece)
        {
            var d=points[i]-mean;if(!Finite(d))continue;
            float a=Vector3.Dot(d,e1.Value),b=Vector3.Dot(d,e2.Value),c=Vector3.Dot(d,e3);
            a0=Math.Min(a0,a);a1=Math.Max(a1,a);b0=Math.Min(b0,b);b1=Math.Max(b1,b);c0=Math.Min(c0,c);c1=Math.Max(c1,c);
        }
        axis=e1.Value;length=a1-a0;float w2=b1-b0,w3=c1-c0;wide=Math.Max(w2,w3);narrow=Math.Min(w2,w3);
        center=mean+e1.Value*((a0+a1)*.5f)+e2.Value*((b0+b1)*.5f)+e3*((c0+c1)*.5f);
        return length>0&&Finite(center);
    }
    private static Vector3? Principal(Func<Vector3,Vector3> apply,Vector3 start,Vector3? orthogonal)
    {
        var v=start;
        for(int k=0;k<60;k++)
        {
            var w=apply(v);if(orthogonal is Vector3 o)w-=o*Vector3.Dot(w,o);
            float len=w.Length();if(!(len>1e-12f)||!float.IsFinite(len))return k==0?null:v;
            v=w/len;
        }
        return Finite(v)?v:null;
    }
    internal static List<List<int>> Pieces(Vector3[] points,int[] triangles)
    {
        int n=points.Length;var key=new Dictionary<(long,long,long),int>();var rep=new int[n];
        for(int i=0;i<n;i++)
        {
            var p=points[i];if(!Finite(p)){rep[i]=i;continue;}
            var k=((long)MathF.Round(p.X/Weld),(long)MathF.Round(p.Y/Weld),(long)MathF.Round(p.Z/Weld));
            if(!key.TryGetValue(k,out int r)){r=i;key[k]=i;}rep[i]=r;
        }
        var parent=new int[n];for(int i=0;i<n;i++)parent[i]=i;
        int Find(int x){while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
        void Join(int a,int b){a=Find(rep[a]);b=Find(rep[b]);if(a!=b)parent[a]=b;}
        for(int t=0;t+2<triangles.Length;t+=3)
        {
            int a=triangles[t],b=triangles[t+1],c=triangles[t+2];
            if((uint)a>=(uint)n||(uint)b>=(uint)n||(uint)c>=(uint)n)continue;
            Join(a,b);Join(b,c);
        }
        var groups=new Dictionary<int,List<int>>();
        for(int i=0;i<n;i++){int g=Find(rep[i]);if(!groups.TryGetValue(g,out var list))groups[g]=list=new List<int>();list.Add(i);}
        return new List<List<int>>(groups.Values);
    }
    // The hook in a hand's frame (the frame its pistol hold is given in): the
    // bar along the pistol grip's line `grip`, the rest of the hook along
    // `forward`, the bar's middle at `fist`. Returns the hook's turn and place.
    internal static (Quaternion rotation,Vector3 position) Hold(Bar bar,Vector3 grip,Vector3 forward,Vector3 fist)
    {
        var from=Basis(bar.Axis,bar.Toward);var to=Basis(grip,forward);
        var q=Quaternion.Normalize(to*Quaternion.Inverse(from));
        return (q,fist-Vector3.Transform(bar.Center,q));
    }
    // The turn taking the unit axes (x, y, z) to (a, b, a×b orthogonalised).
    private static Quaternion Basis(Vector3 a,Vector3 b)
    {
        a=Vector3.Normalize(a);b=Vector3.Normalize(b-a*Vector3.Dot(b,a));var c=Vector3.Cross(a,b);
        var m=new Matrix4x4(a.X,a.Y,a.Z,0,b.X,b.Y,b.Z,0,c.X,c.Y,c.Z,0,0,0,0,1);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
