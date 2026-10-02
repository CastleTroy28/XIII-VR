using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;using PlayMagic.Weapons;
// 0.1.183: the production chest reload (WeaponHands.ChestReload) against a
// simulated game: pistols in both hands, each dropping its magazine with its
// own button and taking a full one when its grip strikes the chest.
class ChestReloadDriverTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static readonly Vector3 Head=new(0,1.7f,0);
 static Vector3 Body(float x,float y,float z)=>Head+new Vector3(x,y,z);
 static readonly Vector3 Ready=Body(.18f,-.30f,.45f),ReadyLeft=Body(-.18f,-.30f,.45f),Chest=Body(.02f,-.30f,.08f),ChestLeft=Body(-.02f,-.30f,.08f);
 static int Total(WeaponHands w)=>w.Game.PrimaryMagazineAmmoCount+w.Holsters.LooseMagazine(1000)+w.Game.ammoPool.Reserve+w.Owned.PrimaryMagazineAmmoCount;
 // The grips of both hands move to where they are asked over n frames (the button edges on the first frame only).
 static void Move(WeaponHands w,Vector3 right,Vector3 left,int n=10,ulong rd=0,ulong ld=0)
 {
  var r0=w.RightButt;var l0=w.LeftButt;
  for(int i=1;i<=n;i++){w.RightButt=Vector3.Lerp(r0,right,i/(float)n);w.LeftButt=Vector3.Lerp(l0,left,i/(float)n);w.Step(i==1?rd:0,i==1?ld:0);}
 }
 static void Main()
 {
  // The game's pistol in the right hand, another pistol (from the ground) in the left.
  var w=new WeaponHands();w.Game.PrimaryMagazineAmmoCount=5;w.Holsters.Loose=9;w.Game.ammoPool.Reserve=40;
  w.HoldLoose(0);w.RightButt=Ready;w.LeftButt=ReadyLeft;w.Step();
  int total=Total(w);
  Check(w.ChestReloads(1)&&w.ChestReloads(0)&&w.ChestGame,"both hands full of pistols do not reload against the chest");
  // Both buttons at once: both magazines out, their rounds back to the reserve.
  int drops=ReloadDrops.Count;
  Move(w,Ready,ReadyLeft,2,HandControls.B,HandControls.B);
  Check(w.Game.PrimaryMagazineAmmoCount==0&&w.Holsters.Loose==0&&w.Game.ammoPool.Reserve==54&&Total(w)==total,"the dropped magazines' rounds are not back in the reserve (or counted twice)");
  Check(!w.GameState.Installed&&!w.LooseState.Installed&&w.LooseCopy.MagazineOut&&ReloadDrops.Count==drops+2,"a magazine not out of each gun (or not drawn falling)");
  Check(!w.GameState.SlideLocked&&!w.LooseCopy.LockedBack,"a magazine with rounds dropped locks the slide back");
  Check(w.Watch=="CHEST"&&w.Glows>0,"the watch/glow does not show the chest while magazines are out");
  // A second press with the magazine out: nothing more drops.
  Move(w,Ready,ReadyLeft,2,HandControls.B,0);Check(ReloadDrops.Count==drops+2&&w.Game.ammoPool.Reserve==54,"a second press drops another magazine");
  // Both grips struck against the chest at once: both full, the slides forward.
  Move(w,Chest,ChestLeft,10);
  Check(w.Game.PrimaryMagazineAmmoCount==15&&w.Holsters.Loose==15&&w.Game.ammoPool.Reserve==24&&Total(w)==total,"both strikes did not load full magazines from the reserve");
  Check(w.GameState.Installed&&!w.GameState.BlocksFire&&w.LooseState.Installed&&!w.LooseCopy.MagazineOut&&!w.LooseCopy.LockedBack,"a struck-in magazine leaves a gun blocked or drawn without it");
  Check(w.FireReleases>=1&&w.NativeResets>=1&&w.Punches==2,"the game's pistol is not released after its strike, or a strike not felt");
  Check(w.Watch==""&&w.Label=="","the watch still asks for the chest");
  // The slide's sound follows the magazine's.
  int cues=w.Audio.Cues.Count;w.Wait(.2f);Check(w.Audio.Cues.Count==cues+2&&w.Audio.Cues.FindAll(c=>c==2).Count==2,"no slide sound after the magazines went in");
  // Pressed on the chest (not out again): no second magazine.
  Move(w,Chest+new Vector3(0,0,-.05f),ChestLeft+new Vector3(0,0,-.05f),5);Check(w.Game.ammoPool.Reserve==24,"one strike loaded twice");
  // Only one hand: the other keeps its magazine.
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ReadyLeft,2,0,HandControls.B);
  Check(w.Holsters.Loose==0&&w.Game.PrimaryMagazineAmmoCount==15,"one hand's button dropped the other's magazine");
  Move(w,Chest,ReadyLeft,10);Check(w.Holsters.Loose==0&&w.Game.ammoPool.Reserve==39,"the right hand's strike loaded the left hand's pistol");
  // Brought slowly to the chest: nothing.
  Move(w,Ready,ChestLeft,200);Check(w.Holsters.Loose==0,"a grip brought slowly to the chest loads");
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ChestLeft,10);Check(w.Holsters.Loose==15&&w.Game.ammoPool.Reserve==24&&Total(w)==total,"the left strike did not load");
  // The last round: the slide stays back; dropped, struck in: forward.
  w.Holsters.Loose=0;w.Step();Check(w.LooseState.SlideLocked&&w.LooseCopy.LockedBack&&w.Watch=="DROP","the empty pistol's slide is not back (or the watch does not ask to drop it)");
  Move(w,Ready,ReadyLeft,2,0,HandControls.B);Check(!w.LooseState.Installed&&w.LooseState.SlideLocked&&w.LooseCopy.LockedBack,"the empty magazine dropped lets the slide forward");
  Move(w,Ready,ChestLeft,10);Check(w.Holsters.Loose==15&&!w.LooseCopy.LockedBack&&!w.LooseState.SlideLocked,"the strike does not let the slide go forward");
  // The magazine dropped with the grip at the chest: out first.
  Move(w,Ready,ChestLeft,12);int reserve=w.Game.ammoPool.Reserve;
  Move(w,Ready,ChestLeft,2,0,HandControls.B);Move(w,Ready,ChestLeft+new Vector3(0,0,-.04f),6);
  Check(w.Holsters.Loose==0&&w.Game.ammoPool.Reserve==reserve+15,"a magazine dropped at the chest goes straight back in");
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ChestLeft,10);Check(w.Holsters.Loose==15,"after leaving the chest the strike does not load");
  // No rounds left: the magazine stays out, the hand feels it once more, written once.
  w.Game.PrimaryMagazineAmmoCount=0;w.Game.ammoPool.Reserve=0;w.Step();total=Total(w);
  Move(w,Ready,ReadyLeft,2,HandControls.B,0);int buzz=w.Buzzes;
  Move(w,Chest,ReadyLeft,10);Move(w,Ready,ReadyLeft,12);Move(w,Chest,ReadyLeft,10);
  Check(w.Game.PrimaryMagazineAmmoCount==0&&w.Game.ammoPool.Reserve==0&&!w.GameState.Installed&&w.Buzzes>=buzz+2&&Total(w)==total,"without rounds the magazine came back (or rounds were made)");
  Check(Bootstrap.Messages.FindAll(m=>m.Contains("no rounds left")).Count==1,"no rounds written more than once");
  Check(w.Watch!="CHEST","the watch asks for the chest with no rounds left");
  w.Game.ammoPool.Reserve=15;w.Step();Check(w.Watch=="CHEST","rounds found again: the watch does not ask for the chest");
  Move(w,Ready,ReadyLeft,12);Move(w,Chest,ReadyLeft,10);Check(w.Game.PrimaryMagazineAmmoCount==15&&w.Game.ammoPool.Reserve==0,"rounds found again: the strike does not load");
  // Infinite rounds: always full, the reserve untouched.
  w.Game.ammoPool.Infinite=true;int kept=w.Game.ammoPool.Reserve;
  Move(w,Ready,ReadyLeft,2,HandControls.B,0);Move(w,Chest,ReadyLeft,10);
  Check(w.Game.PrimaryMagazineAmmoCount==15&&w.Game.ammoPool.Reserve==kept,"infinite rounds change the reserve");
  w.Game.ammoPool.Infinite=false;
  // The game's pistol alone (the left hand free): the belt reload, the game's own reload untouched.
  w.HoldLoose(-1);w.Step();
  Check(!w.ChestReloads(1)&&!w.ChestGame&&w.Label=="","a pistol alone reloads against the chest");
  reserve=w.Game.ammoPool.Reserve;Move(w,Ready,ReadyLeft,2,HandControls.B,0);Check(w.Game.PrimaryMagazineAmmoCount==15&&w.Game.ammoPool.Reserve==reserve,"the right B of a pistol alone drops its magazine the chest way");
  // A hostage in the left hand: the right pistol reloads against the chest.
  GripCarry.Current=new GripCarry{HidesLeft=true};Check(w.ChestReloads(1),"a hostage in the left hand: no chest reload for the right pistol");GripCarry.Current=null;
  // The owned pistol held as a copy in the left hand (the game's weapon another one): its own magazine.
  w.HoldOwned(0);w.Owned.PrimaryMagazineAmmoCount=3;w.Game.ammoPool.Reserve=40;w.Step();total=Total(w);
  Move(w,Ready,ReadyLeft,2,0,HandControls.B);Check(w.Owned.PrimaryMagazineAmmoCount==0&&w.OwnedCopy.MagazineOut&&Total(w)==total,"the owned pistol's magazine (a copy) not dropped from its own gun");
  Move(w,Ready,ChestLeft,10);Check(w.Owned.PrimaryMagazineAmmoCount==15&&!w.OwnedCopy.MagazineOut&&Total(w)==total,"the owned pistol's magazine not struck in");
  // A copy's own reload by itself is not started (the chest reload is its reload).
  Check(!w.StartsHandReload(0),"the hand's reload by itself still runs for a chest-reloaded pistol");
  // The manual reload off: pistols reload by themselves as before.
  WeaponOptions.ManualReload.Value=false;w.Step();Check(!w.ChestReloads(0)&&!w.ChestReloads(1)&&w.Label=="","the chest reload runs with the manual reload off");
  WeaponOptions.ManualReload.Value=true;
  // Another kind in the left hand: it is not reloaded against the chest; the right pistol is.
  w.HoldOther(0,"ak47");w.Step();Check(!w.ChestReloads(0)&&w.ChestReloads(1),"the chest reload follows the wrong hand's weapon");
  // 0.1.194: an Uzi in the left hand is (its magazine goes in the grip like a pistol's).
  w.HoldOther(0,"uzi");w.Step();Check(w.ChestReloads(0)&&w.ChestReloads(1),"an Uzi held in the other hand is not reloaded against the chest");
  w.OtherAmmo.PrimaryMagazineAmmoCount=7;w.Game.ammoPool.Reserve=30;w.Step();total=w.OtherAmmo.PrimaryMagazineAmmoCount+w.Game.ammoPool.Reserve;
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ReadyLeft,2,0,HandControls.B);Check(w.OtherAmmo.PrimaryMagazineAmmoCount==0&&w.Game.ammoPool.Reserve==37,"the Uzi's magazine not dropped by the left Y");
  Move(w,Ready,ChestLeft,10);Check(w.OtherAmmo.PrimaryMagazineAmmoCount==15&&w.OtherAmmo.PrimaryMagazineAmmoCount+w.Game.ammoPool.Reserve==total,"the Uzi's magazine not struck in against the chest");
  Check(Bootstrap.Messages.Exists(m=>m.Contains("left hand uzi")&&m.Contains("bolt forward")),"the Uzi's chest reload not written as the Uzi's");
  // The game's Uzi in the right hand with a weapon in the left: against the chest too.
  w.HoldOwned(0);w.Step();Check(w.ChestReloads(1)&&w.ChestReloads(0),"the game's Uzi with the other hand full is not reloaded against the chest");
  // 0.1.194: the game's two pistols (dual): each its own magazine, its own button, its own strike.
  w.HoldOther(-1,"");w.Dual=true;w.Game.PrimaryMagazineAmmoCount=4;w.LeftAmmo.PrimaryMagazineAmmoCount=6;w.Game.ammoPool.Reserve=40;w.Step();
  Check(w.ChestReloads(0)&&w.ChestReloads(1)&&w.ChestGame,"the game's two pistols do not reload against the chest");
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ReadyLeft,2,0,HandControls.B);
  Check(w.LeftAmmo.PrimaryMagazineAmmoCount==0&&w.Game.PrimaryMagazineAmmoCount==4&&!w.LeftState.Installed&&w.GameState.Installed&&w.Game.ammoPool.Reserve==46,"the left pistol of two: its Y did not drop its own magazine only");
  Move(w,Ready,ChestLeft,10);
  Check(w.LeftAmmo.PrimaryMagazineAmmoCount==15&&w.LeftState.Installed&&w.Game.PrimaryMagazineAmmoCount==4&&w.Game.ammoPool.Reserve==31&&w.DualReleases>=1,"the left pistol of two not loaded by its own strike (or its fire not released)");
  Move(w,Ready,ReadyLeft,12);Move(w,Ready,ReadyLeft,2,HandControls.B,0);Check(w.Game.PrimaryMagazineAmmoCount==0&&w.LeftAmmo.PrimaryMagazineAmmoCount==15,"the right pistol of two: its B dropped the wrong magazine");
  Move(w,Chest,ReadyLeft,10);Check(w.Game.PrimaryMagazineAmmoCount==15&&w.LeftAmmo.PrimaryMagazineAmmoCount==15&&w.Game.ammoPool.Reserve==20,"the right pistol of two not reloaded against the chest");
  w.Dual=false;
  Console.WriteLine("PASS: production chest reload driver: 0.1.194 the Uzi (in either hand) and the game's two pistols (each its own magazine, button and strike); both pistols' magazines out with their own buttons at once (rounds back to the reserve, drawn falling), both struck in at once from the reserve with the slides forward and their sound after; one hand leaves the other alone; slow, pressed-on or already-at-the-chest grips do not load; the last round's slide back until struck in; no reserve: the magazine stays out, written once; infinite rounds; the owned pistol as a copy; a pistol alone, the manual reload off and other kinds keep their reload. Game API simulated; the feel needs the headset.");
 }
}
namespace XiiiXR
{
 internal sealed partial class WeaponHands
 {
  internal readonly AmmoManagementComponent Game=new(),Owned=new(),OtherAmmo=new();internal BodyHolsters Holsters=>holsters!;
  internal Vector3 RightButt,LeftButt;internal int FireReleases,NativeResets,Punches,Buzzes;internal int Glows=>ReloadGlow.Shown;internal ReloadAudio Audio=>reloadAudio??=new ReloadAudio();
  internal HolsterCopy LooseCopy=>holsters!.Copies[1000];internal HolsterCopy OwnedCopy=>holsters!.Copies[2];
  internal ManualReloadState GameState=>reload;internal ManualReloadState LooseState=>looseReloadStates[1000];
  internal string Watch=>WatchReloadLabel;internal string Label=>chestLabel;
  private readonly Equipable pistol=new(){slot=2},other=new(){slot=5};
  internal WeaponHands()
  {
   weapon=pistol;profile="pistol";Game.ammoPool=OtherAmmo.ammoPool=Owned.ammoPool=LeftAmmo.ammoPool=new AmmoPool();ammo=Game;pistol.Fire=new FireComponent{ammoManagementComponent=Game};
   holsters=new BodyHolsters();holsters.Weapons[1000]=pistol;holsters.Copies[1000]=new HolsterCopy{Owner=this,Side=0};
   holsters.Weapons[2]=new Equipable{slot=2,Fire=new FireComponent{ammoManagementComponent=Owned}};holsters.Copies[2]=new HolsterCopy{Owner=this,Side=0};
   holsters.Weapons[5]=other;other.Fire=new FireComponent{ammoManagementComponent=OtherAmmo};holsters.Copies[5]=new HolsterCopy{Owner=this,Side=0};
   Current=this;
  }
  internal static WeaponHands? Current;
  internal void HoldLoose(int s){copyKey[0]=s==0?1000:-1;copyProfile[0]=s==0?"pistol":"";}
  internal void HoldOwned(int s){weapon=other;profile="uzi";ammo=OtherAmmo;reload=new ManualReloadState();copyKey[0]=2;copyProfile[0]="pistol";}
  internal void HoldOther(int s,string kind){weapon=pistol;profile="pistol";ammo=Game;reload=GameKept;copyKey[0]=s==0?5:-1;copyProfile[0]=kind;}
  private readonly ManualReloadState GameKept=new();
  internal bool StartsHandReload(int s)=>!ChestReloads(s);
  internal void Step(ulong rd=0,ulong ld=0)
  {
   Time.realtimeSinceStartup+=1f/90;Time.frameCount++;
   rig.RightControls=new HandControls(true,rd,rd,0);rig.LeftControls=new HandControls(true,ld,ld,0);
   visual!.At=RightButt-visual.ReloadPort;poseValid=true;
   foreach(var c in holsters!.Copies.Values)c.ButtWorld=LeftButt;
   reload.ObserveRounds(ammo!.PrimaryMagazineAmmoCount);
   TickChestReload();
  }
  internal void Wait(float seconds){for(float t=0;t<seconds;t+=1f/90)Step();}
  private WeaponVisual? visual=new();private bool poseValid;private bool enabled=true;private bool foreEndOnly=false;private Inventory? inventory=new();
  private Equipable? weapon;private string profile="";private AmmoManagementComponent? ammo;private ManualReloadState reload=new();
  private readonly Dictionary<int,ManualReloadState> reloadStates=new(),looseReloadStates=new();
  private BodyHolsters? holsters;private readonly int[] copyKey={-1,-1};private readonly string[] copyProfile={"",""};private readonly bool[] copyForeEnd=new bool[2];
  // 0.1.194: the game's two pistols (simulated): the left one's own magazine and state.
  internal bool Dual;internal readonly AmmoManagementComponent LeftAmmo=new();internal readonly ManualReloadState LeftState=new();internal int DualReleases;
  private bool DualActive=>Dual;private bool DualChest(int s)=>Dual;private ManualReloadState? DualLeftState()=>LeftState;
  private AmmoManagementComponent? DualLeftAmmo(out int key){key=100002;return LeftAmmo;}
  private bool DualLeftButt(out Vector3 butt){butt=LeftButt;return true;}private bool DualSwitching=>false;
  private void DropDualLeft(ref Vector3 at){ReloadDrops.Count++;}private void ReleaseDualFire(int s,string why){DualReleases++;}
  private bool ManualEnabled=>WeaponOptions.ManualReload.Value&&!DualActive&&EquipmentProfile.ChestMagazine(profile)&&ammo!=null;
  private int GameKey=>weapon==null?-1:weapon.slot;private int GameSide(int key)=>key<0?-1:1;private bool SupportsCopy(bool right)=>false;
  internal string ReloadLabel=>"";
  private static ManualReloadState ReloadStateFor(string p)=>new();
  internal static FireComponent? FireOf(Equipable e)=>e.Fire;
  private void SetMagazine(int n){ammo!.SetAmmo(n,0);}
  private Vector3 HandleSided(Vector3 v)=>v;private Vector3 NativeGrip(bool right,Vector3 v)=>v;
  private readonly CameraRig rig=new();private ReloadAudio? reloadAudio;private ReloadDrops? drops;
  private bool HandReloading(int s)=>false;private static string Side(int s)=>s==0?"left":"right";
  private void Drop(Vector3 p,Quaternion q,Vector3 v){ReloadDrops.Count++;}
  private void DisarmFireTrigger(){}private void ReleaseNativeFire(string why){FireReleases++;}private void ResetNativeReload(string why){NativeResets++;}
  private static System.Numerics.Vector3 ToN(Vector3 v)=>v.N;
 }
 internal class CameraRig
 {
  internal HandControls LeftControls,RightControls;internal Vector3 HeadPosition=>new(0,1.7f,0);internal Quaternion HeadRotation=>Quaternion.identity;
  internal void ResistanceHaptics(float a,bool right=false){WeaponHands.Current!.Buzzes++;}internal void ReloadHaptics(ReloadAction a,bool right=false){}internal void PunchHaptics(bool right){WeaponHands.Current!.Punches++;}
 }
 internal class Inventory{internal bool isInTransit=>false;}
 internal class GripCarry{internal static GripCarry? Current;internal bool HidesLeft;}
 internal class Config{internal bool Value=true;}internal static class WeaponOptions{internal static Config ManualReload=new();}
 internal class WeaponVisual{internal Vector3 At;internal bool ReloadAvailable=>true;internal Vector3 ReloadPort=>new(0,-.08f,-.01f);internal Vector3 MagazineCenter=>new(0,-.04f,0);internal Matrix4x4 FittedToWorld=>new(At);}
 internal class ReloadMesh{}
 internal class HolsterCopy
 {
  internal WeaponHands? Owner;internal int Side;internal Vector3 ButtWorld;internal bool Shown=>true;internal bool MagazineOut;internal bool HasCycle=>true;internal bool LockedBack;
  internal void LockBack(bool b)=>LockedBack=b;internal Vector3 MagazineWorld=>ButtWorld;internal ReloadMesh? MagazineTemplate=new();internal Quaternion Rotation=>Quaternion.identity;
  internal static readonly Vector3 ButtBelowGrip=new(0,-.075f,-.02f);
 }
 internal class BodyHolsters
 {
  internal readonly Dictionary<int,Equipable> Weapons=new();internal readonly Dictionary<int,HolsterCopy> Copies=new();internal int Loose;
  internal static bool IsLoose(int key)=>key>=1000;internal Equipable? WeaponOf(int key)=>Weapons.TryGetValue(key,out var w)?w:null;
  internal int LooseMagazine(int key)=>key==1000?Loose:0;internal void SetLooseMagazine(int key,int n){if(key==1000)Loose=Math.Max(0,n);}
  internal HolsterCopy? CopyOf(int key)=>Copies.TryGetValue(key,out var c)?c:null;
 }
 internal class ReloadAudio{internal readonly List<int> Cues=new();internal void Play(ReloadAction a,string p,Vector3 at,Vector3 h,Quaternion q){Cues.Add(a==ReloadAction.DropInstalled?0:9);}internal bool PlayCue(string p,int k){Cues.Add(k);return true;}}
 internal class ReloadDrops{internal static int Count;internal void Add(ReloadMesh m,Vector3 p,Quaternion q,Vector3 v){Count++;}}
 internal class ReloadGlow{internal static int Shown;internal void Show(Vector3 p,float r){Shown++;}internal void Hide(){}internal void Dispose(){}}
 internal static class Bootstrap{internal static readonly List<string> Messages=new();internal static void Write(string s)=>Messages.Add(s);internal static void Warn(string s)=>Messages.Add("WARN "+s);}
}
namespace PlayMagic.Weapons
{
 internal class Equipable{static int next;internal IntPtr Pointer=(IntPtr)(++next);internal int slot;internal FireComponent? Fire;internal int GetInstanceID()=>(int)Pointer;}
 internal class FireComponent{internal AmmoManagementComponent? ammoManagementComponent;}
 internal class AmmoManagementComponent
 {
  static int next;internal IntPtr Pointer=(IntPtr)(++next);
  internal int PrimaryMagazineAmmoCount=15;internal int PrimaryReserveAmmoCount=>ammoPool.Infinite?0:ammoPool.Reserve;internal int MaxPrimaryMagazineAmmoCount=>15;internal int SecondaryMagazineAmmoCount=>0;
  internal AmmoPool ammoPool=new();internal int primaryAmmoType=>1;internal bool triggerPrimaryReload;
  internal void CancelReload(){}internal void SetAmmo(int p,int s){PrimaryMagazineAmmoCount=p;}internal void TriggerEventAmmoPoolChanged(){}
 }
 internal class AmmoPool
 {internal bool Infinite;internal bool IsInfinite=>Infinite;internal int Reserve=20;internal int TryRemoveAmmo(int t,int n){int used=Math.Min(Reserve,n);Reserve-=used;return used;}internal void AddAmmo(int t,int n){Reserve+=n;}}
}
namespace UnityEngine
{
 internal readonly struct Vector3
 {
  internal readonly System.Numerics.Vector3 N;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(System.Numerics.Vector3 n){N=n;}
  internal static Vector3 zero=>default;internal static Vector3 forward=>new(0,0,1);internal static Vector3 down=>new(0,-1,0);
  internal static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>new(System.Numerics.Vector3.Lerp(a.N,b.N,t));
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.N-b.N);public static Vector3 operator*(Vector3 a,float b)=>new(a.N*b);
 }
 internal struct Quaternion{internal static Quaternion identity=>new();public static Vector3 operator*(Quaternion q,Vector3 p)=>p;}
 internal readonly struct Matrix4x4{private readonly Vector3 at;internal Matrix4x4(Vector3 p){at=p;}internal Vector3 MultiplyPoint3x4(Vector3 p)=>p+at;internal Quaternion rotation=>Quaternion.identity;}
 internal static class Time{internal static float realtimeSinceStartup;internal static int frameCount;}
}
