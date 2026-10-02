using System;
using System.Globalization;
using BepInEx.Configuration;
using NQ=System.Numerics.Quaternion;
using UQ=UnityEngine.Quaternion;
namespace XiiiXR;
internal static class ControllerAim
{
    private static ConfigEntry<float> pitch=null!,trim=null!;
    private static ConfigEntry<string> saved=null!;
    private static NQ correction=NQ.Identity;
    internal static int Revision { get; private set; }
    internal static void Load(ConfigFile config)
    {
        pitch=config.Bind("ControllerAim","InitialPitchDegrees",45f,"Initial downward aim correction for this Pimax setup. Shift+F5 (or VR settings) calibrates a natural forward grip.");
        trim=config.Bind("ControllerAim","DownwardTrimDegrees",8f,"Extra downward weapon/hand pitch. 0 restores the previous angle; range -20..20. The aim calibration resets this trim.");
        saved=config.Bind("ControllerAim","CalibratedRightRotation","","Local XYZW quaternion saved by the aim calibration; clear to use InitialPitchDegrees.");
        correction=AimMath.Pitch(pitch.Value);
        var v=saved.Value.Split(',');
        if(v.Length==4 && float.TryParse(v[0],NumberStyles.Float,CultureInfo.InvariantCulture,out float x) && float.TryParse(v[1],NumberStyles.Float,CultureInfo.InvariantCulture,out float y)
            && float.TryParse(v[2],NumberStyles.Float,CultureInfo.InvariantCulture,out float z) && float.TryParse(v[3],NumberStyles.Float,CultureInfo.InvariantCulture,out float w) && AimMath.Valid(new NQ(x,y,z,w)))
        {
            var q=NQ.Normalize(new NQ(x,y,z,w));
            // 0.1.157: a saved calibration that is no forward grip is dropped (the factory aim again).
            if(AimMath.PlausibleCalibration(q))correction=q;
            else
            {
                Bootstrap.Warn("AIM CALIBRATION saved earlier is not a forward grip ("+AimMath.CalibrationOff(q).ToString("F0",CultureInfo.InvariantCulture)+" degrees off the usual one; the controller was not held forward): dropped, the factory aim is used");
                try{saved.Value="";}catch(Exception){}
            }
        }
        Revision++;
    }
    internal static UQ Rotation(PoseValue raw)
    {
        var q=AimMath.Apply(raw.Rotation,correction*AimMath.Pitch(float.IsFinite(trim.Value)?Math.Clamp(trim.Value,-20,20):0));
        return new UQ(q.X,q.Y,q.Z,q.W);
    }
    // 0.1.128: the same weapon hold for the left controller, mirrored.
    internal static NQ MirroredCorrection()
    {
        var c=NQ.Normalize(correction*AimMath.Pitch(float.IsFinite(trim.Value)?Math.Clamp(trim.Value,-20,20):0));
        return new NQ(c.X,-c.Y,-c.Z,c.W);
    }
    internal static UQ Mirrored(PoseValue raw)
    {
        var q=AimMath.Apply(raw.Rotation,MirroredCorrection());
        return new UQ(q.X,q.Y,q.Z,q.W);
    }
    internal static void Calibrate(CameraRig rig)
    {
        if (!rig.SampleWorldHands(out _,out var right,out _) || !rig.TryWorldHeadRotation(out var head))
            throw new InvalidOperationException("Head/right hand tracking unavailable; repeat the calibration");
        var q=AimMath.Calibrate(right.Rotation,head);
        if(!AimMath.PlausibleCalibration(q))
            throw new InvalidOperationException("the right controller was not held forward ("+AimMath.CalibrationOff(q).ToString("F0",CultureInfo.InvariantCulture)+" degrees off the usual grip); the aim was kept - hold it forward as you would a pistol and try again");
        correction=q;trim.Value=0; Revision++; rig.DisarmTrigger();
        string encoded=string.Join(",",q.X.ToString("R",CultureInfo.InvariantCulture),q.Y.ToString("R",CultureInfo.InvariantCulture),q.Z.ToString("R",CultureInfo.InvariantCulture),q.W.ToString("R",CultureInfo.InvariantCulture));
        try { saved.Value=encoded; }
        catch(Exception ex) { Bootstrap.Warn("AIM CALIBRATION active this session; saving failed: "+ex.Message); return; }
        Bootstrap.Write("AIM CALIBRATION saved; right hand forward grip now aligned with horizontal head direction.");
    }
    internal static void Reset()
    {
        correction=AimMath.Pitch(45);pitch.Value=45;trim.Value=8;saved.Value="";Revision++;
        Bootstrap.Write("AIM RESET saved calibration cleared; factory Pimax grip restored");
    }
}
