using System;
using System.Collections.Generic;
using System.Numerics;
namespace XiiiXR;
// 0.1.217: the bazooka's tube and its rocket, from its rig and mesh (the
// game's bazooka: wpn_bazooka_SH_BND_JNT carries the tube, the rocket is
// skinned to wpn_bazooka_rocket_SH_BND_JNT; see WEAPON BONES in the log).
// Its fitted frame looks down the tube along +Z.
internal static class BazookaTubeMath
{
    // The rocket's bones: named "rocket", skinning ones (the rig's
    // wpn_bazooka_rocket_zero_SH_JNT is a reference point, not skinned).
    internal static bool RocketBone(string? name)
    {
        if(string.IsNullOrEmpty(name))return false;var lower=name.ToLowerInvariant();
        return lower.Contains("rocket",StringComparison.Ordinal)&&lower.EndsWith("_bnd_jnt",StringComparison.Ordinal);
    }
    // The tube's bone: wpn_bazooka(_SH)_BND_JNT, else the bone whose name prefixes the most others.
    internal static int Root(IReadOnlyList<string?> names)
    {
        for(int i=0;i<names.Count;i++)
        {
            var n=names[i]?.ToLowerInvariant();
            if(n=="wpn_bazooka_sh_bnd_jnt"||n=="wpn_bazooka_bnd_jnt")return i;
        }
        return ReloadBones.CommonRoot(names);
    }
    // How far from the rocket's line the tube's own points count for its mouth (m).
    internal const float MouthRadius=.075f;
    internal const int MouthPoints=8;
    // The tube's mouth: on the rocket's line (its points' middle across the
    // tube), at the front of the tube's own points near that line - not the
    // gun's aim point (0.1.215 put the glow there, well ahead of the tube).
    // NaN when too few points are near the line.
    internal static Vector3 Mouth(IReadOnlyList<Vector3> tube,IReadOnlyList<Vector3> rocket)
    {
        var nan=new Vector3(float.NaN);
        if(rocket.Count==0||tube.Count==0)return nan;
        float x=0,y=0;int n=0;
        foreach(var p in rocket){if(!Finite(p))continue;x+=p.X;y+=p.Y;n++;}
        if(n==0)return nan;x/=n;y/=n;
        float front=float.NegativeInfinity;int near=0;
        foreach(var p in tube)
        {
            if(!Finite(p))continue;
            float dx=p.X-x,dy=p.Y-y;if(dx*dx+dy*dy>MouthRadius*MouthRadius)continue;
            near++;if(p.Z>front)front=p.Z;
        }
        return near>=MouthPoints?new Vector3(x,y,front):nan;
    }
    // A rocket placement that is in the tube (a placement taken while the
    // game moved it, for its reload, is not): long along the tube (+Z), its
    // middle within the tube's width, along part of the tube's length.
    internal static bool InTube(IReadOnlyList<Vector3> rocket,IReadOnlyList<Vector3> tube)
    {
        if(rocket.Count<MouthPoints||tube.Count<MouthPoints)return false;
        if(!Bounds(rocket,out var rlo,out var rhi,out var middle)||!Bounds(tube,out var tlo,out var thi,out _))return false;
        var size=rhi-rlo;float across=Math.Max(size.X,size.Y);
        if(!(size.Z>.05f)||size.Z<across*2.5f)return false;
        if(middle.X<tlo.X||middle.X>thi.X||middle.Y<tlo.Y||middle.Y>thi.Y)return false;
        return rlo.Z<thi.Z&&rhi.Z>tlo.Z;
    }
    private static bool Bounds(IReadOnlyList<Vector3> points,out Vector3 lo,out Vector3 hi,out Vector3 middle)
    {
        lo=new Vector3(float.MaxValue);hi=new Vector3(float.MinValue);var sum=Vector3.Zero;int n=0;
        foreach(var p in points){if(!Finite(p))continue;lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,p);sum+=p;n++;}
        middle=n>0?sum/n:Vector3.Zero;return n>0;
    }
    // Where along the rocket the hand holds it: round its motor tube, this
    // share of its length from the tail (the warhead is the front half).
    // 0.1.219: lower, near the tail (0.1.217 held it a fifth up: too high).
    internal const float GripFromTail=.1f;
    // 0.1.219: the bazooka's own grips (its rig: wpn_bazooka_rear_grip_SH_BND_JNT,
    // the handle with the trigger, and wpn_bazooka_front_grip_SH_BND_JNT).
    internal static bool GripBone(string? name,bool front)
    {
        if(string.IsNullOrEmpty(name))return false;var lower=name.ToLowerInvariant();
        if(!lower.EndsWith("_bnd_jnt",StringComparison.Ordinal)||lower.Contains("support",StringComparison.Ordinal))return false;
        return lower.Contains(front?"front_grip":"rear_grip",StringComparison.Ordinal);
    }
    // 0.1.223: the handle with the trigger is the rig's mid grip
    // (wpn_bazooka_mid_grip_SH_BND_JNT); its rear grip is the shoulder rest
    // behind it (0.1.219 took that for the handle: "not a handle").
    internal static bool TriggerGripBone(string? name)
    {
        if(string.IsNullOrEmpty(name))return false;var lower=name.ToLowerInvariant();
        return lower.EndsWith("_bnd_jnt",StringComparison.Ordinal)&&!lower.Contains("support",StringComparison.Ordinal)&&lower.Contains("mid_grip",StringComparison.Ordinal);
    }
    // The handle's top share left out of its bar (its trigger guard and its join to the tube).
    internal const float TriggerTrim=.3f;
    // 0.1.231: the trigger's own bone (wpn_bazooka_trigger_SH_BND_JNT).
    internal static bool TriggerBone(string? name)
    {
        if(string.IsNullOrEmpty(name))return false;var lower=name.ToLowerInvariant();
        return lower.EndsWith("_bnd_jnt",StringComparison.Ordinal)&&lower.Contains("_trigger",StringComparison.Ordinal)&&!lower.Contains("guard",StringComparison.Ordinal);
    }
    // 0.1.231: the hand on the handle moved along it (up toward the tube, or down)
    // until its index fingertip is level with the trigger: the curl of the three
    // lower fingers centred on the handle's middle left the index 2-3 cm below
    // the trigger, the hand low on the handle (the player's report, 0.1.229).
    // hand/turn: the hand placed on the grip (OnGrip); pad: the index fingertip
    // in that hand's frame as drawn; trigger: the trigger's middle. The move,
    // along the handle's line, within MinRaise..MaxRaise (up only: never lower than the fit put it).
    // knuckle/top: the index knuckle (the web of the hand) not more than
    // KnuckleOver above the handle's own top (its join to the tube), when known.
    internal const float MinRaise=0,MaxRaise=.04f,KnuckleOver=.005f;
    internal static float RaiseToTrigger(GripBarMath.Bar bar,Vector3 hand,Quaternion turn,Vector3 pad,Vector3 trigger,Vector3? knuckle=null,float? top=null)
    {
        var axis=Vector3.Normalize(bar.Axis);
        var at=hand+Vector3.Transform(pad,turn);
        float raise=Vector3.Dot(trigger-at,axis);
        if(!float.IsFinite(raise))return 0;
        float most=MaxRaise;
        if(knuckle is Vector3 k&&top is float t&&float.IsFinite(t))
        {
            float level=Vector3.Dot(hand+Vector3.Transform(k,turn)-bar.Center,axis);
            if(float.IsFinite(level))most=Math.Max(MinRaise,Math.Min(most,t+KnuckleOver-level));
        }
        return Math.Clamp(raise,MinRaise,most);
    }
    // The furthest of the points along the bar's line, from its middle (the handle's own top).
    internal static float? TopAlong(GripBarMath.Bar bar,IEnumerable<Vector3> points)
    {
        var axis=Vector3.Normalize(bar.Axis);float top=float.NegativeInfinity;
        foreach(var p in points)top=Math.Max(top,Vector3.Dot(p-bar.Center,axis));
        return float.IsFinite(top)?top:null;
    }
    // 0.1.223: a hand's hold of one grip moved onto another (the game's own
    // hold of the handle, its fingers on the trigger, put on the front grip):
    // the hand (its place and turn, in the same frame as the bars) kept where
    // it is on `from`, turned with the grip's line onto `to`. mirror: as the
    // other hand (mirrored across `to`'s own middle, x).
    internal static (Vector3 position,Quaternion rotation) MoveHold(Vector3 hand,Quaternion turn,GripBarMath.Bar from,GripBarMath.Bar to,bool mirror)
    {
        var a=Vector3.Normalize(from.Axis);var b=Vector3.Normalize(to.Axis);
        Quaternion align;float d=Vector3.Dot(a,b);
        if(d>.9999f)align=Quaternion.Identity;
        else if(d<-.9999f)align=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI);
        else{var c=Vector3.Cross(a,b);align=Quaternion.Normalize(new Quaternion(c.X,c.Y,c.Z,1+d));}
        var p=to.Center+Vector3.Transform(hand-from.Center,align);var q=Quaternion.Normalize(align*turn);
        if(mirror){p=new Vector3(2*to.Center.X-p.X,p.Y,p.Z);q=new Quaternion(q.X,-q.Y,-q.Z,q.W);}
        return (p,q);
    }
    // 0.1.227: a hand round one of the bazooka's upright grips (fitted frame).
    // 0.1.226 put each hand where a game's own hand had once been (the right
    // hand where the game's right hand lay in the games of 0.1.215 to 0.1.224):
    // drawn there, the closed fingers were 10 cm from the handle (above it, by
    // the tube, and beside it), the hands on the front grip 2 cm off it. Now
    // the hand closes round the grip itself: turned as the game's right hand
    // holds the handle (RightTurn; a left hand that hold mirrored), the line
    // through the middles of its closed fingers' curls laid along the grip
    // (`little`: its way to the little finger, down the grip), and placed so
    // that that line's middle (`channel`, the hand's own frame as drawn:
    // FingerPoseMath.GripChannel, DrawnChannel) is at the grip's middle.
    internal static (Vector3 position,Quaternion rotation) OnGrip(GripBarMath.Bar bar,Quaternion turn,bool right,Vector3 channel,Vector3 little)
    {
        var axis=Vector3.Normalize(bar.Axis);
        var q=Quaternion.Normalize(right?turn:new Quaternion(turn.X,-turn.Y,-turn.Z,turn.W));
        // The fingers' line (its way to the little finger) down the grip.
        if(!(little.LengthSquared()>.25f))little=new Vector3(right?1:-1,0,0);
        q=Quaternion.Normalize(FromTo(Vector3.Transform(Vector3.Normalize(little),q),-axis)*q);
        return (bar.Center-Vector3.Transform(channel,q),q);
    }
    // The turn taking direction a to b by the shortest way.
    internal static Quaternion FromTo(Vector3 a,Vector3 b)
    {
        a=Vector3.Normalize(a);b=Vector3.Normalize(b);float d=Vector3.Dot(a,b);
        if(d>.99999f)return Quaternion.Identity;
        if(d<-.99999f){var side=Vector3.Cross(a,Vector3.UnitY);if(side.LengthSquared()<1e-6f)side=Vector3.Cross(a,Vector3.UnitX);return Quaternion.CreateFromAxisAngle(Vector3.Normalize(side),MathF.PI);}
        var c=Vector3.Cross(a,b);return Quaternion.Normalize(new Quaternion(c.X,c.Y,c.Z,1+d));
    }
    // The canonical channel (FingerPoseMath.GripChannel: from the wrist) in
    // the frame the hand is drawn in at `size` (NativeHandVisual scales the
    // hand round its wrist, NativeHandMesh.WristZ behind that frame's origin).
    internal static Vector3 DrawnChannel(Vector3 canonical,float size)=>new Vector3(0,0,NativeHandMesh.WristZ)+canonical*(float.IsFinite(size)&&size>.3f&&size<3?size:1);
    // Without the hand's own fingers to measure: the channel of the player's
    // hand as logged (HAND REST of 0.1.81) closed round the bazooka's grips
    // (3.6 cm at the hand's 0.75: 0.60 of a fist); the left hand mirrored.
    internal const float FallbackCurl=.6f;
    internal static Vector3 FallbackChannel(bool right,bool trigger)=>trigger?new((right?1:-1)*.0118f,-.0351f,.1019f):new((right?1:-1)*-.0001f,-.0325f,.1022f);
    internal static Vector3 FallbackLittle(bool right,bool trigger)=>trigger?new((right?1:-1)*.9905f,-.0030f,-.1376f):new((right?1:-1)*.9878f,-.1399f,-.0687f);
    // A grip's radius (half its measured thickness), within a hand's reach.
    internal static float GripRadius(GripBarMath.Bar bar)=>float.IsFinite(bar.Thickness)&&bar.Thickness>.01f&&bar.Thickness<.08f?bar.Thickness*.5f:.018f;
    // The game's right hand on the handle (its turn; its place is not used).
    internal static Quaternion RightTurn=>UnityEuler(353.159f,2.004f,276.816f);
    internal const float HandSize=.7456f;
    // Unity's Quaternion.Euler (degrees; z, then x, then y).
    internal static Quaternion UnityEuler(float x,float y,float z)
    {
        const float d=MathF.PI/180;
        return Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY,y*d)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,x*d)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,z*d));
    }
    // 0.1.225: a hand (its root, fitted frame) on that grip: within NearGripReach of its middle
    // (the game's hands are 10-12 cm from the grips' middles; while it is drawn, 20 and more).
    internal const float NearGripReach=.16f;
    internal static bool NearGrip(Vector3 hand,GripBarMath.Bar grip)=>float.IsFinite(hand.X)&&float.IsFinite(hand.Y)&&float.IsFinite(hand.Z)&&Vector3.Distance(hand,grip.Center)<NearGripReach;
    // A grip's bar (fitted frame) from its points: its line from bottom to
    // top (toward the tube: +Y), the way forward (+Z) square to it; a
    // handle's size (4..25 cm long, 1.5..7 cm thick), else none.
    // 0.1.221: trimTop: the share of its length cut off its top (toward the
    // tube) and fitted again - the front grip's bone also carries the bracket
    // that joins it to the tube (14 x 7 cm in a log of 0.1.219: grip and
    // bracket together, the bar above the grip itself).
    internal static GripBarMath.Bar? GripBar(Vector3[] points,float trimTop=0)
    {
        if(points.Length<12)return null;
        var all=new List<int>(points.Length);for(int i=0;i<points.Length;i++)all.Add(i);
        if(!GripBarMath.Shape(points,all,out var center,out var axis,out float length,out float wide,out _))return null;
        if(trimTop>0&&trimTop<.8f)
        {
            if(axis.Y<0)axis=-axis;
            float lo=float.MaxValue,hi=float.MinValue;
            foreach(var p in points){float a=Vector3.Dot(p-center,axis);lo=Math.Min(lo,a);hi=Math.Max(hi,a);}
            float cut=hi-(hi-lo)*trimTop;var kept=new List<int>();
            for(int i=0;i<points.Length;i++)if(Vector3.Dot(points[i]-center,axis)<=cut)kept.Add(i);
            if(kept.Count<12||!GripBarMath.Shape(points,kept,out center,out axis,out length,out wide,out _))return null;
        }
        if(length<.04f||length>.25f||wide<.015f||wide>.07f)return null;
        if(axis.Y<0)axis=-axis;
        if(axis.Y<.3f)return null;
        var toward=Vector3.UnitZ-axis*Vector3.Dot(Vector3.UnitZ,axis);
        if(toward.LengthSquared()<1e-4f)return null;
        return new GripBarMath.Bar(center,axis,Vector3.Normalize(toward),length,wide);
    }
    // The rocket held in the fist as a stick: its line along the grip's line
    // (tail below the little finger, warhead above the thumb), its grip point
    // there; middle/length: the rocket's middle and length (its own frame, +Z
    // to the warhead).
    internal static GripBarMath.Bar HoldBar(Vector3 middle,float length)
    {
        float l=float.IsFinite(length)&&length>.05f?length:.6f;
        return new GripBarMath.Bar(middle+Vector3.UnitZ*(l*(GripFromTail-.5f)),Vector3.UnitZ,Vector3.UnitY,l*.3f,.04f);
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
