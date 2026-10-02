using System;
namespace XiiiXR;
// Input gestures are sampled once per frame. Rendering never advances these states.
internal sealed class ComfortState
{
    private bool snapArmed,teleportArmed,aiming;
    internal bool Aiming=>aiming;
    internal void Reset(){snapArmed=teleportArmed=aiming=false;}
    internal float Turn(float axis,bool snap,float angle,float speed,float dt)
    {
        if(!float.IsFinite(axis)){snapArmed=false;return 0;}
        if(Math.Abs(axis)<.25f)snapArmed=true;
        if(!snap)return LocomotionState.TurnDegrees(axis,speed,dt);
        if(!snapArmed||Math.Abs(axis)<.65f)return 0;
        snapArmed=false;return MathF.CopySign(float.IsFinite(angle)?Math.Clamp(angle,15,90):30,axis);
    }
    internal bool Teleport(StickSample stick,bool allowed)
    {
        if(!allowed||!stick.Valid||!float.IsFinite(stick.Value.X)||!float.IsFinite(stick.Value.Y))
        {teleportArmed=aiming=false;return false;}
        float length=stick.Value.Length();
        if(stick.Value.Y<-.5f){aiming=teleportArmed=false;return false;}
        if(length<.25f){bool commit=aiming;aiming=false;teleportArmed=true;return commit;}
        if(teleportArmed&&stick.Value.Y>.65f&&Math.Abs(stick.Value.X)<.45f){aiming=true;teleportArmed=false;}
        return false;
    }
}
