using System;
using System.Collections.Generic;
using UnityEngine;
using Door=PlayMagic.AI.Door;
namespace XiiiXR;
// 0.1.203: reads what a door's own interaction runs (its receivers' events,
// DoorStoryMath sorts them). Read once when the door is bound; nothing is run.
internal static class DoorStoryEvents
{
    // A leaf animation counts as this door's own motion when it is one of
    // the door's own leaves or near this leaf (a cabinet's other wing); a
    // far one (another room's door, a lift) is part of the story.
    private const float NearMotion=2.5f;
    // 0.1.246: the interaction moves this leaf when used (Grip+A or a strike):
    // one of its receivers' events animates exactly this tool.
    internal static bool Animates(RaycastAction action,CustomAnimationTool tool)
    {
        try
        {
            foreach(var receivers in new[]{action.manualInteractions,action.automaticInteractionList})
            {
                if(receivers==null)continue;
                for(int r=0;r<receivers.Count;r++)
                {
                    var list=receivers[r]?.events;if(list==null)continue;
                    for(int e=0;e<list.Count;e++)
                    {
                        var ev=list[e];if(ev==null||((int)ev.eventTrigger&DoorStoryMath.Input)==0)continue;
                        var target=ev.TryCast<EventReceiver.CustomAnimationToolHandle>()?.customAnimTool;
                        if(target!=null&&target.Pointer==tool.Pointer)return true;
                    }
                }
            }
        }
        catch(Exception){}
        return false;
    }
    internal static string Find(RaycastAction[] actions,CustomAnimationTool tool,Door? door,Vector3 pivot,out string story)
    {
        story="";var events=new List<(string,DoorStoryMath.Kind)>();
        try
        {
            var own=new HashSet<IntPtr>{tool.Pointer};
            if(door?.doorReferences!=null)foreach(var t in door.doorReferences)if(t!=null)own.Add(t.Pointer);
            var seen=new HashSet<IntPtr>();
            foreach(var action in actions)
            {
                if(action==null)continue;
                // The interaction's own receivers, and any automatic one that
                // the interaction (Input) also runs.
                foreach(var receivers in new[]{action.manualInteractions,action.automaticInteractionList})
                {
                    if(receivers==null)continue;
                    for(int r=0;r<receivers.Count;r++)
                    {
                        var receiver=receivers[r];if(receiver==null||!seen.Add(receiver.Pointer))continue;
                        var list=receiver.events;if(list==null)continue;
                        for(int e=0;e<list.Count;e++)
                        {
                            var ev=list[e];if(ev==null)continue;
                            string type=ev.GetIl2CppType().Name;bool ownMotion=false;
                            var handle=ev.TryCast<EventReceiver.CustomAnimationToolHandle>();
                            if(handle!=null)
                            {
                                var target=handle.customAnimTool;
                                ownMotion=target==null||own.Contains(target.Pointer)||(target.transform.position-pivot).magnitude<=NearMotion;
                            }
                            var kind=DoorStoryMath.Classify(type,(int)ev.eventTrigger,ownMotion);
                            if(kind!=null)events.Add((type,kind.Value));
                        }
                    }
                }
            }
        }
        catch(Exception ex){Bootstrap.Warn("DOOR EVENTS unreadable on "+tool.name+": "+ex.Message);}
        return DoorStoryMath.Describe(events,out story);
    }
}
