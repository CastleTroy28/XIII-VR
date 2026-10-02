using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal sealed class GameUiControls : IDisposable
{
    internal static GameUiControls? Current;
    private readonly CameraRig rig;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.menu");
    private PlayerHUDControl? hud;
    private InventoryWheel? wheel;
    private Transform? player;
    private bool owned,armed,selected,disposed,oldDeselectOnZero;
    private bool escapeDown,wheelChordBlocked;
    private readonly MenuChord chord=new();
    private float escapeRelease,nextFind;
    private readonly ObjectivesState objectives=new();
    private readonly EquipmentShortcuts shortcuts=new();
    internal readonly WheelItems Items;
    // 0.1.186: the medkits carried on the forearms' cuts.
    internal readonly ArmMedkits Arms;
    internal string EquipmentNotice=>Items.Notice;
    // A pending item in the RIGHT hand (blocks the right weapon). The
    // grappling hook is held in the left hand and does not.
    // 0.1.186: not a medkit taken from a forearm (held in the hand that took it).
    // 0.1.195: a hand tool (grappling / zipline hook) in either hand does not
    // block the weapon: the weapon goes to the other hand.
    internal bool PendingConsumable=>Items.Pending&&!Items.HandTool&&Items.ArmSide<0;
    internal bool LeftItemHeld=>Items.LeftHeld||ZiplineVr.Current?.HandBusy(false)==true;
    internal bool RightItemHeld=>Items.RightHeld||ZiplineVr.Current?.HandBusy(true)==true;
    internal bool ItemHeldOn(bool right)=>right?RightItemHeld:LeftItemHeld;
    internal bool PointerMenuOpen=>!rig.MovieActive&&(rig.Frontend||WheelOpen||PauseMenuControl.HackGameIsPaused
        ||(!rig.Scripted&&hud!=null&&GameInputManager.IsInputLocked(hud.GetOwner().Id)));
    internal bool ObjectivesOpen=>objectives.Open;
    internal bool WheelOpen => wheel!=null && wheel.IsOpen;
    internal bool BlocksGameplay => PointerMenuOpen || rig.Scripted || ObjectivesOpen || QualityMenu.Open;
    internal PlayerHUDControl? Hud => hud;
    internal GameUiControls(CameraRig camera)
    {
        rig=camera;
        Items=new WheelItems(camera);Arms=new ArmMedkits(camera,Items);
        try
        {
            foreach(string name in new[]{"EvaluateOpenCloseInput","EvaluateDefaultWheelInput"})
                patches.Patch(AccessTools.DeclaredMethod(typeof(InventoryWheel),name),prefix:new HarmonyMethod(typeof(GameUiControls),nameof(AllowNativeInput)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(InventoryWheel),"GetChoosingDirection"),prefix:new HarmonyMethod(typeof(GameUiControls),nameof(ChooseDirection)));
            Current=this;
            Bootstrap.Write("VR MENU ready: hold right A=wheel; left stick=selection; release right A=confirm; left grip+X=Escape (left-handed: right grip+A; either button first); left Y=next weapon; grip+Y disabled; medkits only inside wheel.");
        }
        catch { patches.UnpatchSelf(); throw; }
    }
    internal void Tick()
    {
        if(disposed) return;
        try
        {
            float now=Time.realtimeSinceStartup;
            if(escapeDown && (now>=escapeRelease || !Application.isFocused)) ReleaseEscape();
            if(!(rig.PlayerRoot==player&&hud!=null&&wheel!=null)&&SceneScan.Due(ref nextFind,1)) FindHud();
            if(owned)Items.Hover();
            bool focused=Application.isFocused && rig.HeadTrackingValid;
            bool modifier=rig.LeftControls.Valid && (rig.LeftControls.Held&HandControls.Grip)!=0;
            // 0.1.101: left grip + R3 no longer opens the VR settings; they
            // are an item of the game menu (VrSettingsPage).
            QualityMenu.Tick(focused&&!rig.Scripted,rig.LeftStick,rig.MenuRightControls);
            bool canShowObjectives=!QualityMenu.Open&&!modifier&&focused && rig.RightStick.Valid && !rig.Scripted && !PauseMenuControl.HackGameIsPaused
                && !WheelOpen && hud!=null && !GameInputManager.IsInputLocked(hud.GetOwner().Id);
            bool wasObjectives=objectives.Open;
            objectives.Sample(rig.LeftControls.Valid&&(rig.LeftControls.Held&HandControls.A)!=0,canShowObjectives);
            if(wasObjectives!=objectives.Open)Bootstrap.Write("OBJECTIVES panel open="+objectives.Open);
            var right=rig.RightControls;
            var left=rig.LeftControls;
            // 0.1.150: a left-hander takes things and opens doors with the left
            // grip (+X), so the menu chord moves to the right hand: right grip + A
            // (not while the weapon wheel is open: there the grip takes a weapon).
            bool lefty=WeaponHands.LeftHanded;
            var menuHand=lefty?right:left;
            bool menuModifier=menuHand.Valid&&(menuHand.Held&HandControls.Grip)!=0;
            bool a=menuHand.Valid && (menuHand.Held & HandControls.A)!=0;
            // 0.1.158: in either order (the button a moment first counts too).
            if(chord.Sample(focused&&menuHand.Valid,menuModifier,a,now,out bool late))
            {
                // Left-handed, in the open wheel on a pointed weapon: the grip takes it (below).
                if(lefty&&owned&&selected) { }
                else
                {
                    Items.Clear();rig.DisarmTrigger();
                    // What that same button opened a moment ago closes first:
                    // left-handed the wheel (right A), else the tasks (left X).
                    if(late&&lefty&&owned)Close(false);
                    if(late&&!lefty&&objectives.Open)objectives.Close();
                    if(QualityMenu.Open)QualityMenu.Close();
                    else if(owned) Close(false);
                    else if(objectives.Open)objectives.Close();
                    else OpenGameMenu(lefty,late,now);
                }
            }
            // (The grappling hook stays in the left hand while its placement
            // is tuned in the settings.)
            if(QualityMenu.Open){Close(false);if(!Items.HandTool)Items.Clear();armed=false;shortcuts.Reset();return;}
            bool usable=focused && rig.RightControls.Valid && rig.LeftStick.Valid && !rig.Scripted && !PauseMenuControl.HackGameIsPaused;
            // 0.1.142: with a gun in the left hand its Y reloads it (not the next weapon).
            var command=shortcuts.Sample(usable&&rig.LeftControls.Valid&&WeaponHands.Current?.LeftYReloads!=true,rig.LeftControls.Held);
            bool aHeld=right.Valid&&(right.Held&HandControls.A)!=0;
            if(!aHeld)wheelChordBlocked=false;
            // 0.1.124: the grip on a weapon in the wheel takes it into the hand
            // (held by the grip); on nothing it just closes the wheel.
            if(aHeld&&(right.Held&HandControls.Grip)!=0&&!wheelChordBlocked)
            {
                wheelChordBlocked=true;
                if(owned)
                {
                    bool take=selected&&!Items.Hovered&&WeaponHands.GripMode!=WeaponGripMode.Always;
                    if(take){WeaponHands.Current?.TakenByGrip();Bootstrap.Write("VR MENU wheel: weapon taken with the grip");}
                    Close(take);
                }
            }
            bool hold=aHeld&&!wheelChordBlocked;
            if(!usable) { Close(false); Items.Clear();armed=false; return; }
            if(wheelChordBlocked){armed=false;Arms.Tick(false,null,null);Items.Tick(false);return;}
            bool itemsAllowed=!hold&&!WheelOpen&&!PointerMenuOpen&&hud!=null&&!GameInputManager.IsInputLocked(hud.GetOwner().Id);
            Arms.Tick(itemsAllowed&&!ObjectivesOpen,hud?.inventory,wheel);
            Items.Tick(itemsAllowed);
            if(command!=EquipmentCommand.None && !WheelOpen && !ObjectivesOpen && hud!=null
                && !GameInputManager.IsInputLocked(hud.GetOwner().Id)) Execute(command);
            if(!hold)
            {
                if(owned){bool item=Items.Hovered;Items.Commit();Close(!item&&selected);}
                armed=true; return;
            }
            if(!owned && armed && wheel!=null && !wheel.IsOpen && player!=null && player.gameObject.activeInHierarchy)
            {
                var owner=hud!.GetOwner();
                if(owner.IsInvalid || GameInputManager.IsInputLocked(owner.Id)&&!ObjectivesOpen) return;
                armed=false; selected=false;
                Items.Clear();rig.DisarmTrigger();
                oldDeselectOnZero=wheel.deselectOnZeroCursorOffset;
                objectives.Close();
                wheel.OpenWheel(false); owned=wheel.IsOpen;
                Bootstrap.Write("VR MENU wheel opened="+owned);
            }
            if(owned && wheel!=null && wheel.IsOpen)
            {
                var stick=rig.LeftStick.Value;
                if(stick.LengthSquared()>.3f*.3f&&!Items.Hovered)
                {
                    wheel.cursorOffset=new Vector3(stick.X,stick.Y,0); wheel.deselectOnZeroCursorOffset=false;
                    // HoverOption only updates highlighting. The game's
                    // ChooseSliceBasedOnAngle actually equips a weapon.
                    wheel.HoverOption();
                    selected=wheel.hoveringOption>=0;
                }
            }
        }
        catch(Exception ex)
        {
            Bootstrap.Warn("VR MENU recovering: "+ex.Message);
            ReleaseEscape(); Close(false); hud=null; wheel=null; nextFind=Time.realtimeSinceStartup+1;
        }
    }
    private void Execute(EquipmentCommand command)
    {
        var inventory=hud?.inventory;
        if(inventory==null || inventory.isInTransit || inventory.IsInventoryBlocked)return;
        if(command==EquipmentCommand.NextWeapon){Items.Clear();CycleIcons.Request();long timer=FramePerformance.Begin();try{inventory.SelectNext();}finally{FramePerformance.Scope("native-SelectNext",timer);}Bootstrap.Write("VR EQUIPMENT next weapon");return;}
    }
    internal void ConfirmPointedWeapon(){if(owned&&selected){Close(true);armed=false;}}
    internal bool PointWeapon(int index)
    {
        if(!owned||wheel==null||index<0||wheel.wheelSlices==null||index>=wheel.wheelSlices.Length||!wheel.wheelSlices[index].CanHover())return false;
        // Native HoverOption computes atan2(cursor.y, -cursor.x), nearest index*anglePerSlice.
        float a=index*wheel.anglePerSlice*Mathf.Deg2Rad;
        wheel.cursorOffset=new Vector3(-Mathf.Cos(a),Mathf.Sin(a),0);wheel.deselectOnZeroCursorOffset=false;wheel.HoverOption();
        selected=wheel.hoveringOption==index;return selected;
    }
    internal InventoryWheel? Wheel=>wheel;
    internal void ConfirmPointedItem(){if(owned&&Items.Commit()){Close(false);armed=false;}}
    private void FindHud()
    {
        var root=rig.PlayerRoot;
        if(root==player && hud!=null && wheel!=null) return;
        Close(false); objectives.Close(); shortcuts.Reset();Items.Bind(null);Arms.Reset();player=root; hud=null; wheel=null; armed=false;
        if(root==null) return;
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PlayerHUDControl>()))
        {
            var c=obj.TryCast<PlayerHUDControl>(); if(c==null) continue;
            var owner=c.GetOwner(); if(owner.IsInvalid || !owner.IsPlayer) continue;
            var inv=c.inventory;
            if(inv==null || !inv.transform.IsChildOf(root)) continue;
            hud=c; wheel=c.inventoryWheel;Items.Bind(wheel);
            Bootstrap.Write("VR MENU bound player="+owner.Id+" hud="+c.name+" wheel="+(wheel!=null)); break;
        }
    }
    private static bool AllowNativeInput(InventoryWheel __instance)
    {
        var c=Current;
        return c==null || !c.owned || c.wheel==null || c.wheel.Pointer!=__instance.Pointer;
    }
    private static bool ChooseDirection(InventoryWheel __instance)
    {
        var c=Current;
        if(c==null || !c.owned || c.wheel==null || c.wheel.Pointer!=__instance.Pointer) return true;
        var s=c.rig.LeftStick;
        if(s.Valid && s.Value.LengthSquared()>.09f) __instance.cursorOffset=new Vector3(s.Value.X,s.Value.Y,0);
        __instance.deselectOnZeroCursorOffset=false; return false;
    }
    private void Close(bool confirm)
    {
        if(!owned) return; owned=false;
        try
        {
            if(wheel!=null)
            {
                try { if(wheel.IsOpen) wheel.CloseWheel(confirm,false); }
                finally { if(wheel!=null) wheel.deselectOnZeroCursorOffset=oldDeselectOnZero; }
            }
        }
        catch(Exception ex) { Bootstrap.Warn("VR wheel close: "+ex.Message); }
        Items.ClearHover();rig.DisarmTrigger();selected=false;
    }
    private void OpenGameMenu(bool lefty,bool late,float now)
    {
        string chordName=(lefty?"right grip+A, left-handed":"left grip+X")+(late?", the button first":"");
        if(EscapeKey.Send(true))
        {
            escapeDown=true; escapeRelease=now+.065f;
            Bootstrap.Write("VR MENU Escape pressed ("+chordName+")"+(PauseMenuControl.BlockTogglePause?"; the game does not allow its pause menu right now":""));
            return;
        }
        // 0.1.158: Escape could not be typed for the game: in play its pause
        // menu is opened directly (the reason is written either way).
        string why=EscapeKey.LastRefusal;
        if(rig.Frontend||rig.Scripted||rig.MovieActive){Bootstrap.Warn("VR MENU Escape could not be typed ("+chordName+"): "+why);return;}
        if(GamePause.TryOpen(out string how))Bootstrap.Write("VR MENU Escape could not be typed ("+chordName+": "+why+"); the game's pause menu opened directly");
        else Bootstrap.Warn("VR MENU Escape could not be typed ("+chordName+": "+why+"); the pause menu was not opened: "+how);
    }
    private void ReleaseEscape()
    {
        if(!escapeDown) return; escapeDown=false; EscapeKey.Send(false);
    }
    internal void CancelTransient()
    {
        ReleaseEscape(); Close(false); Items.Clear();objectives.Close();QualityMenu.Close();shortcuts.Reset();armed=false;chord.Reset();
    }
    internal void OnSceneChanged()
    {
        try{CancelTransient();Items.Bind(null);Arms.Reset();}
        finally{hud=null;wheel=null;player=null;owned=false;armed=false;nextFind=0;}
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        CancelTransient();Arms.Reset(); patches.UnpatchSelf(); if(Current==this) Current=null;
    }
}
