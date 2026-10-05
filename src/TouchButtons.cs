using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
// Small local queries; native interaction permissions and events own the press.
// 0.1.208: which interactions a touch presses: TouchControlMath.
internal sealed class TouchButtons
{
    private readonly Il2CppReferenceArray<Collider> nearby=new(24);
    private readonly Vector3[] previous=new Vector3[4];private readonly bool[] sampled=new bool[4];
    private readonly RaycastAction?[] pressed=new RaycastAction?[2];private readonly Vector3[] contact=new Vector3[2];
    // 0.1.208: per collider: the control it presses (null if none), how it
    // was told (named, or by what it runs) and why not (null: no interaction).
    private readonly Dictionary<int,(Collider collider,RaycastAction? action,string how,string? why)> targets=new();
    private readonly Dictionary<int,float> nextNote=new();private int notes;
    private Transform? player;
    internal bool Injecting{get;private set;}
    internal void Reset(){Array.Clear(sampled,0,4);Array.Clear(pressed,0,2);Injecting=false;}
    internal void Tick(CameraRig rig,bool allowed,IInteractionActor? actor)
    {
        if(player!=rig.PlayerRoot){player=rig.PlayerRoot;Reset();targets.Clear();}
        if(!allowed||actor==null||!rig.SampleWorldHands(out var left,out var right,out bool lv)){Reset();return;}
        for(int side=0;side<2;side++)
        {
            bool rhs=side==1;int first=side*2;
            if(side==0&&(!lv||GripCarry.Current?.HidesLeft==true))
            {sampled[first]=sampled[first+1]=false;pressed[side]=null;continue;}
            var pose=rhs?right:left;
            var palm=CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,rhs)*new Vector3(0,-.008f,.03f);
            var tip=palm;bool armed=WeaponHands.Current?.TryButtonContacts(rhs,out palm,out tip)==true;
            if(!armed){palm=CameraRig.UnityPosition(pose)+GloveVisual.Rotation(pose,rhs)*new Vector3(0,-.008f,.03f);sampled[first+1]=false;}
            // Keep the palm probe while armed; use the collision-stopped weapon.
            bool latched=pressed[side]!=null&&Math.Min((palm-contact[side]).sqrMagnitude,armed?(tip-contact[side]).sqrMagnitude:float.PositiveInfinity)<.16f*.16f;
            if(!latched)pressed[side]=null;
            for(int probe=0;probe<(armed?2:1);probe++)
            {
                int index=first+probe;var p=probe==0?palm:tip;
                if(!sampled[index]){previous[index]=p;sampled[index]=true;continue;}
                var from=previous[index];previous[index]=p;var move=p-from;
                if(latched||move.sqrMagnitude<1e-10f||move.sqrMagnitude>.16f)continue;
                float radius=probe==0?.06f:.055f;
                int n=Physics.OverlapSphereNonAlloc(p,radius+.01f,nearby,~0,QueryTriggerInteraction.Collide);
                for(int i=0;i<n&&i<nearby.Length;i++)
                {
                    var c=nearby[i];if(c==null)continue;
                    var a=Resolve(c,out string how,out string? why);
                    if(a==null)
                    {
                        // 0.1.208: a touch that moved into an interaction but
                        // pressed nothing says why, so the next log shows it.
                        if(why!=null&&Hit(c,from,p,radius,out _))Note(c,why);
                        continue;
                    }
                    if(a==pressed[1-side])continue;
                    string? refused=!a.isActiveAndEnabled?"inactive":a.conditional!=RaycastAction.InteractionConditionals.Nothing?"needs "+a.conditional
                        :a.IsInteractionBlocked(actor)?"blocked by the game now":!a.IsActorValid(actor)?"not for the player":null;
                    if(refused!=null){if(Hit(c,from,p,radius,out _))Note(c,refused+" ("+a.name+")");continue;}
                    if(!Hit(c,from,p,radius,out var hit))continue;
                    if(!a.IsRaycastPingValid(actor,hit)){Note(c,"the game refused the touch ("+a.name+")");continue;}
                    bool valid;Injecting=true;
                    try{a.PingRaycastHittable(actor,hit,out valid);}
                    finally{Injecting=false;}
                    if(valid)
                    {pressed[side]=a;contact[side]=hit.point;latched=true;rig.PunchHaptics(rhs);Bootstrap.Write("TOUCH BUTTON "+a.name+" via="+c.name+" side="+(rhs?"R":"L")+" probe="+(probe==0?"palm":"weapon")+" ("+how+")");break;}
                    Note(c,"the game did not take the press ("+a.name+")");
                }
            }
        }
    }
    // 0.1.244: a blow (PunchDriver: a fist, or the hand with a weapon) on a
    // thing the game breaks when used (TouchControlMath.Breaks) uses it, as
    // Grip+A at it does: the game's own breaking, sound and what follows. The
    // game's own checks decide (its condition, a block, the player, the hit);
    // a thing used once is not used again for two seconds.
    private readonly Dictionary<IntPtr,float> struck=new();
    internal bool Strike(Collider collider,RaycastHit hit,IInteractionActor actor,out string note)
    {
        note="";
        var a=collider.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
        if(a==null)return false;
        TouchControlReader.Read(a,collider,false,out string events,out bool breaks);
        if(!breaks)return false;
        float now=Time.realtimeSinceStartup;
        if(struck.TryGetValue(a.Pointer,out float until)&&now<until)return false;
        string? refused=!a.isActiveAndEnabled?"inactive":a.conditional!=RaycastAction.InteractionConditionals.Nothing?"needs "+a.conditional
            :a.IsInteractionBlocked(actor)?"blocked by the game now":!a.IsActorValid(actor)?"not for the player":!a.IsRaycastPingValid(actor,hit)?"the game refused the blow":null;
        if(refused!=null){note=refused+" ("+a.name+"; on use: "+events+")";return false;}
        bool valid;Injecting=true;
        try{a.PingRaycastHittable(actor,hit,out valid);}
        finally{Injecting=false;}
        if(!valid){note="the game did not take it ("+a.name+"; on use: "+events+")";return false;}
        if(struck.Count>=64)struck.Clear();
        struck[a.Pointer]=now+2;note=a.name+" via="+collider.name+" (on use: "+events+")";return true;
    }
    private static bool Hit(Collider c,Vector3 from,Vector3 p,float radius,out RaycastHit hit)
    {
        var move=p-from;var direction=move.normalized;
        if(c.Raycast(new Ray(from-direction*(radius+.01f),direction),out hit,move.magnitude+radius*2+.02f)
            &&(p-hit.point).sqrMagnitude<=(radius+.006f)*(radius+.006f)&&Vector3.Dot(move,hit.normal)<-1e-7f)return true;
        // Off-centre contact has volume. Validate against the actual nearby
        // surface and require movement into it rather than withdrawal.
        if(!ColliderSurface.TryClosest(c,p,out var nearest))return false;
        var delta=nearest-p;float distance=delta.magnitude;
        if(distance<1e-5f||distance>radius+.006f||Vector3.Dot(move,delta)<=1e-8f)return false;
        direction=delta/distance;
        // At an edge the box ray can report either adjacent face normal.
        // The approach test above uses the sphere-to-surface normal instead.
        return c.Raycast(new Ray(p-direction*.01f,direction),out hit,distance+.02f);
    }
    // Once per collider and 15 s, at most 200 a session: what was touched and why it pressed nothing.
    private void Note(Collider c,string why)
    {
        int id=c.GetInstanceID();
        if(notes>=200||nextNote.TryGetValue(id,out float next)&&Time.realtimeSinceStartup<next)return;
        if(nextNote.Count>=256)nextNote.Clear();
        nextNote[id]=Time.realtimeSinceStartup+15;notes++;
        string path=c.name;var t=c.transform.parent;
        for(int depth=0;t!=null&&t!=player&&depth<4;depth++,t=t.parent)path+=" < "+t.name;
        Bootstrap.Write("TOUCH BUTTON nothing pressed at "+path+": "+why);
    }
    private RaycastAction? Resolve(Collider collider,out string how,out string? why)
    {
        int id=collider.GetInstanceID();
        if(targets.TryGetValue(id,out var old)&&old.collider==collider){how=old.how;why=old.why;return old.action;}
        RaycastAction? result=null;Transform? named=null;how="";why=null;
        var direct=collider.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
        var t=collider.transform;
        for(int depth=0;t!=null&&t!=player&&depth<4;depth++,t=t.parent)
            if(Button(t.name)){named=t;break;}
        if(named!=null||direct!=null&&Button(direct.name))
        {
            result=direct;
            if(result==null&&named!=null)
            {
                int budget=32;bool ambiguous=false;
                void Inspect(Transform node,int depth)
                {
                    if(budget--<=0){ambiguous=true;return;}
                    var a=node.GetComponent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
                    if(a!=null){if(result!=null&&result!=a)ambiguous=true;result=a;}
                    if(node.childCount>16){ambiguous=true;return;}
                    if(depth==0)return;
                    for(int j=0;j<node.childCount;j++){if(budget<=0){ambiguous=true;break;}Inspect(node.GetChild(j),depth-1);}
                }
                Inspect(named,2);
                // Some elevator prefabs put the generic raycast_target beside
                // the named trigger, both under the same small button object.
                if(result==null&&!ambiguous&&named.parent!=null&&named.parent!=player&&named.parent.childCount<=8)
                    Inspect(named.parent,2);
                if(ambiguous)result=null;
            }
        }
        // 0.1.208: without a name, what the interaction runs decides; with
        // one, it may still not take, raise the alarm or be a door's leaf.
        bool isNamed=result!=null;
        if(result==null&&direct!=null&&named==null)result=direct;
        if(result!=null)
        {
            var verdict=TouchControlReader.Read(result,collider,isNamed,out string events);
            if(verdict==TouchControlMath.Verdict.Control)how=(isNamed?"named":"by what it runs")+": "+events;
            else
            {
                if(!TouchControlMath.Quiet(verdict))why=TouchControlMath.Describe(verdict)+" ("+result.name+"; on use: "+events+")";
                result=null;
            }
        }
        if(targets.Count>=128)targets.Clear();targets[id]=(collider,result,how,why);return result;
    }
    private static bool Button(string name)
    {var n=name.ToLowerInvariant();return n.Contains("button")||n.Contains("switch")||n.Contains("console")||n.Contains("panel")||n.Contains("triggertogoup")||n.Contains("triggertogodown");}
}
