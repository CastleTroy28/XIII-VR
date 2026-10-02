using System;
using System.Linq;
using System.Numerics;
namespace XiiiXR;
// Numerics uses row vectors. These are the same transforms used by the adapter,
// so imported unit scales and wrist motion can be tested without Unity/VR.
internal static class NativeHandMath
{
    internal static Matrix4x4 Frame(Vector3 wrist,Vector3 elbow,Vector3 thumb,Vector3[] fingers,bool right)
    {
        var directions=fingers.Select(p=>p-wrist).Where(p=>NativeHandMesh.Finite(p)&&p.LengthSquared()>1e-14f).ToArray();
        if(directions.Length==0)throw new InvalidOperationException("No native finger joints");
        float longest=directions.Max(p=>p.Length());
        var forward=Vector3.Zero;
        foreach(var d in directions)if(d.Length()>longest*.7f)forward+=Vector3.Normalize(d);
        if(forward.LengthSquared()<.001f)forward=wrist-elbow;
        forward=Vector3.Normalize(forward);
        var up=Vector3.Cross(forward,thumb-wrist)*(right?-1:1);
        if(!NativeHandMesh.Finite(forward)||up.LengthSquared()<1e-16f)throw new InvalidOperationException("Degenerate native hand frame");
        up=Vector3.Normalize(up);var across=Vector3.Normalize(Vector3.Cross(up,forward));up=Vector3.Cross(forward,across);
        var rotation=new Matrix4x4(across.X,across.Y,across.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1);
        float length=directions.Max(p=>Vector3.Dot(p,forward));
        if(!float.IsFinite(length)||length<1e-6f||length>1e6f)throw new InvalidOperationException("Invalid native finger span "+length);
        return Matrix4x4.CreateTranslation(-wrist)*Matrix4x4.Transpose(rotation)*Matrix4x4.CreateScale(.17f/length);
    }
    internal static Matrix4x4 AnimatedFrame(Matrix4x4 restFrame,Matrix4x4 restWrist,Matrix4x4 liveWrist)
    {
        if(!Matrix4x4.Invert(liveWrist,out var inverse))throw new InvalidOperationException("Singular live wrist");
        return inverse*restWrist*restFrame;
    }
    internal static bool TryGrip(Matrix4x4 canonicalToWeapon,out Vector3 position,out Quaternion rotation,out float size)
    {
        position=Vector3.Zero;rotation=Quaternion.Identity;size=1;
        if(!Matrix4x4.Decompose(canonicalToWeapon,out var scale,out rotation,out var wrist))return false;
        size=(scale.X+scale.Y+scale.Z)/3;
        if(!float.IsFinite(size)||size<.4f||size>2 || !NativeHandMesh.Finite(wrist)||wrist.Length()>1.5f
            || Math.Abs(scale.X-size)>size*.02f||Math.Abs(scale.Y-size)>size*.02f||Math.Abs(scale.Z-size)>size*.02f)return false;
        var rebuilt=Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateScale(size);
        float residual=Vector3.Distance(Vector3.TransformNormal(Vector3.UnitX,canonicalToWeapon),Vector3.TransformNormal(Vector3.UnitX,rebuilt))
            +Vector3.Distance(Vector3.TransformNormal(Vector3.UnitY,canonicalToWeapon),Vector3.TransformNormal(Vector3.UnitY,rebuilt))
            +Vector3.Distance(Vector3.TransformNormal(Vector3.UnitZ,canonicalToWeapon),Vector3.TransformNormal(Vector3.UnitZ,rebuilt));
        if(!float.IsFinite(residual)||residual>size*.025f)return false;
        // Watch stays at unit scale. Scale the hand around the wrist, not around
        // the controller origin, so its cuff still meets the existing bracelet.
        position=wrist-Vector3.Transform(new Vector3(0,0,NativeHandMesh.WristZ),rotation);
        return true;
    }
}
