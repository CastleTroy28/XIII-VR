using System;using XiiiXR;using UnityEngine;using N=System.Numerics.Vector3;
class WheelItemsTests
{
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static void Main()
 {
  var rig=new CameraRig();var items=new WheelItems(rig);var wheel=new InventoryWheel{IsOpen=true};
  var small=new ConsumablesSlot{itemPrefab=new Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS},icon=new Image()};
  var large=new ConsumablesSlot{itemPrefab=new Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL},icon=new Image()};
  large.icon.rectTransform.position=new Vector3(.5f,0,1.7f);wheel.consumableSlots=new[]{small,large};items.Bind(wheel);
  items.Hover();Check(items.Hovered&&small.icon.color.r==1,"ray does not highlight small medkit");
  Check(items.Commit()&&items.Pending&&wheel.playerInventory.Uses==0,"preview invokes consumable or fails");
  Check(small.icon.color.r==.5f,"hover highlight not restored after selection");
  rig.MenuRightControls=new HandControls(true,HandControls.Trigger,0,0);items.Tick(true);Check(wheel.playerInventory.Uses==0,"trigger held over confirmation immediately uses medkit");
  rig.MenuRightControls=new HandControls(true,0,0,0);items.Tick(true);rig.MenuRightControls=new HandControls(true,HandControls.Trigger,0,0);items.Tick(true);
  Check(wheel.playerInventory.Uses==1&&!items.Pending&&wheel.playerInventory.Last==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS,"deliberate use does not call native consumable path once");
  for(int i=0;i<40;i++)items.Tick(true);Check(wheel.playerInventory.Uses==1,"held trigger repeats use");
  rig.Position=new Vector3(.5f,0,0);items.Hover();Check(items.Commit()&&items.Notice==""&&HeldItemVisual.LastSlot==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL,"cannot choose large kit independently");
  var wrist=new Transform();items.Render(wrist);Check(HeldItemVisual.Attachment==wrist,"item not posed on actual hand");
  items.Clear();Check(HeldItemVisual.Disposed>=2,"item resources leaked on cancellation/use");Check(!items.Pending&&items.Notice=="","weapon selection cannot cancel item preview");
  items.Hover();items.Commit();items.Tick(false);Check(items.Pending&&wheel.playerInventory.Uses==1,"brief inventory lock uses or discards preview");items.Clear();Check(!items.Pending,"explicit context loss leaves stale preview");
  wheel.playerInventory.Stock=false;items.Hover();Check(!items.Commit()&&!items.Pending,"empty medkit stock selected");
  wheel.IsOpen=false;items.Hover();Check(!items.Hovered,"hidden wheel remains selectable");
  Check(rig.Disarms>=3,"consumable preview/use does not disarm gameplay trigger");
  // 0.1.150: a left-hander holds the medkit in the left hand and uses it with the left trigger.
  WeaponHands.LeftHanded=true;wheel.IsOpen=true;wheel.playerInventory.Stock=true;rig.Position=new Vector3(0,0,0);
  items.Hover();Check(items.Commit()&&items.Pending,"left-handed: no medkit preview");
  var leftWrist=new Transform();items.Render(wrist,leftWrist);Check(HeldItemVisual.Attachment==leftWrist&&HeldItemVisual.Mirrored,"left-handed: the medkit not in the left hand");
  int uses=wheel.playerInventory.Uses;
  rig.MenuRightControls=new HandControls(true,0,0,0);rig.LeftControls=new HandControls(true,0,0,0);items.Tick(true);
  rig.MenuRightControls=new HandControls(true,HandControls.Trigger,0,0);items.Tick(true);Check(wheel.playerInventory.Uses==uses&&items.Pending,"left-handed: the right trigger used the medkit");
  rig.MenuRightControls=new HandControls(true,0,0,0);rig.LeftControls=new HandControls(true,HandControls.Trigger,0,0);items.Tick(true);Check(wheel.playerInventory.Uses==uses+1&&!items.Pending,"left-handed: the left trigger does not use the medkit");
  rig.LeftControls=default;WeaponHands.LeftHanded=false;
  Console.WriteLine("PASS: a left-hander holds the medkit in the left hand and uses it with the left trigger (the right one does nothing).");
  // 0.1.186: a medkit taken from a forearm by a hand's grip: in that hand (the
  // wheel's item blocks the weapon, this one does not), held while that grip is held.
  var inv=wheel.playerInventory;inv.Stock=true;uses=inv.Uses;int leftDisarms=rig.LeftDisarms;
  const ulong G=HandControls.Grip,T=HandControls.Trigger;
  rig.LeftControls=new HandControls(true,G|T,G,0);rig.MenuLeftControls=new HandControls(true,G|T,G|T,0);
  items.TakeFromArm(PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL,0,new HeldItemVisual(),inv);
  Check(items.Pending&&items.ArmSide==0&&items.ArmHeld(false)&&!items.ArmHeld(true)&&items.ArmSlot==10&&items.GripProfile=="medkit_l","a forearm medkit not held in the left hand");
  Check(rig.LeftDisarms==leftDisarms+1,"taking it does not keep the left trigger from the gun/magazines");
  items.Render(wrist,leftWrist);Check(HeldItemVisual.Attachment==leftWrist&&HeldItemVisual.Mirrored,"a forearm medkit not drawn in the left hand");
  items.Tick(true);Check(inv.Uses==uses,"the trigger held while taking it used it");
  rig.MenuRightControls=new HandControls(true,T,T,0);rig.MenuLeftControls=new HandControls(true,G,0,0);items.Tick(true);
  Check(inv.Uses==uses&&items.Pending,"the right trigger used the medkit in the left hand");
  rig.LeftControls=new HandControls(true,0,0,G);items.Tick(true);
  Check(!items.Pending&&inv.Uses==uses,"letting go of the left grip does not put it back (or used it)");
  // Taken again: the left trigger (after being let go) uses it once.
  rig.LeftControls=new HandControls(true,G,G,0);rig.MenuRightControls=new HandControls(true,0,0,0);rig.MenuLeftControls=new HandControls(true,G,0,0);
  items.TakeFromArm(PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL,0,new HeldItemVisual(),inv);items.Tick(true);
  rig.MenuLeftControls=new HandControls(true,G|T,T,0);items.Tick(true);
  Check(inv.Uses==uses+1&&inv.Last==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitL&&!items.Pending,"the left trigger does not use the forearm medkit");
  for(int i=0;i<20;i++)items.Tick(true);Check(inv.Uses==uses+1,"a held trigger used it again");
  // The right hand: its grip held, the right trigger uses it; the wheel does not take it away.
  int disarms=rig.Disarms;rig.RightControls=new HandControls(true,G,G,0);rig.MenuRightControls=new HandControls(true,G,G,0);rig.LeftControls=default;rig.MenuLeftControls=default;
  items.TakeFromArm(PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS,1,new HeldItemVisual(),inv);
  Check(items.ArmHeld(true)&&rig.Disarms==disarms+1&&items.GripProfile=="medkit_s","a forearm medkit not held in the right hand");
  items.Render(wrist,leftWrist);Check(HeldItemVisual.Attachment==wrist&&!HeldItemVisual.Mirrored,"a forearm medkit not drawn in the right hand");
  items.Tick(false);Check(items.Pending,"a moment's lock dropped the medkit held by the grip");
  items.Tick(true);rig.MenuRightControls=new HandControls(true,G|T,T,0);items.Tick(true);
  Check(inv.Uses==uses+2&&inv.Last==PlayerEquipableInventory.ActiveEquipmentSlot.MedkitS&&!items.Pending,"the right trigger does not use the medkit in the right hand");
  // The wheel's own medkit is not held by a grip.
  rig.RightControls=default;rig.MenuRightControls=new HandControls(true,0,0,0);wheel.IsOpen=true;rig.Position=new Vector3(0,0,0);items.Hover();
  Check(items.Commit()&&items.ArmSide<0,"a wheel medkit taken for a forearm one");items.Tick(true);Check(items.Pending,"a wheel medkit put back without a grip");items.Clear();
  Console.WriteLine("PASS: 0.1.186 a medkit taken from a forearm is held in the taking hand while its grip is held (drawn there, that hand's trigger alone uses it once, the trigger held at the take does not; letting go puts it back unused; a moment's lock keeps it), the wheel's medkit unchanged.");
  // 0.1.195: the zipline hook and the grappling hook from the wheel, in either hand.
  var hands=new WeaponHands();WeaponHands.Current=hands;var zip=new ZiplineVr();ZiplineVr.Current=zip;var driver=new InteractionDriver();InteractionDriver.Current=driver;var grapple=new GrappleVr();GrappleVr.Current=grapple;
  wheel.IsOpen=true;rig.Position=new Vector3(9,9,0);rig.LeftControls=new HandControls(true,0,0,0);rig.RightControls=new HandControls(true,0,0,0);rig.MenuRightControls=new HandControls(true,0,0,0);
  WheelGadgets.Next=new Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.Zipline,name="eqp_zipline_hook"};items.Hover();
  Check(items.Commit()&&items.HandTool&&items.Kind==HandToolKind.Zipline&&items.ToolSide==0&&items.LeftHeld&&!items.RightHeld,"the zipline hook not held in the left hand (or taken for the grappling hook)");
  var lw=new Transform{position=new Vector3(-.2f,0,0)};var rw=new Transform{position=new Vector3(.2f,0,0)};
  items.Render(rw,lw);Check(HeldItemVisual.Attachment==lw&&HeldItemVisual.Mirrored,"the zipline hook not drawn in the left hand");
  // 0.1.197: once the game's own hold is known, the hook lies in the hand as the game's arm holds it.
  zip.Hold=true;items.Render(rw,lw);Check(zip.HeldSide==0&&HeldItemVisual.Attachment==null,"the zipline hook not held as the game holds it");zip.Hold=false;
  items.Render(rw,lw);Check(HeldItemVisual.Attachment==lw,"the fitted hold not used until the game's hold is known");
  // 0.1.198: by its handle (found in its mesh), as a pistol by its grip, at its own size - while that hand is posed so (else as before).
  var hook=HeldItemVisual.Last!;hook.GripBar=new GripBarMath.Bar(new N(0,-.05f,0),N.UnitZ,N.UnitY,.1f,.02f);hook.TrueScale=1.6f;Time.frameCount=100;
  Check(items.TryToolHand(false,new PoseValue(new Vector3(-.2f,0,0)),out _,out _)&&Math.Abs(hands.LastThickness-.032f)<1e-5f,"the left hand not posed as on a pistol for the hook (or the hook not at its own size)");
  Check(!items.TryToolHand(true,new PoseValue(new Vector3(.2f,0,0)),out _,out _),"the right hand posed for the hook held in the left hand");
  zip.HeldSide=-1;zip.Hold=true;HeldItemVisual.Rooted=0;items.Render(rw,lw);Check(HeldItemVisual.Rooted==1&&Math.Abs(HeldItemVisual.RootScale-1.6f)<1e-5f&&zip.HeldSide<0,"the hook not held by its handle (or the game's hold used over it)");
  Time.frameCount=110;items.Render(rw,lw);Check(HeldItemVisual.Rooted==1&&zip.HeldSide==0,"a stale handle hold kept (the hand no longer posed for it)");
  zip.Riding=true;int holds=hands.Holds;Check(!items.TryToolHand(false,new PoseValue(new Vector3(-.2f,0,0)),out _,out _)&&hands.Holds==holds,"the hand posed for the hook while riding");zip.Riding=false;
  hook.GripBar=null;Check(!items.TryToolHand(false,new PoseValue(new Vector3(-.2f,0,0)),out _,out _),"the hand posed for a hook whose handle was not found");zip.Hold=false;HeldItemVisual.Attachment=lw;
  // Pointed at a zipline it starts the ride (without a press, a probe; the hand is the one holding it).
  driver.ZipHit=false;Time.realtimeSinceStartup+=1;items.Tick(true);Check(driver.ZipSide==0&&zip.Started<0,"the hook's hand not probed for a zipline (or a ride started without one)");
  // The right grip far from the left hand: nothing; close to it: the hook goes to the right hand.
  rig.RightAt=new Vector3(.5f,0,0);rig.RightControls=new HandControls(true,HandControls.Grip,HandControls.Grip,0);items.Tick(true);Check(items.ToolSide==0,"a grip far away took the hook");
  rig.RightAt=new Vector3(-.1f,0,0);hands.AllowTake=false;items.Tick(true);Check(items.ToolSide==0&&rig.Buzz>0,"a busy hand took the hook");
  hands.AllowTake=true;items.Tick(true);Check(items.ToolSide==1&&items.RightHeld&&!items.LeftHeld,"the right grip at the left hand did not take the hook over");
  rig.RightControls=new HandControls(true,HandControls.Grip,0,0);items.Render(rw,lw);Check(HeldItemVisual.Attachment==rw&&!HeldItemVisual.Mirrored,"the hook passed to the right hand not drawn there");
  driver.ZipHit=true;Time.realtimeSinceStartup+=1;items.Tick(true);Check(driver.ZipSide==1&&zip.Started==1,"pointed at a zipline from the right hand the ride does not start (by that hand)");
  zip.Riding=true;items.Render(rw,lw);Check(HeldItemVisual.Attachment==null,"the hook in the hand drawn again while riding (drawn on the cable instead)");
  items.Tick(true);zip.Riding=false;
  // At the end the cable is still pointed at: no ride back until the hook points away (or its trigger).
  int pings=driver.Pings;Time.realtimeSinceStartup+=1;items.Tick(true);Time.realtimeSinceStartup+=1;items.Tick(true);Check(driver.Pings==pings,"the ride started back by itself at the end of the cable");
  driver.ZipHit=false;Time.realtimeSinceStartup+=1;items.Tick(true);driver.ZipHit=true;Time.realtimeSinceStartup+=1;items.Tick(true);Check(driver.Pings==pings+1,"pointed away and back at a cable the hook does not ride again");
  items.Clear();
  // The grappling hook: passed to the right hand, the right trigger fires it from there.
  WheelGadgets.Next=new Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.Gadget,name="eqp_grappling_hook"};items.Hover();
  Check(items.Commit()&&items.Kind==HandToolKind.Grapple&&items.ToolSide==0,"the grappling hook not in the left hand");
  items.Render(rw,lw);Check(grapple.Side==0,"the grappling hook's preview not in the left hand");
  rig.RightControls=new HandControls(true,HandControls.Grip,HandControls.Grip,0);rig.RightAt=new Vector3(-.1f,0,0);items.Tick(true);Check(items.ToolSide==1,"the grappling hook not passed to the right hand");
  items.Render(rw,lw);Check(grapple.Side==1,"the grappling hook's preview not in the right hand");
  rig.MenuRightControls=new HandControls(true,0,0,0);items.Tick(true);driver.ToolHit=true;rig.MenuRightControls=new HandControls(true,HandControls.Trigger,HandControls.Trigger,0);items.Tick(true);
  Check(driver.ToolSide==1&&grapple.Side==1&&!items.Pending,"the right trigger did not fire the hook from the right hand");
  items.Clear();WheelGadgets.Next=null;WeaponHands.Current=null;ZiplineVr.Current=null;InteractionDriver.Current=null;GrappleVr.Current=null;
  Console.WriteLine("PASS: 0.1.198 the zipline hook held by its handle (found in its mesh) as a pistol by its grip, at its own size, only while its hand is posed so (not riding, not without a handle, the game's hold after).");
  Console.WriteLine("PASS: 0.1.195 the zipline hook (no longer taken for the grappling hook) and the grappling hook held in either hand, passed by the other hand's grip at it (not from far, not into a busy hand), drawn there (mirrored in the left), the zipline hook pointed at a zipline starts the ride by its hand (at the end of the cable not back by itself until it points away), the grappling hook fired with the right trigger from the right hand.");
  Console.WriteLine("PASS: actual WheelItems selects either icon without native equip/use; held trigger blocked; later explicit use once; cancellation/scene change/absent stock/hidden wheel; highlight restoration.");
 }
}
namespace UnityEngine
{
 static class Time{internal static float realtimeSinceStartup=0;internal static int frameCount=0;}
 class Transform{internal Vector3 position=new(0,0,0);internal Quaternion rotation=new();}
 class GameObject{internal bool activeInHierarchy=true;}
 readonly struct Vector2{internal readonly float x,y;internal Vector2(float a,float b){x=a;y=b;}}
 readonly struct Vector3{internal readonly N N;internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal static Vector3 forward=>new(0,0,1);internal static Vector3 zero=>new(0,0,0);public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);}
 readonly struct Quaternion{public static Vector3 operator*(Quaternion q,Vector3 p)=>p;public static Quaternion operator*(Quaternion a,Quaternion b)=>a;internal static Quaternion identity=>new();}
 struct Matrix4x4{internal static Matrix4x4 identity=>default;}
 struct Color{internal float r,g,b,a;internal Color(float x,float y,float z,float w){r=x;g=y;b=z;a=w;}}
 struct Rect{internal bool Contains(Vector2 p)=>Math.Abs(p.x)<.1f&&Math.Abs(p.y)<.1f;}
 class RectTransform{internal Vector3 position=new(0,0,1.7f);internal Vector3 forward=>Vector3.forward;internal Rect rect=>new();internal Vector3 InverseTransformPoint(Vector3 p)=>new(p.x-position.x,p.y-position.y,p.z-position.z);}
}
class Image{internal GameObject gameObject=new();internal RectTransform rectTransform=new();internal Color color=new(.5f,.5f,.5f,1);}
class Equipable{internal PlayerEquipableInventory.ActiveEquipmentSlot slot;internal string name="";internal string identifier="";}
class ConsumablesSlot{internal Equipable itemPrefab=null!;internal Image icon=null!;}
class PlayerEquipableInventory{internal enum ActiveEquipmentSlot{MedkitS=9,MedkitL=10,Lockpick=11,Zipline=16,Gadget=33}internal bool Stock=true;internal bool IsInventoryBlocked=>false;internal bool isInTransit=>false;internal int Uses;internal ActiveEquipmentSlot Last;internal bool HasEquipableInSlot(ActiveEquipmentSlot s)=>Stock;internal bool HasStacksForConsumable(ActiveEquipmentSlot s)=>Stock;internal Equipable? GetEquipableFromSlot(ActiveEquipmentSlot s)=>new Equipable{slot=s};internal void TryUsingConsumable(ActiveEquipmentSlot s){Uses++;Last=s;}}
class InventoryWheel{internal bool IsOpen;internal ConsumablesSlot[] consumableSlots=Array.Empty<ConsumablesSlot>();internal PlayerEquipableInventory playerInventory=new();}
namespace XiiiXR
{
 readonly struct PoseValue{internal readonly Vector3 Position;internal PoseValue(Vector3 p){Position=p;}}
 enum ReloadAction{TakeSupply}
 class CameraRig{internal Vector3 LeftAt=new(-.2f,0,0),RightAt=new(.2f,0,0);internal int Buzz,Felt;internal void ResistanceHaptics(float a,bool right=false){Buzz++;}internal void ReloadHaptics(ReloadAction a,bool right=false){Felt++;}
  internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){l=new(LeftAt);r=new(RightAt);valid=true;return true;}
  internal Vector3 Position=new(0,0,0);internal HandControls MenuRightControls,LeftControls=default,RightControls=default,MenuLeftControls=default;internal int Disarms,LeftDisarms;internal void DisarmTrigger(){Disarms++;}internal void DisarmLeftTrigger(){LeftDisarms++;}internal bool SamplePointerHand(out PoseValue p){p=new(Position);return true;}internal static Vector3 UnityPosition(PoseValue p)=>p.Position;internal Quaternion PointerRotation(PoseValue p)=>ControllerAim.Rotation(p);}
 static class ControllerAim{internal static Quaternion Rotation(PoseValue p)=>new();}
 static class ContactWorld{internal static N V(Vector3 p)=>p.N;internal static Vector3 U(N p)=>new(p.X,p.Y,p.Z);}
 class HeldItemVisual:IDisposable{internal static int Disposed;internal static PlayerEquipableInventory.ActiveEquipmentSlot LastSlot;internal static Transform? Attachment;internal static HeldItemVisual? Last;internal static HeldItemVisual Create(Equipable e){LastSlot=e.slot;return Last=new();}
  internal GripBarMath.Bar? GripBar{get;set;}internal float TrueScale{get;set;}=1;internal static int Rooted;internal static float RootScale;internal void PoseRoot(Vector3 p,Quaternion q,float scale=1){Rooted++;RootScale=scale;Attachment=null;}
  internal void Pose(Transform? t,bool left=false){Attachment=t;Mirrored=left;}internal static bool Mirrored;internal void PoseIn(Transform? t,bool left)=>Pose(t,left);internal void PreparePinch(NativeHandVisual h){}internal bool FistHand(NativeHandVisual h,ref Vector3 p,ref Quaternion q)=>false;internal string BodyName=>"";internal void PoseBody(UnityEngine.Matrix4x4 m){}internal void Hide(){Attachment=null;}public void Dispose(){Disposed++;}}
 class NativeHandVisual{}
 class WeaponHands{internal static bool LeftHanded;internal static WeaponHands? Current;internal bool AllowTake=true;internal bool CanTakeTool(bool right)=>AllowTake;
  internal float LastThickness;internal int Holds;internal bool TryToolHold(bool right,PoseValue pose,GripBarMath.Bar bar,out Vector3 at,out Quaternion hand,out Vector3 itemAt,out Quaternion itemTurn){at=pose.Position;hand=new();itemAt=new(0,0,.1f);itemTurn=new();LastThickness=bar.Thickness;Holds++;return true;}}
 class ZiplineVr{internal static ZiplineVr? Current;internal bool Riding;internal int Started=-1;internal void StartedBy(int s){Started=s;}internal bool Hold;internal int HeldSide=-1;internal bool TryHeld(int side,HeldItemVisual item){if(!Hold)return false;HeldSide=side;HeldItemVisual.Attachment=null;return true;}}
 class WheelGadgets{internal static Equipable? Next;internal Equipable? Item=>Next;internal Vector3 Point=>new(0,0,0);internal void Clear(){}internal void Hover(InventoryWheel? w,Vector3 o,Vector3 d){}}
 class InteractionDriver{internal static InteractionDriver? Current;internal int ToolSide=-1,ZipSide=-1;internal bool ZipHit,ToolHit;
  internal bool TryUseTool(PlayerEquipableInventory.ActiveEquipmentSlot s,int side){ToolSide=side;return ToolHit;}internal bool TryZipline(int side,bool pressed){ZipSide=side;Pings++;return ZipHit;}internal int Pings;internal bool ZiplineAt(int side){ZipSide=side;return ZipHit;}}
 class GrappleVr{internal static GrappleVr? Current;internal int Side;internal bool Active=>false;internal bool DeviceShown=>false;internal bool ShowPreview(UnityEngine.Transform? h=null,int side=0){Side=side;return false;}internal void HidePreview(){}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
