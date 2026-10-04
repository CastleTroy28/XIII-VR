using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using PlayMagic.Weapons;
using UnityEngine;
using N=System.Numerics.Vector3;
namespace XiiiXR;
// 0.1.139: NPCs react to the player's punches with their skeleton. HitReactionMath plans the reaction from where the fist
// landed and how it moved; here it is put on the NPC's own bones (spine,
// neck, head, arms from the game's AIRigReference) after the game's
// animation and IK of the frame and before the meshes are skinned (the
// Canvas.willRenderCanvases moment), as rotations on top of the animated
// pose that spring back to it. Also:
//  - a hard punch pushes the NPC back a little (its NavMeshAgent, so never
//    through a wall), a hard one also plays the game's own hit animation;
//  - a punch to the forearm of the hand holding the gun (or a hard uppercut)
//    knocks the gun out of the hand: the game drops it as a pickup and the
//    NPC goes on with its fists;
//  - an NPC already down (dead, knocked out, ragdoll) is pushed where hit.
internal sealed partial class NpcHitReactions:IDisposable
{
    internal static NpcHitReactions? Current;
    private sealed class Body
    {
        internal NPC Npc=null!;internal IntPtr Id;internal Transform Frame=null!;internal NpcBones Rig=null!;
        internal readonly Transform?[] Joints=new Transform?[HitPlan.Joints];
        internal readonly Quaternion[] Base=new Quaternion[HitPlan.Joints],Applied=new Quaternion[HitPlan.Joints];
        internal readonly bool[] Has=new bool[HitPlan.Joints];
        internal readonly HitReactionState State=new();
        internal float LastTime=-1;
        // 0.1.145: the whole drawn body (the animator's object under the
        // NPC) leans with the blow too; and how many bones the animation
        // writes each frame (diagnostics: ours are drawn on top of them).
        internal Transform? Model;internal Quaternion ModelBase,ModelApplied;internal Vector3 ModelBaseAt,ModelAppliedAt;internal bool ModelHas;
        internal int Frames,Written,Checked;internal bool Reported;
        // 0.1.147: drawn by the mod (NpcSkinOverlay); how often the bones had
        // changed again between the canvas moment and the cameras; extra
        // angles (a brawler's sway) added to the reaction.
        internal NpcSkinOverlay? Skin;internal int LateChecked,LateChanged;internal bool LateReported;
        internal readonly System.Numerics.Vector3[] Extra=new System.Numerics.Vector3[HitPlan.Joints];
        // 0.1.148: a brawler's hands clenched into fists.
        internal NpcFist? FistL,FistR;internal bool FistTried;
        internal NpcFootwork? Feet;internal bool FeetTried;
    }
    internal const float BodyLean=.45f,MaxBodyLean=.26f;
    // 0.1.147: at most this many enemies drawn by the mod at once.
    internal const int MaxDrawn=4;
    private static bool dumpedParts;
    private readonly Dictionary<IntPtr,Body> bodies=new();
    private readonly Dictionary<IntPtr,EnemyInventory> disarmed=new();
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.npchits");
    private Canvas.WillRenderCanvases? hook;
    private int appliedFrame=-1;private bool reportedHook,reportedApplied;
    private float nextError;private int reports;
    // Joints applied parents first.
    private static readonly HitJoint[] Order={HitJoint.SpineLow,HitJoint.SpineMid,HitJoint.SpineTop,HitJoint.Neck,HitJoint.Head,HitJoint.ArmL,HitJoint.ForearmL,HitJoint.ArmR,HitJoint.ForearmR};
    internal NpcHitReactions()
    {
        Current=this;LinkHostages();
        try{hook=(Action)Late;Canvas.add_willRenderCanvases(hook);}
        catch(Exception ex){hook=null;Bootstrap.Warn("NPC HITS late hook unavailable (reactions drawn one frame late from the camera): "+ex.Message);}
        try
        {
            patches.Patch(AccessTools.DeclaredMethod(typeof(EnemyInventory),"EquipWeapon"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(EquipGuard)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(EnemyInventory),"EquipWeaponInstantly"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(EquipInstantGuard)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(EnemyInventory),"ReplaceMainWeapon"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(Rearmed)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(EnemyInventory),"ApplyWeaponState"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(Rearmed)));
        }
        catch(Exception ex){Bootstrap.Warn("NPC HITS disarm guard unavailable (a disarmed NPC may draw its gun again): "+ex.Message);}
        // 0.1.141: a stunned NPC (or one whose gun the player holds) neither
        // shoots, strikes nor walks.
        try
        {
            patches.Patch(AccessTools.DeclaredMethod(typeof(Combatant),"UseWeapon"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(NpcUseWeapon)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(Combatant),"UseMeleeWeapon"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(NpcUseMelee)));
            patches.Patch(AccessTools.DeclaredMethod(typeof(AIMovementController),"OnAnimatorMove"),prefix:new HarmonyMethod(typeof(NpcHitReactions),nameof(NpcMove)));
        }
        catch(Exception ex){Bootstrap.Warn("NPC HITS stun unavailable (a hit NPC goes on shooting): "+ex.Message);}
        // 0.1.145: a disarmed enemy's punches of the game's own get the fist sound.
        RecoverFightMarker();
        // 0.1.148: five punches at least (NpcHitReactions.Guard).
        try{PatchGuard(patches);}catch(Exception ex){Bootstrap.Warn("NPC HITS five-punch guard unavailable (a punch to the head may knock out at once): "+ex.Message);}
        try{patches.Patch(AccessTools.DeclaredMethod(typeof(PlayerState),"ReceiveDamage"),postfix:new HarmonyMethod(typeof(NpcHitReactions),nameof(PlayerHurt)));}
        catch(Exception ex){Bootstrap.Warn("NPC FIGHT player damage hook unavailable (the game's own punches of a disarmed enemy get no extra sound): "+ex.Message);}
        Bootstrap.Write("NPC HITS ready: skeleton reactions to punches (spine, neck, head, arms), knockback, disarm="+WeaponOptions.NpcDisarm.Value+", a disarmed enemy fights with its fists="+WeaponOptions.NpcFistFight.Value+" (game AI "+WeaponOptions.NpcFistFightGameAi.Value+", a punch "+WeaponOptions.NpcPunchDamage.Value.ToString("F0")+" % of full health)");
    }
    // ---- A punch landed ----
    // point, direction: world; speed: the hand's (m/s); from: where the fist came from.
    internal void Hit(NPC npc,Collider? collider,Vector3 point,Vector3 direction,float speed,bool held,Vector3 from)
    {
        if(!WeaponOptions.NpcReactions.Value||npc==null||NpcAllies.Ally(npc))return;
        try
        {
            if(direction.sqrMagnitude<1e-8f)direction=point-from;
            if(direction.sqrMagnitude<1e-8f)return;direction.Normalize();
            if(!npc.IsAlive||!npc.IsConscious||npc.isRagdoll){PushDown(npc,collider,point,direction,speed);return;}
            if(npc.isHeldByPlayer)return;
            // 0.1.142: without bones it still reacts (stunned, the game's hit
            // animation, disarmed), planned on a standing body.
            var body=BodyOf(npc);var frame=body?.Frame??npc.transform;
            var yaw=Yaw(frame);var inverse=Quaternion.Inverse(yaw);var origin=frame.position;
            var skeleton=body!=null?Skeleton(body,inverse,origin):StandingSkeleton();
            var local=inverse*(point-origin);var dir=inverse*direction;
            var plan=HitReactionMath.Plan(ContactWorld.V(local),ContactWorld.V(dir),speed,held,skeleton);
            body?.State.Add(plan,Time.time);
            string extra=body==null?" (no skeleton: no bending)":"";
            // 0.1.141: out of it for a moment (no shooting, no walking).
            extra+=StunFor(npc,plan.Stun,plan.Stun>=StrongStun);
            // 0.1.187: brawlers use the procedural reaction. Long native hit clips
            // must not keep fighting the restored guard after the shorter stun.
            // Armed enemies only request a native flinch for the heaviest hits.
            if(!brawlers.ContainsKey(npc.Pointer)&&(plan.Strength>=.9f||plan.Uppercut&&plan.Strength>=.8f))
            {
                try
                {
                    var h=AnimOf(npc)?.HitHandler;
                    if(h!=null&&h.CanPlayHitAnimation()&&!h.IsPlayingHitAnimation){h.TryPlayHitAnimation(from,point,true,false);extra+=" +game hit animation";}
                    else extra+=" (game hit animation: "+(h==null?"none":h.IsPlayingHitAnimation?"playing":"not allowed now")+")";
                }
                catch(Exception ex){extra+=" (game hit animation: "+ex.Message+")";}
            }
            // 0.1.147: a brawler hit blocks or counters (BrawlPlan).
            if(brawlers.TryGetValue(npc.Pointer,out var fighter))fighter.Struck=true;
            // 0.1.141: its gun held by the player's other hand: any blow makes it let go.
            int holder=GrabSide(npc.Pointer);
            if(holder>=0&&speed>=GrabStrikeSpeed)extra+=DisarmGrabbed(holder,"a blow");
            // 0.1.147: a
            // punch alone does not disarm unless [Hands] NpcPunchDisarm is on.
            else if(WeaponOptions.NpcDisarm.Value&&WeaponOptions.NpcPunchDisarm.Value&&(plan.ForearmSide>=0&&plan.Strength>=.25f||plan.Uppercut&&plan.Strength>=.8f))
                extra+=Disarm(npc,body,plan,point,false);
            if(reports++<60)Bootstrap.Write("NPC HIT "+npc.name+" "+plan.Region+(plan.Uppercut?" uppercut":plan.Hook?" hook":"")+" side="+(plan.Side>0?"its right":"its left")
                +" speed="+speed.ToString("F1")+" strength="+plan.Strength.ToString("F2")+" knockback="+plan.Knockback.Length().ToString("F2")+" m"+extra);
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC HIT reaction: "+ex.Message);}}
    }
    // 0.1.146: a shot of the player's that hits an NPC who is down (dead,
    // knocked out, a ragdoll) pushes the part of the body it hit, harder for
    // heavier guns (HitReactionMath.ShotImpulse). The first solid thing on
    // the line stops it (a wall before the body); a living NPC is the game's.
    private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> shotHits=new(32);
    private readonly List<(float distance,int index)> shotOrder=new();
    private int shotReports;
    internal void Shot(Vector3 from,Vector3 direction,string profile,Transform? player)
    {
        if(!(direction.sqrMagnitude>1e-6f)||!float.IsFinite(from.sqrMagnitude))return;
        direction.Normalize();
        int count=Physics.RaycastNonAlloc(from,direction,shotHits,120f,~0,QueryTriggerInteraction.Collide);
        shotOrder.Clear();
        for(int i=0;i<Math.Min(count,shotHits.Length);i++)shotOrder.Add((shotHits[i].distance,i));
        shotOrder.Sort((a,b)=>a.distance.CompareTo(b.distance));
        foreach(var (_,i) in shotOrder)
        {
            var hit=shotHits[i];var c=hit.collider;if(c==null||!c.enabled)continue;
            if(player!=null&&c.transform.IsChildOf(player))continue;
            NPC? npc=null;try{npc=c.GetComponentInParent(Il2CppType.Of<NPC>())?.TryCast<NPC>();}catch(Exception){}
            if(npc==null){if(c.isTrigger)continue;return;}
            if(npc.IsAlive&&npc.IsConscious&&!npc.isRagdoll)return;
            var rb=c.attachedRigidbody;
            if(rb==null){if(c.isTrigger)continue;if(shotReports++<20)Bootstrap.Write("NPC SHOT "+npc.name+" (down) hit at "+c.name+": no body part to push there");return;}
            string woke=WakeRagdoll(npc);
            if(rb.isKinematic){if(shotReports++<20)Bootstrap.Write("NPC SHOT "+npc.name+" (down) hit at "+c.name+": its body is held still (kinematic), not pushed"+woke);return;}
            float impulse=HitReactionMath.ShotImpulse(profile);
            var at=hit.distance>0?hit.point:c.ClosestPoint(from);
            rb.AddForceAtPosition(direction*impulse,at,ForceMode.Impulse);rb.WakeUp();
            if(shotReports++<20)Bootstrap.Write("NPC SHOT "+npc.name+" (down) hit at "+rb.name+": pushed, impulse="+impulse.ToString("F0")+" ("+profile+")"+woke);
            return;
        }
    }
    // 0.1.147: a body down but still held by its fall animation is switched
    // to its ragdoll the game's own way (as the body grab does: animator off,
    // bones live), a settled ragdoll (frozen, its drag raised to stop it) is
    // woken and its drag set back, then it is pushed.
    private static string WakeRagdoll(NPC npc)
    {
        string how="";
        try{if(npc.usesRagdolls&&!npc.isRagdoll){npc.ActivateRagdollBehaviour(false);how+=" ragdoll on";}}catch(Exception ex){how+=" (ragdoll: "+ex.Message+")";}
        try
        {
            var r=npc.ragdollControllerComponent;
            if(r!=null)
            {
                if(r.m_currentState!=RagdollState.Alive){r.SetRagdollState(RagdollState.Alive,false);how+=" woken";}
                var all=npc.allRigidBodies;
                if(all!=null)foreach(var body in all)if(body!=null){try{r.ResetBodyDrag(body);}catch(Exception){}body.WakeUp();}
            }
        }
        catch(Exception ex){how+=" (wake: "+ex.Message+")";}
        return how;
    }
    // Down already: the ragdoll is pushed where the fist landed.
    private void PushDown(NPC npc,Collider? collider,Vector3 point,Vector3 direction,float speed)
    {
        var rb=collider!=null?collider.attachedRigidbody:null;
        if(rb==null)return;
        string woke=WakeRagdoll(npc);
        if(rb.isKinematic){if(reports++<60)Bootstrap.Write("NPC HIT "+npc.name+" (down) at "+rb.name+": its body is held still (kinematic)"+woke);return;}
        float impulse=Math.Min(40f,Math.Max(1f,speed)*rb.mass*.6f);
        rb.AddForceAtPosition(direction*impulse,point,ForceMode.Impulse);rb.WakeUp();
        if(reports++<60)Bootstrap.Write("NPC HIT "+npc.name+" (down) pushed "+rb.name+" impulse="+impulse.ToString("F1")+woke);
    }
    // 0.1.142: the bones from the game's rig reference, or found by name (the
    // game's NPCs had no rig reference: no reaction at all). Looked for once
    // per NPC; one without bones still reacts (stun, the game's hit
    // animation) on a standing body's proportions.
    private readonly Dictionary<IntPtr,NpcBones?> boneCache=new();
    private NpcBones? BonesOf(NPC npc)
    {
        if(boneCache.TryGetValue(npc.Pointer,out var cached)&&(cached==null||cached.spineTop!=null))return cached;
        var bones=NpcBones.Of(npc,out string why);
        if(boneCache.Count>48)boneCache.Clear();
        boneCache[npc.Pointer]=bones;
        if(reports++<60)
        {
            if(bones==null)Bootstrap.Warn("NPC HIT "+npc.name+": no skeleton found ("+why+"); it reacts without bending");
            else Bootstrap.Write("NPC HIT "+npc.name+" bones from "+bones.Source+(why.Length>0?" ("+why+")":"")+": "+bones.Found+" found");
        }
        return bones;
    }
    private Body? BodyOf(NPC npc)
    {
        if(bodies.TryGetValue(npc.Pointer,out var b)&&b.Npc!=null&&b.Rig!=null&&b.Rig.spineTop!=null)return b;
        var rig=BonesOf(npc);
        if(rig==null)return null;
        b=new Body{Npc=npc,Id=npc.Pointer,Rig=rig};
        // The NPC's facing: its root (turned by the game as it walks); the
        // animator's transform if the root does not face level.
        var anim=npc.CharacterAnimator;b.Frame=npc.transform;
        if(Mathf.Abs(b.Frame.forward.y)>.7f&&anim!=null)b.Frame=anim.transform;
        if(anim!=null&&anim.transform!=npc.transform&&anim.transform.IsChildOf(npc.transform))b.Model=anim.transform;
        else b.Model=TopChild(rig.spineTop,npc.transform);
        b.Joints[(int)HitJoint.SpineLow]=rig.spineLower;b.Joints[(int)HitJoint.SpineMid]=rig.spineMedium;b.Joints[(int)HitJoint.SpineTop]=rig.spineTop;
        b.Joints[(int)HitJoint.Neck]=rig.neckBase;b.Joints[(int)HitJoint.Head]=rig.head;
        b.Joints[(int)HitJoint.ArmL]=rig.leftShoulder;b.Joints[(int)HitJoint.ArmR]=rig.rightShoulder;b.Joints[(int)HitJoint.ForearmL]=rig.leftElbow;b.Joints[(int)HitJoint.ForearmR]=rig.rightElbow;
        int found=0;foreach(var j in b.Joints)if(j!=null)found++;
        if(bodies.Count>24)Prune(true);
        bodies[npc.Pointer]=b;
        // 0.1.147: once a session, what the NPC is made of (the reactions did
        // not show: which components could move its bones or draw it).
        if(!dumpedParts)
        {
            dumpedParts=true;
            try
            {
                var names=new SortedDictionary<string,int>();
                foreach(var c in npc.GetComponentsInChildren(Il2CppType.Of<Component>(),true)){if(c==null)continue;string n=c.GetIl2CppType().Name;if(n=="Transform")continue;names.TryGetValue(n,out int k);names[n]=k+1;}
                var parts=new List<string>();foreach(var kv in names)parts.Add(kv.Key+(kv.Value>1?"x"+kv.Value:""));
                Bootstrap.Write("NPC HIT "+npc.name+" is made of: "+string.Join(", ",parts));
            }
            catch(Exception ex){Bootstrap.Write("NPC HIT components: "+ex.Message);}
        }
        if(WeaponOptions.NpcDrawnByMod.Value)
        {
            int drawn=0;foreach(var other in bodies.Values)if(other.Skin!=null)drawn++;
            if(drawn<MaxDrawn){b.Skin=NpcSkinOverlay.Create(npc,rig,out string how);if(reports++<60)Bootstrap.Write("NPC HIT "+npc.name+how);}
        }
        if(reports++<60)Bootstrap.Write("NPC HIT skeleton "+npc.name+": "+found+"/9 joints (spine "+Name(rig.spineLower)+"/"+Name(rig.spineMedium)+"/"+Name(rig.spineTop)+", neck "+Name(rig.neckBase)+", head "+Name(rig.head)
            +", arms "+Name(rig.leftShoulder)+"/"+Name(rig.leftElbow)+" "+Name(rig.rightShoulder)+"/"+Name(rig.rightElbow)+") facing="+b.Frame.forward.ToString("F2")+" from "+b.Frame.name
            +" head above the spine by "+(rig.Head.position.y-rig.spineTop!.position.y).ToString("F2")+" m");
        return b;
    }
    // A standing body (1.8 m) in the NPC's own frame, for one without bones.
    private static HitSkeleton StandingSkeleton()=>new HitSkeleton
    {
        SpineLow=new N(0,1.00f,0),SpineMid=new N(0,1.20f,0),SpineTop=new N(0,1.40f,0),NeckBase=new N(0,1.50f,0),NeckTop=new N(0,1.57f,0),Head=new N(0,1.68f,0),Chin=new N(0,1.58f,.08f),
        ShoulderL=new N(-.18f,1.42f,0),ShoulderR=new N(.18f,1.42f,0),ElbowL=new N(-.22f,1.15f,.05f),ElbowR=new N(.22f,1.15f,.05f),WristL=new N(-.22f,.92f,.12f),WristR=new N(.22f,.92f,.12f),
        HipL=new N(-.1f,.95f,0),HipR=new N(.1f,.95f,0),KneeL=new N(-.1f,.5f,0),KneeR=new N(.1f,.5f,0)
    };
    private static string Name(Transform? t)=>t!=null?t.name:"none";
    // 0.1.146: the NPC's animation handler: on it, or anywhere under or above it.
    private static readonly Dictionary<IntPtr,AIAnimationHandler?> animations=new();
    internal static AIAnimationHandler? AnimOf(NPC npc)
    {
        if(npc==null)return null;
        if(animations.TryGetValue(npc.Pointer,out var cached)&&cached!=null)return cached;
        AIAnimationHandler? a=null;
        try{a=npc.AIAnimation;}catch(Exception){}
        if(a==null)try{a=npc.m_aiAnimation;}catch(Exception){}
        if(a==null)try{a=npc.GetComponentInChildren(Il2CppType.Of<AIAnimationHandler>(),true)?.TryCast<AIAnimationHandler>();}catch(Exception){}
        if(a==null)try{a=npc.GetComponentInParent(Il2CppType.Of<AIAnimationHandler>())?.TryCast<AIAnimationHandler>();}catch(Exception){}
        if(a==null)try{var root=npc.transform.parent;if(root!=null)a=root.GetComponentInChildren(Il2CppType.Of<AIAnimationHandler>(),true)?.TryCast<AIAnimationHandler>();}catch(Exception){}
        if(animations.Count>64)animations.Clear();
        animations[npc.Pointer]=a;
        return a;
    }
    // The NPC's child that carries the given bone (the animator sits on the NPC itself).
    private static Transform? TopChild(Transform? bone,Transform npc)
    {
        for(var t=bone;t!=null;t=t.parent)if(t.parent!=null&&t.parent.Pointer==npc.Pointer)return t;
        return null;
    }
    private static Quaternion Yaw(Transform t){var f=t.forward;f.y=0;return f.sqrMagnitude<1e-6f?Quaternion.identity:Quaternion.LookRotation(f.normalized,Vector3.up);}
    private static HitSkeleton Skeleton(Body b,Quaternion inverse,Vector3 origin)
    {
        var r=b.Rig;
        N P(Transform? t,Vector3 fallback)=>ContactWorld.V(inverse*((t!=null?t.position:fallback)-origin));
        var head=r.Head.position;var top=r.spineTop!.position;
        var low=r.spineLower!=null?r.spineLower.position:top-Vector3.up*.3f;var mid=r.spineMedium!=null?r.spineMedium.position:(low+top)*.5f;
        var neckBase=r.neckBase!=null?r.neckBase.position:(top+head)*.5f;var neckTop=r.neckTop!=null?r.neckTop.position:(neckBase+head)*.5f;
        var right=b.Frame.right;right.y=0;right.Normalize();
        var k=new HitSkeleton
        {
            SpineLow=P(null,low),SpineMid=P(null,mid),SpineTop=P(null,top),NeckBase=P(null,neckBase),NeckTop=P(null,neckTop),Head=P(null,head),
            Chin=P(r.chin!=null?r.chin:r.jaw,head+Yaw(b.Frame)*new Vector3(0,-.05f,.08f)),
            ShoulderL=P(r.leftShoulder,top-right*.18f),ShoulderR=P(r.rightShoulder,top+right*.18f),
            ElbowL=P(r.leftElbow,top-right*.2f-Vector3.up*.25f),ElbowR=P(r.rightElbow,top+right*.2f-Vector3.up*.25f),
            WristL=P(r.leftWrist,top-right*.2f-Vector3.up*.5f),WristR=P(r.rightWrist,top+right*.2f-Vector3.up*.5f),
            HipL=P(r.leftHip,low-right*.1f),HipR=P(r.rightHip,low+right*.1f),KneeL=P(r.leftKnee,low-right*.1f-Vector3.up*.45f),KneeR=P(r.rightKnee,low+right*.1f-Vector3.up*.45f)
        };
        return k;
    }
    // ---- Disarm ----
    private string Disarm(NPC npc,Body? body,HitPlan? plan,Vector3 point,bool forced)
    {
        try
        {
            var inv=AnimOf(npc)?.Inventory;if(inv==null)inv=npc.GetComponent(Il2CppType.Of<EnemyInventory>())?.TryCast<EnemyInventory>();
            if(inv==null)return " (disarm: no inventory)";
            var weapon=inv.CurrentWeapon;var slot=inv.CurrentWeaponSlot;
            if(weapon==null||!Armed(slot))return "";
            if(disarmed.ContainsKey(inv.Pointer))return "";
            // A forearm hit disarms only the hand holding the gun (a long gun: either hand on it).
            if(!forced&&plan!=null&&body!=null&&plan.ForearmSide>=0)
            {
                var wrist=plan.ForearmSide==1?body.Rig.rightWrist:body.Rig.leftWrist;
                float near=wrist!=null?Vector3.Distance(wrist.position,weapon.transform.position):99;
                if(near>.35f&&Vector3.Distance(point,weapon.transform.position)>.3f)return " (not the gun hand: "+near.ToString("F2")+" m)";
            }
            var drops=npc.DropsHandler;bool before=npc.hasDroppedWeapons,dropped=false;string how="";
            if(drops!=null)
            {
                try{drops.TryDropWeapon(npc);dropped=npc.hasDroppedWeapons&&!before;how=dropped?"the game's drop":"";}
                catch(Exception ex){how="drop failed ("+ex.Message+")";}
                if(!dropped)
                {
                    try{var prefab=inv.GetWeaponPickup(weapon);if(prefab!=null){drops.DropWeapon(weapon,prefab);dropped=true;how="dropped as "+prefab.name;}}
                    catch(Exception ex){how+=" pickup failed ("+ex.Message+")";}
                }
            }
            npc.hasDroppedWeapons=true;
            disarmed[inv.Pointer]=inv;
            bool fists=false;
            try{inv.EquipWeapon(AIWeaponSlot.Fists,true);fists=inv.CurrentWeaponSlot==AIWeaponSlot.Fists;}
            catch(Exception ex){how+=" fists failed ("+ex.Message+")";}
            if(!fists)try{weapon.gameObject.SetActive(false);}catch(Exception){}
            // 0.1.145: and it fights on with its fists.
            string fight="";try{fight=MakeBrawler(npc,inv,weapon);}catch(Exception ex){fight=" (fist fight: "+ex.Message+")";}
            return " DISARMED "+weapon.name+" ("+how+(fists?"; fists now":"; gun hidden")+fight+")";
        }
        catch(Exception ex){return " (disarm: "+ex.Message+")";}
    }
    private static bool Armed(AIWeaponSlot s)=>s==AIWeaponSlot.Primary||s==AIWeaponSlot.Secondary||s==AIWeaponSlot.DualWield;
    private static bool EquipGuard(EnemyInventory __instance,AIWeaponSlot slot)=>!Blocked(__instance,slot);
    private static bool EquipInstantGuard(EnemyInventory __instance,AIWeaponSlot slot)=>!Blocked(__instance,slot);
    private static bool Blocked(EnemyInventory inv,AIWeaponSlot slot)
    {
        var c=Current;if(c==null||inv==null||!Armed(slot)||!c.disarmed.ContainsKey(inv.Pointer))return false;
        if(c.reports++<60)Bootstrap.Write("NPC HIT a disarmed NPC tried to draw a gun again ("+slot+"): refused");
        return true;
    }
    // A new weapon picked up, or a checkpoint restored: armed as the game says.
    private static void Rearmed(EnemyInventory __instance)
    {
        var c=Current;if(c==null||__instance==null)return;
        if(c.disarmed.Remove(__instance.Pointer)&&c.reports++<60)Bootstrap.Write("NPC HIT a disarmed NPC is armed again (a new weapon or a checkpoint)");
    }
    // ---- Every frame, after animation and IK ----
    // 0.1.146: the reaction was drawn only when the game's canvases
    // were redrawn (Canvas.willRenderCanvases), not in every frame. Now it is
    // drawn from the mod's own LateUpdate (every frame, after the game's
    // animation) and again at the canvas moment (after the game's own late
    // IK, just before skinning): the springs step once a frame, the bones are
    // written each time from the animated pose.
    private int drawFrames,canvasFrames,canvasFrame=-1;private float nextHookReport;
    internal void FromLateUpdate()=>Draw(false);
    private void Late()
    {

        if(!reportedHook){reportedHook=true;Bootstrap.Write("NPC HITS drawn after the game's animation (before skinning), every frame from LateUpdate and again at the canvas moment");}
        Draw(true);
    }
    // 0.1.147: just before the cameras draw - were the bones changed again
    // after the canvas moment (a diagnosis for the log)? - bent again, and
    // the enemies that react drawn by the mod from them.
    internal void BeforeCameras()
    {
        if(bodies.Count==0)return;
        try
        {
            foreach(var b in bodies.Values)
            {
                if(b.Frames<3||b.LateReported)continue;
                for(int i=0;i<HitPlan.Joints;i++){var t=b.Joints[i];if(t==null||!b.Has[i])continue;b.LateChecked++;if(Quaternion.Dot(t.localRotation,b.Applied[i])<=.999999f)b.LateChanged++;}
                if(b.LateChecked>=180&&reports++<80){b.LateReported=true;Bootstrap.Write("NPC HIT "+(b.Npc!=null?b.Npc.name:"?")+": between the canvas moment and the cameras "+b.LateChanged+" of "+b.LateChecked+" bone samples were changed again"+(b.LateChanged>0?" (something after the game's late update moves its bones: bent again now)":""));}
            }
            Draw(false);
            foreach(var b in bodies.Values)b.Skin?.Draw();
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC HIT drawing: "+ex.Message);}}
    }
    private void Draw(bool canvas)
    {
        int frame=Time.frameCount;bool first=appliedFrame!=frame;
        if(first){appliedFrame=frame;drawFrames++;}
        if(canvas&&canvasFrame!=frame){canvasFrame=frame;canvasFrames++;}
        Apply(first);ApplyGrabs();if(first)TickStuns();
        if(bodies.Count>0&&Time.realtimeSinceStartup>=nextHookReport&&reports<200)
        {
            nextHookReport=Time.realtimeSinceStartup+30;
            Bootstrap.Write("NPC HITS drawn in "+drawFrames+" frames; the canvas moment came in "+canvasFrames+" of them"+(canvasFrames<drawFrames*9/10?" (it does not come every frame: the reactions flickered before 0.1.146)":""));
        }
    }
    private readonly List<IntPtr> finished=new();
    private void Apply(bool step)
    {
        if(bodies.Count==0)return;
        try
        {
            float now=Time.time;finished.Clear();
            foreach(var pair in bodies)
            {
                var b=pair.Value;
                if(b.Npc==null||b.Frame==null||!b.Npc.IsAlive||!b.Npc.IsConscious||b.Npc.isRagdoll||b.Npc.isHeldByPlayer){finished.Add(pair.Key);continue;}
                var yaw=Yaw(b.Frame);
                brawlers.TryGetValue(pair.Key,out var brawler);
                b.FistL?.RestoreWrist();b.FistR?.RestoreWrist();b.Feet?.Restore();
                if(brawler!=null)
                {
                    if(!b.FistTried){b.FistTried=true;MakeFists(b);}
                    if(!b.FeetTried)
                    {
                        b.FeetTried=true;b.Feet=NpcFootwork.Create(b.Rig,b.Npc.transform);
                        if(reports++<80)Bootstrap.Write("NPC FIGHT "+b.Npc.name+" footwork: "+(b.Feet!=null?"planted boxer steps":"native locomotion fallback (no complete leg chain)"));
                    }
                    brawler.ProceduralFeet=b.Feet!=null;
                }
                if(step)
                {
                    float dt=b.LastTime<0?0:now-b.LastTime;b.LastTime=now;
                    var moved=b.State.Step(dt,now);
                    if(moved.LengthSquared()>1e-8f)Knock(b,yaw*ContactWorld.U(moved));
                    if(brawler!=null)
                    {
                        brawler.Guard=Mathf.MoveTowards(brawler.Guard,Holding(pair.Key)?0:1,dt*8f);
                        // 0.1.147: alive in its guard - a hunch, a sway from side to
                        // side, the chin down; its torso turns into each punch.
                        // 0.1.153: at its own rhythm and size, some crouched lower.
                        var st=brawler.Style;float t=now*st.BobRate+brawler.Sway,g=brawler.Guard,k=st.BobSize;
                        // 0.1.148: turned harder into the punch, leaning into it (and stepping, below).
                        float reach=brawler.Reach(now,out _),turn=(brawler.Left?1:-1)*BrawlMath.TorsoTurn*reach;
                        b.Extra[(int)HitJoint.SpineMid]=new System.Numerics.Vector3(.06f+st.Crouch+.03f*k*MathF.Sin(t*2)+BrawlMath.Lean*reach,turn*.4f,.06f*k*MathF.Sin(t))*g;
                        b.Extra[(int)HitJoint.SpineTop]=new System.Numerics.Vector3(.05f+st.Crouch*.5f,.05f*k*MathF.Sin(t*.7f)+turn*.6f,.04f*k*MathF.Sin(t))*g;
                        b.Extra[(int)HitJoint.Head]=new System.Numerics.Vector3(.02f,-turn*.5f,-.03f*k*MathF.Sin(t))*g;
                    }
                    else for(int i=0;i<HitPlan.Joints;i++)b.Extra[i]=System.Numerics.Vector3.Zero;
                    b.Frames++;
                }
                // 0.1.145: the whole body leans (about its feet) with the spine.
                if(b.Model!=null)
                {
                    var current=b.Model.localRotation;var place=b.Model.localPosition;
                    if(b.ModelHas&&Quaternion.Dot(current,b.ModelApplied)>.999999f&&(place-b.ModelAppliedAt).sqrMagnitude<1e-10f){current=b.ModelBase;place=b.ModelBaseAt;}
                    b.ModelBase=current;b.ModelBaseAt=place;b.Model.localRotation=current;b.Model.localPosition=place;
                    var lean=(b.State.Angle((int)HitJoint.SpineLow)+b.State.Angle((int)HitJoint.SpineMid)+b.State.Angle((int)HitJoint.SpineTop))*BodyLean;
                    var a=ContactWorld.U(lean);float angle=Math.Min(a.magnitude,MaxBodyLean);
                    if(angle>1e-4f)
                    {
                        // About its feet (the NPC's own position), not the hips.
                        var q=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,yaw*a.normalized);var feet=b.Npc.transform.position;
                        b.Model.rotation=q*b.Model.rotation;b.Model.position=feet+q*(b.Model.position-feet);
                    }
                    // 0.1.148: a brawler's body goes into its punch.
                    if(brawler!=null)
                    {
                        float reach=brawler.Reach(now,out _);
                        float guard=Mathf.Clamp01(brawler.Guard);
                        if(reach>0)b.Model.position+=yaw*Vector3.forward*(reach*BrawlMath.Lunge*guard);
                        if(b.Feet!=null)b.Model.position-=Vector3.up*((.045f+brawler.Style.Crouch*.4f+Math.Min(brawler.ActualSpeed,2f)*.035f)*guard);
                    }
                    b.ModelApplied=b.Model.localRotation;b.ModelAppliedAt=b.Model.localPosition;b.ModelHas=true;
                }
                if(brawler!=null)b.Feet?.Apply(b.Npc.transform,yaw,brawler.Style.LeadLeft,brawler.ActualSpeed,Mathf.Clamp01(brawler.Guard));
                // The spine, neck and head (parents first), then each arm: its
                // animated pose, a brawler's guard or punch (BrawlPose), the reaction.
                foreach(var j in Order)
                {
                    int i=(int)j;
                    if(j==HitJoint.ForearmL||j==HitJoint.ForearmR)continue;
                    if(j==HitJoint.ArmL||j==HitJoint.ArmR)
                    {
                        int f=j==HitJoint.ArmL?(int)HitJoint.ForearmL:(int)HitJoint.ForearmR;
                        Restore(b,i,step);Restore(b,f,step);
                        if(brawler!=null&&brawler.Guard>.001f)BrawlPose(b,brawler,j==HitJoint.ArmR,yaw,now);
                        React(b,i,yaw);React(b,f,yaw);
                        continue;
                    }
                    Restore(b,i,step);React(b,i,yaw);
                }
                // 0.1.148: a brawler's fingers curled into fists.
                if(brawler!=null)
                {
                    // Keep the fingers closed even when hit; only the guard/IK yields to the stun.
                    float g=Mathf.Clamp01((now-brawler.Since)/.25f);
                    b.FistL?.Clench(g);b.FistR?.Clench(g);
                    if(step)PunchContact(b,brawler,now);
                }
                if(!reportedApplied){reportedApplied=true;Bootstrap.Write("NPC HIT reaction on the skeleton of "+b.Npc.name+(b.Model!=null?" (the body leans too: "+b.Model.name+")":" (no model object under it to lean)"));}
                if(step&&!b.Reported&&b.Frames>=12&&b.Checked>0&&reports++<60){b.Reported=true;Bootstrap.Write("NPC HIT "+b.Npc.name+": the animation moved "+b.Written+" of "+b.Checked+" bone samples in "+b.Frames+" frames"+(b.Written==0?" (its bones are not animated: the bends may not show; the lean of the whole body does)":" (ours drawn on top)"));}
                if(step&&brawler==null&&b.State.Settled&&now-b.State.LastHit>.5f)finished.Add(pair.Key);
            }
            foreach(var id in finished)
            {
                if(bodies.TryGetValue(id,out var b))Release(b);
                bodies.Remove(id);
            }
        }
        catch(Exception ex){foreach(var body in bodies.Values)Release(body);bodies.Clear();if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC HIT skeleton: "+ex.Message);}}
    }
    // A joint back to its animated pose: what the animation wrote since our
    // last write, else the pose we started from (a bone the animation did
    // not write still has our reaction on it).
    private static void Restore(Body b,int i,bool count)
    {
        var t=b.Joints[i];if(t==null)return;
        var current=t.localRotation;bool ours=b.Has[i]&&Quaternion.Dot(current,b.Applied[i])>.999999f;
        if(count&&b.Has[i]){b.Checked++;if(!ours)b.Written++;}
        if(ours)current=b.Base[i];
        b.Base[i]=current;t.localRotation=current;
    }
    private static void React(Body b,int i,Quaternion yaw)
    {
        var t=b.Joints[i];if(t==null)return;
        var a=ContactWorld.U(b.State.Angle(i)+b.Extra[i]);float angle=a.magnitude;
        if(angle>1e-4f)t.rotation=Quaternion.AngleAxis(angle*Mathf.Rad2Deg,yaw*(a/angle))*t.rotation;
        b.Applied[i]=t.localRotation;b.Has[i]=true;
    }
    // Back to the animated pose (a bone the animation does not write keeps its base).
    private static void Release(Body b)
    {
        try{b.Skin?.Dispose();}catch(Exception){}b.Skin=null;
        try{b.FistL?.Release();b.FistR?.Release();b.Feet?.Release();}catch(Exception){}
        try{for(int i=0;i<HitPlan.Joints;i++){var t=b.Joints[i];if(t!=null&&b.Has[i]&&Quaternion.Dot(t.localRotation,b.Applied[i])>.999999f)t.localRotation=b.Base[i];}}
        catch(Exception){}
        try{if(b.Model!=null&&b.ModelHas&&Quaternion.Dot(b.Model.localRotation,b.ModelApplied)>.999999f){b.Model.localRotation=b.ModelBase;b.Model.localPosition=b.ModelBaseAt;}}catch(Exception){}
    }
    private void MakeFists(Body b)
    {
        b.FistL=NpcFist.Create(b.Npc,b.Rig.leftWrist,false,out string whyL);
        b.FistR=NpcFist.Create(b.Npc,b.Rig.rightWrist,true,out string whyR);
        if(reports++<80)Bootstrap.Write("NPC FIGHT "+b.Npc.name+" fists: left "+(b.FistL!=null?"clenched":"not ("+whyL+")")+", right "+(b.FistR!=null?"clenched":"not ("+whyR+")"));
    }
    private static void Knock(Body b,Vector3 step){try{MoveOnNavMesh(b.Npc,step);}catch(Exception){}}
    // 0.1.147: moved along the ground, never off the navigation mesh (through
    // a wall). An agent that does not move its NPC itself (the game's
    // movement moves it by the animation): the NPC and the agent both - an
    // agent's Move alone left these NPCs where they stood.
    internal static void MoveOnNavMesh(NPC npc,Vector3 delta)
    {
        delta.y=0;if(delta.sqrMagnitude<1e-10f)return;
        var t=npc.transform;UnityEngine.AI.NavMeshAgent? a=null;try{a=npc.NavMeshAgent;}catch(Exception){}
        if(a!=null&&a.enabled&&a.isOnNavMesh)
        {
            if(a.updatePosition){a.Move(delta);return;}
            var from=a.nextPosition;var to=from+delta;
            if(UnityEngine.AI.NavMesh.Raycast(from,to,out var hit,-1))to=hit.position;
            a.nextPosition=to;
            var p=t.position;p.x+=to.x-from.x;p.z+=to.z-from.z;t.position=p;
            return;
        }
        t.position+=delta;
    }
    private void Prune(bool all)
    {
        foreach(var b in bodies.Values)Release(b);
        bodies.Clear();
        var gone=new List<IntPtr>();foreach(var p in disarmed)if(p.Value==null)gone.Add(p.Key);foreach(var g in gone)disarmed.Remove(g);
    }
    internal void Clear(){EndAllStuns();EndAllBrawls();Prune(true);disarmed.Clear();}
    public void Dispose()
    {
        if(Current==this){Current=null;UnlinkHostages();}
        try{if(hook!=null)Canvas.remove_willRenderCanvases(hook);}catch(Exception){}
        hook=null;
        ReleaseAllGrabs("stopped");EndAllStuns();EndAllBrawls();
        if(fightMarkerUntil>0){fightMarkerUntil=0;try{System.IO.File.Delete(FightMarker);}catch(Exception){}}
        try{patches.UnpatchSelf();}catch(Exception){}
        Prune(true);disarmed.Clear();
    }
}
