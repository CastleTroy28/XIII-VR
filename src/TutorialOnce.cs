using System;
using System.Collections.Generic;
namespace XiiiXR;
// 0.1.175:
// the hint comes with the AK lying there, and the mod leaves the gun on the
// floor for the hand to take, so walking over it again made the game show the
// hint again. Each tutorial hint is shown once per game; later calls for the
// same hint are dropped (TutorialController.ShowMessage).
internal static class TutorialOnce
{
    private static readonly HashSet<string> shown=new(StringComparer.Ordinal);
    private static readonly HashSet<string> reported=new(StringComparer.Ordinal);
    // showing: the same hint is on the screen now (the game keeps it up while
    // its condition lasts): that is not a new showing.
    internal static bool Allow(string? term,bool showing=false)
    {
        if(string.IsNullOrEmpty(term)||showing)return true;
        if(shown.Add(term))return true;
        if(reported.Add(term))Bootstrap.Write("TUTORIAL "+term+" was shown already: not again");
        return false;
    }
    // 0.1.179: the text was dropped, but the game's tutorial step itself
    // (EventReceiver.TutorialShowEvent, "weapon wheel tutorial") ran again
    // and opened the wheel and locked the controls. A step whose hint was
    // shown already, or that ran already, is skipped whole. terms: the step's
    // hint names (default and per controller); showing: its hint is on the
    // screen now (the game repeating it while it lasts).
    private static readonly HashSet<string> steps=new(StringComparer.Ordinal);
    internal static bool AllowStep(string?[] terms,bool wheel,bool showing=false)
    {
        string first="";bool seen=false;
        foreach(var term in terms)
        {
            if(string.IsNullOrEmpty(term))continue;
            if(first.Length==0)first=term!;
            if(shown.Contains(term!)||steps.Contains(term!))seen=true;
        }
        if(first.Length==0||showing)return true;
        if(!seen){foreach(var term in terms)if(!string.IsNullOrEmpty(term))steps.Add(term!);return true;}
        if(reported.Add("step "+first))Bootstrap.Write("TUTORIAL step "+first+(wheel?" (opens the weapon wheel)":"")+" ran already: skipped whole, not again");
        return false;
    }
    internal static void ResetForTests(){shown.Clear();reported.Clear();steps.Clear();}
}
