using System;using System.Reflection;using XiiiXR;using PlayMagic;using UnityEngine;using N=System.Numerics;
class LocomotionDriverTests
{
 internal static object? Hook(string name,params object?[] args)=>typeof(LocomotionDriver).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static float Axis(int action,int id,float original=0){object?[] a={action,id,original};Hook("RunningAxis",a);return (float)a[2]!;}
 static Vector2 Look(CharacterControllerInputProvider p){object?[] a={p,new Vector2()};Hook("Look",a);return (Vector2)a[1]!;}
 static Vector2 Move(CharacterControllerInputProvider p){object?[] a={p,new Vector2()};Hook("Move",a);return (Vector2)a[1]!;}
 static void Frame(CameraRig r,LocomotionDriver d,float lx=0,float ly=0,float rx=0,bool click=false){Time.frameCount++;r.LeftStick=new(true,new(lx,ly),click);r.RightStick=new(true,new(rx,0));d.Tick();}
 static bool Button(string hook,int action){object?[] a={action,42,false};Hook(hook,a);return (bool)a[2]!;}
 static void Main()
 {
  LocomotionOptions.Load(new BepInEx.Configuration.ConfigFile());var rig=new CameraRig();var player=new CustomCharacterController();var input=new CharacterControllerInputProvider();player.inputProvider=input;rig.PlayerRoot=player.transform;player.transform.Character=player;player.transform.Provider=input;input.transform.parent=player.transform;
  using var d=new LocomotionDriver(rig);Frame(rig,d);Frame(rig,d,ly:1,click:true);
  Check(Move(input).y==1,"VR walking missing");Check(Axis(0,42)==0,"VR axis leaks into native movement twice");
  Hook("BeginRunning",player);Check(Axis(0,42)>0.99f&&Axis(1,42)==0,"native toggle sprint still thinks VR movement is zero");Check(Axis(0,10)==0&&Axis(80,42,20)==20,"axis injected for other player/action");Hook("EndRunning");Check(Axis(0,42)==0,"running scope leaked");
  Frame(rig,d);Hook("BeginRunning",player);Check(Axis(0,42)==0,"stopping fails native sprint resting check");Hook("EndRunning");
  Frame(rig,d,ly:1);GameInputManager.Locked=true;Hook("BeginRunning",player);Check(Axis(0,42)==0,"sprint bypasses input lock");Hook("EndRunning");GameInputManager.Locked=false;
  LocomotionOptions.SnapTurn.Value=true;Frame(rig,d);Frame(rig,d,rx:1);Check(Look(input).x==30&&Look(input).x==0,"snap applied twice in one frame");Frame(rig,d,rx:1);Check(Look(input).x==0,"held snap repeats");
  // 0.1.83: a carried body/hostage turns the character with the head, once per frame, on top of the stick.
  rig.Carry=4;Frame(rig,d);Check(Look(input).x==4&&Look(input).x==0,"carry follow turn missing or repeated");Check(d.LastStickTurn==0,"carry follow counted as stick turn");rig.Carry=0;
  // 0.1.94: crouching for real crouches the character (hold, or toggle with a second press).
  const int crouch=2;
  rig.PhysicalCrouch=true;Frame(rig,d);Check(Button("ButtonDown",crouch)&&Button("ButtonHeld",crouch),"physical crouch not pressed");
  player.IsCrouching=true;Frame(rig,d);Check(!Button("ButtonDown",crouch)&&Button("ButtonHeld",crouch),"physical crouch not held or repeated");Check(d.PhysicalCrouchOwned,"physical crouch not owned");
  rig.PhysicalCrouch=false;Frame(rig,d);Check(Button("ButtonUp",crouch)&&!Button("ButtonHeld",crouch),"standing up does not release crouch");
  Time.realtimeSinceStartup+=.5f;Frame(rig,d);Check(Button("ButtonDown",crouch),"toggle crouch not pressed a second time");player.IsCrouching=false;
  Frame(rig,d);Frame(rig,d);Frame(rig,d);Check(Button("ButtonUp",crouch),"second press not released");Frame(rig,d);Check(!Button("ButtonHeld",crouch)&&!Button("ButtonDown",crouch),"crouch input stuck");
  player.IsCrouching=true;rig.PhysicalCrouch=true;Frame(rig,d);Check(!Button("ButtonDown",crouch)&&!d.PhysicalCrouchOwned,"stick crouch toggled off by sitting down");rig.PhysicalCrouch=false;Frame(rig,d);player.IsCrouching=false;Frame(rig,d);
  LocomotionOptions.Teleport.Value=true;Frame(rig,d);Frame(rig,d,ly:1);Check(Move(input).y==0,"teleport also slides player");object?[] nativeLeak={input,new Vector2(.5f,1)};Hook("Move",nativeLeak);Check(((Vector2)nativeLeak[1]!).y==0,"native gamepad mirror bypasses teleport");Hook("BeginRunning",player);Check(Axis(0,42)==0,"teleport aim starts sprint");Hook("EndRunning");
  // 0.1.108: water controls (teleport mode must not stop swimming).
  const int jump=4;
  void Swim(float lx=0,float ly=0,float rx=0,float ry=0){Time.frameCount++;Time.realtimeSinceStartup+=.01f;rig.LeftStick=new(true,new(lx,ly));rig.RightStick=new(true,new(rx,ry));d.Tick();}
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Surfaced;Swim();Swim();
  // 0.1.110: same sticks as on land: left stick swims, right X turns, right up/down rises/dives.
  Swim(ly:1);Check(Move(input).y>.99f,"left stick does not swim forward");Check(!Button("ButtonHeld",jump),"left stick forward rises");
  Swim(lx:1);Check(Move(input).x>.99f,"left stick does not swim sideways");
  Swim();Swim(rx:1);Check(Look(input).x!=0&&Move(input).y==0,"right stick X does not turn / still moves");
  Swim();Swim(ry:1);Check(Button("ButtonDown",jump)&&Button("ButtonHeld",jump),"right stick up does not rise");
  Swim();Swim(ry:-1);Check(Button("ButtonDown",crouch)&&Button("ButtonHeld",crouch),"right stick down does not dive");
  Swim();Check(!Button("ButtonHeld",crouch)&&!Button("ButtonHeld",jump),"swim up/down stuck");
  // At the surface, swimming forward while looking down dives.
  rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitX,.7f);Swim(ly:1);Check(Button("ButtonHeld",crouch),"looking down + forward does not dive");
  rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitX,-.7f);Swim(ly:1);Check(!Button("ButtonHeld",jump),"looking up at the surface jumps");
  // Under water, forward swimming follows the gaze: vertical velocity, less horizontal.
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Submerged;player.swimSpeed=4;Swim(ly:1);
  Check(!Button("ButtonHeld",jump)&&!Button("ButtonHeld",crouch),"gaze swim presses native up/down under water");
  float Vy(){object?[] a={player,new Vector3()};Hook("SwimVelocity",a);return ((Vector3)a[1]!).y;}
  float up=Vy();Check(up>2.4f&&up<2.7f,"looking up 40 degrees does not rise along the gaze: "+up);
  Check(MathF.Abs(Move(input).y-MathF.Cos(.7f))<.02f,"horizontal part not reduced along the gaze");
  rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitX,.7f);Swim(ly:1);Check(Vy()< -2.4f,"looking down does not dive along the gaze");
  Swim();Check(Vy()==0,"no input still moves vertically");
  Swim(ly:1,ry:1);Check(Vy()==0&&Button("ButtonHeld",jump),"stick up under water not native");
  rig.Head=N.Quaternion.Identity;Swim();Swim();Check(!Button("ButtonHeld",jump)&&Move(input).y==0,"standing still still rises/moves");
  // Breaststroke: hands spread apart at 1.5 m/s for 0.3 s -> forward swim along the gaze, controllers buzz.
  int buzz=rig.Buzz;rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitX,-.7f);
  for(int i=0;i<30;i++){rig.L-=new N.Vector3(.015f,0,0);rig.R+=new N.Vector3(.015f,0,0);Swim();}
  Check(Move(input).y>.2f&&rig.Buzz>buzz&&Vy()>.5f,"arm stroke does not swim forward along the gaze / vibrate");
  rig.Head=N.Quaternion.Identity;
  for(int i=0;i<250;i++){rig.L+=new N.Vector3(.0005f,0,0);rig.R-=new N.Vector3(.0005f,0,0);Swim();}
  Check(Move(input).y==0,"stroke push does not fade out");
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Idling;Swim();Swim(ly:1);Check(Move(input).y==0,"left stick moves on land in teleport mode");
  object?[] land={player,new Vector3(0,-3,0)};Hook("SwimVelocity",land);Check(((Vector3)land[1]!).y==-3,"gaze swim changes land velocity");
  LocomotionOptions.Teleport.Value=false;Swim();Swim(ly:1);Check(Move(input).y>.99f,"land controls not restored after water");
  // 0.1.163: on the rope the right stick's forward/back swings where the head looks (the game swings along the character's forward).
  GrappleVr.Current=new GrappleVr{OnRope=true};rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY,MathF.PI/2);
  Time.frameCount++;rig.LeftStick=new(true,new(0,0));rig.RightStick=new(true,new(0,1));d.Tick();
  var rope=Move(input);
  Check(MathF.Abs(rope.x-1)<.02f&&MathF.Abs(rope.y)<.02f,"rope swing not turned toward the head's heading: "+rope.x+","+rope.y);
  Check(MathF.Abs(Axis(1,42)-1)<.02f&&MathF.Abs(Axis(0,42))<.02f,"rope swing on the game's own axes not turned toward the head's heading");
  rig.Head=N.Quaternion.Identity;Time.frameCount++;rig.RightStick=new(true,new(0,-1));d.Tick();
  rope=Move(input);Check(MathF.Abs(rope.y+1)<.02f&&MathF.Abs(rope.x)<.02f,"rope swing back lost");
  // 0.1.197: the hook fired from the right hand: the LEFT stick swings (the right stick climbs, in GrappleVr).
  GrappleVr.Current.RightHanded=true;Time.frameCount++;rig.LeftStick=new(true,new(0,1));rig.RightStick=new(true,new(0,-1));d.Tick();
  rope=Move(input);Check(MathF.Abs(rope.y-1)<.02f&&MathF.Abs(rope.x)<.02f,"right-handed rope: the left stick does not swing (or the right one does): "+rope.x+","+rope.y);
  Time.frameCount++;rig.LeftStick=new(true,new(0,0));d.Tick();rope=Move(input);Check(MathF.Abs(rope.y)<.02f,"right-handed rope: the right stick swings");
  Console.WriteLine("PASS: 0.1.197 on the rope of a hook fired from the right hand the left stick swings, the right stick does not.");
  GrappleVr.Current=null;Time.frameCount++;rig.RightStick=new(true,new(0,0));rig.LeftStick=new(true,new(0,0));d.Tick();
  // 0.1.171: on the game's ladder the stick is given as it is (forward climbs up whatever the head's heading).
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Climbing;rig.Head=N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY,MathF.PI/2);
  Time.frameCount++;rig.LeftStick=new(true,new(0,1));d.Tick();var climb=Move(input);
  Check(MathF.Abs(climb.y-1)<.02f&&MathF.Abs(climb.x)<.02f,"ladder climb turned by the head's heading: "+climb.x+","+climb.y);
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Idling;Time.frameCount++;d.Tick();var walk=Move(input);
  Check(MathF.Abs(walk.x-1)<.02f,"walking no longer follows the head after the ladder: "+walk.x+","+walk.y);
  rig.Head=N.Quaternion.Identity;Time.frameCount++;rig.LeftStick=new(true,new(0,0));d.Tick();
  // 0.1.173: the left stick held forward while jumping into the water keeps swimming (no letting go needed), and walking on coming out.
  Time.frameCount++;rig.LeftStick=new(true,new(0,1));d.Tick();Check(Move(input).y>.99f,"no walking before the water");
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Surfaced;Time.frameCount++;d.Tick();Check(Move(input).y>.99f,"the stick held forward stopped on entering the water");
  player.CurrentPlayerState=CustomCharacterController.PlayerStates.Idling;Time.frameCount++;d.Tick();Check(Move(input).y>.99f,"the stick held forward stopped on leaving the water");
  Time.frameCount++;rig.LeftStick=new(true,new(0,0));d.Tick();
  Console.WriteLine("PASS: rope swing follows the head's heading. PASS: actual locomotion adapter supplies VR axes exclusively to native sprint, preserves player/action/lock scope; stop returns zero; snap consumed once; teleport suppresses stick slide; water: left stick swims, right stick turns/rises/dives, looking down at the surface dives, under water forward/strokes follow the gaze (vertical velocity), strokes buzz and fade, land untouched.");
 }
}
namespace BepInEx.Configuration{class ConfigEntry<T>{internal T Value;internal ConfigEntry(T v){Value=v;}}class ConfigFile{internal ConfigEntry<T> Bind<T>(string s,string k,T v,string d)=>new(v);}}
namespace HarmonyLib{class Harmony{internal Harmony(string s){}internal void Patch(MethodInfo? m,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null,HarmonyMethod? finalizer=null){}internal void UnpatchSelf(){}}class HarmonyMethod{internal HarmonyMethod(Type t,string m){}}static class AccessTools{internal static MethodInfo DeclaredMethod(Type t,string m)=>typeof(object).GetMethod("ToString")!;}}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Object{internal T? TryCast<T>() where T:class=>this as T;internal IntPtr Pointer= (IntPtr)42;}
 class GameObject{internal bool activeInHierarchy=true;}
 class Transform:Object{internal string name="player";internal Quaternion rotation=Quaternion.identity;internal Transform? parent;internal CustomCharacterController? Character;internal CharacterControllerInputProvider? Provider;internal Object? GetComponent(Type t)=>t==typeof(CustomCharacterController)?Character:Provider;internal bool IsChildOf(Transform t)=>this==t||parent==t;}
 struct Vector3{internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 struct Quaternion{internal float x,y,z,w;internal static Quaternion identity=>new(){x=0,y=0,z=0,w=1};}
 struct Vector2{internal float x,y;internal Vector2(float a,float b){x=a;y=b;}public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);internal static Vector2 ClampMagnitude(Vector2 a,float v){float n=MathF.Sqrt(a.x*a.x+a.y*a.y);return n>v?new(a.x/n*v,a.y/n*v):a;}}
 static class Time{internal static float realtimeSinceStartup=1,timeScale=1,deltaTime=.01f;internal static int frameCount=1;}
 static class Application{internal static bool isFocused=true;}
}
class OwnerInfo{internal bool IsPlayer=>true;internal bool IsInvalid=>false;internal int Id=>42;}
class PauseMenuControl{internal static bool HackGameIsPaused=>false;}
enum InputActions{Movement_ForwardBackwards=0,Movement_LeftRight=1,Movement_Sprint=3,Movement_Jump=4,Movement_Crouch=2}
static class GameInputManager{internal static bool Locked;internal static int InputActionToRewiredID(InputActions a)=>(int)a;internal static bool IsInputLocked(int id)=>Locked;internal static bool IsAxisLocked(int id)=>Locked;}
namespace PlayMagic
{
 class CustomCharacterController:UnityEngine.Object{internal Transform transform=new();internal GameObject gameObject=new();internal UnityEngine.Object? inputProvider;internal OwnerInfo GetOwner()=>new();internal bool IsCrouching{get;set;}internal PlayerStates CurrentPlayerState{get;set;}=PlayerStates.Idling;internal float swimSpeed;internal enum PlayerStates{None,Idling,Walking,Running,Crouching,Jumping,Sliding,Falling,Climbing,Surfaced,Submerged,Grappling,Mounted}}
 class CharacterControllerInputProvider:UnityEngine.Object{internal Transform transform=new();internal bool isActiveAndEnabled=true;internal int ownerID=42;internal OptionsClass Options=new();internal bool CanProcessInputDirection()=>true;internal TypeInfo GetIl2CppType()=>new();}
 class TypeInfo{internal string FullName=>"mock provider";}class OptionsClass{internal bool GetToggleCrouch()=>true;internal bool GetToggleSprint()=>true;}
}
namespace XiiiXR
{
 class GrappleVr{internal static GrappleVr? Current;internal bool OnRope;internal bool RightHanded;}
 class CameraRig{internal bool PhysicalCrouch{get;set;}internal HandControls RightControls=default,LeftControls=default;internal float Carry;internal float CarryFollowTurn(float dt)=>Carry;internal Transform? PlayerRoot;internal bool Scripted=>false;internal bool HeadTrackingValid=>true;internal StickSample LeftStick,RightStick;internal void PollControls(){}internal void AlignCollisionBody(CustomCharacterController c){}internal N.Quaternion Head=N.Quaternion.Identity;internal bool TryWorldHeadRotation(out N.Quaternion q){q=Head;return true;}internal N.Vector3 L=new(-.2f,1.3f,.3f),R=new(.2f,1.3f,.3f),H=new(0,1.6f,0);internal int Buzz;internal bool PhysicalHand(bool right,out N.Vector3 p){p=right?R:L;return true;}internal bool PhysicalHead(out N.Vector3 p){p=H;return true;}internal void SwimHaptics(float a){if(a>0)Buzz++;}}
 class FirstPersonVisibility{internal void Tick(CustomCharacterController? c,bool h){}internal void Restore(){}}
 class TeleportDriver:IDisposable{internal void Cancel(){}internal void Tick(CameraRig r,CustomCharacterController? c,bool a,bool b,bool d){}public void Dispose(){}}
 class GameUiControls{internal static GameUiControls? Current=>null;internal bool BlocksGameplay=>false;}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
namespace XiiiXR { static class WindowFocus { internal static bool Playable=>UnityEngine.Application.isFocused; } }
namespace XiiiXR { sealed class CeilingFans { internal static CeilingFans? Current=null; internal float ConsumeTurn()=>0; } }
