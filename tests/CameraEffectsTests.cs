using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using XiiiXR;
class CameraEffectsTests
{
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    static void Main()
    {
        var legacy=new CTAA_PC();var copy=new RenderPostCTAA();var blink=new PostProcessMaterial();
        var grade=new CustomPostProcessing();var fog=new DeferredFogEffect();
        var oldVolume=new UnityEngine.PostProcessing.PostProcessingBehaviour();
        var volume=new PostProcessLayer{antialiasingMode=PostProcessLayer.Antialiasing.TemporalAntialiasing};
        var inactive=new PostProcessMaterial{enabled=false};
        var later=new PostProcessMaterial{enabled=false};
        var timeline=new NativeTimeline();
        var owned=new CustomPostProcessing();StoryColorEffect.Owned=owned;
        var camera=new Camera{Components=new MonoBehaviour[]{legacy,copy,blink,grade,fog,oldVolume,volume,inactive,later,timeline,owned}};
        var otherEffect=new PostProcessMaterial();var untracked=new Camera{Components=new MonoBehaviour[]{otherEffect}};
        var effects=new CameraEffects();
        effects.Inspect(camera,true);
        Check(!legacy.enabled&&!copy.enabled&&!blink.enabled&&!grade.enabled&&!fog.enabled&&!oldVolume.enabled&&!volume.enabled,"desktop image effect remains in stereo render route");
        Check(volume.antialiasingMode==PostProcessLayer.Antialiasing.TemporalAntialiasing,"native AA setting unnecessarily changed");
        Check(timeline.enabled,"authored camera/Timeline component disabled");
        Check(owned.enabled,"private native stereo color carrier disabled with desktop effects");
        Check(!inactive.enabled&&!later.enabled,"initially inactive effect forced on");
        int scanned=camera.ComponentScans;
        blink.enabled=true;volume.enabled=true;later.enabled=true; // story re-enable between discoveries
        effects.BeforeRender(camera);
        Check(!blink.enabled&&!volume.enabled&&!later.enabled,"Timeline re-enable survives until next discovery");
        int writes=blink.Writes+volume.Writes+later.Writes;
        for(int i=0;i<1000;i++)effects.BeforeRender(camera);
        Check(camera.ComponentScans==scanned,"per-eye guard scans/allocates component arrays");
        Check(writes==blink.Writes+volume.Writes+later.Writes,"guard repeatedly writes unchanged enabled states");
        effects.BeforeRender(untracked);Check(otherEffect.enabled&&untracked.ComponentScans==0,"untracked desktop camera was changed");
        effects.Toggle();Check(effects.Bypassed&&!blink.enabled&&!volume.enabled,"F7 re-enabled known broken stereo path");
        effects.Inspect(camera,false); // re-discovery must not lose restoration ownership
        effects.Dispose();
        Check(!effects.Bypassed&&legacy.enabled&&copy.enabled&&blink.enabled&&grade.enabled&&fog.enabled&&oldVolume.enabled&&volume.enabled,"F10/shutdown did not restore owned effects");
        Check(later.enabled&&!inactive.enabled,"shutdown lost the game's latest enable intent or activated untouched effect");
        writes=blink.Writes+inactive.Writes;
        effects.Dispose();effects.BeforeRender(camera);effects.Inspect(camera,false);effects.Toggle();
        Check(blink.enabled&&!inactive.enabled&&writes==blink.Writes+inactive.Writes,"disposed guard still changes native camera");
        Console.WriteLine("PASS: .33 stereo bypass restored, native Timeline unaffected, re-enabled effects blocked before each render without rescanning, F7 cannot restore mono path, F10 restores owned native states. Simulated Unity API; actual stereo output needs headset verification.");
    }
}
class CTAA_PC:MonoBehaviour{}
class RenderPostCTAA:MonoBehaviour{}
class PostProcessMaterial:MonoBehaviour{}
class CustomPostProcessing:MonoBehaviour{}
class DeferredFogEffect:MonoBehaviour{}
class NativeTimeline:MonoBehaviour{}
namespace UnityEngine
{
    class Behaviour
    {
        static int next;readonly int id=++next;bool active=true;
        internal int Writes;internal bool enabled{get=>active;set{active=value;Writes++;}}
        internal int GetInstanceID()=>id;
    }
    class MonoBehaviour:Behaviour{internal T? TryCast<T>() where T:class=>this as T;internal Type GetIl2CppType()=>GetType();}
    class Camera:Behaviour
    {
        internal string name="native";internal int ComponentScans;
        internal MonoBehaviour[] Components=Array.Empty<MonoBehaviour>();
        internal MonoBehaviour[] GetComponents(Type t){ComponentScans++;return Components;}
    }
}
namespace UnityEngine.PostProcessing{class PostProcessingBehaviour:MonoBehaviour{}}
namespace UnityEngine.Rendering.PostProcessing
{
    class PostProcessLayer:MonoBehaviour
    {
        internal enum Antialiasing{None,TemporalAntialiasing}
        internal Antialiasing antialiasingMode;
    }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace XiiiXR{static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}static class StoryColorEffect{internal static UnityEngine.Behaviour? Owned;internal static bool Owns(UnityEngine.Behaviour b)=>b==Owned;}}
