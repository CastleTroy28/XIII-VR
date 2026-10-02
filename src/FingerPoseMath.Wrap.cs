using System;
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
    internal static string WrapProfile(float curl)=>WrapPrefix+((int)MathF.Round(Math.Clamp(float.IsFinite(curl)?curl:HeldCurl,HeldCurl,MaxWrapCurl)*100)).ToString(CultureInfo.InvariantCulture);
    // The fingers' closure for a held thing's profile (HeldCurl but for a wrap profile).
    internal static float HeldAmount(string profile)
    {
        if(!profile.StartsWith(WrapPrefix,StringComparison.Ordinal))return HeldCurl;
        if(!int.TryParse(profile.AsSpan(WrapPrefix.Length),NumberStyles.Integer,CultureInfo.InvariantCulture,out int percent))return HeldCurl;
        return Math.Clamp(percent/100f,HeldCurl,MaxWrapCurl);
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
