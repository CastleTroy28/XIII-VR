using System;using XiiiXR;using UnityEngine;using PlayMagic.Weapons;
class ReloadDriverTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var w=new WeaponHands();var a=w.Ammo;
  w.Step(HandControls.B,HandControls.B);w.Step(0,0,HandControls.B);
  Check(a.PrimaryMagazineAmmoCount==0&&a.PrimaryReserveAmmoCount==27&&ReloadDrops.Count==1,"tap B did not eject only installed ammo");
  Check(!w.Stock(a)&&w.Stock(new AmmoManagementComponent()),"stock reload guard affects wrong weapon");
  // 0.1.122: the grenade launcher (secondary) reloads natively even in manual mode.
  Check(!w.Refused,"a new weapon starts with the game's reload refused");
  Check(!w.Start(a,true)&&w.Start(a,false)&&w.Start(new AmmoManagementComponent(),true),"secondary (grenade launcher) reload blocked or primary allowed");
  Check(w.Refused,"refusing the game's own reload is not remembered (0.1.123: its fire wait must be ended)");
  // 0.1.221: the grip takes from the pouch, not the trigger.
  w.Step(lh:HandControls.Trigger,ld:HandControls.Trigger,hand:new Vector3(-1,0,0));w.Step();
  Check(!w.Holding&&a.PrimaryReserveAmmoCount==27,"the trigger still takes a magazine from the pouch");
  int beforeTake=w.Resets;
  w.Pick();Check(a.PrimaryMagazineAmmoCount==0&&a.PrimaryReserveAmmoCount==15&&w.Held==12,"pouch pickup did not reserve exact magazine");
  Check(w.Resets==beforeTake+1,"taking ammunition from the pouch does not cancel the game's reload");
  int releases=w.FireReleases;
  int resets=w.Resets;
  w.Insert();Check(w.FireReleases==releases+1,"insert does not end the game's wait for its own reload (0.1.123 crossbow)");
  Check(w.Resets==resets+1,"insert does not cancel the game's own reload (0.1.124 crossbow shot sound without a bolt)");
  Check(w.Redraws==0,"a pistol magazine insert draws the gun again (only the crossbow needs it)");Check(a.PrimaryMagazineAmmoCount==12&&a.PrimaryReserveAmmoCount==15&&!w.Blocked,"insert refills reserve, or (0.1.242) a magazine changed with rounds left asks for the bolt");
  w.Rack();Check(!w.Blocked,"working the bolt with a round chambered blocked the gun");
  int writes=a.Writes;for(int i=0;i<30;i++)w.Step();Check(a.Writes==writes,"idle commits ammunition repeatedly");
  w.Step(HandControls.B,HandControls.B);w.Step(HandControls.B,dt:.35f);
  w.Step(HandControls.B,lh:HandControls.Grip,ld:HandControls.Grip,hand:Vector3.zero);
  Check(w.Held==12&&a.PrimaryMagazineAmmoCount==0&&a.PrimaryReserveAmmoCount==15,"manual extraction commits wrong ammo");
  w.Cancel();Check(a.PrimaryReserveAmmoCount==27&&w.Blocked,"old extracted magazine not refunded on focus loss");
  a.ammoPool.Reserve=8;
  w.Pick();Check(a.PrimaryReserveAmmoCount==0&&w.Held==8,"partial reserve magazine incorrect");
  w.Cancel();w.Cancel();Check(a.PrimaryReserveAmmoCount==8,"menu/refocus returned ammunition more than once");
  w.Pick();w.Step();Check(w.Held==0&&a.PrimaryReserveAmmoCount==8,"discarded fresh magazine did not return ammo");
  a.ammoPool.Reserve=0;
  w.Pick();Check(w.Holding&&w.Held==0,"no reserve cannot yield empty magazine");w.Insert();Check(a.PrimaryMagazineAmmoCount==0,"empty magazine manufactures ammo");
  w.Rack();
  // HUD callback failure cannot duplicate ammo, disable the module or undo an insertion.
  var b=new WeaponHands();b.Ammo.ThrowHud=true;b.Eject();b.Pick();b.Insert();
  Check(b.Ammo.PrimaryMagazineAmmoCount==12&&b.Ammo.PrimaryReserveAmmoCount==15,"HUD exception invalidates ammo transaction");
  b.Cancel();Check(b.Ammo.PrimaryReserveAmmoCount==15&&!b.Blocked,"focus after insertion refunded installed magazine/chambered round");
  b.Change(2);Check(!b.Blocked,"new weapon inherits removed magazine");b.Change(1);Check(!b.Blocked,"the weapon's kept chamber lost on changing weapons");
  // Failed native write returns an uninserted fresh magazine without duplicating bullets.
  var fail=new WeaponHands();fail.Eject();fail.Pick();fail.Ammo.ThrowSet=true;fail.Insert();
  Check(fail.Ammo.PrimaryMagazineAmmoCount==0&&fail.Ammo.PrimaryReserveAmmoCount==27&&!fail.Holding,"failed insertion loses/duplicates reserved ammunition");
  var shell=new WeaponHands("shotgun");shell.Ammo.PrimaryMagazineAmmoCount=0;shell.Pick();shell.Insert();
  Check(shell.Ammo.PrimaryMagazineAmmoCount==1&&shell.Ammo.PrimaryReserveAmmoCount==19&&shell.Blocked,"shell insert not exactly one or empty shotgun skips pump");
  shell.Rack();Check(!shell.Blocked,"pump does not release shotgun");
  for(int i=0;i<8;i++)
  {
   shell.Step(lh:HandControls.Trigger|HandControls.Grip,hand:new Vector3(-1,0,0));
   Check(shell.Holding&&shell.Held==1,"cannot take shell again with held grip: "+i);
   shell.Insert();Check(shell.Ammo.PrimaryMagazineAmmoCount==i+2,"repeated shell not inserted: "+i);
  }
  var repeat=new WeaponHands("ak47");
  for(int cycle=0;cycle<5;cycle++){repeat.Eject();repeat.Pick();repeat.Insert();repeat.Rack();Check(!repeat.Blocked&&repeat.Ammo.PrimaryMagazineAmmoCount>0,"AK repeat cycle blocked: "+cycle);}
  // Holding a hostage at the shotgun fore-end must not start a pump stroke.
  var carryGun=new WeaponHands("shotgun");GripCarry.Current=new GripCarry{HidesLeft=true};
  carryGun.Step(lh:HandControls.Grip,ld:HandControls.Grip,hand:new Vector3(0,.055f,.145f));
  Check(!carryGun.Racking,"hostage grip starts shotgun pump");
  GripCarry.Current=null;
  Check(ContactRig.Current.Resets>0,"reload transitions never reset stale contact state");
  var mode=new WeaponHands();mode.Eject();mode.Pick();mode.Mode(false);
  Check(mode.Ammo.PrimaryReserveAmmoCount==27&&!mode.Holding&&mode.Stock(mode.Ammo),"automatic mode loses held ammunition or blocks native reload");
  mode.Mode(true);Check(!mode.Stock(mode.Ammo),"manual mode cannot be restored");
  {
   // 0.1.133: the gun in the left hand: the right hand takes one from the pouch (0.1.221: its grip) and inserts it, the right hand racks; the left trigger does not reload.
   // 0.1.142: its magazine is dropped with the left Y (the hand holding it), not the right B.
   var m=new WeaponHands();m.Mirror=true;var am=m.Ammo;int loaded=am.PrimaryMagazineAmmoCount;
   m.StepR(HandControls.B,HandControls.B);m.StepR(ru:HandControls.B);
   Check(loaded>0&&am.PrimaryMagazineAmmoCount==loaded,"the right B still drops the left hand's magazine");
   m.StepR(lh:HandControls.B,ld:HandControls.B);m.StepR(lu:HandControls.B);
   Check(am.PrimaryMagazineAmmoCount==0&&am.PrimaryReserveAmmoCount==27,"the left Y does not drop the left hand's magazine");
   m.StepR(lh:HandControls.Grip,ld:HandControls.Grip,left:new Vector3(-1,0,0));
   Check(!m.Holding,"the left hand (holding the gun) takes a magazine from the pouch");
   m.StepR(HandControls.Grip,HandControls.Grip,hand:new Vector3(-1,0,0));
   Check(m.Holding&&m.Held==12&&am.PrimaryReserveAmmoCount==15,"the right hand cannot take a magazine for the left hand's gun");
   m.StepR(HandControls.Grip,hand:new Vector3(0,-.1f,-.015f));m.StepR(HandControls.Grip,hand:new Vector3(0,-.025f,-.015f));
   Check(am.PrimaryMagazineAmmoCount==12&&m.FireLocked,"the right hand cannot insert into the left hand's gun (or its trigger fires at once)");
   Check(!m.Stock(am),"the game's own reload takes over the left hand's gun");
   // 0.1.183: with a weapon in the right hand too, the left hand's pistol reloads against the chest, never the game's way.
   m.SetCopy(1,7);Check(m.Stock(am),"the left hand's gun with the right hand full lost the game's reload (no chest reload)");
   m.ChestOn=true;Check(!m.Stock(am),"the game's own reload takes over a pistol reloaded against the chest");
   m.ChestOn=false;m.SetCopy(1,-1);
   m.StepR();m.StepR(HandControls.Grip,HandControls.Grip,hand:new Vector3(0,.055f,.145f));m.StepR(HandControls.Grip,hand:new Vector3(0,.055f,.04f));m.StepR();
   Check(!m.Blocked,"the right hand cannot rack the left hand's gun");
  }
  Console.WriteLine("PASS: production WeaponHands.Reload driver against simulated game API: reserve/loaded conservation, tap drop, hold extraction, no stock auto-reload, left grip pouch/insert/bolt (0.1.223), the right hand for a gun in the left hand (its magazine out with the left Y), partial/empty supply, discard, menu/focus refunds, per-weapon chamber state, HUD exception isolation and rejected native write.");
  {
   // 0.1.122: the hint is the part's outline; a small glow only without a part shape or when the outline cannot be drawn.
   var h=new WeaponHands();h.Ammo.PrimaryMagazineAmmoCount=0;
   Outlines.Magazine=new Mesh();int glows=ReloadGlow.Shown,outlines=Outlines.Shown;
   h.Hint();Check(Outlines.Shown==outlines+1&&ReloadGlow.Shown==glows,"empty gun: magazine outline not drawn (or glow drawn too)");
   Outlines.Magazine=null;h.Hint();Check(ReloadGlow.Shown==glows+1,"no magazine shape: no fallback glow");
   Outlines.Magazine=new Mesh();Outlines.Fail=true;var f=new WeaponHands();f.Ammo.PrimaryMagazineAmmoCount=0;
   f.Hint();f.Hint();Check(ReloadGlow.Shown==glows+3&&Outlines.Shown==outlines+1,"a failing outline is retried or leaves no hint");
   Outlines.Fail=false;
  }
  Console.WriteLine("Native engine calls, model fit and actual tracking need game/headset verification.");
 }
}
namespace XiiiXR
{
 internal sealed partial class WeaponHands
 {
  // 0.1.194: no double-barrelled shotgun in these runs (BreakActionTests).
  // 0.1.215: no bazooka in these runs (ThrowLandingTests covers its math).
  private bool BazookaManual=>false;private int rocketSide=-1;
  private bool Clubbed=>false;private bool BreakReady=>false;private bool BreakHolding=>false;private bool BreakBlocksFire=>false;private string BreakLabel=>"";
  private bool BreakAccess(Vector3 p,out Vector3 a,out Vector3 b){a=b=default;return false;}private bool BreakWantsShell(Vector3 h)=>false;
  private void TickBreak(bool m,Vector3 h,Quaternion q,Vector3 w,HandControls g,HandControls l,PoseValue p,float now){}
  private void RenderBreak(PoseValue l){}private void CancelBreak(){}private static bool BreakGun(Equipable? w)=>false;
  private bool DualActive=>false;private bool DualChestOn=>false;private bool OwnsAmmo(AmmoManagementComponent a)=>false;private bool ChestGame=>ChestOn;internal bool ChestOn;internal void SetCopy(int s,int key)=>copyKey[s]=key;private bool RevolverReady=>false;private RevolverReloadState revolver=new();private void CancelRevolver(){}
  internal static WeaponHands? Current;private CameraRig rig=new();private WeaponVisual? visual=new();private Equipable? weapon=new();private Inventory? inventory=new();private string profile;private int playerId=0;private bool enabled=true;
  internal WeaponHands(string p="pistol"){profile=p;reloadAction=2;_ = reloadAction;ammo=new AmmoManagementComponent();Current=this;BindReload();TickReloadProps();}
  internal AmmoManagementComponent Ammo=>ammo!;internal int Held=>reload.HeldRounds;internal bool Holding=>reload.Holding;internal bool Blocked=>reload.BlocksFire;internal bool Racking=>reload.Racking;
  internal bool Stock(AmmoManagementComponent a)=>AllowStockReload(a);
  internal bool Start(AmmoManagementComponent a,bool primary)=>AllowStartReload(a,primary);
  internal void Hint()=>UpdateGlow();
  internal void Change(int id){CancelReloadGesture();weapon!.Id=id;BindReload();}
  internal void Cancel()=>CancelReloadGesture();
  internal void Mode(bool enabled){WeaponOptions.ManualReload.Value=enabled;TickReloadProps();}
  internal void Step(ulong rh=0,ulong rd=0,ulong ru=0,ulong lh=0,ulong ld=0,Vector3? hand=null,float dt=.1f)
  {Time.realtimeSinceStartup+=dt;Time.frameCount++;rig.RightControls=new(true,rh,rd,ru);rig.LeftControls=new(true,lh,ld,0);rig.Left=new PoseValue(hand??new Vector3(1,1,1));TickReload();}
  internal void Eject(){Step(HandControls.B,HandControls.B);Step(ru:HandControls.B);}
  // 0.1.133: the game's gun in the left hand: the right hand reloads it.
  internal void StepR(ulong rh=0,ulong rd=0,ulong ru=0,Vector3? hand=null,ulong lh=0,ulong ld=0,Vector3? left=null,ulong lu=0)
  {Time.realtimeSinceStartup+=.1f;Time.frameCount++;rig.RightControls=new(true,rh,rd,ru);rig.LeftControls=new(true,lh,ld,lu);rig.Right=new PoseValue(hand??new Vector3(1,1,1));rig.Left=new PoseValue(left??new Vector3(1,1,1));TickReload();}
  internal bool FireLocked=>leftTriggerLocked;
  internal void Pick()=>Step(lh:HandControls.Grip,ld:HandControls.Grip,hand:new Vector3(-1,0,0));
  internal void Insert(){Step(lh:HandControls.Grip,hand:new Vector3(0,-.1f,-.015f));Step(lh:HandControls.Grip,hand:new Vector3(0,-.025f,-.015f));}
  internal void Rack()
  {
   Step();ulong bit=HandControls.Grip;   // 0.1.223: the grip works the bolt too (was the trigger)
   Step(lh:bit,ld:bit,hand:new Vector3(0,.055f,.145f));Step(lh:bit,hand:new Vector3(0,.055f,.04f));
   if(profile=="shotgun")Step(lh:bit,hand:new Vector3(0,.055f,.145f));else Step();
  }
  private bool CanControl(int id)=>true;internal int FireReleases;internal bool Refused=>nativeReloadRefused;private void ReleaseNativeFire(string why){FireReleases++;}internal int Resets;private void ResetNativeReload(string why){Resets++;}internal int Redraws;private void RequestRedraw(string why){Redraws++;}internal bool LeftHoldsWeapon=>false;private readonly int[] copyKey={-1,-1};internal bool PrimaryLeft=>Mirror;internal bool Mirror;internal static bool LeftHanded=>false;internal bool leftTriggerLocked;private bool foreEndOnly=false;private bool copyShooting=false;private void StopOwnedFire(){}private void ReleaseSupport(){}private static System.Numerics.Vector3 ToN(Vector3 p)=>p.N;
 }
 internal class GripCarry{internal static GripCarry? Current=null;internal bool HidesLeft=false;internal bool HoldingBody=false;}
 internal class Inventory{internal bool isInTransit=>false;internal PlayerAmmoPool playerAmmo=new();}
 internal class PlayerAmmoPool{internal AmmoPool AmmoPool=new();internal int GetAmmoCount(ActorAmmoPool.AmmoType type)=>AmmoPool.Reserve;}
 internal class ActorAmmoPool{internal enum AmmoType{Shotgun_12GBuckshot=7}}
 internal readonly record struct PoseValue(Vector3 Position);
 internal class CameraRig
 {
  internal bool Scripted=>false;internal Transform? PlayerRoot=new();internal Quaternion HeadRotation=>Quaternion.identity;internal Vector3 HeadPosition=>Vector3.zero;internal HandControls LeftControls,RightControls;internal PoseValue Left,Right;
  internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){l=Left;r=Right;valid=true;return true;}
  internal static Vector3 UnityPosition(PoseValue p)=>p.Position;internal void DisarmTrigger(){}internal void ReloadHaptics(ReloadAction action,bool right=false){}internal void ResistanceHaptics(float a,bool right=false){}
 }
 internal class Config{internal bool Value=true;}internal static class WeaponOptions{internal static Config ManualReload=new();}
 internal static class GloveVisual{internal static Quaternion Rotation(PoseValue p,bool right)=>Quaternion.identity;}
 internal class WeaponVisual
 {internal bool BreakAction=>false;internal Vector3 CylinderSocket=>Vector3.zero;internal bool StableGun;internal void EjectShell(ReloadAudio? audio){}
  internal bool ManualMode;internal bool CylinderReady=>false;internal bool PrepareCylinder()=>false;internal void PoseCylinder(bool a,bool b,Vector3 c,Quaternion d){}internal bool ReloadAvailable=>true;internal bool ReloadUnavailable=>false;internal Vector3? CoverGrab=>null;internal Vector3? CoverMiddle=>null;internal Mesh? BoltShape=>null;internal float ArrowLength=>0;internal void PoseArrowOnRail(float along){}internal float StringDraw=1;internal float StringDrawFor(float offset)=>1-offset;internal Vector3 BoltShapeCenter=>Vector3.zero;internal Mesh? CoverShape=>null;internal Matrix4x4 CoverShapeToFit=>new();internal const float CoverOpenDegrees=75;internal bool CoverOpen;internal float? CoverPushDegrees;internal bool CoverFrame(out Vector3 h,out Vector3 e){h=e=default;return false;}internal bool PrepareReload()=>true;internal Matrix4x4 FittedToWorld=>new();internal Vector3 MagazineCenter=>Vector3.zero;internal Vector3 ReloadPort=>Vector3.zero;internal Vector3 ReloadBolt=>new(0,.03f,.16f);internal Vector3 InsertAxis=>Vector3.up;internal ReloadMesh Ammunition=new();internal void ReloadPose(bool a,bool b,bool c,float d,Vector3 e,Quaternion f,bool empty=false){}
 internal void PoseAmmunition(Vector3 p,Quaternion q,bool right=false){}internal Vector3 AmmunitionTip(Vector3 p,Quaternion q,bool right=false)=>p+new Vector3(0,-.025f,.015f);
 internal Vector3 AmmunitionForward(Quaternion q,bool right=false)=>Vector3.up;
 internal void AmmunitionPose(Vector3 p,Quaternion r,out Vector3 v,out Quaternion q,bool right=false){v=p;q=r;} }
 internal class ReloadAudio{internal bool PlayCue(string p,int k)=>true;internal void Tick(Vector3 p,Quaternion q){}internal void Play(ReloadAction a,string s,Vector3 p,Vector3 h,Quaternion q){}}
 internal class ReloadMesh{internal void Pose(Vector3 p,Quaternion q){}internal Mesh? Shape=>Outlines.Magazine;}
 internal static class Outlines{internal static Mesh? Magazine;internal static int Shown;internal static bool Fail;}
 internal class ReloadOutline{internal ReloadOutline(){if(Outlines.Fail)throw new System.InvalidOperationException("no Standard");}internal bool Show(Mesh? m,Matrix4x4 p){if(m==null)return false;Outlines.Shown++;return true;}internal void Hide(){}internal void Dispose(){}}
 internal class AmmoPouch{internal bool Valid=>true;internal void Dispose(){}internal void SetShellCount(int count){}internal void Pose(Vector3 h,Quaternion t,bool right=false){}internal bool Near(Vector3 p)=>p.x<-.8f;internal bool NearShell(Vector3 p)=>Near(p);internal void Hide(){}internal bool Shown=>true;internal Vector3 Center=>Vector3.zero;internal Mesh? Shape=>null;internal Matrix4x4 ShapeToWorld=>new();}
 internal sealed class WeaponImpactAudio{internal WeaponImpactAudio(){}}
 internal class ReloadGlow{internal static int Shown;internal void Show(Vector3 p,float r){Shown++;}internal void Hide(){}internal void Dispose(){}}
 internal class ReloadDrops{internal static int Count;internal void Add(ReloadMesh a,Vector3 p,Quaternion q,Vector3 v){Count++;}internal void Tick(Transform? t){}}
 internal sealed class ContactRig{internal static ContactRig Current=new();internal int Resets;internal bool ResolveHand(bool a,bool b,ref Vector3 p,ref Quaternion q)=>true;internal void ResetHand(bool right){if(right!=(WeaponHands.Current?.PrimaryLeft==true))throw new Exception("reload reset wrong hand");Resets++;}}
 internal static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
namespace PlayMagic.Weapons
{
 internal class Equipable{internal IntPtr Pointer= (IntPtr)1;internal int Id=1;internal int GetInstanceID()=>Id;}
 internal class AmmoManagementComponent
 {
  static int next;internal IntPtr Pointer=(IntPtr)(++next);internal int PrimaryMagazineAmmoCount=7;internal int PrimaryReserveAmmoCount=>ammoPool.Reserve;internal int MaxPrimaryMagazineAmmoCount=>12;internal int SecondaryMagazineAmmoCount=>0;
  internal AmmoPool ammoPool=new();internal int primaryAmmoType=>1;internal bool triggerPrimaryReload,triggerSecondaryReload,ThrowHud,ThrowSet;internal int Writes;
  internal void CancelReload(){}internal void SetAmmo(int p,int s){if(ThrowSet)throw new Exception("set failure");PrimaryMagazineAmmoCount=p;Writes++;}
  internal void TriggerEventAmmoPoolChanged(){if(ThrowHud)throw new Exception("HUD failure");}
 }
 internal class AmmoPool
 {internal bool IsInfinite=>false;internal int Reserve=20;internal int TryRemoveAmmo(int t,int n){int used=Math.Min(Reserve,n);Reserve-=used;return used;}internal void AddAmmo(int t,int n){Reserve+=n;}}
}
namespace UnityEngine
{
 internal readonly struct Vector3
 {
  internal readonly System.Numerics.Vector3 N;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3 normalized=>From(System.Numerics.Vector3.Normalize(N));internal float x=>N.X;internal float sqrMagnitude=>N.LengthSquared();
  internal static Vector3 forward=>new(0,0,1);internal static Vector3 positiveInfinity=>new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);
  internal static float Dot(Vector3 a,Vector3 b)=>System.Numerics.Vector3.Dot(a.N,b.N);
  internal static Vector3 From(System.Numerics.Vector3 n)=>new(n.X,n.Y,n.Z);internal static Vector3 zero=>default;internal static Vector3 up=>new(0,1,0);internal static Vector3 down=>new(0,-1,0);
  public static Vector3 operator+(Vector3 a,Vector3 b)=>From(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>From(a.N-b.N);public static Vector3 operator*(Vector3 a,float b)=>From(a.N*b);public static Vector3 operator/(Vector3 a,float b)=>From(a.N/b);
  internal static Vector3 ClampMagnitude(Vector3 a,float n)=>a.N.Length()>n?From(System.Numerics.Vector3.Normalize(a.N)*n):a;
 }
 internal struct Quaternion{internal static Quaternion identity=>new();public static Vector3 operator*(Quaternion q,Vector3 p)=>p;}
 internal class Mesh{}
 internal struct Matrix4x4{internal static Matrix4x4 identity=>new();internal static Matrix4x4 Translate(Vector3 v)=>new();public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>a;internal Matrix4x4 inverse=>new();internal Quaternion rotation=>new();internal Vector3 MultiplyPoint3x4(Vector3 p)=>p;internal Vector3 MultiplyVector(Vector3 p)=>p;}
 internal class Transform{}
 internal static class Time{internal static float realtimeSinceStartup;internal static int frameCount;}
}
