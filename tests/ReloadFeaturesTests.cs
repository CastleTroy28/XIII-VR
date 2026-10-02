using System;using System.Numerics;using System.Linq;using XiiiXR;
class ReloadFeaturesTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var menu=new MenuNavigation();
  Check(menu.Step(true,Vector2.UnitY,0)==0,"unarmed held stick moves menu");menu.Step(true,Vector2.Zero,.1f);
  Check(menu.Step(true,-Vector2.UnitY,.2f)==40,"down arrow missing");
  Check(menu.Step(true,-Vector2.UnitY,.4f)==0&&menu.Step(true,-Vector2.UnitY,.6f)==40,"repeat timing wrong");
  Check(menu.Step(true,Vector2.UnitX,.61f)==39,"direction change delayed");
  menu.Step(false,Vector2.Zero,.7f);Check(menu.Step(true,Vector2.UnitY,.8f)==0,"focus recovery leaks held stick");
  foreach(var profile in new[]{"pistol","ak47","shotgun"})
  {
   var size=profile=="pistol"?new Vector3(.022f,.11f,.035f):profile=="ak47"?new Vector3(.03f,.22f,.065f):new Vector3(.019f,.019f,.06f);
   var fit=ReloadGripMath.Fit(profile,-size*.5f,size*.5f);
   Check(Vector3.Dot(fit.Forward,profile=="ak47"?Vector3.UnitX:Vector3.UnitZ)>.999f,"feed end not pointing along the defined grip axis");
   Check(Math.Abs(fit.Rotation.LengthSquared()-1)<1e-5f,"held ammo scaled by rotation");
   var a=Vector3.Transform(-size*.5f,fit.Rotation)+fit.Position;
   var b=Vector3.Transform(size*.5f,fit.Rotation)+fit.Position;
   Check(Math.Abs(Vector3.Distance(a,b)-size.Length())<1e-5f,"held ammo geometry deformed");
   Check(Vector3.Dot(fit.Tip-fit.Position,fit.Forward)>0,"insertion compares base instead of feed end");
  }
  var loader=ReloadGripMath.Fit("revolver",new Vector3(-.023f,-.023f,0),new Vector3(.023f,.023f,.043f));
  Check(loader.Min.Z>.04f&&loader.Tip.Z>.10f,"loader embedded in palm or wrong insertion tip");
  Check(loader.Forward==Vector3.UnitZ&&loader.Max.Z==loader.Tip.Z,"loader collision and insertion axis disagree");
  var pump=new ManualReloadState(true);Check(!pump.TakeSpentCase(),"empty pump invents spent shell");pump.OnShot();Check(pump.TakeSpentCase()&&!pump.TakeSpentCase(),"spent shell not emitted exactly once");var bolt=new Vector3(0,0,.2f);
  ReloadAction Move(Vector3 hand,bool grip)=>pump.Step(1,false,false,false,false,false,false,hand,Vector3.Zero,Vector3.Zero,bolt,Vector3.UnitZ,3,5,grip);
  Move(bolt,true);Check(Move(bolt-new Vector3(0,0,.1f),true)==ReloadAction.RackBack,"rear pump cue missing");
  Check(pump.Suspend()==0&&pump.BlocksFire&&Math.Abs(pump.RackTravel-.0735f)<1e-6f,"focus loss auto-chambers half-cycled pump");
  Move(bolt-new Vector3(0,0,.0735f),true);Check(Move(bolt,true)==ReloadAction.Chamber&&!pump.BlocksFire,"resumed forward stroke fails");
  Check(pump.Racking,"closing pump released held grip");
  for(int i=0;i<30;i++)Check(Move(bolt,true)==ReloadAction.None&&pump.Racking&&!pump.BlocksFire,"closed pump repeats chamber/loses latch");
  pump.OnShot();Check(pump.BlocksFire&&pump.Racking,"second shot lost pump grip");
  Check(Move(bolt-new Vector3(0,0,.1f),true)==ReloadAction.RackBack,"held support cannot pump next shot");
  Check(Move(bolt,true)==ReloadAction.Chamber&&pump.Racking,"next forward stroke dropped grip");
  Move(bolt,false);Check(!pump.Racking&&!pump.BlocksFire,"release did not detach closed pump");
  Console.WriteLine("PASS: menu arming/repeat/focus, ammunition feed directions/rigid shape, manual pump back/forward recovery.");
 }
}
