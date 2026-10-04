using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
namespace XiiiXR;
internal static class VrPromptLabels
{
    private static readonly Dictionary<string,TutorialHintContext> meanings=new();
    private static PlayerHUDControl? lastHud;
    // 0.1.212: prompts shown as the game's icon alone (text, button and its box hidden).
    private static readonly HashSet<IntPtr> iconOnly=new();
    private static readonly List<ButtonPrompt> hudPrompts=new();
    // 0.1.103: labels follow the game's language (not only ru/en).
    private static string language="en";
    private static string L(bool ru,string english)=>UiLanguage.L(english,ru?"ru":language=="ru"?"en":language);
    private static void ReadLanguage(){try{language=(I2.Loc.LocalizationManager.CurrentLanguageCode??"en").Split('-','_')[0].ToLowerInvariant();}catch{language="en";}}
    internal static string Label(InputActions action,bool ru,TutorialHintContext context=TutorialHintContext.None)=>L(ru,Source(action,context));
    // 0.1.215: the label in English (what is translated, and what names the
    // button drawn on the controller icon).
    internal static string Source(InputActions action,TutorialHintContext context=TutorialHintContext.None)
    {
        if(action==InputActions.Gameplay_Interact||action==InputActions.Gameplay_HostageTaking)
        {
            if(context==TutorialHintContext.ReleaseBody)return "Release left Grip";
            // 0.1.213: the hand pointing at the hostage takes him with its own grip.
            if(context==TutorialHintContext.Hostage&&GripCarry.Current?.HostagePointSide==1)return "Hold right Grip";
            if(context==TutorialHintContext.Hostage||context==TutorialHintContext.Body)return "Hold left Grip";
        }
        // 0.1.88: on the rope, left stick up/down climbs, right stick swings,
        // L3 (or right B) lets go; grapple points are shot with the left trigger.
        bool rope=GrappleVr.Current?.OnRope==true;
        // 0.1.197: fired from the right hand, mirrored: the right stick climbs, the left swings, R3 lets go.
        bool ropeRight=GrappleVr.Current?.RightHanded==true;
        if(rope&&(action==InputActions.Movement_ForwardBackwards||action==InputActions.Movement_LeftRight))
            return (ropeRight?"Left stick: swing · R3: let go":"Right stick: swing · L3: let go");
        if(rope&&action==InputActions.Movement_Jump)return (ropeRight?"R3: let go":"L3 (or right B): let go");
        if(ropeRight&&action==InputActions.GrapplingHook_Extend)return "Right stick down";
        if(ropeRight&&action==InputActions.GrapplingHook_Retract)return "Right stick up";
        if(ropeRight&&action==InputActions.GrapplingHook_LengthChangeHold)return "Right stick up/down";
        // 0.1.110: in water the left stick or arm strokes swim; the right
        // stick turns and rises/dives, as on land.
        if(LocomotionDriver.Current?.Swimming==true&&(action==InputActions.Movement_ForwardBackwards||action==InputActions.Movement_LeftRight))
            return "Left stick or arm strokes";
        // 0.1.195: the hook in either hand; the zipline hook pointed at a cable.
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.GrappleTarget==true)
            return (GameUiControls.Current?.Items.GrappleTool==true&&GameUiControls.Current.Items.ToolSide==1?"Right trigger (hook in right hand)":"Left trigger (hook in left hand)");
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.ZiplineTarget==true)
            return (GameUiControls.Current?.Items.ZiplineTool==true?"Point the zipline hook at the cable":"Wheel: zipline hook, point it at the cable");
        // 0.1.118: a grenade in hand is thrown by hand, not by a button.
        if(WeaponHands.Current?.Profile=="grenade"&&(action==InputActions.Weapon_PrimaryFire||action==InputActions.Weapon_SecondaryFire))
            return (WeaponHands.GripMode==WeaponGripMode.Always?"Left trigger at the grenade: pin · hold right trigger, swing, let go":"Left trigger at the grenade: pin · swing and let go of the grip");
        // 0.1.149: the knife and the game's throwable things: the grip, not the trigger.
        if((WeaponHands.Current?.Profile=="knife"||WeaponHands.Current?.PropThrowable==true)&&(action==InputActions.Weapon_PrimaryFire||action==InputActions.Weapon_SecondaryFire))
            return "Grip: hold · swing and let go: throw";
        // 0.1.119: the mounted gun is taken with both grips and left by letting go.
        if(action==InputActions.Gameplay_Interact&&MountedGunVr.Current?.Mounted==true)return "Release both Grips";
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.TurretTarget==true)return "Both Grips";
        // 0.1.119: the M16's grenade launcher.
        if(WeaponHands.Current?.Profile=="m16"&&action==InputActions.Weapon_SecondaryFire)
            return "Left trigger while holding the fore-end";
        return action switch
    {
        InputActions.GrapplingHook_Extend=>"Left stick down",
        InputActions.GrapplingHook_Retract=>"Left stick up",
        InputActions.GrapplingHook_LengthChangeHold=>"Left stick up/down",
        InputActions.Movement_ForwardBackwards or InputActions.Movement_LeftRight=>"Left stick",
        InputActions.Movement_Sprint=>"Left stick click",
        InputActions.Movement_Jump=>"Right stick up",
        InputActions.Movement_Crouch=>"Right stick down",
        InputActions.Camera_LookHorizontal=>"Right stick",
        InputActions.Camera_LookVertical=>"Head movement",
        // 0.1.215: a key, card or lockpick lock: the stick click of the hand
        // that takes things; a thing to take: either grip takes it.
        InputActions.Gameplay_Interact=>InteractionDriver.Current?.BodyTarget==true?"Hold left Grip"
            :InteractionDriver.Current?.LockTarget==true?LockClick
            :InteractionDriver.Current?.DoorTarget==true?(WeaponHands.LeftHanded?"Left Grip + X":"Right Grip + A"):"Left/Right Grip",
        InputActions.Gameplay_HostageTaking=>"Hold left Grip",
        InputActions.Gameplay_ObjectiveVisionMode=>"Left X",
        InputActions.Weapon_PrimaryFire=>(WeaponHands.Current?.PrimaryLeft==true?"Left trigger":"Right trigger"),
        InputActions.Weapon_SecondaryFire=>"Right stick click",
        // 0.1.142: the gun in the left hand reloads with the left Y.
        InputActions.Weapon_Reload=>(WeaponHands.Current?.PrimaryLeft==true?"Left Y":"Right B"),
        InputActions.Weapon_AimDownSights=>(WeaponHands.LeftHanded?"Right Grip: support weapon":"Left Grip: support weapon"),
        InputActions.WeaponInventory_PistolSlot or InputActions.WeaponInventory_ShotgunSlot or InputActions.WeaponInventory_RifleSlot
        or InputActions.WeaponInventory_HeavySlot or InputActions.WeaponInventory_KnifeSlot or InputActions.WeaponInventory_GrenadeSlot
        or InputActions.WeaponInventory_AkSlot or InputActions.WeaponInventory_RevolverSlot or InputActions.WeaponInventory_UziSlot
        or InputActions.WeaponInventory_M16Slot or InputActions.WeaponInventory_SniperSlot or InputActions.WeaponInventory_CrossbowSlot
        or InputActions.WeaponInventory_M60Slot or InputActions.WeaponInventory_BazookaSlot or InputActions.Weapon_Grenade
        or InputActions.EquipmentWheel_ConsumableSlot1 or InputActions.EquipmentWheel_ConsumableSlot2=>"A: wheel, left stick",
        InputActions.Weapon_Melee=>"Grip + swing",
        // 0.1.216: a hard punch in his back knocks him out (PunchDriver.BackKnockout).
        InputActions.Weapon_TakeDown=>"Fist in the back, swing hard",
        InputActions.WeaponInventory_NextWeapon=>"Left Y",
        InputActions.WeaponInventory_PreviousWeapon or InputActions.WeaponInventory_UnequipWeapon=>"Right A: wheel, left stick",
        InputActions.EquipmentWheel_OpenInventoryWheel=>"Hold right A",
        InputActions.EquipmentWheel_UpDownSelection or InputActions.EquipmentWheel_LeftRightSelection or InputActions.UI_Vertical or InputActions.UI_Horizontal=>"Left stick",
        InputActions.Items_UseSmallMedkit or InputActions.Items_UseBigMedkit or InputActions.Items_UseMedkit=>"A: wheel → medkit → trigger",
        InputActions.UI_Submit or InputActions.UI_ApplyOption=>"Right A",
        InputActions.UI_Back=>"Right B",
        // 0.1.150: a left-hander's left grip + X opens doors; the menu is on the right grip + A.
        // 0.1.214: the weapon wheel tutorial's hint: right A ends it (WheelTutorial).
        InputActions.UI_Cancel or InputActions.UI_DiscardOption or InputActions.Other_Pause=>(WheelTutorial.LabelsA?"Right A":WeaponHands.LeftHanded?"Right Grip + A":"Left Grip + X"),
        _=>""
    };
    }
    internal static string LockClick=>WeaponHands.LeftHanded?"Left stick click":"Right stick click";
    internal static string TutorialLabel(TutorialController tutorial,InputActions action,bool ru)
    {
        string term=tutorial.lastCachedTerm??"";
        ReadLanguage();
        string key=language+"\n"+term;
        if(!meanings.TryGetValue(key,out var context))
        {
            string translation=term.Length==0?"":I2.Loc.LocalizationManager.GetTranslation(term,true,0,true,false,null,null)??"";
            context=TutorialHintMeaning.Detect(term,translation);
            if(meanings.Count>=128)meanings.Clear();meanings[key]=context;
            Bootstrap.Write("VR TUTORIAL term="+term+" context="+context);
        }
        return Label(action,ru,context);
    }
    internal static void RefreshHud(PlayerHUDControl hud)
    {
        if(CameraRig.Current==null||hud==null)return;
        if(lastHud==null||lastHud.Pointer!=hud.Pointer)
        {
            lastHud=hud;hudPrompts.Clear();
            foreach(var component in hud.GetComponentsInChildren(Il2CppType.Of<ButtonPrompt>(),true))
            {var prompt=component.TryCast<ButtonPrompt>();if(prompt!=null)hudPrompts.Add(prompt);}
        }
        foreach(var prompt in hudPrompts)if(prompt!=null&&prompt.isActiveAndEnabled)Apply(prompt,false);
    }
    // A door, cabinet, locker, hatch or vent (not a hook point, zipline or gun),
    // or a thing the game breaks on Interact.
    internal static bool IconOnly(HUDInteractionPrompt.PromptType? type,InteractionDriver? interaction)
    {
        if(type==HUDInteractionPrompt.PromptType.Destructible)return true;
        if(type!=HUDInteractionPrompt.PromptType.General||interaction==null)return false;
        return interaction.DoorTarget&&!interaction.BodyTarget&&!interaction.GrappleTarget&&!interaction.ZiplineTarget&&!interaction.TurretTarget;
    }
    internal static void Apply(ButtonPrompt prompt,bool force=true)
    {
        if(CameraRig.Current==null||prompt==null||prompt.textRef==null)return;
        ReadLanguage();bool ru=language=="ru";
        var group=prompt.GetComponentInParent(Il2CppType.Of<HUDInteractionPrompt>())?.TryCast<HUDInteractionPrompt>();
        var context=group?.currentPrimaryType==HUDInteractionPrompt.PromptType.HostagePickup?TutorialHintContext.Hostage:
            group?.currentPrimaryType==HUDInteractionPrompt.PromptType.BodyPickup?TutorialHintContext.Body:TutorialHintContext.None;
        var interaction=InteractionDriver.Current;
        bool lockType=group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Key||group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Keycard
            ||group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Lockpick;
        // 0.1.212 (testers pressed the chord the prompt showed and never tried
        // their hands): opening a door, a cabinet or a hatch and breaking a
        // grate, a panel or glass show only the game's icon - no text, no
        // button. The hand does these (Grip + A still works). Key, card and
        // lockpick locks keep their text.
        if(prompt.actionForPrompt==InputActions.Gameplay_Interact&&!lockType&&IconOnly(group?.currentPrimaryType,interaction))
        {
            if(force||iconOnly.Add(prompt.Pointer))
            {
                iconOnly.Add(prompt.Pointer);
                try{prompt.EnableTextRef(false);prompt.EnableImageRef(false);prompt.EnablePCBackground(false);}
                catch(Exception ex){Bootstrap.Warn("PROMPT icon only: "+ex.Message);}
                // The game's own icon on doors and breakable things (not the controller).
                PromptIcons.TypeIcon(group,null);PromptIcons.Show(prompt,null);
            }
            return;
        }
        // Shown again: the game sets the prompt up anew (its hint calls back here).
        if(iconOnly.Remove(prompt.Pointer))
        {
            try{prompt.SetInputHint();return;}
            catch(Exception ex){Bootstrap.Warn("PROMPT shown again: "+ex.Message);}
        }
        var english=Source(prompt.actionForPrompt,context);if(english.Length==0)return;
        // Once the item is in hand the next action is a gesture, not another
        // button press. Keep the normal chord before drawing and after cancel.
        // 0.1.220: a lockpick door's prompt is not always a lock prompt (it
        // kept the stick click with the pick out): the lock aimed at is enough.
        if(prompt.actionForPrompt==InputActions.Gameplay_Interact&&interaction?.KeyActive==true&&(lockType||interaction.LockTarget))
            english=interaction.KeyGripProfile=="card"?"Hold card to reader":interaction.KeyPicking?"Hold lockpick in lock":interaction.KeyLockpick?"Turn lockpick":"Turn key";
        // 0.1.215: key/card/lockpick locks: the stick click takes the item out
        // (R3; L3 left-handed; Grip + A still does too).
        else if(prompt.actionForPrompt==InputActions.Gameplay_Interact&&lockType)
            english=LockClick;
        var label=L(ru,english);
        // 0.1.215: the controller with the button lit, in the game's
        // interaction hints (not the menus' button rows): in place of the
        // hand icon on a pick-up, else right of the text.
        var icon=group!=null?ControllerIconMath.For(english):null;
        bool hand=group!=null&&group.currentPrimaryType==HUDInteractionPrompt.PromptType.General;
        PromptIcons.TypeIcon(group,hand?icon:null);
        bool changed=prompt.textRef.text!=label;
        if(force||changed){prompt.EnableImageRef(false);prompt.EnableTextRef(true);if(changed)prompt.textRef.text=label;}
        PromptIcons.Show(prompt,hand?null:icon);
    }
}
