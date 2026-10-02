using System;using System.Numerics;using XiiiXR;
class PhysicalHandsMathTests
{
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main()
 {
  for(int sign=-1;sign<=1;sign+=2)
  {
   var p=new Vector3(1,0,0);var q=Vector3.Transform(p,Quaternion.CreateFromAxisAngle(Vector3.UnitY,sign*.1f));
   float d=PhysicalHandsMath.HingeDelta(p,q,Vector3.Zero,Vector3.UnitY);
   Check(Math.Abs(d-sign*.1f*180/MathF.PI)<.001f,"push/pull hinge sign");
   var rotation=Quaternion.CreateFromYawPitchRoll(.9f,.4f,.1f);var origin=new Vector3(9,3,-7);
   float rotated=PhysicalHandsMath.HingeDelta(Vector3.Transform(p,rotation)+origin,Vector3.Transform(q,rotation)+origin,origin,Vector3.Transform(Vector3.UnitY,rotation));
   Check(Math.Abs(d-rotated)<.002f,"hinge changes with room orientation");
  }
  Check(PhysicalHandsMath.HingeDelta(Vector3.Zero,Vector3.One,Vector3.Zero,Vector3.UnitY)==0,"grip at hinge generates torque");
  Check(PhysicalHandsMath.Follow(new Vector3(10),new Vector3(20)).Length()<=6.001,"spring launches object through wall");
  Check(PhysicalHandsMath.Limit(new Vector3(float.NaN),8)==Vector3.Zero,"invalid tracking velocity retained");
  Check(PhysicalHandsMath.SmallProp("beer_bottle_01_mesh")&&!PhysicalHandsMath.SmallProp("building_01")&&!PhysicalHandsMath.SmallProp("npc_spotter"),"static scenery incorrectly converted");
  // 0.1.149: food, cans and rubbish are small things too; walls, cannons and sandbags are not.
  Check(PhysicalHandsMath.SmallProp("burger_01")&&PhysicalHandsMath.SmallProp("soda_can_02")&&PhysicalHandsMath.SmallProp("can01")&&PhysicalHandsMath.SmallProp("prop_can")&&PhysicalHandsMath.SmallProp("paper_crumpled_03")&&PhysicalHandsMath.SmallProp("donut_box"),"food, cans, rubbish");
  Check(!PhysicalHandsMath.SmallProp("cannon_01")&&!PhysicalHandsMath.SmallProp("canvas_02")&&!PhysicalHandsMath.SmallProp("sandbag_01")&&!PhysicalHandsMath.SmallProp("wall_paper_01"),"cannons, canvas, sandbags, walls");
  Console.WriteLine("PASS: hinge push/pull and rotated rooms; hinge singularity; bounded physical following; invalid tracking; conservative prop recognition. No game runtime validation.");
 }
}
