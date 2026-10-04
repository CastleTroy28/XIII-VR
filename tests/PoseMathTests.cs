using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Valve.VR;
using XiiiXR;
internal static class PoseMathTests
{
    static void Check(bool condition,string name) { if (!condition) throw new Exception(name); }
    static bool Near(Vector3 a,Vector3 b) => Vector3.Distance(a,b)<0.0001f;
    static bool SameRotation(Quaternion a,Quaternion b) => MathF.Abs(Quaternion.Dot(a,b))>0.99999f;
    static HmdMatrix34_t Matrix(float yaw, float x=0,float y=0,float z=0)
    {
        float c=MathF.Cos(yaw),s=MathF.Sin(yaw);
        return new HmdMatrix34_t {m0=c,m2=s,m3=x,m5=1,m7=y,m8=-s,m10=c,m11=z};
    }
    public static void Main()
    {
        var identity=PoseMath.FromOpenVR(Matrix(0,1,2,-3));
        Check(Near(identity.Position,new Vector3(1,2,3)),"OpenVR translation reflection");
        Check(SameRotation(identity.Rotation,Quaternion.Identity),"identity rotation");
        var yaw=PoseMath.FromOpenVR(Matrix(MathF.PI/2));
        Check(Near(Vector3.Transform(Vector3.UnitZ,yaw.Rotation),-Vector3.UnitX),"OpenVR positive Y rotation must turn Unity forward towards -X");
        var neutral=new PoseValue(new Vector3(3,1.6f,-2),Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2));
        var relative=PoseMath.Relative(neutral,neutral);
        Check(Near(relative.Position,Vector3.Zero)&&SameRotation(relative.Rotation,Quaternion.Identity),"recenter removes position and yaw");
        var moved=new PoseValue(neutral.Position+Vector3.UnitX,neutral.Rotation);
        Check(Near(PoseMath.Relative(moved,neutral).Position,Vector3.UnitZ),"movement transformed into reference yaw basis");
        var tilted=new PoseValue(neutral.Position,neutral.Rotation*Quaternion.CreateFromAxisAngle(Vector3.UnitX,0.3f));
        Check(SameRotation(PoseMath.Relative(tilted,neutral).Rotation,Quaternion.CreateFromAxisAngle(Vector3.UnitX,0.3f)),"recenter preserves pitch and horizon");
        var l=PoseMath.FromOpenVR(Matrix(0,-0.032f));var r=PoseMath.FromOpenVR(Matrix(0,0.032f));
        Check(MathF.Abs(Vector3.Distance(l.Position,r.Position)-0.064f)<0.00001f,"native 64mm eye separation retained");
        var yawed=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);
        Check(Vector3.Distance(Vector3.Transform(l.Position,yawed),Vector3.Transform(r.Position,yawed))>0.06399f,"eye separation survives head rotation");
        Check(!PoseMath.Valid(new PoseValue(new Vector3(float.NaN,0,0),Quaternion.Identity)),"invalid tracking rejected");
        Check(Marshal.SizeOf<HmdMatrix34_t>()==48 && Marshal.SizeOf<TrackedDevicePose_t>()==80,"OpenVR pose ABI layout");
        Check(Marshal.SizeOf<VRControllerState_t>()==64 && Marshal.OffsetOf<VRControllerState_t>("ulButtonPressed").ToInt32()==8,"Windows x64 controller ABI layout");
        Check(OpenVR.IVRSystem_Version=="IVRSystem_023"&&OpenVR.IVRCompositor_Version=="IVRCompositor_029","function-table versions match confirmed runtime interfaces");
        // 0.1.233: the world scale divides the eye distance (0.9: eyes 11% further apart, the world 10% smaller); the eye's turn kept.
        var eye=new PoseValue(new System.Numerics.Vector3(-.0315f,.001f,-.002f),System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,.05f));
        var small=PoseMath.ScaledEye(eye,.9f);
        Check(System.Numerics.Vector3.Distance(small.Position,eye.Position/.9f)<1e-6f&&small.Rotation==eye.Rotation,"world scale 0.9 does not widen the eye offset by 1/0.9");
        Check(PoseMath.ScaledEye(eye,1f).Position==eye.Position&&PoseMath.ScaledEye(eye,float.NaN).Position==eye.Position&&PoseMath.ScaledEye(eye,0).Position==eye.Position,"a bad world scale changes the eyes");
        // 0.1.235: the plugin's eyes (xiii_openxr.dll) are drawn as far apart as the mod's: 1 / world scale.
        Check(MathF.Abs(PoseMath.ViewScale(.8f)-1.25f)<1e-6f&&PoseMath.ViewScale(1f)==1f&&PoseMath.ViewScale(float.NaN)==1f&&PoseMath.ViewScale(0)==1f&&PoseMath.ViewScale(9)==1f,"the plugin's eye scale is not 1 / world scale");
        Check(MathF.Abs(PoseMath.ScaledEye(eye,.8f).Position.X-eye.Position.X*PoseMath.ViewScale(.8f))<1e-6f,"the mod's and the plugin's eyes drawn differently apart");
        Console.WriteLine("PASS: 0.1.233 the world scale divides the eye distance the game is drawn with, the eyes' turn kept; a bad value changes nothing.");
        Console.WriteLine("PASS: 0.1.235 the eyes of Unity's OpenXR plugin are drawn as far apart as the mod's (1 / world scale).");
        Console.WriteLine("PASS: pose basis conversion, translation, yaw recenter, pitch preservation, eye separation, invalid poses, OpenVR ABI and interface versions.");
    }
}
