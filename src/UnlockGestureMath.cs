using System;using System.Numerics;
namespace XiiiXR;
internal sealed class UnlockGestureMath
{
    private Quaternion initial;private Vector3 start;private bool near;private float started;
    private float touchedSince=-1;
    internal void Reset(){near=false;touchedSince=-1;}
    // 0.1.203: the card opens once it has touched the reader for CardHold
    // without leaving it; the old downward swipe still opens too.
    internal const float CardHold=.15f;
    // Touching: a point of the card within CardTouch of the reader's
    // colliders; a reader without colliders, within CardNear of its point.
    internal const float CardTouch=.03f,CardNear=.07f;
    internal bool Card(bool touching,Vector3 position,Vector3 target,float now)
    {
        if(!touching)touchedSince=-1;
        else
        {
            if(touchedSince<0)touchedSince=now;
            if(now-touchedSince>=CardHold)return true;
        }
        return Sample(true,position,Quaternion.Identity,target,now);
    }
    internal bool Sample(bool card,Vector3 position,Quaternion rotation,Vector3 target,float now)
    {
        if(Vector3.DistanceSquared(position,target)>.32f*.32f){near=false;return false;}
        if(!near){initial=Quaternion.Normalize(rotation);start=position;started=now;near=true;return false;}
        if(now-started<.12f)return false;
        if(card)return start.Y-position.Y>=.10f&&new Vector2(start.X-position.X,start.Z-position.Z).Length()<.16f;
        // Swing/twist decomposition about the original controller's forward axis.
        var relative=Quaternion.Normalize(Quaternion.Inverse(initial)*rotation);
        float angle=2*MathF.Atan2(relative.Z,relative.W)*180/MathF.PI;
        if(angle>180)angle-=360;if(angle< -180)angle+=360;
        var forward=Vector3.Transform(Vector3.UnitZ,relative);
        // Unity +Z roll is counter-clockwise as seen from the grip towards the tip.
        return angle<=-45&&angle>=-120&&forward.Z>.75f;
    }
}
