using System;using XiiiXR;
class RuntimeChoiceTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(RuntimeChoice.Normalize("Auto")=="Auto"&&RuntimeChoice.Normalize(" steamvr ")=="SteamVR"&&RuntimeChoice.Normalize("OPENXR")=="OpenXR"&&RuntimeChoice.Normalize("openvr")=="OpenVR"&&RuntimeChoice.Normalize("opencomposite")=="OpenComposite","a runtime setting not recognised");
  Check(RuntimeChoice.Normalize("")=="Auto"&&RuntimeChoice.Normalize(null)=="Auto"&&RuntimeChoice.Normalize("Oculus")=="Auto","an unknown runtime setting is not Auto");
  Check(RuntimeChoice.NativeOpenXr("Auto")&&RuntimeChoice.NativeOpenXr("OpenXR")&&!RuntimeChoice.NativeOpenXr("OpenVR")&&!RuntimeChoice.NativeOpenXr("SteamVR")&&!RuntimeChoice.NativeOpenXr("OpenComposite"),"which settings use the mod's own OpenXR");
  Check(RuntimeChoice.FallBack("Auto")&&!RuntimeChoice.FallBack("OpenXR")&&!RuntimeChoice.FallBack("SteamVR"),"only Auto falls back to the OpenVR way");
  Check(RuntimeChoice.SwitchMode("Auto")=="auto"&&RuntimeChoice.SwitchMode("OpenVR")=="auto"&&RuntimeChoice.SwitchMode("OpenXR")=="auto"&&RuntimeChoice.SwitchMode("SteamVR")=="steamvr"&&RuntimeChoice.SwitchMode("OpenComposite")=="openxr","what the runtime switch is told");
  Check(RuntimeChoice.Variable=="XIIIVR_RUNTIME","the variable the runtime switch (openvr-shim/shim.c) reads was renamed");
  Console.WriteLine("PASS: the VR runtime setting (Auto/OpenXR/OpenVR/SteamVR/OpenComposite, anything else Auto): own OpenXR first, fallback only on Auto, what the runtime switch is told.");
 }
}
