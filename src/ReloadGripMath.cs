using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
internal sealed class ReloadGripMath
{
    // Kept local (not EquipmentProfile) so the pure math compiles on its own.
    internal static bool RifleMagazine(string profile)=>profile is "ak47" or "sniper" or "m16";
    internal readonly Quaternion Rotation;
    internal readonly Vector3 Position,Tip,Forward,Min,Max;
    private ReloadGripMath(Quaternion q,Vector3 p,Vector3 tip,Vector3 forward,Vector3 min,Vector3 max)
    {Rotation=q;Position=p;Tip=tip;Forward=forward;Min=min;Max=max;}
    internal static ReloadGripMath Fit(string profile,Vector3 min,Vector3 max,Vector3[]? points=null)
    {
        if(profile=="revolver")return Make(Quaternion.Identity,new Vector3(0,-.035f,.065f),new Vector3(0,0,.043f),Vector3.UnitZ,min,max);
        var size=max-min;bool shell=profile is "shotgun" or "crossbow";Vector3 axis=Vector3.UnitY;
        if(shell)axis=size.Z>=size.Y&&size.Z>=size.X?Vector3.UnitZ:size.Y>=size.X?Vector3.UnitY:Vector3.UnitX;
        Quaternion q;
        if(shell)q=axis==Vector3.UnitZ?Quaternion.Identity:axis==Vector3.UnitY?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2):Quaternion.CreateFromAxisAngle(Vector3.UnitY,-MathF.PI/2);
        else q=RifleMagazine(profile)?Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-MathF.PI/2):Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI/2);
        var center=(min+max)*.5f;
        Vector3 anchor;
        var feed=new Vector3(center.X,max.Y,center.Z);
        if(!shell&&points!=null)
        {Vector3 sum=Vector3.Zero;int n=0;foreach(var v in points)if(v.Y>max.Y-Math.Max(.006f,size.Y*.12f)){sum+=v;n++;}
            if(n>0){feed=sum/n;feed.Y=max.Y;}}
        if(shell)
        {
            float lo=Vector3.Dot(min,axis),hi=Vector3.Dot(max,axis);
            anchor=center+axis*(lo+.008f-Vector3.Dot(center,axis));
            // Brass base between thumb/index tips; other fingers stay relaxed.
            var palm=new Vector3(.025f,-.027f,.023f);
            return Make(q,palm-Vector3.Transform(anchor,q),center+axis*(hi-Vector3.Dot(center,axis)),axis,min,max);
        }
        // 0.1.194: the Uzi's magazine (into the grip) is held by its base like a pistol's.
        bool grip=profile is "pistol" or "uzi";
        anchor=grip?new Vector3(center.X,min.Y+.006f,center.Z):new Vector3(center.X,min.Y+size.Y*.32f,center.Z);
        if(RifleMagazine(profile))
        {
            // Grasp the local curved body across the fingers. Its full bounding
            // box includes the distant feed lips and is not the grip thickness.
            float zlo=float.PositiveInfinity,zhi=float.NegativeInfinity;
            if(points!=null)foreach(var v in points)if(Math.Abs(v.Y-anchor.Y)<.045f){zlo=Math.Min(zlo,v.Z);zhi=Math.Max(zhi,v.Z);}
            if(float.IsFinite(zlo)&&float.IsFinite(zhi))anchor.Z=(zlo+zhi)*.5f;
            var fit=Make(q,new Vector3(0,-.032f,.006f)-Vector3.Transform(anchor,q),feed,axis,min,max);
            if(points!=null)fit.MakeSections(points,min,max);
            return fit;
        }
        // +Z is beyond the fingertips, not across the width of the palm.
        // The pistol base is pinched at the distal pads, leaving its entire
        // feed end free. Keep the base within thumb opposition reach; the old
        // 30 mm forward offset left the thumb unable to reach the magazine.
        // The rifle is held on the lower third of its side.
        var offset=grip?new Vector3(.010f,-.035f,.010f):new Vector3(.008f,-.053f,.023f);
        return Make(q,offset-Vector3.Transform(anchor,q),feed,axis,min,max);
    }
    internal bool SegmentClear(Vector3 a,Vector3 b,float padding=.002f)
    {
        if(sections.Count>0)
        {
            foreach(var section in sections)if(!BoxClear(a,b,section.min,section.max,padding))return false;
            return true;
        }
        return BoxClear(a,b,Min,Max,padding);
    }
    private readonly List<(Vector3 min,Vector3 max)> sections=new();
    internal System.Collections.Generic.IReadOnlyList<(Vector3 min,Vector3 max)> Sections=>sections;
    private void MakeSections(Vector3[] points,Vector3 min,Vector3 max)
    {
        const int count=12;
        for(int i=0;i<count;i++)
        {
            float lo=min.Y+(max.Y-min.Y)*i/count,hi=min.Y+(max.Y-min.Y)*(i+1)/count;
            Vector3 a=new(float.PositiveInfinity),b=new(float.NegativeInfinity);int n=0;
            foreach(var v in points)if(v.Y>=lo-.003f&&v.Y<=hi+.003f)
            {var p=Position+Vector3.Transform(v,Rotation);a=Vector3.Min(a,p);b=Vector3.Max(b,p);n++;}
            if(n>0)sections.Add((a,b));
        }
    }
    private static bool BoxClear(Vector3 a,Vector3 b,Vector3 minBound,Vector3 maxBound,float padding)
    {
        // Ammunition remains rigid. This conservative box prevents a finger
        // solving straight through it to an otherwise reachable fingertip.
        var lo=minBound-new Vector3(padding);var hi=maxBound+new Vector3(padding);var d=b-a;
        float enter=0,leave=1;
        for(int i=0;i<3;i++)
        {
            float p=i==0?a.X:i==1?a.Y:a.Z,v=i==0?d.X:i==1?d.Y:d.Z;
            float min=i==0?lo.X:i==1?lo.Y:lo.Z,max=i==0?hi.X:i==1?hi.Y:hi.Z;
            if(Math.Abs(v)<1e-8f){if(p<min||p>max)return true;continue;}
            float t0=(min-p)/v,t1=(max-p)/v;if(t0>t1)(t0,t1)=(t1,t0);
            enter=Math.Max(enter,t0);leave=Math.Min(leave,t1);if(enter>leave)return true;
        }
        return leave<0||enter>1;
    }
    private static ReloadGripMath Make(Quaternion q,Vector3 p,Vector3 tip,Vector3 axis,Vector3 min,Vector3 max)
    {
        Vector3 a=new(float.PositiveInfinity),b=new(float.NegativeInfinity);
        for(int i=0;i<8;i++)
        {
            var v=new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z);
            v=p+Vector3.Transform(v,q);a=Vector3.Min(a,v);b=Vector3.Max(b,v);
        }
        return new ReloadGripMath(q,p,p+Vector3.Transform(tip,q),Vector3.Transform(axis,q),a,b);
    }
}
internal static class ReloadGripGeometry
{
    private static readonly Dictionary<string,ReloadGripMath> grips=new();
    internal static int Revision{get;private set;}
    internal static void Set(string profile,ReloadGripMath grip){grips[profile]=grip;Revision++;}
    internal static ReloadGripMath? Get(string profile)=>grips.TryGetValue(profile,out var grip)?grip:null;
}
