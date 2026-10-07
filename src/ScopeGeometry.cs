using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.117: finds a telescopic sight on a gun that has no scope bones (the
// crossbow): a tube parallel to the barrel in the upper part of the gun.
// Points are the gun's vertices in its fitted frame (+Z towards the muzzle,
// +Y up). A candidate axis (x, y) and radius r is supported by the vertices
// lying on that cylinder; a real tube has them all around its circumference
// and along several centimetres. The barrel (a tube reaching the muzzle) is
// not a scope; of the good candidates the highest one is taken.
internal static class ScopeGeometry
{
    internal readonly record struct Tube(float X,float Y,float Radius,float Rear,float Front,int Sectors,int Support)
    {
        internal float Length=>Front-Rear;
    }
    internal const float MinRadius=.008f,MaxRadius=.03f,MinLength=.05f,Shell=.0015f,MuzzleClear=.03f;
    // 0.1.122: a scope is at most 40 cm long, and with a hint (the turret on
    // top of it) the tube must run under the hint: the crossbow's 65 cm rail
    // below it was taken for the scope and the lens sat at the stock.
    internal const float MaxLength=.40f,HintAbove=.045f,HintBelow=.005f,HintAlong=.03f;
    internal const int Sectors=12,MinSectors=9;
    // 0.1.119: with a hint (a scope bone of the rig: the turret or the lock),
    // the axis is taken at the hint's x and searched a few centimetres around
    // its height — the wide crossbow limbs pulled the median x off the scope
    // and the lens ended up by the stock.
    internal static Tube? Find(IReadOnlyList<Vector3> points,Vector3? hint=null)
    {
        if(points==null)return null;
        var all=new List<Vector3>();float minY=float.PositiveInfinity,maxY=float.NegativeInfinity,maxZ=float.NegativeInfinity;
        foreach(var p in points)if(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)){all.Add(p);minY=MathF.Min(minY,p.Y);maxY=MathF.Max(maxY,p.Y);maxZ=MathF.Max(maxZ,p.Z);}
        if(all.Count<30||!(maxY-minY>2*MinRadius))return null;
        float floor=minY+(maxY-minY)*.4f;
        var upper=new List<Vector3>();foreach(var p in all)if(p.Y>floor-MaxRadius)upper.Add(p);
        var xs=new List<float>();foreach(var p in upper)if(p.Y>floor)xs.Add(p.X);
        if(xs.Count<30)return null;xs.Sort();float cx=xs[xs.Count/2];
        float yLo=floor,yHi=maxY-MinRadius+.0005f;
        if(hint is Vector3 h&&float.IsFinite(h.X)&&float.IsFinite(h.Y))
        {
            cx=h.X;yLo=MathF.Max(minY,h.Y-.06f);yHi=MathF.Min(maxY-MinRadius+.0005f,h.Y+.02f);
            upper.Clear();foreach(var p in all)if(MathF.Abs(p.X-cx)<MaxRadius+.004f&&p.Y>yLo-MaxRadius&&p.Y<yHi+MaxRadius)upper.Add(p);
            if(upper.Count<16)return null;
        }
        var candidates=new List<(Tube tube,float score)>();var bins=new HashSet<int>();var sector=new int[Sectors];
        for(float cy=yLo;cy<=yHi;cy+=.002f)
        for(float r=MinRadius;r<=MaxRadius+1e-5f&&cy+r<=maxY+.003f;r+=.001f)
        {
            int support=0;float rear=float.PositiveInfinity,front=float.NegativeInfinity;bins.Clear();Array.Clear(sector);
            foreach(var p in upper)
            {
                float dx=p.X-cx,dy=p.Y-cy,d=MathF.Sqrt(dx*dx+dy*dy);
                if(MathF.Abs(d-r)>Shell)continue;
                support++;sector[(int)((MathF.Atan2(dy,dx)+MathF.PI)/(2*MathF.PI)*Sectors)%Sectors]++;
                bins.Add((int)MathF.Floor(p.Z/.005f));rear=MathF.Min(rear,p.Z);front=MathF.Max(front,p.Z);
            }
            int covered=0,most=0;foreach(int n in sector){if(n>0)covered++;most=Math.Max(most,n);}
            float length=bins.Count*.005f;
            // Round all the way (not a flat wall touching the circle in a few
            // places, not an open channel) and evenly supported.
            if(covered<MinSectors||most>support*.35f||length<MinLength||support<16||front>maxZ-MuzzleClear||front-rear>MaxLength)continue;
            if(hint is Vector3 k&&(k.Z<rear-HintAlong||k.Z>front+HintAlong||k.Y-(cy+r)>HintAbove||(cy+r)-k.Y>HintBelow))continue;
            candidates.Add((new Tube(cx,cy,r,rear,front,covered,support),length*covered*MathF.Sqrt(support)));
        }
        if(candidates.Count==0)return null;
        float bestScore=0;foreach(var c in candidates)bestScore=MathF.Max(bestScore,c.score);
        Tube? best=null;
        foreach(var c in candidates)if(c.score>=bestScore*.5f&&(best==null||c.tube.Y+c.tube.Radius>best.Value.Y+best.Value.Radius+1e-5f||MathF.Abs(c.tube.Y+c.tube.Radius-(best.Value.Y+best.Value.Radius))<=1e-5f&&c.score>Score(best.Value)))best=c.tube;
        return best;
        float Score(Tube t){foreach(var c in candidates)if(c.tube==t)return c.score;return 0;}
    }
    // Without a tube found at the hint: a plain tube under the turret bone.
    internal static Tube FromHint(Vector3 hint)=>new(hint.X,hint.Y-.03f,.018f,hint.Z-.10f,hint.Z+.10f,0,0);
    // 0.1.123: the crossbow's picture was a disc as wide as the fitted tube
    // (wider than the eyepiece: it stuck out of the model) and 4 mm inside the
    // tube's rear, while the eyepiece reached further back: the picture sat
    // deep inside the scope behind a dark ring. Now it is measured at the
    // eyepiece itself: its rearmost points near the axis, and the widest empty
    // circle among them (the opening; its centre searched a few mm around the
    // axis). The lens is half a millimetre inside that rim and just fills the
    // opening, like the glass of a real eyepiece.
    internal readonly record struct Eye(float Z,float X,float Y,float Radius,float Rear,float Depth,int Sectors,int Points);
    internal const float EyeReach=.004f,EyeBehind=.04f,EyeBelow=.012f,EyeGlass=.004f,EyeFill=.96f,EyeInside=.0005f;
    private static readonly float[] EyeDepths={.006f,.010f,.015f,.020f};
    internal static Eye Eyepiece(IReadOnlyList<Vector3> points,Tube t)
    {
        float reach=t.Radius+EyeReach;var near=new List<Vector3>();float rear=float.PositiveInfinity;
        foreach(var p in points)
        {
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
            float dx=p.X-t.X,dy=p.Y-t.Y;
            // Below the axis only a little: the stock lies under the eyepiece.
            if(dy<-EyeBelow||dx*dx+dy*dy>reach*reach||p.Z<t.Rear-EyeBehind||p.Z>t.Rear+.03f)continue;
            near.Add(p);rear=MathF.Min(rear,p.Z);
        }
        var fallback=new Eye(t.Rear+.004f,t.X,t.Y,Math.Clamp(t.Radius*.75f,.006f,.018f),t.Rear,0,0,near.Count);
        if(near.Count<8)return fallback;
        var window=new List<Vector3>();var sector=new bool[Sectors];
        foreach(float depth in EyeDepths)
        {
            window.Clear();foreach(var p in near)if(p.Z<=rear+depth)window.Add(p);
            if(window.Count<8)continue;
            // The widest circle free of points (glass vertices at the very centre aside).
            float bestR=0,bx=t.X,by=t.Y;
            for(float ox=-.004f;ox<=.0041f;ox+=.0005f)for(float oy=-.004f;oy<=.0041f;oy+=.0005f)
            {
                float cx=t.X+ox,cy=t.Y+oy,m=float.PositiveInfinity;
                foreach(var p in window){float dx=p.X-cx,dy=p.Y-cy,r=MathF.Sqrt(dx*dx+dy*dy);if(r>EyeGlass)m=MathF.Min(m,r);}
                if(float.IsFinite(m)&&m>bestR+1e-5f){bestR=m;bx=cx;by=cy;}
            }
            if(!(bestR>EyeGlass))continue;
            // The rim must close around the opening (not two ears at its sides).
            Array.Clear(sector);int covered=0;float rimZ=float.PositiveInfinity;
            foreach(var p in window)
            {
                float dx=p.X-bx,dy=p.Y-by,r=MathF.Sqrt(dx*dx+dy*dy);if(r<bestR-1e-4f||r>bestR+.006f)continue;
                int k=(int)((MathF.Atan2(dy,dx)+MathF.PI)/(2*MathF.PI)*Sectors)%Sectors;if(!sector[k]){sector[k]=true;covered++;}
                rimZ=MathF.Min(rimZ,p.Z);
            }
            // The lower third may be cut away (EyeBelow): 8 of 12 sectors.
            if(covered<8||!float.IsFinite(rimZ))continue;
            float radius=Math.Clamp(bestR*EyeFill,.004f,Math.Min(t.Radius,.024f));
            return new Eye(rimZ+EyeInside,bx,by,radius,rear,depth,covered,window.Count);
        }
        return fallback;
    }
    // 0.1.252: the eyepiece's own glass. The tactical crossbow's eyepiece is
    // wider than its scope's tube (an eyecup 4 cm across on a 2.8 cm tube):
    // the opening was measured only within the tube's radius, so the picture
    // was 2.8 cm across in a 3.6 cm cup and 8 mm in front of the model's green
    // glass, which showed around it. A flat glass across the opening (a vertex
    // at its middle and a ring at one depth, within GlassDepth of the cup's
    // rear) is looked for around the measured lens: the picture is put just in
    // front of it, centred on it and as wide as the cup's opening in front of
    // it (the glass's own radius at most). A narrow inner lens (not across the
    // opening) or no glass: the lens measured at the rim stays.
    internal readonly record struct Glass(float Z,float X,float Y,float Radius,float Opening,int Ring);
    internal const float GlassDepth=.02f,GlassFlat=.0006f,GlassCentre=.005f,GlassMinRing=.006f,GlassAcross=.85f,GlassMaxRadius=.024f;
    internal static Glass? FindGlass(IReadOnlyList<Vector3> points,Eye eye)
    {
        if(points==null||!float.IsFinite(eye.Z)||!float.IsFinite(eye.Rear)||!(eye.Radius>0))return null;
        float reach=Math.Max(eye.Radius*2.5f,.02f);var near=new List<Vector3>();
        foreach(var p in points)
        {
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))continue;
            if(p.Z<eye.Rear-.002f||p.Z>eye.Rear+GlassDepth)continue;
            float dx=p.X-eye.X,dy=p.Y-eye.Y;if(dx*dx+dy*dy>reach*reach)continue;
            near.Add(p);
        }
        // Candidate middles: closest to the eye first.
        var middles=new List<Vector3>();
        foreach(var p in near){float dx=p.X-eye.X,dy=p.Y-eye.Y;if(dx*dx+dy*dy<GlassCentre*GlassCentre&&p.Z>=eye.Z-.001f)middles.Add(p);}
        middles.Sort((a,b)=>a.Z.CompareTo(b.Z));
        var sector=new bool[Sectors];var radii=new List<float>();
        foreach(var c in middles)
        {
            radii.Clear();
            foreach(var p in near)
            {
                if(MathF.Abs(p.Z-c.Z)>GlassFlat)continue;
                float r=MathF.Sqrt((p.X-c.X)*(p.X-c.X)+(p.Y-c.Y)*(p.Y-c.Y));
                if(r>=GlassMinRing&&r<=GlassMaxRadius)radii.Add(r);
            }
            if(radii.Count<8)continue;
            radii.Sort();
            // The ring: the widest cluster of radii (within 0.8 mm) closing all round.
            for(int start=0;start<radii.Count;)
            {
                int end=start;while(end+1<radii.Count&&radii[end+1]-radii[end]<.0008f)end++;
                int count=end-start+1;float ring=0;for(int k=start;k<=end;k++)ring+=radii[k];ring/=count;
                start=end+1;
                if(count<8)continue;
                Array.Clear(sector);int covered=0;
                foreach(var p in near)
                {
                    if(MathF.Abs(p.Z-c.Z)>GlassFlat)continue;
                    float dx=p.X-c.X,dy=p.Y-c.Y,r=MathF.Sqrt(dx*dx+dy*dy);if(MathF.Abs(r-ring)>.0008f)continue;
                    int k=(int)((MathF.Atan2(dy,dx)+MathF.PI)/(2*MathF.PI)*Sectors)%Sectors;if(!sector[k]){sector[k]=true;covered++;}
                }
                if(covered<MinSectors)continue;
                // The cup's opening in front of the glass: the nearest point to its middle between the rear and it.
                float opening=float.PositiveInfinity;
                foreach(var p in near)
                {
                    if(p.Z>=c.Z-GlassFlat)continue;
                    float r=MathF.Sqrt((p.X-c.X)*(p.X-c.X)+(p.Y-c.Y)*(p.Y-c.Y));
                    if(r>EyeGlass)opening=MathF.Min(opening,r);
                }
                if(!float.IsFinite(opening))opening=ring;
                // Across the opening (not a small lens deep inside it).
                if(ring<opening*GlassAcross)continue;
                return new Glass(c.Z,c.X,c.Y,ring,opening,covered);
            }
        }
        return null;
    }
    // The picture just in front of the glass, as wide as the opening before it (the glass's radius at most).
    internal static Eye OnGlass(Eye eye,Glass g)=>eye with{Z=g.Z-EyeInside,X=g.X,Y=g.Y,Radius=Math.Clamp(MathF.Min(g.Radius,g.Opening)*EyeFill,.004f,GlassMaxRadius)};
    // 0.1.251: the search's distances (tube radii and length, the eyepiece's
    // depths and rim) were measured on the crossbows drawn 72 cm long. Drawn
    // longer (the tactical crossbow 92 cm), the same eyepiece no longer closed
    // within them: a narrower ring a centimetre deeper inside was taken, the
    // picture half as wide and hidden in the scope. The search is made on the
    // gun brought back to that size around its fitted muzzle (anchor; the
    // fitted frame grows around it) and its result grown back.
    internal static Vector3 ToTuned(Vector3 p,Vector3 anchor,float growth)=>growth>0&&float.IsFinite(growth)?anchor+(p-anchor)/growth:p;
    internal static Vector3 FromTuned(Vector3 p,Vector3 anchor,float growth)=>growth>0&&float.IsFinite(growth)?anchor+(p-anchor)*growth:p;
    internal static Tube FromTuned(Tube t,Vector3 anchor,float growth)
    {
        if(!(growth>0&&float.IsFinite(growth)))return t;
        var axis=FromTuned(new Vector3(t.X,t.Y,anchor.Z),anchor,growth);
        return new Tube(axis.X,axis.Y,t.Radius*growth,anchor.Z+(t.Rear-anchor.Z)*growth,anchor.Z+(t.Front-anchor.Z)*growth,t.Sectors,t.Support);
    }
    internal static Eye FromTuned(Eye e,Vector3 anchor,float growth)
    {
        if(!(growth>0&&float.IsFinite(growth)))return e;
        var at=FromTuned(new Vector3(e.X,e.Y,e.Z),anchor,growth);
        return new Eye(at.Z,at.X,at.Y,e.Radius*growth,anchor.Z+(e.Rear-anchor.Z)*growth,e.Depth*growth,e.Sectors,e.Points);
    }
    // 0.1.121: the scope's own camera renders the whole scene once more (it
    // held the game at half rate whenever a scoped gun was in the hand). It
    // renders only while an eye is at the eyepiece: close behind the lens and
    // looking along the scope; otherwise the lens is dark glass.
    internal const float ViewDistance=.30f,ViewCone=.75f;
    internal static bool Viewing(Vector3 lens,Vector3 head,Vector3 forward)
    {
        var toHead=head-lens;float d=toHead.Length();float f=forward.Length();
        if(!float.IsFinite(d)||!float.IsFinite(f)||f<1e-5f)return false;
        if(d<1e-4f)return true;
        return d<ViewDistance&&Vector3.Dot(toHead/d,-forward/f)>ViewCone;
    }
}
