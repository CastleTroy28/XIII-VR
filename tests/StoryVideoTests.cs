#pragma warning disable CS0649
using System;using XiiiXR;using UnityEngine;using UnityEngine.Video;using UnityEngine.UI;
class StoryVideoTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var native=new CutscenePlayer{m_videoPlayer=new VideoPlayer{renderMode=VideoRenderMode.CameraNearPlane,isPlaying=true,texture=new Texture{width=1920,height=1080}}};
  Resources.Items=new UnityEngine.Object[]{native};
  var rig=new CameraRig();
  using(var bridge=new StoryVideo())
  {
   bridge.Tick();bridge.Render(rig);
   Check(bridge.Active&&native.m_videoPlayer.renderMode==VideoRenderMode.APIOnly,"active native movie not bridged");
   Check(RawImage.Last.texture==native.m_videoPlayer.texture&&!RawImage.Last.raycastTarget,"decoder output absent or steals skip input");
   Check(Canvas.Last.worldCamera==rig.MainCamera&&Canvas.Last.sortingOrder<0,"video does not preserve foreground subtitle ordering");
   Check(bridge.Skip()&&native.Stops==1&&!native.m_anyPlayerCanSkip,"explicit VR skip fails or changes native permission persistently");native.m_anyPlayerCanSkip=true;
   Check(bridge.Skip()&&native.Stops==2,"native skip callback missing");
   native.m_videoPlayer.isPlaying=false;native.m_videoPlayer.isPaused=true;Time.realtimeSinceStartup+=1;bridge.Tick();
   Check(bridge.Active&&RawImage.Last.texture!=null,"paused frame disappeared");
   native.m_videoIsComplete=true;bridge.Tick();
   Check(!bridge.Active&&native.m_videoPlayer.renderMode==VideoRenderMode.CameraNearPlane&&RawImage.Last.texture==null,"completed movie failed to restore original mode");
   native.m_videoIsComplete=false;native.m_videoPlayer.isPaused=false;native.m_videoPlayer.isPlaying=true;
   native.m_videoPlayer.renderMode=VideoRenderMode.RenderTexture;Time.realtimeSinceStartup+=1;bridge.Tick();
   Check(bridge.Active,"second opening video missing");
   native.gameObject.activeInHierarchy=false;bridge.Tick();
   Check(!bridge.Active&&native.m_videoPlayer.renderMode==VideoRenderMode.RenderTexture,"scene deactivation failed to restore borrowed mode");
   native.gameObject.activeInHierarchy=true;Time.realtimeSinceStartup+=1;bridge.Tick();
   Check(bridge.Active,"restarting movie after scene returned");
   // 0.1.162: the players are kept; the scene-wide search is rare.
   native.m_videoIsComplete=true;bridge.Tick();Check(!bridge.Active,"completed movie still active");
   SceneScan.Report();
   for(int i=0;i<20;i++){Time.realtimeSinceStartup+=.5f;bridge.Tick();}
   int searches=SceneScan.Count;
   Check(searches<=1,"the movie search ran "+searches+" times in 10 s (the kept players should be checked instead)");
   native.m_videoIsComplete=false;Time.realtimeSinceStartup+=.3f;bridge.Tick();
   Check(bridge.Active,"kept movie player not checked between searches");
   native.m_videoIsComplete=true;bridge.Tick();SceneScan.Report();
   SceneScan.SceneChanged();Time.realtimeSinceStartup+=.01f;bridge.Tick();
   Check(SceneScan.Count==1,"no new movie search after a scene change");
  }
  Check(native.m_videoPlayer.renderMode==VideoRenderMode.RenderTexture,"VR shutdown failed to restore native video");
  Check(native.m_videoPlayer.isPlaying,"bridge changed native playback");
  Console.WriteLine("PASS: decoded frame bridge, pause, consecutive intro movies, native scene deactivation, original render-mode restoration and shutdown. Decoder/GPU output requires game testing.");
 }
}
namespace XiiiXR
{
 class CameraRig{internal Camera MainCamera=new();internal Vector3 HeadPosition;internal Quaternion HeadRotation=Quaternion.identity;internal Vector3 CinemaPosition;internal Quaternion CinemaRotation=Quaternion.identity;}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}
class CutscenePlayer:UnityEngine.Component{internal VideoPlayer? m_videoPlayer;internal bool m_videoIsComplete,m_anyPlayerCanSkip;internal int Stops;internal void TryStopCurrentVideo(){Stops++;}}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Object{internal string name="";internal T? TryCast<T>() where T:class=>this as T;internal static void Destroy(Object o){}internal static void DontDestroyOnLoad(Object o){}}
 class Component:Object{private GameObject? go;internal GameObject gameObject{get=>go??=new GameObject("");set=>go=value;}internal Transform transform=>gameObject.transform;}
 class GameObject:Object
 {
  internal bool activeInHierarchy=true;internal int layer;internal Scene scene=new();internal Transform transform=new RectTransform();
  internal GameObject(string s){name=s;}
  internal void SetActive(bool b){activeInHierarchy=b;}
  internal Object AddComponent(Type t){var c=(Component)Activator.CreateInstance(t)!;c.gameObject=this;return c;}
 }
 class Scene{internal bool IsValid()=>true;}
 class Transform:Object{internal Vector3 localPosition,localScale;internal Quaternion localRotation;internal void SetParent(Transform p,bool b){}internal void SetPositionAndRotation(Vector3 p,Quaternion q){}}
 class RectTransform:Transform{internal Vector2 pivot,sizeDelta,anchorMin,anchorMax,offsetMin,offsetMax;}
 class Canvas:Component{internal static Canvas Last=null!;public Canvas(){Last=this;}internal RenderMode renderMode;internal int sortingOrder;internal Camera worldCamera=null!;}
 enum RenderMode{WorldSpace}
 class Camera:Component{}
 class Texture:Object{internal int width,height;}
 struct Color{internal static Color white=>new();}
 struct Vector2{internal float x,y;internal Vector2(float a,float b){x=a;y=b;}internal static Vector2 zero=>new();internal static Vector2 one=>new(1,1);}
 struct Vector3{internal Vector3(float a,float b,float c){}internal static Vector3 zero=>new();internal static Vector3 one=>new(1,1,1);public static Vector3 operator*(Vector3 a,float b)=>a;public static Vector3 operator+(Vector3 a,Vector3 b)=>a;}
 struct Quaternion{internal static Quaternion identity=>new();public static Vector3 operator*(Quaternion q,Vector3 v)=>v;}
 static class Resources{internal static Object[] Items=Array.Empty<Object>();internal static Object[] FindObjectsOfTypeAll(Type t)=>Items;}
 static class Time{internal static float realtimeSinceStartup;internal static int frameCount=>(int)(realtimeSinceStartup*1000);}
}
namespace UnityEngine.UI
{
 class RawImage:Component{internal static RawImage Last=null!;public RawImage(){Last=this;}internal Texture? texture;internal bool raycastTarget;internal Color color;internal RectTransform rectTransform=>(RectTransform)transform;}
}
namespace UnityEngine.Video
{
 enum VideoRenderMode{CameraNearPlane,APIOnly,RenderTexture}
 class VideoPlayer:Component{internal VideoRenderMode renderMode;internal bool isPlaying,isPaused,isPrepared;internal Texture? texture;internal long frame;}
}
