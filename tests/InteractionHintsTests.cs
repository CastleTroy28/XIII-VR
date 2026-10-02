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
  Console.WriteLine("PASS: enabled native prompts retain their own visibility; global off/on and shutdown restore native alpha. No door/key/card exception or replacement gesture text.");
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace UnityEngine
{
 class Obj{internal T? TryCast<T>()where T:class=>this as T;}
 class GameObject:Obj{internal CanvasGroup Group=new();internal int GetInstanceID()=>GetHashCode();internal Obj GetComponent(Type t)=>Group;internal Obj AddComponent(Type t)=>Group;}
 class CanvasGroup:Obj{internal float alpha=1;}
 class Transform{internal string name="door";internal Transform? parent=>null;}
 class Component:Obj{internal Transform transform=new();internal Obj? GetComponentInParent(Type t)=>null;}
}
class RaycastAction:Component{}
namespace PlayMagic.AI{class Door{}}
class PlayerHUDControl{internal Group promptGroup=new();internal GameObject secondaryPromptGroup=new(),hitInteractionLabel=new();internal Tutorial tutorialController=new();internal Owner GetOwner()=>new();internal class Group{internal GameObject gameObject=new();}internal class Tutorial{internal GameObject group=new();}internal class Owner{internal bool IsPlayer=>true;}}
namespace XiiiXR{class CameraRig{internal static CameraRig Current=new();}static class QualityOptions{internal static Toggle Hints=new();internal class Toggle{internal bool Value=true;}}static class Bootstrap{internal static void Warn(string s)=>throw new Exception(s);}}
