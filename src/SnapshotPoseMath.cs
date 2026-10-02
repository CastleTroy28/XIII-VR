using System;
using System.Numerics;
namespace XiiiXR;
internal static class SnapshotPoseMath
{
    internal static void Decompose(Matrix4x4 m,Quaternion fallback,out Vector3 position,out Quaternion rotation,out Vector3 scale)
    {
        var x=Vector3.TransformNormal(Vector3.UnitX,m);var y=Vector3.TransformNormal(Vector3.UnitY,m);var z=Vector3.TransformNormal(Vector3.UnitZ,m);
        position=m.Translation;scale=new Vector3(x.Length(),y.Length(),z.Length());
        if(!NativeHandMesh.Finite(position)||!NativeHandMesh.Finite(scale))throw new InvalidOperationException("Nonfinite snapshot bone");
        if(m.GetDeterminant()<0)scale.X=-scale.X;
        if(Math.Abs(scale.X)<1e-8f||scale.Y<1e-8f||scale.Z<1e-8f)
        {
            if(!float.IsFinite(fallback.LengthSquared())||fallback.LengthSquared()<.1f)throw new InvalidOperationException("Invalid collapsed bone rotation");
            rotation=Quaternion.Normalize(fallback);
            scale=new Vector3(Vector3.Dot(x,Vector3.Transform(Vector3.UnitX,rotation)),Vector3.Dot(y,Vector3.Transform(Vector3.UnitY,rotation)),Vector3.Dot(z,Vector3.Transform(Vector3.UnitZ,rotation)));
        }
        else
        {
            var forward=z/scale.Z;var across=Vector3.Normalize(Vector3.Cross(y/scale.Y,forward));var up=Vector3.Cross(forward,across);
            rotation=Quaternion.CreateFromRotationMatrix(new Matrix4x4(across.X,across.Y,across.Z,0,up.X,up.Y,up.Z,0,forward.X,forward.Y,forward.Z,0,0,0,0,1));
        }
        var rebuilt=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(rotation);
        float error=Vector3.Distance(x,Vector3.TransformNormal(Vector3.UnitX,rebuilt))+Vector3.Distance(y,Vector3.TransformNormal(Vector3.UnitY,rebuilt))+Vector3.Distance(z,Vector3.TransformNormal(Vector3.UnitZ,rebuilt));
        if(!float.IsFinite(error)||error>Math.Max(1e-6f,scale.Length()*.003f))throw new InvalidOperationException("Snapshot bone contains shear");
    }
}
