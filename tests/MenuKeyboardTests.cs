using System;using System.Collections.Generic;using System.Linq;using XiiiXR;using UnityEngine;using N=System.Numerics.Vector3;
class MenuKeyboardTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var rig=new CameraRig();var start=new StartScreenControl();Resources.Items.Add(start);using var k=new MenuKeyboard(rig);
  void Step(ulong r=0,ulong l=0){Time.realtimeSinceStartup+=.12f;Time.frameCount+=10;rig.MenuRightControls=new(true,r,0,0);rig.LeftControls=new(true,l,0,0);k.Tick();}
  // 0.1.213: the start screen gets Enter at the press, in VR without the window's focus too (the key brings it forward).
  Step(HandControls.Trigger);Check(EscapeKey.Sent.Count==0,"held trigger at VR start accepted");
  Step();Application.isFocused=false;WindowFocus.InVr=true;Step(0,HandControls.Grip);
  Check(EscapeKey.Sent.SequenceEqual(new[]{(13,true)}),"a controller press does not type Enter at once (without the window's focus in VR)");
  Step(0,HandControls.Grip);Check(EscapeKey.Sent.SequenceEqual(new[]{(13,true),(13,false)}),"start key not released/nonblocking pair");
  Step(0,HandControls.Grip);Step(0,HandControls.Grip);Check(EscapeKey.Sent.Count==2,"held button repeats start key");
  Step();Step(HandControls.A);Check(EscapeKey.Sent.Count==3&&EscapeKey.Sent[^1]==(13,true),"a second press on a start screen still waiting types no Enter");Step();
  // 0.1.236: a start screen with its splashes is moved on as the game itself does on a button: no key typed, so the game's window need not be in front.
  start.loopSplashscreen=new();start.outroSplashscreen=new();start.outroSplashscreen.gameObject.activeSelf=false;start.outroSplashSound=new();
  int typedBefore=EscapeKey.Sent.Count;Step(HandControls.A);
  Check(EscapeKey.Sent.Count==typedBefore&&start.m_state==StartScreenControl.SplashState.SplashOutro&&!start.loopSplashscreen.gameObject.activeSelf&&start.outroSplashscreen.gameObject.activeSelf&&start.outroSplashSound.Played==1,"the start screen not moved on as the game does on a button (a key typed, or the splashes, sound or state not as the game sets them)");
  Step();Step(HandControls.A);Check(start.outroSplashSound.Played==1&&EscapeKey.Sent.Count==typedBefore,"a start screen already moved on moved again");Step();
  Application.isFocused=true;WindowFocus.InVr=false;
  start.m_state=StartScreenControl.SplashState.End;GameUiControls.Current=new(){PointerMenuOpen=true};Step();Step(HandControls.B);
  Check(EscapeKey.Sent[^1]==(8,true),"menu B does not go back");Application.isFocused=false;Time.realtimeSinceStartup+=.1f;k.Tick();Check(EscapeKey.Sent[^1]==(8,false),"focus loss leaves key pressed");
  // 0.1.210: in VR the menu keys work without the window's focus too (the key brings it forward).
  WindowFocus.InVr=true;Step();Step(HandControls.B);Check(EscapeKey.Sent[^1]==(8,true),"menu B without the window's focus in VR does not go back");Step();
  WindowFocus.InVr=false;int unfocused=EscapeKey.Sent.Count;Step(HandControls.B);Check(EscapeKey.Sent.Count==unfocused,"a desktop key typed without focus outside VR");Step();Application.isFocused=true;
  Step();Step(HandControls.A);Check(EscapeKey.Sent[^1]==(13,true),"menu A not Enter");Step();
  rig.LeftStick.Value=System.Numerics.Vector2.Zero;Step();rig.LeftStick.Value=new(0,-1);Step();
  Check(EscapeKey.Sent[^1]==(40,true),"left stick did not send down arrow");
  rig.MenuRightControls=new(true,HandControls.A,0,0);k.Tick();
  Check(EscapeKey.Sent[^1]==(13,true)&&EscapeKey.Sent[^2]==(40,false),"A during arrow pulse lost confirmation or left arrow stuck");
  Step();rig.LeftStick.Value=System.Numerics.Vector2.Zero;
  GameUiControls.Current.WheelOpen=true;int before=EscapeKey.Sent.Count;Step(HandControls.A);Step(HandControls.B);Check(EscapeKey.Sent.Count==before,"weapon wheel dispatches native menu keys");
  GameUiControls.Current.PointerMenuOpen=false;Step();Step(HandControls.B);Check(EscapeKey.Sent.Count==before,"reload in gameplay dispatches Backspace");
  Check(MenuKeyboard.PromptKey(InputActions.UI_Back)==8&&MenuKeyboard.PromptKey(InputActions.UI_ApplyOption)==13&&MenuKeyboard.PromptKey(InputActions.Weapon_PrimaryFire)==0,"footer command mapping wrong");
  var back=new ButtonPrompt{actionForPrompt=InputActions.UI_Back};back.textRef.rectTransform.position=new(0,0,1);
  var behind=new ButtonPrompt{actionForPrompt=InputActions.UI_Submit};behind.textRef.rectTransform.position=new(0,0,2);
  var gameplay=new ButtonPrompt{actionForPrompt=InputActions.Weapon_PrimaryFire};gameplay.textRef.rectTransform.position=new(0,0,.2f);
  Resources.Items.Add(back);Resources.Items.Add(behind);Resources.Items.Add(gameplay);var prompts=new MenuPrompts();
  Check(prompts.Hit(new(0,0,0),new(0,0,1),out var target,out _,out ushort key)&&target==back&&key==8,"ray selects wrong footer/nearer gameplay prompt");
  back.Groups.Add(new CanvasGroup{alpha=0});Check(prompts.Hit(new(0,0,0),new(0,0,1),out target,out _,out key)&&target==behind&&key==13,"invisible prompt intercepts ray");
  behind.isActiveAndEnabled=false;Check(!prompts.Hit(new(0,0,0),new(0,0,1),out _,out _,out _),"hidden footer remains actionable");
  behind.isActiveAndEnabled=true;Check(!prompts.Hit(new(1,0,0),new(0,0,1),out _,out _,out _),"off-target ray activates footer");
  EscapeKey.Fail=true;Check(!k.Pulse(13),"rejected native key queued as held");EscapeKey.Fail=false;Check(k.Pulse(13),"key failure blocked later retry");k.Dispose();Check(EscapeKey.Sent[^1]==(13,false),"dispose leaves key held");
  Console.WriteLine("PASS: 0.1.236 the start screen moved on by a controller press as the game does on a button (closing splash, its sound, its state), with no key typed; Enter only when it lacks its splashes.");
  Console.WriteLine("PASS: production menu keyboard startup arming/any controller (Enter at the press, without Windows focus in VR; every new press), paired release/focus loss/dispose, A/B menu-only mapping (without focus in VR), and actual prompt ray hit/visibility/action filtering.");
 }
}
enum InputActions{UI_Back,UI_Cancel,UI_DiscardOption,UI_Submit,UI_ApplyOption,Weapon_PrimaryFire}
class StartScreenControl:UnityEngine.Object{internal enum SplashState{PlayerStartInputWait,SplashOutro,End}internal SplashState m_state;internal GameObject gameObject=new();internal bool isActiveAndEnabled=true;internal Director? loopSplashscreen,outroSplashscreen;internal Emitter? outroSplashSound;}
class Director{internal GameObject gameObject=new();}
class Emitter{internal int Played;internal void TryPlayWithDelay()=>Played++;}
class ButtonPrompt:UnityEngine.Object{internal InputActions actionForPrompt;internal GameObject gameObject=new();internal bool isActiveAndEnabled=true;internal Graphic textRef=new();internal Graphic? imageRef=>null;internal List<CanvasGroup> Groups=new();internal UnityEngine.Object[] GetComponentsInParent(Type t,bool a)=>Groups.Cast<UnityEngine.Object>().ToArray();}
class Graphic:UnityEngine.Object{internal bool isActiveAndEnabled=true;internal Color color=new();internal RectTransform rectTransform=new();}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Object{internal T? TryCast<T>() where T:class=>this as T;}
 class GameObject{internal Scene scene=new();internal bool activeSelf=true;internal void SetActive(bool v)=>activeSelf=v;}class Scene{internal bool IsValid()=>true;}
 class CanvasGroup:Object{internal float alpha=1;}
 class RectTransform:Object{internal Vector3 position=new(0,0,1);internal Vector3 forward=>new(0,0,1);internal Rect rect=>new();internal Vector3 InverseTransformPoint(Vector3 p)=>new(p.N-position.N);}
 class Rect{internal bool Contains(Vector2 p)=>Math.Abs(p.x)<.2f&&Math.Abs(p.y)<.15f;}
 class Color{internal float a=1;}
 readonly struct Vector2{internal readonly float x,y;internal Vector2(float a,float b){x=a;y=b;}}
 readonly struct Vector3{internal readonly N N;internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(N n){N=n;}public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator*(Vector3 a,float f)=>new(a.N*f);}
 static class Resources{internal static List<Object> Items=new();internal static Object[] FindObjectsOfTypeAll(Type t)=>Items.Where(t.IsInstanceOfType).ToArray();}
 static class Time{internal static float realtimeSinceStartup;internal static int frameCount;}
 static class Application{internal static bool isFocused=true;}
}
namespace XiiiXR
{
 class CameraRig{internal bool HeadTrackingValid=true;internal bool Frontend=true;internal HandControls LeftControls,MenuRightControls;internal HandControls MenuLeftControls=>LeftControls;internal readonly StickSample LeftStick=new(),RightStick=new();internal void DisarmTrigger(){}}
 class StickSample{internal bool Clicked=>false;internal bool Valid=>true;internal System.Numerics.Vector2 Value;}
 class GameUiControls{internal static GameUiControls? Current;internal bool PointerMenuOpen,WheelOpen;}
 static class QualityMenu{internal static bool Open=>false;}static class ControlsSheet{internal static bool Open=>false;}
 static class EscapeKey{internal static bool GameInFront()=>false;internal static bool Fail;internal static string LastRefusal="";internal static List<(int,bool)> Sent=new();internal static bool SendKey(ushort key,bool down){if(Fail){LastRefusal="refused";return false;}Sent.Add((key,down));return true;}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
 static class WindowFocus{internal static bool InVr;internal static string? Foreground()=>"another program";}
 static class ContactWorld{internal static N V(Vector3 p)=>p.N;internal static Vector3 U(N p)=>new(p);}
}
