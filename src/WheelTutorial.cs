using System;
using UnityEngine;
namespace XiiiXR;
// 0.1.214. The game's weapon wheel tutorial (the first level) opens the wheel
// and locks the controls until the game's pause button is pressed; its hint
// names that button. In VR right A ends it, and its hint names right A.
// Ending it is what the game itself does on its pause button
// (PauseMenuControl.Update): GameInputManager.forceOpenWeaponWheel goes off,
// then the wheel's own Update closes the wheel, lets the game go on and hides
// the hint.
internal static class WheelTutorial
{
    // The hint is written before the game asks for its wheel, so from the
    // tutorial step on its labels already name right A.
    private static float startedAt=-100;
    internal static void Started()=>startedAt=Time.realtimeSinceStartup;
    internal static bool Forced
    {
        get{try{return GameInputManager.forceOpenWeaponWheel;}catch(Exception){return false;}}
    }
    internal static bool LabelsA=>WheelTutorialMath.Labels(Forced,Time.realtimeSinceStartup-startedAt);
    // wheelOpen: the game opened its wheel already (it waits while the
    // inventory is busy); player: whose controls the tutorial locked.
    internal static string End(bool wheelOpen,int player)
    {
        startedAt=-100;
        GameInputManager.forceOpenWeaponWheel=false;
        if(wheelOpen)return "the game closes its wheel and goes on";
        // Its wheel never opened: nothing of the game's would lift the lock
        // the tutorial set or hide its hint.
        try{GameInputManager.SetInputLock(false,player);}
        catch(Exception ex){return "its wheel had not opened; its lock could not be lifted: "+ex.Message;}
        try{GameUIManager.HideTutorialMessage(player);}catch(Exception){}
        return "its wheel had not opened yet: the lock it set is lifted and its hint hidden";
    }
}
