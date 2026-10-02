using System;using XiiiXR;
class StoryModeTests
{
 static void Check(StoryMode actual,StoryMode expected,string why){if(actual!=expected)throw new Exception(why+": "+actual);}
 static void Main()
 {
  Check(StoryModePolicy.Select(false,false,false,false,false,false,false,false),StoryMode.Frontend,"main menu");
  Check(StoryModePolicy.Select(false,true,false,false,false,false,false,false),StoryMode.Movie,"intro without player");
  Check(StoryModePolicy.Select(true,true,true,true,true,true,true,false),StoryMode.Movie,"movie beats timeline");
  Check(StoryModePolicy.Select(true,false,true,false,false,false,false,false),StoryMode.PlayableFlashback,"ship accepts native movement");
  Check(StoryModePolicy.Select(true,false,true,true,false,false,false,false),StoryMode.Cinematic,"scripted jump");
  Check(StoryModePolicy.Select(true,false,true,false,false,false,true,false),StoryMode.Cinematic,"flashback transition restricts player");
  Check(StoryModePolicy.Select(true,false,true,false,true,true,false,false),StoryMode.Cinematic,"native locked scene");
  Check(StoryModePolicy.Select(true,false,true,false,true,true,false,true),StoryMode.PlayableFlashback,"pause inside playable ship is not cinematic");
  Check(StoryModePolicy.Select(true,false,false,false,true,true,false,true),StoryMode.Gameplay,"game menu is not a cutscene");
  Check(StoryModePolicy.Select(true,false,false,false,false,false,false,false),StoryMode.Gameplay,"next mission unlocked");
  Check(StoryModePolicy.Select(true,false,true,false,false,false,false,false),StoryMode.PlayableFlashback,"broad flag left set must not freeze controls");
  Check(StoryModePolicy.Select(true,false,false,false,false,false,false,false,true),StoryMode.Cinematic,"authored timeline without broad/input flags must retain native camera");
  foreach(var name in new[]{"in_camera_cs_seq_seq_0101","Camera_blink_cs&seq_seq_02","vc_00 - player camera animation"})
   if(!StoryModePolicy.AuthoredCamera(name))throw new Exception("authored camera lost: "+name);
  foreach(var name in new[]{"AnimatorDriven","Hurt","Scoped","Walking",""})
   if(StoryModePolicy.AuthoredCamera(name))throw new Exception("gameplay falsely cinematic: "+name);
  var skip=new StorySkipLatch();
  if(skip.Sample(true,true)||skip.Sample(true,false)||!skip.Sample(true,true)||skip.Sample(true,true))throw new Exception("cutscene skip must require release then press, once per press");
  skip.Sample(false,false);if(skip.Sample(true,true))throw new Exception("focus reconnect skips scene");
  Console.WriteLine("PASS: movie, scripted jump, authored scene, playable flashback, pause and next-mission policy. No forced native unlock.");
 }
}
