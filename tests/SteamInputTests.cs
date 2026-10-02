using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Runtime.InteropServices;using System.Text.Json;using System.Numerics;using Valve.VR;using XiiiXR;
class SteamInputTests
{
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static readonly Dictionary<ulong,string> Names=new();static ulong next=1;
 static EVRInputError Handle(IntPtr name,ref ulong handle){handle=next++;Names[handle]=Marshal.PtrToStringUTF8(name)!;return EVRInputError.None;}
 static bool active=true;static EVRInputError updateError=EVRInputError.None,hapticError=EVRInputError.None;
 static readonly List<(string action,string device,float amplitude,float duration)> pulses=new();
 static void Main()
 {
  string directory=Path.Combine(Path.GetTempPath(),"xiii-input-tests-"+Guid.NewGuid());
  string path=SteamInputManifest.Write(directory);
  using(var doc=JsonDocument.Parse(File.ReadAllText(path)))
  {
   var actions=doc.RootElement.GetProperty("actions").EnumerateArray().Select(a=>a.GetProperty("name").GetString()).ToHashSet();
   Check(actions.Count==14,"missing input actions alongside haptics");
   string binding=doc.RootElement.GetProperty("default_bindings")[0].GetProperty("binding_url").GetString()!;
   using var b=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,binding)));
   var bindingRoot=b.RootElement.GetProperty("bindings").GetProperty("/actions/xiii");
   foreach(var source in bindingRoot.GetProperty("sources").EnumerateArray())foreach(var input in source.GetProperty("inputs").EnumerateObject())
    Check(actions.Contains(input.Value.GetProperty("output").GetString()),"binding output not in manifest");
   var sources=bindingRoot.GetProperty("sources").EnumerateArray().ToArray();
   Check(sources.Any(s=>s.GetProperty("path").GetString()=="/user/hand/left/input/y"&&s.GetProperty("inputs").GetProperty("click").GetProperty("output").GetString()=="/actions/xiii/in/left_b"),"left Y cycle binding changed");
   Check(sources.Any(s=>s.GetProperty("path").GetString()=="/user/hand/right/input/a"&&s.GetProperty("inputs").GetProperty("click").GetProperty("output").GetString()=="/actions/xiii/in/right_a"),"right A wheel binding changed");
   foreach(var h in bindingRoot.GetProperty("haptics").EnumerateArray())Check(actions.Contains(h.GetProperty("output").GetString()),"haptic output not registered");
  }
  var table=new IVRInput
  {
   SetActionManifestPath=p=>File.Exists(Marshal.PtrToStringUTF8(p))?EVRInputError.None:EVRInputError.NameNotFound,
   GetActionSetHandle=Handle,GetActionHandle=Handle,GetInputSourceHandle=Handle,
   UpdateActionState=(s,size,count)=>{Check(count==1&&size==Marshal.SizeOf<VRActiveActionSet_t>(),"action-set ABI");return updateError;},
   GetDigitalActionData=(ulong a,ref InputDigitalActionData_t d,uint size,ulong hand)=>{Check(size==Marshal.SizeOf<InputDigitalActionData_t>(),"digital ABI");d.bActive=active;d.bState=Names[a].EndsWith("_a")||Names[a].EndsWith("_click");return EVRInputError.None;},
   GetAnalogActionData=(ulong a,ref InputAnalogActionData_t d,uint size,ulong hand)=>{Check(size==Marshal.SizeOf<InputAnalogActionData_t>(),"analog ABI");d.bActive=active;d.x=.4f;d.y=.7f;return EVRInputError.None;},
   TriggerHapticVibrationAction=(action,start,duration,frequency,amplitude,device)=>{Check(start==0&&frequency==150,"wrong haptic parameters");pulses.Add((Names[action],Names[device],amplitude,duration));return hapticError;}
  };
  IntPtr native=Marshal.AllocHGlobal(Marshal.SizeOf<IVRInput>());
  try
  {
   Marshal.StructureToPtr(table,native,false);var input=new SteamInput(native,path);
   Check(!input.Read(0,out _,out _)&&!input.Vibrate(false,1,.16f),"unupdated actions used");input.Update();
   Check(input.Read(0,out ulong held,out var stick)&&held==HandControls.A&&stick.Valid&&stick.Value==new Vector2(.4f,.7f)&&stick.Click,"buttons/stick/click mapping");
   Check(input.Vibrate(false,1,.16f)&&input.Vibrate(true,2,.2f),"action vibration did not dispatch");
   Check(pulses[0].device=="/user/hand/left"&&pulses[0].action.EndsWith("/left")&&pulses[1].device=="/user/hand/right"&&pulses[1].amplitude==1,"haptic action wrong device/strength");
   active=false;Check(!input.Read(0,out _,out _),"inactive binding suppresses legacy fallback");active=true;
   updateError=EVRInputError.NoData;input.Update();Check(!input.Read(0,out _,out _)&&!input.Vibrate(true,1,.16f),"failed action update still used");
   updateError=EVRInputError.None;input.Update();hapticError=EVRInputError.NoData;Check(!input.Vibrate(false,1,.16f),"haptic error prevents legacy fallback");
  }
  finally{Marshal.FreeHGlobal(native);GC.KeepAlive(table);Directory.Delete(directory,true);}
  Console.WriteLine("PASS: manifest input/output coverage, Touch A/X/Y bindings, real Valve FnTable marshaling, hand routing, action state gating and haptic errors. Fake runtime; no hardware confirmation.");
 }
}
namespace XiiiXR{internal readonly record struct StickSample(bool Valid,Vector2 Value,bool Click);static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}}
namespace UnityEngine{static class Time{internal static float realtimeSinceStartup=>1;}}
