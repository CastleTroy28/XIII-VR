using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using XiiiXR;
using UnityEngine;
using PlayMagic.Weapons;
using FMOD;
class PropImpactAudioTests
{
    static void Check(bool value,string why){if(!value)throw new Exception(why);}
    static SurfaceDetailsSFX Detail(string key)=>new(){paramToAmount=new(){new(){parameter=key,amount=11}}};
    static void Main()
    {
        var wav=SelectedImpactSound.ChairWav();
        Check(Convert.ToHexString(SHA256.HashData(wav)).ToLowerInvariant()=="0d0516505dc1afa694c9c72dfe1083fbed1a4cece17a9b63453a427b54e83215","chair recording changed from supplied WAV");
        Check(wav.Length==110252&&BitConverter.ToUInt16(wav,22)==2&&BitConverter.ToUInt32(wav,24)==48000,"WAV metadata changed");
        Check(PropImpactAudio.IsChair("wpn_ms_chair")&&PropImpactAudio.IsChair("WPN_MS_CHAIR_02")&&!PropImpactAudio.IsChair("wpn_ms_bottle_03")&&!PropImpactAudio.IsChair(null),"chair selection leaks to other props");
        var point=new Vector3(2,3,4);var hit=new RaycastHit{point=point};var origin=new Vector3(9,9,9);
        var item=new Equipable{identifier="wpn_ms_chair"};var source=new MeleeComponent();item.Melee=source;
        var audio=new PropImpactAudio();audio.Prepare();Check(Fake.Created==0,"initializes FMOD before game");
        Time.realtimeSinceStartup=6;FMODUnity.RuntimeManager.IsInitialized=true;audio.Prepare();
        audio.Surface(source,item,true,true,hit,origin);
        Check(Fake.Created==1&&Fake.Started==1&&source.Sounds.Count==0&&source.Fx==1,"chair NPC impact missing/duplicated or removed VFX");
        Check(Fake.Point==point&&Fake.Groups[^1]==(IntPtr)17,"chair not at contact / bypasses game bus");
        audio.Surface(source,item,true,false,hit,origin);
        Check(Fake.Started==1&&source.Sounds.Count==1&&source.Sounds[^1].path=="native/body","chair wall sound replaced");
        audio.Surface(source,item,false,true,hit,origin);
        Check(Fake.Started==1&&source.Sounds.Count==2,"empty-hand NPC hit mistaken for held chair");
        item.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Pistol;
        audio.Surface(source,item,true,true,hit,origin);Check(Fake.Started==1&&source.Sounds.Count==3,"pistol strike replaced with prop sound");
        item.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
        item.identifier="wpn_ms_shovel";source.overrideFmodEvent="native/mop";source.surfaceHitVisualAudioInfo!.Flesh=Detail("Flesh");source.surfaceHitVisualAudioInfo.Generic=Detail("Generic");source.Sounds.Clear();
        audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&source.Sounds[0].path=="native/mop"&&ReferenceEquals(source.Sounds[0].parameters,source.surfaceHitVisualAudioInfo.Generic!.paramToAmount),"NPC ignores mop override / selects silent Flesh variant");
        Check(source.Sounds[0].position==point,"native impact uses swing origin instead of contact point");
        item.identifier="wpn_ms_bottle_03";source.overrideFmodEvent="native/bottle";source.surfaceHitVisualAudioInfo.Flesh=null;source.surfaceHitVisualAudioInfo.Generic=Detail("Generic");source.Sounds.Clear();
        audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&source.Sounds[0].path=="native/bottle"&&ReferenceEquals(source.Sounds[0].parameters,source.surfaceHitVisualAudioInfo.Generic.paramToAmount),"missing Flesh mutes bottle");
        source.surfaceHitVisualAudioInfo.Generic=null;source.Sounds.Clear();audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&source.Sounds[0].parameters.Count==0,"missing surface table prevents event");
        source.overrideFmodEvent="";source.surfaceHitVisualAudioInfo.fmodEvent="native/pan";source.Sounds.Clear();audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&source.Sounds[0].path=="native/pan","table event ignored without override");
        source.surfaceHitVisualAudioInfo=null;source.Sounds.Clear();audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&source.Sounds[0].path=="event:/SFX/WPN/Sfx_FistImpact","missing sound has no body-thud fallback");
        source.overrideFmodEvent="native/bottle";
        var other=new MeleeComponent();source.DuringFx=()=>Check(PropImpactAudio.AllowNativeSound(other),"unrelated NPC/weapon globally muted");
        source.ThrowFx=true;source.Sounds.Clear();audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&PropImpactAudio.AllowNativeSound(source),"VFX exception mutes impact or leaks suppression");
        source.ThrowFx=false;source.DuringFx=null;
        // Damage may use a fists component; use the actual item for audio.
        other.Sounds.Clear();source.Sounds.Clear();audio.Surface(other,item,true,true,hit,origin);
        Check(other.Sounds.Count==0&&source.Sounds.Count==1&&source.Sounds[0].path=="native/bottle","damage fallback replaces item audio with fists");
        item.identifier="wpn_ms_chair";source.Sounds.Clear();Fake.NoBus=true;audio.Surface(source,item,true,true,hit,origin);
        Check(Fake.Groups[^1]==(IntPtr)19&&Fake.Started==2,"core master fallback failed");
        Fake.FailPosition=true;audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==1&&Fake.Released==1&&Fake.Started==2,"partial custom playback failed without native fallback/cleanup");
        Fake.FailPosition=false;audio.Surface(source,item,true,true,hit,origin);
        Check(source.Sounds.Count==2&&Fake.Created==1,"failed sound retries every collision / silence during backoff");
        Time.realtimeSinceStartup=12;audio.Surface(source,item,true,true,hit,origin);
        Check(Fake.Created==2&&Fake.Started==3,"recording never recovers after FMOD error");
        for(int i=0;i<8;i++)audio.Surface(source,item,true,true,hit,origin);
        Check(Fake.Created==2&&Fake.MaxLive<=4,"decodes per hit or leaks unbounded channels");
        audio.Dispose();audio.Dispose();Check(Fake.Released==2&&Fake.Live==0,"cached sample/channel leak");
        audio.Prepare();Check(Fake.Created==2,"disposed adapter resurrected");
        Check(Fake.Pitch==1,"chair recording played off pitch");
        // 0.1.161: a wooden thing (broom, mop) knocks - on an enemy instead of its silent event, on a wall besides the game's own.
        {
            var wood=new PropImpactAudio();int created=Fake.Created,started=Fake.Started;Time.realtimeSinceStartup=20;
            var broom=new Equipable{identifier="wpn_ms_broom",Melee=source};source.overrideFmodEvent="native/broom";source.surfaceHitVisualAudioInfo=new();source.Sounds.Clear();
            wood.Prepare();Check(Fake.Created==created+1,"wood knock made before a wooden thing strikes (only the chair recording is prepared)");created=Fake.Created;
            wood.Surface(source,broom,true,true,hit,origin);
            Check(Fake.Created==created+1&&Fake.Started==started+1&&source.Sounds.Count==0&&Fake.Point==point&&Fake.Pitch>=.9f&&Fake.Pitch<=1.12f,"broom on an enemy: no knock, or the silent event as well");
            Time.realtimeSinceStartup+=.1f;wood.Surface(source,broom,true,false,hit,origin);
            Check(Fake.Started==started+2&&source.Sounds.Count==1&&source.Sounds[0].path=="native/body","broom on a wall: no knock or the game's own surface sound removed");
            wood.Surface(source,broom,true,false,hit,origin);Check(Fake.Started==started+2,"two knocks within 60 ms");
            Time.realtimeSinceStartup+=.1f;broom.identifier="wpn_ms_bottle_03";source.Sounds.Clear();wood.Surface(source,broom,true,true,hit,origin);
            Check(Fake.Started==started+2&&source.Sounds.Count==1,"a bottle knocks like wood");
            Time.realtimeSinceStartup+=.1f;broom.identifier="wpn_ms_mop";Check(wood.Knock(point,.5f,"a touch")&&Fake.Started==started+3&&Fake.Created==created+1,"soft touch knock or knock sound made twice");
            wood.Dispose();
        }
        Console.WriteLine("PASS 0.1.161: wooden things knock (made on first use, a little off pitch each time): on an enemy instead of their silent event, on a wall besides the game's own sound, not twice within 60 ms; bottles unchanged.");
        Console.WriteLine("PASS production prop sound routing: supplied WAV hash/format; chair-only NPC replacement; world/fists/guns unchanged; item override + Flesh/Generic/default; exactly one cue; correct contact point; VFX failure and other-owner isolation; bounded FMOD voices, bus routing, failure fallback/retry/disposal.");
        Console.WriteLine("Native and FMOD calls simulated; headset output not tested.");
    }
}
namespace XiiiXR{static class PropStudioSound{internal static MeleeComponent? Source;internal static void Play(string path,Vector3 point,Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> parameters){Source!.SpawnSurfaceHitSFX(path,point,parameters);}}static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}static class Cast{internal static T? TryCast<T>(this object o)where T:class=>o as T;}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays{class Il2CppStructArray<T>{internal T[] Values;internal Il2CppStructArray(T[] values){Values=values;}}}
namespace Il2CppSystem.Collections.Generic{class List<T>:System.Collections.Generic.List<T>{}}
class PlayerEquipableInventory{internal enum ActiveEquipmentSlot{Enviromental,Fist,Pistol}}
class SurfaceDetection{internal enum SurfaceTypes{Generic,Flesh}}
class SurfaceDetailsSFX
{
    internal class ParameterToAmount{internal string parameter="";internal float amount;}
    internal Il2CppSystem.Collections.Generic.List<ParameterToAmount> paramToAmount=new();
}
class SurfaceHitVfxSfxParameters
{
    internal string fmodEvent="native/body";internal SurfaceDetailsSFX? Flesh=new(),Generic;
    internal SurfaceDetailsSFX? ObtainSFXSurfaceDetails(SurfaceDetection.SurfaceTypes key)=>key==SurfaceDetection.SurfaceTypes.Flesh?Flesh:Generic;
}
namespace PlayMagic.Weapons
{
    class Equipable{internal string identifier="";internal PlayerEquipableInventory.ActiveEquipmentSlot slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;internal MeleeComponent? Melee;internal object? GetComponent(Type t){XiiiXR.PropStudioSound.Source=Melee;return Melee;}}
    class MeleeComponent
    {
        static long next;internal readonly IntPtr Pointer=(IntPtr)(++next);internal SurfaceHitVfxSfxParameters? surfaceHitVisualAudioInfo=new();internal string overrideFmodEvent="";internal int Fx;internal bool ThrowFx;internal Action? DuringFx;
        internal readonly List<(string path,Vector3 position,Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> parameters)> Sounds=new();
        internal void SurfaceHitFX(RaycastHit hit,Vector3 origin)
        {Fx++;DuringFx?.Invoke();if(ThrowFx)throw new Exception("VFX failure");if(surfaceHitVisualAudioInfo?.Flesh!=null)SpawnSurfaceHitSFX(surfaceHitVisualAudioInfo.fmodEvent,origin,surfaceHitVisualAudioInfo.Flesh.paramToAmount);}
        internal void SpawnSurfaceHitSFX(string path,Vector3 point,Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> parameters)
        {if(PropImpactAudio.AllowNativeSound(this))Sounds.Add((path,point,parameters));}
    }
}
namespace UnityEngine
{
    readonly record struct Vector3(float x,float y,float z);
    struct RaycastHit{internal Vector3 point;}
    static class Time{internal static float realtimeSinceStartup;}
}
namespace FMOD
{
    static class Fake
    {
        internal static float Pitch=1;internal static int Created,Started,Released,MaxLive;internal static bool FailPosition,NoBus;internal static Vector3 Point;
        internal static readonly HashSet<IntPtr> Active=new();internal static int Live=>Active.Count;
        internal static readonly List<IntPtr> Groups=new();internal static int ChannelSequence;
    }
    enum RESULT{OK,ERROR}
    [Flags]enum MODE{OPENMEMORY=1,CREATESAMPLE=2,LOOP_OFF=4,_3D=8}
    [StructLayout(LayoutKind.Sequential)]struct CREATESOUNDEXINFO{internal int cbsize;internal uint length;}
    struct VECTOR{internal float x,y,z;}
    struct Sound{internal IntPtr handle;internal RESULT release(){Fake.Released++;return RESULT.OK;}}
    struct ChannelGroup{internal IntPtr handle;}
    struct Channel
    {
        internal IntPtr handle;
        internal RESULT stop(){Fake.Active.Remove(handle);return RESULT.OK;}
        internal RESULT set3DAttributes(ref VECTOR p,ref VECTOR v){Fake.Point=new(p.x,p.y,p.z);return Fake.FailPosition?RESULT.ERROR:RESULT.OK;}
        internal RESULT set3DMinMaxDistance(float min,float max)=>min>0&&max>min?RESULT.OK:RESULT.ERROR;
        internal RESULT set3DSpread(float spread)=>spread==0?RESULT.OK:RESULT.ERROR;
        internal RESULT setVolume(float volume)=>volume>0&&volume<=1?RESULT.OK:RESULT.ERROR;
        internal RESULT setPitch(float pitch){Fake.Pitch=pitch;return pitch>=.5f&&pitch<=2?RESULT.OK:RESULT.ERROR;}
        internal RESULT setPaused(bool paused){if(!paused)Fake.Started++;return RESULT.OK;}
    }
    struct System
    {
        internal RESULT createSound(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> data,MODE mode,ref CREATESOUNDEXINFO info,out Sound sound)
        {
            if(info.cbsize!=Marshal.SizeOf<CREATESOUNDEXINFO>()||info.length!=data.Values.Length||mode!=(MODE.OPENMEMORY|MODE.CREATESAMPLE|MODE.LOOP_OFF|MODE._3D))throw new Exception("invalid WAV load");
            sound=new Sound{handle=(IntPtr)(++Fake.Created)};return RESULT.OK;
        }
        internal RESULT playSound(Sound sound,ChannelGroup group,bool paused,out Channel channel)
        {
            if(!paused)throw new Exception("sound starts before position/gain");channel=new Channel{handle=(IntPtr)(++Fake.ChannelSequence)};
            Fake.Active.Add(channel.handle);Fake.MaxLive=Math.Max(Fake.MaxLive,Fake.Live);Fake.Groups.Add(group.handle);return RESULT.OK;
        }
        internal RESULT getMasterChannelGroup(out ChannelGroup group){group=new(){handle=(IntPtr)19};return RESULT.OK;}
    }
}
namespace FMOD.Studio{struct Bus{internal RESULT getChannelGroup(out ChannelGroup group){group=new(){handle=(IntPtr)17};return Fake.NoBus?RESULT.ERROR:RESULT.OK;}}}
namespace FMODUnity
{
    static class RuntimeManager{internal static bool IsInitialized;internal static FMOD.System CoreSystem=>new();internal static FMOD.Studio.Bus GetBus(string path)=>new();}
    static class RuntimeUtils{internal static VECTOR ToFMODVector(Vector3 p)=>new(){x=p.x,y=p.y,z=p.z};}
}
