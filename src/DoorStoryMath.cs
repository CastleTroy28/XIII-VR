using System;
using System.Collections.Generic;
using System.Linq;
namespace XiiiXR;
// 0.1.203. What a door's own interaction does
// besides moving its leaf: the events its receivers run on that interaction
// (Input) are sorted into the leaf's own motion, sound or look, and the rest -
// the authored story (guards let in, a dialogue, a checkpoint). A door with a
// story is opened from closed by its native interaction, never by the hand
// alone: moving the leaf by hand ran none of these events.
internal static class DoorStoryMath
{
    internal enum Kind{Motion,Cosmetic,Story}
    // ActionTrigger.Input: what Grip+A or a strike on the door fires.
    internal const int Input=2;
    // Event types that only sound or look; they move nothing on.
    private static readonly HashSet<string> cosmetic=new(StringComparer.Ordinal)
    {
        "SoundSender","FmodEmitterToggleEvent","FmodEmitterChangeParametersEvent","FmodStudioBankLoader","AudioReverbSnapshot",
        "EmitAISoundEvent","VentAudioEvent","HighlightEventHandler","CameraShakeSet","LightActivateShadows","GraphicsOverrideHandle",
        "FogSourceChanger","EffectSpawner"
    };
    internal static bool Cosmetic(string type)=>cosmetic.Contains(type);
    // One event; null when the door's interaction does not run it.
    internal static Kind? Classify(string type,int trigger,bool ownMotion)
    {
        if((trigger&Input)==0)return null;
        if(type=="CustomAnimationToolHandle"&&ownMotion)return Kind.Motion;
        return cosmetic.Contains(type)?Kind.Cosmetic:Kind.Story;
    }
    // A short list of the events ("door motion, SoundSender, ActivateBehaviorTree x2")
    // and the story part of it ("" for an ordinary door).
    internal static string Describe(IEnumerable<(string type,Kind kind)> events,out string story)
    {
        var all=events.ToList();
        static string Join(IEnumerable<string> names)=>string.Join(", ",names.GroupBy(n=>n).OrderBy(g=>g.Key=="door motion"?0:1).ThenBy(g=>g.Key,StringComparer.Ordinal).Select(g=>g.Key+(g.Count()>1?" x"+g.Count():"")));
        story=Join(all.Where(e=>e.kind==Kind.Story).Select(e=>e.type));
        return all.Count==0?"none":Join(all.Select(e=>e.kind==Kind.Motion?"door motion":e.type));
    }
}
