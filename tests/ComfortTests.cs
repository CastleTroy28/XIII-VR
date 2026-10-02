using System;using System.Numerics;using XiiiXR;
class ComfortTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static StickSample Stick(float y,bool valid=true)=>new(valid,new Vector2(0,y));
 static void Main()
 {
  var c=new ComfortState();Check(c.Turn(1,true,45,90,.01f)==0,"held startup stick snaps");c.Turn(0,true,45,90,.01f);
  Check(c.Turn(1,true,45,90,.01f)==45,"snap angle incorrect");for(int i=0;i<100;i++)Check(c.Turn(1,true,45,90,.01f)==0,"held stick repeats snap");
  Check(c.Turn(-1,true,45,90,.01f)==0,"side-to-side snap without neutral");c.Turn(0,true,45,90,.01f);Check(c.Turn(-1,true,45,90,.01f)==-45,"left snap incorrect");
  Check(Math.Abs(c.Turn(.5f,false,30,90,.02f)-.9f)<1e-5f,"smooth angular speed");
  c.Reset();Check(!c.Teleport(Stick(1),true)&&!c.Aiming,"held startup teleports");c.Teleport(Stick(0),true);c.Teleport(Stick(1),true);Check(c.Aiming,"aim not armed");
  Check(c.Teleport(Stick(0),true)&&!c.Aiming,"release does not commit");Check(!c.Teleport(Stick(0),true),"second teleport on neutral");
  c.Teleport(Stick(1),true);c.Teleport(Stick(-1),true);Check(!c.Teleport(Stick(0),true)&&!c.Aiming,"cancel commits");
  c.Teleport(Stick(1),true);c.Teleport(Stick(0,false),true);Check(!c.Teleport(Stick(0),true),"tracking loss commits");
  c.Teleport(Stick(1),true);c.Teleport(Stick(0),false);Check(!c.Teleport(Stick(0),true),"menu lock commits");
  var h=new HapticChannel();h.Impact(1,7);h.CancelShot();Check(h.TryPulse(1,7,true,false,out var strength)&&strength==3999,"punch erased by gun reset or grip release");
  Check(!h.TryPulse(1.001f,7,true,false,out _),"too-frequent pulse");Check(h.TryPulse(1.02f,7,true,false,out _),"impact train cut short");
  h.Queue(1.025f,7,3000,.08f);h.Impact(1.025f,7);Check(h.TryPulse(1.03f,7,true,true,out strength)&&strength==3999,"sources double-send or lose stronger pulse");
  Check(!h.TryPulse(1.031f,7,true,true,out _),"source overlap exceeds controller rate");h.TryPulse(1.04f,7,false,true,out _);Check(!h.TryPulse(1.06f,7,true,true,out _),"focus-loss haptic resumes");
  h.Impact(2,7);Check(!h.TryPulse(2.02f,8,true,true,out _),"impact delivered to replacement device");
  var f=new EyeFrustum(-1.2f,1.2f,-1,1);var left=new PoseValue(new(-.035f,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.16f));var right=new PoseValue(new(.035f,0,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,.16f));
  var b=ViewCoverage.Bounds(f,left,f,right,1.2f);Check(b.X< -1.8f&&b.Y>1.8f&&b.Z< -1.3f&&b.W>1.3f,"wide canted Pimax view cropped");
  // 0.1.182: a headset that sees further down (Quest): Top is the tangent down (-1.3), Bottom up (0.9).
  var q=new EyeFrustum(-1.1f,1.1f,-1.3f,.9f);var straight=new PoseValue(new(-.03f,0,0),Quaternion.Identity);var straightRight=new PoseValue(new(.03f,0,0),Quaternion.Identity);
  var qb=ViewCoverage.Bounds(q,straight,q,straightRight,1.2f);
  Check(qb.Z<=-1.3f*1.2f&&qb.W<1.3f*1.2f&&qb.W>=.9f*1.2f,"the view down/up taken the wrong way round (bottom "+qb.Z+", top "+qb.W+")");
  Check(ViewCoverage.HealingAlpha(0,0)<ViewCoverage.HealingAlpha(.7f,.7f)&&ViewCoverage.HealingAlpha(1,1)<255,"glow hard opaque centre");
  foreach(bool large in new[]{false,true})foreach(var extent in new[]{new Vector3(3,1,5),new Vector3(1,5,3),new Vector3(5,3,1)})
  {
   var center=new Vector3(7,-3,5);var fit=MedkitGeometry.Fit(center,extent,large);var size=MedkitGeometry.Size(large);Vector3 min=new(float.MaxValue),max=new(float.MinValue);
   foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1}){var p=Vector3.Transform(center+extent*new Vector3(x,y,z)*.5f,fit);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
   Check(Vector3.Distance(max-min,size)<1e-5f&&Math.Abs(max.Z-MedkitGeometry.FarEdge)<1e-5f&&Math.Abs(max.Y-MedkitGeometry.PalmFace)<1e-5f,"native item axes do not fit shared palm grip");
   Check(fit.GetDeterminant()>0,"medkit winding mirrored");
   float sx=Vector3.TransformNormal(Vector3.UnitX,fit).Length(),sy=Vector3.TransformNormal(Vector3.UnitY,fit).Length(),sz=Vector3.TransformNormal(Vector3.UnitZ,fit).Length();
   Check(Math.Abs(sx-sy)<1e-5f&&Math.Abs(sx-sz)<1e-5f,"medkit squashed with nonuniform scale");
   Check(min.Z>-.065f,"case extends through wrist into forearm");
  }
  Console.WriteLine("PASS: snap neutral/angle/speed; teleport confirm/cancel/locks; independent punch pulse train/rate/device/focus; canted-eye effect coverage; both medkit palm/edge fits.");
 }
}
