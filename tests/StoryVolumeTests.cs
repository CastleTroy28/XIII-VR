using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using XiiiXR;
class StoryVolumeTests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0001f,why+" got "+a+" expected "+b);
    static PostProcessVolume Volume(float saturation,float priority=0,float weight=1)
    {
        return new PostProcessVolume{priority=priority,weight=weight,profileRef=new PostProcessProfile{
            settings=new List<PostProcessEffectSettings>{new ColorGrading{
                saturation=new FloatParameter{overrideState=true,value=saturation},
                enabled=new BoolParameter{overrideState=true,value=true}}}}};
    }
    static ColorGrading Grade(PostProcessVolume volume)=>(ColorGrading)volume.profileRef.settings[0];
    static void Main()
    {
        var layer=new PostProcessLayer{enabled=false,volumeLayer=new LayerMask{value=1},volumeTrigger=new Transform()};
        var camera=new Camera{Layer=layer};var reader=new StoryVolumeColor(camera);
        var baseVolume=Volume(-100);var top=Volume(0,10,.5f);var ignored=Volume(-100,20);ignored.gameObject.layer=2;
        var inactive=Volume(-100,30);inactive.isActiveAndEnabled=false;
        var asset=Volume(-100,40);asset.gameObject.scene.Valid=false;
        PostProcessManager.instance.Volumes=new[]{top,ignored,baseVolume,inactive,asset};
        Near(reader.Read(),.5f,"priority, layer/asset/active filtering or volume weight incorrect");
        Check(!layer.enabled,"reader enabled unsafe desktop postprocessing");
        int finds=PostProcessManager.instance.Reads;
        Grade(top).saturation.value=-50;
        Near(reader.Read(),.75f,"animated native saturation not reflected immediately");
        Check(PostProcessManager.instance.Reads==finds,"sampling enumerates volumes every eye/frame");
        Grade(top).enabled.value=false;
        Near(reader.Read(),0,"higher-priority disabled color grading ignored");
        Grade(top).enabled.overrideState=false;
        Near(reader.Read(),.75f,"disabled parameter without override erases base state");
        Grade(top).saturation.overrideState=false;
        Near(reader.Read(),1,"non-overridden saturation changes base state");
        Grade(baseVolume).active=false;Near(reader.Read(),0,"inactive setting still contributes");
        Grade(baseVolume).active=true;baseVolume.weight=0;Near(reader.Read(),0,"zero weight still contributes");

        var local=Volume(-100);local.isGlobal=false;local.blendDistance=1;
        local.Colliders=new[]{new Collider{Point=new Vector3(1,0,0)}};
        PostProcessManager.instance.Volumes=new[]{local};Time.realtimeSinceStartup=2;
        Near(reader.Read(),.75f,"native local volume distance/2 convention differs");
        local.Colliders[0].Point=new Vector3(0,0,0);local.blendDistance=0;
        Near(reader.Read(),1,"zero blend inside volume loses full influence");
        local.Colliders[0].Point=new Vector3(.1f,0,0);
        Near(reader.Read(),0,"zero blend outside volume contributes");
        local.blendDistance=1;local.Colliders[0].Point=new Vector3(3,0,0);
        Near(reader.Read(),0,"outside blend range still contributes");
        local.Colliders[0].Point=new Vector3(0,0,0);local.Colliders[0].enabled=false;
        Near(reader.Read(),0,"disabled collider contributes");
        local.Colliders[0].enabled=true;layer.volumeTrigger=null;
        Near(reader.Read(),0,"local volume applies without a trigger");
        local.isGlobal=true;Near(reader.Read(),1,"global volume requires a trigger");
        Grade(local).saturation.value=float.NaN;Near(reader.Read(),0,"nonfinite saturation reaches LUT");
        Grade(local).saturation.value=-100;local.weight=float.NaN;Near(reader.Read(),0,"nonfinite weight reaches LUT");
        local.weight=1;local.Destroyed=true;Near(reader.Read(),0,"destroyed cached volume used");
        PostProcessManager.instance.Volumes=Array.Empty<PostProcessVolume>();Time.realtimeSinceStartup=4;
        Near(reader.Read(),0,"scene unload leaves stale grade");
        var replacement=Volume(-100);PostProcessManager.instance.Volumes=new[]{replacement};Time.realtimeSinceStartup=6;
        Near(reader.Read(),1,"replacement scene volume not discovered");
        Check(!layer.enabled,"reader changed native layer state");
        Near(StoryVolumeMath.Influence(float.PositiveInfinity,1),0,"missing collider influence");
        Near(StoryVolumeMath.Influence(0,float.NaN),0,"invalid blend radius influence");
        // 0.1.122: level volumes grey the view only in cutscenes; memories are always grey.
        Near(StoryVolumeMath.Applied(1,false,false),0,"a level volume greyed plain gameplay");
        Near(StoryVolumeMath.Applied(.6f,false,true),.6f,"cutscene volume saturation lost");
        Near(StoryVolumeMath.Applied(0,true,false),1,"memory not grey");
        Near(StoryVolumeMath.Applied(float.NaN,false,true),0,"invalid volume value");
        Console.WriteLine("PASS: production volume adapter follows priority, bool/saturation overrides, animated weights/values, layer and lifecycle filtering, local collider blending, disabled renderer and bounded discovery. Unity APIs are stand-ins; scene content needs the game.");
    }
}
namespace UnityEngine
{
    class Object
    {
        internal bool Destroyed;
        public static bool operator ==(Object? a,Object? b)=>(a is null||a.Destroyed)?b is null||b.Destroyed:ReferenceEquals(a,b);
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? o)=>ReferenceEquals(this,o);
        public override int GetHashCode()=>base.GetHashCode();
        internal T? TryCast<T>()where T:class=>this as T;
    }
    class Scene{internal bool Valid=true;internal bool IsValid()=>Valid;}
    class GameObject{internal int layer;internal Scene scene=new();}
    class Component:Object{internal GameObject gameObject=new();internal string name="volume";}
    class Camera:Component
    {
        internal PostProcessLayer Layer=null!;
        internal Component GetComponent(Type type)=>Layer;
    }
    class Transform:Component{internal Vector3 position=default;}
    class Collider:Component
    {
        internal bool enabled=true;internal Vector3 Point;
        internal Vector3 ClosestPoint(Vector3 position)=>Point;
    }
    struct Vector3
    {
        float x,y,z;internal Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        internal float sqrMagnitude=>x*x+y*y+z*z;
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    struct LayerMask{internal int value;}
    static class Time{internal static float realtimeSinceStartup;}
}
namespace UnityEngine.Rendering.PostProcessing
{
    class PostProcessEffectSettings:UnityEngine.Object{internal bool active=true;}
    class FloatParameter{internal bool overrideState;internal float value;}
    class BoolParameter{internal bool overrideState,value;}
    class ColorGrading:PostProcessEffectSettings{internal FloatParameter saturation=new();internal BoolParameter enabled=new();}
    class PostProcessProfile:UnityEngine.Object{internal List<PostProcessEffectSettings> settings=new();}
    class PostProcessLayer:Component{internal bool enabled;internal LayerMask volumeLayer;internal Transform? volumeTrigger;}
    class PostProcessVolume:Component
    {
        internal bool isActiveAndEnabled=true,isGlobal=true;internal float weight=1,priority,blendDistance;
        internal PostProcessProfile profileRef=new();internal Collider[] Colliders=Array.Empty<Collider>();
        internal Component[] GetComponents(Type type)=>Colliders;
    }
    class PostProcessManager
    {
        internal static PostProcessManager instance=new();internal PostProcessVolume[] Volumes=Array.Empty<PostProcessVolume>();internal int Reads;
        internal PostProcessVolume[] GrabVolumes(LayerMask layer){Reads++;return Volumes;}
    }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace XiiiXR{static class Bootstrap{internal static void Warn(string message){}}}
