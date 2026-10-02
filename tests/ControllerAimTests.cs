using System;using System.Collections.Generic;using System.Numerics;using XiiiXR;using BepInEx.Configuration;
class ControllerAimTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Vector3 Forward(Quaternion q)=>Vector3.Transform(Vector3.UnitZ,q);
 static void Main()
 {
  var c=new ConfigFile();ControllerAim.Load(c);var raw=new PoseValue(Quaternion.Identity);
  var expected=Forward(AimMath.Pitch(53));Check(Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),expected)<1e-5f,"default trim does not lower aim by eight degrees");
  var saved=c.Entry<string>("CalibratedRightRotation");saved.Value="0,0,0,1";ControllerAim.Load(c);
  Check(Math.Abs(Forward(ControllerAim.Rotation(raw).Q).Y+MathF.Sin(8*MathF.PI/180))<1e-5f,"saved calibration bypasses trim");
  c.Entry<float>("DownwardTrimDegrees").Value=float.NaN;Check(Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),Vector3.UnitZ)<1e-5f,"NaN trim corrupts pose");
  c.Entry<float>("DownwardTrimDegrees").Value=100;Check(Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),Forward(AimMath.Pitch(20)))<1e-5f,"trim clamp missing");
  var rig=new CameraRig{Raw=AimMath.Pitch(-35)};ControllerAim.Calibrate(rig);
  Check(c.Entry<float>("DownwardTrimDegrees").Value==0,"F5 double applies trim");
  Check(Vector3.Distance(Forward(ControllerAim.Rotation(new(rig.Raw)).Q),Vector3.UnitZ)<1e-5f,"calibrated natural grip misses horizontal target");
  Check(saved.Value.Split(',').Length==4,"calibration not persisted");
  // 0.1.128: the left hand's weapon hold is the right one mirrored (yaw/roll change sides, pitch stays).
  var turned=Quaternion.CreateFromYawPitchRoll(.4f,-.3f,.2f);var mirrorRaw=new Quaternion(turned.X,-turned.Y,-turned.Z,turned.W);
  var r0=ControllerAim.Rotation(new(turned)).Q;var l0=ControllerAim.Mirrored(new(mirrorRaw)).Q;
  var f0=Forward(r0);var f1=Forward(l0);Check(Vector3.Distance(new Vector3(-f0.X,f0.Y,f0.Z),f1)<1e-4f,"left hand aim is not the right hand's mirrored");
  var u0=Vector3.Transform(Vector3.UnitY,r0);var u1=Vector3.Transform(Vector3.UnitY,l0);Check(Vector3.Distance(new Vector3(-u0.X,u0.Y,u0.Z),u1)<1e-4f,"left hand roll not mirrored");
  int revision=ControllerAim.Revision;ControllerAim.Reset();ControllerAim.Load(c);
  Check(saved.Value==""&&ControllerAim.Revision>revision&&Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),expected)<1e-5,"factory hand reset not persisted");
  // 0.1.157: a calibration that is no forward grip is refused, and one saved earlier is dropped.
  saved.Value="-0.6895145,0.13856298,0.4562397,0.54517466";ControllerAim.Load(c);
  Check(saved.Value==""&&Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),expected)<1e-5f,"a saved calibration 152 degrees off the usual grip still turns the pointer");
  var before=Forward(ControllerAim.Rotation(raw).Q);bool refused=false;
  try{ControllerAim.Calibrate(new CameraRig{Raw=Quaternion.CreateFromYawPitchRoll(0,0,MathF.PI)});}catch(InvalidOperationException){refused=true;}
  Check(refused&&saved.Value==""&&Vector3.Distance(Forward(ControllerAim.Rotation(raw).Q),before)<1e-5f,"a calibration with the controller upside down was kept");
  Check(AimMath.PlausibleCalibration(AimMath.Pitch(20))&&AimMath.PlausibleCalibration(AimMath.Pitch(90))&&!AimMath.PlausibleCalibration(AimMath.Pitch(-45)),"forward grips 20-90 degrees down are calibrations, one pointing up is not");
  Console.WriteLine("PASS: production aim correction lowers default/saved grip, clamps invalid settings, resets trim during F5 and preserves calibrated horizontal direction; 0.1.157: a calibration that is no forward grip (60+ degrees off the usual one) is refused, and a saved one dropped.");
 }
}
namespace BepInEx.Configuration
{
 class ConfigEntry<T>{internal T Value;internal ConfigEntry(T v){Value=v;}}
 class ConfigFile{readonly Dictionary<string,object> values=new();internal ConfigEntry<T> Bind<T>(string a,string b,T value,string d){if(!values.ContainsKey(b))values[b]=new ConfigEntry<T>(value);return (ConfigEntry<T>)values[b];}internal ConfigEntry<T> Entry<T>(string name)=>(ConfigEntry<T>)values[name];}
}
namespace UnityEngine{readonly struct Quaternion{internal readonly System.Numerics.Quaternion Q;internal Quaternion(float x,float y,float z,float w){Q=new(x,y,z,w);}}}
namespace XiiiXR
{
 readonly struct PoseValue{internal readonly Quaternion Rotation;internal PoseValue(Quaternion q){Rotation=q;}}
 class CameraRig{internal Quaternion Raw=Quaternion.Identity;internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){l=new(Quaternion.Identity);r=new(Raw);valid=true;return true;}internal bool TryWorldHeadRotation(out Quaternion head){head=Quaternion.Identity;return true;}internal void DisarmTrigger(){}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
