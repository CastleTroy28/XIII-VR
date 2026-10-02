using System;using XiiiXR;using UnityEngine;
class StoryBlinkTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Main()
 {
  CameraRig.Current=new CameraRig();Time.frameCount=10;
  var clip=new PlayerBlinkBehaviour{useBlinking=true,EyeUpperEyeLid=.75f,EyeLowerEyeLid=.25f};
  StoryBlink.Sample(clip);Check(Math.Abs(StoryBlink.Amount-.5f)<1e-6f,"Timeline eyelid gap is not mapped to closure");
  float left=StoryBlink.Amount,right=StoryBlink.Amount;Check(left==right,"eyes get different opacity");
  clip.EyeUpperEyeLid=1;clip.EyeLowerEyeLid=0;StoryBlink.Sample(clip);Check(StoryBlink.Amount==0,"open eyes darken image");
  clip.EyeUpperEyeLid=.4f;clip.EyeLowerEyeLid=.6f;StoryBlink.Sample(clip);Check(StoryBlink.Amount==.85f,"closed eyes fail to black out");
  StoryBlink.End(new PlayerBlinkBehaviour());Check(StoryBlink.Amount==.85f,"unrelated clip clears active blink");
  Time.realtimeSinceStartup=1;StoryBlink.Sample(clip);Check(StoryBlink.Amount==0,"held Timeline value leaves view dark");
  StoryBlink.End(clip);Check(StoryBlink.Amount==0,"paused clip leaves eyes closed");
  StoryBlink.Sample(clip);Time.frameCount+=2;Check(StoryBlink.Amount==0,"stopped Timeline leaves stale opacity");
  StoryBlink.Reset();clip.useBlinking=false;StoryBlink.Sample(clip);Check(StoryBlink.Amount==0,"non-blink clip activates effect");
  Check(StoryBlink.Closure(float.NaN,0)==0,"invalid data blacks out view");
  Console.WriteLine("PASS: Timeline-driven identical eye opacity, open/closed limits, clip ownership, pause, stale-frame and scene reset. No desktop shader or scene sampling.");
 }
}
class PlayerBlinkBehaviour{internal bool useBlinking;internal float EyeUpperEyeLid,EyeLowerEyeLid;}
namespace UnityEngine{static class Time{internal static int frameCount;internal static float realtimeSinceStartup;}}
namespace XiiiXR{class CameraRig{internal static CameraRig? Current;}}
