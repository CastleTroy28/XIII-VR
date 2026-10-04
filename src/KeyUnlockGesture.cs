using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// PingRaycastHittable STARTS the native item-use coroutine. ResolveConditional
// COMPLETES it (verified in this game's native binary). Never start that coroutine in VR.
internal sealed class KeyUnlockGesture:IDisposable
{
    private readonly CameraRig rig;private readonly UnlockGestureMath gesture=new();
    private RaycastAction? action;private IInteractionActor? actor;private PlayerEquipableInventory? inventory;
    private Equipable? item;private Equipable? previousItem;private HeldItemVisual? visual;
    private Collider? fallbackSurface;
    // 0.1.203: the reader's colliders (the aimed one, the reader's own) and
    // the drawn card's points, for holding the card to the reader.
    private readonly System.Collections.Generic.List<Collider> reader=new();
    private readonly Vector3[] cardProbe=new Vector3[HeldItemVisual.ProbePoints];
    private Transform? anchor;private Vector3 targetLocal;private bool card,pick;
    internal static RaycastAction? Completing;
    internal bool Active=>visual!=null;
    internal string GripProfile=>card?"card":pick?ScrewdriverGrip.Profile:"key";
    // 0.1.103: a lockpick lock is opened like a key: bring the pick to the
    // lock and turn it 45 degrees.
    internal bool Lockpick=>Active&&pick;
    internal void PrepareHand(NativeHandVisual hand)=>visual?.PreparePinch(hand);
    internal KeyUnlockGesture(CameraRig rig){this.rig=rig;}
    internal static bool LockedKey(RaycastAction? a)=>a!=null&&!a.GetConditionState()&&(a.conditional==RaycastAction.InteractionConditionals.Key||a.conditional==RaycastAction.InteractionConditionals.Keycard
        ||a.conditional==RaycastAction.InteractionConditionals.Lockpick);
    internal bool Intercept(RaycastAction candidate,IInteractionActor caller,RaycastSystem? ray,RaycastHit hit)
    {
        if(caller==null||ray==null||ray.InteractionActor==null||caller.GetActorID()!=ray.InteractionActor.GetActorID())return true;
        if(!LockedKey(candidate))return true;
        // Once this is a local key interaction, never fall back to an automatic arm animation.
        if(Active)return false;
        if(!candidate.IsRaycastPingValid(caller,hit)||!candidate.IsActorValid(caller)||candidate.IsInteractionBlocked(caller))return false;
        var inv=caller.GetGameObject()?.GetComponentInChildren(Il2CppType.Of<PlayerEquipableInventory>(),true)?.TryCast<PlayerEquipableInventory>();
        bool isCard=candidate.conditional==RaycastAction.InteractionConditionals.Keycard;
        bool isPick=candidate.conditional==RaycastAction.InteractionConditionals.Lockpick;
        if(inv==null||inv.IsInventoryBlocked||inv.isInTransit||!inv.HasItemToResolveConditional(candidate.conditional,isCard?candidate.keyCardTypeToCheck:candidate.keyTypeToCheck))return false;
        var key=inv.GetEquipableFromSlot(isCard?PlayerEquipableInventory.ActiveEquipmentSlot.Card:isPick?PlayerEquipableInventory.ActiveEquipmentSlot.Lockpick:PlayerEquipableInventory.ActiveEquipmentSlot.Key);
        if(key==null)return false;
        try{visual=KeyPreviewFactory.Create(key,inv);}catch(Exception e){Bootstrap.Warn("KEY preview unavailable: "+e.Message);return false;}
        action=candidate;actor=caller;inventory=inv;item=key;previousItem=inv.currentEquipable;card=isCard;pick=isPick;
        anchor=FindAnchor(candidate,hit.point,isCard);targetLocal=anchor.InverseTransformPoint(hit.point);
        if(anchor!=candidate.transform)targetLocal=Vector3.zero;
        fallbackSurface=anchor==candidate.transform?hit.collider:null;
        reader.Clear();if(isCard)CollectReader(candidate,anchor,hit.collider);
        gesture.Reset();rig.DisarmTrigger();NativeItemCue.Play(key,AudioComponent.AudioTrigger.Equip,hit.point);
        Bootstrap.Write("KEY DRAW "+(card?"card":pick?"lockpick":"key")+" target="+candidate.name+" anchor="+anchor.name+" native use coroutine suppressed");return false;
    }
    internal static bool IsPhysicalDoor(RaycastAction action)
    {
        try{return action.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.Door>())!=null;}
        catch(Exception){return false;}
    }
    private static Transform FindAnchor(RaycastAction candidate,Vector3 hit,bool card)
    {
        var door=candidate.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.Door>())?.TryCast<PlayMagic.AI.Door>();
        var root=door!=null?door.transform:candidate.transform.parent??candidate.transform;
        Transform best=candidate.transform;float distance=1.5f;
        foreach(var c in root.GetComponentsInChildren(Il2CppType.Of<Transform>(),true))
        {
            var t=c.TryCast<Transform>();if(t==null||t==candidate.transform)continue;
            string n=t.name.ToLowerInvariant();bool named=card?(n.Contains("reader")||n.Contains("keypad")||n.Contains("card")):(n.Contains("keyhole")||n.Contains("handle")||n.Contains("lock"));
            if(!named||n.Contains("raycast")||n.Contains("trigger"))continue;
            float d=(t.position-hit).magnitude;if(d<distance){distance=d;best=t;}
        }
        return best;
    }
    private void CollectReader(RaycastAction candidate,Transform anchor,Collider? aimed)
    {
        void Add(Collider? c){if(c!=null&&reader.Count<24&&!reader.Contains(c))reader.Add(c);}
        Add(aimed);
        foreach(var root in new[]{candidate.transform,anchor})
            foreach(var o in root.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))Add(o.TryCast<Collider>());
    }
    // Whether the drawn card touches the reader: a point of the card (the
    // controller's tip while the card is not drawn yet) within CardTouch of
    // one of the reader's colliders or inside it; without any, within
    // CardNear of the reader itself.
    private bool CardTouches(Vector3 tip,Vector3 target)
    {
        int n=visual?.WorldProbe(cardProbe)??0;bool any=false;float best=float.MaxValue;
        foreach(var c in reader)
        {
            if(c==null||!c.enabled||!c.gameObject.activeInHierarchy)continue;any=true;
            if(n==0)best=Math.Min(best,Gap(c,tip));
            else for(int i=0;i<n;i++)best=Math.Min(best,Gap(c,cardProbe[i]));
        }
        if(any)return best<=UnlockGestureMath.CardTouch;
        if(n==0)best=(tip-target).magnitude;else for(int i=0;i<n;i++)best=Math.Min(best,(cardProbe[i]-target).magnitude);
        return best<=UnlockGestureMath.CardNear;
    }
    private static float Gap(Collider c,Vector3 p)
    {
        if(ColliderSurface.TryClosest(c,p,out var q))return (q-p).magnitude;
        // A non-convex mesh: its box.
        var b=c.bounds;return Vector3.Max(b.min-p,Vector3.Max(Vector3.zero,p-b.max)).magnitude;
    }
    internal void Render(Transform? hand){if(Active)visual?.Pose(hand);}
    internal void Tick(bool allowed)
    {
        if(!Active)return;
        if(!allowed||action==null||anchor==null||actor==null||inventory==null||item==null||inventory.isInTransit||inventory.IsInventoryBlocked
            ||inventory.currentEquipable!=previousItem||!action.isActiveAndEnabled||action.IsInteractionBlocked(actor)||!rig.SampleWorldHands(out var leftHand,out var rightHand,out bool leftValid)){Cancel();return;}
        // 0.1.150: the key is in the taking hand (the left one for a left-hander); its B (Y) puts it away.
        bool lefty=WeaponHands.LeftHanded;if(lefty&&!leftValid){Cancel();return;}
        var hand=lefty?leftHand:rightHand;
        if(((lefty?rig.LeftControls:rig.RightControls).Down&HandControls.B)!=0){Cancel();return;}
        var p=CameraRig.UnityPosition(hand);var q=ControllerAim.Rotation(hand);
        // The tip is in front of the controller; use it for reaching the lock.
        var contact=p+q*new Vector3(0,0,.10f);var target=anchor.TransformPoint(targetLocal);
        // Unnamed doors: accept the lock region on this same interaction surface,
        // rather than forcing the player back to the original aim pixel.
        if(fallbackSurface!=null&&ColliderSurface.TryClosest(fallbackSurface,contact,out var nearest))target=nearest;
        // 0.1.220: a pick turned in the lock: the game's own time is counted down first.
        if(pickStarted>=0){TickPicking(contact,target);return;}
        if(card?!gesture.Card(CardTouches(contact,target),ContactWorld.V(contact),ContactWorld.V(target),Time.realtimeSinceStartup)
            :!gesture.Sample(false,ContactWorld.V(contact),new System.Numerics.Quaternion(q.x,q.y,q.z,q.w),ContactWorld.V(target),Time.realtimeSinceStartup))return;
        if(pick&&BeginPicking(target))return;
        Complete(target);
    }
    private void Complete(Vector3 target)
    {
        if(action==null||actor==null||inventory==null||item==null)return;
        var pending=action;var who=actor;var inv=inventory;var key=item;
        if(!inv.HasItemToResolveConditional(pending.conditional,card?pending.keyCardTypeToCheck:pending.keyTypeToCheck)){Cancel();return;}
        bool usedPick=pick;
        // A card reader is a separate action: its receivers unlock the actual
        // door, update indicators and advance authored events. Suppressing its
        // TriggerInteractions only marked the reader resolved, leaving the door
        // locked. Preserve that native chain. A direct key still waits for a
        // separate push/press.
        bool previousResolveMode=pending.resolveConditionalWhenResolved;
        // Only a real hinged door may wait for a separate push. Other locked
        // targets (the car before the bank, gates, scripted objects) have no
        // second interaction: their TriggerInteractions IS the authored event
        // (mission end, cutscene). Deferring it there left the mission stuck.
        bool physicalDoor=!card&&IsPhysicalDoor(pending);
        Completing=physicalDoor?pending:null;
        try
        {
            // The native use coroutine sets this dispatch mode before its card
            // branch (MoveNext RVA 0x195d54b). VR skips that coroutine, so restore
            // the same mode for this already-validated completion only. This is
            // not conditionalResolved/conditionState and grants no ownership.
            // A key on a non-door target (the car before the bank) gets the same
            // native dispatch as a card: its receivers carry the authored event.
            if(!physicalDoor)pending.resolveConditionalWhenResolved=true;
            pending.ResolveConditional(who);
        }
        finally{pending.resolveConditionalWhenResolved=previousResolveMode;Completing=null;}
        if(!pending.GetConditionState()){gesture.Reset();Bootstrap.Warn("KEY unlock refused by native conditional");return;}
        pending.NotifyAchievementsAboutUnlocking(pending.conditional);
        // 0.1.84: the player's own recording of a key turning, at the moment
        // the hand completes the turn. Cards keep their native reader cue.
        if(card||usedPick||!keyTurn.Play(target))NativeItemCue.Play(key,AudioComponent.AudioTrigger.KeyItem,target);
        if(usedPick)keyTurn.Play(target);
        bool remove=key.CurrentEquipableParameters?.removeAfterUse==true;
        bool usedCard=card;Cancel();if(remove)inv.Remove(key);
        rig.PunchHaptics(true);Bootstrap.Write("KEY UNLOCKED "+pending.name+(usedPick?" with lockpick":"")+(usedCard?"; native card reader events completed":physicalDoor?"; door waits for touch or fresh Grip+A":"; native key events completed (not a door)"));
    }
    private readonly ChairImpactClip keyTurn=new(KeyTurnSound.Wav,"key-turn recording",1,12);
    // 0.1.220: picking the lock (the game's lockpicking: its time, its HUD
    // countdown, its picking sound and noise), from the turn of the pick.
    private float pickStarted=-1,pickTime,nextNoise;private bool pickNoiseFailed,pickSounding;
    private FMOD.Studio.EventInstance pickSound;private LockpickComponent? lockpick;
    internal bool Picking=>pickStarted>=0;
    private bool BeginPicking(Vector3 target)
    {
        if(item==null)return false;
        float configured=float.NaN;bool instant=false;
        try{lockpick=item.GetOptionalEquipableComponent<LockpickComponent>();if(lockpick!=null){configured=lockpick.lockpickTime;instant=lockpick.playerState!=null&&lockpick.playerState.InstantLockPick;}}
        catch(Exception ex){lockpick=null;Bootstrap.Warn("LOCKPICK time unreadable: "+ex.Message);}
        if(instant){Bootstrap.Write("LOCKPICK instant (the game's instant lockpicking is on)");return false;}
        pickTime=LockpickTimerMath.Time(configured);pickStarted=Time.realtimeSinceStartup;nextNoise=0;pickNoiseFailed=false;
        try{GameUIManager.ToggleLockpickingHud(true,0);GameUIManager.UpdateLockpickingHud(pickTime,0);}
        catch(Exception ex){Bootstrap.Warn("LOCKPICK HUD: "+ex.Message);}
        PickSound(target);rig.PunchHaptics(!WeaponHands.LeftHanded);
        Bootstrap.Write("LOCKPICK turned in "+(action!=null?action.name:"?")+": picking for "+pickTime.ToString("F1")+" s"+(float.IsFinite(configured)?" (the game's lockpick time)":" (the game's time unreadable: the default)")+"; pulled out of the lock it stops");
        return true;
    }
    private void TickPicking(Vector3 contact,Vector3 target)
    {
        float now=Time.realtimeSinceStartup;
        if(LockpickTimerMath.PulledOut((contact-target).magnitude)){StopPicking("pulled out of the lock: turn it again to start over");gesture.Reset();return;}
        float left=LockpickTimerMath.Remaining(pickStarted,pickTime,now);
        try{GameUIManager.UpdateLockpickingHud(left,0);}catch(Exception){}
        // The game's own picking noise (guards nearby can hear it).
        if(!pickNoiseFailed&&now>=nextNoise)
        {
            nextNoise=now+.25f;
            try{var noise=lockpick?.noiseComponent;if(noise!=null)noise.GenerateNoise(5,true,0f);}
            catch(Exception ex){pickNoiseFailed=true;Bootstrap.Warn("LOCKPICK noise: "+ex.Message);}
        }
        // Its picking sound, started again whenever it ends while picking.
        if(pickSounding&&pickSound.getPlaybackState(out var state)==FMOD.RESULT.OK&&state==FMOD.Studio.PLAYBACK_STATE.STOPPED){ReleasePickSound();PickSound(target);}
        if(left>0)return;
        StopPicking("");Bootstrap.Write("LOCKPICK done after "+pickTime.ToString("F1")+" s");
        Complete(target);
    }
    private void PickSound(Vector3 at)
    {
        if(item==null||pickSounding)return;
        pickSounding=NativeItemCue.Start(item,AudioComponent.AudioTrigger.Lockpick,at,out pickSound)||NativeItemCue.Start(item,AudioComponent.AudioTrigger.Equip,at,out pickSound);
    }
    private void ReleasePickSound()
    {
        if(!pickSounding)return;pickSounding=false;
        try{pickSound.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);pickSound.release();}catch(Exception){}
        pickSound=default;
    }
    private void StopPicking(string why)
    {
        if(pickStarted<0)return;pickStarted=-1;
        try{GameUIManager.ToggleLockpickingHud(false,0);}catch(Exception ex){Bootstrap.Warn("LOCKPICK HUD: "+ex.Message);}
        ReleasePickSound();
        if(why.Length>0)Bootstrap.Write("LOCKPICK stopped: "+why);
    }
    internal void Cancel(){StopPicking("put away");lockpick=null;pick=false;action=null;actor=null;inventory=null;item=previousItem=null;anchor=null;fallbackSurface=null;reader.Clear();visual?.Dispose();visual=null;gesture.Reset();rig.DisarmTrigger();}
    public void Dispose(){Cancel();keyTurn.Dispose();}
}
