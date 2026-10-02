using System;using System.Collections.Generic;using XiiiXR;
// 0.1.191: Russian back among the game's voice languages, only selectable.
class AudioLanguageMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var list=new List<string>{"English","French","German","Italian","Spanish"};
  Check(AudioLanguageMath.Missing(list,"Russian",true)=="Russian","Russian not offered though its voice is installed");
  Check(AudioLanguageMath.Missing(list,"Russian",false)==null,"Russian offered without its voice installed");
  Check(AudioLanguageMath.Missing(list,null,true)==null,"a language offered that the game does not know");
  list.Add("Russian");Check(AudioLanguageMath.Missing(list,"Russian",true)==null,"Russian added twice");
  Check(AudioLanguageMath.Missing(new List<string>{"English","Русский"},"Russian",true)==null,"a Russian already listed under its own name added again");
  Check(AudioLanguageMath.IsRussian("Russian","ru")&&AudioLanguageMath.IsRussian("Russe","ru-RU")&&!AudioLanguageMath.IsRussian("German","de"),"Russian not told from the other languages");
  // The game's files: full Russian dialogue banks, an empty Russian cutscene stub.
  var files=new Dictionary<string,bool>{{"dx_npc_ru",true},{"dx_npc_en",true},{"cine_ru",false},{"cine_en",true},{"00_Common",true}};
  bool Usable(string b)=>files.TryGetValue(b,out var ok)&&ok;
  Check(AudioLanguageMath.RussianBank("dx_npc_en","ru","dx_npc_ru",Usable)=="dx_npc_ru","Russian dialogue not loaded");
  Check(AudioLanguageMath.RussianBank("dx_npc","ru",null,Usable)=="dx_npc_ru","Russian dialogue not found from its family");
  Check(AudioLanguageMath.RussianBank("cine_en","ru","cine_ru",Usable)=="cine_en","the empty Russian cutscene bank used (silent cutscenes)");
  Check(AudioLanguageMath.RussianBank("cine_no-vo","ru",null,Usable)=="cine_en","a cutscene family not recognised");
  Check(AudioLanguageMath.RussianBank("00_Common","ru","00_Common",Usable)=="00_Common","a common bank changed");
  Check(AudioLanguageMath.RussianBank("dx_npc_en","en","dx_npc_en",Usable)==null&&AudioLanguageMath.RussianBank("dx_npc_en","de","dx_npc_gr",Usable)==null,"another language's banks touched");
  Check(AudioLanguageMath.RussianBank("dx_unknown","ru","dx_unknown_ru",Usable)==null,"a bank neither Russian nor English invented");
  Check(AudioLanguageMath.Family("dx_brighton_beach01_ru")=="dx_brighton_beach01"&&AudioLanguageMath.Family("wpn_9MM_XIII")=="wpn_9MM_XIII","bank families wrong");
  // 0.1.192: the Russian row of the game's bank table, from the English row.
  files["dx_brighton_beach02_ru"]=true;files["dx_brighton_beach02_en"]=true;files["dx_XIII_ru"]=true;
  var english=new List<string>{"cine_en","dx_brighton_beach02_en","dx_npc_en","dx_XIII_en","dx_lost_en"};
  var ru=AudioLanguageMath.RussianTable(english,Usable);
  Check(string.Join(",",ru)=="cine_en,dx_brighton_beach02_ru,dx_npc_ru,dx_XIII_ru,dx_lost_en","the Russian row not the English one with its installed Russian twins, in order: "+string.Join(",",ru));
  Check(AudioLanguageMath.RussianTable(null,Usable).Count==0&&AudioLanguageMath.RussianTable(new List<string>{"","cine_en","cine_en"},Usable).Count==1,"an empty or repeated English row breaks the Russian row");
  Check(AudioLanguageMath.SameTable(new List<string>(ru),ru)&&!AudioLanguageMath.SameTable(new List<string>{"cine_ru"},ru)&&!AudioLanguageMath.SameTable(null,ru),"a Russian row already right not told from a wrong one");
  Check(AudioLanguageMath.BetterEnglish(null,"en")&&AudioLanguageMath.BetterEnglish("en-US","en")&&!AudioLanguageMath.BetterEnglish("en","en-GB")&&!AudioLanguageMath.BetterEnglish(null,"fr")&&AudioLanguageMath.BetterEnglish(null,"en-US"),"the English row to copy not found");
  // The game asks for the chosen language's banks without naming it when a level loads.
  Check(AudioLanguageMath.Effective(null,"ru")=="ru"&&AudioLanguageMath.Effective("","ru")=="ru"&&AudioLanguageMath.Effective("en","ru")=="en","the chosen voice not used when the game names no language");
  Check(AudioLanguageMath.RussianBank("dx_npc_en",AudioLanguageMath.Effective(null,"ru"),null,Usable)=="dx_npc_ru","a level's Russian banks not loaded when the game names no language");
  // One language of a voice bank at a time: what the switch left behind is finished.
  var voice=new HashSet<string>{"cine_en","cine_fr","dx_npc_en","dx_npc_ru","dx_npc_fr","dx_brighton_beach02_en","dx_brighton_beach02_ru","dx_XIII_en","dx_XIII_ru"};
  string? ToRu(string b){var f=AudioLanguageMath.Family(b);return f=="cine"?"cine_en":f+"_ru";}
  string? ToFr(string b)=>AudioLanguageMath.Family(b)+"_fr";
  AudioLanguageMath.LoadedVoice V(string n,int r=1,bool ok=true)=>new(n,r,ok);
  // English dx_npc used by two loaders: the game's one unload left it, the Russian one refused.
  var sw=AudioLanguageMath.Swaps(new[]{V("Master Bank",3),V("cine_en"),V("dx_npc_en",1),V("dx_brighton_beach02_ru"),V("dx_XIII_en",2),V("dx_npc")},voice.Contains,ToRu);
  Check(sw.Count==2&&sw[0].Family=="dx_npc"&&sw[0].Desired=="dx_npc_ru"&&sw[0].Removed.Count==1&&sw[0].Removed[0]=="dx_npc_en"&&sw[0].Refs==1
   &&sw[1].Desired=="dx_XIII_ru"&&sw[1].Refs==2,"English voice banks left loaded after choosing Russian not swapped (or their users lost)");
  // A Russian bank the game registered though FMOD refused it (another language held): all of the family reloaded, users kept.
  sw=AudioLanguageMath.Swaps(new[]{V("dx_npc_en",1),V("dx_npc_ru",2,false)},voice.Contains,ToRu);
  Check(sw.Count==1&&sw[0].Removed.Count==2&&sw[0].Desired=="dx_npc_ru"&&sw[0].Refs==3,"a refused Russian bank next to the English one not reloaded");
  sw=AudioLanguageMath.Swaps(new[]{V("dx_npc_ru",2,false)},voice.Contains,ToRu);
  Check(sw.Count==1&&sw[0].Removed[0]=="dx_npc_ru"&&sw[0].Refs==2,"a Russian bank that is not really loaded left silent");
  // Already right: nothing touched (cutscenes stay English under Russian).
  Check(AudioLanguageMath.Swaps(new[]{V("cine_en"),V("dx_npc_ru",3),V("dx_XIII_ru")},voice.Contains,ToRu).Count==0,"a right set of voice banks reloaded");
  // Leaving Russian: back to the new language.
  sw=AudioLanguageMath.Swaps(new[]{V("cine_en"),V("dx_npc_ru",2)},voice.Contains,ToFr);
  Check(sw.Count==2&&sw[0].Desired=="cine_fr"&&sw[1].Desired=="dx_npc_fr"&&sw[1].Refs==2,"Russian voice banks left after choosing another language");
  // The game knowing no bank, or a bank of another family: left alone.
  Check(AudioLanguageMath.Swaps(new[]{V("dx_npc_en")},voice.Contains,b=>null).Count==0&&AudioLanguageMath.Swaps(new[]{V("dx_npc_en")},voice.Contains,b=>"dx_XIII_ru").Count==0,"a voice bank swapped for nothing or for another family");
  Check(AudioLanguageMath.Swaps(new[]{V("wpn_9MM_XIII"),V("00_Common"),V("dx_npc")},voice.Contains,ToRu).Count==0,"a bank that is not a voice bank touched");
  Console.WriteLine("PASS: 0.1.192 the game's bank table gets a Russian row made from the English one (Russian twins where installed, English where missing or empty, in the English order); a level's banks follow the chosen voice when the game names none; after a switch each voice family keeps only the chosen language's bank with all its users, a refused bank reloaded, other banks untouched.");
  Console.WriteLine("PASS: 0.1.191 Russian added at the end of the game's voice languages only when its dialogue is installed (never twice, other places kept); Russian dialogue banks loaded, the empty Russian cutscene bank replaced by English, other languages and common banks untouched.");
 }
}
