using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using Door=PlayMagic.AI.Door;
namespace XiiiXR;
// Authored door and cabinet hinges/sliders; native lock permissions remain in force.
internal sealed class PhysicalDoors:IDisposable
{
    private sealed class Wing
    {
        internal Door? Door;internal RaycastAction[] Actions=Array.Empty<RaycastAction>();internal CustomAnimationTool Tool=null!;internal DoorMotion Motion=null!;
        internal Transform Pivot=null!;
        internal Collider[] Colliders=Array.Empty<Collider>();internal bool Changed;internal bool Started;internal float NextInfo,NextPushCommit,NextStrike,LastMove,LastStrong,Moved;internal bool Sounded;
        // 0.1.203: the authored events its own interaction runs besides the
        // leaf's motion (DoorStoryMath); the hand's travel on it while closed.
        internal string Story="";internal float Pull,StoryTried=-100;internal bool StoryRefused;
        // 0.1.246: when its interactions are looked for again; the game's door info is not kept (it threw).
        internal float NextActions;internal int Looks;internal bool NoInfo;
    }
    private readonly List<Wing> wings=new();
    // 0.1.247: the interaction the game targets for the player now (InteractionDriver.TargetAction).
    internal static Func<RaycastAction?>? GameTarget;
    private readonly HashSet<int> scanned=new(),reportedTools=new();
    private readonly Wing?[] held=new Wing?[2];
    private readonly Vector3[] previous=new Vector3[2],previousTip=new Vector3[2];
    private readonly Wing?[] pushed=new Wing?[2];
    private readonly Wing?[] struck=new Wing?[2];
    private readonly float[] previousTime=new float[2],nextStrike=new float[2];
    private readonly Vector3[] pushNormal=new Vector3[2];
    private readonly bool[] sampled=new bool[2],consumed=new bool[2];
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> nearby=new(48);
    private Transform? player;private float nextFind,nextError,nextSound;private Wing? sounding;
    // 0.1.87: the mod's door recording when a hand starts moving a leaf.
    private readonly ChairImpactClip creak=new(DoorMoveSound.Wav,"door-move recording",.2f,14);
    internal bool ConsumesRight=>Consumes(true);
    internal bool Consumes(bool right)=>consumed[right?1:0]||held[right?1:0]!=null;
    internal bool Holding(bool right)=>held[right?1:0]!=null;
    internal void Tick(CameraRig rig,bool allowed,IInteractionActor? actor,LooseProps props)
    {
        consumed[0]=consumed[1]=false;
        if(player!=rig.PlayerRoot){Cancel();wings.Clear();reportedTools.Clear();player=rig.PlayerRoot;nextFind=0;}
        if(!allowed||actor==null||!rig.SampleWorldHands(out var l,out var r,out bool lv)){Cancel();return;}
        try
        {
            if(Time.realtimeSinceStartup>=nextFind){nextFind=Time.realtimeSinceStartup+.4f;Discover(CameraRig.UnityPosition(l));Discover(CameraRig.UnityPosition(r));}
            Step(0,l,lv,rig.LeftControls,rig,actor,props);Step(1,r,true,rig.RightControls,rig,actor,props);
            foreach(var w in wings)if(w.Changed&&w!=held[0]&&w!=held[1]&&Time.realtimeSinceStartup>=w.NextPushCommit)Commit(w);
            // The recording ends when the leaf stops moving.
            if(sounding!=null&&Time.realtimeSinceStartup-sounding.LastStrong>.15f){creak.Stop();sounding=null;}
        }
        catch(Exception ex){Cancel();if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("PHYSICAL DOOR: "+ex);}}
    }
    private void Discover(Vector3 hand)
    {
        wings.RemoveAll(w=>w.Tool==null||w.Pivot==null||(w!=held[0]&&w!=held[1]&&(w.Pivot.position-hand).sqrMagnitude>25));
        scanned.Clear();int budget=128;var walked=new HashSet<int>();
        int count=Physics.OverlapSphereNonAlloc(hand,.8f,nearby,~0,QueryTriggerInteraction.Collide);
        for(int i=0;i<count&&i<nearby.Length;i++)
        {
            var collider=nearby[i];if(collider==null)continue;
            var door=collider.GetComponentInParent(Il2CppType.Of<Door>())?.TryCast<Door>();
            if(door!=null&&door.doorReferences!=null)foreach(var tool in door.doorReferences)
                if(tool!=null&&!wings.Exists(w=>w.Tool==tool))Bind(tool,door,DoorActions(door,tool,collider));
            // Cabinets put their animation component above the glass/handle,
            // sometimes on an unnamed sibling. Search a bounded local tree;
            // no scene-wide scan and no English object-name requirement.
            var parent=collider.transform;
            for(int depth=0;parent!=null&&depth<8;depth++,parent=parent.parent)
            {
                Inspect(parent,door,collider);
                if(budget>0&&parent.childCount<=24)
                    for(int child=0;child<parent.childCount&&budget>0;child++)InspectTree(parent.GetChild(child),door,collider,2);
            }
        }
        // 0.1.247: the interaction the game targets for the player now (what
        // Grip+A would use) is one of a nearby leaf's (it belongs to its door
        // or moves it) but not among the ones found: it is taken too.
        RaycastAction? target=null;try{target=GameTarget?.Invoke();}catch(Exception){}
        if(target!=null)foreach(var w in wings)
        {
            if(w.Tool==null||System.Array.IndexOf(w.Actions,target)>=0||(w.Pivot.position-hand).sqrMagnitude>4)continue;
            var owner=target.GetComponentInParent(Il2CppType.Of<Door>())?.TryCast<Door>();
            if(!(w.Door!=null&&owner!=null&&owner==w.Door)&&!DoorStoryEvents.Animates(target,w.Tool))continue;
            var more=new RaycastAction[w.Actions.Length+1];System.Array.Copy(w.Actions,more,w.Actions.Length);more[^1]=target;
            foreach(var other in wings)if(other.Tool==w.Tool)other.Actions=more;
            Bootstrap.Write("PHYSICAL DOOR "+w.Tool.name+": the interaction the game targets ("+target.name+") is this door's too; the hand uses it as Grip+A would");
        }
        // 0.1.246: a leaf bound without its interaction is looked at again
        // every two seconds while near (the game may set its door up later).
        foreach(var w in wings)
        {
            if(w.Actions.Length>0||w.Tool==null||Time.realtimeSinceStartup<w.NextActions||(w.Pivot.position-hand).sqrMagnitude>4)continue;
            w.NextActions=Time.realtimeSinceStartup+(w.Looks++<3?2:15);
            var found=w.Door!=null?DoorActions(w.Door,w.Tool,null):AnimatingActions(w.Tool,null,null);
            if(found.Length==0)continue;
            foreach(var other in wings)if(other.Tool==w.Tool)other.Actions=found;
            Bootstrap.Write("PHYSICAL DOOR "+w.Tool.name+": its interaction found now ("+string.Join("/",System.Linq.Enumerable.Select(found,a=>a.name))+"); the hand moves it");
        }
        void InspectTree(Transform node,Door? door,Collider touched,int depth)
        {
            if(budget<=0||!walked.Add(node.GetInstanceID()))return;budget--;Inspect(node,door,touched);
            if(depth>0&&node.childCount<=24)for(int i=0;i<node.childCount&&budget>0;i++)InspectTree(node.GetChild(i),door,touched,depth-1);
        }
        void Inspect(Transform node,Door? door,Collider touched)
        {
            if(!scanned.Add(node.GetInstanceID()))return;
            foreach(var obj in node.GetComponents(Il2CppType.Of<CustomAnimationTool>()))
            {
                var tool=obj.TryCast<CustomAnimationTool>();if(tool==null||wings.Exists(w=>w.Tool==tool))continue;
                var motions=new List<DoorMotion>(DoorMotion.Read(tool));
                if(motions.Count==0)
                {
                    if(reportedTools.Count<128&&reportedTools.Add(tool.GetInstanceID()))
                    {
                        string detail="";
                        if(tool.customAnimationQueue!=null)foreach(var q in tool.customAnimationQueue)
                            if(q!=null)detail+=" queue="+(int)q.queueSettings+" elements="+(q.animationQueue?.Count??0);
                        Bootstrap.Write("CABINET unsupported animation="+tool.name+" type="+(int)tool.animationType+" trigger="+(int)tool.animationTrigger+" contact="+touched.name+detail);
                    }
                    continue;
                }
                // Only the collider's actual rotating assembly may be bound.
                bool owns=false;
                for(int i=0;i<count&&i<nearby.Length;i++)
                {
                    var candidate=nearby[i];if(candidate==null)continue;
                    foreach(var motion in motions)if(candidate.transform.IsChildOf(motion.Pivot))owns=true;
                    if(tool.meshColliders!=null)foreach(var c in tool.meshColliders)if(c==candidate)owns=true;
                }
                if(!owns)continue;
                var actions=new List<RaycastAction>();
                foreach(var c in tool.GetComponentsInChildren(Il2CppType.Of<RaycastAction>(),true))
                {var action=c.TryCast<RaycastAction>();if(action!=null&&!actions.Contains(action))actions.Add(action);}
                var permission=tool.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
                if(permission!=null&&!actions.Contains(permission))actions.Add(permission);
                permission=touched.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>();
                if(permission!=null&&!actions.Contains(permission))actions.Add(permission);
                // 0.1.246: none there: the interaction beside it that moves this leaf when used.
                if(actions.Count==0)actions.AddRange(AnimatingActions(tool,door,touched));
                Bind(tool,door,actions.ToArray());
            }
        }
    }
    // 0.1.246: a door's interactions. The game's list on the door
    // (doorRaycastTargets) was empty on some doors (the emerald base's toilet
    // doors: the game had not set that door up, its door info empty too), so
    // the hand could not move them and only Grip+A opened them. Then the
    // door's own interaction is the one that moves this leaf when used: the
    // door's set one, or one found under the door, around the leaf or above
    // the touched collider that animates it (DoorStoryEvents.Animates). A door
    // with no such interaction (a story gate) stays as it was: not moved.
    // 0.1.247: and always the others that move this leaf when used: a door
    // can list only one of its interactions (door_13_b in the emerald base
    // lists "- all"; the player's own, "- player only", the one Grip+A uses,
    // was not on the list, and the hand could not open that door).
    private static RaycastAction[] DoorActions(Door door,CustomAnimationTool tool,Collider? touched)
    {
        var listed=new List<RaycastAction>();
        if(door.doorRaycastTargets!=null)foreach(var a in door.doorRaycastTargets)if(a!=null&&!listed.Contains(a))listed.Add(a);
        foreach(var a in AnimatingActions(tool,door,touched,listed.Count>0))if(!listed.Contains(a))listed.Add(a);
        return listed.ToArray();
    }
    private static RaycastAction[] AnimatingActions(CustomAnimationTool tool,Door? door,Collider? touched,bool listed=false)
    {
        var found=new List<RaycastAction>();
        void Consider(RaycastAction? a){if(a!=null&&!found.Contains(a)&&DoorStoryEvents.Animates(a,tool))found.Add(a);}
        void Under(Transform? t){if(t!=null)foreach(var c in t.GetComponentsInChildren(Il2CppType.Of<RaycastAction>(),true))Consider(c.TryCast<RaycastAction>());}
        try
        {
            if(door!=null){Consider(door.m_raycastAction);Under(door.transform);}
            Under(tool.transform);
            Consider(tool.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>());
            if(touched!=null)Consider(touched.GetComponentInParent(Il2CppType.Of<RaycastAction>())?.TryCast<RaycastAction>());
            // The prefab's root (the leaf, its frame and its interaction side by side), bounded.
            var root=(door!=null?door.transform:tool.transform).parent;
            for(int up=0;found.Count==0&&root!=null&&up<2;up++,root=root.parent)if(root.childCount<=24)Under(root);
        }
        catch(Exception ex){Bootstrap.Warn("PHYSICAL DOOR "+tool.name+" interaction search: "+ex.Message);}
        if(!listed&&found.Count>0&&reportedFallback.Count<64&&reportedFallback.Add(tool.GetInstanceID()))
            Bootstrap.Write("PHYSICAL DOOR "+tool.name+": the door's own list of interactions is empty; the one that moves this leaf is used ("+string.Join("/",System.Linq.Enumerable.Select(found,a=>a.name))+")");
        return found.ToArray();
    }
    private static readonly HashSet<int> reportedFallback=new();
    private void Bind(CustomAnimationTool tool,Door? door,RaycastAction[] actions)
    {
        string events="",story="";bool read=false;
        foreach(var motion in DoorMotion.Read(tool))
        {
            var pivot=motion.Pivot;var colliders=new List<Collider>();
            foreach(var c in pivot.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))
            {var collider=c.TryCast<Collider>();if(collider!=null)colliders.Add(collider);}
            if(colliders.Count==0&&tool.meshColliders!=null)foreach(var c in tool.meshColliders)if(c!=null&&!c.isTrigger)colliders.Add(c);
            if(colliders.Count==0)continue;
            if(!read){read=true;events=DoorStoryEvents.Find(actions,tool,door,pivot.position,out story);}
            wings.Add(new Wing{Door=door,Actions=actions,Tool=tool,Motion=motion,Pivot=pivot,Colliders=colliders.ToArray(),Story=story});
            Bootstrap.Write("PHYSICAL DOOR bound="+tool.name+" pivot="+pivot.name+" mode="+(motion.Queue==null?"state":"reversible queue")+" slide="+motion.Sliding+" range="+motion.Low+".."+motion.High
                +" interaction="+(actions.Length==0?"none":string.Join("/",System.Linq.Enumerable.Select(actions,a=>a==null?"?":a.name)))+" events: "+events
                +(story.Length>0?"; story events, so from closed it opens by its own interaction":""));
        }
    }

    // The hand may move a leaf when Grip+A could open it now: one of its
    // interactions the player may use (active, for the player, not blocked).
    // 0.1.247: an interaction not for the player (an AI's), or one switched
    // off or blocked beside the player's own, no longer keeps a door shut that
    // Grip+A opens; a lock not yet opened (a key, a card, a lockpick) still
    // keeps it shut, whichever of its interactions has it.
    private static bool Unlocked(Wing w,IInteractionActor actor)
    {
        if(w.Tool==null||w.Pivot==null||!w.Tool.gameObject.activeInHierarchy||(w.Door!=null&&w.Door.WaitForSpecialDoorSetup))return false;
        bool usable=false;
        foreach(var action in w.Actions)
        {
            if(action==null||!action.isActiveAndEnabled||!action.IsActorValid(actor))continue;
            if(action.conditional!=RaycastAction.InteractionConditionals.Nothing&&!action.GetConditionState())return false;
            if(!action.IsInteractionBlocked(actor))usable=true;
        }
        // Missing permissions are not permission to move a story door.
        return usable||(w.Door==null&&w.Actions.Length==0);
    }
    private static float Near(Wing w,Vector3 p)
    {
        float nearest=float.MaxValue;
        foreach(var c in w.Colliders)if(c!=null&&c.enabled&&c.gameObject.activeInHierarchy&&ColliderSurface.TryClosest(c,p,out var point))nearest=Math.Min(nearest,(point-p).magnitude);
        return nearest;
    }
    private void Step(int side,PoseValue pose,bool valid,HandControls input,CameraRig rig,IInteractionActor actor,LooseProps props)
    {
        if(!valid||!input.Valid||props.Holding(side==1)){Release(side);sampled[side]=false;return;}
        var p=CameraRig.UnityPosition(pose);var tip=p;
        float nowTime=Time.realtimeSinceStartup,dt=nowTime-previousTime[side];
        bool gripHeld=(input.Held&HandControls.Grip)!=0;
        bool free=WeaponHands.Current?.HandFree(side==1)!=false&&(side==1||GripCarry.Current?.HidesLeft!=true);
        // 0.1.156: either hand's weapon pushes with its far end (the game's
        // weapon in the left hand, or a copy in either hand).
        bool armed=false;
        if(!free&&WeaponHands.Current?.MeleeHand(side==1)==true&&WeaponHands.Current.TryMeleeTip(side==1,out var weaponTip)&&(weaponTip-p).sqrMagnitude<1){tip=weaponTip;armed=true;}
        else if(side==1&&!free&&WeaponHands.Current?.TryMeleeTip(out var gameTip)==true&&(gameTip-p).sqrMagnitude<1)tip=gameTip;
        if(struck[side]!=null&&Near(struck[side]!,tip)>.22f)struck[side]=null;
        if(sampled[side]&&(p-previous[side]).sqrMagnitude>.25f){Release(side);sampled[side]=false;}
        if(held[side]!=null)
        {
            var w=held[side]!;consumed[side]=true;
            if(!free||!gripHeld||!Unlocked(w,actor)||Near(w,p)>.65f){Release(side);}
            else if(sampled[side])
            {
                var story=OpenStory(side,w,actor,previous[side],p);
                // Opened by its interaction: the native swing runs to its end
                // (the leaf leaves the hand; its end events are native too).
                if(story==StoryOpened){Release(side);rig.PunchHaptics(side==1);}
                else if(story==NotStory)Move(w,previous[side],p);
            }
        }
        else
        {
            Wing? best=null;float distance=.16f;
            foreach(var w in wings)
            {
                if(w==struck[side]||w==held[1-side])continue;
                if(nowTime<w.NextStrike)
                {
                    // Both hands can reach one leaf in the same frame. Consume
                    // that contact too; a continued touch must not reopen it.
                    if(Near(w,free?p:tip)<=.07f)struck[side]=w;
                    continue;
                }
                if(w.Motion.Playing||(w.Pivot.position-p).sqrMagnitude>9)continue;
                float d=Near(w,free?p:tip);if(d<distance&&Unlocked(w,actor)){distance=d;best=w;}
            }
            if(best==null){pushed[side]=null;}
            if(best!=null)
            {
                if(free&&gripHeld)
                {held[side]=best;consumed[side]=true;rig.PunchHaptics(side==1);Bootstrap.Write("PHYSICAL DOOR grip="+best.Tool.name);}
                else if(sampled[side]&&(free||side==1||armed))
                {
                    var before=free?previous[side]:previousTip[side];var now=free?p:tip;
                    // Native interaction toggles the settled door: an open leaf
                    // closes on the same fast contact that opens a closed one.
                    {
                        if(pushed[side]!=best)pushed[side]=null;
                        foreach(var collider in best.Colliders)
                        {
                            if(collider==null||!ColliderSurface.TryClosest(collider,before,out var closest))continue;
                            var outward=before-closest;
                            if(outward.sqrMagnitude<.000004f)continue;
                            pushNormal[side]=Quaternion.Inverse(best.Pivot.rotation)*outward.normalized;pushed[side]=best;break;
                        }
                    }
                    var normal=best.Pivot.rotation*pushNormal[side];
                    float inward=-Vector3.Dot(now-before,normal);
                    int story=pushed[side]==best&&inward>.0001f?OpenStory(side,best,actor,before,now):NotStory;
                    if(story==StoryOpened)rig.PunchHaptics(side==1);
                    else if(story==NotStory&&pushed[side]==best&&inward>.0001f)
                    {
                        if(DoorStrikeMath.FastContact(inward,dt,distance,best.Motion.Value,nowTime>=nextStrike[side]&&nowTime>=best.NextStrike)&&TryToggle(best,actor,before,now))
                        {struck[side]=best;nextStrike[side]=best.NextStrike=nowTime+.65f;rig.PunchHaptics(side==1);}
                        else {Move(best,before,now);if(nowTime>=best.NextPushCommit){Commit(best);best.NextPushCommit=nowTime+.15f;}}
                    }
                }
            }
        }
        previous[side]=p;previousTip[side]=tip;previousTime[side]=nowTime;sampled[side]=true;
    }
    private static bool TryToggle(Wing w,IInteractionActor actor,Vector3 before,Vector3 now)
    {
        if(!Unlocked(w,actor))return false;
        foreach(var action in w.Actions)
        {
            if(action==null||!action.isActiveAndEnabled||!action.IsActorValid(actor)||action.IsInteractionBlocked(actor))continue;
            if(action.conditional!=RaycastAction.InteractionConditionals.Nothing&&!action.GetConditionState())continue;
            var hit=new RaycastHit{point=now,distance=(now-before).magnitude};
            if(!action.IsRaycastPingValid(actor,hit))continue;
            float fromState=w.Motion.Value;
            action.PingRaycastHittable(actor,hit,out bool valid);
            if(valid){w.Changed=false;Bootstrap.Write("DOOR SWING native toggle="+action.name+" fromState="+fromState);return true;}
        }
        if(w.Door==null&&w.Actions.Length==0)
        {w.Tool.StartCustomAnimationTool(actor,1);w.Changed=false;return true;}
        return false;
    }
    // How far the hand's move from `from` to `to` turns (degrees) or slides (metres) the leaf.
    private static float Delta(Wing w,Vector3 from,Vector3 to)
    {
        var m=w.Motion;var localAxis=m.Axis==0?Vector3.right:m.Axis==1?Vector3.up:Vector3.forward;
        var parent=w.Pivot.parent;var axis=parent==null?localAxis:parent.rotation*localAxis;
        return m.Sliding?Vector3.Dot(to-from,axis)/(parent==null?1:Math.Max(.0001f,parent.TransformVector(localAxis).magnitude))
            :PhysicalHandsMath.HingeDelta(ContactWorld.V(from),ContactWorld.V(to),ContactWorld.V(w.Pivot.position),ContactWorld.V(axis));
    }
    // 0.1.203: a door whose own
    // interaction runs authored events besides its leaf's motion (the bank's
    // hostage door lets the guards in) opens from closed only by that native
    // interaction, as Grip+A or a strike opens it; the hand's motion alone ran
    // none of them. The hand first has to mean it (StoryPull of travel), then
    // the door swings open natively. An open story door, and every ordinary
    // door, follow the hand as before.
    internal const int NotStory=0,StoryWaiting=1,StoryOpened=2;
    internal const float StoryPullDegrees=3,StoryPullSlide=.015f;
    private int OpenStory(int side,Wing w,IInteractionActor actor,Vector3 from,Vector3 to)
    {
        if(w.Story.Length==0||w.StoryRefused||w.Pivot==null)return NotStory;
        if(w.Motion.Playing||Math.Abs(w.Motion.Value)>=.03f){w.Pull=0;w.StoryTried=-100;return NotStory;}
        w.Pull+=Delta(w,from,to);
        if(Math.Abs(w.Pull)<(w.Motion.Sliding?StoryPullSlide:StoryPullDegrees))return StoryWaiting;
        w.Pull=0;float now=Time.realtimeSinceStartup;
        if(now<w.NextStrike)return StoryWaiting;
        // Never leave a door shut: refused, or its interaction did not swing
        // it open (still shut and still a few seconds after), its leaf
        // follows the hand as before.
        bool unopened=now-w.StoryTried<5;
        if(unopened||!TryToggle(w,actor,from,to))
        {
            w.StoryRefused=true;Bootstrap.Write("DOOR STORY "+w.Tool.name+": its interaction "+(unopened?"did not open it":"refused")+"; the leaf follows the hand ("+(unopened?"its events ran: ":"events not run: ")+w.Story+")");
            return NotStory;
        }
        w.StoryTried=now;struck[side]=w;nextStrike[side]=w.NextStrike=now+.65f;
        Bootstrap.Write("DOOR STORY "+w.Tool.name+" opened from closed by its own interaction (its events: "+w.Story+")");
        return StoryOpened;
    }
    private void Move(Wing w,Vector3 from,Vector3 to)
    {
        if(w.Pivot==null)return;
        var m=w.Motion;
        float delta=Delta(w,from,to);
        float offset=m.Offset,next=Math.Clamp(offset+delta,m.Low,m.High);
        if(Math.Abs(next-offset)<(m.Sliding?.0001f:.05f))return;
        if(!w.Started){w.Started=true;w.Tool.OnAnimationStarted?.Invoke(w.Tool,Math.Abs(m.Value)<.03f);}
        m.Set(next,w.Tool);
        // A fresh movement (door at rest for a moment) plays the recording once;
        // a continuous swing does not restart it.
        // 0.1.88: only a real movement plays it (a grip drag of a few degrees
        // or a longer push), never a brush against a locked leaf.
        float now=Time.realtimeSinceStartup;
        if(now-w.LastMove>.5f){w.Moved=0;w.Sounded=false;}
        w.Moved+=Math.Abs(next-offset);w.LastMove=now;
        if(Math.Abs(next-offset)>=(m.Sliding?.0015f:.35f))w.LastStrong=now;
        bool grip=w==held[0]||w==held[1];
        float needed=m.Sliding?(grip?.012f:.04f):(grip?3f:8f);
        if(!w.Sounded&&w.Moved>=needed&&now>=nextSound){w.Sounded=true;sounding=w;w.LastStrong=now;nextSound=now+1.2f;creak.Play(w.Pivot.position+(to-w.Pivot.position)*.5f);}
        if(w.Door!=null&&Time.realtimeSinceStartup>=w.NextInfo){DoorInfo(w,false);w.NextInfo=Time.realtimeSinceStartup+.15f;}w.Changed=true;
    }

    private static void Commit(Wing w)
    {
        if(w.Changed&&w.Tool!=null)
        {
            if(w.Door!=null)DoorInfo(w,true);
            if(Math.Abs(w.Motion.Value)<.03f||Math.Abs(w.Motion.Value)>.97f)
            {w.Motion.Finish(w.Tool);w.Started=false;}
            w.Changed=false;
        }
    }
    // 0.1.246: the game's door info (what its AI and saves read) when the hand
    // moved the leaf. A door the game had not set up has none (the toilet
    // doors: IndexOutOfRange, and every door the hands held let go); its leaf
    // still moves, without that info.
    private static void DoorInfo(Wing w,bool end)
    {
        if(w.Door==null||w.NoInfo||w.Tool==null)return;
        try
        {
            var info=w.Door.m_doorsInfo;
            if(info==null||info.Length==0){w.NoInfo=true;Bootstrap.Write("PHYSICAL DOOR "+w.Tool.name+": the game has no door info for it (not set up); the leaf moves without it");return;}
            w.Door.UpdateDoorInfo(w.Tool,end);
        }
        catch(Exception ex){w.NoInfo=true;Bootstrap.Write("PHYSICAL DOOR "+w.Tool.name+": the game's door info not kept ("+ex.GetBaseException().Message.Split('\n')[0]+"); the leaf moves without it");}
    }
    private void Release(int side){if(held[side]!=null)Commit(held[side]!);held[side]=null;}
    internal void ResumeNative(CustomAnimationTool tool){foreach(var w in wings)if(w.Tool==tool)w.Motion.ResumeNative();}
    internal void Cancel(){if(sounding!=null){creak.Stop();sounding=null;}for(int i=0;i<2;i++){Release(i);sampled[i]=false;consumed[i]=false;struck[i]=null;previousTime[i]=0;}}
    public void Dispose(){Cancel();wings.Clear();creak.Dispose();}
}
