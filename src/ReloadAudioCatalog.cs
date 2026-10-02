using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
namespace XiiiXR;
// Read native audio references once per weapon; never start an audio event or
// change the player's ammo. The collector uses these exact bank names.
internal static class ReloadAudioCatalog
{
    private static readonly Dictionary<string,object> entries=new();
    internal static void Record(Equipable weapon,string profile)
    {
        if(entries.ContainsKey(profile))return;
        try
        {
            var banks=new List<string>();
            if(weapon.playerBanks?.banks!=null)
                foreach(var bank in weapon.playerBanks.banks)
                    if(bank!=null&&!string.IsNullOrWhiteSpace(bank.bank))banks.Add(bank.bank);
            var audio=weapon.GetComponent(Il2CppType.Of<AudioComponent>())?.TryCast<AudioComponent>();
            var events=new List<object>();
            if(audio?.audioSubComponents!=null)foreach(var sub in audio.audioSubComponents)
            {
                var reload=sub?.TryCast<ReloadSound>();if(reload==null)continue;
                events.Add(new{parameter=BaseAudioSubComponent.eqpFmodParam,value=reload.reloadSoundToUse,
                    secondaryValue=reload.secondaryReloadSoundValue,loop=reload.notSingleShot,type=reload.audioType.ToString()});
            }
            entries[profile]=new{banks,handling=audio?.handlingEqpEvent2D,fire=audio?.fireEqpEvent2D,events};
            string directory=Path.Combine(BepInEx.Paths.GameRootPath,"XIII-VR-Audio");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"Reload-events.json"),JsonSerializer.Serialize(entries,new JsonSerializerOptions{WriteIndented=true}));
            Bootstrap.Write("RELOAD AUDIO SOURCES "+profile+" banks="+string.Join(",",banks)+" handling="+audio?.handlingEqpEvent2D);
        }
        catch(Exception ex){Bootstrap.Warn("Reload audio catalog unavailable: "+ex.Message);}
    }
}
