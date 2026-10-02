using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace XiiiXR;
// 0.1.107: the game's death screen is a comic layout built for its flat UI
// camera (and a snapshot of the world camera). In VR it surrounded the player
// as mirrored, smeared fragments. While it is up (the world camera is off),
// VR shows only black and one panel: the message and Continue / Quit, which
// call the native death screen's own button handlers.
internal sealed class DeathScreenVr:IDisposable
{
    private const float Width=DeathPanelLayout.Width,Height=DeathPanelLayout.Height,ButtonWidth=DeathPanelLayout.ButtonWidth,ButtonHeight=DeathPanelLayout.ButtonHeight,ButtonY=DeathPanelLayout.ButtonY,ButtonX=DeathPanelLayout.ButtonX;
    private readonly CameraRig rig;
    private DeathScreenControl? control;
    private bool shown,acted,quit,died;
    private float nextScan,nextError,shownAt,actedAt;
    private GameObject? cameraRoot;private Camera? overlay;
    private GameObject? panel;private TMP_FontAsset? font;
    private TextMeshProUGUI? title,subtitle,continueLabel,quitLabel;private Image? continueBack,quitBack;
    private int layer=-1;private string language="",nativeSubtitle="";
    private readonly MenuBeam beam=new();private readonly UiPointerState pointer=new();
    private static readonly Color Idle=new(.16f,.16f,.17f,1),Hover=new(.80f,.12f,.12f,1);
    // 0.1.162: the game's death screens, kept between rare searches (every
    // 30 s since 0.1.164, at once after a scene change, and within a second around a death
    // or a failed level while none is known); the game's hook hands over one
    // that starts. The kept ones are checked as often as before.
    private readonly SceneFind<DeathScreenControl> screens=new("death screen",30,found=>
    {
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<DeathScreenControl>()))
        {var c=obj.TryCast<DeathScreenControl>();if(c!=null&&c.gameObject.scene.IsValid())found.Add(c);}
    });
    internal DeathScreenVr(CameraRig owner)
    {
        rig=owner;LevelFailWatch.Install();
        SceneHooks.Death=c=>
        {
            foreach(var k in screens.Items)if(k!=null&&k.Pointer==c.Pointer){nextScan=0;return;}
            screens.Add(c);nextScan=0;
        };
    }
    internal bool Shown=>shown;
    internal void Tick()
    {
        try
        {
            float now=Time.realtimeSinceStartup;
            // 0.1.121: 4 times a second only around a death (once a second
            // otherwise), never with another scene search in the frame.
            // 0.1.162: the kept screens; the scene search itself rarely.
            bool searched=screens.Refresh();
            if(searched||now>=nextScan){nextScan=now+(shown||watchClosely?.25f:1f);Scan();}
            if(shown)Point(now);
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("DEATH SCREEN VR: "+ex.Message);}
            Hide();
        }
    }
    private string lastState="";private bool watchClosely;
    private void Scan()
    {
        // 0.1.109: the native "started" flag alone missed a real death (the
        // mirrored native screen stayed). Also use the local player's death.
        DeathScreenControl? started=null,present=null;bool enabled=false;
        screens.Prune();
        foreach(var c in screens.Items)
        {
            if(c==null||!c.gameObject.activeInHierarchy)continue;
            present??=c;enabled|=c.isActiveAndEnabled;
            if(c.isActiveAndEnabled&&c.isStarted){started=c;break;}
        }
        bool dead=PlayerDead(out string health,false);bool world=rig.GameplayCameraRendering;
        // A death or a failed level with no screen known: search again soon.
        if(present==null&&(dead||LevelFailWatch.Active))screens.Soon(1);
        // 0.1.153: a failed level (the player alive) gets the same screen.
        LevelFailWatch.Observe(present!=null&&enabled);
        bool failed=LevelFailWatch.Active;watchClosely=dead||started!=null||failed;
        bool gameplay=!rig.Frontend&&!rig.MovieActive&&!rig.Scripted;
        bool want=gameplay&&DeathPanelLayout.Overlay(started!=null,present!=null,dead,world,failed&&enabled);
        string state="present="+(present!=null)+" enabled="+enabled+" started="+(started!=null)+" dead="+dead+" failed="+failed+" health="+health+" worldCamera="+world+" gameplay="+gameplay;
        if(state!=lastState&&(present!=null||dead||shown||lastState.Contains("present=True")||lastState.Contains("dead=True")))Bootstrap.Write("DEATH SCREEN state "+state+" overlay="+want);
        lastState=state;
        var screen=started??present;
        if(want&&(!shown||(screen!=null&&(control==null||screen.Pointer!=control.Pointer))))Show(screen);
        else if(!want&&shown)Hide();
        if(shown)UpdateTexts();
    }
    private void Show(DeathScreenControl? c)
    {
        control=c;acted=quit=false;shownAt=Time.realtimeSinceStartup;
        UseLayer(QuietLayer());
        EnsureCamera();
        if(panel==null&&!Build())return;
        died=PlayerDead(out string health,true);
        nativeSubtitle=c==null?"":NativeSubtitle(c,out string term0);string term=c==null?"none":TermOf(c);
        var yaw=Quaternion.Euler(0,rig.HeadRotation.eulerAngles.y,0);
        panel!.transform.SetPositionAndRotation(rig.HeadPosition+yaw*new Vector3(0,-.08f,1.35f),yaw);
        panel.SetActive(true);overlay!.cullingMask=1<<layer;overlay.enabled=true;
        language="";UpdateTexts();shown=true;
        Bootstrap.Write("DEATH SCREEN VR shown died="+died+" health="+health+(died?"":" levelFailed reason="+LevelFailWatch.Reason+" term="+LevelFailWatch.Term)+" subtitleTerm="+term+" nativeSubtitle=\""+nativeSubtitle+"\" layer="+layer+" ("+layerNote+"); black surround, own panel only");
    }
    private void Hide()
    {
        if(!shown)return;shown=false;LevelFailWatch.Clear();
        try{if(overlay!=null)overlay.enabled=false;}catch{}
        try{if(panel!=null)panel.SetActive(false);}catch{}
        try{beam.Hide();}catch{}
        pointer.Sample(false,false,0);control=null;
        Bootstrap.Write("DEATH SCREEN VR hidden");
    }
    private void UpdateTexts()
    {
        string code=UiLanguage.Code;if(code==language||title==null||subtitle==null)return;language=code;
        // 0.1.174: "Don't hit your bro" is gone; the title is the game's.
        title.text=died?UiLanguage.L("YOUR CHARACTER HAS DIED"):UiLanguage.L("OBJECTIVE FAILED");
        // 0.1.121: no joke line after a death any more (all languages); the
        // game's own subtitle, if it has one.
        subtitle.text=nativeSubtitle;
        if(continueLabel!=null)continueLabel.text=UiLanguage.L("CONTINUE");
        if(quitLabel!=null)quitLabel.text=UiLanguage.L("QUIT");
    }
    private void Point(float now)
    {
        if(panel==null)return;
        // After a button (or with the pause menu open) the game's own menus
        // and loading screen must be visible: add the UI layer, drop our panel.
        // Quit that led nowhere (the death screen is still up): offer the
        // panel again.
        if(acted&&quit&&now-actedAt>4){acted=quit=false;}
        bool native=acted||PauseMenuControl.HackGameIsPaused;
        overlay!.cullingMask=native?(1<<layer)|(1<<5):1<<layer;
        if(native){if(panel.activeSelf)panel.SetActive(false);pointer.Sample(false,false,0);beam.Hide();return;}
        if(!panel.activeSelf)panel.SetActive(true);
        if(!rig.SamplePointerHand(out var hand)){pointer.Sample(false,false,0);beam.Hide();return;}
        var origin=CameraRig.UnityPosition(hand);var direction=rig.PointerRotation(hand)*Vector3.forward;
        var t=panel.transform;var normal=t.forward;float facing=Vector3.Dot(direction,normal);
        int target=0;Vector3 end=origin+direction*1.5f;
        if(facing>.05f)
        {
            float distance=Vector3.Dot(t.position-origin,normal)/facing;
            if(distance>0&&distance<6)
            {
                var hit=origin+direction*distance;var local=t.InverseTransformPoint(hit);
                if(DeathPanelLayout.OnPanel(local.x,local.y)){end=hit;target=DeathPanelLayout.Hit(local.x,local.y);}
            }
        }
        // Ignore the first second: a trigger still held from the last shot.
        if(now-shownAt<1)target=0;
        if(continueBack!=null)continueBack.color=target==1?Hover:Idle;
        if(quitBack!=null)quitBack.color=target==2?Hover:Idle;
        var input=rig.MenuPointerControls;bool held=input.Valid&&(input.Held&HandControls.Trigger)!=0;
        var step=pointer.Sample(true,held,target);
        beam.Show(origin,end,target!=0);
        if(!step.Click||target==0)return;
        acted=true;quit=target==2;actedAt=now;rig.PunchHaptics(!rig.PointerLeft);rig.DisarmMenuTriggers();
        Bootstrap.Write("DEATH SCREEN VR "+(target==1?"continue":"quit")+" pressed");
        var screen=control??AnyDeathScreen();
        if(screen==null){Bootstrap.Warn("DEATH SCREEN VR: native death screen component not found; showing the game's own screen");return;}
        control=screen;
        if(target==1)screen.OnRetryButtonPressed();else screen.OnQuitButtonPressed();
    }
    private void EnsureCamera()
    {
        if(overlay!=null)return;
        cameraRoot=new GameObject("XIII VR death screen camera");UnityEngine.Object.DontDestroyOnLoad(cameraRoot);
        overlay=cameraRoot.AddComponent(Il2CppType.Of<Camera>()).TryCast<Camera>()!;
        overlay.enabled=false;
        // Last camera, clearing to black: nothing the game draws shows through.
        overlay.clearFlags=CameraClearFlags.SolidColor;overlay.backgroundColor=Color.black;
        overlay.depth=1001;overlay.cullingMask=1<<layer;overlay.nearClipPlane=.02f;overlay.farClipPlane=30;
        overlay.stereoTargetEye=StereoTargetEyeMask.Both;
        rig.AttachTrackedCamera(overlay);
        beam.Layer=layer;
    }
    // 0.1.153: the panel's own layer, so the overlay camera draws nothing
    // else. Up to 0.1.152 an unnamed layer was looked for; XIII names every
    // one, so the panel fell back to layer 5 - the game's UI, and its own death
    // screen was drawn over ours. Now the layer with no active renderer or
    // canvas on it (the emptiest otherwise).
    private string layerNote="";
    private int QuietLayer()
    {
        var used=new int[32];var named=new bool[32];
        try
        {
            for(int i=0;i<32;i++)named[i]=!string.IsNullOrEmpty(LayerMask.LayerToName(i));
            foreach(var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Renderer>()))
            {var r=o.TryCast<Renderer>();if(r!=null&&!Own(r.gameObject))used[r.gameObject.layer&31]++;}
            foreach(var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<Canvas>()))
            {var c=o.TryCast<Canvas>();if(c!=null&&!Own(c.gameObject))used[c.gameObject.layer&31]++;}
        }
        catch(Exception ex){Bootstrap.Warn("DEATH SCREEN VR layer count: "+ex.Message);}
        int l=DeathPanelLayout.QuietLayer(used,named);
        layerNote=(named[l]?"\""+LayerMask.LayerToName(l)+"\"":"unnamed")+", "+used[l]+" other objects on it";
        return l;
    }
    private bool Own(GameObject go)
    {
        var t=go.transform;
        return panel!=null&&(t.IsChildOf(panel.transform))||go.name.StartsWith("XIII menu ",StringComparison.Ordinal);
    }
    private void UseLayer(int l)
    {
        if(l==layer)return;layer=l;beam.Layer=l;
        if(overlay!=null)overlay.cullingMask=1<<l;
        if(panel!=null)SetLayer(panel.transform,l);
    }
    private static void SetLayer(Transform t,int l)
    {
        t.gameObject.layer=l;
        for(int i=0;i<t.childCount;i++)SetLayer(t.GetChild(i),l);
    }
    private PlayerState? playerState;private Transform? stateRoot;
    // unknown: what to answer when the player's health cannot be read.
    private bool PlayerDead(out string health,bool unknown)
    {
        health="unknown";
        try
        {
            var root=rig.PlayerRoot;if(root==null)return unknown;
            if(stateRoot==null||root.Pointer!=stateRoot.Pointer||playerState==null)
            {
                stateRoot=root;
                playerState=root.GetComponentInChildren(Il2CppType.Of<PlayerState>(),true)?.TryCast<PlayerState>()
                    ??root.GetComponentInParent(Il2CppType.Of<PlayerState>())?.TryCast<PlayerState>();
            }
            if(playerState==null)return unknown;
            health=playerState.currentHealth.ToString("F1");return playerState.currentHealth<=0;
        }
        catch(Exception){playerState=null;return unknown;}
    }
    private static DeathScreenControl? AnyDeathScreen()
    {
        foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<DeathScreenControl>()))
        {var c=obj.TryCast<DeathScreenControl>();if(c!=null&&c.gameObject.scene.IsValid())return c;}
        return null;
    }
    private static string TermOf(DeathScreenControl c)
    {
        try{return c.levelEndMessageSubtitle?.Term??"";}catch(Exception){return "";}
    }
    private static string NativeSubtitle(DeathScreenControl c,out string term)
    {
        term="";
        try
        {
            var loc=c.levelEndMessageSubtitle;if(loc==null)return "";
            term=loc.Term??"";
            var text=loc.GetComponent(Il2CppType.Of<TextMeshProUGUI>())?.TryCast<TextMeshProUGUI>();
            return text?.text??"";
        }
        catch(Exception){return "";}
    }
    private bool Build()
    {
        if(font==null)
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TextMeshProUGUI>()))
            {var t=obj.TryCast<TextMeshProUGUI>();if(t!=null&&t.font!=null&&t.gameObject.scene.IsValid()){font=t.font;break;}}
        font??=TMP_Settings.defaultFontAsset;
        if(font==null)return false;
        panel=new GameObject("XIII VR death screen");panel.layer=layer;UnityEngine.Object.DontDestroyOnLoad(panel);
        var rect=panel.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        var canvas=panel.AddComponent(Il2CppType.Of<Canvas>()).TryCast<Canvas>()!;canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=300;
        var bg=panel.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;bg.color=new Color(.035f,.035f,.04f,1);bg.raycastTarget=false;
        rect.sizeDelta=new Vector2(Width,Height);panel.transform.localScale=Vector3.one*.001f;
        var banner=Child("Banner",new Vector2(0,120),new Vector2(Width,130)).gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;
        banner.color=new Color(.78f,.11f,.11f,1);banner.raycastTarget=false;
        title=Label("Title",new Vector2(0,120),new Vector2(Width-60,130),62);title.fontStyle=FontStyles.Bold;
        subtitle=Label("Subtitle",new Vector2(0,-5),new Vector2(Width-80,110),36);subtitle.color=new Color(.86f,.86f,.86f,1);subtitle.enableWordWrapping=true;
        // 0.1.153: a long reason (two lines, some languages longer) shrinks instead of running into the buttons.
        subtitle.enableAutoSizing=true;subtitle.fontSizeMin=22;subtitle.fontSizeMax=36;
        title.enableAutoSizing=true;title.fontSizeMin=40;title.fontSizeMax=62;
        continueBack=Button("Continue",-ButtonX,out continueLabel);quitBack=Button("Quit",ButtonX,out quitLabel);
        panel.SetActive(false);
        return true;
    }
    private Image Button(string name,float x,out TextMeshProUGUI label)
    {
        var back=Child(name,new Vector2(x,ButtonY),new Vector2(ButtonWidth,ButtonHeight)).gameObject.AddComponent(Il2CppType.Of<Image>()).TryCast<Image>()!;
        back.color=Idle;back.raycastTarget=false;
        label=Label(name+" label",new Vector2(x,ButtonY),new Vector2(ButtonWidth-20,ButtonHeight),38);label.fontStyle=FontStyles.Bold;
        return back;
    }
    private RectTransform Child(string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name);go.layer=layer;go.transform.SetParent(panel!.transform,false);
        var r=go.AddComponent(Il2CppType.Of<RectTransform>()).TryCast<RectTransform>()!;
        r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    private TextMeshProUGUI Label(string name,Vector2 position,Vector2 size,float points)
    {
        var text=Child(name,position,size).gameObject.AddComponent(Il2CppType.Of<TextMeshProUGUI>()).TryCast<TextMeshProUGUI>()!;
        text.font=font;text.fontSize=points;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.richText=false;text.enableWordWrapping=false;
        return text;
    }
    public void Dispose()
    {
        try{Hide();}catch{}
        beam.Dispose();
        if(panel!=null)UnityEngine.Object.Destroy(panel);panel=null;
        if(cameraRoot!=null)UnityEngine.Object.Destroy(cameraRoot);cameraRoot=null;overlay=null;
    }
}
