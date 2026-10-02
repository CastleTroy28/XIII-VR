using System;using XiiiXR;using UnityEngine;using N=System.Numerics.Vector3;using Q=System.Numerics.Quaternion;
class WorldCanvasTests
{
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static void Main()
 {
  var rect=new RectTransform();rect.localPosition=new Vector3(1,2,3);rect.localScale=new Vector3(2,3,4);rect.sizeDelta=new Vector2(1400,900);
  rect.parent=new Transform{lossyScale=new Vector3(-2,3,1)};
  var canvas=new Canvas{transform=rect,renderMode=RenderMode.ScreenSpaceOverlay};var oldCamera=new Camera();canvas.worldCamera=oldCamera;
  var rig=new CameraRig{Frontend=true,HeadPosition=new Vector3(0,1.6f,0),HeadRotation=Quaternion.identity,MainCamera=new Camera()};
  var pin=new WorldCanvas(canvas);pin.Pose(rig);
  Check(canvas.renderMode==RenderMode.WorldSpace&&!canvas.Scaler.enabled,"native scaler overwrites world UI size");
  Check(rect.localScale.x<0&&Math.Abs(rect.localScale.x*rect.parent.lossyScale.x-2.2f/1920)<1e-7f,"negative parent mirrors panel");
  var initial=rect.position;rig.HeadPosition=new Vector3(.2f,1.6f,0);rig.HeadRotation=Quaternion.Euler(0,45,0);pin.Pose(rig);
  Check(N.Distance(initial.N,rect.position.N)<1e-6f,"frontend menu follows head while aiming");
  pin.Recenter();pin.Pose(rig);Check(N.Distance(initial.N,rect.position.N)>.2f,"recenter cannot move menu back in front");
  pin.Restore();Check(canvas.renderMode==RenderMode.ScreenSpaceOverlay&&canvas.worldCamera==oldCamera&&canvas.Scaler.enabled,"canvas/scaler/camera not restored after gameplay transition");
  Check(rect.localPosition.N==new N(1,2,3)&&rect.localScale.N==new N(2,3,4)&&rect.sizeDelta.x==1400,"native transform/layout not restored");
  var compact=new Canvas{transform=new RectTransform{sizeDelta=new Vector2(720,96)},name="WeaponInventoryIndicator",renderMode=RenderMode.ScreenSpaceOverlay};
  new WorldCanvas(compact).Pose(rig);
  Check(((RectTransform)compact.transform).sizeDelta.y==96,"Y-cycle canvas height stretched into dark columns");
  // Late-created subtitle/menu roots share one placement, including mode changes.
  var menu=new Canvas{transform=new RectTransform(),name="MainCanvas",renderMode=RenderMode.ScreenSpaceOverlay};
  Resources.Items=new UnityEngine.Object[]{menu};Time.realtimeSinceStartup=1;
  using(var owner=new FrontendMenu(rig))
  {
   owner.Tick();owner.Render();var first=menu.transform.position;
   rig.HeadPosition=new Vector3(1,2,3);Time.realtimeSinceStartup+=1;
   var subtitles=new Canvas{transform=new RectTransform(),name="SubtitlePlayer",renderMode=RenderMode.ScreenSpaceOverlay};
   Resources.Items=new UnityEngine.Object[]{menu,subtitles};owner.Tick();owner.Render();
   Check(menu.transform.position.N==first.N&&subtitles.transform.position.N==first.N,"late subtitle layer acquired a separate menu anchor");
   rig.Frontend=false;rig.Scripted=true;Time.realtimeSinceStartup+=1;owner.Tick();owner.Render();
   Check(subtitles.renderMode==RenderMode.WorldSpace&&subtitles.transform.position.N==menu.transform.position.N,"game transition detached native subtitles");
   int settledDiscoveries=CinematicFrame.Discoveries;
   for(int tick=0;tick<60;tick++){Time.realtimeSinceStartup+=1;owner.Tick();}
   Check(CinematicFrame.Discoveries==settledDiscoveries,"unchanged gameplay periodically rescans all UI graphics");
   owner.SceneChanged();Time.realtimeSinceStartup+=1;owner.Tick();
   Check(CinematicFrame.Discoveries==settledDiscoveries+1,"scene change fails to refresh UI graphics");
   var scenePosition=menu.transform.position;rig.HeadPosition=new Vector3(2,3,4);owner.Render();
   Check(menu.transform.position.N!=scenePosition.N&&menu.transform.position.N==subtitles.transform.position.N,"cinematic layers move independently");
   Check(menu.transform.localScale.N==subtitles.transform.localScale.N,"cinematic layers use different scale");
  }
  Check(menu.renderMode==RenderMode.ScreenSpaceOverlay,"shared owner failed to restore native canvas on stop");
  // Execute production FrontendMenu with a failing optional adapter. Exceptions
  // must not escape to CameraRig.Tick/TrackedCamera.ApplyEyes and kill tracking.
  using(var owner=new FrontendMenu(rig))
  {
   Time.realtimeSinceStartup=20;CinematicFrame.FailDiscover=true;
   int tries=CinematicFrame.Discoveries;owner.Tick();owner.Tick();
   Check(CinematicFrame.Discoveries==tries+1,"failed discovery spins on each frame");
   CinematicFrame.FailDiscover=false;Time.realtimeSinceStartup+=.6f;owner.Tick();
   Check(CinematicFrame.Discoveries==tries+2,"UI discovery never recovers after native failure");
   CinematicFrame.FailRender=true;owner.Render();
   CinematicFrame.FailRender=false;Time.realtimeSinceStartup+=.6f;owner.Render();
   Check(CinematicFrame.Rendered,"UI rendering does not recover independently of tracking");
   // Checkpoint/new scene bypasses the optional retry timer.
   CinematicFrame.FailRender=true;owner.Render();CinematicFrame.FailRender=false;CinematicFrame.Rendered=false;
   owner.SceneChanged();owner.Render();Check(CinematicFrame.Rendered,"scene restore remains blocked by old UI failure");
  }
  Console.WriteLine("PASS: actual WorldCanvas cancels mirrored parent scale; holds frontend panel while head moves; recenters; restores original mode/camera/scaler/transform/layout.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Object{internal string name="";internal T? TryCast<T>() where T:class=>this as T;}
 class Transform:Object{internal Transform? parent;internal Vector3 localPosition,localScale,position;internal Vector3 lossyScale=Vector3.one;internal Quaternion localRotation;internal void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;}}
 class RectTransform:Transform{internal Vector2 sizeDelta,anchorMin,anchorMax,pivot;}
 class Canvas:Object{internal GameObject gameObject=new();internal Canvas rootCanvas=>this;internal RenderMode renderMode;internal Camera? worldCamera;internal Transform transform=null!;internal readonly UI.CanvasScaler Scaler=new();internal Object GetComponent(Type t)=>Scaler;}
 class Camera:Object{}
 class GameObject:Object{internal Scene scene=new();}
 class Scene{internal bool IsValid()=>true;}
 static class Resources{internal static Object[] Items=Array.Empty<Object>();internal static Object[] FindObjectsOfTypeAll(Type t)=>Items;}
 static class Time{internal static float realtimeSinceStartup;internal static int frameCount=>(int)(realtimeSinceStartup*1000);}
 enum RenderMode{ScreenSpaceOverlay,WorldSpace}
 readonly struct Vector2{internal readonly float x,y;internal Vector2(float a,float b){x=a;y=b;}}
 readonly struct Vector3{internal readonly N N;internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(N p){N=p;}internal static Vector3 one=>new(1,1,1);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);}
 readonly struct Quaternion{internal readonly Q Q;Quaternion(Q q){Q=q;}internal static Quaternion identity=>new(Q.Identity);internal Vector3 eulerAngles{get{var f=N.Transform(N.UnitZ,Q);return new(0,MathF.Atan2(f.X,f.Z)*180/MathF.PI,0);}}internal static Quaternion Euler(float x,float y,float z)=>new(Q.CreateFromYawPitchRoll(y*MathF.PI/180,x*MathF.PI/180,z*MathF.PI/180));public static Vector3 operator*(Quaternion q,Vector3 v)=>new(N.Transform(v.N,q.Q));}
}
namespace UnityEngine.UI{class CanvasScaler:UnityEngine.Object{internal bool enabled=true;}}
namespace XiiiXR
{
 class CameraRig{internal bool Frontend,Scripted,MovieActive=false;internal Vector3 HeadPosition;internal Quaternion HeadRotation;internal Camera MainCamera=null!;}
 class CinematicFrame:IDisposable{internal static bool FailDiscover,FailRender,Rendered;internal static int Discoveries;internal void Discover(){Discoveries++;if(FailDiscover)throw new NullReferenceException("stale native frame");}internal void Render(bool b){if(FailRender)throw new NullReferenceException("stale native renderer");Rendered=true;}public void Dispose(){}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
 class GameUiControls{internal static GameUiControls? Current=>null;internal bool PointerMenuOpen=>false;}
}

namespace XiiiXR{class CycleIcons:IDisposable{internal static UnityEngine.Vector2 LayoutSize(UnityEngine.Canvas c,UnityEngine.Vector2 v)=>v;internal void Discover(){}internal void Render(){}public void Dispose(){}}}

namespace XiiiXR{internal class FullscreenEffects:System.IDisposable{internal void Discover(){}internal void Render(CameraRig r){}public void Dispose(){}}}

namespace XiiiXR{class CinematicMask:System.IDisposable{internal void Render(CameraRig r){}public void Dispose(){}}static class FramePerformance{internal static long Begin()=>0;internal static void Scope(string s,long t){}}}

namespace XiiiXR {sealed class FocusMarkers {internal void Render(CameraRig rig){} internal void Dispose(){}}}

public static class PauseMenuControl{public static bool HackGameIsPaused;}
