using System;
using XiiiXR;
class FocusMarkersTests
{
    static void Check(bool b,string reason){if(!b)throw new Exception(reason);}
    static void Main()
    {
        var stale=new FocusHUDSystem{isActiveAndEnabled=false};
        var live=new FocusHUDSystem();
        UnityEngine.Resources.Items=new object[]{stale,live};
        var marker=new FocusMarker();var widget=new FocusHUDElement();live.m_trackedObjectiveMarkers.Add(marker,widget);
        var ui=new GameUiControls();GameUiControls.Current=ui;
        var rig=new CameraRig();var adapter=new FocusMarkers();
        adapter.Render(rig);Check(live.Triggers==0,"idle forces markers");
        ui.ObjectivesOpen=true;widget.gameObject.activeInHierarchy=false;adapter.Render(rig);
        Check(live.Triggers==1&&stale.Triggers==0,"VR objective key misses native focus trigger or selects inactive HUD");
        Check(OwnedFocusMarker.Last?.Visible==true&&!widget.gameObject.activeInHierarchy,"marker still depends on desktop visibility");
        Check(OwnedFocusMarker.Last!.Position.z<1.2f,"objective card occludes marker");
        int hidden=OwnedFocusMarker.HideCalls;
        live.m_triggerMarkersTimeLeft=0;adapter.Render(rig);
        Check(OwnedFocusMarker.HideCalls==hidden,"active marker toggled off between camera cull/render passes");
        Check(live.Triggers==1&&live.m_triggerMarkersTimeLeft>=.25f,"held panel fails to sustain visibility or repeats sound");
        marker.IsVisible=false;adapter.Render(rig);Check(OwnedFocusMarker.Last?.Visible!=true,"mission-hidden marker exposed");
        marker.IsVisible=true;marker.destroyed=true;adapter.Render(rig);Check(OwnedFocusMarker.Last?.Visible!=true,"destroyed marker resurrected");
        marker.destroyed=false;ui.ObjectivesOpen=false;live.m_triggerMarkersTimeLeft=.1f;adapter.Render(rig);
        Check(live.m_triggerMarkersTimeLeft==.1f,"closing panel overwrites native timer");
        ui.ObjectivesOpen=true;adapter.Render(rig);Check(live.Triggers==2,"second panel opening cannot trigger focus");
        live.m_trackedObjectiveMarkers.Clear();adapter.Render(rig);Check(OwnedFocusMarker.Last?.Visible!=true,"unregistered marker remains visible");
        adapter.Dispose();
        Console.WriteLine("PASS: actual focus adapter triggers native visibility on VR objectives, sustains timer, prefers active HUD and respects mission visibility/destruction.");
    }
}
static class CastExtensions{public static T? TryCast<T>(this object o) where T:class=>o as T;}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace Il2CppSystem.Collections.Generic{class Dictionary<K,V>:System.Collections.Generic.Dictionary<K,V> where K:notnull{}}
namespace UnityEngine
{
    class GameObject{internal bool activeInHierarchy=true;internal Scene scene=new();}
    class Scene{internal bool IsValid()=>true;}
    class Transform{internal Vector3 position=new(1,0,2);internal void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;}}
    struct Vector3
    {
        internal float x,y,z;internal Vector3(float a,float b,float c){x=a;y=b;z=c;}
        internal float magnitude=>MathF.Sqrt(x*x+y*y+z*z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    struct Quaternion{internal static Quaternion Inverse(Quaternion q)=>q;public static Vector3 operator *(Quaternion q,Vector3 v)=>v;}
    static class Time{internal static float realtimeSinceStartup=>1;internal static int frameCount=>(int)(realtimeSinceStartup*1000);}
    static class Resources{internal static object[] Items=Array.Empty<object>();internal static object[] FindObjectsOfTypeAll(Type t)=>Items;}
    class Canvas{internal GameObject gameObject=new();}
}
class FocusMarker{internal UnityEngine.Transform transform=new();internal bool IsVisible=true,destroyed;internal bool isPermanent=>false;internal int GetInstanceID()=>1;}
class FocusHUDElement
{
    internal UnityEngine.GameObject gameObject=new();internal UnityEngine.Transform transform=new();internal int Sets;
    internal void Set(UnityEngine.Vector3 p,float scale,bool arrow,float angle,float distance){Sets++;}
}
class FocusHUDSystem
{
    internal bool isActiveAndEnabled=true;internal UnityEngine.GameObject gameObject=new();internal UnityEngine.Canvas m_focusHUDCanvas=new();
    internal Il2CppSystem.Collections.Generic.Dictionary<FocusMarker,FocusHUDElement> m_trackedObjectiveMarkers=new(),m_trackedAlarmBoxesVfXs=new();
    internal float m_triggerMarkersTimeLeft;internal int Triggers;
    internal static void ShowMarker(System.Collections.Generic.KeyValuePair<FocusMarker,FocusHUDElement> e,bool show){e.Value.gameObject.activeInHierarchy=show;}
    internal void TriggerTrackedMarkers(){Triggers++;m_triggerMarkersTimeLeft=3;}
}
namespace XiiiXR
{
    class CameraRig{internal FakeCamera? MainCamera=>null;internal bool Frontend=>false;internal bool MovieActive=>false;internal UnityEngine.Vector3 HeadPosition=>new();internal UnityEngine.Quaternion HeadRotation=>new();}
    class FakeCamera{internal int cullingMask=>-1;}
    class GameUiControls{internal static GameUiControls? Current;internal bool ObjectivesOpen;}
    static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}

namespace XiiiXR{class OwnedFocusMarker:IDisposable
{
 internal static int HideCalls;internal static OwnedFocusMarker? Last;internal bool Seen,Visible;internal UnityEngine.Vector3 Position;
 internal OwnedFocusMarker(FocusMarker m,FocusHUDElement? w){Last=this;}
 internal void Show(bool v){if(!v)HideCalls++;Visible=v;}
 internal void Pose(UnityEngine.Vector3 p,UnityEngine.Quaternion q,bool edge,float angle,float distance){Visible=true;Position=p;}
 public void Dispose(){Visible=false;}
}}
