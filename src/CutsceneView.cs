using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.234: the steady cutscene view (VR SETTINGS "Cutscene camera"). The
// film camera no longer turns or tilts the player's view: the view is where
// the film camera is, turned by the player's own head. At each cut (the film
// camera jumps) the view is turned about the vertical only, so that where
// the player looks then is where the film camera looks; between cuts the
// film camera's pans, tilts and shakes are not followed. The head's own
// movement since the cut is added (at most MaxHeadOffset).
// Before 0.1.234 the view was the film camera's alone: it turned and tilted
// the whole view while the head stood still, and turning the head did nothing.
internal sealed class CutsceneView
{
    internal const float CutMeters=.75f,CutDegrees=25f,MaxHeadOffset=.6f;
    private bool active;private float alignYaw;
    private Vector3 headAtCut,lastPosition;private Quaternion lastRotation=Quaternion.Identity;
    internal int Cuts{get;private set;}
    internal bool CutThisCall{get;private set;}
    // How far the film camera jumped at the last cut.
    internal float LastJumpMeters{get;private set;}
    internal float LastJumpDegrees{get;private set;}
    // A new cutscene (or the steady view switched off): the next call is a cut; Cuts counts again.
    internal void Reset(){active=false;CutThisCall=false;Cuts=0;}
    internal bool Active=>active;
    internal (Vector3 position,Quaternion rotation) View(Vector3 filmPosition,Quaternion filmRotation,Vector3 headPosition,Quaternion headRotation)
    {
        filmRotation=Normalized(filmRotation);headRotation=Normalized(headRotation);
        float moved=active?Vector3.Distance(filmPosition,lastPosition):0,turned=active?Angle(filmRotation,lastRotation):0;
        CutThisCall=!active||Cut(moved,turned);
        if(CutThisCall)
        {
            alignYaw=YawDegrees(filmRotation)-YawDegrees(headRotation);headAtCut=headPosition;
            LastJumpMeters=moved;LastJumpDegrees=turned;active=true;Cuts++;
        }
        lastPosition=filmPosition;lastRotation=filmRotation;
        var align=Quaternion.CreateFromAxisAngle(Vector3.UnitY,alignYaw*MathF.PI/180);
        var offset=Limit(headPosition-headAtCut,MaxHeadOffset);
        return (filmPosition+Vector3.Transform(offset,align),Quaternion.Normalize(align*headRotation));
    }
    // A jump in one frame: no camera moves 0.75 m or turns 25 degrees between two frames in a shot.
    internal static bool Cut(float meters,float degrees)=>float.IsFinite(meters)&&float.IsFinite(degrees)?meters>CutMeters||degrees>CutDegrees:true;
    // The heading of a rotation about the vertical (degrees; +: to the right).
    // Looking straight up or down: the heading of its top (or bottom).
    internal static float YawDegrees(Quaternion q)
    {
        var f=Vector3.Transform(Vector3.UnitZ,q);
        if(f.X*f.X+f.Z*f.Z<1e-4f){var u=Vector3.Transform(Vector3.UnitY,q);f=f.Y<0?u:-u;}
        if(f.X*f.X+f.Z*f.Z<1e-8f)return 0;
        return MathF.Atan2(f.X,f.Z)*180/MathF.PI;
    }
    internal static float Angle(Quaternion a,Quaternion b)
    {
        float d=Math.Clamp(Math.Abs(Quaternion.Dot(Normalized(a),Normalized(b))),0,1);
        return 2*MathF.Acos(d)*180/MathF.PI;
    }
    internal static Vector3 Limit(Vector3 v,float max)
    {
        float l=v.Length();
        if(!float.IsFinite(l))return Vector3.Zero;
        return l>max?v*(max/l):v;
    }
    private static Quaternion Normalized(Quaternion q)
    {
        float l=q.Length();
        return float.IsFinite(l)&&l>1e-6f?Quaternion.Normalize(q):Quaternion.Identity;
    }
}
// 0.1.238: cutscenes on a screen that stands still in the room (VR SETTINGS
// "Cutscene camera": screen, the default). The film camera's view, turned and
// tilted as the film turns it, inside the game's own frame; the frame stays
// where it was in front of you when the cutscene began. Turning your head
// looks at another part of that screen; the picture does not follow the head.
// (The steady view of 0.1.234 let the head turn the picture instead, and
// the film's own turns were lost.)
internal static class CutsceneScreen
{
    // The screen: where the head faced when the cutscene began (its heading only: upright and level).
    internal static Quaternion Facing(Quaternion head)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,CutsceneView.YawDegrees(head)*MathF.PI/180);
    // The view: the film camera's turn, and the head's turn away from the screen on top.
    // Anything turned as the film camera (the frame, the subtitles) then stands still in the room.
    internal static Quaternion View(Quaternion film,Quaternion screen,Quaternion head)
    {
        film=Safe(film);screen=Safe(screen);head=Safe(head);
        return Quaternion.Normalize(film*Quaternion.Inverse(screen)*head);
    }
    private static Quaternion Safe(Quaternion q){float l=q.Length();return float.IsFinite(l)&&l>1e-6f?Quaternion.Normalize(q):Quaternion.Identity;}
}
