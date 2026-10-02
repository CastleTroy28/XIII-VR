using UnityEngine;
namespace XiiiXR;
internal static class ColliderSurface
{
    internal static bool TryClosest(Collider c,Vector3 from,out Vector3 point)
    {
        // ClosestPoint is not supported for non-convex meshes/terrain in this player.
        // Calling it produced hundreds of native errors and bogus contact normals.
        point=from;
        if(c.TryCast<BoxCollider>()==null&&c.TryCast<SphereCollider>()==null&&c.TryCast<CapsuleCollider>()==null
            &&c.TryCast<MeshCollider>()?.convex!=true)return false;
        point=c.ClosestPoint(from);return true;
    }
}
