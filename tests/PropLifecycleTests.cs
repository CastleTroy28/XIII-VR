using System;
using XiiiXR;
using PlayMagic.Weapons;
class PropLifecycleTests
{
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static void Main()
 {
  var c=new WeaponHands();var item=c.Item;
  c.BreakHeldProp(item);c.BreakHeldProp(item);
  Check(c.Inventory.Returns==1&&c.Hidden==1,"missing fragments leave prop held / duplicate consumption");
  c=new WeaponHands();var a=new DestructableWeaponPart();var b=new DestructableWeaponPart{Fail=true};c.Item.Parts=new[]{a,b};
  c.BreakHeldProp(c.Item);Check(a.Calls==1&&b.Calls==1&&c.Inventory.Returns==1,"child fragments or fragment failure blocks weapon restoration");
  c=new WeaponHands();c.Switch(false);Check(c.Inventory.Removed==0&&c.Hidden==0,"rejected native selection discards prop");
  c.Switch(true);Check(c.Inventory.Removed==1&&c.Hidden==1,"accepted selection leaves held item alive");
  c=new WeaponHands();Check(c.Request(c.Inventory,PlayerEquipableInventory.ActiveEquipmentSlot.Fist),"fists cannot be selected while holding prop");
  Check(!c.Request(new PlayerEquipableInventory(),PlayerEquipableInventory.ActiveEquipmentSlot.Pistol),"other inventory intercepted");
  Check(!c.Request(c.Inventory,PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental),"environment pickup intercepted");
  c.Scripted=true;Check(!c.Request(c.Inventory,PlayerEquipableInventory.ActiveEquipmentSlot.Pistol),"scripted selection changed");
  // 0.1.150: a broom or shovel holds four blows on enemies and breaks on the fifth; hits on walls do not wear it.
  c=new WeaponHands();item=c.Item;
  for(int i=0;i<10;i++)c.WearHeldProp(item,false);
  for(int i=1;i<MeleeDamageMath.PropDurability;i++){c.WearHeldProp(item,true);Check(c.Inventory.Returns==0&&c.Hidden==0,"the prop broke on blow "+i);}
  c.WearHeldProp(item,true);Check(c.Inventory.Returns==1&&c.Hidden==1,"the prop did not break on its last blow");
  c=new WeaponHands();var first=c.Item;c.WearHeldProp(first,true);c.WearHeldProp(first,true);
  var other=new Equipable();c.WearHeldProp(other,true);c.WearHeldProp(first,true);c.WearHeldProp(first,true);c.WearHeldProp(first,true);
  Check(c.Inventory.Returns==0,"another prop's blows counted on this one");
  Console.WriteLine("PASS: a broom/shovel breaks on its 5th blow on an enemy (not on walls, each prop counted apart).");
  Console.WriteLine("PASS: actual prop lifecycle adapter: consume once with missing/failed child fragments; successful selection discards prop, rejected selection preserves it; owner and scripted-state isolation. Mock inventory, not a game run.");
 }
}
namespace XiiiXR
{
 internal sealed partial class WeaponHands
 {
  internal static WeaponHands? Current;private bool enabled=true,disposed=false;private CameraRig rig=new();private string profile="prop";
  private PlayerEquipableInventory? inventory=new();private Equipable? weapon=new();private WeaponVisual? visual=new();private bool poseValid=true;
  internal WeaponHands(){Current=this;inventory!.currentEquipable=weapon;}
  internal Equipable Item=>weapon!;internal PlayerEquipableInventory Inventory=>inventory!;internal int Hidden=>visual!.Hides;
  internal bool Scripted{set=>rig.Scripted=value;}
  private void StopOwnedFire(){}private void ReleaseSupport(){}private void ClearThrow(){}
  internal bool Request(PlayerEquipableInventory i,PlayerEquipableInventory.ActiveEquipmentSlot slot)
  {bool ignore=false,animation=false;SelectAwayFromProp(i,slot,ref ignore,ref animation,out _);return ignore&&animation;}
  internal void Switch(bool accepted)
  {bool ignore=false,animation=false;SelectAwayFromProp(inventory!,PlayerEquipableInventory.ActiveEquipmentSlot.Pistol,ref ignore,ref animation,out var state);SelectedAwayFromProp(inventory!,accepted,state);_ = poseValid;}
 }
 internal class CameraRig{internal bool Scripted;}
 internal class WeaponVisual{internal int Hides;internal string GripProfile=>"prop";internal UnityEngine.Matrix4x4 FittedToWorld=>new();internal UnityEngine.Matrix4x4 GameWorldToFitted=>new();internal void Hide(){Hides++;}}
 internal class GripCarry{internal static GripCarry? Current=null;internal void Forget(Equipable item){}}
 internal static class Bootstrap{internal static void Write(string message){}internal static void Warn(string message){}}
}
class PlayerEquipableInventory
{
 static int next;internal IntPtr Pointer=(IntPtr)(++next);internal Equipable? currentEquipable;internal int Returns,Removed;
 internal enum ActiveEquipmentSlot{Fist=20,Pistol=21,Enviromental=34}
 internal void Remove(Equipable item){Removed++;currentEquipable=null;}
 internal void RemoveAndSwitch(Equipable item){Returns++;currentEquipable=null;}
}
namespace PlayMagic.Weapons
{
 class Equipable
 {static int next;internal IntPtr Pointer=(IntPtr)(++next);internal string identifier="wpn_ms_shovel";internal PlayerEquipableInventory.ActiveEquipmentSlot slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
 internal DestructableWeaponPart[] Parts=Array.Empty<DestructableWeaponPart>();internal object[] GetComponentsInChildren(Type type,bool inactive)=>Parts;}
 class DestructableWeaponPart{internal int Calls;internal bool Fail;internal UnityEngine.GameObject[] pieces=Array.Empty<UnityEngine.GameObject>();internal void Shatter(Equipable item){Calls++;if(Fail)throw new Exception("bad fragments");}}
}
namespace Il2CppInterop.Runtime
{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}static class Cast{internal static T? TryCast<T>(this object item)where T:class=>item as T;}}
namespace UnityEngine
{
 struct Vector3{internal static Vector3 zero=>new();}struct Quaternion{}
 struct Matrix4x4{public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new();internal Quaternion rotation=>new();internal Vector3 MultiplyPoint3x4(Vector3 v)=>v;}
 class Transform{internal Matrix4x4 localToWorldMatrix=>new();internal void SetPositionAndRotation(Vector3 p,Quaternion q){}}
 class GameObject{internal Transform transform=new();}
}
