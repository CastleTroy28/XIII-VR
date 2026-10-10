using System;
using System.Numerics;
using XiiiXR;
internal static class HeadingGeometryTests
{
    static void Check(bool b,string text) { if(!b) throw new Exception(text); }
    static void Close(Vector2 a,Vector2 b,string text) => Check(Vector2.Distance(a,b)<.0001f,text);
    static void Close(Vector3 a,Vector3 b,string text) => Check(Vector3.Distance(a,b)<.0002f,text);
    static void Reject(Action f) { bool caught=false; try { f(); } catch(InvalidOperationException) { caught=true; } Check(caught,"invalid bounds accepted"); }
    static Quaternion Q(float yaw,float pitch=0,float roll=0) => Quaternion.CreateFromYawPitchRoll(yaw,pitch,roll);
    static void Main()
    {
        float h=0; var forward=Vector2.UnitY; float half=MathF.PI*.5f;
        Close(HeadingMath.InBodyFrame(forward,Q(half),Q(0),ref h),Vector2.UnitX,"look right + forward must move world right");
        Close(HeadingMath.InBodyFrame(forward,Q(-half),Q(0),ref h),-Vector2.UnitX,"look left + forward");
        Close(HeadingMath.InBodyFrame(forward,Q(MathF.PI),Q(0),ref h),-forward,"look behind + forward");
        Close(HeadingMath.InBodyFrame(forward,Q(half,1.1f,.7f),Q(0),ref h),Vector2.UnitX,"pitch or roll changes horizontal heading");
        Close(HeadingMath.InBodyFrame(-forward,Q(half),Q(0),ref h),-Vector2.UnitX,"backwards relative to gaze");
        Close(HeadingMath.InBodyFrame(Vector2.UnitX,Q(half),Q(0),ref h),-forward,"strafe relative to gaze");
        Close(HeadingMath.InBodyFrame(forward,Q(half),Q(half),ref h),forward,"body turn was counted twice");
        Close(HeadingMath.InBodyFrame(forward,Q(MathF.PI),Q(half),ref h),Vector2.UnitX,"head turn after body turn");
        h=half;
        Close(HeadingMath.InBodyFrame(forward,Q(.1f,half),Q(0),ref h),Vector2.UnitX,"straight up loses last horizontal direction");
        Close(HeadingMath.InBodyFrame(forward,new Quaternion(float.NaN,0,0,1),Q(0),ref h),Vector2.UnitX,"invalid quaternion should preserve heading");
        var stick=new Vector2(.3f,.6f);
        for(int i=-180;i<=180;i++) { var r=HeadingMath.InBodyFrame(stick,Q(i*MathF.PI/180,1),Q(.7f),ref h); Check(MathF.Abs(r.Length()-stick.Length())<.0001f,"rotation changes analog speed"); }
        Console.WriteLine("PASS: head-relative forward/back/strafe; body turn counted once; pitch/roll, vertical fallback and analog speed.");
        foreach(var profile in new[]{"pistol","shotgun","ak47"})
        foreach(float scale in new[]{.01f,1f,100f})
        foreach(var offset in new[]{Vector3.Zero,new Vector3(2,-4,7)})
        {
            var min=new Vector3(-.04f,-.14f,-.22f)*scale+offset; var max=new Vector3(.04f,.04f,0)*scale+offset;
            var fit=WeaponGeometry.Fit(min,max,profile); var size=max-min;
            var anchor=new Vector3((min.X+max.X)*.5f,max.Y-size.Y*.22f,max.Z);
            Close(fit.Point(anchor),fit.Muzzle,"remote prefab pivot displaced fitted muzzle");
            float expected=EquipmentProfile.Length(profile);   // 0.1.257: the AK 10% larger (EquipmentProfile.Growth)
            Check(MathF.Abs((fit.Point(max)-fit.Point(min)).Z-expected)<.0002f,"inherited scale changed physical length");
            Check(fit.Point(min).Length()<2 && fit.Point(max).Length()<2,"copy remains far from controller");
        }
        Reject(()=>WeaponGeometry.Fit(Vector3.Zero,Vector3.Zero,"pistol"));
        Reject(()=>WeaponGeometry.Fit(Vector3.Zero,new Vector3(float.NaN,1,1),"pistol"));
        Reject(()=>WeaponGeometry.Fit(Vector3.Zero,new Vector3(20,1,1),"pistol"));
        Console.WriteLine("PASS: pistol/shotgun/AK mesh fit bounds controller distance across inherited scales and remote pivots; rejects degenerate/oversized geometry.");
    }
}
