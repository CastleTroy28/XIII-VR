using System;
using HarmonyLib;
using UnityEngine;
namespace XiiiXR;
// 0.1.153: the
// death screen overlay only followed the player's death. The game's own
// HandleLevelFailed (any failed objective: a blow to a friend, an alarm, a
// civilian) and its death screen's Initiate are now observed, so a failed
// level with the player alive gets the same VR screen.
internal static class LevelFailWatch
{
    private static readonly Harmony patches=new("xiii.vr.xrbootstrap.levelfail");
    private static bool installed,seen;
    private static float failedAt=-100;
    internal static int Reason {get;private set;}
    internal static string Term {get;private set;}="";
    // A level failed and its screen has not gone yet (30 s at most before it shows).
    internal static bool Active=>failedAt>0&&(seen||Time.realtimeSinceStartup-failedAt<30);
    internal static void Install()
    {
        if(installed)return;installed=true;
        try{patches.Patch(AccessTools.Method(typeof(PlayerState),nameof(PlayerState.HandleLevelFailed)),postfix:new HarmonyMethod(typeof(LevelFailWatch),nameof(Failed)));}
        catch(Exception ex){Bootstrap.Warn("LEVEL FAIL watch (HandleLevelFailed): "+ex.Message);}
        try{patches.Patch(AccessTools.Method(typeof(DeathScreenControl),nameof(DeathScreenControl.Initiate)),postfix:new HarmonyMethod(typeof(LevelFailWatch),nameof(Initiated)));}
        catch(Exception ex){Bootstrap.Warn("LEVEL FAIL watch (death screen): "+ex.Message);}
    }
    private static void Failed(LevelFailReason __0,string __1)
    {
        try
        {
            failedAt=Time.realtimeSinceStartup;seen=false;Reason=(int)__0;Term=__1??"";
            Bootstrap.Write("LEVEL FAILED reason="+__0+" term="+Term);
        }
        catch(Exception){}
    }
    private static void Initiated(int __0,string __1)
    {
        try
        {
            if(failedAt<0||Time.realtimeSinceStartup-failedAt>30){failedAt=Time.realtimeSinceStartup;seen=false;Reason=0;Term=__1??"";}
            Bootstrap.Write("LEVEL END screen started for player "+__0+" subtitle="+(__1??""));
        }
        catch(Exception){}
    }
    // Called with every death screen scan: once the screen has come and gone, the failure is over.
    internal static void Observe(bool present)
    {
        if(failedAt<0)return;
        if(present){seen=true;return;}
        if(seen||Time.realtimeSinceStartup-failedAt>=30)Clear();
    }
    internal static void Clear(){failedAt=-100;seen=false;Reason=0;Term="";}
}
