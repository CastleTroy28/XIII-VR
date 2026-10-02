using System;using System.Numerics;using XiiiXR;
class HostageShieldMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var a=new Vector3(0,.7f,.5f);var b=new Vector3(0,1.4f,.5f);const float r=.18f;
  Check(HostageShieldMath.Capsule(new(0,1,3),new(0,1,0),a,b,r,out float t)&&Math.Abs(t-(3-.68f)/3)<1e-5,"frontal bullet not intercepted at body surface");
  Check(!HostageShieldMath.Capsule(new(.3f,1,3),new(.3f,1,0),a,b,r,out _),"bullet beside body blocked");
  Check(!HostageShieldMath.Capsule(new(0,1,-3),new(0,1,0),a,b,r,out _),"bullet from behind player blocked");
  Check(!HostageShieldMath.Capsule(new(0,2,3),new(0,2,0),a,b,r,out _),"exposed head blocked by torso");
  Check(HostageShieldMath.Capsule(new(0,2,.5f),new(0,1,.5f),a,b,r,out t)&&Math.Abs(t-.42f)<1e-5,"parallel ray misses capsule cap");
  Check(HostageShieldMath.Capsule(new(0,1,.5f),new(0,1,0),a,b,r,out t)&&t==0,"origin inside body not intercepted");
  Check(!HostageShieldMath.Capsule(Vector3.Zero,Vector3.Zero,a,b,r,out _),"zero segment accepted");
  Check(!HostageShieldMath.Capsule(new(float.NaN,0,0),Vector3.Zero,a,b,r,out _),"NaN accepted");
  Check(HostageShieldMath.Sphere(new(0,1,3),new(0,1,0),new(0,1,.5f),r,out t)&&Math.Abs(t-(3-.68f)/3)<1e-5,"sphere nearest surface wrong");
  Check(HostageShieldMath.Box(new(0,0,3),new(0,0,0),new(0,0,1),new(.3f,.5f,.2f),out t)&&Math.Abs(t-1.9f/3)<1e-5,"box entry wrong");
  Check(!HostageShieldMath.Box(new(.3f,0,3),new(.3f,0,0),new(0,0,1),new(.3f,.5f,.2f),out _),"box near miss blocked");
  // Rotational/translation invariance catches component-wise approximations.
  for(int k=0;k<100;k++)
  {
   var q=Quaternion.CreateFromYawPitchRoll(k*.137f,k*.029f,k*.031f);var offset=new Vector3(12,-4,7);
   Vector3 T(Vector3 p)=>Vector3.Transform(p,q)+offset;
   Check(HostageShieldMath.Capsule(T(new(0,1,3)),T(new(0,1,0)),T(a),T(b),r,out t)&&Math.Abs(t-(3-.68f)/3)<.0001f,"rotated hurtbox incorrect");
  }
  for(int f=0;f<128;f++)Check(HostageShieldMath.Gangster(f)==(f==8),"police/FBI/military/composite faction matched");
    // 0.1.205: a bullet past the hostage goes into his body first, then his head, an arm, a leg (NPC.DamageArea: Body 0, Head 1, UpperLimb 2, Unknown 3, LowerLimb 4).
  if(!(HostageShieldMath.ShieldRank(0)<HostageShieldMath.ShieldRank(1)&&HostageShieldMath.ShieldRank(1)<HostageShieldMath.ShieldRank(2)&&HostageShieldMath.ShieldRank(2)<HostageShieldMath.ShieldRank(4)&&HostageShieldMath.ShieldRank(4)<HostageShieldMath.ShieldRank(3)))throw new Exception("a bullet past the hostage goes into the wrong part of him");
  Console.WriteLine("PASS: shield entry/near miss/side/back/exposed head, parallel capsule cap, origin inside, invalid segments, sphere/box, 100 rotated fixtures and all 128 faction masks.");
 }
}
