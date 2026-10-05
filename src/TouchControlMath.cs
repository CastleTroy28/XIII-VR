using System;
using System.Collections.Generic;
using System.Linq;
namespace XiiiXR;
// 0.1.208.
// A touch pressed only an interaction whose object names said "button",
// "switch" or "panel". The lift of cp_elevator_02 and the power boxes that
// cut the alarms (power_box_06_b) are named "raycast_target", "trigger" and
// "Collider". Now what decides is what the interaction runs when used
// (Grip+A): a small interaction that does something - an animation, a
// script, a lift, the alarms off - is pressed by a touch. Never by a touch:
// taking or carrying (pick-ups, ladders, hooks, ziplines), a leaf the hand
// moves itself (a door, a cabinet), raising the alarm, nothing at all, and
// a large volume (a room-sized trigger brushed in passing).
internal static class TouchControlMath
{
    // ActionTrigger.Input: what Grip+A fires.
    internal const int Input=DoorStoryMath.Input;
    // A control is small: its collider's largest side at most this (m).
    internal const float LargestControl=1f;
    internal enum Verdict{Control,NotInteraction,DoorAction,Leaf,Take,Alarm,NoEvents,Large}
    // Taking, carrying or moving the player: grip, walking or Grip+A do these.
    private static readonly HashSet<string> take=new(StringComparer.Ordinal)
    {
        "PickUpItem","ProgressionItemCollect","GiveItemToPlayer","SpawnPickup","LadderGrab","GrapplingHook","ZiplineEvent","PlaceProp","SetCharacterMount"
    };
    // events: each event's type, trigger mask, and whether it animates the
    // touched collider's own leaf (a door or a cabinet the hand moves).
    // named: the object is named as a button, a switch or a panel.
    internal static Verdict Classify(IEnumerable<(string type,int trigger,bool leaf)> events,bool named,float size)
    {
        bool acts=false;
        foreach(var e in events)
        {
            if((e.trigger&Input)==0)continue;
            if(e.leaf)return Verdict.Leaf;
            if(take.Contains(e.type))return Verdict.Take;
            // An accidental touch on an alarm box would fail the mission.
            if(e.type=="AlarmActivator")return Verdict.Alarm;
            if(!DoorStoryMath.Cosmetic(e.type))acts=true;
        }
        if(named)return Verdict.Control;
        if(!acts)return Verdict.NoEvents;
        if(!(size<=LargestControl))return Verdict.Large;
        return Verdict.Control;
    }
    // 0.1.244: a thing the game breaks when used (its use runs
    // DestructableObjectHandle: the vent behind the leaves in the sanctuary's
    // entrance) is broken by a blow too (a fist, or the hand with a weapon),
    // as Grip+A at it breaks it; its damage alone did nothing. Never a thing
    // that takes or carries, raises the alarm or moves a leaf.
    internal const string BreakEvent="DestructableObjectHandle";
    internal static bool Breaks(IEnumerable<(string type,int trigger,bool leaf)> events)
    {
        bool breaks=false;
        foreach(var e in events)
        {
            if((e.trigger&Input)==0)continue;
            if(e.leaf||take.Contains(e.type)||e.type=="AlarmActivator")return false;
            if(e.type==BreakEvent)breaks=true;
        }
        return breaks;
    }
    // Rejections not worth a log line: they come with every door, pick-up and body.
    internal static bool Quiet(Verdict v)=>v is Verdict.NotInteraction or Verdict.DoorAction or Verdict.Leaf or Verdict.Take;
    internal static string Describe(Verdict v)=>v switch
    {
        Verdict.Control=>"a control",
        Verdict.NotInteraction=>"not an interaction (a pick-up, a person)",
        Verdict.DoorAction=>"a door's own interaction (the hand moves the door)",
        Verdict.Leaf=>"it moves the touched leaf itself (the hand moves it)",
        Verdict.Take=>"it takes or carries (grip or Grip+A does that)",
        Verdict.Alarm=>"it raises the alarm (never by a touch; Grip+A does it)",
        Verdict.NoEvents=>"it runs nothing on use",
        Verdict.Large=>"a large volume (over "+LargestControl.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" m), not a control",
        _=>v.ToString()
    };
    // "CustomAnimationToolHandle, DisableAlarmsEvent x2" (only what Grip+A runs).
    internal static string Summary(IEnumerable<(string type,int trigger,bool leaf)> events)
    {
        var used=events.Where(e=>(e.trigger&Input)!=0).Select(e=>e.leaf?"leaf motion":e.type).ToList();
        return used.Count==0?"none":string.Join(", ",used.GroupBy(n=>n).OrderBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key+(g.Count()>1?" x"+g.Count():"")));
    }
}
