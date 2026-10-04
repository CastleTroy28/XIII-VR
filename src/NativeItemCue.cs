using System;
using System.Collections.Generic;
using FMODUnity;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
internal static class NativeItemCue
{
    private static readonly HashSet<string> loaded=new(StringComparer.Ordinal);
    internal static void Release()
    {
        foreach(var name in loaded)try{if(RuntimeManager.HasBankLoaded(name))RuntimeManager.UnloadBank(name);}catch(Exception e){Bootstrap.Warn("KEY bank cleanup: "+e.Message);}loaded.Clear();
    }
    // 0.1.220: the cue kept playing (to be stopped or started again): the
    // lockpick's own picking sound for as long as the lock is picked.
    internal static bool Start(Equipable item,AudioComponent.AudioTrigger trigger,Vector3 position,out FMOD.Studio.EventInstance instance)
    {
        instance=default;
        try
        {
            LoadBanks(item);
            var audio=item.GetOptionalEquipableComponent<AudioComponent>();if(audio==null||audio.audioSubComponents==null)return false;
            foreach(var sub in audio.audioSubComponents)
            {
                if(sub==null||sub.audioTrigger!=trigger)continue;
                var path=sub.audioType==BaseAudioSubComponent.AudioType.Handling?audio.handlingEqpEvent2D:audio.fireEqpEvent2D;
                if(string.IsNullOrEmpty(path))path=sub.audioType==BaseAudioSubComponent.AudioType.Handling?audio.handlingEqpEvent3D:audio.firegEqpEvent3D;
                if(string.IsNullOrEmpty(path))continue;
                instance=RuntimeManager.CreateInstance(path);
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(position));
                if(!string.IsNullOrEmpty(BaseAudioSubComponent.eqpFmodParam))instance.setParameterByName(BaseAudioSubComponent.eqpFmodParam,sub.eqpFmodParamValue,false);
                if(instance.start()!=FMOD.RESULT.OK){instance.release();instance=default;return false;}
                Bootstrap.Write("KEY SOUND "+trigger+" event="+path+" (kept playing)");return true;
            }
        }
        catch(Exception e){Bootstrap.Warn("KEY SOUND "+trigger+": "+e.Message);}
        return false;
    }
    private static void LoadBanks(Equipable item)
    {
        if(item.playerBanks?.banks!=null)foreach(var bank in item.playerBanks.banks)
            if(bank!=null&&!bank.isLocalizedBank&&!string.IsNullOrEmpty(bank.bank)&&!RuntimeManager.HasBankLoaded(bank.bank))
            {RuntimeManager.LoadBank(bank.bank,bank.preLoadSamples);loaded.Add(bank.bank);}
    }
    internal static void Play(Equipable item,AudioComponent.AudioTrigger trigger,Vector3 position)
    {
        try
        {
            // Preview does not enable/equip the native item, so its effect bank may
            // not have been loaded by the usual OnEnable path.
            if(item.playerBanks?.banks!=null)foreach(var bank in item.playerBanks.banks)
                if(bank!=null&&!bank.isLocalizedBank&&!string.IsNullOrEmpty(bank.bank)&&!RuntimeManager.HasBankLoaded(bank.bank))
                {RuntimeManager.LoadBank(bank.bank,bank.preLoadSamples);loaded.Add(bank.bank);}
            var audio=item.GetOptionalEquipableComponent<AudioComponent>();if(audio==null||audio.audioSubComponents==null)return;
            foreach(var sub in audio.audioSubComponents)
            {
                if(sub==null||sub.audioTrigger!=trigger)continue;
                var path=sub.audioType==BaseAudioSubComponent.AudioType.Handling?audio.handlingEqpEvent2D:audio.fireEqpEvent2D;
                if(string.IsNullOrEmpty(path))path=sub.audioType==BaseAudioSubComponent.AudioType.Handling?audio.handlingEqpEvent3D:audio.firegEqpEvent3D;
                if(string.IsNullOrEmpty(path))continue;
                var parameters=new Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount>();
                if(!string.IsNullOrEmpty(BaseAudioSubComponent.eqpFmodParam))parameters.Add(new SurfaceDetailsSFX.ParameterToAmount{parameter=BaseAudioSubComponent.eqpFmodParam,amount=sub.eqpFmodParamValue});
                PropStudioSound.Play(path,position,parameters);Bootstrap.Write("KEY SOUND "+trigger+" event="+path);return;
            }
            Bootstrap.Warn("KEY SOUND no authored cue for "+trigger+" item="+item.name);
        }
        catch(Exception e){Bootstrap.Warn("KEY SOUND "+trigger+": "+e.Message);}
    }
}
