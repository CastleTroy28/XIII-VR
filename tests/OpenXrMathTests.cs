using System;using System.Numerics;using XiiiXR;
class OpenXrMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near(Vector3 a,Vector3 b)=>Vector3.Distance(a,b)<1e-5f;
 static Vector3 Forward(PoseValue p)=>Vector3.Transform(Vector3.UnitZ,p.Rotation);
 static void Main()
 {
  float s45=MathF.Sin(MathF.PI/4),c45=MathF.Cos(MathF.PI/4),s15=MathF.Sin(MathF.PI/12),c15=MathF.Cos(MathF.PI/12);
  var still=OpenXrMath.Pose(new float[]{1,2,3,0,0,0,1},0);
  Check(Near(still.Position,new Vector3(1,2,-3))&&Near(Forward(still),Vector3.UnitZ),"OpenXR's -Z forward is not Unity's +Z (or the position is not mirrored)");
  var left=OpenXrMath.Pose(new float[]{9,0,0,0,0,s45,0,c45},1);
  Check(Near(Forward(left),-Vector3.UnitX),"a head turned 90 degrees left in OpenXR does not look left: "+Forward(left));
  var up=OpenXrMath.Pose(new float[]{0,0,0,s15,0,0,c15},0);
  Check(Near(Forward(up),new Vector3(0,.5f,MathF.Sqrt(3)/2)),"a head looking 30 degrees up in OpenXR does not look up: "+Forward(up));
  // the same as SteamVR's matrix read by PoseMath.FromOpenVR (both runtimes share OpenVR's axes)
  var q=Quaternion.Normalize(new Quaternion(.2f,-.4f,.1f,.87f));var m=Matrix4x4.CreateFromQuaternion(q);
  var vr=new Valve.VR.HmdMatrix34_t{m0=m.M11,m1=m.M21,m2=m.M31,m3=.3f,m4=m.M12,m5=m.M22,m6=m.M32,m7=1.6f,m8=m.M13,m9=m.M23,m10=m.M33,m11=-.2f};
  var a=PoseMath.FromOpenVR(vr);var b=OpenXrMath.Pose(new float[]{.3f,1.6f,-.2f,q.X,q.Y,q.Z,q.W},0);
  Check(Near(a.Position,b.Position)&&Near(Forward(a),Forward(b))&&Near(Vector3.Transform(Vector3.UnitY,a.Rotation),Vector3.Transform(Vector3.UnitY,b.Rotation)),"an OpenXR pose differs from the same pose through OpenVR");
  // OpenComposite's own example (Rift S): OpenXR tangents left 1.000000? no: angles give SteamVR -1.150368, 1.000000, -0.965689, 1.035530
  var fr=OpenXrMath.Frustum(MathF.Atan(-1.150368f),MathF.Atan(1f),MathF.Atan(1.035530f),MathF.Atan(-0.965689f));
  Check(MathF.Abs(fr.Left+1.150368f)<1e-5&&MathF.Abs(fr.Right-1)<1e-5&&MathF.Abs(fr.Top+0.965689f)<1e-5&&MathF.Abs(fr.Bottom-1.035530f)<1e-5&&fr.Valid,"field of view not as SteamVR/OpenComposite give it");
  Check(!OpenXrMath.Held(.5f,false)&&OpenXrMath.Held(.55f,false)&&OpenXrMath.Held(.45f,true)&&!OpenXrMath.Held(.39f,true)&&!OpenXrMath.Held(float.NaN,true),"trigger/grip hysteresis");
  Check(OpenXrMath.Buttons(true,false,0)==HandControls.Trigger&&OpenXrMath.Buttons(false,true,0)==HandControls.Grip&&OpenXrMath.Buttons(false,false,1)==HandControls.A&&OpenXrMath.Buttons(false,false,2)==HandControls.B&&OpenXrMath.Buttons(false,false,4|8)==0,"buttons as OpenVR's bits");
  Check(OpenXrMath.StickClick(8)&&!OpenXrMath.StickClick(7)&&OpenXrMath.Profile(1)=="Oculus Touch"&&OpenXrMath.Profile(0)=="none","stick click / profile names");
  Console.WriteLine("PASS: OpenXR poses in Unity's axes (turned, looking up), the field of view as SteamVR's, trigger/grip hysteresis, buttons as OpenVR bits.");
 }
}
