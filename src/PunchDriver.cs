using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using UnityEngine;
using PlayMagic.Weapons;
namespace XiiiXR;
// Real tracked fist contact invokes the equipped native fists' damage pipeline.
// No remote ray attacks or direct health writes. Contacts use native impact effects and damage.
internal sealed partial class PunchDriver : IDisposable
{
    private readonly PropImpactAudio impactAudio=new();private readonly float[] nextSoftKnock=new float[2];
    private readonly WeaponImpactDamage impactDamage=new();
    private Vector3 previousObjectPosition;private Quaternion previousObjectRotation;
    private readonly PunchMotion[] motion={new(),new()};
    private readonly Vector3[] previous=new Vector3[2];
    private readonly Vector3[] rawPrevious=new Vector3[2];
    // 0.1.172: the other end of a weapon copy in the hand (its stock), last frame.
    private readonly Vector3[] previousOther=new Vector3[2];private readonly bool[] seenOther=new bool[2];
    private readonly bool[] seen=new bool[2],sounded=new bool[2];
    private readonly Il2CppSystem.Collections.Generic.List<IDamageReceiver> receivers=new();
    private readonly Il2CppStructArray<RaycastHit> hits=new(64);
    private readonly ContactFilter filter=new();
    private Transform? player;
    private MeleeComponent? fists;private float nextFists;
    private string stage="idle";
    private int reportedWeapon;
    private float nextReport,nextSoftReport;
    private static bool IsHeldThing(Equipable e)=>e.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Fist;
    internal void Reset(){foreach(var m in motion)m.Reset();Array.Clear(seen,0,2);Array.Clear(seenOther,0,2);Array.Clear(sounded,0,2);HandImpact.Clear(false);HandImpact.Clear(true);}
    private System.Numerics.Vector3[] partOffsets=Array.Empty<System.Numerics.Vector3>();private float nextSaturatedReport,nextSpentReport;
    internal void Tick(CameraRig rig,PlayerEquipableInventory? inventory,bool allowed)
    {
        try
        {
            var selected=inventory?.currentEquipable;
            if(!allowed||!WeaponOptions.PhysicalPunches.Value||inventory==null||inventory.isInTransit||selected==null
                ||!selected.isEquipableSetUp
                ||!selected.gameObject.activeInHierarchy||!rig.SampleWorldHands(out var left,out var right,out bool validLeft))
            {Reset();return;}
            int selectedSlot=(int)selected.slot;
            if(selectedSlot!=20&&selectedSlot!=32&&selectedSlot!=34&&(selectedSlot<21||selectedSlot>30)){Reset();return;}
            var root=rig.PlayerRoot;if(root==null||!selected.transform.IsChildOf(root)){Reset();return;}
            impactAudio.Prepare();
            if(player!=root){impactDamage.Clear();Reset();player=root;fists=null;nextFists=0;}
            var melee=selected.GetComponent(Il2CppType.Of<MeleeComponent>())?.TryCast<MeleeComponent>();
            if(fists==null&&Time.realtimeSinceStartup>=nextFists)
            {nextFists=Time.realtimeSinceStartup+1;foreach(var obj in root.GetComponentsInChildren(Il2CppType.Of<MeleeComponent>(),true))
                {var m=obj.TryCast<MeleeComponent>();if(m?.baseEquipable?.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist){fists=m;break;}}}
            if(melee==null||!float.IsFinite(melee.MeleeDamage)||melee.MeleeDamage<=0)melee=fists;
            if(melee==null||melee.baseEquipable==null||!float.IsFinite(melee.MeleeDamage)||melee.MeleeDamage<=0){Reset();return;}
            // The fists selected: their melee is the fists (a thrown weapon hits through it).
            if(fists==null&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist)fists=melee;
            if(reportedWeapon!=selected.GetInstanceID())
            {reportedWeapon=selected.GetInstanceID();Bootstrap.Write(IsHeldThing(selected)?"VR MELEE ready: "+selected.identifier+" hits with its whole shape (stock, grip, barrel), in one hand or two; damage="+melee.MeleeDamage:"VR PUNCH ready: selected native fists, grip + physical swing, damage="+melee.MeleeDamage);}
            var equippedMelee=melee;
            bool prop=selected.slot!=PlayerEquipableInventory.ActiveEquipmentSlot.Fist;
            for(int hand=0;hand<2;hand++)
            {
                // 0.1.130: either hand may swing a weapon (the game's one, by the
                // handle or hanging by the fore-end, or a copy); the other punches.
                bool isRight=hand==1;var weapons=WeaponHands.Current;
                bool heldObject=prop&&((weapons?.MeleeHand(isRight)??isRight)||!isRight&&weapons?.LeftPistolVisible==true);
                if(InteractionDriver.Current?.HandOccupied(isRight)==true){motion[hand].Reset();seen[hand]=false;continue;}
                // 0.1.141: a hand holding an enemy's gun by the barrel does not punch.
                if(NpcHitReactions.Current?.Grabbing(isRight)==true){motion[hand].Reset();seen[hand]=false;continue;}
                melee=heldObject||fists==null?equippedMelee:fists;
                if(!heldObject&&weapons?.PunchBlocked(isRight)==true){motion[hand].Reset();seen[hand]=false;continue;}
                var input=isRight?rig.RightControls:rig.LeftControls;
                if((!isRight&&!validLeft)||!input.Valid||!rig.PhysicalHand(isRight,out var physical))
                {motion[hand].Reset();seen[hand]=false;continue;}
                var pose=isRight?right:left;
                var position=CameraRig.UnityPosition(pose);var rotation=GloveVisual.Rotation(pose,isRight);
                var center=position+rotation*new Vector3(0,-.015f,.015f);
                if(heldObject&&(WeaponHands.Current==null||!WeaponHands.Current.TryMeleeTip(isRight,out center))){Reset();return;}
                Vector3 objectPosition=Vector3.zero,objectSafe=Vector3.zero;Quaternion objectRotation=Quaternion.identity,objectSafeRotation=Quaternion.identity;
                var meleeHands=WeaponHands.Current;var shape=heldObject&&meleeHands!=null&&meleeHands.GameMeleeHand(isRight)?meleeHands.MeleeShape:null;
                if(shape!=null&&WeaponHands.Current?.TryMeleePose(out objectPosition,out objectRotation,out objectSafe,out objectSafeRotation)!=true){Reset();return;}
                var oldObjectPosition=previousObjectPosition;var oldObjectRotation=previousObjectRotation;
                if(shape!=null){previousObjectPosition=objectSafe;previousObjectRotation=objectSafeRotation;}
                var travel=center-rawPrevious[hand];rawPrevious[hand]=center;
                if(!heldObject&&ContactRig.Current?.ResolveHand(isRight,false,ref position,ref rotation,(input.Held&HandControls.Grip)!=0)==false)
                {motion[hand].Reset();seen[hand]=false;continue;}
                // Every strike starts at the last collision-constrained fist.
                // A real controller pushed through a wall cannot seed a later
                // punch behind that wall. The desired endpoint detects contact.
                var from=previous[hand];previous[hand]=heldObject?WeaponHands.Current!.SafeMeleeTip(isRight):position+rotation*new Vector3(0,-.015f,.015f);bool had=seen[hand];seen[hand]=true;
                // 0.1.172: a copy's other end (its stock), swept from where it was.
                bool other=false;var otherFrom=previousOther[hand];var otherEnd=Vector3.zero;
                if(heldObject&&shape==null&&WeaponHands.Current?.TryMeleeOtherEnd(isRight,out otherEnd)==true){other=seenOther[hand];previousOther[hand]=otherEnd;seenOther[hand]=true;}
                else seenOther[hand]=false;
                float now=Time.realtimeSinceStartup;
                // 0.1.152: a
                // long thing (shovel, broom, chair) is timed by its end, not the
                // hand - swung with one or two hands its end moves several times
                // faster. In the player's own space: walking does not count.
                var swingPoint=physical;
                bool byEnd=false;
                if(shape!=null&&meleeHands!=null&&meleeHands.LongStick&&meleeHands.TryLongStickEnd(out var stickEnd))
                {byEnd=true;var local=root.InverseTransformPoint(stickEnd);swingPoint=new System.Numerics.Vector3(local.x,local.y,local.z);}
                // 0.1.200: a thing taken from the level (a chair) swung in a
                // hand was timed by the hand (1.8 m/s: a soft touch, a blow needs
                // 3) - its far end moves several times faster. Timed by its part
                // farthest from the hand, like a broom by its end.
                else if(shape!=null&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental)
                {
                    var handLocal=Quaternion.Inverse(objectRotation)*(position-objectPosition);
                    if(partOffsets.Length<shape.Length)partOffsets=new System.Numerics.Vector3[shape.Length];
                    for(int k=0;k<shape.Length;k++)partOffsets[k]=shape[k].Offset;
                    int far=PunchMotion.FarthestPart(partOffsets,shape.Length,new System.Numerics.Vector3(handLocal.x,handLocal.y,handLocal.z));
                    if(far>=0){byEnd=true;var end=objectPosition+objectRotation*ContactWorld.U(shape[far].Offset);var local=root.InverseTransformPoint(end);swingPoint=new System.Numerics.Vector3(local.x,local.y,local.z);}
                }
                bool strike=motion[hand].Sample(swingPoint,now,heldObject||(input.Held&HandControls.Grip)!=0,true,byEnd?physical:null);
                if(!strike)sounded[hand]=false;
                // 0.1.206: a fast swing of a held thing that does not count: not rearmed since its last touch.
                if(!strike&&heldObject&&motion[hand].Spent&&motion[hand].Speed>2.5f&&now>=nextSpentReport)
                {nextSpentReport=now+3;Bootstrap.Write("VR PUNCH side="+(isRight?"R":"L")+" swung at "+motion[hand].Speed.ToString("F1")+" m/s but not rearmed since its last touch (pull back, or carry it off that touch)");}
                if(strike&&!sounded[hand]){sounded[hand]=true;try{melee.playerArmsAnimationControl?.PlaySmashSound();}catch(Exception ex){Bootstrap.Warn("VR swing sound: "+ex.Message);}}
                if(!had||!strike)continue;
                var delta=center-from;float distance=delta.magnitude;
                if(shape==null&&(distance<.0001f||distance>.4f)){motion[hand].Reset();continue;}
                // Nearest solid hit wins, including walls before the NPC. Native
                // character damage is applied once for the whole swing, not per collider.
                // 0.1.202: each part of a long thing has its own nearest hit; a part
                // that reaches an enemy (or a damageable thing) with nothing in its
                // way strikes it even if another part touched a wall or the floor
                // first (a long thing drags on them: that touch, at the very start
                // of its sweep, was the nearest of all and dropped the whole stroke).
                // A part just leaving what it touches does not stop at it.
                stage="SphereCastNonAlloc";
                int count=0,nearest=-1;float nearestDistance=float.PositiveInfinity;RaycastHit nearestHit=default;
                Vector3 impactFrom=from,impactDelta=delta;bool saturated=false,insideWorld=false;
                int worldProbe=-1;float worldDistance=float.PositiveInfinity;RaycastHit worldHit=default;Vector3 worldFrom=from,worldDelta=delta;
                int probes=shape?.Length??(other?2:1);
                for(int probe=0;probe<probes;probe++)
                {
                    var probeFrom=from;var probeDelta=delta;float radius=.05f;
                    if(shape!=null)
                    {
                        var part=shape[probe];var offset=ContactWorld.U(part.Offset);
                        probeFrom=oldObjectPosition+oldObjectRotation*offset;
                        probeDelta=objectPosition+objectRotation*offset-probeFrom;radius=part.Radius;
                    }
                    else if(probe==1){probeFrom=otherFrom;probeDelta=otherEnd-otherFrom;radius=.06f;}
                    // 0.1.154: a long stick's far end moves further in a frame.
                    float length=probeDelta.magnitude;if(length<.0001f||length>(byEnd?1.1f:.65f))continue;
                    int found=Physics.SphereCastNonAlloc(probeFrom,radius,probeDelta/length,hits,length+.008f,~0,QueryTriggerInteraction.Collide);
                    // Too many to tell which is nearest: this part is not guessed at (the others still count).
                    if(found>=hits.Length){saturated=true;count+=found;continue;}count+=found;
                    float best=float.PositiveInfinity;RaycastHit bestHit=default;bool bestTarget=false;
                    for(int i=0;i<found;i++)
                    {
                        var candidate=hits[i];var c=candidate.collider;
                        if(c==null||!c.enabled||c.transform.IsChildOf(root)||filter.Excluded(c))continue;
                        bool npcPart=c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null;
                        if(c.isTrigger&&(!npcPart||!melee.ValidateIfDamageable(c)))continue;
                        bool target=npcPart||c.GetComponentInParent(Il2CppType.Of<DamageAction>())!=null||c.GetComponentInParent(Il2CppType.Of<DestructableObject>())!=null;
                        if(candidate.distance<=.0001f)
                        {
                            // Touching it where the sweep starts: struck only if moving into it.
                            if(!ColliderSurface.TryClosest(c,probeFrom,out var closest))continue;
                            var outward=probeFrom-closest;
                            if(outward.sqrMagnitude<1e-10f){if(!target){insideWorld=true;continue;}}
                            else if(Vector3.Dot(probeDelta,outward)>=-1e-7f)continue;
                        }
                        float fraction=candidate.distance/length;
                        if(fraction<best){best=fraction;bestHit=candidate;bestTarget=target;}
                    }
                    if(!float.IsFinite(best))continue;
                    if(bestTarget){if(best<nearestDistance){nearest=probe;nearestDistance=best;nearestHit=bestHit;impactFrom=probeFrom;impactDelta=probeDelta;}}
                    else if(best<worldDistance){worldProbe=probe;worldDistance=best;worldHit=bestHit;worldFrom=probeFrom;worldDelta=probeDelta;}
                }
                if(saturated&&now>=nextSaturatedReport){nextSaturatedReport=now+5;Bootstrap.Write("VR PUNCH a part's sweep met "+hits.Length+" colliders or more: that part not counted");}
                // 0.1.156:
                // held in two hands the thing is stopped at the enemy's body
                // (the weapon's collisions), and a sweep that starts touching a
                // body never reports it. An enemy already touching the stopped
                // thing on a real stroke is struck where the stroke goes.
                // 0.1.205: his fists up in a guard
                // stop the club before his chest and head, where the sweeps
                // cannot see what they start touching - and a touch of a wall,
                // a table or the floor by another part of the long gun came
                // first and took the stroke. An enemy the weapon is touching
                // (or stopped by) now wins over the world.
                if(nearest<0&&shape!=null)
                {
                    var touch=new Touch();TouchingNpc(shape,oldObjectPosition,oldObjectRotation,objectSafe,objectSafeRotation,objectPosition,objectRotation,root,melee,ref touch);
                    // 0.1.200: or stopped by an enemy's body (a club swung against it).
                    if(!touch.Found)StoppedByNpc(shape,oldObjectPosition,oldObjectRotation,objectSafe,objectSafeRotation,objectPosition,objectRotation,root,melee,ref touch);
                    if(touch.Found){nearest=touch.Probe;nearestHit=touch.Hit;impactFrom=touch.From;impactDelta=touch.Delta;}
                }
                if(saturated&&nearest<0&&worldProbe<0){motion[hand].Contact(now);continue;}
                // No part reached an enemy: a wall or the floor met (its knock), or a part inside the world.
                bool worldOnly=nearest<0&&worldProbe>=0;
                if(worldOnly){nearest=worldProbe;nearestDistance=worldDistance;nearestHit=worldHit;impactFrom=worldFrom;impactDelta=worldDelta;}
                if(nearest<0&&insideWorld&&shape==null){motion[hand].Contact(now);continue;}
                // 0.1.205: a held weapon's stroke that found no enemy with one close by: what it met instead.
                if(shape!=null&&(nearest<0||worldOnly))ReportNearMiss(isRight,motion[hand].Speed,worldOnly?nearestHit.collider:null,nearest,objectSafe,(objectPosition-objectSafe).magnitude,root,now);
                if(nearest<0&&now>=nextReport)
                {nextReport=now+4;Bootstrap.Write("VR PUNCH swing no contact side="+(isRight?"R":"L")+" speed="+motion[hand].Speed.ToString("F2")+" candidates="+count+(shape!=null?" parts="+shape.Length+(byEnd?" timed by its end":"")+" weaponGap="+((objectPosition-objectSafe).magnitude*100).ToString("F0")+"cm":""));}
                if(nearest>=0)
                {
                    stage="native receiver";
                    var hit=nearestHit;var collider=hit.collider;from=impactFrom;delta=impactDelta;if(heldObject)travel=impactDelta;
                    if(collider==null||collider.transform.IsChildOf(root))continue;
                    if(hit.distance<=.0001f)
                    {
                        if(!ColliderSurface.TryClosest(collider,from,out var closest))continue;
                        var outward=from-closest;
                        // Do not skip a zero-distance wall and damage an NPC
                        // behind it. Retraction blocks this stroke entirely.
                        if(outward.sqrMagnitude<1e-10f)
                        {
                            // Inside a world solid has no trustworthy approach side.
                            // A native hurtbox already touching the stopped fist may
                            // count only during an earned physical strike window.
                            if(collider.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())==null&&collider.GetComponentInParent(Il2CppType.Of<DamageAction>())==null&&collider.GetComponentInParent(Il2CppType.Of<DestructableObject>())==null)
                            {motion[hand].Contact(now);continue;}
                        }
                        else if(Vector3.Dot(travel,outward)>=-1e-7f)continue;
                    }
                    // 0.1.108: a held prop only touching a wall/NPC (no real
                    // swing speed) neither breaks nor hits.
                    if(heldObject&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental&&!PunchMotion.PropSwing(motion[hand].Peak))
                    {
                        motion[hand].Contact(now);
                        // 0.1.161: a wooden thing knocks softly on what it touches.
                        if(MeleeDamageMath.WoodenProp(selected.identifier)&&now>=nextSoftKnock[hand]&&motion[hand].Peak>.6f)
                        {
                            nextSoftKnock[hand]=now+.35f;var at=nearestHit.point;
                            if(nearestHit.distance<=0&&ColliderSurface.TryClosest(collider,from,out var touched))at=touched;
                            if(float.IsFinite(at.sqrMagnitude))impactAudio.Knock(at,Math.Clamp(motion[hand].Peak/PunchMotion.PropSwingSpeed,.25f,.8f),"a touch");
                        }
                        if(now>=nextSoftReport){nextSoftReport=now+2;Bootstrap.Write("VR PROP soft touch target="+collider.name+" peak="+motion[hand].Peak.ToString("F2")+(byEnd?" at its end":" at the hand")+" (a blow needs "+PunchMotion.PropSwingSpeed.ToString("F1")+" m/s)");}
                        continue;
                    }
                    motion[hand].Contact(now);bool hitNpc=false;
                    var local=Quaternion.Inverse(rotation)*delta;
                    HandImpact.Hit(isRight,new System.Numerics.Vector3(local.x,local.y,local.z),now);
                    // Queue after native OnHit/pain callbacks, which can change
                    // gamepad rumble. Every earned contact has feedback, including
                    // surviving NPCs; it is not conditional on death or damage.
                    try
                    {
                    if(hit.distance<=0){if(!ColliderSurface.TryClosest(collider,center,out var surface))continue;hit.point=surface;}
                    if(!float.IsFinite(hit.point.sqrMagnitude))continue;
                    var npc=collider.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();
                    impactAudio.Surface(melee,selected,heldObject,npc!=null,hit,from);
                    // 0.1.173: a gun's or a
                    // thing's own melee has no body-hit sound or comic; on an enemy
                    // the fists' own ones play too (the thud, the comic picture).
                    var bodyFx=heldObject&&npc!=null&&fists!=null&&fists.Pointer!=melee.Pointer?fists:null;
                    if(bodyFx!=null)impactAudio.Body(bodyFx,hit,from);
                    try{(bodyFx??melee).SpawnMuzzleFlash(hit,hit.point);}
                    catch(Exception ex){Bootstrap.Warn("VR impact effects: "+ex.Message);}
                    bool destructible=heldObject&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
                    // 0.1.150: long-handled things
                    // last several blows and hit like the fists (several blows, the
                    // enemy bends at each).
                    bool durable=destructible&&MeleeDamageMath.DurableProp(selected.identifier);
                    // 0.1.196: a gun swung by its barrel hits like a bat (as a broom or a shovel).
                    bool clubbing=heldObject&&WeaponHands.Current?.Clubbing(isRight)==true;
                    // The earned strike window already requires a real fast swing.
                    // Do not reject its natural deceleration at contact.
                    if(npc==null)
                    {
                        var damage=collider.GetComponentInParent(Il2CppType.Of<DamageAction>())?.TryCast<DamageAction>();
                        if(damage==null)damage=collider.GetComponentInParent(Il2CppType.Of<DestructableObject>())?.TryCast<DestructableObject>()?.healthTracker;
                        var target=damage?.TryCast<IDamageReceiver>();
                        if(target!=null&&target.CanReceiveDamage())
                        {
                            receivers.Clear();
                            bool ignore=damage!.ignoreNonLethalDamage;
                            try{damage.ignoreNonLethalDamage=false;melee.ApplyDamageTo(receivers,target,hit,from);inventory.playerUsedMeleeForce=true;}
                            finally{if(damage!=null)damage.ignoreNonLethalDamage=ignore;receivers.Clear();}
                            Bootstrap.Write("VR IMPACT world target="+collider.name+" damage="+melee.MeleeDamage);
                        }
                    }
                    // 0.1.139: an NPC already down (dead, knocked out) is pushed where hit.
                    float strikeSpeed=Math.Max(motion[hand].Speed,motion[hand].Peak);
                    // A living NPC the game does not let this hit damage (its gun,
                    // a scripted moment) still reacts.
                    if(npc!=null&&(!npc.IsAlive||!npc.IsConscious))NpcHitReactions.Current?.Hit(npc,collider,hit.point,delta,strikeSpeed,heldObject,from);
                    // 0.1.200: a
                    // stroke stopped on an enemy's body collider (not one the game takes
                    // blows through) strikes its nearest one that does.
                    if(npc!=null&&npc.IsAlive&&npc.IsConscious&&npc.CanReceiveDamage()&&!melee.ValidateIfDamageable(collider))
                        RetargetHurtbox(npc,melee,delta,ref collider,ref hit);
                    if(npc==null||!npc.CanReceiveDamage()||!melee.ValidateIfDamageable(collider))
                    {if(npc!=null&&npc.IsAlive&&npc.IsConscious)NpcHitReactions.Current?.Hit(npc,collider,hit.point,delta,strikeSpeed,heldObject,from);if(destructible&&!durable){melee.OnHit?.Invoke(selected,true);melee.OnSuccessful?.Invoke(hit,from);}continue;}
                    hitNpc=true;
                    var receiver=npc.GetDamageReceiver();if(receiver==null||!receiver.CanReceiveDamage()){if(npc.IsAlive&&npc.IsConscious)NpcHitReactions.Current?.Hit(npc,collider,hit.point,delta,strikeSpeed,heldObject,from);continue;}
                    if(hit.distance<=0){if(!ColliderSurface.TryClosest(collider,center,out var surface))continue;hit.point=surface;}
                    if(!float.IsFinite(hit.point.sqrMagnitude))continue;
                    float multiplier=melee.currentMeleeDamageMultiplier,baseDamage=melee._MeleeDamage_k__BackingField;string dealt="",guarded="";float guardShare=0;
                    try
                    {
                        receivers.Clear();
                        melee.currentMeleeDamageMultiplier=melee.baseMeleeDamageMultiplier;
                        var areaHit=npc.GetColliderArea(collider);
                        float modified=npc.GetModifiedDamageByBodyPart(1,areaHit,DamageType.Melee);
                        if(destructible&&!durable)
                            melee.currentMeleeDamageMultiplier=impactDamage.Multiplier(npc.GetInstanceID(),npc.CurrentHP,melee.MeleeDamage,melee.baseMeleeDamageMultiplier,modified,true);
                        else
                        {
                            // 0.1.141: fists 5..8 blows, a gun in the hand 4..6, by
                            // the swing (not the game's fixed fist damage: 3 blows).
                            // 0.1.150: a broom / shovel 5..3, never fewer than 3.
                            float strength=MeleeDamageMath.Strength(strikeSpeed),hits=durable||clubbing?MeleeDamageMath.PropHits(strength):MeleeDamageMath.Hits(strength,heldObject);
                            // 0.1.148: five blows at least from full health (the head included).
                            float share=MeleeDamageMath.Share(npc.MaxHealth,hits,MeleeDamageMath.AreaFactor((int)areaHit),durable||clubbing?MeleeDamageMath.MinPropBlows:MeleeDamageMath.MinBlows);
                            if(share>0&&float.IsFinite(share))
                            {
                                guardShare=share;
                                melee._MeleeDamage_k__BackingField=share/Math.Max(.05f,float.IsFinite(modified)?modified:1);melee.currentMeleeDamageMultiplier=1;
                                dealt=" damage="+share.ToString("F0")+"/"+npc.MaxHealth.ToString("F0")+" ("+areaHit+", ~"+(npc.MaxHealth/share).ToString("F1")+" blows like this)";
                            }
                        }
                        stage="ApplyDamageTo";
                        NpcHitReactions.PunchBegins(npc,guardShare);
                        try{melee.ApplyDamageTo(receivers,receiver,hit,from);}
                        finally{guarded=NpcHitReactions.PunchEnds(npc);}
                        inventory.playerUsedMeleeForce=true;
                    }
                    finally{melee.currentMeleeDamageMultiplier=multiplier;melee._MeleeDamage_k__BackingField=baseDamage;receivers.Clear();}
                    // Notify the same native hit listeners after successful contact
                    // (0.1.150: not for a thing that lasts several blows - they may break it).
                    if(!durable)
                    {
                        try{melee.OnHit?.Invoke(selected,true);melee.OnSuccessful?.Invoke(hit,from);}
                        catch(Exception ex){Bootstrap.Warn("Punch hit feedback: "+ex.Message);}
                    }
                    Bootstrap.Write("VR PUNCH side="+(isRight?"R":"L")+(clubbing?" clubbing":"")+(heldObject?" with "+selected.identifier+(shape!=null?" (part "+nearest+" of "+shape.Length+")":other&&nearest==1?" (its other end)":""):"")+" target="+npc.name+" speed="+strikeSpeed.ToString("F2")+dealt+" hp="+npc.CurrentHP.ToString("F0")+guarded);
                    // 0.1.139: the NPC's skeleton reacts (bends, is thrown back, drops its gun).
                    NpcHitReactions.Current?.Hit(npc,collider,hit.point,delta,strikeSpeed,heldObject,from);
                    }
                    finally
                    {
                        rig.PunchHaptics(isRight);
                        if(heldObject&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental)
                        {
                            // 0.1.150: a broom / shovel breaks after its last blow on an enemy (walls do not wear it out).
                            if(MeleeDamageMath.DurableProp(selected.identifier))WeaponHands.Current?.WearHeldProp(selected,hitNpc);
                            else WeaponHands.Current?.BreakHeldProp(selected);
                        }
                    }
                    continue;
                }
            }
        }
        catch(Exception ex)
        {
            Reset();if(Time.realtimeSinceStartup>=nextReport)
            {nextReport=Time.realtimeSinceStartup+5;Bootstrap.Warn("VR PUNCH unavailable stage="+stage+"; other controls continue: "+ex.ToString());}
        }
    }
    // 0.1.141: a weapon (a copy of the player's) thrown into an NPC: a little
    // damage through the native fists (10..16 such throws take it down, by the
    // throw's speed; the head counts more, an arm or a leg less), and its
    // skeleton reacts where it was hit. False: that collider does not take it.
    private float nextThrownReport;
    // 0.1.156: PunchDriver.Touch.cs.
    private struct Touch{internal bool Found{get;set;}internal RaycastHit Hit{get;set;}internal Vector3 From{get;set;}internal Vector3 Delta{get;set;}internal int Probe{get;set;}}
    partial void TouchingNpc(ContactSphere[] shape,Vector3 oldPosition,Quaternion oldRotation,Vector3 safePosition,Quaternion safeRotation,Vector3 desiredPosition,Quaternion desiredRotation,Transform root,MeleeComponent melee,ref Touch touch);
    partial void RetargetHurtbox(PlayMagic.AI.NPC npc,MeleeComponent melee,Vector3 stroke,ref Collider collider,ref RaycastHit hit);
    partial void ReportNearMiss(bool right,float speed,Collider? world,int part,Vector3 safe,float gap,Transform root,float now);
    partial void StoppedByNpc(ContactSphere[] shape,Vector3 oldPosition,Quaternion oldRotation,Vector3 safePosition,Quaternion safeRotation,Vector3 desiredPosition,Quaternion desiredRotation,Transform root,MeleeComponent melee,ref Touch touch);
    internal bool Thrown(Collider collider,RaycastHit hit,Vector3 velocity,string profile,Transform? playerRoot)
    {
        if(collider==null||playerRoot!=null&&collider.transform.IsChildOf(playerRoot))return false;
        var npc=collider.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();
        if(npc==null||npc.isHeldByPlayer)return false;
        float speed=velocity.magnitude;var point=hit.point;
        var from=point-(speed>1e-4f?velocity/speed:Vector3.forward)*.3f;
        var root=player!=null?player:playerRoot;
        if(fists==null&&root!=null)
            foreach(var obj in root.GetComponentsInChildren(Il2CppType.Of<MeleeComponent>(),true))
            {var m=obj.TryCast<MeleeComponent>();if(m?.baseEquipable?.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist){fists=m;break;}}
        var melee=fists;
        if(collider.isTrigger&&(melee==null||!melee.ValidateIfDamageable(collider)))return false;
        string dealt=" (no damage: ";
        if(!npc.IsAlive||!npc.IsConscious)dealt+="down)";
        else if(melee==null||!float.IsFinite(melee.MeleeDamage))dealt+="no native fists yet)";
        else if(!npc.CanReceiveDamage()||!melee.ValidateIfDamageable(collider))dealt+="the game does not let it be hurt now)";
        else
        {
            var receiver=npc.GetDamageReceiver();
            if(receiver==null||!receiver.CanReceiveDamage())dealt+="no damage receiver)";
            else
            {
                float multiplier=melee.currentMeleeDamageMultiplier,baseDamage=melee._MeleeDamage_k__BackingField;
                try
                {
                    receivers.Clear();
                    var areaHit=npc.GetColliderArea(collider);
                    float modified=npc.GetModifiedDamageByBodyPart(1,areaHit,DamageType.Melee);
                    float share=npc.MaxHealth/MeleeDamageMath.ThrownHits(speed)*MeleeDamageMath.AreaFactor((int)areaHit);
                    if(share>0&&float.IsFinite(share))
                    {
                        melee._MeleeDamage_k__BackingField=share/Math.Max(.05f,float.IsFinite(modified)?modified:1);melee.currentMeleeDamageMultiplier=1;
                        melee.ApplyDamageTo(receivers,receiver,hit,from);
                        dealt=" damage="+share.ToString("F0")+"/"+npc.MaxHealth.ToString("F0")+" ("+areaHit+")";
                    }
                    else dealt+="no health)";
                }
                finally{melee.currentMeleeDamageMultiplier=multiplier;melee._MeleeDamage_k__BackingField=baseDamage;receivers.Clear();}
                try{melee.SpawnMuzzleFlash(hit,point);}catch(Exception){}
            }
        }
        if(Time.realtimeSinceStartup>=nextThrownReport){nextThrownReport=Time.realtimeSinceStartup+.5f;Bootstrap.Write("THROWN "+profile+" hit "+npc.name+" at "+collider.name+" speed="+speed.ToString("F1")+dealt+" hp="+npc.CurrentHP.ToString("F0"));}
        // Its skeleton reacts where the weapon landed (thrown speed counts
        // less than a fist's: the weapon is lighter than an arm behind it).
        NpcHitReactions.Current?.Hit(npc,collider,point,velocity,speed*MeleeDamageMath.ThrownReaction,true,from);
        return true;
    }
    public void Dispose(){Reset();impactAudio.Dispose();}
}
