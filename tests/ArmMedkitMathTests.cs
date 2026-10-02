using System;using System.Numerics;using XiiiXR;
// 0.1.186: the tiny medkits on the forearms' cuts (ArmMedkitMath).
class ArmMedkitMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near(float a,float b,float e=1e-4f)=>MathF.Abs(a-b)<e;
 static void Main()
 {
  // The left forearm carries the small medkit (9), the right the large one (10); a hand takes from the other forearm.
  Check(ArmMedkitMath.Slot(0)==9&&ArmMedkitMath.Slot(1)==10&&ArmMedkitMath.ArmOf(9)==0&&ArmMedkitMath.ArmOf(10)==1&&ArmMedkitMath.ArmOf(11)<0,"medkits on the wrong forearms");
  Check(ArmMedkitMath.ArmFor(1)==0&&ArmMedkitMath.ArmFor(0)==1&&!ArmMedkitMath.Large(0)&&ArmMedkitMath.Large(1),"a hand takes from its own forearm");
  // Tiny: the small one 4.5 cm long, the large one 5.5 cm (the game's 14 / 18 cm cases).
  float s=ArmMedkitMath.Scale(false,.14f),l=ArmMedkitMath.Scale(true,.18f);
  Check(Near(.14f*s,.045f)&&Near(.18f*l,.055f)&&s<.4f&&l<.4f,"the forearm models are not tiny");
  Check(float.IsFinite(ArmMedkitMath.Scale(false,0))&&ArmMedkitMath.Scale(false,0)>0,"a degenerate case size breaks the scale");
  // On the cut, out toward the elbow by half its thickness (not inside the arm).
  var cut=new Vector3(.3f,1.1f,.2f);var outward=new Vector3(0,0,-2);
  var c=ArmMedkitMath.Center(cut,outward,.012f);
  Check(Near(c.X,.3f)&&Near(c.Y,1.1f)&&Near(c.Z,.2f-.006f-ArmMedkitMath.Gap),"the model not on the cut facing the elbow");
  var d=ArmMedkitMath.Center(cut,Vector3.Zero,.012f);Check(float.IsFinite(d.Z),"no direction out of the cut breaks it");
  // Its front: the hand's up, square to the forearm.
  var f=ArmMedkitMath.Front(new Vector3(0,1,.5f),new Vector3(0,0,-1));
  Check(Near(f.Length(),1)&&Near(Vector3.Dot(f,new Vector3(0,0,-1)),0)&&f.Y>.99f,"the model's front not the hand's up across the forearm");
  var g=ArmMedkitMath.Front(new Vector3(0,0,-1),new Vector3(0,0,-1));
  Check(Near(g.Length(),1)&&Near(Vector3.Dot(g,new Vector3(0,0,-1)),0),"the hand's up along the forearm breaks the model's front");
  // A grip within 10 cm of it takes it.
  Check(ArmMedkitMath.Reach(c+new Vector3(.06f,0,.06f),c)&&!ArmMedkitMath.Reach(c+new Vector3(.08f,0,.08f),c),"the reach is not 10 cm");
  // No rim found: the forearm's line at the cut.
  var a=ArmMedkitMath.AlongForearm(new Vector3(.02f,.04f,-.26f),-.235f);
  Check(Near(a.Z,-.235f)&&Near(a.X,.02f*.235f/.26f)&&Near(a.Y,.04f*.235f/.26f),"the cut without a rim not on the forearm's line");
  Check(ArmMedkitMath.AlongForearm(new Vector3(0,0,.1f),-.2f)==new Vector3(0,0,-.2f),"a bad elbow breaks the cut");
  Console.WriteLine("PASS: 0.1.186 a tiny small medkit on the left forearm's cut, a tiny large one on the right; on the cut toward the elbow, square to the forearm; the other hand's grip within 10 cm takes it.");
 }
}
