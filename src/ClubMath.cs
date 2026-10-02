using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.196: a long gun held by its barrel at the muzzle, stock up
// above the fist, swung like a bat or a club - in the left or the right hand.
// Fitted frame of a gun: +Z towards the muzzle, +Y up; its barrel runs along Z
// through the muzzle point.
internal static class ClubMath
{
    // Rifles, shotguns (the double-barrelled one too), machine guns.
    internal static bool Clubbable(string profile)=>profile is "shotgun" or "m16" or "ak47" or "sniper" or "m60" or "rifle" or "heavy";
    // The fist on the barrel: at least this far back from the muzzle (the
    // muzzle just under the little finger), at most MaxFromMuzzle (or 40 % of
    // the gun) back; the hand at most Reach from the barrel's line.
    internal const float MinFromMuzzle=.05f,MaxFromMuzzle=.35f,Reach=.10f,Slack=.04f;
    // The stock leans forward of straight up (above the thumb) by this much:
    // the barrel lies in the fist as a pistol's grip does.
    internal const float TiltDegrees=18f;
    // The gun turns from where it was into the fist within this time.
    internal const float TurnSeconds=.2f;
    internal static float MaxAlong(float length)=>Math.Clamp(float.IsFinite(length)?length*.4f:MaxFromMuzzle,MinFromMuzzle+.02f,MaxFromMuzzle);
    // hand: the hand in the gun's fitted frame; along: where the fist holds the
    // barrel (m back from the muzzle, clamped); false: not at the barrel's front.
    internal static bool Along(Vector3 hand,Vector3 muzzle,float length,out float along)
    {
        along=MinFromMuzzle;
        if(!Finite(hand)||!Finite(muzzle))return false;
        float max=MaxAlong(length),back=muzzle.Z-hand.Z;
        float miss=new Vector2(hand.X-muzzle.X,hand.Y-muzzle.Y).Length();
        along=Math.Clamp(back,MinFromMuzzle,max);
        return back>=-Slack&&back<=max+Slack&&miss<=Reach;
    }
    // The fist's point on the barrel (fitted frame).
    internal static Vector3 GrabPoint(Vector3 muzzle,float along)=>new(muzzle.X,muzzle.Y,muzzle.Z-(float.IsFinite(along)?along:MinFromMuzzle));
    // The stock's end on the barrel's line (fitted frame): its far end.
    internal static Vector3 StockEnd(Vector3 muzzle,float length)=>new(muzzle.X,muzzle.Y,muzzle.Z-(float.IsFinite(length)&&length>.2f?length:.8f));
    // A hand gripping at the barrel's front, not at the fore-end the support
    // grip holds: nearer the front of the barrel than that grip point (world).
    internal static bool NearerFront(Vector3 hand,Vector3 front,Vector3 socket)=>Finite(hand)&&Finite(front)&&(!Finite(socket)||Vector3.Distance(hand,front)<Vector3.Distance(hand,socket));
    // The gun in the hand's aim frame (x right, y up, z forward - the way a
    // pistol would point): muzzle down, stock up leaning forward by the tilt,
    // its sights facing forward. The same numbers as Unity's Euler(90+tilt,0,0).
    internal static Quaternion Turn=>Quaternion.CreateFromAxisAngle(Vector3.UnitX,(90+TiltDegrees)*MathF.PI/180);
    // A pistol grip's line in the gun's frame (bottom to top, leaning forward
    // by the tilt) and the way forward square to it.
    internal static Vector3 GripLine=>new(0,MathF.Cos(TiltDegrees*MathF.PI/180),MathF.Sin(TiltDegrees*MathF.PI/180));
    internal static Vector3 GripForward=>new(0,-MathF.Sin(TiltDegrees*MathF.PI/180),MathF.Cos(TiltDegrees*MathF.PI/180));
    // 0.1.198: the closed hand's grip point moved deeper into the fingers -
    // toward the wrist and the palm (hand frame: z to the fingertips, y to the
    // back of the hand), mm from the VR settings (config file).
    internal const int DefaultForwardMm=-28,DefaultUpMm=-4;
    // 0.1.200: the hook's handle 8 mm nearer the palm.
    internal const int ZiplineForwardMm=0,ZiplineUpMm=8;
    internal static Vector3 Deeper(int forwardMm,int upMm)=>new(0,Math.Clamp(upMm,-60,60)*.001f,Math.Clamp(forwardMm,-80,40)*.001f);
    // 0.1.200: the
    // barrel's thickness where the fist holds it, from the gun's own mesh
    // (fitted frame): the points of a 2.4 cm slice across the barrel there
    // within 1.8 cm of its line; twice the distance from the line most of
    // them lie within (4 of 5: a grenade launcher's tube or a sight below or
    // above it is only touched at its edge). NaN: too few points.
    internal const float SliceHalf=.012f,SliceReach=.018f;
    internal static float Thickness(System.Collections.Generic.IEnumerable<Vector3> points,Vector3 at)
    {
        var radial=new System.Collections.Generic.List<float>();
        if(!Finite(at))return float.NaN;
        foreach(var p in points)
        {
            if(!Finite(p)||Math.Abs(p.Z-at.Z)>SliceHalf)continue;
            float r=new Vector2(p.X-at.X,p.Y-at.Y).Length();
            if(r<=SliceReach)radial.Add(r);
        }
        if(radial.Count<8)return float.NaN;
        radial.Sort();
        return 2*radial[Math.Min(radial.Count-1,(int)(radial.Count*.8f))];
    }
    // The turn from where the gun was into the fist (0..1, eased).
    internal static float Blend(float seconds)
    {
        if(!float.IsFinite(seconds))return 1;
        float t=Math.Clamp(seconds/TurnSeconds,0,1);return t*t*(3-2*t);
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
