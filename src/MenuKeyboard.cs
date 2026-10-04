using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
// Native start screen and footer legends poll keyboard/Rewired; they are not
// clickable UI Buttons. Send a short, foreground-only native key pair.
// 0.1.210: menu keys no longer wait for Windows focus: the game's window is
// brought to the front for the key (with Virtual Desktop it is often not).
// 0.1.213: the start screen gets Enter at the press again. The game's
// any-button input (GetAnyButtonDown) did not answer it, and Enter came only
// half a second after the last press, so pressing on kept it waiting.
internal sealed class MenuKeyboard : IDisposable
{
    private readonly CameraRig rig;
    private ushort heldKey;
    private readonly MenuNavigation navigation=new();
    internal bool StickMode{get;private set;}
    private float releaseAt,nextFind;
    private readonly List<StartScreenControl> starts=new();
    private bool startupArmed,menuAArmed,menuBArmed;
    private float nextStartReport;
    internal MenuKeyboard(CameraRig camera){rig=camera;}
    internal static ushort PromptKey(InputActions action)=>action switch
    {
        InputActions.UI_Back=>0x08,
        InputActions.UI_Cancel or InputActions.UI_DiscardOption=>0x1b,
        InputActions.UI_Submit or InputActions.UI_ApplyOption=>0x0d,
        _=>0
    };
    internal bool Pulse(ushort key)
    {
        if(key==0||heldKey!=0)return false;
        if(!EscapeKey.SendKey(key,true))return false;
        heldKey=key;releaseAt=Time.realtimeSinceStartup+.08f;
        Bootstrap.Write("MENU native key="+key);return true;
    }
    internal void Tick()
    {
        if(heldKey!=0&&Time.realtimeSinceStartup>=releaseAt)Release();
        bool valid=rig.HeadTrackingValid&&(Application.isFocused||WindowFocus.InVr);
        if(!valid){startupArmed=menuAArmed=menuBArmed=false;navigation.Reset();StickMode=false;return;}
        // 0.1.121: the start screen exists only before a level (no gameplay camera).
        if(!rig.Frontend)starts.Clear();
        else if(SceneScan.Due(ref nextFind,.5f))
        {
            long scanStart=SceneScan.Begin();starts.Clear();
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<StartScreenControl>()))
            {var s=obj.TryCast<StartScreenControl>();if(s!=null&&s.gameObject.scene.IsValid())starts.Add(s);}
            SceneScan.End("start screen",scanStart);
        }
        bool waiting=false;
        foreach(var s in starts)
            if(s!=null&&s.isActiveAndEnabled&&s.m_state==StartScreenControl.SplashState.PlayerStartInputWait){waiting=true;break;}
        const ulong buttons=HandControls.A|HandControls.B|HandControls.Grip|HandControls.Trigger|(1UL<<32);
        var l=rig.MenuLeftControls;var r=rig.MenuRightControls;
        bool any=l.Valid&&(l.Held&buttons)!=0||r.Valid&&(r.Held&buttons)!=0||rig.LeftStick.Clicked||rig.RightStick.Clicked;
        if(!any)startupArmed=true;
        if(waiting)
        {
            if(any&&startupArmed)
            {
                startupArmed=false;rig.DisarmTrigger();
                bool typed=Pulse(0x0d);
                if(!typed&&Time.realtimeSinceStartup>=nextStartReport){nextStartReport=Time.realtimeSinceStartup+5;Bootstrap.Warn("START SCREEN Enter could not be typed: "+EscapeKey.LastRefusal);}
            }
            menuAArmed=menuBArmed=false;navigation.Reset();StickMode=false;return;
        }
        bool menu=GameUiControls.Current?.PointerMenuOpen==true&&GameUiControls.Current?.WheelOpen!=true&&!QualityMenu.Open&&!ControlsSheet.Open;
        bool a=r.Valid&&(r.Held&HandControls.A)!=0,b=r.Valid&&(r.Held&HandControls.B)!=0;
        if(!menu||!r.Valid){menuAArmed=menuBArmed=false;navigation.Reset();StickMode=false;return;}
        if(!a)menuAArmed=true;else if(menuAArmed){menuAArmed=false;Release();Pulse(0x0d);}
        if(!b)menuBArmed=true;else if(menuBArmed){menuBArmed=false;Release();Pulse(0x08);}
        var stick=rig.LeftStick;ushort nav=navigation.Step(stick.Valid,stick.Value,Time.realtimeSinceStartup);
        if(nav!=0&&!a&&!b&&Pulse(nav))StickMode=true;
        if((r.Held&HandControls.Trigger)!=0||l.Valid&&(l.Held&HandControls.Trigger)!=0)StickMode=false;
    }
    private void Release(){if(heldKey==0)return;ushort key=heldKey;heldKey=0;EscapeKey.SendKey(key,false);}
    public void Dispose(){Release();starts.Clear();}
}
