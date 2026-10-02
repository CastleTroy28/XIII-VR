using System;using XiiiXR;
class RuntimeRecoveryTests
{
 static void Check(bool b,string text){if(!b)throw new Exception(text);}
 static void Main()
 {
  var state=new TrackingRecovery();Check(state.Ready(0)&&state.Active,"initial tracking not active");
  Check(state.Suspend(8)&&!state.Active,"failure does not gate stale poses/input");
  for(float t=8;t<8.49f;t+=.01f)Check(!state.Ready(t)&&!state.Active,"retry spins before delay");
  Check(state.Ready(8.5f)&&state.Active,"native transient leaves rig permanently disabled");
  Check(!state.Suspend(9),"repeating native errors flood log");
  state.SceneChanged();Check(state.Ready(9)&&state.Active,"checkpoint cannot restore tracking before cooldown");
  state.Suspend(10);state.Stop();state.SceneChanged();Check(!state.Ready(100)&&!state.Active,"scene callback revives explicitly stopped XR");
  int attempts=0;bool broken=true;
  var ui=new OptionalWork("test UI",()=>{attempts++;if(broken)throw new NullReferenceException("native Object.get_name");});
  ui.Run(10);ui.Run(10.1f);Check(attempts==1&&Bootstrap.Warnings==1,"optional render failure escapes or spins");
  ui.Run(10.5f);Check(attempts==2&&Bootstrap.Warnings==1,"repeated UI exception floods log");
  broken=false;ui.Run(11);Check(attempts==3,"optional presentation never retries");
  broken=true;ui.Run(12);broken=false;ui.Reset();ui.Run(12);Check(attempts==5,"new scene is blocked by previous UI retry delay");
  Console.WriteLine("PASS: transient tracking failure recovers without reload; checkpoint clears cooldown; explicit stop is permanent; optional UI faults cannot escape, spam or disable tracking.");
 }
}
namespace XiiiXR{static class Bootstrap{internal static int Warnings;internal static void Warn(string s){Warnings++;}}}
