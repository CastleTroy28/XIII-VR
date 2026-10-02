using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
namespace XiiiXR;
internal static class VrPromptLabels
{
    private static readonly Dictionary<string,TutorialHintContext> meanings=new();
    private static PlayerHUDControl? lastHud;
    private static readonly List<ButtonPrompt> hudPrompts=new();
    // 0.1.103: labels follow the game's language (not only ru/en).
    private static string language="en";
    private static string L(bool ru,string english)=>UiLanguage.L(english,ru?"ru":language=="ru"?"en":language);
    private static void ReadLanguage(){try{language=(I2.Loc.LocalizationManager.CurrentLanguageCode??"en").Split('-','_')[0].ToLowerInvariant();}catch{language="en";}}
    internal static string Label(InputActions action,bool ru,TutorialHintContext context=TutorialHintContext.None)
    {
        if(action==InputActions.Gameplay_Interact||action==InputActions.Gameplay_HostageTaking)
        {
            if(context==TutorialHintContext.ReleaseBody)return L(ru,"Release left Grip");
            if(context==TutorialHintContext.Hostage||context==TutorialHintContext.Body)return L(ru,"Hold left Grip");
        }
        // 0.1.88: on the rope, left stick up/down climbs, right stick swings,
        // L3 (or right B) lets go; grapple points are shot with the left trigger.
        bool rope=GrappleVr.Current?.OnRope==true;
        // 0.1.197: fired from the right hand, mirrored: the right stick climbs, the left swings, R3 lets go.
        bool ropeRight=GrappleVr.Current?.RightHanded==true;
        if(rope&&(action==InputActions.Movement_ForwardBackwards||action==InputActions.Movement_LeftRight))
            return L(ru,ropeRight?"Left stick: swing · R3: let go":"Right stick: swing · L3: let go");
        if(rope&&action==InputActions.Movement_Jump)return L(ru,ropeRight?"R3: let go":"L3 (or right B): let go");
        if(ropeRight&&action==InputActions.GrapplingHook_Extend)return L(ru,"Right stick down");
        if(ropeRight&&action==InputActions.GrapplingHook_Retract)return L(ru,"Right stick up");
        if(ropeRight&&action==InputActions.GrapplingHook_LengthChangeHold)return L(ru,"Right stick up/down");
        // 0.1.110: in water the left stick or arm strokes swim; the right
        // stick turns and rises/dives, as on land.
        if(LocomotionDriver.Current?.Swimming==true&&(action==InputActions.Movement_ForwardBackwards||action==InputActions.Movement_LeftRight))
            return L(ru,"Left stick or arm strokes");
        // 0.1.195: the hook in either hand; the zipline hook pointed at a cable.
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.GrappleTarget==true)
            return L(ru,GameUiControls.Current?.Items.GrappleTool==true&&GameUiControls.Current.Items.ToolSide==1?"Right trigger (hook in right hand)":"Left trigger (hook in left hand)");
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.ZiplineTarget==true)
            return L(ru,GameUiControls.Current?.Items.ZiplineTool==true?"Point the zipline hook at the cable":"Wheel: zipline hook, point it at the cable");
        // 0.1.118: a grenade in hand is thrown by hand, not by a button.
        if(WeaponHands.Current?.Profile=="grenade"&&(action==InputActions.Weapon_PrimaryFire||action==InputActions.Weapon_SecondaryFire))
            return L(ru,WeaponHands.GripMode==WeaponGripMode.Always?"Left trigger at the grenade: pin · hold right trigger, swing, let go":"Left trigger at the grenade: pin · swing and let go of the grip");
        // 0.1.149: the knife and the game's throwable things: the grip, not the trigger.
        if((WeaponHands.Current?.Profile=="knife"||WeaponHands.Current?.PropThrowable==true)&&(action==InputActions.Weapon_PrimaryFire||action==InputActions.Weapon_SecondaryFire))
            return L(ru,"Grip: hold · swing and let go: throw");
        // 0.1.119: the mounted gun is taken with both grips and left by letting go.
        if(action==InputActions.Gameplay_Interact&&MountedGunVr.Current?.Mounted==true)return L(ru,"Release both Grips");
        if(action==InputActions.Gameplay_Interact&&InteractionDriver.Current?.TurretTarget==true)return L(ru,"Both Grips");
        // 0.1.119: the M16's grenade launcher.
        if(WeaponHands.Current?.Profile=="m16"&&action==InputActions.Weapon_SecondaryFire)
            return L(ru,"Left trigger while holding the fore-end");
        return action switch
    {
        InputActions.GrapplingHook_Extend=>L(ru,"Left stick down"),
        InputActions.GrapplingHook_Retract=>L(ru,"Left stick up"),
        InputActions.GrapplingHook_LengthChangeHold=>L(ru,"Left stick up/down"),
        InputActions.Movement_ForwardBackwards or InputActions.Movement_LeftRight=>L(ru,"Left stick"),
        InputActions.Movement_Sprint=>L(ru,"Left stick click"),
        InputActions.Movement_Jump=>L(ru,"Right stick up"),
        InputActions.Movement_Crouch=>L(ru,"Right stick down"),
        InputActions.Camera_LookHorizontal=>L(ru,"Right stick"),
        InputActions.Camera_LookVertical=>L(ru,"Head movement"),
        InputActions.Gameplay_Interact=>InteractionDriver.Current?.BodyTarget==true?(L(ru,"Hold left Grip"))
            :InteractionDriver.Current?.DoorTarget==true?L(ru,WeaponHands.LeftHanded?"Left Grip + X":"Right Grip + A"):L(ru,WeaponHands.LeftHanded?"Left Grip":"Right Grip"),
        InputActions.Gameplay_HostageTaking=>L(ru,"Hold left Grip"),
        InputActions.Gameplay_ObjectiveVisionMode=>L(ru,"Left X"),
        InputActions.Weapon_PrimaryFire=>L(ru,WeaponHands.Current?.PrimaryLeft==true?"Left trigger":"Right trigger"),
        InputActions.Weapon_SecondaryFire=>L(ru,"Right stick click"),
        // 0.1.142: the gun in the left hand reloads with the left Y.
        InputActions.Weapon_Reload=>L(ru,WeaponHands.Current?.PrimaryLeft==true?"Left Y":"Right B"),
        InputActions.Weapon_AimDownSights=>L(ru,WeaponHands.LeftHanded?"Right Grip: support weapon":"Left Grip: support weapon"),
        InputActions.WeaponInventory_PistolSlot or InputActions.WeaponInventory_ShotgunSlot or InputActions.WeaponInventory_RifleSlot
        or InputActions.WeaponInventory_HeavySlot or InputActions.WeaponInventory_KnifeSlot or InputActions.WeaponInventory_GrenadeSlot
        or InputActions.WeaponInventory_AkSlot or InputActions.WeaponInventory_RevolverSlot or InputActions.WeaponInventory_UziSlot
        or InputActions.WeaponInventory_M16Slot or InputActions.WeaponInventory_SniperSlot or InputActions.WeaponInventory_CrossbowSlot
        or InputActions.WeaponInventory_M60Slot or InputActions.WeaponInventory_BazookaSlot or InputActions.Weapon_Grenade
        or InputActions.EquipmentWheel_ConsumableSlot1 or InputActions.EquipmentWheel_ConsumableSlot2=>L(ru,"A: wheel, left stick"),
        InputActions.Weapon_Melee or InputActions.Weapon_TakeDown=>L(ru,"Grip + swing"),
        InputActions.WeaponInventory_NextWeapon=>L(ru,"Left Y"),
        InputActions.WeaponInventory_PreviousWeapon or InputActions.WeaponInventory_UnequipWeapon=>L(ru,"Right A: wheel, left stick"),
        InputActions.EquipmentWheel_OpenInventoryWheel=>L(ru,"Hold right A"),
        InputActions.EquipmentWheel_UpDownSelection or InputActions.EquipmentWheel_LeftRightSelection or InputActions.UI_Vertical or InputActions.UI_Horizontal=>L(ru,"Left stick"),
        InputActions.Items_UseSmallMedkit or InputActions.Items_UseBigMedkit or InputActions.Items_UseMedkit=>L(ru,"A: wheel → medkit → trigger"),
        InputActions.UI_Submit or InputActions.UI_ApplyOption=>L(ru,"Right A"),
        InputActions.UI_Back=>L(ru,"Right B"),
        // 0.1.150: a left-hander's left grip + X opens doors; the menu is on the right grip + A.
        InputActions.UI_Cancel or InputActions.UI_DiscardOption or InputActions.Other_Pause=>L(ru,WeaponHands.LeftHanded?"Right Grip + A":"Left Grip + X"),
        _=>""
    };
    }
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
    internal static void Apply(ButtonPrompt prompt,bool force=true)
    {
        if(CameraRig.Current==null||prompt==null||prompt.textRef==null)return;
        ReadLanguage();bool ru=language=="ru";
        var group=prompt.GetComponentInParent(Il2CppType.Of<HUDInteractionPrompt>())?.TryCast<HUDInteractionPrompt>();
        var context=group?.currentPrimaryType==HUDInteractionPrompt.PromptType.HostagePickup?TutorialHintContext.Hostage:
            group?.currentPrimaryType==HUDInteractionPrompt.PromptType.BodyPickup?TutorialHintContext.Body:TutorialHintContext.None;
        var label=Label(prompt.actionForPrompt,ru,context);if(label.Length==0)return;
        // Once the item is in hand the next action is a gesture, not another
        // button press. Keep the normal chord before drawing and after cancel.
        var interaction=InteractionDriver.Current;
        bool lockType=group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Key||group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Keycard
            ||group?.currentPrimaryType==HUDInteractionPrompt.PromptType.Lockpick;
        if(prompt.actionForPrompt==InputActions.Gameplay_Interact&&interaction?.KeyActive==true&&lockType)
            label=interaction.KeyGripProfile=="card"?L(ru,"Hold card to reader"):interaction.KeyLockpick?L(ru,"Turn lockpick"):L(ru,"Turn key");
        // Key/card/lockpick locks keep the deliberate chord to take the item out.
        else if(prompt.actionForPrompt==InputActions.Gameplay_Interact&&lockType)
            label=L(ru,WeaponHands.LeftHanded?"Left Grip + X":"Right Grip + A");
        if(!force&&prompt.textRef.text==label)return;
        prompt.EnableImageRef(false);prompt.EnableTextRef(true);if(prompt.textRef.text!=label)prompt.textRef.text=label;
    }
}
