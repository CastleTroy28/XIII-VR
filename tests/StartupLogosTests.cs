using System;using System.Reflection;using XiiiXR;
class StartupLogosTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static bool Start(StartLogosController c)=>(bool)typeof(StartupLogos).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{c})!;
 static void Main()
 {
  StartupLogos.Install();StartupLogos.Install();Check(HarmonyLib.Harmony.Patches==1,"logo hook installed repeatedly");
  var c=new StartLogosController();QualityOptions.SkipLogos.Value=false;Check(Start(c)&&c.Transitions==0,"disabled skip changed startup");
  QualityOptions.SkipLogos.Value=true;Check(!Start(c)&&c.Transitions==1,"enabled skip does not use native transition");
  Check(!Start(c)&&c.Transitions==1,"duplicate startup requests another scene");
  c=new(){Fail=true};Check(Start(c),"native transition error blocks original startup");
  Console.WriteLine("PASS: actual logo patch installs once, respects opt-out, uses native transition once, falls back on native transition failure.");
 }
}
class StartLogosController{internal bool leavingToMainMenu,Fail;internal int Transitions;public void Start(){}internal void GoToMainMenu(){if(Fail)throw new Exception("test transition failure");Transitions++;leavingToMainMenu=true;}}
namespace XiiiXR{static class QualityOptions{internal sealed class Entry{internal bool Value;}internal static Entry SkipLogos=new();}static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}}
namespace HarmonyLib
{
 class Harmony{internal static int Patches;internal Harmony(string s){}internal void Patch(MethodInfo m,HarmonyMethod prefix){Patches++;}internal void UnpatchSelf(){}}
 class HarmonyMethod{internal HarmonyMethod(Type t,string s){}}
 static class AccessTools{internal static MethodInfo DeclaredMethod(Type t,string s)=>t.GetMethod(s)!;}
}
