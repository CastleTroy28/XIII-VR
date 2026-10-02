using System;using System.Numerics;
namespace XiiiXR;
internal static class PropGripGeometry
{
    internal static bool Controlled(string profile)=>profile.Contains("ashtray")||profile.Contains("chair")||profile.Contains("stool");
    internal static (Vector3 handle,Quaternion rotation,Vector3 contact) Fit(string profile,Vector3 min,Vector3 max)
    {
        var center=(min+max)*.5f;
        if(profile.Contains("ashtray"))
            // Native ashtray in barrel space: X wide, Y long, Z thin.
            // Put its nearest rim under the fingertips, with the bowl facing up.
            return (new Vector3(center.X,max.Y,center.Z),Quaternion.CreateFromAxisAngle(Vector3.UnitX,-MathF.PI/2),new Vector3(0,-.044f,.073f));
        // Chair's long Z axis runs from the feet to the back rail in this mesh.
        // Rotate feet (-Z) towards +Z; grasp the middle of the back's top rail.
        return (new Vector3(center.X,max.Y-(max.Y-min.Y)*.04f,max.Z-(max.Z-min.Z)*.035f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI),new Vector3(0,-.025f,.073f));
    }
}
