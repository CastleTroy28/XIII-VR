using System;using XiiiXR;
class StoryModeTests
{
 static void Check(StoryMode actual,StoryMode expected,string why){if(actual!=expected)throw new Exception(why+": "+actual);}
 static void Ok(bool ok,string why){if(!ok)throw new Exception(why);}
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
  // 0.1.230: a story Timeline that is no Cutscene is fast-forwarded too; not a looping, hand-run, ambient or played-out one.
  Ok(StorySkipPolicy.Skippable(24,3,2,1)&&StorySkipPolicy.Skippable(24,3,0,2)&&!StorySkipPolicy.Skippable(24,3,1,1)&&!StorySkipPolicy.Skippable(24,3,2,3)
   &&!StorySkipPolicy.Skippable(24,23.9,2,1)&&!StorySkipPolicy.Skippable(3600,3,2,1)&&!StorySkipPolicy.Skippable(double.PositiveInfinity,3,2,1)&&!StorySkipPolicy.Skippable(.2,0,2,1),"which story timeline a skip fast-forwards");
  Ok(StorySkipPolicy.HeldAtEnd(24,24,0)&&!StorySkipPolicy.HeldAtEnd(24,12,0)&&!StorySkipPolicy.HeldAtEnd(24,24,2),"a Hold timeline at its last frame is played out");
  Console.WriteLine("PASS: 0.1.230 a story timeline that is no Cutscene (the last mission's memory opening) is skipped by fast-forward; never a looping, hand-run or ambient one; a Hold timeline ends the skip at its last frame.");
  Console.WriteLine("PASS: movie, scripted jump, authored scene, playable flashback, pause and next-mission policy. No forced native unlock.");
 }
}
