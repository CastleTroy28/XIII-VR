using System;using System.Collections.Generic;using System.Linq;using XiiiXR;
using N=System.Numerics;
class WatchAttachmentTests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void CheckMount(UnityEngine.Transform parent,WristFit fit,float scale)
    {
        // Measure the rendered case underside against the native dorsal skin,
        // including the wrist blend seam, at both free and attached-hand sizes.
        var underside=new N.Vector3(0,fit.RadiusY-.0055f,-.11f);
        var localMatrix=N.Matrix4x4.CreateScale(parent.localScale.N)*N.Matrix4x4.CreateFromQuaternion(parent.localRotation.N)*N.Matrix4x4.CreateTranslation(parent.localPosition.N);
        var actual=N.Vector3.Transform(underside,localMatrix);
        var surface=fit.Center+N.Vector3.Transform(N.Vector3.UnitY*(fit.RadiusY+.0025f),fit.Rotation);
        int blend=(int)MathF.Round(ArmIkMath.ForearmWeight(fit.Center.Z)*64);
        var expected=new N.Vector3(0,0,NativeHandMesh.WristZ)+N.Vector3.Transform(surface,N.Quaternion.Slerp(N.Quaternion.Identity,NativeHandVisual.Swing,blend/64f))*scale;
        Check(N.Vector3.Distance(actual,expected)<1e-5,"case underside separates from dorsal bracelet surface");
    }
    static void Main()
    {
        _=new CameraRig();
        using(var hand=new GloveVisual(true))
        {
            var meshes=RigidMeshVisual.All.Skip(1).ToArray();Check(meshes.Length==2&&ReferenceEquals(meshes[0].Parent,meshes[1].Parent),"case and digits have different parents");
            var parent=meshes[0].Parent;
            for(int frame=0;frame<3600;frame++)
            {
                UnityEngine.Time.frameCount=frame;
                var q=N.Quaternion.CreateFromYawPitchRoll(frame*.01f,MathF.Sin(frame*.03f),MathF.Cos(frame*.02f));
                var pose=new PoseValue(new N.Vector3(frame*.002f,1,MathF.Sin(frame*.02f)),q);
                hand.Pose(pose,0,0);
                var matrix=parent.Matrix;
                // UI and second-eye callbacks can provide later tracking samples.
                hand.Pose(new PoseValue(pose.Position+N.Vector3.One,q),1,1);
                Check(parent.Matrix==matrix,"later camera moved hand after first cull");
                var local=HandMeshGeometry.ScreenPosition;
                var world=N.Vector3.Transform(local,parent.Matrix);N.Matrix4x4.Invert(parent.Matrix,out var inverse);
                Check(N.Vector3.Distance(N.Vector3.Transform(world,inverse),local)<3e-6,"digits drift relative to wrist during walk/turn");
            }
            int before=meshes[1].Sets;hand.Readout("<b>10</b>","120");Check(meshes[1].Sets==before+1,"shot readout not rebuilt");hand.Readout("10","120");Check(meshes[1].Sets==before+1,"unchanged readout rebuilds every frame");
            hand.Hide();Check(!parent.parent!.gameObject.activeSelf,"hand remains visible on pause");
            hand.BindNative(new UnityEngine.GameObject("local player").transform);
            InteractionDriver.Current=new();ContactRig.Current=new();UnityEngine.Time.frameCount++;
            hand.Pose(new PoseValue(N.Vector3.Zero,N.Quaternion.Identity),0,0);
            Check(hand.Visible&&NativeHandVisual.Held&&InteractionDriver.Current.Rendered==1,"key hand depends on suspended weapon collision or preview not attached");InteractionDriver.Current=null;ContactRig.Current=null;
            hand.BindNative(new UnityEngine.GameObject("local player").transform);UnityEngine.Time.frameCount++;hand.Pose(new PoseValue(N.Vector3.Zero,N.Quaternion.Identity),0,0);Check(hand.Visible,"bound native hand is not visible");
        }
        using(var hand=new GloveVisual(false))
        {
            hand.BindNative(new UnityEngine.GameObject("replacement player").transform);
            var weapons=new WeaponHands();WeaponHands.Current=weapons;
            var parent=RigidMeshVisual.All[^1].Parent;var controller=new PoseValue(new N.Vector3(0,1,.2f),N.Quaternion.Identity);
            weapons.Attached=true;UnityEngine.Time.frameCount++;hand.Pose(controller,1,0);
            Check(parent.parent!.position.N==weapons.Anchor && NativeHandVisual.Held,"support grip does not move hand and bracelet together");
            Check(parent.localScale.N==new N.Vector3(.76f),"watch does not scale with native grip hand");
            CheckMount(parent,WristFit.Default,.76f);
            weapons.Attached=false;UnityEngine.Time.frameCount++;hand.Pose(controller,0,0);
            Check(parent.parent!.position.N==controller.Position && !NativeHandVisual.Held,"released support hand remains on gun");
            weapons.Attached=true;UnityEngine.Time.frameCount++;hand.Pose(controller,1,0);
            Check(parent.parent!.position.N==weapons.Anchor,"support cannot reattach after release");
            WeaponHands.Current=null;
        }
        using(var carryHand=new GloveVisual(false))
        {
            carryHand.BindNative(new UnityEngine.GameObject("held neck").transform);
            GripCarry.Current=new();WeaponHands.Current=new(){Attached=true};ContactRig.Current=new();
            for(int f=0;f<50;f++)
            {
                UnityEngine.Time.frameCount++;
                carryHand.Pose(new PoseValue(new N.Vector3(f,5,-2),N.Quaternion.CreateFromYawPitchRoll(f,f*.3f,f*.8f)),0,0);
                Check(carryHand.Attachment!.position.N==GripCarry.Current.Hand.N,"controller/weapon/contact stole neck hand");
                Check(NativeHandVisual.Held,"carry hand used open idle fingers");
            }
            GripCarry.Current=null;WeaponHands.Current=null;ContactRig.Current=null;UnityEngine.Time.frameCount++;
            carryHand.Pose(new PoseValue(N.Vector3.Zero,N.Quaternion.Identity),0,0);
            Check(carryHand.Attachment!.position.N==N.Vector3.Zero,"released hand stayed stuck to neck");
        }
        foreach(float distance in new[]{.010f,.020f,.052f})using(var hand=new GloveVisual(false))
        {
            NativeHandVisual.Mount=new WristFit(new N.Vector3(0,0,-distance),N.Quaternion.Identity,.025f,.026f);
            hand.BindNative(new UnityEngine.GameObject("native band near wrist seam").transform);UnityEngine.Time.frameCount++;
            hand.Pose(new PoseValue(N.Vector3.Zero,N.Quaternion.Identity),0,0);
            var parent=RigidMeshVisual.All[^1].Parent;
            CheckMount(parent,NativeHandVisual.Mount,1);
        }
        Check(HandMeshGeometry.BuildWatch(false,.025f,.026f,false).Vertices.Count<HandMeshGeometry.BuildWatch(false,.025f,.026f,true).Vertices.Count,"native band mode retains extra ring");
        // Tilted watch face must not tilt the enclosing wrist strap.
        var tilted=new WristFit(new N.Vector3(.01f,-.01f,-.052f),N.Quaternion.Identity,.03f,.04f,
            new N.Vector3(.02f,.04f,-.052f),N.Vector3.Normalize(new N.Vector3(.5f,1,0)));
        var band=WatchMountMath.BandPose(tilted,N.Quaternion.Identity,1);
        var bandCenter=band.position+N.Vector3.Transform(new N.Vector3(0,-.008f,-.11f),band.rotation);
        Check(N.Vector3.Distance(bandCenter,tilted.Center+new N.Vector3(0,0,NativeHandMesh.WristZ))<1e-6,"band not centered on skin section");
        Check(Math.Abs(N.Vector3.Dot(N.Vector3.Transform(N.Vector3.UnitY,band.rotation),N.Vector3.UnitY)-1)<1e-6,"watch tilt rotates wrist strap");
        Check(RigidMeshVisual.All.All(x=>x.Disposed)&&NativeHandVisual.Disposed,"owned render resources survive cleanup");
        Console.WriteLine("PASS: production hand root shares case/digits; 3600 walking/turning frames; repeated camera/eye callbacks cannot change pose; readout caching; hide/rebind/dispose.");
        Console.WriteLine("Simulated Unity transforms/native skin; actual XR rendering requires a headset test.");
    }
}
namespace XiiiXR
{
    internal class GripCarry{internal static GripCarry? Current;internal UnityEngine.Vector3 Hand=new(.12f,1.3f,.56f);internal bool TryCarryHand(bool right,out UnityEngine.Vector3 p,out UnityEngine.Quaternion q,out UnityEngine.Vector3 e){p=Hand;q=new(N.Quaternion.Identity);e=new(-.15f,1.3f,.56f);return !right;}}
    internal sealed class InteractionDriver{internal static InteractionDriver? Current;internal bool KeyActive=>true;internal string KeyGripProfile=>"key";internal void PrepareKeyHand(NativeHandVisual hand){}internal bool KeyFistHand(NativeHandVisual hand,ref UnityEngine.Vector3 p,ref UnityEngine.Quaternion q)=>false;internal int Rendered;internal void RenderKey(UnityEngine.Transform? t){Rendered++;}}
    internal readonly record struct PoseValue(N.Vector3 Position,N.Quaternion Rotation);
    internal sealed class CameraRig
    {internal static CameraRig? Current;internal UnityEngine.Vector3 HeadPosition=new(0,2,0);internal UnityEngine.Quaternion HeadRotation=new(N.Quaternion.Identity);internal CameraRig(){Current=this;}internal static UnityEngine.Vector3 UnityPosition(PoseValue p)=>UnityEngine.Vector3.From(p.Position);internal static UnityEngine.Quaternion UnityRotation(PoseValue p)=>new(p.Rotation);}
    internal static class ControllerAim{internal static UnityEngine.Quaternion Rotation(PoseValue p)=>CameraRig.UnityRotation(p);}
    internal static class FramePerformance{internal static long Begin()=>0;internal static void End(long t,int c){}}
    internal static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
    internal sealed class NativeHandVisual:IDisposable
    {internal void ResetRenderPose(){}internal static bool Disposed;internal bool Valid=>true;internal bool BelongsTo(UnityEngine.Transform player)=>true;internal static NativeHandVisual Create(UnityEngine.Transform a,UnityEngine.Transform b,bool right)=>new();internal static bool Held;internal static N.Quaternion Swing=>N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitX,.3f);internal N.Quaternion ForearmSwing=>Swing;internal bool NativeBand=>true;internal static WristFit Mount=WristFit.Default;internal WristFit Wrist=>Mount;internal N.Vector3 ForearmRest=>new(0,0,-.25f);internal void Refresh(bool held,float size,float g,float t,string p,N.Vector3 e){Held=held;}public void Dispose(){Disposed=true;}internal bool TryCropWorld(out UnityEngine.Vector3 c,out UnityEngine.Vector3 o,out UnityEngine.Vector3 u){c=o=u=UnityEngine.Vector3.zero;return false;}}
    internal sealed class ContactRig {internal static ContactRig? Current;internal bool ResolveHand(bool r,bool a,ref UnityEngine.Vector3 p,ref UnityEngine.Quaternion q,bool closedFist=false)=>!a;internal void ResetHand(bool r){}}
    internal sealed class WeaponHands
    {
        internal static bool LeftHanded=>false;internal bool LeftPistolVisible=>false;internal bool LeftReloadHolding=>false;internal bool ReloadHandHolding(bool right)=>false;internal string Profile=>"pistol";internal string HandProfile=>Profile;internal string? HandProfileFor(bool right)=>null;internal static WeaponHands? Current;internal bool Attached;internal N.Vector3 Anchor=new(.1f,1.2f,.4f);
        internal const string LongHandleGrip="prop_long_handle";
        internal void RegisterHand(NativeHandVisual native,bool right){}
        internal void PoseHeldAmmunition(bool right,UnityEngine.Vector3 p,UnityEngine.Quaternion q){}
        internal void RestOnRail(bool right,ref UnityEngine.Vector3 p,UnityEngine.Quaternion q){}
        internal bool TryPoseHand(NativeHandVisual n,bool r,out UnityEngine.Vector3 p,out UnityEngine.Quaternion q,out float size)
        {p=UnityEngine.Vector3.From(Anchor);q=new UnityEngine.Quaternion(N.Quaternion.Identity);size=.76f;return Attached;}
    }
    internal sealed class RigidMeshVisual:IDisposable
    {internal static List<RigidMeshVisual> All=new();internal UnityEngine.Transform Parent;internal int Sets;internal bool Disposed;internal RigidMeshVisual(UnityEngine.Transform p,string n,bool d=false){Parent=p;All.Add(this);}internal void Set(HandMeshGeometry d){Sets++;}internal void Show(bool b){}public void Dispose(){Disposed=true;}}
}
namespace UnityEngine
{
    class Object{internal static void Destroy(Object o){}}
    class GameObject:Object
    {internal bool activeSelf=true;internal int layer;internal Transform transform;internal GameObject(string n){transform=new Transform(this);}internal void SetActive(bool x){activeSelf=x;}}
    class Transform
    {
        internal GameObject gameObject;internal Transform(GameObject g){gameObject=g;}internal Transform? parent;
        internal Vector3 localPosition,localScale=Vector3.one;internal Quaternion localRotation=new(N.Quaternion.Identity);
        internal Vector3 position {get=>Vector3.From(Matrix.Translation);set=>localPosition=value;}
        internal Quaternion rotation {get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=value;}
        internal void SetParent(Transform p,bool world){parent=p;}
        internal N.Matrix4x4 Matrix=>N.Matrix4x4.CreateScale(localScale.N)*N.Matrix4x4.CreateFromQuaternion(localRotation.N)*N.Matrix4x4.CreateTranslation(localPosition.N)*(parent?.Matrix??N.Matrix4x4.Identity);
        internal void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}internal Vector3 up=>Vector3.From(N.Vector3.Transform(N.Vector3.UnitY,rotation.N));
        internal Vector3 TransformPoint(Vector3 p)=>Vector3.From(N.Vector3.Transform(p.N,Matrix));
    }
    readonly struct Vector3
    {internal readonly N.Vector3 N;internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal static Vector3 From(N.Vector3 p)=>new(p.X,p.Y,p.Z);internal static Vector3 one=>new(1,1,1);internal static Vector3 zero=>new(0,0,0);internal Vector3 normalized=>N.LengthSquared()>0?From(System.Numerics.Vector3.Normalize(N)):new(0,0,0);public static Vector3 operator*(Vector3 a,float b)=>From(a.N*b);public static Vector3 operator+(Vector3 a,Vector3 b)=>From(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>From(a.N-b.N);internal static float Dot(Vector3 a,Vector3 b)=>System.Numerics.Vector3.Dot(a.N,b.N);internal static float Distance(Vector3 a,Vector3 b)=>System.Numerics.Vector3.Distance(a.N,b.N);}
    readonly struct Quaternion
    {internal readonly N.Quaternion N;internal static Quaternion identity=>new(System.Numerics.Quaternion.Identity);internal Quaternion(float x,float y,float z,float w){N=new(x,y,z,w);}internal Quaternion(N.Quaternion n){N=n;}internal Vector3 eulerAngles=>new(0,0,0);internal static Quaternion Inverse(Quaternion q)=>new(System.Numerics.Quaternion.Inverse(q.N));public static Vector3 operator*(Quaternion q,Vector3 v)=>Vector3.From(System.Numerics.Vector3.Transform(v.N,q.N));internal static Quaternion Euler(float x,float y,float z)=>new(System.Numerics.Quaternion.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));public static Quaternion operator*(Quaternion a,Quaternion b)=>new(a.N*b.N);}
    static class Time{internal static int frameCount;internal static float realtimeSinceStartup=>frameCount/90f;internal static float unscaledDeltaTime=>1/90f;}
    static class Mathf{internal static float Clamp01(float x)=>Math.Clamp(x,0,1);}
}

namespace XiiiXR{internal class GameUiControls{internal static GameUiControls? Current=>null;internal bool ItemHeldOn(bool right)=>false;internal bool PendingConsumable=>false;internal bool LeftItemHeld=>false;internal WheelItems Items=>new();}internal class WheelItems{internal bool ArmHeld(bool right)=>false;internal HandToolKind Kind=>HandToolKind.None;internal string GripProfile=>"";internal bool PinchHeld=>false;internal void PrepareHand(NativeHandVisual h){}internal bool FistHand(NativeHandVisual h,ref UnityEngine.Vector3 p,ref UnityEngine.Quaternion q)=>false;internal bool TryToolHand(bool right,PoseValue pose,out UnityEngine.Vector3 p,out UnityEngine.Quaternion q){p=default;q=default;return false;}}internal class ZiplineVr{internal static ZiplineVr? Current=>null;internal bool HidesHand(bool right)=>false;}internal class GrappleVr{internal static GrappleVr? Current=>null;internal int Side=0;internal string HandProfileFor(bool right)=>"dual_pistol";internal string HandProfile=>"dual_pistol";internal bool DeviceShown=>false;}}
