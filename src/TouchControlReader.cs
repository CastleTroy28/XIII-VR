using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using Door=PlayMagic.AI.Door;
namespace XiiiXR;
// 0.1.208: what an interaction runs when used (its receivers' events), read
// once per touched collider for TouchControlMath. Nothing is run here.
internal static class TouchControlReader
{
    internal static TouchControlMath.Verdict Read(RaycastAction action,Collider collider,bool named,out string events)=>Read(action,collider,named,out events,out _);
    // 0.1.244: and whether a blow breaks it (TouchControlMath.Breaks).
    internal static TouchControlMath.Verdict Read(RaycastAction action,Collider collider,bool named,out string events,out bool breaks)
    {
        events="";breaks=false;
        try
        {
            if(!named&&action.GetRaycastHittableType()!=RaycastHittableType.Interaction)return TouchControlMath.Verdict.NotInteraction;
            var door=collider.GetComponentInParent(Il2CppType.Of<Door>())?.TryCast<Door>();
            if(door?.doorRaycastTargets!=null)foreach(var t in door.doorRaycastTargets)if(t!=null&&t.Pointer==action.Pointer)return TouchControlMath.Verdict.DoorAction;
            var list=new List<(string type,int trigger,bool leaf)>();var seen=new HashSet<IntPtr>();
            foreach(var receivers in new[]{action.manualInteractions,action.automaticInteractionList})
            {
                if(receivers==null)continue;
                for(int r=0;r<receivers.Count;r++)
                {
                    var receiver=receivers[r];if(receiver==null||!seen.Add(receiver.Pointer))continue;
                    var all=receiver.events;if(all==null)continue;
                    for(int e=0;e<all.Count;e++)
                    {
                        var ev=all[e];if(ev==null)continue;
                        var tool=ev.TryCast<EventReceiver.CustomAnimationToolHandle>()?.customAnimTool;
                        list.Add((ev.GetIl2CppType().Name,(int)ev.eventTrigger,tool!=null&&Leaf(tool,collider)));
                    }
                }
            }
            var size=collider.bounds.size;
            events=TouchControlMath.Summary(list);breaks=TouchControlMath.Breaks(list);
            return TouchControlMath.Classify(list,named,Math.Max(size.x,Math.Max(size.y,size.z)));
        }
        catch(Exception ex){events="unreadable: "+ex.Message;breaks=false;return named?TouchControlMath.Verdict.Control:TouchControlMath.Verdict.NoEvents;}
    }
    // The animation moves the touched collider's own leaf, the way a door or
    // a cabinet moves: the hand moves that (PhysicalDoors), a touch never uses it.
    private static bool Leaf(CustomAnimationTool tool,Collider collider)
    {
        bool motion=false;
        foreach(var m in DoorMotion.Read(tool))
        {
            motion=true;
            if(m.Pivot!=null&&collider.transform.IsChildOf(m.Pivot))return true;
        }
        if(motion&&tool.meshColliders!=null)foreach(var c in tool.meshColliders)if(c!=null&&c.Pointer==collider.Pointer)return true;
        return false;
    }
}
