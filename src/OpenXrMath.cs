using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.181: the mod's own OpenXR (xiii_openxr.dll) into the mod's terms.
internal static class OpenXrMath
{
    // OpenXR is right-handed with -Z forward (like OpenVR); Unity has +Z
    // forward. Z is reflected on both sides, as PoseMath.FromOpenVR does.
    // f: x y z qx qy qz qw from at.
    internal static PoseValue Pose(float[] f,int at)
        =>new PoseValue(new Vector3(f[at],f[at+1],-f[at+2]),Quaternion.Normalize(new Quaternion(-f[at+3],-f[at+4],f[at+5],f[at+6])));
    // OpenVR's GetProjectionRaw from an OpenXR field of view, as OpenComposite
    // gives it (SteamVR's top is the tangent of the angle down).
    internal static EyeFrustum Frustum(float angleLeft,float angleRight,float angleUp,float angleDown)
        =>new EyeFrustum(MathF.Tan(angleLeft),MathF.Tan(angleRight),MathF.Tan(angleDown),MathF.Tan(angleUp));
    // Trigger and grip are analog in OpenXR; their OpenVR button bits are held
    // from 0.55 on and let go below 0.40 (no chatter at the edge).
    internal const float PressAt=.55f,ReleaseAt=.40f;
    internal static bool Held(float value,bool before)=>float.IsFinite(value)&&(before?value>=ReleaseAt:value>=PressAt);
    // bits (xiii_openxr.dll): 1 A/X, 2 B/Y, 4 menu, 8 stick click.
    internal static ulong Buttons(bool trigger,bool grip,int bits)
        =>(trigger?HandControls.Trigger:0)|(grip?HandControls.Grip:0)|((bits&1)!=0?HandControls.A:0)|((bits&2)!=0?HandControls.B:0);
    internal static bool StickClick(int bits)=>(bits&8)!=0;
    internal static string Profile(int id)=>id switch{1=>"Oculus Touch",2=>"Index",3=>"Vive",4=>"WMR",5=>"HP Reverb G2",6=>"simple",7=>"other",_=>"none"};
}
