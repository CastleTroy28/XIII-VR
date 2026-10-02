using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.207: the hand's tremor, a fifth of a degree,
// fills a good part of a 3-5 degree scope view. While an eye is at the scope
// the aim is steadied like a held breath: slow small motion (the tremor, the
// body's sway) is smoothed away, a deliberate turn passes almost at once
// (an adaptive "one euro" filter on the rotation), and the steadied aim never
// lags more than MaxLag behind the hands. Off the eye it follows the hands.
internal sealed class ScopeSteadyMath
{
    // The cutoff while still (Hz): about half a second of smoothing.
    internal const float StillCutoff=.35f;
    // How fast the cutoff rises with the aim's turning speed above SpeedFloor
    // (Hz per deg/s): 10 deg/s -> ~2.9 Hz, 30 deg/s -> ~9 Hz. The tremor's own
    // back-and-forth is not a turn: the speed is the trend's (TrendCutoff).
    internal const float Beta=.32f,SpeedFloor=2f,TrendCutoff=1.5f;
    // The cutoff off the eye: the hands' aim as it is.
    internal const float FreeCutoff=60f;
    // The trend's turning speed is smoothed with this cutoff (Hz).
    internal const float SpeedCutoff=4f;
    // At most this far behind the hands (degrees).
    internal const float MaxLag=2.5f;
    // Coming to the eye and leaving it: blended over this long (s).
    internal const float Blend=.25f;
    private bool ready;private Quaternion output=Quaternion.Identity,lastTarget=Quaternion.Identity,trend=Quaternion.Identity;
    private float speed,weight;
    internal float Weight=>weight;
    internal float TurnSpeed=>speed;
    internal void Reset(){ready=false;speed=0;weight=0;}
    internal static float Degrees(Quaternion a,Quaternion b)=>2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b))),0,1))*180/MathF.PI;
    private static float Alpha(float cutoff,float dt)=>1-MathF.Exp(-2*MathF.PI*Math.Max(.01f,cutoff)*dt);
    // strength: 0 off, 1 the default, up to 2 (twice the smoothing).
    internal Quaternion Step(Quaternion target,float dt,bool atEye,float strength=1)
    {
        if(!float.IsFinite(target.LengthSquared())||target.LengthSquared()<.5f){Reset();return target;}
        target=Quaternion.Normalize(target);
        if(Quaternion.Dot(target,lastTarget)<0&&ready)target=-target;
        strength=float.IsFinite(strength)?Math.Clamp(strength,0,2):1;
        if(!ready||!float.IsFinite(dt)||dt<=0||dt>.1f){ready=true;output=lastTarget=trend=target;speed=0;weight=0;return target;}
        weight=Math.Clamp(weight+(atEye&&strength>0?1:-1)*dt/Blend,0,1);
        // The aim's trend (its tremor smoothed away) and how fast that turns (deg/s).
        lastTarget=target;if(Quaternion.Dot(trend,target)<0)trend=-trend;
        var before=trend;trend=Quaternion.Normalize(Quaternion.Slerp(trend,target,Alpha(TrendCutoff,dt)));
        float turn=Degrees(before,trend)/dt;
        speed+=(turn-speed)*Alpha(SpeedCutoff,dt);
        float still=StillCutoff/Math.Max(.25f,strength);
        float steady=still+Beta/Math.Max(.25f,strength)*Math.Max(0,speed-SpeedFloor);
        float cutoff=FreeCutoff+(steady-FreeCutoff)*weight;
        if(Quaternion.Dot(output,target)<0)output=-output;
        output=Quaternion.Normalize(Quaternion.Slerp(output,target,Alpha(cutoff,dt)));
        float lag=Degrees(output,target);
        if(lag>MaxLag)output=Quaternion.Normalize(Quaternion.Slerp(target,output,MaxLag/lag));
        return output;
    }
}
