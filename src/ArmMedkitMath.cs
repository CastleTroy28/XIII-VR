using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.186. A tiny model of each medkit owned sits on its
// forearm's cut; the other hand's grip there takes it.
internal static class ArmMedkitMath
{
    // Long edge of the tiny models (the small one, the large one), metres.
    internal const float SmallLength=.045f,LargeLength=.055f;
    // A grip within this of a model takes it; the hand coming this close feels a tick.
    internal const float GrabRadius=.10f;
    // Clear of the cut face.
    internal const float Gap=.003f;
    // The left forearm carries the small medkit (slot 9), the right the large one (10).
    internal static int Slot(int arm)=>arm==0?9:10;
    internal static int ArmOf(int slot)=>slot==9?0:slot==10?1:-1;
    internal static bool Large(int arm)=>arm==1;
    // A hand takes from the other forearm (it cannot reach its own).
    internal static int ArmFor(int hand)=>1-hand;
    internal static float Scale(bool large,float longEdge)=>(large?LargeLength:SmallLength)/Math.Max(.001f,longEdge);
    // The model's middle: on the cut, out toward the elbow by half its thickness.
    internal static Vector3 Center(Vector3 cut,Vector3 outward,float thickness)
    {
        var o=outward.LengthSquared()>1e-10f?Vector3.Normalize(outward):-Vector3.UnitZ;
        return cut+o*(Math.Max(0,thickness)*.5f+Gap);
    }
    // The model's front (its depth axis): the hand's up across the cut.
    internal static Vector3 Front(Vector3 up,Vector3 outward)
    {
        var o=outward.LengthSquared()>1e-10f?Vector3.Normalize(outward):-Vector3.UnitZ;
        var f=up-o*Vector3.Dot(up,o);
        if(f.LengthSquared()<1e-8f)f=Vector3.Cross(o,Math.Abs(o.Y)<.9f?Vector3.UnitY:Vector3.UnitX);
        return Vector3.Normalize(f);
    }
    internal static bool Reach(Vector3 hand,Vector3 model)=>Vector3.Distance(hand,model)<GrabRadius;
    // No rim found: the forearm's line from the wrist toward the elbow, at the cut.
    internal static Vector3 AlongForearm(Vector3 elbow,float cutZ)
    {
        if(!(elbow.Z<-.01f))return new Vector3(0,0,cutZ);
        float t=cutZ/elbow.Z;return new Vector3(elbow.X*t,elbow.Y*t,cutZ);
    }
}
