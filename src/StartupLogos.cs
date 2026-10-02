using System;
using HarmonyLib;
namespace XiiiXR;
internal static class StartupLogos
{
    private static Harmony? patches;
    internal static void Install()
    {
        if(patches!=null)return;
        var h=new Harmony("xiii.vr.xrbootstrap.logos");
        try{h.Patch(AccessTools.DeclaredMethod(typeof(StartLogosController),"Start"),prefix:new HarmonyMethod(typeof(StartupLogos),nameof(Start)));patches=h;}
        catch(Exception ex){h.UnpatchSelf();Bootstrap.Warn("STARTUP LOGOS patch unavailable; original startup retained: "+ex.Message);}
    }
    private static bool Start(StartLogosController __instance)
    {
        if(!QualityOptions.SkipLogos.Value)return true;
        try
        {
            // Same transition as native Start() with an empty logo list.
            // Do not patch CutscenePlayer or scene preload/loading routines.
            if(!__instance.leavingToMainMenu)__instance.GoToMainMenu();
            Bootstrap.Write("STARTUP LOGOS skipped; native main-menu loading requested");return false;
        }
        catch(Exception ex){Bootstrap.Warn("STARTUP LOGOS fallback: "+ex.Message);return true;}
    }
}
