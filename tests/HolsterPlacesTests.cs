using System;using System.Linq;using System.Numerics;using XiiiXR;
class HolsterPlacesTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Near(Vector3 a,Vector3 b,float e=1e-4f)=>Vector3.Distance(a,b)<e;
 static void Main()
 {
  var saved=new System.Collections.Generic.Dictionary<HolsterSlot,string>();
  HolsterPlaces.Write=(s,t)=>saved[s]=t;
  HolsterLayout.Offset=HolsterPlaces.Offset;
  // Every place but the two on the back can be moved.
  Check(HolsterPlaces.Movable.Length==7&&!HolsterPlaces.Movable.Contains(HolsterSlot.LeftShoulder)&&!HolsterPlaces.Movable.Contains(HolsterSlot.RightBack),"the back places are movable or a place is missing");
  Check(!HolsterPlaces.CanMove(HolsterSlot.RightBack)&&!HolsterPlaces.CanMove(HolsterSlot.None)&&HolsterPlaces.CanMove(HolsterSlot.Belly),"which places move");
  // Config text: centimetres right, up, forward.
  Check(HolsterPlaces.TryParse("3,-2.5,10",out var o)&&Near(o,new Vector3(.03f,-.025f,.10f)),"config text not read in centimetres");
  Check(!HolsterPlaces.TryParse("1,2",out _)&&!HolsterPlaces.TryParse("a,b,c",out _)&&!HolsterPlaces.TryParse(null,out _)&&!HolsterPlaces.TryParse("1,NaN,2",out _),"bad config text read");
  Check(HolsterPlaces.Format(new Vector3(.031f,-.025f,0))=="3.1,-2.5,0","config text not written in centimetres");
  // Nothing moved: the mod's places.
  var belt=HolsterLayout.Pose(HolsterSlot.BeltRight,false);
  Check(Near(belt.Grip,new Vector3(.20f,-.50f,0))&&!HolsterPlaces.Moved(HolsterSlot.BeltRight),"an unmoved place is not the mod's");
  // Pointed to a new point (torso frame as drawn): the place's hand point goes there, its grip with it.
  HolsterPlaces.MoveTo(HolsterSlot.BeltRight,new Vector3(.28f,-.40f,.05f),false);HolsterPlaces.Save(HolsterSlot.BeltRight);
  var moved=HolsterLayout.Pose(HolsterSlot.BeltRight,false);
  Check(Near(moved.Reach,new Vector3(.28f,-.40f,.05f))&&Near(moved.Grip-moved.Reach,belt.Grip-belt.Reach),"the place's hand point is not where it was pointed, or its grip not with it");
  Check(moved.Forward==belt.Forward&&moved.Up==belt.Up,"moving a place turned its weapon");
  Check(saved[HolsterSlot.BeltRight]=="8,5,5","a moved place not saved as its offset in centimetres");
  // A left-hander: the place is drawn mirrored, and moved the mirror way.
  var lefty=HolsterLayout.Pose(HolsterSlot.BeltRight,true);
  Check(Near(lefty.Reach,new Vector3(-.28f,-.40f,.05f)),"a left-hander's moved place not the mirror");
  HolsterPlaces.MoveTo(HolsterSlot.BeltRight,new Vector3(-.30f,-.40f,.05f),true);
  Check(Near(HolsterLayout.Pose(HolsterSlot.BeltRight,true).Reach,new Vector3(-.30f,-.40f,.05f))&&Near(HolsterLayout.Pose(HolsterSlot.BeltRight,false).Reach,new Vector3(.30f,-.40f,.05f)),"a left-hander's move not kept as the mirror");
  // Kept beside the body: never above the shoulders, nor far away.
  HolsterPlaces.MoveTo(HolsterSlot.ChestLeft,new Vector3(-2,1,3),false);
  var chest=HolsterLayout.Pose(HolsterSlot.ChestLeft,false).Reach;
  Check(chest.Y<=HolsterPlaces.BoxMax.Y+1e-5f&&chest.X>=HolsterPlaces.BoxMin.X-1e-5f&&chest.Z<=HolsterPlaces.BoxMax.Z+1e-5f,"a place pointed far away is not kept beside the body");
  HolsterPlaces.Set(HolsterSlot.Belly,new Vector3(5,-5,float.NaN));Check(HolsterPlaces.Offset(HolsterSlot.Belly)==Vector3.Zero,"a broken offset kept");
  HolsterPlaces.Set(HolsterSlot.Belly,new Vector3(5,-5,0));Check(MathF.Abs(HolsterPlaces.Offset(HolsterSlot.Belly).X-HolsterPlaces.MaxOffset)<1e-6f,"an offset beyond the limit kept");
  // The back places are never moved.
  HolsterPlaces.Set(HolsterSlot.LeftShoulder,new Vector3(.1f,0,0));Check(Near(HolsterLayout.Pose(HolsterSlot.LeftShoulder,false).Grip,new Vector3(.05f,-.55f,-.19f)),"a back place moved");
  // Reset: one place, then all of them, saved as 0,0,0.
  HolsterPlaces.Reset(HolsterSlot.BeltRight);Check(Near(HolsterLayout.Pose(HolsterSlot.BeltRight,false).Grip,belt.Grip)&&saved[HolsterSlot.BeltRight]=="0,0,0","a place not reset");
  HolsterPlaces.ResetAll();Check(HolsterPlaces.Movable.All(s=>!HolsterPlaces.Moved(s)&&saved[s]=="0,0,0"),"not every place reset");
  HolsterPlaces.Load(HolsterSlot.Belly,"0,10,-5");Check(Near(HolsterLayout.Pose(HolsterSlot.Belly,false).Grip,new Vector3(.10f,-.37f,.12f)),"a place read from the config not moved");
  HolsterPlaces.Load(HolsterSlot.Belly,"junk");Check(!HolsterPlaces.Moved(HolsterSlot.Belly),"bad config text moved a place");
  Check(Near(HolsterLayout.Pose(HolsterSlot.Belly,false,false).Grip,new Vector3(.10f,-.47f,.17f)),"the mod's own place not available");
  // Names as the player sees them (mirrored for a left-hander).
  Check(HolsterPlaces.Name(HolsterSlot.BeltRight,false)=="Pistol, right hip"&&HolsterPlaces.Name(HolsterSlot.BeltRight,true)=="Pistol, left hip"&&HolsterPlaces.Name(HolsterSlot.ArmpitLeft,true)=="Pistol, under the right arm"&&HolsterPlaces.Name(HolsterSlot.ChestRight,false)=="Grenades","place names");
  // The ray takes the ball nearest to it, within reach.
  var centers=new[]{new Vector3(0,0,1),new Vector3(.05f,0,1.2f),new Vector3(1,0,1)};
  Check(HolsterPlaces.Pick(Vector3.Zero,Vector3.UnitZ,centers,.07f,out float along)==0&&MathF.Abs(along-1)<1e-4f,"the ray does not take the ball on it");
  Check(HolsterPlaces.Pick(Vector3.Zero,Vector3.Normalize(new Vector3(.05f,0,1.2f)),centers,.07f,out _)==1,"the ray does not take the ball it points at");
  Check(HolsterPlaces.Pick(Vector3.Zero,-Vector3.UnitZ,centers,.07f,out _)==-1&&HolsterPlaces.Pick(Vector3.Zero,Vector3.Zero,centers,.07f,out _)==-1,"a ball behind the ray (or no ray) taken");
  // The body's heading (shared with the weapons on the body): kept while looking steeply down.
  HolsterPlaces.ResetTorso();
  Check(MathF.Abs(HolsterPlaces.Torso(new Vector3(1,0,0))-90)<.01f,"the body's heading is not the head's");
  Check(MathF.Abs(HolsterPlaces.Torso(Vector3.Normalize(new Vector3(-.2f,-1,0)))-90)<.01f,"looking down turned the body");
  Check(MathF.Abs(HolsterPlaces.Torso(Vector3.Normalize(new Vector3(0,-.3f,1)))-0)<.01f,"the body does not turn with the head");
  Console.WriteLine("PASS: 0.1.234 weapon places: each but the two on the back moved where pointed (its grip with it, mirrored for a left-hander), kept beside the body, saved in centimetres, reset one or all; the ray takes the ball it points at; the body's heading shared.");
 }
}
