using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.151: the ceiling fans in the rooms. Reach up to one (a jump,
// the arm stretched) and its blades are outlined in white; the grip then
// takes a blade and the player hangs on it and goes round with it (the body
// turning with it) while the grip is held; let go and he falls.
//  - a ceiling fan: one of the game's looping animations (CustomAnimationTool)
//    turning a part a full circle about an axis near the vertical, 0.25-2 m;
//  - a blade: the disc its parts sweep, not the hub; where the blades are is
//    read from the mesh when the game lets it be read (else the whole disc);
//  - while held the fan turns at most 120 degrees a second (it slows down
//    with the player on it); its own animation takes over again on letting go;
//  - the hand is held on the blade by HandClimbing (the character's velocity).
internal sealed class CeilingFans:IDisposable
{
    internal static CeilingFans? Current;
    private sealed class Fan
    {
        internal CustomAnimationTool Tool=null!;internal Transform Pivot=null!;internal int Axis;internal float Speed;
        internal float Radius,Low,High;internal bool[]? Blades;internal Vector3 LocalAxis=Vector3.up;
        internal readonly List<(Renderer renderer,Mesh mesh)> Parts=new();
        internal readonly List<GameObject> Outline=new();internal bool Built,Lit;internal string Name="";
    }
    internal const float Thickness=.012f;
    private readonly List<Fan> fans=new();private readonly HashSet<int> seen=new();private readonly List<Mesh> shells=new();
    private float nextScan;private int scanEpoch=int.MinValue;private Transform? scene;private Material? back,front;private int reports;
    private Fan? held;private readonly bool[] holding=new bool[2];private readonly Vector3[] anchor=new Vector3[2];
    private float rideAngle,rideSpeed,rideTarget,pendingTurn;private Vector3 baseEuler;
    // 0.1.154: the fan and the blade point each
    // free hand points at; a blade taken that way carries the player to it
    // (pulled up to the fan, the fan standing still meanwhile), then the ride.
    private readonly Fan?[] aimed=new Fan?[2];private readonly Vector3[] aimPoint=new Vector3[2];
    private readonly bool[] pulling=new bool[2];private readonly float[] pullSince=new float[2];
    internal bool Pulling(int side)=>side>=0&&side<2&&pulling[side];
    internal void Arrived(int side){if(side>=0&&side<2&&pulling[side]){pulling[side]=false;Bootstrap.Write("FAN the "+(side==0?"left":"right")+" hand reached the blade: the ride begins");}}
    internal float PullSeconds(int side)=>side>=0&&side<2&&pulling[side]?Time.realtimeSinceStartup-pullSince[side]:0;
    internal bool Holding(bool right)=>holding[right?1:0];
    internal bool Riding=>held!=null;
    internal Transform? Pivot=>held?.Pivot;
    internal CeilingFans(){Current=this;}
    // Every frame (HandClimbing): the fans found, the outline where a free hand reaches one, the ride.
    internal void Tick(Transform? player,bool allowed,Vector3 left,bool leftOk,Vector3 right,bool rightOk)=>Tick(player,allowed,left,Vector3.zero,leftOk,right,Vector3.zero,rightOk);
    internal void Tick(Transform? player,bool allowed,Vector3 left,Vector3 leftAim,bool leftOk,Vector3 right,Vector3 rightAim,bool rightOk)
    {
        if(scene!=player){Clear();scene=player;nextScan=0;}
        // 0.1.162: every 30 s (0.1.164) and after a scene change (was every 5 s).
        if(player!=null&&SceneScan.Due(ref nextScan,30,ref scanEpoch))Scan();
        if(held!=null&&(held.Pivot==null||!allowed)){holding[0]=holding[1]=false;LetGo("not allowed now");}
        if(held!=null)
        {
            float dt=Math.Clamp(Time.deltaTime,0,.1f);
            rideSpeed=FanMath.RideRamp(rideSpeed,rideTarget,pulling[0]||pulling[1],dt);
            float before=rideAngle;rideAngle+=rideSpeed*dt;
            pendingTurn=Math.Clamp(pendingTurn+YawTurn(held,before,rideAngle),-90,90);
        }
        float blink=OutlineShell.Blink(Time.realtimeSinceStartup);bool anyLit=false;
        for(int i=fans.Count-1;i>=0;i--){var f=fans[i];if(f.Tool==null||f.Pivot==null)fans.RemoveAt(i);}
        Aim(0,allowed&&leftOk,left,leftAim);Aim(1,allowed&&rightOk,right,rightAim);
        for(int i=fans.Count-1;i>=0;i--)
        {
            var f=fans[i];
            bool lit=allowed&&held!=f&&(leftOk&&Reaches(f,left)||rightOk&&Reaches(f,right)||aimed[0]==f||aimed[1]==f);
            if(lit!=f.Lit){f.Lit=lit;Show(f,lit);}
            anyLit|=lit;
        }
        if(anyLit){var c=new Color(blink,blink,blink,1);if(back!=null)back.color=c;if(front!=null)front.color=c;}
    }
    // After the game's own animation (LateUpdate): the held fan where the ride has it.
    internal void LateTick()
    {
        var f=held;if(f==null||f.Pivot==null)return;
        try{f.Pivot.localEulerAngles=Euler(rideAngle);}catch(Exception){held=null;holding[0]=holding[1]=false;}
    }
    // The body turns with the fan (LocomotionDriver's turn, degrees).
    internal float ConsumeTurn(){float t=pendingTurn;pendingTurn=0;return t;}
    internal bool TryGrab(int side,Vector3 hand)
    {
        Fan? best=null;var at=hand;bool far=false;
        foreach(var f in fans)if(f.Tool!=null&&f.Pivot!=null&&Reaches(f,hand)){best=f;break;}
        // 0.1.154: the blade the hand points at (outlined), from afar.
        if(best==null&&side>=0&&side<2&&aimed[side] is Fan a&&a.Tool!=null&&a.Pivot!=null){best=a;at=aimPoint[side];far=(at-hand).sqrMagnitude>FanMath.Arrive*FanMath.Arrive;}
        if(best==null||held!=null&&held!=best)return false;
        if(held==null)
        {
            held=best;baseEuler=best.Pivot.localEulerAngles;rideAngle=baseEuler[best.Axis];rideTarget=FanMath.Ride(best.Speed);rideSpeed=far?0:rideTarget;pendingTurn=0;
            Bootstrap.Write("FAN "+best.Name+" taken by the "+(side==0?"left":"right")+" hand"+(far?" pointed at it "+(at-hand).magnitude.ToString("F2")+" m away (the player is pulled up to the blade, the fan still meanwhile)":"")+": the player hangs on it and goes round at "+Math.Abs(rideTarget).ToString("F0")+" degrees a second (its own "+Math.Abs(best.Speed).ToString("F0")+"); let go of the grip to fall");
        }
        holding[side]=true;anchor[side]=Ride(held).inverse.MultiplyPoint3x4(at);
        pulling[side]=far;pullSince[side]=Time.realtimeSinceStartup;
        if(best.Lit){best.Lit=false;Show(best,false);}
        return true;
    }
    // Where the hand that holds the blade has to be (the blade turned by the ride).
    internal bool Anchor(int side,out Vector3 world)
    {
        world=default;var f=held;if(f==null||f.Pivot==null||!holding[side])return false;
        world=Ride(f).MultiplyPoint3x4(anchor[side]);return true;
    }
    internal void Release(int side)
    {
        if(side<0||side>1||!holding[side])return;holding[side]=false;pulling[side]=false;
        if(!holding[0]&&!holding[1])LetGo("the grip let go");
    }
    private void LetGo(string why)
    {
        if(held==null)return;
        Bootstrap.Write("FAN "+held.Name+" let go ("+why+"): the player falls; its own animation turns it again");
        held=null;pendingTurn=0;pulling[0]=pulling[1]=false;
    }
    private Vector3 Euler(float angle){var e=baseEuler;e[held?.Axis??1]=angle;return e;}
    private Matrix4x4 Ride(Fan f)
    {
        var e=baseEuler;e[f.Axis]=rideAngle;
        var local=Matrix4x4.TRS(f.Pivot.localPosition,Quaternion.Euler(e),f.Pivot.localScale);
        var parent=f.Pivot.parent;return parent!=null?parent.localToWorldMatrix*local:local;
    }
    private float YawTurn(Fan f,float from,float to)
    {
        var parent=f.Pivot.parent!=null?f.Pivot.parent.rotation:Quaternion.identity;
        var a=f.LocalAxis;var perp=Math.Abs(a.x)<.9f?Vector3.Cross(a,Vector3.right).normalized:Vector3.Cross(a,Vector3.up).normalized;
        var e0=baseEuler;e0[f.Axis]=from;var e1=baseEuler;e1[f.Axis]=to;
        var w0=Vector3.ProjectOnPlane(parent*Quaternion.Euler(e0)*perp,Vector3.up);var w1=Vector3.ProjectOnPlane(parent*Quaternion.Euler(e1)*perp,Vector3.up);
        if(w0.sqrMagnitude<1e-6f||w1.sqrMagnitude<1e-6f)return 0;
        return Vector3.SignedAngle(w0,w1,Vector3.up);
    }
    private void Aim(int side,bool ok,Vector3 hand,Vector3 direction)
    {
        aimed[side]=null;
        if(!ok||direction.sqrMagnitude<1e-6f)return;
        float best=float.MaxValue;
        foreach(var f in fans)
        {
            if(f==held)continue;
            var c=f.Pivot.position;if((c-hand).sqrMagnitude>(FanMath.PointReach+f.Radius)*(FanMath.PointReach+f.Radius))continue;
            var q=f.Pivot.rotation;
            if(!FanMath.RayHit(new N(hand.x,hand.y,hand.z),new N(direction.x,direction.y,direction.z),new N(c.x,c.y,c.z),new System.Numerics.Quaternion(q.x,q.y,q.z,q.w),
                new N(f.LocalAxis.x,f.LocalAxis.y,f.LocalAxis.z),f.Radius,f.Low,f.High,f.Blades,FanMath.PointReach,out float t)||t>=best)continue;
            best=t;aimed[side]=f;aimPoint[side]=hand+direction.normalized*t;
        }
    }
    private static bool Reaches(Fan f,Vector3 hand)
    {
        var center=f.Pivot.position;var rel=hand-center;
        if(rel.sqrMagnitude>(f.Radius+.6f)*(f.Radius+.6f))return false;
        var rotation=f.Pivot.rotation;var axis=rotation*f.LocalAxis;
        FanMath.Split(new N(rel.x,rel.y,rel.z),new N(axis.x,axis.y,axis.z),out float r,out float h);
        if(!FanMath.InDisc(r,h,f.Radius,f.Low,f.High))return false;
        var local=Quaternion.Inverse(rotation)*rel;
        return FanMath.OnBlade(f.Blades,FanMath.Angle(new N(local.x,local.y,local.z),new N(f.LocalAxis.x,f.LocalAxis.y,f.LocalAxis.z)));
    }
    private void Scan()
    {
        long start=SceneScan.Begin();
        try
        {
            foreach(var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<CustomAnimationTool>()))
            {
                var tool=o.TryCast<CustomAnimationTool>();if(tool==null||!seen.Add(tool.GetInstanceID()))continue;
                try{var f=Read(tool);if(f!=null)fans.Add(f);}
                catch(Exception ex){if(reports++<20)Bootstrap.Warn("FAN "+tool.name+" unreadable: "+ex.Message);}
            }
        }
        finally{SceneScan.End("ceiling fans",start);}
    }
    // A looping animation turning a part a full circle.
    private Fan? Read(CustomAnimationTool tool)
    {
        if(tool.customAnimationQueue==null)return null;
        foreach(var q in tool.customAnimationQueue)
        {
            if(q==null||(int)q.queueSettings!=1||q.animationQueue==null)continue;
            foreach(var e in q.animationQueue)
            {
                if(e?.valueCurveList==null)continue;
                foreach(var c in e.valueCurveList)
                {
                    if(c==null)continue;int effect=(int)c.effect;if(effect<3||effect>5)continue;
                    float range=c.maxValue-c.minValue;if(!float.IsFinite(range)||Math.Abs(range)<300)continue;
                    var pivot=q.objTransform??e.transform??c.trans;if(pivot==null)continue;
                    float speed=e.duration>.01f?range/e.duration:0;
                    return Build(tool,pivot,effect-3,speed);
                }
            }
        }
        return null;
    }
    private Fan? Build(CustomAnimationTool tool,Transform pivot,int axisIndex,float speed)
    {
        var f=new Fan{Tool=tool,Pivot=pivot,Axis=axisIndex,Speed=speed,Name=tool.name};
        // The axis: turning the animated angle by a degree.
        var euler=pivot.localEulerAngles;var e1=euler;e1[axisIndex]+=1;
        (Quaternion.Euler(e1)*Quaternion.Inverse(Quaternion.Euler(euler))).ToAngleAxis(out _,out var parentAxis);
        var parentRotation=pivot.parent!=null?pivot.parent.rotation:Quaternion.identity;
        var axis=(parentRotation*parentAxis).normalized;
        f.LocalAxis=(Quaternion.Inverse(pivot.rotation)*axis).normalized;
        var center=pivot.position;float radius=0,low=float.MaxValue,high=float.MinValue;
        foreach(var o in pivot.GetComponentsInChildren(Il2CppType.Of<MeshRenderer>(),false))
        {
            var r=o.TryCast<MeshRenderer>();if(r==null||!r.enabled)continue;
            var mesh=r.GetComponent(Il2CppType.Of<MeshFilter>())?.TryCast<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
            var b=mesh.bounds;var m=r.transform.localToWorldMatrix;
            for(int k=0;k<8;k++)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                var rel=m.MultiplyPoint3x4(corner)-center;
                FanMath.Split(new N(rel.x,rel.y,rel.z),new N(axis.x,axis.y,axis.z),out float rr,out float hh);
                radius=Math.Max(radius,rr);low=Math.Min(low,hh);high=Math.Max(high,hh);
            }
            f.Parts.Add((r,mesh));
        }
        // Corners of boxes overstate the reach of the blades a little (diagonals).
        radius*=.92f;
        if(f.Parts.Count==0||!FanMath.CeilingFan(new N(axis.x,axis.y,axis.z),radius,speed))
        {
            if(reports++<20)Bootstrap.Write("FAN "+tool.name+" turns but is not a ceiling fan (radius "+radius.ToString("F2")+" m, axis "+axis.ToString("F2")+", "+speed.ToString("F0")+" degrees a second, parts "+f.Parts.Count+")");
            return null;
        }
        f.Radius=radius;f.Low=low;f.High=high;
        f.Blades=ReadBlades(f,center,out string blades);
        Bootstrap.Write("FAN found "+tool.name+" at "+center.ToString("F1")+": blades "+radius.ToString("F2")+" m, "+Math.Abs(speed).ToString("F0")+" degrees a second, axis "+axis.ToString("F2")+", "+blades);
        return f;
    }
    private static bool[]? ReadBlades(Fan f,Vector3 center,out string report)
    {
        var points=new List<N>();var inverse=Quaternion.Inverse(f.Pivot.rotation);bool unreadable=false;
        foreach(var (r,mesh) in f.Parts)
        {
            if(!mesh.isReadable||mesh.vertexCount>60000){unreadable=true;continue;}
            var m=r.transform.localToWorldMatrix;
            foreach(var v in mesh.vertices){var local=inverse*(m.MultiplyPoint3x4(v)-center);points.Add(new N(local.x,local.y,local.z));}
        }
        var blades=FanMath.Blades(points,new N(f.LocalAxis.x,f.LocalAxis.y,f.LocalAxis.z),f.Radius);
        report=blades!=null?FanMath.Count(blades)+" blades read from the mesh":unreadable?"the mesh cannot be read: the whole disc takes a hand":"no blades told apart: the whole disc takes a hand";
        return blades;
    }
    private void Show(Fan f,bool lit)
    {
        try
        {
            if(lit&&!f.Built)Build(f);
            foreach(var o in f.Outline)if(o!=null&&o.activeSelf!=lit)o.SetActive(lit);
        }
        catch(Exception ex){if(reports++<20)Bootstrap.Warn("FAN outline "+f.Name+": "+ex.Message);f.Built=true;}
    }
    // The white outline: each part's mesh a little larger and turned inside
    // out (as the reload outline), or, when the mesh cannot be read, the part
    // itself a little larger with its front faces culled.
    private void Build(Fan f)
    {
        f.Built=true;
        back??=MakeMaterial(CullMode.Back);front??=MakeMaterial(CullMode.Front);
        foreach(var (r,mesh) in f.Parts)
        {
            if(r==null||mesh==null)continue;
            var go=new GameObject("XIII VR fan outline");go.layer=r.gameObject.layer;go.transform.SetParent(r.transform,false);
            var filter=go.AddComponent(Il2CppType.Of<MeshFilter>()).TryCast<MeshFilter>()!;
            var renderer=go.AddComponent(Il2CppType.Of<MeshRenderer>()).TryCast<MeshRenderer>()!;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var s=r.transform.lossyScale;float scale=(Math.Abs(s.x)+Math.Abs(s.y)+Math.Abs(s.z))/3;
            Mesh? shell=null;
            if(mesh.isReadable&&mesh.vertexCount<=60000&&scale>1e-5f)
            {
                var vs=mesh.vertices;var ns=mesh.normals;var points=new N[vs.Length];var normals=new N[ns.Length==vs.Length?vs.Length:0];
                for(int i=0;i<vs.Length;i++)points[i]=new N(vs[i].x,vs[i].y,vs[i].z);
                for(int i=0;i<normals.Length;i++)normals[i]=new N(ns[i].x,ns[i].y,ns[i].z);
                var (outer,faces)=OutlineShell.Build(points,normals.Length>0?normals:null,mesh.triangles,Thickness/scale);
                if(faces.Length>=3)
                {
                    shell=new Mesh{name="XIII fan outline of "+mesh.name};if(outer.Length>65000)shell.indexFormat=IndexFormat.UInt32;
                    var v=new Vector3[outer.Length];for(int i=0;i<v.Length;i++)v[i]=new Vector3(outer[i].X,outer[i].Y,outer[i].Z);
                    shell.vertices=v;shell.triangles=faces;shell.RecalculateNormals();shell.RecalculateBounds();shells.Add(shell);
                }
            }
            if(shell!=null){filter.sharedMesh=shell;renderer.sharedMaterial=back;}
            else
            {
                filter.sharedMesh=mesh;var mats=new Material[Math.Max(1,mesh.subMeshCount)];for(int i=0;i<mats.Length;i++)mats[i]=front;renderer.sharedMaterials=mats;
                const float grow=1.06f;var c=mesh.bounds.center;go.transform.localScale=Vector3.one*grow;go.transform.localPosition=c-c*grow;
            }
            go.SetActive(false);f.Outline.Add(go);
        }
        Bootstrap.Write("FAN outline built for "+f.Name+" ("+f.Outline.Count+" parts)");
    }
    private static Material MakeMaterial(CullMode cull)
    {
        var shader=Shader.Find("Hidden/Internal-Colored")??Shader.Find("Unlit/Color")??throw new InvalidOperationException("no outline shader");
        var m=new Material(shader){hideFlags=HideFlags.DontUnloadUnusedAsset,color=Color.white};
        if(shader.name=="Hidden/Internal-Colored")
        {
            m.SetInt("_Cull",(int)cull);m.SetInt("_ZWrite",1);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);
            m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.Zero);m.renderQueue=2450;
        }
        return m;
    }
    private void Clear()
    {
        holding[0]=holding[1]=false;held=null;pendingTurn=0;pulling[0]=pulling[1]=false;aimed[0]=aimed[1]=null;
        foreach(var f in fans)foreach(var o in f.Outline)if(o!=null)UnityEngine.Object.Destroy(o);
        foreach(var m in shells)if(m!=null)UnityEngine.Object.Destroy(m);shells.Clear();
        fans.Clear();seen.Clear();
    }
    public void Dispose()
    {
        Clear();
        if(back!=null)UnityEngine.Object.Destroy(back);if(front!=null)UnityEngine.Object.Destroy(front);back=front=null;
        if(Current==this)Current=null;
    }
}
