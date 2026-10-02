using System;
namespace XiiiXR;
// 0.1.156: the game points each arc from the character's heading;
// the HUD in the headset follows the head. The arc is turned by the angle
// between them, so it points at the enemy from where the player looks.
internal static class SpotMath
{
    // Degrees -180..180.
    internal static float Delta(float from,float to)
    {
        if(!float.IsFinite(from)||!float.IsFinite(to))return 0;
        float d=(to-from)%360f;if(d>180)d-=360;if(d<-180)d+=360;return d;
    }
    // The game's arc angle (gameZ) for an enemy `fromPlayer` degrees right of
    // the character's heading, turned for the enemy `fromHead` degrees right of
    // where the head looks. sign: how the game's angle follows the enemy's
    // (-1: a turn to the right turns the arc clockwise, the UI's usual).
    internal static float Corrected(float gameZ,float fromPlayer,float fromHead,int sign)
    {
        if(!float.IsFinite(gameZ))return gameZ;
        if(sign==0)sign=-1;
        return gameZ+sign*Delta(fromPlayer,fromHead);
    }
    // From two looks at one arc: +1/-1 when the arc clearly followed the
    // enemy's angle that way, 0 when it cannot be told yet.
    internal static int Sign(float z0,float a0,float z1,float a1)
    {
        float da=Delta(a0,a1);if(Math.Abs(da)<10)return 0;
        float r=Delta(z0,z1)/da;
        return r>.6f&&r<1.4f?1:r<-.6f&&r>-1.4f?-1:0;
    }
    // 0.1.159: the game's damage strip (HUDDamageIndicatorComponent) is set
    // once, at the hit, from the game's heading. Its screen angle z0 (the UI
    // turns anticlockwise for a positive angle: a source further right is
    // further clockwise) for a source r0 degrees right of that heading; now
    // the source is rNow degrees right of where the head looks.
    internal static float Strip(float z0,float r0,float rNow)
    {
        if(!float.IsFinite(z0)||!float.IsFinite(r0)||!float.IsFinite(rNow))return z0;
        return z0-Delta(r0,rNow);
    }
    // Which heading the game's angle was measured from: 0 the character's,
    // 1 the head's, -1 neither (more than MatchDegrees off either, either sign).
    internal const float MatchDegrees=25f;
    internal static int Reference(float angles,float fromBody,float fromHead)
    {
        float Off(float r)=>float.IsFinite(r)?Math.Min(Math.Abs(Delta(angles,r)),Math.Abs(Delta(angles,-r))):float.PositiveInfinity;
        float b=Off(fromBody),h=Off(fromHead);
        if(!float.IsFinite(angles)||Math.Min(b,h)>MatchDegrees)return -1;
        return b<=h?0:1;
    }
}
