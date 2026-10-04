using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.223: the M60's open top cover pressed shut by a hand, as if it were
// solid: a hand that comes onto it from above (within Reach of its surface)
// pushes it down as far as the hand goes and it stays there; pressed down
// to ShutDegrees it snaps shut (it took the grip at its edge before). Fitted
// frame: the cover turns about +X at its hinge (front edge), its free edge
// rising from closed by the opening angle.
internal sealed class CoverPush
{
    internal const float Width=.075f,Reach=.045f,Above=.035f,ShutDegrees=12,NearHinge=.2f,BeyondEdge=1.35f;
    private readonly bool[] touching=new bool[2];
    // How far the hands left it open (degrees; NaN: as far as it opens).
    internal float Opening{get;private set;}=float.NaN;
    internal bool Touching(int hand)=>hand>=0&&hand<2&&touching[hand];
    // Opened again (or gone): open as far as it goes, untouched.
    internal void Reset(){Opening=float.NaN;touching[0]=touching[1]=false;}
    // Hand `hand` (0/1) at `at`; true: pressed shut now.
    internal bool Step(int hand,Vector3 at,Vector3 hinge,Vector3 edge,float openDegrees)
    {
        if(hand<0||hand>1)return false;
        var closed=edge-hinge;float length=MathF.Sqrt(closed.Y*closed.Y+closed.Z*closed.Z);
        if(!(length>.02f)||!Finite(at)||!Finite(hinge)||!Finite(edge)||!float.IsFinite(openDegrees)){touching[hand]=false;return false;}
        float current=float.IsNaN(Opening)?openDegrees:Math.Min(Opening,openDegrees);
        var rel=at-hinge;float y=rel.Y,z=rel.Z,r=MathF.Sqrt(y*y+z*z);
        float cy=closed.Y/length,cz=closed.Z/length;
        // The hand's angle from the closed cover, the way it opens.
        float phi=MathF.Atan2(cy*z-cz*y,cy*y+cz*z)*180/MathF.PI;
        float across=MathF.Abs(rel.X-closed.X*.5f);
        bool over=across<Width&&r>length*NearHinge&&r<length*BeyondEdge&&phi>-8;
        if(!over){touching[hand]=false;return false;}
        float gap=r*(phi-current)*MathF.PI/180;   // above its surface (+) or under it (-)
        if(!touching[hand]){if(MathF.Abs(gap)>Reach)return false;touching[hand]=true;}
        else if(gap>Above){touching[hand]=false;return false;}
        if(phi<current)Opening=Math.Max(0,phi);
        if(!float.IsNaN(Opening)&&Opening<=ShutDegrees){Opening=0;touching[0]=touching[1]=false;return true;}
        return false;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
