using System;using System.Collections.Generic;using XiiiXR;
class TutorialOnceTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  TutorialOnce.ResetForTests();
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Swap/02"),"a hint not shown the first time");
  Check(!TutorialOnce.Allow("Gameplay/Tutorial/Swap/02")&&!TutorialOnce.Allow("Gameplay/Tutorial/Swap/02"),"a hint shown again");
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Armour"),"another hint dropped");
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Armour",true),"a hint kept up on the screen dropped");
  Check(TutorialOnce.Allow("")&&TutorialOnce.Allow("")&&TutorialOnce.Allow(null),"an empty hint (hide/clear) dropped");
  Check(Bootstrap.Messages.FindAll(m=>m.Contains("Swap/02")).Count==1,"a repeated hint written more than once");
  // 0.1.179: the whole tutorial step (it opens the weapon wheel) once.
  TutorialOnce.ResetForTests();Bootstrap.Messages.Clear();
  var wheel=new string?[]{"Gameplay/Tutorial/Swap/02",null,null};
  Check(TutorialOnce.AllowStep(wheel,true),"the weapon wheel step not run the first time");
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Swap/02"),"the first step's hint dropped");
  Check(TutorialOnce.AllowStep(wheel,true,true),"the step repeated while its hint is on the screen dropped");
  Check(!TutorialOnce.AllowStep(wheel,true)&&!TutorialOnce.AllowStep(wheel,true),"the weapon wheel step ran again when coming back");
  Check(Bootstrap.Messages.FindAll(m=>m.Contains("step Gameplay/Tutorial/Swap/02")&&m.Contains("weapon wheel")).Count==1,"the skipped step not written once");
  // a step whose hint was shown some other way, or that ran without its hint yet (a delayed hint)
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Medkit/01"),"another hint dropped");
  Check(!TutorialOnce.AllowStep(new string?[]{null,"Gameplay/Tutorial/Medkit/01","Gameplay/Tutorial/Medkit/02"},false),"a step whose (per-controller) hint was shown ran again");
  Check(TutorialOnce.AllowStep(new string?[]{"Gameplay/Tutorial/Armour",null,null},false)&&!TutorialOnce.AllowStep(new string?[]{"Gameplay/Tutorial/Armour",null,null},false),"a step that ran (hint not shown yet) ran again");
  Check(TutorialOnce.Allow("Gameplay/Tutorial/Armour"),"the delayed hint of a step that ran once dropped");
  Check(TutorialOnce.AllowStep(new string?[]{null,"",null},false)&&TutorialOnce.AllowStep(new string?[]{null,"",null},false),"a step without a hint dropped");
  Console.WriteLine("PASS: each tutorial hint once per game; others and empty calls pass; the drop written once; the whole tutorial step (weapon wheel) once, kept while its hint is up.");
 }
}
namespace XiiiXR{static class Bootstrap{internal static readonly List<string> Messages=new();internal static void Write(string s)=>Messages.Add(s);}}
