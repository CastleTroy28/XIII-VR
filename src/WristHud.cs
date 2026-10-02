using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
internal sealed class WristHud : IDisposable
{
    internal static WristHud? Current;
    private readonly CameraRig rig;
    private PlayerHUDControl? hud;
    private HUDHealthArmorComponent? health;
    private HUDObjectiveGroup? objective;
    private TextMeshProUGUI? objectiveSource;
    private GameObject? goals;
    private GloveVisual? leftGlove,rightGlove;
    private TextMeshProUGUI? goalText,performanceText;
    private GameObject? performancePanel;
    private TextMeshProUGUI? goalsTitle,goalsClose;
    private readonly List<(CanvasGroup group,bool owned,float alpha)> hidden=new();
    private float nextFind,nextError,nextPauseFind;
    private readonly SceneFind<PauseMenuControl> pauseMenus=new("pause menu",45,found=>
    {
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PauseMenuControl>()))
        {var pause=obj.TryCast<PauseMenuControl>();if(pause!=null&&pause.gameObject.scene.IsValid())found.Add(pause);}
    });
    private bool disposed,notificationsReported;
    internal bool LeftHandVisible=>leftGlove?.Visible==true;
    internal bool RightHandVisible=>rightGlove?.Visible==true;
    internal WristHud(CameraRig camera) { rig=camera; Current=this; }
    internal void Tick()
    {
        if(disposed) return;
        try
        {
            if(Time.realtimeSinceStartup>=nextFind) { nextFind=Time.realtimeSinceStartup+1; Discover(); }
            // 0.1.146: a left-hander wears the ammunition on the left wrist
            // (the weapon hand) and the health on the right one.
            bool lefty=WeaponHands.LeftHanded;
            var healthGlove=lefty?rightGlove:leftGlove;var ammoGlove=lefty?leftGlove:rightGlove;
            if(leftGlove!=null)leftGlove.AmmoFace=lefty;
            if(rightGlove!=null)rightGlove.AmmoFace=!lefty;
            if(health!=null && healthGlove!=null)
                healthGlove.Readout(health.health?.value==null?"-":health.health.value.text,
                    health.armorBar?.value==null?"-":health.armorBar.value.text,
                    health.health?.bar==null?0:health.health.bar.fillAmount,
                    health.armorBar?.bar==null?0:health.armorBar.bar.fillAmount);
            if(ammoGlove!=null)
            {
                var counter=hud==null || hud.hudCrosshair==null?null:hud.hudCrosshair.currentCounter;
                bool show=counter!=null && counter.container!=null && counter.container.activeInHierarchy
                    && hud!=null && hud.inventory!=null && !hud.inventory.isInTransit && hud.inventory.currentEquipable!=null;
                string magazine=show && counter!.magazineAmmo!=null?counter.magazineAmmo.text:"-";
                string reserve=show && counter!.ammoPool!=null?counter.ammoPool.text:"-";
                if(show && counter!.secondaryAmmo!=null && counter.secondaryAmmo.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(counter.secondaryAmmo.text))
                    reserve=counter.secondaryAmmo.text+" / "+reserve;
                if(WeaponHands.Current?.DualAmmo(out var dualMag,out var dualReserve)==true){magazine=dualMag;reserve=dualReserve;}
                // 0.1.126: a weapon in the left hand too: right / left.
                else if(WeaponHands.Current?.JuggleAmmo(magazine,reserve,out var leftMag,out var leftReserve)==true){magazine=leftMag;reserve=leftReserve;}
                // 0.1.119: M16 — grenades for the underbarrel launcher after the rounds.
                if(WeaponHands.Current?.UnderbarrelGrenades(out int grenades)==true)magazine=magazine+" "+UiLanguage.L("GL")+grenades;
                // 0.1.117: the reload stage in the game's language.
                // 0.1.183: pistols reloaded against the chest (both hands full): their step.
                string stage=UiLanguage.L(WeaponHands.Current?.WatchReloadLabel??"");
                ammoGlove.Readout(magazine,stage.Length>0?stage:reserve);
            }
            if(goalText!=null)
            {
                string text=objectiveSource==null?"":objectiveSource.text;
                if(objective!=null && objective.secondaryObjectiveParent!=null)
                    foreach(var obj in objective.secondaryObjectiveParent.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(),false))
                    {
                        var t=obj.TryCast<TextMeshProUGUI>();
                        if(t!=null && !string.IsNullOrWhiteSpace(t.text)) text+="\n"+t.text;
                    }
                goalText.text=string.IsNullOrWhiteSpace(text)?UiLanguage.L("No current objectives"):text;
                // The game language can change while the panel exists.
                if(goalsTitle!=null)goalsTitle.text=UiLanguage.L("OBJECTIVES");
                if(goalsClose!=null)goalsClose.text=UiLanguage.L("X (left controller) — close");
            }
        }
        catch(Exception ex) { Report(ex); }
    }
    private void Discover()
    {
        var current=GameUiControls.Current?.Hud;
        if(current==null) { if(hud!=null || leftGlove!=null) Clear(); return; }
        if(hud!=current) { Clear(); hud=current; }
        // Hide the individual cards visually; preserve collection/journal logic,
        // queue scripts and the native state of every unrelated HUD component.
        var notification=hud.collectableNotification;
        if(notification!=null && notification.notificationInstance!=null)
        {
            int count=0;
            foreach(var card in notification.notificationInstance)
                if(card!=null){HideOriginal(card.gameObject);count++;}
            if(count>0 && !notificationsReported){notificationsReported=true;Bootstrap.Write("HUD dossier/collectable popups hidden="+count+"; journal logic retained.");}
        }
        if(health==null && hud.hudComponents!=null)
            foreach(var c in hud.hudComponents)
            {var h=c?.TryCast<HUDHealthArmorComponent>();if(h!=null){health=h;break;}}
        if(objective==null && hud.notificationReceivers!=null)
            foreach(var c in hud.notificationReceivers)
            {var h=c?.TryCast<HUDObjectiveGroup>();if(h!=null){objective=h;break;}}
        if(objectiveSource==null && objective!=null && objective.primaryObjectiveTextLocalizeComponent!=null)
            objectiveSource=objective.primaryObjectiveTextLocalizeComponent.GetComponent(Il2CppType.Of<TextMeshProUGUI>())?.TryCast<TextMeshProUGUI>();
        TMP_FontAsset? font=health?.health?.value?.font;
        if(font==null)font=objectiveSource?.font;
        // Skin rebinding must continue even while the next scene loads its HUD font.
        if(leftGlove!=null&&!leftGlove.Valid){leftGlove.Dispose();leftGlove=null;}
        if(rightGlove!=null&&!rightGlove.Valid){rightGlove.Dispose();rightGlove=null;}
        if((leftGlove==null || rightGlove==null))
        {
            try {leftGlove??=new GloveVisual(false);rightGlove??=new GloveVisual(true);}
            catch(Exception ex){Report(ex);return;}
        }
        leftGlove?.BindNative(rig.PlayerRoot);rightGlove?.BindNative(rig.PlayerRoot);
        if(font==null)return;
        if(performancePanel==null)
        {
            performancePanel=Panel("XIII F3 performance",380,160,.001f,true);
            performanceText=Text(performancePanel.transform,font,Vector2.zero,new Vector2(350,145),22);
            performanceText.alignment=TextAlignmentOptions.TopLeft;
        }
        if(goals==null)
        {
            goals=Panel("XIII objectives on left X",800,360,.0009f,true);
            var title=Text(goals.transform,font,new Vector2(0,130),new Vector2(740,50),32);goalsTitle=title;title.text=UiLanguage.L("OBJECTIVES");title.color=new Color(.4f,.85f,1,1);
            goalText=Text(goals.transform,font,new Vector2(0,-16),new Vector2(740,220),30);
            goalText.alignment=TextAlignmentOptions.TopLeft;goalText.enableWordWrapping=true;goalText.overflowMode=TextOverflowModes.Ellipsis;
            goalsClose=Text(goals.transform,font,new Vector2(0,-152),new Vector2(740,25),17);goalsClose.text=UiLanguage.L("X (left controller) — close");
        }
        if(health!=null)HideOriginal(health.gameObject);
        if(objective!=null)HideOriginal(objective.gameObject);
        var weaponHud=hud.hudCrosshair;
        if(weaponHud?.weaponIcon!=null)HideOriginal(weaponHud.weaponIcon.gameObject);
        if(weaponHud!=null)
            foreach(var counter in new[]{weaponHud.standardCounter,weaponHud.throwableCounter,weaponHud.dualWieldCounter,weaponHud.m16Counter})
                if(counter!=null && counter.container!=null)HideOriginal(counter.container);
        PinFor(hud.gameplayUIGroup);PinFor(hud.specialUIGroup);
        if(hud.tutorialController!=null)PinFor(hud.tutorialController.group);
        if(hud.inventoryWheel!=null)PinFor(hud.inventoryWheel.inventoryGroup);
        // 0.1.121: every 5 s, not every second (a scene-wide search).
        // 0.1.162: the pause menus kept between rare searches (every 45 s since 0.1.164, and
        // after a scene change); the kept ones are pinned every 5 s as before.
        bool searched=pauseMenus.Refresh();
        if(searched||Time.realtimeSinceStartup>=nextPauseFind)
        {
            nextPauseFind=Time.realtimeSinceStartup+5;pauseMenus.Prune();
            foreach(var pause in pauseMenus.Items)if(pause!=null)PinFor(pause.m_uiContainer);
        }
    }
    private void HideOriginal(GameObject source)
    {
        if(source==null)return;
        foreach(var h in hidden)if(h.group!=null && h.group.gameObject==source)return;
        var group=source.GetComponent(Il2CppType.Of<CanvasGroup>())?.TryCast<CanvasGroup>();
        bool owned=group==null;
        if(group==null)group=source.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>();
        if(group!=null){hidden.Add((group,owned,group.alpha));group.alpha=0;}
    }
    private void PinFor(GameObject? source)
    {
        if(source==null)return;
        var canvas=source.GetComponentInParent(Il2CppType.Of<Canvas>())?.TryCast<Canvas>();
        if(canvas==null)return;canvas=canvas.rootCanvas;
        rig.PinCanvas(canvas);
    }
    // 0.1.121: once per pre-render pass (it ran on every camera callback,
    // about 7 times a frame, each with its own hand sample).
    private int renderFrame=-1,renderSerial=-1;
    internal void Render()
    {
        if(disposed || !rig.Prepared)return;
        var (frame,serial)=rig.RenderPhase;if(frame==renderFrame&&serial==renderSerial)return;renderFrame=frame;renderSerial=serial;
        // 0.1.164: which part of it costs (it took ~1.1 ms a frame in the log).
        var clock=wristClock;clock.Begin();
        try
        {
            rig.RenderCanvases();clock.Mark("canvases");
            foreach(var h in hidden)if(h.group!=null)h.group.alpha=0;
            if(performancePanel!=null)
            {
                performancePanel.SetActive(FramePerformance.Visible);
                if(FramePerformance.Visible)
                {
                    performancePanel.transform.SetPositionAndRotation(rig.HeadPosition+rig.HeadRotation*new Vector3(-.36f,.20f,1),rig.HeadRotation);
                    performanceText!.text=FramePerformance.Display;
                }
            }
            // 0.1.101: the VR settings are drawn by VrSettingsPage.
            bool quiet=QualityMenu.Open || rig.Scripted || PauseMenuControl.HackGameIsPaused || GameUiControls.Current?.WheelOpen==true;
            bool objectivesOpen=!quiet && GameUiControls.Current?.ObjectivesOpen==true;
            if(goals!=null)
            {
                goals.transform.localScale=Vector3.one*.0009f;
                goals.transform.SetPositionAndRotation(rig.HeadPosition+rig.HeadRotation*new Vector3(0,-.03f,1.2f),rig.HeadRotation);
                goals.SetActive(objectivesOpen);
            }
            clock.Mark("panels");
            if(quiet || !rig.SampleWorldHands(out var l,out var r,out bool validLeft)){HideHands();return;}
            clock.Mark("hand sample");
            PoseGlove(leftGlove,l,validLeft,false);clock.Mark("left hand");PoseGlove(rightGlove,r,true,true);clock.Mark("right hand");
            var leftAttach=leftGlove?.Visible==true?leftGlove.Attachment:null;
            var rightAttach=rightGlove?.Visible==true?rightGlove.Attachment:null;
            // 0.1.195: the grappling hook in either hand; the zipline hook on the cable.
            GrappleVr.Current?.Hands(leftAttach,rightAttach);
            GameUiControls.Current?.Items.Render(rightAttach,leftAttach);clock.Mark("items");
            GameUiControls.Current?.Arms.Render(leftGlove,rightGlove);clock.Mark("arm medkits");
            GrappleVr.Current?.Render(leftAttach,rightAttach);clock.Mark("grapple");
            ZiplineVr.Current?.Render(leftAttach,rightAttach);clock.Mark("zipline");
        }
        catch(Exception ex){HideHands();if(goals!=null)goals.SetActive(false);Report(ex);}
        finally{clock.End();}
    }
    private readonly StepClock wristClock=new("wrist");
    private void PoseGlove(GloveVisual? glove,PoseValue pose,bool valid,bool rightHand)
    {
        if(glove==null)return;
        // 0.1.119: on a mounted gun the game's own arms hold its handles.
        if(!valid||(GripCarry.Current?.HidesHand(rightHand)==true&&GripCarry.Current.HasCarryHand(rightHand)!=true)||MountedGunVr.HidesHands){glove.Hide();return;}
        var input=rightHand?rig.RightControls:rig.LeftControls;
        bool weapon=WeaponHands.Current?.HandHoldsWeapon(rightHand)==true;
        bool item=rightHand!=WeaponHands.LeftHanded&&GameUiControls.Current?.PendingConsumable==true||GameUiControls.Current?.ItemHeldOn(rightHand)==true
            ||GameUiControls.Current?.Items.ArmHeld(rightHand)==true;
        bool reloadItem=WeaponHands.Current?.ReloadHandHolding(rightHand)==true;
        float grip=reloadItem?.9f:item?.7f:weapon || (input.Valid && (input.Held & HandControls.Grip)!=0)?1:0;
        float trigger=item?.75f:input.Valid && (input.Held & HandControls.Trigger)!=0?1:weapon?.45f:0;
        glove.Pose(pose,grip,trigger);
    }
    private static GameObject Panel(string name,float w,float h,float scale,bool background)
    {
        var go=new GameObject(name);go.layer=5;
        var rect=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        var canvas=go.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;
        canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=100;
        if(background)
        {
            var bg=go.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;
            bg.color=new Color(.055f,.12f,.16f,.82f);bg.raycastTarget=false;
        }
        rect.sizeDelta=new Vector2(w,h);go.transform.localScale=Vector3.one*scale;
        go.SetActive(false);return go;
    }
    private static RectTransform Child(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name);go.layer=5;
        var r=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
        r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private static TextMeshProUGUI Text(Transform parent,TMP_FontAsset font,Vector2 position,Vector2 size,float points)
    {
        var r=Child(parent,"Text",position,size);
        var text=r.gameObject.AddComponent(Il2CppType.Of<TextMeshProUGUI>()).TryCast<TextMeshProUGUI>()!;
        text.font=font;text.fontSize=points;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;
        text.raycastTarget=false;text.richText=true;return text;
    }
    internal void PositionCanvases(){rig.RenderCanvases();}
    internal void RecenterCanvases(){rig.RecenterUi();}
    private void Report(Exception ex)
    {if(Time.realtimeSinceStartup<nextError)return;nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("WRIST HUD recovering: "+ex.Message);}
    internal void InvalidatePose(){leftGlove?.InvalidatePose();rightGlove?.InvalidatePose();}
    private void HideHands()
    {leftGlove?.Hide();rightGlove?.Hide();GameUiControls.Current?.Items.Render(null);GameUiControls.Current?.Arms.Hide();}
    private void Clear()
    {
        foreach(var h in hidden)
            try{if(h.group!=null){if(h.owned)UnityEngine.Object.Destroy(h.group);else h.group.alpha=h.alpha;}}catch(Exception ex){Report(ex);}
        hidden.Clear();
        
        if(performancePanel!=null)UnityEngine.Object.Destroy(performancePanel);performancePanel=null;performanceText=null;

        if(goals!=null)UnityEngine.Object.Destroy(goals);
        leftGlove?.Dispose();rightGlove?.Dispose();leftGlove=rightGlove=null;
        goals=null;health=null;objective=null;objectiveSource=null;hud=null;
        goalText=null;
        notificationsReported=false;
    }
    public void Dispose(){if(disposed)return;disposed=true;Clear();if(Current==this)Current=null;}
}
