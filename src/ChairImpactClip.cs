using System;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
// The supplied WAV is cached in the game's FMOD system. No Unity AudioSource,
// file read or decode is needed for each contact, and dropping the prop cannot
// cut off the sound. OPENMEMORY copies the supplied bytes before returning.
internal sealed class ChairImpactClip : IDisposable
{
    private readonly Func<byte[]> source;private readonly string label;private readonly float volume,maxDistance;
    internal ChairImpactClip():this(SelectedImpactSound.ChairWav,"SFX_Hit_Chair.wav",.9f,18){}
    // 0.1.84: the same cached FMOD playback for other supplied recordings.
    internal ChairImpactClip(Func<byte[]> wav,string name,float gain,float distance){source=wav;label=name;volume=gain;maxDistance=distance;}
    private Sound sound;
    private readonly Channel[] channels=new Channel[4];
    private int nextChannel;
    private float retryAt,nextWarning;
    private bool disposed;
    internal void Prepare()
    {
        if(disposed||sound.handle!=IntPtr.Zero||Time.realtimeSinceStartup<retryAt)return;
        retryAt=Time.realtimeSinceStartup+5;
        try
        {
            if(!RuntimeManager.IsInitialized)return;
            byte[] wav=source();
            var info=new CREATESOUNDEXINFO{cbsize=Marshal.SizeOf<CREATESOUNDEXINFO>(),length=(uint)wav.Length};
            Require(RuntimeManager.CoreSystem.createSound(new Il2CppStructArray<byte>(wav),
                MODE.OPENMEMORY|MODE.CREATESAMPLE|MODE.LOOP_OFF|MODE._3D,ref info,out sound),"create "+label);
            Bootstrap.Write(label=="SFX_Hit_Chair.wav"?"PROP IMPACT chair recording cached: SFX_Hit_Chair.wav (the mod's own)":"SOUND cached: "+label+" (the mod's own)");
        }
        catch(Exception ex){Fail(ex);}
    }
    internal bool Play(Vector3 point)=>Play(point,1,1,false);
    // 0.1.137: louder/quieter and higher/lower per play (landing speed, kind of weapon).
    internal bool Play(Vector3 point,float gain,float pitch,bool quiet)
    {
        Prepare();if(disposed||sound.handle==IntPtr.Zero)return false;
        try
        {
            var system=RuntimeManager.CoreSystem;
            ChannelGroup group=default;
            if(RuntimeManager.GetBus("bus:/").getChannelGroup(out group)!=RESULT.OK)
                Require(system.getMasterChannelGroup(out group),"master group");
            int index=nextChannel++%channels.Length;
            if(channels[index].handle!=IntPtr.Zero)channels[index].stop();
            Require(system.playSound(sound,group,true,out channels[index]),"play chair");
            var position=RuntimeUtils.ToFMODVector(point);VECTOR velocity=default;
            Require(channels[index].set3DAttributes(ref position,ref velocity),"impact position");
            Require(channels[index].set3DMinMaxDistance(1,maxDistance),"impact distance");
            Require(channels[index].set3DSpread(0),"point source");
            Require(channels[index].setVolume(volume*Math.Clamp(gain,0,1.5f)),"volume");
            Require(channels[index].setPitch(Math.Clamp(pitch,.5f,2f)),"pitch");
            Require(channels[index].setPaused(false),"start chair");
            if(!quiet)Bootstrap.Write(label=="SFX_Hit_Chair.wav"?"PROP NPC SOUND item=chair recording=SFX_Hit_Chair.wav result=OK":"SOUND played "+label);return true;
        }
        catch(Exception ex){Fail(ex);return false;}
    }
    // 0.1.90: stop a recording that follows a movement (door) when it ends.
    internal void Stop(){try{foreach(var channel in channels)if(channel.handle!=IntPtr.Zero)channel.stop();}catch(Exception ex){Fail(ex);}}
    private static void Require(RESULT result,string operation)
    {if(result!=RESULT.OK)throw new InvalidOperationException(operation+": "+result);}
    private void Fail(Exception ex)
    {
        Release();retryAt=Time.realtimeSinceStartup+5;
        if(Time.realtimeSinceStartup<nextWarning)return;nextWarning=retryAt;
        Bootstrap.Warn("PROP IMPACT recording unavailable; native impact fallback: "+ex.Message);
    }
    private void Release()
    {
        foreach(var channel in channels)if(channel.handle!=IntPtr.Zero)channel.stop();
        Array.Clear(channels,0,channels.Length);
        if(sound.handle!=IntPtr.Zero)sound.release();sound=default;
    }
    public void Dispose(){if(disposed)return;disposed=true;Release();}
}
