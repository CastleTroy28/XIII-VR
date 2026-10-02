using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using FMODUnity;
namespace XiiiXR;
// 0.1.191: the game ships the Russian dialogue
// banks (dx_*_ru.bank, full size) but leaves Russian out of its list of voice
// languages. Russian is added at the end of that list (the other languages
// keep their places, so a saved choice keeps meaning the same language), and
// only when the Russian dialogue is really installed. Its cutscene bank
// (cine_ru.bank) is an empty stub in the game: a Russian bank that is missing
// or empty is replaced by the English one, so cutscenes are not silent.
// Nothing is chosen for the player; the game's own menu and save do that.
// 0.1.192:
// - the game's bank table has no Russian row: the game could neither load the
//   Russian banks when a level loads (it asks for the chosen language's banks
//   without naming it) nor unload them when switching away. A Russian row is
//   added to the table, made from the English one (AudioLanguageMath);
// - the game swaps a language's banks by unloading each old bank once and
//   loading the new one; a bank several of its loaders use stays loaded, and
//   FMOD then silently refuses the new language of that bank (the same bank).
//   After each switch that involves Russian, and after banks load while
//   Russian is chosen, every voice family is left with only the chosen
//   language's bank, holding all the users the old one had.
internal static class AudioLanguages
{
    private static Harmony? patches;
    private static string? russian,russianCode;private static bool reportedList;private static int reportedBanks,reportedSwitches;
    private static long nextLook;
    private static readonly Dictionary<string,bool> usable=new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> voiceBanks=new(StringComparer.Ordinal);
    private static IntPtr tableDone;private static string before="";
    private static string? folder;
    internal const int StubBytes=4096;
    internal static void Install()
    {
        if(patches!=null)return;patches=new Harmony("xiii.vr.xrbootstrap.audiolanguages");
        try{folder=Application.streamingAssetsPath;}catch(Exception){}
        try{patches.Patch(AccessTools.Method(typeof(GameplayConfigOptions),nameof(GameplayConfigOptions.GetAllAudioLanguages)),postfix:new HarmonyMethod(typeof(AudioLanguages),nameof(AddRussian)));}
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES list hook: "+ex.Message);}
        try{patches.Patch(AccessTools.Method(typeof(AudioLocalizationManager),nameof(AudioLocalizationManager.GetLocalizedBank)),prefix:new HarmonyMethod(typeof(AudioLanguages),nameof(BeforeBank)),postfix:new HarmonyMethod(typeof(AudioLanguages),nameof(RussianBank)));}
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES bank hook: "+ex.Message);}
        try{patches.Patch(AccessTools.Method(typeof(AudioLocalizationManager),nameof(AudioLocalizationManager.UnloadLocalizedBanks)),prefix:new HarmonyMethod(typeof(AudioLanguages),nameof(BeforeSwitch)),postfix:new HarmonyMethod(typeof(AudioLanguages),nameof(AfterSwitch)));}
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES switch hook: "+ex.Message);}
        try{patches.Patch(AccessTools.Method(typeof(BanksListSO),nameof(BanksListSO.LoadBanks)),postfix:new HarmonyMethod(typeof(AudioLanguages),nameof(AfterLoad)));}
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES load hook: "+ex.Message);}
    }
    private static void AddRussian(Il2CppSystem.Collections.Generic.List<string> __result)
    {
        try
        {
            if(__result==null)return;
            var have=new List<string>();for(int i=0;i<__result.Count;i++)have.Add(__result[i]);
            var name=RussianName();
            bool voice=Usable("dx_npc_ru");
            var add=AudioLanguageMath.Missing(have,name,voice);
            if(add!=null)__result.Add(add);
            if(!reportedList)
            {
                reportedList=true;
                Bootstrap.Write("VOICE LANGUAGES the game's list: "+string.Join(", ",have)+(add!=null?"; + "+add+" ("+russianCode+"; the game's Russian dialogue banks are installed; its choice was switched off)":name==null?"; no Russian language known to the game":!voice?"; Russian voice not installed (dx_npc_ru.bank)":"; Russian already in it"));
            }
        }
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES list: "+ex.Message);}
    }
    // Before the game picks a bank for a language: its table has the Russian row.
    private static void BeforeBank(AudioLocalizationManager __instance){try{Table(__instance);}catch(Exception){}}
    private static void RussianBank(string bankToSearch,string languageCode,ref string __result)
    {
        try
        {
            string? game=__result;
            var code=AudioLanguageMath.Effective(languageCode,CurrentCode());
            var chosen=AudioLanguageMath.RussianBank(bankToSearch,code,game,Usable);
            if(chosen==null)return;
            bool changed=chosen!=game;
            if(changed)__result=chosen;
            if(reportedBanks++<16)Bootstrap.Write("VOICE LANGUAGES Russian: "+bankToSearch+" -> "+chosen+(changed?" (the game gave "+(string.IsNullOrEmpty(game)?"nothing":game)+")":" (the game's table)")+(string.IsNullOrEmpty(languageCode)?" for the chosen voice "+code:""));
        }
        catch(Exception ex){if(reportedBanks++<16)Bootstrap.Warn("VOICE LANGUAGES bank: "+ex.Message);}
    }
    private static void BeforeSwitch(AudioLocalizationManager __instance,string oldLanguageCode,string newLanguageCode)
    {
        try
        {
            Table(__instance);
            before=AudioLanguageMath.IsRussian(null,oldLanguageCode)||AudioLanguageMath.IsRussian(null,newLanguageCode)?Loaded():"";
        }
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES switch: "+ex.Message);}
    }
    // After the game swapped the voice banks for the new language.
    private static void AfterSwitch(AudioLocalizationManager __instance,string oldLanguage,string oldLanguageCode,string newLanguage,string newLanguageCode)
    {
        try
        {
            if(!AudioLanguageMath.IsRussian(null,oldLanguageCode)&&!AudioLanguageMath.IsRussian(null,newLanguageCode)&&!RussianLoaded())return;
            var swapped=Reconcile(__instance,newLanguageCode);
            if(reportedSwitches++<24)Bootstrap.Write("VOICE LANGUAGES switched "+oldLanguage+" ("+oldLanguageCode+") -> "+newLanguage+" ("+newLanguageCode+"): voice banks before ["+before+"], after ["+Loaded()+"]"+(swapped.Length>0?"; finished by the mod: "+swapped:"; the game's own swap was complete"));
        }
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES switch: "+ex.Message);}
        before="";
    }
    // After a loader (a level, a weapon, the menu) loaded its banks.
    private static void AfterLoad()
    {
        try
        {
            var code=CurrentCode();
            if(!AudioLanguageMath.IsRussian(null,code)&&!RussianLoaded())return;
            var alm=AudioLocalizationManager.Instance;if(alm==null)return;
            Table(alm);
            var swapped=Reconcile(alm,code);
            if(swapped.Length>0&&reportedSwitches++<24)Bootstrap.Write("VOICE LANGUAGES banks loaded for "+code+": finished by the mod: "+swapped+"; voice banks now ["+Loaded()+"]");
        }
        catch(Exception ex){if(reportedSwitches++<24)Bootstrap.Warn("VOICE LANGUAGES load: "+ex.Message);}
    }
    // The Russian row in the game's bank table (made once per table).
    private static void Table(AudioLocalizationManager alm)
    {
        if(alm==null)return;
        var so=alm.FmodLocalizedBanks;if(so==null)return;
        var rows=so.languageDictionary;if(rows==null||rows.Pointer==tableDone)return;
        if(RussianName()==null||!Usable("dx_npc_ru"))return;
        string code=russianCode??"ru";
        FmodBankToLanguage.LanguageFmod? english=null,ru=null;var codes=new List<string>();
        voiceBanks.Clear();
        for(int i=0;i<rows.Count;i++)
        {
            var row=rows[i];if(row==null)continue;
            var c=row.languageCode??"";int n=row.fmodBanks?.Count??0;
            codes.Add(c+"("+n+(string.IsNullOrEmpty(row.langaugeCodeToFillBank)?"":", fill "+row.langaugeCodeToFillBank)+")");
            for(int b=0;b<n;b++){var bank=row.fmodBanks![b];if(!string.IsNullOrEmpty(bank))voiceBanks.Add(bank);}
            if(string.Equals(c,code,StringComparison.OrdinalIgnoreCase))ru=row;
            else if(n>0&&AudioLanguageMath.BetterEnglish(english?.languageCode,c))english=row;
        }
        if(english==null){tableDone=rows.Pointer;Bootstrap.Warn("VOICE LANGUAGES the game's bank table has no English row to make the Russian one from: "+string.Join(", ",codes));return;}
        var have=new List<string>();for(int b=0;b<(english.fmodBanks?.Count??0);b++)have.Add(english.fmodBanks![b]);
        var want=AudioLanguageMath.RussianTable(have,Usable);
        foreach(var bank in want)voiceBanks.Add(bank);
        var had=new List<string>();for(int b=0;ru?.fmodBanks!=null&&b<ru.fmodBanks.Count;b++)had.Add(ru.fmodBanks[b]);
        string what;
        if(ru!=null&&AudioLanguageMath.SameTable(had,want))what="already there";
        else
        {
            bool added=ru==null;
            if(ru==null){ru=new FmodBankToLanguage.LanguageFmod(code);ru.langaugeCodeToFillBank=english.languageCode;}
            if(ru.fmodBanks==null)ru.fmodBanks=new Il2CppSystem.Collections.Generic.List<string>();
            ru.fmodBanks.Clear();foreach(var bank in want)ru.fmodBanks.Add(bank);
            if(added)rows.Add(ru);
            what=added?"added":"fixed (it had "+had.Count+" banks)";
        }
        tableDone=rows.Pointer;
        int kept=0;foreach(var bank in want)if(!bank.EndsWith("_ru",StringComparison.OrdinalIgnoreCase))kept++;
        Bootstrap.Write("VOICE LANGUAGES the game's bank table: "+string.Join(", ",codes)+"; Russian row ("+code+") "+what+": "+want.Count+" banks, "+(want.Count-kept)+" Russian, "+kept+" English where the Russian one is missing or empty ("+string.Join(", ",want.FindAll(b=>!b.EndsWith("_ru",StringComparison.OrdinalIgnoreCase)))+")");
    }
    // Every voice family left with only the chosen language's bank.
    private static string Reconcile(AudioLocalizationManager alm,string? code)
    {
        if(string.IsNullOrEmpty(code))return "";
        var banks=RuntimeManager.Instance?.loadedBanks;if(banks==null)return "";
        var loaded=new List<AudioLanguageMath.LoadedVoice>();
        foreach(var name in Names(banks))
        {
            if(!voiceBanks.Contains(name))continue;
            int refs=1;bool valid=true;
            try{var lb=banks[name];refs=lb.RefCount;valid=lb.Bank.isValid();}catch(Exception){}
            loaded.Add(new AudioLanguageMath.LoadedVoice(name,refs,valid));
        }
        var swaps=AudioLanguageMath.Swaps(loaded,voiceBanks.Contains,b=>alm.GetLocalizedBank(b,code));
        var done=new List<string>();
        foreach(var s in swaps)
        {
            foreach(var name in s.Removed){int guard=0;while(guard++<64&&RuntimeManager.HasBankLoaded(name))RuntimeManager.UnloadBank(name);}
            string result;
            try
            {
                RuntimeManager.LoadBank(s.Desired,false);
                if(banks.ContainsKey(s.Desired))
                {
                    var lb=banks[s.Desired];
                    if(lb.RefCount<s.Refs){lb.RefCount=s.Refs;banks[s.Desired]=lb;}
                    bool ok=true;try{ok=lb.Bank.isValid();}catch(Exception){}
                    result=ok?"":" (FMOD did not load it)";
                }
                else result=" (not loaded)";
            }
            catch(Exception ex){result=" (failed: "+ex.Message+")";}
            done.Add(string.Join("+",s.Removed)+" -> "+s.Desired+" x"+s.Refs+result);
        }
        return string.Join(", ",done);
    }
    private static List<string> Names(Il2CppSystem.Collections.Generic.Dictionary<string,RuntimeManager.LoadedBank> banks)
    {
        var names=new List<string>();
        foreach(var name in banks.Keys)if(!string.IsNullOrEmpty(name))names.Add(name);
        return names;
    }
    private static bool RussianLoaded()
    {
        try
        {
            var banks=RuntimeManager.Instance?.loadedBanks;if(banks==null)return false;
            foreach(var name in Names(banks))if(name.EndsWith("_ru",StringComparison.OrdinalIgnoreCase))return true;
        }
        catch(Exception){}
        return false;
    }
    private static string Loaded()
    {
        try
        {
            var banks=RuntimeManager.Instance?.loadedBanks;if(banks==null)return "";
            var list=new List<string>();
            foreach(var name in Names(banks))
            {
                if(!voiceBanks.Contains(name))continue;
                string refs="";try{var lb=banks[name];refs=" x"+lb.RefCount+(lb.Bank.isValid()?"":" (not really loaded)");}catch(Exception){}
                list.Add(name+refs);
            }
            return string.Join(", ",list);
        }
        catch(Exception ex){return "? "+ex.Message;}
    }
    private static string? CurrentCode()
    {
        try{return I2.Loc.LocalizationManager.CurrentAudioLanguageCode;}catch(Exception){return null;}
    }
    // The game's own name for Russian (its localization's language list).
    private static string? RussianName()
    {
        if(russian!=null)return russian;
        long now=Environment.TickCount64;if(now<nextLook)return null;nextLook=now+1000;
        try
        {
            var all=I2.Loc.LocalizationManager.GetAllLanguages(false);
            for(int i=0;all!=null&&i<all.Count;i++)
            {
                var n=all[i];string code="";
                try{code=I2.Loc.LocalizationManager.GetLanguageCode(n)??"";}catch(Exception){}
                if(AudioLanguageMath.IsRussian(n,code)){russian=n;russianCode=string.IsNullOrEmpty(code)?"ru":code;break;}
            }
            if(russian==null&&all!=null&&all.Count>0)nextLook=long.MaxValue;
        }
        catch(Exception ex){Bootstrap.Warn("VOICE LANGUAGES languages: "+ex.Message);}
        return russian;
    }
    // A bank file that is there and not an empty stub.
    private static bool Usable(string bank)
    {
        if(string.IsNullOrEmpty(bank))return false;
        if(usable.TryGetValue(bank,out bool ok))return ok;
        try
        {
            folder??=Application.streamingAssetsPath;
            var name=bank.EndsWith(".bank",StringComparison.OrdinalIgnoreCase)?bank:bank+".bank";
            var file=new FileInfo(Path.Combine(folder,name));
            ok=file.Exists&&file.Length>StubBytes;
        }
        catch(Exception){ok=false;}
        usable[bank]=ok;return ok;
    }
}
