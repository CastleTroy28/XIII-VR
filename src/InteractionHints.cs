using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
namespace XiiiXR;
internal static class InteractionHints
{
    private static readonly Dictionary<int,(CanvasGroup group,float alpha)> hidden=new();
    internal static bool IsDoor(Component? a)
    {
        if(a==null)return false;
        if(a.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.Door>())!=null)return true;
        var t=a.transform;for(int i=0;i<4&&t!=null;i++,t=t.parent)if(ChordName(t.name))return true;
        return false;
    }
    // Doors, cabinets, lockers and (0.1.83) breakable ventilation grilles keep
    // the deliberate right Grip + A.
    internal static bool ChordName(string name)
    {
        var n=name.ToLowerInvariant();
        return n.Contains("door")||n.Contains("cabinet")||n.Contains("locker")||n.Contains("cupboard")
            ||Word(n,"vent")||n.Contains("grate")||n.Contains("grill")||n.Contains("hatch");
        // "vent" as a word start: not "event", "inventory".
        static bool Word(string n,string w){for(int i=n.IndexOf(w);i>=0;i=n.IndexOf(w,i+1))if(i==0||!char.IsLetter(n[i-1]))return true;return false;}
    }
    internal static void Apply(PlayerHUDControl hud)
    {
        try
        {
            if(CameraRig.Current==null||hud==null||!hud.GetOwner().IsPlayer)return;
            bool all=QualityOptions.Hints?.Value==false;
            Set(hud.promptGroup?.gameObject,all);Set(hud.secondaryPromptGroup,all);
            Set(hud.hitInteractionLabel,all);Set(hud.tutorialController?.group,all);
        }
        catch(Exception e){Bootstrap.Warn("INTERACTION HINTS: "+e.Message);}
    }
    private static void Set(GameObject? obj,bool hide)
    {
        if(obj==null)return;int id=obj.GetInstanceID();
        if(!hide){if(hidden.TryGetValue(id,out var old)){if(old.group!=null)old.group.alpha=old.alpha;hidden.Remove(id);}return;}
        if(!hidden.TryGetValue(id,out var entry))
        {
            var group=obj.GetComponent(Il2CppType.Of<CanvasGroup>())?.TryCast<CanvasGroup>()??obj.AddComponent(Il2CppType.Of<CanvasGroup>()).TryCast<CanvasGroup>()!;
            entry=(group,group.alpha);hidden[id]=entry;
        }
        if(entry.group!=null)entry.group.alpha=0;
    }
    internal static void Restore(){foreach(var h in hidden.Values)if(h.group!=null)h.group.alpha=h.alpha;hidden.Clear();}
}
