using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
// 0.1.200: the
// fingers of a held long thing close by a fixed amount (HeldCurl), right for a
// stick or a thick barrel; round a thin one (the M4's, 1.6 cm) they stayed
// open around it. A thinner thing is gripped with the fingers closed further:
// the circle this hand's own fingers lie on (their joints and pads) narrowed by
// as much as the thing is thinner, its middle moved with it; the closure is
// handed on in the hand's profile ("prop_long_handle@96": 0.96 of a fist).
internal sealed partial class FingerPoseMath
{
    internal const float HeldCurl=.88f,MaxWrapCurl=1.2f;
    internal const string WrapPrefix="prop_long_handle@";
    internal static string WrapProfile(float curl)=>WrapPrefix+((int)MathF.Round(Math.Clamp(float.IsFinite(curl)?curl:HeldCurl,MinWrapCurl,MaxWrapCurl)*100)).ToString(CultureInfo.InvariantCulture);
    // 0.1.226: a thicker thing (the bazooka's rocket) is held with the fingers
    // and the thumb opened as much as it is thicker (WrapCurlThick), down to this.
    internal const float MinWrapCurl=.5f;
    // The fingers' closure for a held thing's profile (HeldCurl but for a wrap profile).
    internal static float HeldAmount(string profile)
    {
        // 0.1.227: any held thing's profile with its closure after '@' (the
        // bazooka's handle with the index on its trigger: "bazooka_trigger@60").
        int at=profile.LastIndexOf('@');
        if(at<=0||!int.TryParse(profile.AsSpan(at+1),NumberStyles.Integer,CultureInfo.InvariantCulture,out int percent))return HeldCurl;
        return Math.Clamp(percent/100f,MinWrapCurl,MaxWrapCurl);
    }
    // 0.1.227: a closure's profile name (prefix and hundredths of a fist).
    internal static string ClosureProfile(string prefix,float curl)=>prefix+((int)MathF.Round(Math.Clamp(float.IsFinite(curl)?curl:HeldCurl,MinWrapCurl,MaxWrapCurl)*100)).ToString(CultureInfo.InvariantCulture);
    // 0.1.227: the hand on the bazooka's handle: the index on the trigger, the
    // other fingers closed round the handle by `curl` ("bazooka_trigger@60").
    internal const string TriggerWrapPrefix="bazooka_trigger@";
    internal static string TriggerWrapProfile(float curl)=>ClosureProfile(TriggerWrapPrefix,curl);
    // 0.1.227: the fingers' closure round a long thing `radius` thick (hand
    // units): tighter round a thinner one than the stick, more open round a thicker one.
    internal float GripCurl(float radius,out Vector3 shift)=>float.IsFinite(radius)&&radius>0&&radius<StickRadius?WrapCurl(radius,out shift):WrapCurlThick(radius,out shift);
    // 0.1.227: where a long thing held in this hand lies (canonical frame):
    // its line through the middles of the circles the fingers closed by `curl`
    // lie on (each finger's own, as WrapCircle takes them; fitted along X) -
    // the fingers then lie round it, neither in it nor off it - those round it
    // being all four, or (trigger) the three below the index finger, which is
    // on the trigger. channel: the line at those fingers' middle; little: the
    // line's way toward the little finger (+X for a right hand, -X for a left
    // one, leaning as the shorter fingers' curls sit lower: at most 25 degrees).
    internal bool GripChannel(float curl,bool trigger,out Vector3 channel,out Vector3 little)
    {
        channel=Vector3.Zero;little=new Vector3(rightHand?1:-1,0,0);
        var centers=new List<Vector3>();
        for(int i=0;i<fingers.Count;i++){if(trigger&&i==indexFinger)continue;if(FingerCircle(fingers[i],curl,out var c,out _))centers.Add(c);}
        if(centers.Count==0)return false;
        var mean=centers.Aggregate(Vector3.Zero,(a,c)=>a+c)/centers.Count;
        float sxx=0,sxy=0,sxz=0;foreach(var c in centers){var d=c-mean;sxx+=d.X*d.X;sxy+=d.X*d.Y;sxz+=d.X*d.Z;}
        const float steep=.466f;
        float sy=sxx>1e-8f?Math.Clamp(sxy/sxx,-steep,steep):0,sz=sxx>1e-8f?Math.Clamp(sxz/sxx,-steep,steep):0;
        channel=mean;little=Vector3.Normalize(new Vector3(1,sy,sz))*(rightHand?1:-1);
        return float.IsFinite(channel.X)&&float.IsFinite(channel.Y)&&float.IsFinite(channel.Z)&&float.IsFinite(little.X)&&float.IsFinite(little.Y)&&float.IsFinite(little.Z);
    }
    // The circle one finger closed by `curl` lies on (seen along X; its x the
    // finger's own mean), its radius less the finger's thickness.
    private bool FingerCircle(int[] chain,float curl,out Vector3 center,out float radius)
    {
        center=Vector3.Zero;radius=0;
        var pose=(Matrix4x4[])rest.Clone();Bend(pose,chain,curl,true,false);var r=ChainRadius(chain);
        var pts=new[]{pose[chain[0]].Translation,pose[chain[1]].Translation,pose[chain[2]].Translation,Vector3.Transform(RimLocalPad(chain),pose[chain[2]])};
        if(!Circle(pts.Select(p=>new Vector2(p.Y,p.Z)).ToArray(),out var c,out float R))return false;
        center=new Vector3(pts.Average(p=>p.X),c.X,c.Y);radius=R-(r[0]+r[1]+r[2])/3;return true;
    }
    // The thumb's closure for a held thing's profile: opened with the fingers round a thicker thing.
    internal static float ThumbAmount(string profile)
    {
        const float closed=.85f;float a=HeldAmount(profile);
        if(a>=HeldCurl)return closed;
        float k=a/HeldCurl;return closed*k*k;
    }
    // The stick the fixed closure (HeldCurl) is right for: 3.2 cm (LongHandleMath.StickThickness).
    internal const float StickRadius=.016f;
    // The fingers' (not the thumb's) closure round a long thing `radius`
    // thick across the hand (hand units: a scaled hand's radius is divided by
    // its scale) and how far its middle moves with them: the circle the
    // fingers lie on (WrapCircle) narrowed by as much as the thing is thinner
    // than the stick, its middle followed. HeldCurl and no move for a stick
    // or thicker. The fixed closure stays right for the things it was set
    // for; a thinner thing is gripped as much tighter as it is thinner.
    internal float WrapCurl(float radius,out Vector3 shift)
    {
        shift=Vector3.Zero;
        if(!float.IsFinite(radius)||radius<=0||radius>=StickRadius)return HeldCurl;
        if(!WrapCircle(HeldCurl,out var c0,out float r0))return HeldCurl;
        float target=r0-(StickRadius-radius);
        float curl=HeldCurl;Vector3 c=c0;
        for(float k=HeldCurl+.01f;k<=MaxWrapCurl+1e-4f;k+=.01f)
        {
            if(!WrapCircle(k,out var ck,out float rk))break;
            curl=MathF.Round(k*100)/100;c=ck;
            if(rk<=target)break;
        }
        shift=new Vector3(0,c.Y-c0.Y,c.Z-c0.Z);
        if(shift.Length()>.02f)shift=Vector3.Normalize(shift)*.02f;
        return curl;
    }
    // 0.1.226: round a thing thicker than the stick: the fingers opened until
    // the circle they lie on is as much wider (down to MinWrapCurl), its middle
    // followed (moving out from the palm). HeldCurl for the stick or thinner.
    internal float WrapCurlThick(float radius,out Vector3 shift)
    {
        shift=Vector3.Zero;
        if(!float.IsFinite(radius)||radius<=StickRadius)return HeldCurl;
        if(!WrapCircle(HeldCurl,out var c0,out float r0))return HeldCurl;
        float target=r0+(radius-StickRadius);
        float curl=HeldCurl;Vector3 c=c0;
        for(float k=HeldCurl-.01f;k>=MinWrapCurl-1e-4f;k-=.01f)
        {
            if(!WrapCircle(k,out var ck,out float rk))break;
            curl=MathF.Round(k*100)/100;c=ck;
            if(rk>=target)break;
        }
        shift=new Vector3(0,c.Y-c0.Y,c.Z-c0.Z);
        if(shift.Length()>.025f)shift=Vector3.Normalize(shift)*.025f;
        return curl;
    }
    // 0.1.227: a long thing held in this hand, fitted: its line across the
    // hand through the middles of the fingers' curls (GripChannel), each
    // finger then closed until it lies on the thing (not in it, not off it:
    // the fingers are not all as long, and one closure for all of them put
    // the little finger into the bazooka's handle and the middle one off it),
    // the thumb wrapped round it clear of it (closed the usual way it went into
    // the bazooka's front grip and the rocket's tube) or, failing that, opened.
    // The fit is kept for the profile name returned (prefix + the closure) and
    // drawn with it (HeldPose). radius, halfLength: the thing's, hand units
    // (the thing reaches halfLength along X either side of the channel).
    private readonly Dictionary<string,(float[] fingers,float thumb)> gripFits=new();
    private int fitRevision;
    internal int FitRevision=>fitRevision;
    internal string? FitGrip(string prefix,float radius,float halfLength,bool trigger,out float curl,out Vector3 channel,out Vector3 little)
    {
        curl=GripCurl(radius,out _);channel=Vector3.Zero;little=new Vector3(rightHand?1:-1,0,0);
        if(!float.IsFinite(radius)||radius<=0||!GripChannel(curl,trigger,out channel,out little))return null;
        if(!float.IsFinite(halfLength)||halfLength<=0)halfLength=1;
        var line=(channel,little,halfLength);
        var amounts=new float[fingers.Count];
        for(int f=0;f<fingers.Count;f++)
        {
            if(trigger&&f==indexFinger){amounts[f]=float.NaN;continue;}
            float best=float.NaN;
            for(float k=.2f;k<=MaxWrapCurl+1e-4f;k+=.01f)
            {
                var pose=(Matrix4x4[])rest.Clone();Bend(pose,fingers[f],k,true,false);
                if(Clearance(pose,fingers[f],false,line,radius)<-.0005f)break;
                best=MathF.Round(k*100)/100;
            }
            amounts[f]=float.IsNaN(best)?curl:best;
        }
        string name=ClosureProfile(prefix,curl);
        float thumbBase=ThumbAmount(name),thumbFit=thumbBase;
        // 0.1.228: the thumb wraps round the thing as if it went on past the hand
        // (a handle goes on up into its gun): above the bazooka's handle it was
        // left open, along the gun's side, not round the handle.
        var thumbLine=(channel,little,1f);
        bool Clear(float t){var pose=(Matrix4x4[])rest.Clone();if(t!=0)Bend(pose,thumb,t,true,true);return Clearance(pose,thumb,true,thumbLine,radius)>=.001f;}
        if(!Clear(thumbBase))
        {
            thumbFit=float.NaN;
            for(float t=thumbBase+.05f;t<=1.8f+1e-4f;t+=.05f)if(Clear(t)){thumbFit=MathF.Round(t*100)/100;break;}
            if(float.IsNaN(thumbFit))for(float t=thumbBase-.05f;t>=-.4f-1e-4f;t-=.05f)if(Clear(t)){thumbFit=MathF.Round(t*100)/100;break;}
            if(float.IsNaN(thumbFit))thumbFit=thumbBase;
        }
        if(gripFits.Count>=16)gripFits.Clear();
        gripFits[name]=(amounts,thumbFit);fitRevision++;
        return name;
    }
    internal bool GripFit(string profile,out float[] fingerAmounts,out float thumbAmount)
    {
        fingerAmounts=Array.Empty<float>();thumbAmount=0;
        if(!gripFits.TryGetValue(profile,out var fit))return false;
        fingerAmounts=fit.fingers;thumbAmount=fit.thumb;return true;
    }
    // The least room between a posed finger (thumb) and a thing
    // along `line` (`radius` thick, reaching `half` either side of its point):
    // its bones and pad as capsules, from its middle joint on (the
    // thumb from half its first bone): the base of a finger lies on the thing
    // in the palm, out of sight, where the skin gives.
    private float Clearance(Matrix4x4[] pose,int[] chain,bool isThumb,(Vector3 at,Vector3 along,float half) line,float radius)
    {
        var r=ChainRadius(chain);
        var pts=new[]{pose[chain[0]].Translation,pose[chain[1]].Translation,pose[chain[2]].Translation,Vector3.Transform(RimLocalPad(chain),pose[chain[2]])};
        float least=float.PositiveInfinity;
        for(int j=0;j<3;j++)for(int u=0;u<=6;u++)
        {
            float t=u/6f;if(j==0&&(!isThumb||t<.5f))continue;
            var v=Vector3.Lerp(pts[j],pts[j+1],t)-line.at;float a=Vector3.Dot(v,line.along);if(MathF.Abs(a)>line.half)continue;
            float d=(v-line.along*a).Length()-(r[j]+(r[j+1]-r[j])*t)-radius;
            if(d<least)least=d;
        }
        return least;
    }
    // The fingers closed by `curl` lie round a cylinder across the hand: each
    // finger's joints and pad on a circle (seen along X) - its middle, the
    // cylinder's; its radius less the finger's thickness, the cylinder's.
    // The fingers' mean (canonical frame; X: the fingers' mean).
    internal bool WrapCircle(float curl,out Vector3 center,out float radius)
    {
        center=Vector3.Zero;radius=0;var pose=(Matrix4x4[])rest.Clone();int n=0;float sumX=0;var sum=Vector2.Zero;float sumR=0;
        foreach(var chain in fingers)
        {
            Bend(pose,chain,curl,true,false);
            var r=ChainRadius(chain);
            var pts=new[]{pose[chain[0]].Translation,pose[chain[1]].Translation,pose[chain[2]].Translation,Vector3.Transform(RimLocalPad(chain),pose[chain[2]])};
            if(!Circle(pts.Select(p=>new Vector2(p.Y,p.Z)).ToArray(),out var c,out float R))continue;
            sum+=c;sumR+=R-(r[0]+r[1]+r[2])/3;sumX+=pts.Average(p=>p.X);n++;
        }
        if(n==0)return false;
        center=new Vector3(sumX/n,sum.X/n,sum.Y/n);radius=sumR/n;return float.IsFinite(radius);
    }
    // Least-squares circle through points (Kasa fit).
    private static bool Circle(Vector2[] p,out Vector2 center,out float radius)
    {
        center=Vector2.Zero;radius=0;
        double sx=0,sy=0,sxx=0,syy=0,sxy=0,sxz=0,syz=0,sz=0;int n=p.Length;
        foreach(var q in p){double x=q.X,y=q.Y,z=x*x+y*y;sx+=x;sy+=y;sxx+=x*x;syy+=y*y;sxy+=x*y;sxz+=x*z;syz+=y*z;sz+=z;}
        // Solve [sxx sxy sx; sxy syy sy; sx sy n] [a b c] = [sxz syz sz]
        double[,] m={{sxx,sxy,sx,sxz},{sxy,syy,sy,syz},{sx,sy,n,sz}};
        for(int i=0;i<3;i++)
        {
            int piv=i;for(int r=i+1;r<3;r++)if(Math.Abs(m[r,i])>Math.Abs(m[piv,i]))piv=r;
            if(Math.Abs(m[piv,i])<1e-14)return false;
            for(int c=0;c<4;c++){var t=m[i,c];m[i,c]=m[piv,c];m[piv,c]=t;}
            for(int r=0;r<3;r++)if(r!=i){double f=m[r,i]/m[i,i];for(int c=0;c<4;c++)m[r,c]-=f*m[i,c];}
        }
        double a=m[0,3]/m[0,0],b=m[1,3]/m[1,1],cc=m[2,3]/m[2,2];
        double cx=a/2,cy=b/2,rr=cc+cx*cx+cy*cy;if(!(rr>0))return false;
        center=new Vector2((float)cx,(float)cy);radius=(float)Math.Sqrt(rr);return true;
    }
}
