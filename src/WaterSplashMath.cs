using System;
namespace XiiiXR;
// 0.1.111: when a hand moving into the water makes a splash.
internal static class WaterSplashMath
{
    internal const float MinSpeed=1f;      // m/s
    internal const float MaxUpward=-.25f;  // direction.y: must move down into the water
    internal static bool Slap(float speed,float downward)=>float.IsFinite(speed)&&float.IsFinite(downward)&&speed>=MinSpeed&&downward<=MaxUpward;
    // 0.1.113: splash sounds alternate: the game's own, then the recording.
    internal static bool RecordingTurn(ref int turn){bool recording=(turn&1)==1;turn=(turn+1)&1;return recording;}
    // Louder/bigger with speed: 0.35 at 1 m/s .. 1 at 4 m/s.
    internal static float Strength(float speed)=>float.IsFinite(speed)?Math.Clamp(.35f+(speed-MinSpeed)*.65f/3f,.35f,1f):.35f;
}
