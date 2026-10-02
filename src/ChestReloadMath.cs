using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.183: a pistol reloaded with its own hand while both hands
// hold weapons - its grip struck against the chest puts a full magazine in.
// The torso frame is the body places' one (HolsterLayout): from the eyes,
// x right, y up, z forward, turned with the head's heading only; metres.
internal static class ChestReloadMath
{
    // The chest: across the body, from the collarbones to above the belt,
    // from a hand's width behind the eyes to a little in front of the chest
    // (the grip is struck there, the controller stops at the body).
    internal const float HalfWidth=.22f,Top=-.10f,Bottom=-.55f,Front=.24f,Back=-.12f;
    // Out this far past the chest's edge the next strike is armed again.
    internal const float Margin=.03f;
    // Towards the body at least this fast (m/s) as the grip comes into the chest.
    internal const float MinApproach=.35f;
    // More than this in one frame is a turn or a jump of the body, not a strike.
    internal const float MaxStep=.30f;
    // Where the faint glow shows while a magazine is out (in front of the sternum).
    internal static readonly Vector3 GlowPoint=new(0,-.28f,.14f);
    internal static bool Inside(Vector3 p,float margin=0)=>
        MathF.Abs(p.X)<=HalfWidth+margin&&p.Y<=Top+margin&&p.Y>=Bottom-margin&&p.Z<=Front+margin&&p.Z>=Back-margin;
    // How fast a point moves towards the body (backwards in the torso frame).
    internal static float Approach(Vector3 before,Vector3 now,float dt)=>dt>.0001f?-(now.Z-before.Z)/dt:0;
    // The head's heading (yaw only) from where it looks; while it looks
    // nearly straight down the last heading stays.
    internal static bool Heading(Vector3 forward,out float yaw)
    {
        yaw=0;
        if(!float.IsFinite(forward.X)||!float.IsFinite(forward.Z)||forward.X*forward.X+forward.Z*forward.Z<=.20f)return false;
        yaw=MathF.Atan2(forward.X,forward.Z);return true;
    }
    // A world point in the torso frame (the eyes at head, turned by yaw about +y).
    internal static Vector3 Local(Vector3 point,Vector3 head,float yaw)
    {
        var d=point-head;float c=MathF.Cos(yaw),s=MathF.Sin(yaw);
        return new Vector3(c*d.X-s*d.Z,d.Y,s*d.X+c*d.Z);
    }
    internal static Vector3 World(Vector3 local,Vector3 head,float yaw)
    {
        float c=MathF.Cos(yaw),s=MathF.Sin(yaw);
        return head+new Vector3(c*local.X+s*local.Z,local.Y,-s*local.X+c*local.Z);
    }
}
// One hand's grip against the chest: a strike is the grip coming into the
// chest moving towards the body; the next one only after it was out again.
internal sealed class ChestStrike
{
    private Vector3 last;private float lastAt=-1;private bool armed;
    internal bool Armed=>armed;
    // A new magazine out (or the hand changed): the grip must leave the chest first.
    internal void Reset(){lastAt=-1;armed=false;}
    internal bool Step(Vector3 local,float now)
    {
        if(!float.IsFinite(local.X)||!float.IsFinite(local.Y)||!float.IsFinite(local.Z)||!float.IsFinite(now)){lastAt=-1;return false;}
        bool had=lastAt>=0;float dt=now-lastAt;var before=last;last=local;lastAt=now;
        if(!ChestReloadMath.Inside(local,ChestReloadMath.Margin)){armed=true;return false;}
        if(!armed||!had||!(dt>.0001f&&dt<.1f)||!ChestReloadMath.Inside(local))return false;
        if(Vector3.Distance(before,local)>ChestReloadMath.MaxStep)return false;
        if(ChestReloadMath.Approach(before,local,dt)<ChestReloadMath.MinApproach)return false;
        armed=false;return true;
    }
}
