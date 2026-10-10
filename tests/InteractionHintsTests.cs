using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;
class InteractionHintsTests
{
 static void Check(bool x,string m){if(!x)throw new Exception(m);}
 static void Main()
 {
  var hud=new PlayerHUDControl();hud.promptGroup.gameObject.Group.alpha=.8f;hud.secondaryPromptGroup.Group.alpha=.7f;hud.hitInteractionLabel.Group.alpha=.6f;
  InteractionHints.Apply(hud);Check(hud.promptGroup.gameObject.Group.alpha==.8f&&hud.secondaryPromptGroup.Group.alpha==.7f&&hud.hitInteractionLabel.Group.alpha==.6f,"enabled hints override native fade or hide door/lock labels");
  QualityOptions.Hints.Value=false;InteractionHints.Apply(hud);Check(hud.promptGroup.gameObject.Group.alpha==0&&hud.hitInteractionLabel.Group.alpha==0&&hud.tutorialController.group.Group.alpha==0,"global hints switch incomplete");
  QualityOptions.Hints.Value=true;InteractionHints.Apply(hud);Check(hud.promptGroup.gameObject.Group.alpha==.8f&&hud.secondaryPromptGroup.Group.alpha==.7f&&hud.hitInteractionLabel.Group.alpha==.6f,"re-enable fails to restore native prompts");
  QualityOptions.Hints.Value=false;InteractionHints.Apply(hud);InteractionHints.Restore();Check(hud.promptGroup.gameObject.Group.alpha==.8f,"shutdown leaves native prompts hidden");
  // 0.1.83: vent grilles and hatches need Grip + A like doors; "event"/"inventory" do not.
  foreach(var n in new[]{"door_01_b","cabinet_01","vent_set01_cap_01 (2)","cp_vent_set01_cap_01","floor_hatch"})Check(InteractionHints.ChordName(n),n+" not a Grip+A target");
  foreach(var n in new[]{"ashtray_01","chair_01 (2)","event_trigger","inventory_pickup","bottle"})Check(!InteractionHints.ChordName(n),n+" wrongly needs Grip+A");
  // 0.1.253: a thing to take is never a door, whatever its parents are called or hold (a bottle in the prison's laundry needed Grip + A).
  Component Placed(string name,params string[] parents){var c=new Component();c.transform.name=name;var t=c.transform;foreach(var n in parents){t.Parent=new Transform{name=n};t=t.Parent;}return c;}
  var bottle=Placed("bottle_01 (9)","props_laundry","Lockers_Room");
  Check(InteractionHints.IsDoor(bottle),"the name test reaches the parents (a locker room)");
  bottle.InParent.Add(typeof(PickableItem));
  Check(!InteractionHints.IsDoor(bottle),"a bottle among the lockers still needs Grip + A");
  Check(XiiiXR.Bootstrap.Lines.Count==1&&XiiiXR.Bootstrap.Lines[0].Contains("bottle_01 (9) / props_laundry / Lockers_Room")&&XiiiXR.Bootstrap.Lines[0].Contains("either grip"),"the thing taken by the grip despite its place is not logged");
  InteractionHints.IsDoor(bottle);Check(XiiiXR.Bootstrap.Lines.Count==1,"logged every frame");
  var key=Placed("key_cell_03","cell_cabinet_02");key.InParent.Add(typeof(PlayMagic.AI.Door));key.InParent.Add(typeof(PickableItem));
  Check(!InteractionHints.IsDoor(key)&&XiiiXR.Bootstrap.Lines.Count==2&&XiiiXR.Bootstrap.Lines[1].Contains("key_cell_03"),"a key lying in a cabinet (a door above it) still needs Grip + A");
  var cabinet=Placed("raycast_target","cabinet_01");Check(InteractionHints.IsDoor(cabinet),"a cabinet no longer needs Grip + A");
  var door=Placed("raycast_target","room");door.InParent.Add(typeof(PlayMagic.AI.Door));Check(InteractionHints.IsDoor(door),"a door no longer needs Grip + A");
  var chair=Placed("chair_01 (2)","props");chair.InParent.Add(typeof(PickableItem));Check(!InteractionHints.IsDoor(chair)&&XiiiXR.Bootstrap.Lines.Count==2,"a chair called a door, or logged");
  Console.WriteLine("PASS: enabled native prompts retain their own visibility; global off/on and shutdown restore native alpha. No door/key/card exception or replacement gesture text. A thing to take is taken by the grip wherever it lies.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Obj{internal T? TryCast<T>()where T:class=>this as T;}
 class GameObject:Obj{internal CanvasGroup Group=new();internal int GetInstanceID()=>GetHashCode();internal Obj GetComponent(Type t)=>Group;internal Obj AddComponent(Type t)=>Group;}
 class CanvasGroup:Obj{internal float alpha=1;}
 class Transform{internal string name="door";internal Transform? Parent;internal Transform? parent=>Parent;}
 class Component:Obj{internal Transform transform=new();internal List<Type> InParent=new();internal Obj? GetComponentInParent(Type t)=>InParent.Contains(t)?this:null;}
}
class RaycastAction:Component{}
class PickableItem:Component{}
namespace PlayMagic.AI{class Door{}}
class PlayerHUDControl{internal Group promptGroup=new();internal GameObject secondaryPromptGroup=new(),hitInteractionLabel=new();internal Tutorial tutorialController=new();internal Owner GetOwner()=>new();internal class Group{internal GameObject gameObject=new();}internal class Tutorial{internal GameObject group=new();}internal class Owner{internal bool IsPlayer=>true;}}
namespace XiiiXR{class CameraRig{internal static CameraRig Current=new();}static class QualityOptions{internal static Toggle Hints=new();internal class Toggle{internal bool Value=true;}}static class Bootstrap{internal static List<string> Lines=new();internal static void Write(string s)=>Lines.Add(s);internal static void Warn(string s)=>throw new Exception(s);}}
