using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using N=System.Numerics.Vector3;
using Q=System.Numerics.Quaternion;
namespace XiiiXR;
internal sealed class ContactWorld : IContactWorld,IDisposable
{
    private readonly Il2CppStructArray<RaycastHit> hits=new(64);
    private readonly Il2CppReferenceArray<Collider> overlaps=new(64);
    private readonly ContactFilter filter=new();
    private GameObject? queryRoot;
    private SphereCollider? query;
    internal Transform? Player;
    internal ContactPose WeaponPose;
    internal ContactSphere[]? WeaponShape;
    internal bool IncludeWeapon,Access,AccessAmmunition;
    internal ContactPose PeerPose;internal ContactSphere[]? PeerShape;
    internal N AccessPoint,AccessAxis=N.UnitY;
    internal float AccessRadius=.060f;
    // Cache only within one synchronous solve. Moving doors and NPCs are
    // queried again next solve, even if another hand runs in the same frame.
    private readonly Collider?[] candidates=new Collider?[64];
    private readonly Vector3[] candidatePositions=new Vector3[64];
    private readonly Quaternion[] candidateRotations=new Quaternion[64];
    private N broadCenter;private float broadRadius;private int candidateCount;private bool broadValid;
    private float queryRadius=-1;
    private static long broadQueries,overlapQueries,sweepQueries,penetrationQueries,reusedQueries;
    private long solveStarted;private static double solveMs;
    public void BeginSolve(){broadValid=false;candidateCount=0;solveStarted=System.Diagnostics.Stopwatch.GetTimestamp();}
    public void EndSolve(){broadValid=false;if(solveStarted!=0){solveMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-solveStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;solveStarted=0;}}
    private void Radius(float radius){if(query!=null&&queryRadius!=radius){query.radius=radius;queryRadius=radius;}}
    internal static string Report(int frames)
    {
        float n=Math.Max(1,frames);
        string report=FormattableString.Invariant($"contactMs={solveMs/n:F3} contacts/frame=broad:{broadQueries/n:F1},overlap:{overlapQueries/n:F1},sweep:{sweepQueries/n:F1},penetration:{penetrationQueries/n:F1},reused:{reusedQueries/n:F1}");
        broadQueries=overlapQueries=sweepQueries=penetrationQueries=reusedQueries=0;solveMs=0;return report;
    }
    private bool ammunitionProbe;
    public void Probe(bool ammunition)=>ammunitionProbe=ammunition;
    private bool Accessible(ContactSphere s,N probe,float radius)
    {
        if(!Access)return false;
        if(AccessAmmunition)
        {
            // Only the held ammunition can enter the loading corridor; the
            // palm stays solid. Never remove a whole receiver sphere merely
            // because its centre happens to be near the loading port.
            if(!ammunitionProbe)return false;
            var d=probe-AccessPoint;float axial=N.Dot(d,AccessAxis);
            if(axial<-.32f||axial>.12f||(d-AccessAxis*axial).Length()>AccessRadius+radius*.15f)return false;
            var obstacle=WeaponPose.Point(s.Offset)-AccessPoint;
            float along=N.Dot(obstacle,AccessAxis);
            return (obstacle-AccessAxis*along).Length()<.045f+s.Radius&&along>-.20f-s.Radius&&along<.16f+s.Radius;
        }
        return N.Distance(probe,AccessPoint)<.055f && N.Distance(WeaponPose.Point(s.Offset),AccessPoint)<.045f+s.Radius;
    }
    internal Collider? LastObstacle;
    internal ContactWorld()
    {
        queryRoot=new GameObject("XIII disabled collision query shape");queryRoot.layer=2;
        query=queryRoot.AddComponent(Il2CppType.Of<SphereCollider>()).TryCast<SphereCollider>()!;
        query.enabled=false; // This collider can NEVER block walking or move props.
    }
    internal Transform? IgnoredRoot;
    private bool Solid(Collider? c)=>c!=null&&c.enabled&&(IgnoredRoot==null||!c.transform.IsChildOf(IgnoredRoot))&&(Player==null||!c.transform.IsChildOf(Player))&&!filter.Excluded(c);
    public float Sweep(N start,N end,float radius,out N normal)
    {
        normal=N.Zero;var delta=end-start;float length=delta.Length();if(length<1e-6f)return 1;
        sweepQueries++;
        int count=Physics.SphereCastNonAlloc(U(start),radius,U(delta/length),hits,length,~0,QueryTriggerInteraction.Ignore);
        if(count>=hits.Length)return 0;float best=1;
        for(int i=0;i<count;i++)
        {
            var h=hits[i];if(!Solid(h.collider))continue;
            var n=V(h.normal);
            if(h.distance<=.0001f)
            {
                // Unity reports -sweepDirection for an initial overlap. That
                // artificial normal blocks withdrawal in EVERY direction.
                // Recover the actual surface normal before testing motion.
                var c=h.collider!;
                bool penetrating=false;
                if(query!=null)
                {
                    Radius(radius);penetrationQueries++;
                    penetrating=Physics.ComputePenetration(query,U(start),Quaternion.identity,c,c.transform.position,c.transform.rotation,out var separation,out float depth)&&depth>0;
                    if(penetrating)n=V(separation);
                }
                if(!penetrating)
                {
                    if(!ColliderSurface.TryClosest(c,U(start),out var closest))continue;
                    var outward=start-V(closest);
                    if(outward.LengthSquared()>1e-10f)n=N.Normalize(outward);
                    else {normal=-delta/length;return 0;} // Unresolved inside solid: never tunnel.
                    // Broad-phase touching/roundoff with an actual gap is not a contact.
                    if(outward.Length()>radius+.002f)continue;
                }
                if(!ContactSolver.IntoSurface(delta,n))continue;
            }
            float t=Math.Clamp((h.distance-.0015f)/length,0,1);
            if(t<best){best=t;normal=n;LastObstacle=h.collider;}
        }
        if(IncludeWeapon&&WeaponShape!=null)foreach(var s in WeaponShape)
            if(ContactSolver.SphereSweep(start,end,WeaponPose.Point(s.Offset),radius+s.Radius,out float t,out var n)&&t<best)
            {
                // Evaluate access at the contact, not at a far-away tracked
                // endpoint that might already have tunneled through the gun.
                var contact=N.Lerp(start,end,t);
                if(Accessible(s,contact,radius)&&Accessible(s,end,radius))continue;
                best=Math.Max(0,t-.0015f/length);normal=n;
            }
        if(PeerShape!=null)foreach(var part in PeerShape)
            if(ContactSolver.SphereSweep(start,end,PeerPose.Point(part.Offset),radius+part.Radius,out float t,out var n)&&t<best)
            {best=Math.Max(0,t-.0015f/length);normal=n;}
        return best;
    }
    // 0.1.121: broad check of the solver — nothing solid in this sphere (the
    // other hand/gun shapes included; a reload corridor is never "clear").
    public bool Clear(N center,float radius)
    {
        broadValid=false;candidateCount=0;broadQueries++;
        int count=Physics.OverlapSphereNonAlloc(U(center),radius,overlaps,~0,QueryTriggerInteraction.Ignore);
        if(count>=overlaps.Length)return false;
        for(int i=0;i<count;i++)
        {
            var c=overlaps[i];if(c==null||!Solid(c))continue;
            int k=candidateCount++;candidates[k]=c;
            var t=c.transform;candidatePositions[k]=t.position;candidateRotations[k]=t.rotation;
        }
        broadCenter=center;broadRadius=radius;broadValid=true;
        if(candidateCount>0||Access)return false;
        if(IncludeWeapon&&WeaponShape!=null)foreach(var s in WeaponShape)if(N.Distance(center,WeaponPose.Point(s.Offset))<radius+s.Radius)return false;
        if(PeerShape!=null)foreach(var s in PeerShape)if(N.Distance(center,PeerPose.Point(s.Offset))<radius+s.Radius)return false;
        return true;
    }

    public bool Overlap(N center,float radius,out N correction)
    {
        correction=N.Zero;
        bool cached=broadValid&&ContactQueryBounds.Contains(broadCenter,broadRadius,center,radius);
        int count;
        if(cached){count=candidateCount;reusedQueries++;}
        else{overlapQueries++;count=Physics.OverlapSphereNonAlloc(U(center),radius,overlaps,~0,QueryTriggerInteraction.Ignore);if(count>=overlaps.Length){correction=new N(float.NaN);return true;}}
        float depth=0;Radius(radius);
        for(int i=0;i<count;i++)
        {
            var c=cached?candidates[i]:overlaps[i];if(c==null||query==null||!cached&&!Solid(c))continue;
            Vector3 p;Quaternion q;
            if(cached){p=candidatePositions[i];q=candidateRotations[i];}else{var t=c.transform;p=t.position;q=t.rotation;}
            penetrationQueries++;
            if(Physics.ComputePenetration(query,U(center),Quaternion.identity,c,p,q,out var direction,out float amount)
                &&amount>depth){depth=amount;correction=V(direction)*(amount+.0015f);LastObstacle=c;}
        }
        if(IncludeWeapon&&WeaponShape!=null)foreach(var s in WeaponShape)
        {
            if(Accessible(s,center,radius))continue;
            var delta=center-WeaponPose.Point(s.Offset);float d=delta.Length(),penetration=radius+s.Radius-d;
            if(penetration>depth){depth=penetration;correction=(d>.0001f?delta/d:N.UnitX)*(penetration+.0015f);}
        }
        if(PeerShape!=null)foreach(var part in PeerShape)
        {
            var delta=center-PeerPose.Point(part.Offset);float d=delta.Length(),penetration=radius+part.Radius-d;
            if(penetration>depth){depth=penetration;correction=(d>.0001f?delta/d:N.UnitX)*(penetration+.0015f);}
        }
        return depth>0;
    }
    internal static Vector3 U(N p)=>new(p.X,p.Y,p.Z);
    internal static N V(Vector3 p)=>new(p.x,p.y,p.z);
    public void Dispose(){if(queryRoot!=null)UnityEngine.Object.Destroy(queryRoot);queryRoot=null;query=null;WeaponShape=null;}
}
