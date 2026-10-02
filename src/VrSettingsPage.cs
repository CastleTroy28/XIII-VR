using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace XiiiXR;
// 0.1.101: "VR SETTINGS" is an item of the game's own main menu and pause
// menu (a copy of the native button placed above Quit). It opens the VR
// settings page (the rows of QualityMenu) as a panel in front of the player,
// driven by the right-controller ray or the left stick. The old left grip + R3
// chord is gone.
internal sealed class VrSettingsPage:IDisposable
{
    internal static VrSettingsPage? Current;
    private const float Width=900,RowHeight=45,TitleHeight=80,FooterHeight=250,Arrow=130;
    private static float Height=>TitleHeight+RowHeight*QualityMenu.RowCount+FooterHeight;
    private readonly CameraRig rig;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.settings");
    private sealed class Entry{internal EventTriggerButton? Button;internal GameObject? Template;internal string Where="";internal bool Failed;}
    private readonly Dictionary<int,Entry> entries=new();
    private readonly HashSet<IntPtr> ours=new();
    private float nextScan,nextError,releaseAt,nextMenuFind;
    private GameObject? panel;private TextMeshProUGUI? title,footer;private Image? highlight;private TMP_FontAsset? font;
    private readonly TextMeshProUGUI?[] rows=new TextMeshProUGUI?[QualityMenu.RowCount];
    private bool placed;
    private readonly MenuBeam beam=new();
    private readonly UiPointerState pointer=new();
    private string openedFrom="";
    private MenuPage? blockedPage;private bool blockedPrevious;private EventSystem? blockedSystem;private bool navigationPrevious;private bool blocking;
    internal VrSettingsPage(CameraRig owner)
    {
        rig=owner;Current=this;
        try
        {
            foreach(string method in new[]{"ButtonPress","ButtonPressEnd","OnPointerDown","OnPointerUp"})
            {
                var target=AccessTools.DeclaredMethod(typeof(EventTriggerButton),method);
                if(target!=null)patches.Patch(target,prefix:new HarmonyMethod(typeof(VrSettingsPage),nameof(Press)));
            }
            Bootstrap.Write("VR SETTINGS menu item ready (main menu and pause menu, above Quit); left grip+R3 removed");
        }
        catch{patches.UnpatchSelf();throw;}
    }
    // Our copied button never runs the copied native action (its persistent
    // listeners are also switched off); it opens the VR page instead.
    private static bool Press(EventTriggerButton __instance)
    {
        var c=Current;if(c==null||__instance==null||!c.ours.Contains(__instance.Pointer))return true;
        try{c.OpenFrom(__instance);}catch(Exception ex){Bootstrap.Warn("VR SETTINGS open: "+ex.Message);}
        return false;
    }
    private void OpenFrom(EventTriggerButton button)
    {
        if(QualityMenu.Open)return;
        foreach(var e in entries.Values)if(e.Button!=null&&e.Button.Pointer==button.Pointer)openedFrom=e.Where;
        QualityMenu.Show();
        Block(button.GetComponentInParent(Il2CppType.Of<MenuPage>())?.TryCast<MenuPage>());
        Bootstrap.Write("VR SETTINGS opened from "+(openedFrom==""?"menu":openedFrom)+" menu");
    }
    // Called every frame before the menu input (GameUiControls) runs.
    internal void Tick()
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            if(now>=nextScan){nextScan=now+.5f;Scan();}
            if(QualityMenu.Open&&!blocking&&openedFrom=="")Block(null);
            if(QualityMenu.Open)
            {
                if(openedFrom=="pause"&&!PauseMenuControl.HackGameIsPaused||openedFrom=="main"&&!rig.Frontend){QualityMenu.Close();Bootstrap.Write("VR SETTINGS closed with its menu");}
                else if(rig.MenuRightControls.Valid&&(rig.MenuRightControls.Down&HandControls.B)!=0)QualityMenu.Close();
            }
            if(!QualityMenu.Open)
            {
                QualityMenu.PointerOwnsTrigger=false;openedFrom="";pointer.Sample(false,false,0);beam.Hide();
                // Give the native menu its input back only after the buttons
                // that closed this page are released (B would go "back").
                if(blocking)
                {
                    bool held=rig.MenuRightControls.Valid&&(rig.MenuRightControls.Held&(HandControls.B|HandControls.A|HandControls.Trigger))!=0
                        ||rig.MenuLeftControls.Valid&&(rig.MenuLeftControls.Held&HandControls.Trigger)!=0;
                    if(held)releaseAt=now+.2f;else if(now>=releaseAt)Unblock();
                }
                return;
            }
            releaseAt=now+.2f;
            Point();
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("VR SETTINGS page: "+ex.Message);}}
    }
    private void Point()
    {
        QualityMenu.PointerOwnsTrigger=false;
        if(panel==null||!placed||!panel.activeSelf||!rig.SamplePointerHand(out var hand)){pointer.Sample(false,false,0);beam.Hide();return;}
        var origin=CameraRig.UnityPosition(hand);var direction=rig.PointerRotation(hand)*Vector3.forward;
        var t=panel.transform;var normal=t.forward;float facing=Vector3.Dot(direction,normal);
        int target=0,row=-1,side=0;Vector3 end=origin+direction*1.5f;
        if(facing>.05f)
        {
            float distance=Vector3.Dot(t.position-origin,normal)/facing;
            if(distance>0&&distance<5)
            {
                var hit=origin+direction*distance;var local=t.InverseTransformPoint(hit);
                if(Math.Abs(local.x)<=Width*.5f&&Math.Abs(local.y)<=Height*.5f)
                {
                    end=hit;QualityMenu.PointerOwnsTrigger=true;
                    float fromTop=Height*.5f-local.y-TitleHeight;
                    if(fromTop>=0){row=(int)(fromTop/RowHeight);if(row>=QualityMenu.RowCount)row=-1;}
                    if(row>=0)
                    {
                        side=local.x<-Width*.5f+Arrow?-1:local.x>Width*.5f-Arrow?1:0;
                        if(!QualityMenu.Adjustable(row))side=0;
                        target=(row+1)*4+side+2;QualityMenu.Hover(row);
                    }
                }
            }
        }
        var input=rig.MenuPointerControls;bool held=input.Valid&&(input.Held&HandControls.Trigger)!=0;
        var step=pointer.Sample(true,held,target);
        beam.Show(origin,end,target!=0);
        if(step.Click&&row>=0){QualityMenu.Click(row,side);rig.PunchHaptics(!rig.PointerLeft);rig.DisarmMenuTriggers();}
    }
    // Stop the native page from reacting to the same stick/A/B while the VR
    // page is on top of it.
    private void Block(MenuPage? page)
    {
        if(blocking)return;blocking=true;
        blockedPage=page;if(page!=null){blockedPrevious=page.m_blockInput;page.m_blockInput=true;}
        blockedSystem=EventSystem.current;if(blockedSystem!=null){navigationPrevious=blockedSystem.sendNavigationEvents;blockedSystem.sendNavigationEvents=false;}
    }
    private void Unblock()
    {
        if(!blocking)return;blocking=false;
        try{if(blockedPage!=null)blockedPage.m_blockInput=blockedPrevious;}catch{}
        try{if(blockedSystem!=null)blockedSystem.sendNavigationEvents=navigationPrevious;}catch{}
        blockedPage=null;blockedSystem=null;
    }
    private void Scan()
    {
        foreach(var key in new List<int>(entries.Keys))
        {
            var e=entries[key];if(e.Failed)continue;
            if(e.Button==null||e.Template==null){if(e.Button!=null)UnityEngine.Object.Destroy(e.Button.gameObject);entries.Remove(key);continue;}
            // Follow the native neighbour: shown/hidden and faded with it.
            if(e.Button.gameObject.activeSelf!=e.Template.activeSelf)e.Button.gameObject.SetActive(e.Template.activeSelf);
            Label(e.Button.gameObject); // follows the game language
            var a=e.Template.GetComponent(Il2CppType.Of<CanvasGroup>())?.TryCast<CanvasGroup>();
            var b=e.Button.GetComponent(Il2CppType.Of<CanvasGroup>())?.TryCast<CanvasGroup>();
            if(a!=null&&b!=null){b.alpha=a.alpha;b.interactable=a.interactable;b.blocksRaycasts=a.blocksRaycasts;}
        }
        ours.Clear();foreach(var e in entries.Values)if(e.Button!=null)ours.Add(e.Button.Pointer);
        // 0.1.121: the main and pause menus are searched only while one can
        // be on screen (a visible Quit button is required anyway).
        if(!(rig.Frontend||PauseMenuControl.HackGameIsPaused)||!SceneScan.Due(ref nextMenuFind,.5f))return;
        long scanStart=SceneScan.Begin();
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MainMenuControl>()))
        {var menu=obj.TryCast<MainMenuControl>();if(menu!=null&&menu.gameObject.scene.IsValid())Ensure(menu.quitButton,"main");}
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<PauseMenuControl>()))
        {var pause=obj.TryCast<PauseMenuControl>();if(pause!=null&&pause.gameObject.scene.IsValid())Ensure(pause.quitButton,"pause");}
        SceneScan.End("menus",scanStart);
    }
    private void Ensure(GameObject? quit,string where)
    {
        if(quit==null)return;var parent=quit.transform.parent;if(parent==null)return;
        int id=parent.GetInstanceID();if(entries.ContainsKey(id))return;
        if(!quit.activeInHierarchy)return; // copy a visible, settled button
        // Template: the nearest visible native button above Quit (same look).
        GameObject? template=null;int quitIndex=quit.transform.GetSiblingIndex();
        for(int i=quitIndex-1;i>=0&&template==null;i--)
        {
            var sibling=parent.GetChild(i).gameObject;
            if(sibling.activeSelf&&sibling.GetComponent(Il2CppType.Of<EventTriggerButton>())!=null)template=sibling;
        }
        template??=quit.GetComponent(Il2CppType.Of<EventTriggerButton>())!=null?quit:null;
        if(template==null){Bootstrap.Warn("VR SETTINGS no native button to copy in the "+where+" menu");entries[id]=new Entry{Where=where,Failed=true};return;}
        var copy=UnityEngine.Object.Instantiate(template,parent,false);copy.name="XIII VR settings button";
        var button=copy.GetComponent(Il2CppType.Of<EventTriggerButton>()).TryCast<EventTriggerButton>()!;
        foreach(var e in new UnityEngine.Events.UnityEventBase?[]{button.OnButtonPress,button.OnButtonPressEnd,button.TriggerEvents})
            if(e!=null)for(int i=0;i<e.GetPersistentEventCount();i++)e.SetPersistentListenerState(i,UnityEngine.Events.UnityEventCallState.Off);
        foreach(var loc in copy.GetComponentsInChildren(Il2CppType.Of<I2.Loc.Localize>(),true))UnityEngine.Object.DestroyImmediate(loc);
        foreach(var component in copy.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(),true))
        {var text=component.TryCast<TextMeshProUGUI>();if(text!=null)font??=text.font;}
        Label(copy);
        bool layout=parent.GetComponent(Il2CppType.Of<LayoutGroup>())!=null;
        // 0.1.155: the list's room before the button (to keep it the same).
        copy.SetActive(false);
        var group=parent.TryCast<RectTransform>();float top0=0,bottom0=0;bool measured=group!=null&&layout&&Span(group,null,out top0,out bottom0);
        copy.transform.SetSiblingIndex(quitIndex); // directly above Quit
        if(!layout)
        {
            // No layout group: place it one step below Quit, never over a
            // native button.
            var rect=copy.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
            var quitRect=quit.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
            var templateRect=template.GetComponent(Il2CppType.Of<RectTransform>())?.TryCast<RectTransform>();
            if(rect!=null&&quitRect!=null)
            {
                var stepVector=templateRect!=null&&template!=quit?quitRect.anchoredPosition-templateRect.anchoredPosition:new Vector2(0,-quitRect.rect.height*1.2f);
                if(stepVector.sqrMagnitude<1)stepVector=new Vector2(0,-Math.Max(40,quitRect.rect.height*1.2f));
                rect.anchoredPosition=quitRect.anchoredPosition+stepVector;
            }
        }
        copy.SetActive(true);
        entries[id]=new Entry{Button=button,Template=template,Where=where};ours.Add(button.Pointer);
        string fit="";
        if(measured)try{fit=KeepRoom(group!,top0,bottom0);}catch(Exception ex){fit="; the list not fitted ("+ex.Message+")";}
        Bootstrap.Write("VR SETTINGS button added to the "+where+" menu: copy of "+template.name+" under "+parent.name+" layout="+layout+fit);
    }
    // The top and bottom of a list's shown buttons, in its parent's space.
    private static bool Span(RectTransform group,GameObject? skip,out float top,out float bottom)
    {
        top=float.NegativeInfinity;bottom=float.PositiveInfinity;
        LayoutRebuilder.ForceRebuildLayoutImmediate(group);
        var outer=group.parent;
        for(int i=0;i<group.childCount;i++)
        {
            var child=group.GetChild(i);if(child==null||!child.gameObject.activeSelf||skip!=null&&child.gameObject==skip)continue;
            var r=child.TryCast<RectTransform>();if(r==null)continue;var rect=r.rect;
            foreach(float y in new[]{rect.yMin,rect.yMax})
            {
                var p=r.TransformPoint(new Vector3(rect.center.x,y,0));if(outer!=null)p=outer.InverseTransformPoint(p);
                top=Math.Max(top,p.y);bottom=Math.Min(bottom,p.y);
            }
        }
        return float.IsFinite(top)&&float.IsFinite(bottom)&&top>bottom;
    }
    // 0.1.155: the list with our button in no more room than before: its gaps
    // smaller, then (if still needed) the whole list a little smaller, its top
    // where it was - Quit stays inside the menu.
    private static string KeepRoom(RectTransform group,float top0,float bottom0)
    {
        if(!Span(group,null,out float top1,out float bottom1))return "";
        float oldSpan=top0-bottom0,newSpan=top1-bottom1;
        if(newSpan<=oldSpan+.5f)return "; the list had room";
        var line=group.GetComponent(Il2CppType.Of<HorizontalOrVerticalLayoutGroup>())?.TryCast<HorizontalOrVerticalLayoutGroup>();
        int shown=0;for(int i=0;i<group.childCount;i++){var c=group.GetChild(i);if(c!=null&&c.gameObject.activeSelf)shown++;}
        float spacing=line!=null?line.spacing:0;var scale0=group.localScale;
        var (newSpacing,scale)=MenuFitMath.Plan(oldSpan,newSpan,spacing,line!=null?shown-1:0);
        if(line!=null&&Math.Abs(newSpacing-spacing)>.01f)line.spacing=newSpacing;
        if(scale<.999f)group.localScale=new Vector3(scale0.x*scale,scale0.y*scale,scale0.z);
        Span(group,null,out float top2,out float bottom2);
        var at=group.anchoredPosition;float shift=top0-top2;group.anchoredPosition=new Vector2(at.x,at.y+shift);
        Span(group,null,out float top3,out float bottom3);
        return "; the list kept in its room: "+oldSpan.ToString("F0")+" high, "+newSpan.ToString("F0")+" with the button -> "+(top3-bottom3).ToString("F0")
            +" (gaps "+spacing.ToString("F0")+" -> "+newSpacing.ToString("F0")+", scale "+scale.ToString("F2")+", moved "+shift.ToString("F0")+"; bottom "+bottom0.ToString("F0")+" -> "+bottom3.ToString("F0")+")";
    }
    private static void Label(GameObject button)
    {
        string label=UiLanguage.L("VR SETTINGS");
        foreach(var component in button.GetComponentsInChildren(Il2CppType.Of<TextMeshProUGUI>(),true))
        {var text=component.TryCast<TextMeshProUGUI>();if(text!=null&&text.text!=label)text.text=label;}
    }
    // Panel: drawn in the menu (frontend) and in the game (pause menu).
    internal void Render()
    {
        try
        {
            if(!QualityMenu.Open){if(panel!=null&&panel.activeSelf)panel.SetActive(false);placed=false;return;}
            if(panel==null&&!Build())return;
            if(!placed)
            {
                var yaw=Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0);
                panel!.transform.SetPositionAndRotation(rig.HeadPosition+yaw*new Vector3(0,-.05f,1.0f),yaw);placed=true;
            }
            panel!.SetActive(true);
            var lines=QualityMenu.Text.Split('\n');
            if(title!=null)title.text=lines.Length>0?lines[0]:"";
            for(int i=0;i<rows.Length;i++)
            {
                var r=rows[i];if(r==null)continue;
                string text=lines.Length>2+i?lines[2+i]:"";if(text.Length>=2)text=text.Substring(2);
                r.text=QualityMenu.Adjustable(i)?"‹   "+text+"   ›":text;
                r.color=i==QualityMenu.Row?Color.white:new Color(.82f,.88f,.92f,1);
            }
            if(highlight!=null)highlight.rectTransform.anchoredPosition=new Vector2(0,Height*.5f-TitleHeight-RowHeight*(QualityMenu.Row+.5f));
            int footerStart=2+QualityMenu.RowCount;
            if(footer!=null)footer.text=lines.Length>footerStart?string.Join("\n",lines,footerStart,lines.Length-footerStart).Trim('\n'):"";
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("VR SETTINGS panel: "+ex.Message);}}
    }
    private bool Build()
    {
        if(font==null)
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextMeshProUGUI>()))
            {var t=obj.TryCast<TextMeshProUGUI>();if(t!=null&&t.font!=null&&t.gameObject.scene.IsValid()){font=t.font;break;}}
        font??=TMP_Settings.defaultFontAsset;
        if(font==null)return false;
        panel=new GameObject("XIII VR settings page");panel.layer=5;UnityEngine.Object.DontDestroyOnLoad(panel);
        var rect=panel.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        var canvas=panel.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=200;
        var bg=panel.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;bg.color=new Color(.04f,.09f,.12f,.94f);bg.raycastTarget=false;
        rect.sizeDelta=new Vector2(Width,Height);panel.transform.localScale=Vector3.one*.001f;
        var bar=Child("Highlight",new Vector2(0,0),new Vector2(Width-20,RowHeight-4));
        highlight=bar.gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;highlight.color=new Color(.16f,.46f,.62f,.75f);highlight.raycastTarget=false;
        title=Label("Title",new Vector2(0,Height*.5f-TitleHeight*.5f),new Vector2(Width-60,TitleHeight),34,TextAlignmentOptions.Center);
        title.color=new Color(.45f,.85f,1,1);
        for(int i=0;i<rows.Length;i++)
            rows[i]=Label("Row "+i,new Vector2(0,Height*.5f-TitleHeight-RowHeight*(i+.5f)),new Vector2(Width-60,RowHeight),24,TextAlignmentOptions.Center);
        footer=Label("Footer",new Vector2(0,-Height*.5f+FooterHeight*.5f),new Vector2(Width-60,FooterHeight-20),19,TextAlignmentOptions.Top);
        footer.color=new Color(.75f,.8f,.84f,1);footer.enableWordWrapping=true;
        panel.SetActive(false);
        return true;
    }
    private RectTransform Child(string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name);go.layer=5;go.transform.SetParent(panel!.transform,false);
        var r=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private TextMeshProUGUI Label(string name,Vector2 position,Vector2 size,float points,TextAlignmentOptions alignment)
    {
        var text=Child(name,position,size).gameObject.AddComponent(Il2CppType.Of<TextMeshProUGUI>()).TryCast<TextMeshProUGUI>()!;
        text.font=font;text.fontSize=points;text.color=Color.white;text.alignment=alignment;text.raycastTarget=false;text.richText=true;text.enableWordWrapping=false;
        return text;
    }
    public void Dispose()
    {
        try{Unblock();QualityMenu.PointerOwnsTrigger=false;beam.Dispose();}
        finally
        {
            foreach(var e in entries.Values)if(e.Button!=null)UnityEngine.Object.Destroy(e.Button.gameObject);
            entries.Clear();ours.Clear();
            if(panel!=null)UnityEngine.Object.Destroy(panel);panel=null;
            patches.UnpatchSelf();if(Current==this)Current=null;
        }
    }
}
