using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
internal readonly record struct ContactSphere(Vector3 Offset,float Radius,bool Ammunition=false);
internal readonly record struct ContactPose(Vector3 Position,Quaternion Rotation)
{
    internal Vector3 Point(Vector3 p)=>Position+Vector3.Transform(p,Rotation);
    internal static ContactPose Blend(ContactPose a,ContactPose b,float t)=>new(Vector3.Lerp(a.Position,b.Position,t),Quaternion.Slerp(a.Rotation,b.Rotation,t));
}
internal interface IContactWorld
{
    void BeginSolve() {}
    void EndSolve() {}
    void Probe(bool ammunition) {}
    // 0.1.121: true only when nothing solid is inside this sphere.
    bool Clear(Vector3 center,float radius)=>false;
    float Sweep(Vector3 start,Vector3 end,float radius,out Vector3 normal);
    bool Overlap(Vector3 center,float radius,out Vector3 correction);
}
// Swept kinematic volumes, no forces or colliders attached to the character.
internal sealed class ContactSolver
{
    private bool ready;
    private ContactPose previous;
    private int blockedSamples;
    internal bool Blocked {get;private set;}
    internal bool Safe {get;private set;}
    internal bool Recovered {get;private set;}
    internal void Reset(){ready=false;Safe=false;Blocked=Recovered=false;blockedSamples=0;}
    // 0.1.122: a hand resting against the held gun moves with the gun: its
    // last pose is carried by the gun's own motion before the next solve, so
    // the gun's movement no longer knocks the hand about (it shook).
    internal bool Ready=>ready;
    internal ContactPose Previous=>previous;
    internal void Carry(ContactPose from,ContactPose to)
    {
        if(!ready||!Finite(from.Position)||!Finite(to.Position)||from.Rotation.LengthSquared()<.5f||to.Rotation.LengthSquared()<.5f)return;
        var delta=Quaternion.Normalize(to.Rotation*Quaternion.Inverse(from.Rotation));
        var moved=new ContactPose(to.Position+Vector3.Transform(previous.Position-from.Position,delta),Quaternion.Normalize(delta*previous.Rotation));
        if(Finite(moved.Position)&&Vector3.Distance(moved.Position,previous.Position)<.5f)previous=moved;
    }
    // 0.1.124: a hand touching the held gun, steadied in the gun's frame:
    // the gun's own motion is followed exactly; the hand's place on the gun
    // is smoothed (jitter of a few millimetres - the hand shook against the
    // gun's collision cells - is filtered away, a deliberate move of several
    // centimetres follows almost at once).
    internal const float SteadySlow=.12f,SteadyFast=.02f,SteadyStill=.01f,SteadyMove=.03f;
    internal static ContactPose Steady(ContactPose gun,ContactPose hand,ref ContactPose relative,ref bool valid,float dt)
    {
        var inverse=Quaternion.Inverse(gun.Rotation);
        var rel=new ContactPose(Vector3.Transform(hand.Position-gun.Position,inverse),Quaternion.Normalize(inverse*hand.Rotation));
        if(valid&&float.IsFinite(dt)&&Finite(relative.Position))
        {
            var prev=relative;float move=Vector3.Distance(rel.Position,prev.Position);
            if(move<.25f)
            {
                float tau=SteadySlow+(SteadyFast-SteadySlow)*Math.Clamp((move-SteadyStill)/SteadyMove,0,1);
                float k=dt<=0?0:1-MathF.Exp(-dt/tau);
                rel=ContactPose.Blend(prev,rel,k);
            }
        }
        if(!Finite(rel.Position)||rel.Rotation.LengthSquared()<.5f){valid=false;return hand;}
        relative=rel;valid=true;
        return new ContactPose(gun.Position+Vector3.Transform(rel.Position,gun.Rotation),Quaternion.Normalize(gun.Rotation*rel.Rotation));
    }
    // Whether two shapes are within a margin of touching.
    internal static bool Near(ContactSphere[] a,ContactPose pa,ContactSphere[] b,ContactPose pb,float margin)
    {
        foreach(var s in a){var p=pa.Point(s.Offset);foreach(var t in b)if(Vector3.Distance(p,pb.Point(t.Offset))<s.Radius+t.Radius+margin)return true;}
        return false;
    }
    internal ContactPose Solve(ContactPose desired,ContactPose seed,ContactSphere[] shape,IContactWorld world)
    {
        world.BeginSolve();
        try
        {
        Blocked=Recovered=false;Safe=true;
        if(!Finite(desired.Position)||!float.IsFinite(desired.Rotation.LengthSquared())||desired.Rotation.LengthSquared()<.5f||!Finite(seed.Position)||!float.IsFinite(seed.Rotation.LengthSquared())||seed.Rotation.LengthSquared()<.5f){Safe=false;return previous;}
        if(!ready || Vector3.Distance(previous.Position,desired.Position)>3){previous=seed;ready=true;}
        // 0.1.121: one query around the whole shape at the last and the new
        // pose (every point of the move and of the turn lies inside it). With
        // nothing solid there the tracked pose stands, without the per-sphere
        // overlap, sweep and turn queries (a gun of 96 cells made several
        // hundred physics queries a call).
        if(Bound(shape,previous,desired,out var middle,out float reach)&&world.Clear(middle,reach))
        {blockedSamples=0;previous=desired;return desired;}
        var pose=previous;
        // Moving doors/props and initially overlapping objects need separation.
        for(int pass=0;pass<=4;pass++)
        {
            Vector3 correction=Vector3.Zero;float largest=0;
            foreach(var s in shape)
            {
                world.Probe(s.Ammunition);
                if(!world.Overlap(pose.Point(s.Offset),s.Radius,out var c))continue;
                if(!Finite(c)){Safe=false;return previous;}
                if(c.LengthSquared()>largest){correction=c;largest=c.LengthSquared();}
            }
            if(largest<1e-10f)break;
            Blocked=true;if(pass==4||largest>.25f){Safe=false;ready=false;return previous;}
            pose=new ContactPose(pose.Position+correction,pose.Rotation);
        }
        // Translation and rotation have separate contacts. A blocked fingertip
        // rotation must not discard a valid withdrawal through a doorway.
        var remaining=desired.Position-pose.Position;
        var firstNormal=Vector3.Zero;
        for(int pass=0;pass<4&&remaining.LengthSquared()>1e-10f;pass++)
        {
            float f=1;var normal=Vector3.Zero;
            foreach(var s in shape)
            {
                world.Probe(s.Ammunition);
                float hit=world.Sweep(pose.Point(s.Offset),pose.Point(s.Offset)+remaining,s.Radius,out var n);
                if(!float.IsFinite(hit)){Safe=false;return previous;}
                if(hit<f){f=Math.Clamp(hit,0,1);normal=n;}
            }
            pose=new ContactPose(pass==0&&f>=1?desired.Position:pose.Position+remaining*f,pose.Rotation);
            if(f>.99999f)break;
            Blocked=true;remaining*=1-f;
            if(normal.LengthSquared()<.5f)break;
            var slide=remaining-normal*Math.Min(0,Vector3.Dot(remaining,normal));
            // At two faces slide along their crease, not back into the first.
            if(firstNormal.LengthSquared()>.5f&&Vector3.Dot(slide,firstNormal)<-1e-6f)
            {
                var crease=Vector3.Cross(firstNormal,normal);
                slide=crease.LengthSquared()>1e-8f?Vector3.Normalize(crease)*Vector3.Dot(slide,Vector3.Normalize(crease)):Vector3.Zero;
            }
            if(pass==0)firstNormal=normal;
            if(f<.00001f&&(slide-remaining).LengthSquared()<1e-12f)break;
            remaining=slide;
        }
        float angle=2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(pose.Rotation,desired.Rotation)),0,1));
        int steps=Math.Clamp((int)MathF.Ceiling(angle/.10f),1,32);
        var start=pose;var rotatedGoal=new ContactPose(pose.Position,desired.Rotation);
        for(int step=1;step<=steps;step++)
        {
            var goal=ContactPose.Blend(start,rotatedGoal,step/(float)steps);
            float fraction=1;
            foreach(var s in shape)
            {
                world.Probe(s.Ammunition);
                float f=world.Sweep(pose.Point(s.Offset),goal.Point(s.Offset),s.Radius,out _);
                if(!float.IsFinite(f)){Safe=false;return previous;}
                if(f<fraction)fraction=Math.Clamp(f,0,1);
            }
            pose=ContactPose.Blend(pose,goal,fraction);
            if(fraction<.99999f)
            {
                Blocked=true;
                break;
            }
        }
        bool separated=Vector3.Distance(pose.Position,desired.Position)>.22f;
        blockedSamples=Blocked&&separated?blockedSamples+1:0;
        // Walking through an open doorway can leave the old world-space hand
        // on the far side of its frame. Recover only with an independently
        // verified clear path from the current body to the tracked destination.
        if(blockedSamples>=8&&CanRecover(desired,seed,shape,world))
        {pose=desired;Blocked=false;Recovered=true;blockedSamples=0;}
        previous=pose;return pose;
            }
        finally{world.EndSolve();}
    }
    // A sphere holding every sphere of the shape at both poses and on the way
    // between them (a turn keeps each sphere at its distance from the pose).
    internal static bool Bound(ContactSphere[] shape,ContactPose a,ContactPose b,out Vector3 center,out float radius)
    {
        center=(a.Position+b.Position)*.5f;radius=0;
        if(shape.Length==0)return false;
        foreach(var s in shape)radius=Math.Max(radius,s.Offset.Length()+s.Radius);
        radius+=Vector3.Distance(a.Position,b.Position)*.5f+.01f;
        return Finite(center)&&float.IsFinite(radius);
    }
    private static bool CanRecover(ContactPose target,ContactPose seed,ContactSphere[] shape,IContactWorld world)
    {
        foreach(var s in shape)
        {
            world.Probe(s.Ammunition);
            var a=seed.Point(s.Offset);var b=target.Point(s.Offset);
            if(world.Overlap(a,s.Radius,out _)||world.Overlap(b,s.Radius,out _))return false;
            float f=world.Sweep(a,b,s.Radius,out _);if(!float.IsFinite(f)||f<.99999f)return false;
        }
        return true;
    }
    internal static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    internal static bool IntoSurface(Vector3 movement,Vector3 outward)=>Vector3.Dot(movement,outward)<-1e-6f;
    internal static bool SphereSweep(Vector3 start,Vector3 end,Vector3 center,float radius,out float t,out Vector3 normal)
    {
        var d=end-start;var m=start-center;float a=d.LengthSquared(),b=Vector3.Dot(m,d),c=m.LengthSquared()-radius*radius;
        t=1;normal=Vector3.Zero;if(a<1e-12f)return false;
        if(c<=0){if(b>=0)return false;t=0;normal=m.LengthSquared()>1e-10f?Vector3.Normalize(m):-Vector3.Normalize(d);return true;}
        float discriminant=b*b-a*c;if(b>=0||discriminant<0)return false;
        t=(-b-MathF.Sqrt(discriminant))/a;if(t<0||t>1)return false;
        normal=Vector3.Normalize(start+d*t-center);return true;
    }
    internal static ContactSphere[] Weapon(string profile)
    {
        float length=profile=="pistol"?.22f:profile=="shotgun"?.95f:.85f;
        float end=profile=="pistol"?.14f:profile=="shotgun"?.62f:.50f;
        float radius=profile=="pistol"?.017f:.045f;float spacing=profile=="pistol"?.026f:.065f;int count=(int)MathF.Ceiling(length/spacing)+1;
        var result=new ContactSphere[count+1];
        for(int i=0;i<count;i++)result[i]=new(new Vector3(0,.04f,end-length+length*i/(count-1)),radius);
        result[count]=new(new Vector3(0,-.025f,0),profile=="pistol"?.022f:.052f);return result;
    }
    internal static ContactSphere[] Box(Vector3 min,Vector3 max)
    {
        var size=max-min;
        int nx=Math.Clamp((int)MathF.Ceiling(size.X/.16f),1,2),ny=Math.Clamp((int)MathF.Ceiling(size.Y/.16f),1,2),nz=Math.Clamp((int)MathF.Ceiling(size.Z/.16f),1,6);
        var step=new Vector3(size.X/nx,size.Y/ny,size.Z/nz);float radius=step.Length()*.5f+.002f;
        var result=new ContactSphere[nx*ny*nz];int n=0;
        for(int x=0;x<nx;x++)for(int y=0;y<ny;y++)for(int z=0;z<nz;z++)result[n++]=new ContactSphere(min+new Vector3((x+.5f)*step.X,(y+.5f)*step.Y,(z+.5f)*step.Z),radius);
        return result;
    }
    // 0.1.117: occupied cells of a point cloud (fitted mesh vertices), the
    // cell grown by 18% until at most maxCells remain; one sphere per cell
    // (radius 0.72 cell). Null when there are no finite points.
    internal static ContactSphere[]? Cells(IReadOnlyList<Vector3> points,float start,int maxCells,out float cell)
    {
        cell=start;
        if(points.Count==0||!(start>0)||maxCells<1)return null;
        var cells=new Dictionary<(int,int,int),Vector3>();
        for(int pass=0;pass<24;pass++)
        {
            cells.Clear();
            foreach(var p in points)
            {
                if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
                var key=((int)MathF.Floor(p.X/cell),(int)MathF.Floor(p.Y/cell),(int)MathF.Floor(p.Z/cell));
                if(!cells.ContainsKey(key))cells[key]=new Vector3((key.Item1+.5f)*cell,(key.Item2+.5f)*cell,(key.Item3+.5f)*cell);
            }
            if(cells.Count<=maxCells)break;
            cell*=1.18f;
        }
        if(cells.Count==0)return null;
        // 0.1.146: a cell only partly filled (a thin
        // barrel, a sight) gets the sphere around its own points (their
        // middle, out to the farthest one), not the whole cell's.
        var sum=new Dictionary<(int,int,int),(Vector3 sum,int count)>();
        foreach(var p in points)
        {
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
            var key=((int)MathF.Floor(p.X/cell),(int)MathF.Floor(p.Y/cell),(int)MathF.Floor(p.Z/cell));
            sum.TryGetValue(key,out var s);sum[key]=(s.sum+p,s.count+1);
        }
        var reach=new Dictionary<(int,int,int),float>();
        foreach(var p in points)
        {
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
            var key=((int)MathF.Floor(p.X/cell),(int)MathF.Floor(p.Y/cell),(int)MathF.Floor(p.Z/cell));
            var s=sum[key];float d=Vector3.Distance(p,s.sum/s.count);
            reach.TryGetValue(key,out var r);if(d>r)reach[key]=d;
        }
        var result=new ContactSphere[cells.Count];int n=0;float full=cell*.72f;
        foreach(var pair in cells)
        {
            var s=sum[pair.Key];reach.TryGetValue(pair.Key,out var r);
            result[n++]=r+MinCellRadius<full?new ContactSphere(s.sum/s.count,r+MinCellRadius):new ContactSphere(pair.Value,full);
        }
        return result;
    }
    // A cell's own points are covered with this much to spare (the surface
    // between two vertices).
    internal const float MinCellRadius=.004f;
    // 0.1.146: the spheres of two held guns touch (margin: how close counts).
    internal static bool Touching(ContactSphere[] a,ContactPose pa,ContactSphere[] b,ContactPose pb,float margin,out Vector3 point)
    {
        point=Vector3.Zero;float best=float.PositiveInfinity;
        foreach(var s in a)
        {
            var p=pa.Point(s.Offset);
            foreach(var t in b)
            {
                var q=pb.Point(t.Offset);float gap=Vector3.Distance(p,q)-s.Radius-t.Radius;
                if(gap<margin&&gap<best){best=gap;float d=Vector3.Distance(p,q);point=d>1e-6f?p+(q-p)*(s.Radius/Math.Max(d,s.Radius+t.Radius)):p;}
            }
        }
        return float.IsFinite(best);
    }
    internal static readonly ContactSphere[] Hand={new(new Vector3(0,0,-.025f),.040f),new(new Vector3(0,-.008f,.03f),.045f),new(new Vector3(0,-.01f,.085f),.035f)};
    internal static readonly ContactSphere[] Fist={new(new Vector3(0,-.015f,.015f),.05f)};
}
