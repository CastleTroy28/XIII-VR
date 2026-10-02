using System;using System.Collections.Generic;using System.Runtime.InteropServices;using XiiiXR;using FMOD;
class ReloadAudioTests
{
 static void Check(bool b,string why){if(!b)throw new Exception(why);}
 static void Main()
 {
  foreach(var pair in new[]{("pistol",0),("pistol",1),("pistol",3),("ak47",0),("ak47",1),("ak47",3),
   ("shotgun",2),("shotgun",3),("shotgun",4),("shotgun",5),("shotgun",6),("shotgun",7),("shotgun",8),("shotgun",9),("shotgun",10),
   ("revolver",0),("revolver",1),("revolver",2),("revolver",4),("revolver",7),("revolver",8),("sniper",0),("sniper",3),("sniper",9),("m16",0)})
  {var pcm=SelectedReloadSounds.Get(pair.Item1,pair.Item2);Check(pcm!=null&&pcm.Length>1000,"selected recording missing");foreach(float v in pcm!)Check(float.IsFinite(v)&&Math.Abs(v)<=1,"invalid selected PCM");}
  Check(SelectedReloadSounds.Get("sniper",1)==null,"SVD insert must use the AK recording");
  Check(SelectedReloadSounds.Get("shotgun",0)==null&&SelectedReloadSounds.Get("shotgun",1)==null,"old shotgun magazine cues retained");
  var audio=new ReloadAudio();
  audio.Tick(default,default);Check(Fake.Created==0,"audio initializes FMOD before the game");
  UnityEngine.Time.realtimeSinceStartup=6;FMODUnity.RuntimeManager.IsInitialized=true;
  // 0.1.162: one recording a frame (all at once froze the start of a mission).
  audio.Tick(default,default);Check(Fake.Created==1,"all recordings made in one frame");
  for(int i=0;i<80&&Fake.Created<25;i++)audio.Tick(default,default);
  Check(Fake.Created==25,"mechanical cues not cached");
  audio.Play(ReloadAction.TakeSupply,"pistol",default,default,default);Check(Fake.Played==0,"pouch movement plays gun latch");
  foreach(var profile in new[]{"pistol","ak47","shotgun"})foreach(var action in new[]{ReloadAction.DropInstalled,ReloadAction.TakeInstalled,ReloadAction.Insert,ReloadAction.RackBack,ReloadAction.Chamber})
   audio.Play(action,profile,default,default,default);
  Check(Fake.Played==11&&Fake.Unpaused==11&&Fake.Created==25,"reload callback reloads samples / fails to start audio");
  Check(Fake.Groups.TrueForAll(x=>x==(IntPtr)17),"mechanical sound bypasses game master bus");
  // SVD: own magazine-out/bolt/scope clips (created 22..24 after 21 older clips); insert uses the AK (handle 5).
  Check(audio.PlayCue("sniper",0)&&Fake.Sounds[^1]==(IntPtr)22,"SVD magazine-out clip not used");
  Check(audio.PlayCue("sniper",1)&&Fake.Sounds[^1]==(IntPtr)5,"SVD magazine-in is not the AK recording");
  Check(audio.PlayCue("sniper",9)&&Fake.Sounds[^1]==(IntPtr)24,"SVD scope zoom clip not used");
  Check(audio.PlayCue("sniper",3)&&Fake.Sounds[^1]==(IntPtr)23,"SVD bolt recording not used");
  Check(!audio.PlayCue("sniper",2)&&!audio.PlayCue("bazooka",1),"missing SVD cue or unknown profile plays something");
  // 0.1.194: the Uzi uses the pistol's recordings.
  Check(ReloadAudio.Alias("uzi")=="pistol"&&audio.PlayCue("uzi",1),"the Uzi has no reload sound");
  // 0.1.117: the M16 uses the AK recordings, the crossbow the shotgun shell ones.
  // 0.1.121: except the M16's magazine-out: the mod's M4 recording (clip 25).
  Check(audio.PlayCue("m16",1)&&Fake.Sounds[^1]==(IntPtr)5,"M16 magazine-in is not the AK recording");
  audio.Play(ReloadAction.DropInstalled,"m16",default,default,default);Check(Fake.Sounds[^1]==(IntPtr)25,"M4 magazine-out is not the mod's recording");
  audio.Play(ReloadAction.TakeInstalled,"m16",default,default,default);Check(Fake.Sounds[^1]==(IntPtr)25,"M4 magazine taken out without the mod's recording");
  audio.Play(ReloadAction.RackBack,"m16",default,default,default);Check(Fake.Sounds[^1]==(IntPtr)6,"M4 bolt is not the AK recording");
  audio.Play(ReloadAction.DropInstalled,"m60",default,default,default);Check(Fake.Sounds[^1]==(IntPtr)4,"M60 box got the M4 recording");
  Check(ReloadAudio.Alias("crossbow")=="shotgun"&&ReloadAudio.Alias("m16")=="m16"&&ReloadAudio.Alias("m60")=="ak47"&&ReloadAudio.Alias("pistol")=="pistol"&&ReloadAudio.Fallback("m16")=="ak47"&&ReloadAudio.Fallback("pistol")==null,"reload sound aliases");
  Check(ReloadAudio.HasOwn("m16",0)&&!ReloadAudio.HasOwn("m16",1),"M4 block holds only the magazine-out recording");
  {var m4=SelectedReloadSounds.Get("m16",0)!;float peak=0;foreach(float v in m4)peak=Math.Max(peak,Math.Abs(v));Check(m4.Length==9216&&Math.Abs(peak-26000/32768f)<.001f&&Math.Abs(m4[0])<1e-6f&&Math.Abs(m4[^1])<1e-6f,"M4 recording not peak-matched or not faded");}
  Check(audio.PlayCue("revolver",1)&&Fake.Sounds[^1]==(IntPtr)17,"revolver cylinder load clip not used");
  Fake.NoBus=true;audio.Play(ReloadAction.Insert,"shotgun",default,default,default);Check(Fake.Groups[^1]==(IntPtr)19,"unloaded Studio group has no safe fallback");
  Fake.FailPlay=true;audio.Play(ReloadAction.Insert,"pistol",default,default,default);
  Check(Fake.Released==25,"failed playback leaks cached sounds");
  Fake.FailPlay=false;UnityEngine.Time.realtimeSinceStartup+=6;
  // A cue wanted before its turn is made on the spot (clip 26 = the pistol magazine-out).
  Check(audio.PlayCue("pistol",0)&&Fake.Sounds[^1]==(IntPtr)26,"early cue not made on the spot");
  for(int i=0;i<80&&Fake.Created<50;i++)audio.Tick(default,default);
  Check(Fake.Created==50,"audio error permanently mutes reload");audio.Dispose();Check(Fake.Released==50,"sound samples leaked on disable");
  Console.WriteLine("PASS: production FMOD reload audio adapter: delayed initialization, twenty-five selected recordings (SVD magazine-out/bolt/scope, SVD insert = AK, revolver load, the mod's M4 magazine-out; other M4 cues = AK); no synthetic fallback, master routing, action cues, bounded channel pool, error cleanup/retry/dispose. Simulated FMOD; actual device output not tested.");
 }
}
namespace XiiiXR
{
 internal enum ReloadAction{None,DropInstalled,TakeInstalled,TakeSupply,DropHeld,Insert,RackBack,Chamber,OpenCover,CloseCover}
 internal static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
namespace UnityEngine{struct Vector3{}struct Quaternion{}static class Time{internal static float realtimeSinceStartup;}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays{class Il2CppStructArray<T>{internal T[] Values;internal Il2CppStructArray(T[] a){Values=a;}}}
namespace FMOD
{
 static class Fake{internal static int Created,Played,Unpaused,Released;internal static bool FailPlay,NoBus;internal static List<IntPtr> Groups=new(),Sounds=new();}
 enum RESULT{OK,ERROR}
 [Flags]enum MODE{OPENMEMORY=1,OPENRAW=2,CREATESAMPLE=4,LOOP_OFF=8,_2D=16}
 enum SOUND_FORMAT{PCMFLOAT}
 [StructLayout(LayoutKind.Sequential)]struct CREATESOUNDEXINFO{internal int cbsize;internal uint length;internal int numchannels,defaultfrequency;internal SOUND_FORMAT format;}
 struct Sound{internal IntPtr handle;internal RESULT release(){Fake.Released++;return RESULT.OK;}}
 struct ChannelGroup{internal IntPtr handle;}
 struct Channel
 {
  internal IntPtr handle;internal RESULT stop()=>RESULT.OK;
  internal RESULT setVolume(float value)=>value>0&&value<=1?RESULT.OK:RESULT.ERROR;
  internal RESULT setPitch(float value)=>value>.5f&&value<=1?RESULT.OK:RESULT.ERROR;
  internal RESULT setPaused(bool value){if(!value)Fake.Unpaused++;return RESULT.OK;}
 }
 struct System
 {
  internal RESULT createSound(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes,MODE mode,ref CREATESOUNDEXINFO info,out Sound sound)
  {
   if(info.cbsize!=Marshal.SizeOf<CREATESOUNDEXINFO>()||info.length!=bytes.Values.Length||info.numchannels!=1||(info.defaultfrequency!=24000&&info.defaultfrequency!=48000)||mode!=(MODE.OPENMEMORY|MODE.OPENRAW|MODE.CREATESAMPLE|MODE.LOOP_OFF|MODE._2D))throw new Exception("invalid FMOD PCM layout");
   sound=new Sound{handle=(IntPtr)(++Fake.Created)};return RESULT.OK;
  }
  internal RESULT playSound(Sound s,ChannelGroup g,bool paused,out Channel c){c=default;if(Fake.FailPlay)return RESULT.ERROR;if(!paused)throw new Exception("plays before gain/pitch");c.handle=(IntPtr)(++Fake.Played);Fake.Groups.Add(g.handle);Fake.Sounds.Add(s.handle);return RESULT.OK;}
  internal RESULT getMasterChannelGroup(out ChannelGroup g){g=new ChannelGroup{handle=(IntPtr)19};return RESULT.OK;}
 }
}
namespace FMOD.Studio{struct Bus{internal RESULT getChannelGroup(out ChannelGroup g){g=new ChannelGroup{handle=(IntPtr)17};return Fake.NoBus?RESULT.ERROR:RESULT.OK;}}}
namespace FMODUnity{static class RuntimeManager{internal static bool IsInitialized;internal static FMOD.System CoreSystem=>new();internal static FMOD.Studio.Bus GetBus(string path)=>new();}}
