using System;using System.Numerics;using XiiiXR;
class LayFlatMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near(Vector3 a,Vector3 b,float e=1e-3f)=>Vector3.Distance(a,b)<e;
 static void Main()
 {
  Check(LayFlatMath.LongAxis(new Vector3(.18f,1.4f,.4f))==1&&LayFlatMath.LongAxis(new Vector3(2,1,1))==0&&LayFlatMath.LongAxis(new Vector3(.1f,.2f,.9f))==2,"long side");
  var a=Vector3.Normalize(new Vector3(.3f,1,-.2f));var b=Vector3.Normalize(new Vector3(-1,.1f,.4f));
  Check(Near(Vector3.Transform(a,LayFlatMath.FromTo(a,b)),b),"from-to turn");
  Check(Near(Vector3.Transform(Vector3.UnitY,LayFlatMath.FromTo(Vector3.UnitY,-Vector3.UnitY)),-Vector3.UnitY),"from-to turn of opposite directions");
  Check(LayFlatMath.FromTo(Vector3.Zero,b)==Quaternion.Identity,"from-to of nothing");
  // A broom standing (long side up), turned a little about itself; laid along where its head pointed (right-forward).
  var standing=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.4f);
  var lying=LayFlatMath.Level(standing,Vector3.UnitY,new Vector3(1,-.8f,1));
  var along=Vector3.Transform(Vector3.UnitY,lying);
  Check(MathF.Abs(along.Y)<1e-3f&&Near(along,Vector3.Normalize(new Vector3(1,0,1))),"not laid level along its heading: "+along);
  Check(Near(Vector3.Transform(Vector3.UnitY,LayFlatMath.Level(standing,Vector3.UnitY,new Vector3(0,-1,0))),Vector3.UnitX,1e-2f)||MathF.Abs(Vector3.Transform(Vector3.UnitY,LayFlatMath.Level(standing,Vector3.UnitY,new Vector3(0,-1,0))).Y)<1e-3f,"straight down heading not laid level");
  // On the floor: the lowest corner at the floor, the middle over the point where it was let go of.
  var min=new Vector3(-.09f,-.05f,-.2f);var max=new Vector3(.09f,1.35f,.2f);
  var root=LayFlatMath.Rest(min,max,lying,0,3,4);
  float lowest=float.PositiveInfinity;for(int i=0;i<8;i++){var c=new Vector3((i&1)==0?min.X:max.X,(i&2)==0?min.Y:max.Y,(i&4)==0?min.Z:max.Z);lowest=MathF.Min(lowest,(root+Vector3.Transform(c,lying)).Y);}
  var middle=root+Vector3.Transform((min+max)*.5f,lying);
  Check(MathF.Abs(lowest)<1e-4f&&MathF.Abs(middle.X-3)<1e-4f&&MathF.Abs(middle.Z-4)<1e-4f,"not resting on the floor under where it was let go of");
  Check(middle.Y<.25f,"lying broom's middle too high: "+middle.Y);
  // Falling: nothing at the start, done after sqrt(2h/g), a drop of nothing is done at once.
  float h=1.2f,tEnd=MathF.Sqrt(2*h/LayFlatMath.Gravity);
  Check(LayFlatMath.Fall(0,h)==0&&MathF.Abs(LayFlatMath.Fall(tEnd,h)-1)<1e-4f&&LayFlatMath.Fall(tEnd*2,h)==1&&LayFlatMath.Fall(tEnd*.5f,h)<.3f,"fall not as under gravity");
  Check(LayFlatMath.Fall(.1f,0)==1&&LayFlatMath.Fall(float.NaN,1)==1,"fall of nothing or bad time");
  // Its head: the lower end while it stood; lying, the end nearer its origin.
  Check(LayFlatMath.HeadEnd(.9f,-.05f,1.35f)==-1&&LayFlatMath.HeadEnd(-.9f,-.05f,1.35f)==1&&LayFlatMath.HeadEnd(.1f,-.05f,1.35f)==-1&&LayFlatMath.HeadEnd(.1f,-1.3f,.05f)==1,"head end");
  Console.WriteLine("PASS: 0.1.161 lay flat: the long side, the shortest turn to lie level along its heading (straight down too), resting with its lowest corner on the floor under where it was let go of, falling as under gravity, the head the lower end while it stood.");
 }
}
