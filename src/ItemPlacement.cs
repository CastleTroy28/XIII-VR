using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.197: a placement read back from a matrix that may mirror (a thing on
// the game's mirrored left wrist, or a hold mirrored into the other hand):
// its turn and its size, the size's x negative for a mirror image, so that
// turn and size rebuild the very same matrix.
internal static class ItemPlacement
{
    // x, y, z: the matrix's three columns (the images of the thing's own axes).
    internal static (Quaternion rotation,Vector3 scale)? Decompose(Vector3 x,Vector3 y,Vector3 z)
    {
        float sx=x.Length(),sy=y.Length(),sz=z.Length();
        if(!float.IsFinite(sx+sy+sz)||!(sx>1e-6f&&sy>1e-6f&&sz>1e-6f))return null;
        if(Vector3.Dot(Vector3.Cross(x,y),z)<0)sx=-sx;
        var ax=x/sx;var ay=y/sy;var az=z/sz;
        var m=new Matrix4x4(ax.X,ax.Y,ax.Z,0,ay.X,ay.Y,ay.Z,0,az.X,az.Y,az.Z,0,0,0,0,1);
        var q=Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
        if(!float.IsFinite(q.X+q.Y+q.Z+q.W))return null;
        return (q,new Vector3(sx,sy,sz));
    }
}
