using System;using System.Collections.Generic;using XiiiXR;
class VrPromptLabelsTests
{
 static void Check(bool value,string error){if(!value)throw new Exception(error);}
 static void Main()
 {
  var host=TutorialHintMeaning.Detect("tutorial_17","Чтобы взять заложника, подойдите сзади и нажмите {[BUTTON_1]}.");
  Check(host==TutorialHintContext.Hostage,"localized hostage meaning lost behind generic term");
  Check(TutorialHintMeaning.Detect("TakeHostage","")==host,"English term not recognized");
  Check(TutorialHintMeaning.Detect("body_pickup","")==TutorialHintContext.Body,"body term not recognized");
  Check(TutorialHintMeaning.Detect("tutorial","Pick up a body")==TutorialHintContext.Body,"English body pickup");
  Check(TutorialHintMeaning.Detect("tutorial","Отпустите заложника")==TutorialHintContext.ReleaseBody,"release requires hold");
  Check(TutorialHintMeaning.Detect("tutorial","Take a hostage, then release him")==host,"multi-step tutorial mislabeled as release");
  Check(TutorialHintMeaning.Detect("door","Подойдите к двери и откройте замок")==TutorialHintContext.None,"door inherits hostage label");
  Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true,host)=="Удерживать левый Grip","generic tutorial Interact still right handed");
  Check(VrPromptLabels.Label(InputActions.Gameplay_HostageTaking,false)=="Hold left Grip","native hostage action");
  Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,false,TutorialHintContext.ReleaseBody)=="Release left Grip","body release input wrong");
  Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,false)=="Right Grip","pickup is not right Grip alone");
  InteractionDriver.Current=new InteractionDriver();InteractionDriver.Door=true;
  Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Правый Grip + A","doors lost Grip + A");
  // 0.1.108: water controls in the prompts.
  LocomotionDriver.Current=new LocomotionDriver{Swimming=true};
  Check(VrPromptLabels.Label(InputActions.Movement_ForwardBackwards,true)=="Левый стик или гребки руками"&&VrPromptLabels.Label(InputActions.Movement_LeftRight,false)=="Left stick or arm strokes"
   &&VrPromptLabels.Label(InputActions.Movement_Jump,false)=="Right stick up"&&VrPromptLabels.Label(InputActions.Movement_Crouch,false)=="Right stick down","swimming prompts wrong");
  LocomotionDriver.Current=null;
  Check(VrPromptLabels.Label(InputActions.Movement_Jump,false)=="Right stick up"&&VrPromptLabels.Label(InputActions.Movement_ForwardBackwards,false)=="Left stick","land prompts changed");
  InteractionDriver.Door=false;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Правый Grip","pickup hint not right Grip");
  InteractionDriver.Grapple=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Левый триггер (кошка в левой руке)","grapple point not left trigger");InteractionDriver.Grapple=false;
  // 0.1.195: the grappling hook in the right hand; the zipline hook at a cable.
  GameUiControls.Current=new GameUiControls();GameUiControls.Current.Items.GrappleTool=true;GameUiControls.Current.Items.ToolSide=1;
  InteractionDriver.Grapple=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Правый триггер (кошка в правой руке)","grapple point in the right hand not the right trigger");InteractionDriver.Grapple=false;
  InteractionDriver.Zip=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,false)=="Wheel: zipline hook, point it at the cable","a zipline without the hook: no hint to take it");
  GameUiControls.Current.Items.ZiplineTool=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Наведите крюк на трос","a zipline with the hook held: no hint to point it");
  InteractionDriver.Zip=false;GameUiControls.Current=null;InteractionDriver.Current=null;
  Check(VrPromptLabels.Label(InputActions.Movement_ForwardBackwards,true)=="Левый стик","walking lost left stick");
  Check(UiLanguage.L("Left stick","de")=="Linker Stick"&&UiLanguage.L("Left stick","fr")=="Stick gauche"&&UiLanguage.L("Left stick","pt-BR")=="Analógico esquerdo"&&UiLanguage.L("Left stick","ja")=="Left stick","prompt translations");
  Check(UiLanguage.L("Turn lockpick","ru")=="Поверните отмычку"&&UiLanguage.L("VR SETTINGS","es")=="AJUSTES VR","lockpick/settings translations");
  GrappleVr.Current=new GrappleVr();
  Check(VrPromptLabels.Label(InputActions.Movement_ForwardBackwards,true)=="Правый стик: раскачка · L3: отцепиться","rope swing is not right stick / L3");
  Check(VrPromptLabels.Label(InputActions.Movement_Jump,false)=="L3 (or right B): let go","rope let go");
  Check(VrPromptLabels.Label(InputActions.GrapplingHook_Retract,true)=="Левый стик вверх"&&VrPromptLabels.Label(InputActions.GrapplingHook_Extend,true)=="Левый стик вниз","rope climb labels");
  // 0.1.197: the hook fired from the right hand: mirrored.
  GrappleVr.Current.RightHanded=true;
  Check(VrPromptLabels.Label(InputActions.Movement_ForwardBackwards,true)=="Левый стик: раскачка · R3: отцепиться"&&VrPromptLabels.Label(InputActions.Movement_Jump,false)=="R3: let go"
   &&VrPromptLabels.Label(InputActions.GrapplingHook_Retract,true)=="Правый стик вверх"&&VrPromptLabels.Label(InputActions.GrapplingHook_Extend,true)=="Правый стик вниз"&&VrPromptLabels.Label(InputActions.GrapplingHook_LengthChangeHold,false)=="Right stick up/down","right-handed rope labels not mirrored");
  foreach(var lang in new[]{"ru","de","fr","es","it","pl","pt"})Check(UiLanguage.L("R3: let go",lang)!="R3: let go"||lang=="es"&&false,"R3 label untranslated in "+lang);
  GrappleVr.Current=null;
  Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,false,host)=="Right trigger","hostage context rewrites unrelated shoot placeholder");
  // 0.1.118: grenade in hand — pin with the left trigger, throw by swinging and letting go.
  WeaponHands.Current=new WeaponHands{Profile="grenade"};
  Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true)=="Левый триггер у гранаты: чека · замах и отпустите Grip","grenade throw prompt (ru): "+VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true));
  WeaponHands.GripMode=WeaponGripMode.Always;
  Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true)=="Левый триггер у гранаты: чека · держите правый триггер, замах, отпустите","grenade throw prompt, always in the hand (ru): "+VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true));
  WeaponHands.GripMode=WeaponGripMode.Hold;
  // 0.1.149: the knife and throwable things: the grip, not the trigger.
  var hands=WeaponHands.Current!;string was=hands.Profile;
  hands.Profile="knife";Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true)=="Grip: держать · замах и отпустить: бросок","knife prompt (ru): "+VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true));
  hands.Profile="prop";Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,false)=="Grip: hold · swing and let go: throw","bottle prompt (en): "+VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,false));
  hands.Profile=was;
  Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,false)!="Right trigger"&&VrPromptLabels.Label(InputActions.Weapon_Reload,true)=="B на правом контроллере","grenade prompt leaks to other actions or stays a button");
  // 0.1.142: the gun in the left hand reloads with the left Y.
  var savedHands=WeaponHands.Current;WeaponHands.Current=new WeaponHands{PrimaryLeft=true};
  Check(VrPromptLabels.Label(InputActions.Weapon_Reload,true)=="Y на левом контроллере","the left hand's gun reload prompt still says the right B");
  WeaponHands.Current=savedHands;
  WeaponHands.Current=null;
  // 0.1.119: mounted gun — both grips to take it, let go of both to leave; M16 launcher on the left trigger.
  WeaponHands.Current=new WeaponHands{Profile="m16"};
  Check(VrPromptLabels.Label(InputActions.Weapon_SecondaryFire,true)=="Левый триггер, держа цевьё","M16 launcher prompt");
  WeaponHands.Current=null;
  var savedDriver=InteractionDriver.Current;InteractionDriver.Current=new InteractionDriver();
  InteractionDriver.Turret=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Оба Grip","mounted gun take prompt");
  MountedGunVr.Current=new MountedGunVr{Mounted=true};Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Отпустите оба Grip","mounted gun leave prompt");
  MountedGunVr.Current=null;InteractionDriver.Turret=false;InteractionDriver.Current=savedDriver;
  Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,false,host)=="Right trigger","grenade prompt stays after the grenade");
  Check(VrPromptLabels.Label(InputActions.Gameplay_ObjectiveVisionMode,false)=="Left X","objective close still stick button");
  Check(VrPromptLabels.Label(InputActions.EquipmentWheel_OpenInventoryWheel,false)=="Hold right A","wheel input");
  Check(VrPromptLabels.Label(InputActions.Weapon_Reload,false)=="Right B","reload input");
  Check(VrPromptLabels.Label(InputActions.Other_Pause,false)=="Left Grip + X","pause input");
  CameraRig.Current=new();var tutorial=new TutorialController{lastCachedTerm="generic_17"};
  I2.Loc.LocalizationManager.Translation="Чтобы взять заложника...";
  Check(VrPromptLabels.TutorialLabel(tutorial,InputActions.Gameplay_Interact,true)=="Удерживать левый Grip","tutorial wrapper ignores translation");
  VrPromptLabels.TutorialLabel(tutorial,InputActions.Weapon_PrimaryFire,true);Check(I2.Loc.LocalizationManager.Reads==1,"translation repeats for every button");
  tutorial.lastCachedTerm="door";I2.Loc.LocalizationManager.Translation="Open the door";
  Check(VrPromptLabels.TutorialLabel(tutorial,InputActions.Gameplay_Interact,false)=="Right Grip","tutorial context sticks after message change");
  var prompt=new ButtonPrompt{actionForPrompt=InputActions.Gameplay_Interact};var hud=new PlayerHUDControl{Children=new[]{prompt}};
  prompt.Parent=new HUDInteractionPrompt{currentPrimaryType=HUDInteractionPrompt.PromptType.HostagePickup};
  VrPromptLabels.RefreshHud(hud);Check(prompt.textRef.text=="Удерживать левый Grip","visible hostage prompt uses right-ray context");
  prompt.Parent.currentPrimaryType=HUDInteractionPrompt.PromptType.General;VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Правый Grip"&&hud.Scans==1,"HUD target transition stale or rescan every frame");
  int writes=prompt.Writes;VrPromptLabels.RefreshHud(hud);Check(prompt.Writes==writes,"unchanged HUD prompt rewritten each frame");
  var interaction=new InteractionDriver();InteractionDriver.Current=interaction;
  prompt.Parent.currentPrimaryType=HUDInteractionPrompt.PromptType.Key;VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Правый Grip + A","key gesture replaces draw chord before key exists");
  interaction.KeyActive=true;interaction.KeyGripProfile="key";VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Поверните ключ","short key gesture missing after draw");
  writes=prompt.Writes;VrPromptLabels.RefreshHud(hud);Check(prompt.Writes==writes,"gesture hint rewritten every frame");
  prompt.Parent.currentPrimaryType=HUDInteractionPrompt.PromptType.Keycard;interaction.KeyGripProfile="card";VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Приложите карту","short card gesture missing after draw");
  I2.Loc.LocalizationManager.CurrentLanguageCode="en";VrPromptLabels.RefreshHud(hud);Check(prompt.textRef.text=="Hold card to reader","English card hint");
  foreach(var lang in new[]{"ru","de","fr","es","it","pl","pt"})Check(UiLanguage.L("Hold card to reader",lang)!="Hold card to reader","card hint untranslated in "+lang);
  prompt.Parent.currentPrimaryType=HUDInteractionPrompt.PromptType.Key;interaction.KeyGripProfile="key";VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Turn key","English key hint");
  prompt.actionForPrompt=InputActions.Weapon_PrimaryFire;VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Right trigger","gesture replaces unrelated button");
  prompt.actionForPrompt=InputActions.Gameplay_Interact;interaction.KeyActive=false;VrPromptLabels.RefreshHud(hud);
  Check(prompt.textRef.text=="Right Grip + A","completion/cancel leaves stale gesture prompt");
  // 0.1.150: a left-hander takes things with the left grip, the menu is on the right grip + A.
  WeaponHands.LeftHanded=true;
  Check(VrPromptLabels.Label(InputActions.Other_Pause,false)=="Right Grip + A"&&VrPromptLabels.Label(InputActions.Gameplay_Interact,true)=="Левый Grip"&&VrPromptLabels.Label(InputActions.Weapon_AimDownSights,false)=="Right Grip: support weapon","left-handed prompts");
  InteractionDriver.Door=true;Check(VrPromptLabels.Label(InputActions.Gameplay_Interact,false)=="Left Grip + X","left-handed door prompt");InteractionDriver.Door=false;
  interaction.KeyActive=false;prompt.Parent.currentPrimaryType=HUDInteractionPrompt.PromptType.Key;VrPromptLabels.RefreshHud(hud);Check(prompt.textRef.text=="Left Grip + X","left-handed key draw chord");
  WeaponHands.LeftHanded=false;VrPromptLabels.RefreshHud(hud);Check(prompt.textRef.text=="Right Grip + A","right-handed key draw chord back");
  WeaponHands.Current=new WeaponHands{PrimaryLeft=true};Check(VrPromptLabels.Label(InputActions.Weapon_PrimaryFire,true)=="Левый триггер","the gun in the left hand fires with the left trigger");WeaponHands.Current=null;
  foreach(var lang in new[]{"de","fr","es","it","pl","pt"})foreach(var t in new[]{"Left Grip","Left trigger","Right Grip: support weapon"})Check(UiLanguage.L(t,lang)!=t,"untranslated "+t+" in "+lang);
  CameraRig.Current=null;prompt.textRef.text="native";VrPromptLabels.Apply(prompt);Check(prompt.textRef.text=="native","non-VR hints overwritten");
  Console.WriteLine("PASS: Russian/English hostage/body tutorial semantics, shared Interact action, hold/release, unrelated placeholders, current controls, translation cache, message/target changes, one HUD discovery and no per-frame text writes. Engine UI simulated.");
 }
}
class Obj{internal T? TryCast<T>()where T:class=>this as T;}
class Text{internal string text="";}
class ButtonPrompt:Obj
{
 internal InputActions actionForPrompt;internal Text textRef=new();internal HUDInteractionPrompt? Parent;internal bool isActiveAndEnabled=>true;internal int Writes;
 internal Obj? GetComponentInParent(Type t)=>Parent;internal void EnableImageRef(bool v){Writes++;}internal void EnableTextRef(bool v){Writes++;}
}
class HUDInteractionPrompt:Obj{internal enum PromptType{General,BodyPickup,HostagePickup,Key,Keycard,Lockpick}internal PromptType currentPrimaryType;}
class PlayerHUDControl:Obj{internal IntPtr Pointer= new(1);internal ButtonPrompt[] Children=Array.Empty<ButtonPrompt>();internal int Scans;internal Obj[] GetComponentsInChildren(Type t,bool active){Scans++;return Children;}}
class TutorialController{internal string lastCachedTerm="";}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace XiiiXR{class LocomotionDriver{internal static LocomotionDriver? Current;internal bool Swimming;}class CameraRig{internal static CameraRig? Current;}class InteractionDriver{internal static InteractionDriver? Current;internal bool BodyTarget=>false;internal bool DoorTarget=>Door;internal static bool Door;internal bool GrappleTarget=>Grapple;internal static bool Grapple;internal bool ZiplineTarget=>Zip;internal static bool Zip;internal bool TurretTarget=>Turret;internal static bool Turret;internal bool KeyActive;internal string KeyGripProfile="key";internal bool KeyLockpick{get;set;}}static class Bootstrap{internal static void Write(string s){}}class GrappleVr{internal static GrappleVr? Current;internal bool OnRope=true;internal bool RightHanded;}class WeaponHands{internal static WeaponHands? Current;internal string Profile="";internal bool PrimaryLeft;internal bool PropThrowable=>Profile=="prop";internal static WeaponGripMode GripMode=WeaponGripMode.Hold;internal static bool LeftHanded;}enum WeaponGripMode{Hold=0,Toggle=1,Always=2}class MountedGunVr{internal static MountedGunVr? Current;internal bool Mounted;}class WheelItems{internal bool GrappleTool,ZiplineTool;internal int ToolSide;}class GameUiControls{internal static GameUiControls? Current;internal WheelItems Items=new();}}
namespace I2.Loc{static class LocalizationManager{internal static string CurrentLanguageCode="ru";internal static string Translation="";internal static int Reads;internal static string GetTranslation(string term,bool a,int b,bool c,bool d,object? e,object? f){Reads++;return Translation;}}}

enum InputActions{GrapplingHook_Extend=602,GrapplingHook_Retract=603,GrapplingHook_LengthChangeHold=604,Camera_LookHorizontal,Camera_LookVertical,EquipmentWheel_ConsumableSlot1,EquipmentWheel_ConsumableSlot2,EquipmentWheel_LeftRightSelection,EquipmentWheel_OpenInventoryWheel,EquipmentWheel_UpDownSelection,Gameplay_HostageTaking,Gameplay_Interact,Gameplay_ObjectiveVisionMode,Items_UseBigMedkit,Items_UseMedkit,Items_UseSmallMedkit,Movement_Crouch,Movement_ForwardBackwards,Movement_Jump,Movement_LeftRight,Movement_Sprint,Other_Pause,UI_ApplyOption,UI_Back,UI_Cancel,UI_DiscardOption,UI_Horizontal,UI_Submit,UI_Vertical,WeaponInventory_AkSlot,WeaponInventory_BazookaSlot,WeaponInventory_CrossbowSlot,WeaponInventory_GrenadeSlot,WeaponInventory_HeavySlot,WeaponInventory_KnifeSlot,WeaponInventory_M16Slot,WeaponInventory_M60Slot,WeaponInventory_NextWeapon,WeaponInventory_PistolSlot,WeaponInventory_PreviousWeapon,WeaponInventory_RevolverSlot,WeaponInventory_RifleSlot,WeaponInventory_ShotgunSlot,WeaponInventory_SniperSlot,WeaponInventory_UnequipWeapon,WeaponInventory_UziSlot,Weapon_AimDownSights,Weapon_Grenade,Weapon_Melee,Weapon_PrimaryFire,Weapon_Reload,Weapon_SecondaryFire,Weapon_TakeDown}
