using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.191: Russian back in the game's voice languages (AudioLanguages).
// 0.1.192: the Russian row of the game's bank table, and which loaded voice
// banks must be swapped for the chosen language.
internal static class AudioLanguageMath
{
    internal static bool IsRussian(string? name,string? code)
        =>(code??"").StartsWith("ru",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"Russian",StringComparison.OrdinalIgnoreCase)
          ||(name??"").StartsWith("Рус",StringComparison.OrdinalIgnoreCase);
    // Russian to add at the end of the game's list: known, its voice installed, not there yet.
    internal static string? Missing(IList<string> have,string? russian,bool voiceInstalled)
    {
        if(string.IsNullOrEmpty(russian)||!voiceInstalled)return null;
        foreach(var h in have)if(string.Equals(h,russian,StringComparison.OrdinalIgnoreCase)||IsRussian(h,null))return null;
        return russian;
    }
    // The game's localized banks end in a language code (dx_npc_ru, cine_no-vo).
    private static readonly string[] Codes={"en","es","fr","gr","de","it","ru","no-vo"};
    internal static string Family(string bank)
    {
        int i=bank.LastIndexOf('_');
        if(i>0){var tail=bank.Substring(i+1).ToLowerInvariant();if(Array.IndexOf(Codes,tail)>=0)return bank.Substring(0,i);}
        return bank;
    }
    // For the Russian voice: the Russian bank when it is really there; else the
    // game's own choice when that is a real bank; else the English one (the
    // game's Russian cutscene bank is an empty stub). Null: leave the game's.
    internal static string? RussianBank(string? bankToSearch,string? languageCode,string? game,Func<string,bool> usable)
    {
        if(string.IsNullOrEmpty(bankToSearch)||!(languageCode??"").StartsWith("ru",StringComparison.OrdinalIgnoreCase))return null;
        var family=Family(bankToSearch!);
        var ru=family+"_ru";if(usable(ru))return ru;
        if(!string.IsNullOrEmpty(game)&&usable(game!))return game;
        var en=family+"_en";if(usable(en))return en;
        return null;
    }
    // 0.1.192.
    // The game's bank table (FmodBankToLanguage) has no Russian row, so the
    // game had no Russian banks to load or to unload. The Russian row is made
    // from the English one: each English bank's Russian twin when it is really
    // installed, else the English bank itself (the empty cutscene bank), in the
    // English order - the game picks a bank from a row by its name without the
    // language, first match.
    internal static List<string> RussianTable(IEnumerable<string>? english,Func<string,bool> usable)
    {
        var table=new List<string>();
        if(english==null)return table;
        foreach(var bank in english)
        {
            if(string.IsNullOrEmpty(bank))continue;
            var family=Family(bank);var ru=family+"_ru";
            var pick=family!=bank&&usable(ru)?ru:bank;
            if(!table.Exists(x=>string.Equals(x,pick,StringComparison.OrdinalIgnoreCase)))table.Add(pick);
        }
        return table;
    }
    // The English row to copy: "en" itself, else the first English variant.
    internal static bool BetterEnglish(string? have,string? candidate)
    {
        if(!(candidate??"").StartsWith("en",StringComparison.OrdinalIgnoreCase))return false;
        if(have==null)return true;
        return string.Equals(candidate,"en",StringComparison.OrdinalIgnoreCase)&&!string.Equals(have,"en",StringComparison.OrdinalIgnoreCase);
    }
    internal static bool SameTable(IList<string>? have,IList<string> want)
    {
        if(have==null||have.Count!=want.Count)return false;
        for(int i=0;i<want.Count;i++)if(!string.Equals(have[i],want[i],StringComparison.OrdinalIgnoreCase))return false;
        return true;
    }
    // A voice bank loaded under the game's bank loader (its name, its count of
    // users, whether FMOD really holds it).
    internal readonly struct LoadedVoice
    {
        internal readonly string Name;internal readonly int Refs;internal readonly bool Valid;
        internal LoadedVoice(string name,int refs,bool valid){Name=name;Refs=refs;Valid=valid;}
    }
    internal sealed class BankSwap
    {
        internal string Family="",Desired="";
        internal readonly List<string> Removed=new();
        internal int Refs;
    }
    // One language's voice bank of a family at a time: FMOD refuses a second
    // language of a bank that is still loaded (same bank), silently. The game
    // unloads the old language once per bank, but a bank used by several of its
    // loaders stays loaded, so the new language never loads. For each voice
    // family loaded: when the chosen language's bank is not the only one there,
    // or is there but not really loaded, all of that family go and the chosen
    // one is loaded with all their users (never fewer, so no loader loses it).
    internal static List<BankSwap> Swaps(IEnumerable<LoadedVoice> loaded,Func<string,bool> localized,Func<string,string?> desiredFor)
    {
        var groups=new Dictionary<string,List<LoadedVoice>>(StringComparer.OrdinalIgnoreCase);var order=new List<string>();
        foreach(var v in loaded)
        {
            if(string.IsNullOrEmpty(v.Name)||!localized(v.Name))continue;
            var family=Family(v.Name);if(family==v.Name)continue;
            if(!groups.TryGetValue(family,out var g)){g=new List<LoadedVoice>();groups[family]=g;order.Add(family);}
            g.Add(v);
        }
        var swaps=new List<BankSwap>();
        foreach(var family in order)
        {
            var g=groups[family];
            string? desired;try{desired=desiredFor(g[0].Name);}catch(Exception){desired=null;}
            if(string.IsNullOrEmpty(desired)||!string.Equals(Family(desired!),family,StringComparison.OrdinalIgnoreCase))continue;
            if(g.Count==1&&g[0].Name==desired&&g[0].Valid)continue;
            var swap=new BankSwap{Family=family,Desired=desired!};
            foreach(var v in g){swap.Removed.Add(v.Name);swap.Refs+=Math.Max(1,v.Refs);}
            swap.Refs=Math.Max(1,swap.Refs);
            swaps.Add(swap);
        }
        return swaps;
    }
    // The code a bank is asked for: the one given, else the voice language the
    // game has chosen (the game's own rule when the level loads its banks).
    internal static string? Effective(string? given,string? current)=>string.IsNullOrEmpty(given)?current:given;
}
