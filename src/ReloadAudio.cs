using System;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
// Use the game's active FMOD output device. Unity AudioSource can remain silent
// when this FMOD title disables Unity audio; a second AudioListener cannot fix it.
internal sealed class ReloadAudio:IDisposable
{
    // 11 cue kinds per profile; 0.1.106 adds the SVD ("sniper") block,
    // 0.1.121 the M4/M16 block.
    private static readonly string[] Profiles={"pistol","ak47","shotgun","revolver","sniper","m16"};
    private readonly Sound[] clips=new Sound[Profiles.Length*11];
    private readonly Channel[] channels=new Channel[8];
    private int channelIndex;private bool ready;private float nextAttempt;private bool reported;private int nextClip;
    // 0.1.162: one recording a call (a frame): all of them at once (decoded
    // and handed to FMOD, several MB) stopped the game at the start of a
    // mission. A cue wanted before its turn is made on the spot.
    internal void Tick(Vector3 head,Quaternion rotation)
    {
        if(ready||Time.realtimeSinceStartup<nextAttempt)return;
        try
        {
            if(!RuntimeManager.IsInitialized){nextAttempt=Time.realtimeSinceStartup+5;return;}
            for(;nextClip<clips.Length;nextClip++)if(Make(nextClip)){nextClip++;break;}
            if(nextClip>=clips.Length){ready=true;Bootstrap.Write("RELOAD AUDIO FMOD ready: the mod's pistol/AK/shotgun/revolver/SVD/M4 recordings cached on game output (one a frame)");}
        }
        catch(Exception ex){if(!reported){reported=true;Bootstrap.Warn("RELOAD AUDIO FMOD retry: "+ex.Message);}Release();nextAttempt=Time.realtimeSinceStartup+5;}
    }
    // Makes clip i if it has a recording and is not made yet; true when it made it.
    private bool Make(int i)
    {
        if(clips[i].handle!=IntPtr.Zero)return false;
        var pcm=SelectedReloadSounds.Get(Profiles[i/11],i%11);if(pcm==null)return false;
        var bytes=new byte[pcm.Length*sizeof(float)];Buffer.BlockCopy(pcm,0,bytes,0,bytes.Length);
        var info=new CREATESOUNDEXINFO{cbsize=Marshal.SizeOf<CREATESOUNDEXINFO>(),length=(uint)bytes.Length,numchannels=1,defaultfrequency=48000,format=SOUND_FORMAT.PCMFLOAT};
        // OPENMEMORY copies the PCM before returning (not OPENMEMORY_POINT).
        var result=RuntimeManager.CoreSystem.createSound(new Il2CppStructArray<byte>(bytes),MODE.OPENMEMORY|MODE.OPENRAW|MODE.CREATESAMPLE|MODE.LOOP_OFF|MODE._2D,ref info,out clips[i]);
        Require(result,"create PCM "+i);
        return true;
    }
    // 0.1.117: the M16 uses the AK recordings, the crossbow the shell ones
    // (take a bolt, push it in).
    // 0.1.121: the M16 (M4) has its own magazine-out recording; its other
    // cues are still the AK ones (Fallback).
    // 0.1.194: the Uzi (9 mm, magazine in the grip) uses the pistol's recordings.
    internal static string Alias(string profile)=>profile switch{"m60"=>"ak47","crossbow"=>"shotgun","uzi"=>"pistol",_=>profile};
    internal static string? Fallback(string profile)=>profile is "sniper" or "m16"?"ak47":null;
    internal static bool HasOwn(string profile,int kind)=>SelectedReloadSounds.Get(profile,kind)!=null;
    internal void Play(ReloadAction action,string profile,Vector3 position,Vector3 head,Quaternion rotation)
    {
        profile=Alias(profile);
        int kind=action switch {ReloadAction.OpenCover=>3,ReloadAction.CloseCover=>2,ReloadAction.DropInstalled or ReloadAction.TakeInstalled=>0,ReloadAction.Insert=>profile=="shotgun"?4:1,ReloadAction.TakeSupply=>5,ReloadAction.RackBack=>3,ReloadAction.Chamber=>2,_=>-1};
        PlayCue(profile,kind);
    }
    internal bool PlayCue(string profile,int kind)
    {
        profile=Alias(profile);
        int block=Array.IndexOf(Profiles,profile);
        if(kind<0||kind>=11||block<0)return false;
        Tick(default,default);
        if(!ready&&(Time.realtimeSinceStartup<nextAttempt||!RuntimeManager.IsInitialized))return false;
        int clipIndex=block*11+kind;
        // 0.1.106: the SVD has its own magazine and scope recordings; any SVD
        // cue without one (the bolt) still uses the AK recording. The same for
        // the M4 (0.1.121).
        var fallback=Fallback(profile);
        try
        {
            if(!ready){Make(clipIndex);if(fallback!=null)Make(Array.IndexOf(Profiles,fallback)*11+kind);}
            if(clips[clipIndex].handle==IntPtr.Zero&&fallback!=null){profile=fallback;clipIndex=Array.IndexOf(Profiles,fallback)*11+kind;}
            if(clips[clipIndex].handle==IntPtr.Zero)return false;
            var system=RuntimeManager.CoreSystem;
            // Route through the Studio master bus so pause/mute/master gain also
            // affect mechanical cues. Fall back to the core master if unloaded.
            ChannelGroup group=default;
            var bus=RuntimeManager.GetBus("bus:/");
            if(bus.getChannelGroup(out group)!=RESULT.OK)Require(system.getMasterChannelGroup(out group),"master group");
            int index=channelIndex++%channels.Length;
            if(channels[index].handle!=IntPtr.Zero)channels[index].stop();
            Require(system.playSound(clips[clipIndex],group,true,out channels[index]),"play");
            Require(channels[index].setVolume(.9f),"volume");
            Require(channels[index].setPitch(1),"pitch");
            Require(channels[index].setPaused(false),"unpause");
            Bootstrap.Write("RELOAD AUDIO FMOD cue="+kind+" profile="+profile+" result=OK");return true;
        }
        catch(Exception ex){Bootstrap.Warn("RELOAD AUDIO FMOD play: "+ex.Message);Release();nextAttempt=Time.realtimeSinceStartup+5;}
        return false;
    }
    private static void Require(RESULT result,string operation){if(result!=RESULT.OK)throw new InvalidOperationException(operation+": "+result);}
    private void Release()
    {
        foreach(var channel in channels)if(channel.handle!=IntPtr.Zero)channel.stop();
        foreach(var clip in clips)if(clip.handle!=IntPtr.Zero)clip.release();
        Array.Clear(clips,0,clips.Length);Array.Clear(channels,0,channels.Length);ready=false;nextClip=0;
    }
    public void Dispose()=>Release();
}
