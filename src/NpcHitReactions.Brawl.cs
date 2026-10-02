using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using PlayMagic.Weapons;
using T5AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.186: a separate guarded fight after disarm. The game still owns health,
// navigation and ragdolls; the mod owns the brawler's choices and layered pose.
internal sealed partial class NpcHitReactions
{
    private sealed class Brawler
    {
        internal NPC Npc=null!;internal EnemyInventory Inv=null!;internal Combatant? Fighter;internal AIEnemyBehaviour? Ai;
        internal Equipable? Fists,Old;internal OwnerInfo Owner;internal bool HasOwner;
        internal AIEnemyArchetype? Original,Clone;internal bool OriginalFollow;internal AIEnemyFunction? Run;
        internal float Since,NextSwing,SwingAt=-1,LastGameMelee=-10,LastGameHurt=-10,AnimCheckAt;
        internal bool Left,AnimChecked;internal int Swings,Landed,Missed,GameMelees=0,GameHurts;
        // 0.1.146: its fists up in a guard (0..1, down while stunned) and the
        // punch thrown with one of them (drawn by BrawlPose).
        internal float Guard,SwingStart=-10;
        // 0.1.147: its fight (BrawlPlan: guard, step in, punches, step back,
        // block, counter), hit since the last look, its own AI paused while
        // it fights, walking or standing.
        internal BrawlPlan Plan=new();internal bool Struck,AiPaused,AiWasEnabled,Walking,WalkSet;internal float Sway;internal int PlanReports;
        // 0.1.148: its legs (the game's animator handler run by the mod while
        // its AI is paused), its root motion held off, a hook or a straight.
        internal string LegState="";internal float WalkSince,NextLegCheck;internal bool LegsReported,HandlerFailed,RootMotion,Hook;internal Animator? Animator;
        // 0.1.153: its own way of fighting; the animator's own speed before the mod changed it.
        internal BrawlStyle Style=>Plan.Style;internal float AnimSpeed=-1;
        internal Vector3 SwingAim,PreviousFist,ContactPoint;internal bool ContactSpent=true,HasPreviousFist,ProceduralFeet;
        internal float ActualSpeed,BlockedFor,NextSight;internal bool Sight;
        internal bool AgentSaved,AgentStopped,AgentRotation;
        // Where its punch is now (0 guard .. 1 out), at its own punch speed.
        internal float Reach(float now,out bool pulling)=>BrawlMath.Punch((now-SwingStart)*Style.PunchSpeed,out pulling);
    }
    private readonly Dictionary<IntPtr,Brawler> brawlers=new();
    private readonly List<IntPtr> brawlGone=new();
    private readonly System.Random brawlRandom=new();
    private PlayerState? playerState;private Transform? playerStateRoot;private Collider? playerCollider;
    private MeleeComponent? playerFists;private Transform? playerFistsRoot;private float nextFistsLook;
    private readonly Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> noSoundParameters=new();
    private int brawlControlFrame=-1;
    private bool ownPunch;private float nextBrawlError,fightMarkerUntil;private int fightReports;private Vector3 lastHead;private CameraRig? brawlRig;
    private static bool fightAiTried;
    internal const string FistEvent="event:/SFX/WPN/Sfx_FistImpact";
    internal int Brawlers=>brawlers.Count;
    private static string FightMarker=>Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-npc-fight.pending");
    // At start: the previous run closed while the game's AI of a brawler was changed.
    private static void RecoverFightMarker()
    {
        try
        {
            if(!File.Exists(FightMarker))return;
            File.Delete(FightMarker);
            if(WeaponOptions.NpcFistFightGameAi.Value)WeaponOptions.NpcFistFightGameAi.Value=false;
            Bootstrap.Warn("NPC FIGHT: the previous run closed while a disarmed enemy was switched to the game's close-combat AI; that part is off now ([Hands] NpcFistFightGameAi), the mod's own punches of such enemies stay");
        }
        catch(Exception ex){Bootstrap.Warn("NPC FIGHT marker: "+ex.Message);}
    }
    // ---- A disarmed enemy becomes a brawler (from Disarm) ----
    private string MakeBrawler(NPC npc,EnemyInventory inv,Equipable? old)
    {
        if(!WeaponOptions.NpcFistFight.Value||npc==null||inv==null)return "";
        if(brawlers.ContainsKey(npc.Pointer))return "";
        float now=Time.time;
        var br=new Brawler{Npc=npc,Inv=inv,Old=old,Since=now,NextSwing=now+.9f};
        try{br.Fighter=npc.TryCast<Combatant>();br.Ai=br.Fighter?.AIBehaviour;}catch(Exception){}
        try{if(br.Ai!=null&&br.Ai.isAlly)return "; an ally: it does not fight";}catch(Exception){}
        string how="; it fights with its fists"+FistsPose(br);
        // Who hits the player: the enemy's own fists (or the gun it had).
        foreach(var e in new[]{br.Fists,old})
        {
            if(e==null)continue;
            try{var o=e.GetOwner();if(!o.IsInvalid&&!o.IsPlayer&&o.Id!=0){br.Owner=o;br.HasOwner=true;break;}}catch(Exception){}
        }
        // Its body (bones) for the guard and the punches drawn by the mod.
        try{if(BodyOf(npc)==null)how+=" (no skeleton: no guard drawn)";}catch(Exception ex){how+=" (skeleton: "+ex.Message+")";}
        // 0.1.147: its own AI paused while it
        // fights (no running, no game melee); the mod moves it in short steps.
        // 0.1.153: its own way of fighting (not all alike).
        br.Plan=new BrawlPlan(BrawlStyle.From(()=>(float)brawlRandom.NextDouble()),now+.4f);var st=br.Style;
        how+=StopFiring(npc);
        how+=PauseAi(br);
        br.Sway=st.Phase;
        how+="; its style: "+(st.LeadLeft?"left":"right")+" fist leads, pace x"+st.Speed.ToString("F2")+", eagerness x"+st.Aggression.ToString("F2")+", circles "+st.Circle.ToString("F2")+", punches x"+st.PunchSpeed.ToString("F2")+", hooks "+(st.Hook*100).ToString("F0")+" %";
        brawlers[npc.Pointer]=br;
        return how;
    }
    // Its weapon animation set to the fists.
    private static string FistsPose(Brawler br)
    {
        try
        {
            var anim=AnimOf(br.Npc);if(anim==null)return " (the game has no animation handler on it: the mod draws its fists up)";
            Equipable? fists=null;try{fists=br.Inv.fistsWeapon;}catch(Exception){}
            br.Fists=fists;br.AnimCheckAt=Time.time+1f;
            var before=anim.WeaponState;string how="";
            try{anim.ToggleAiming(false);anim.ToggleIsShooting(false,true);anim.ToggleIsShooting(false,false);anim.ToggleAimDownSights(false);}catch(Exception){}
            if(fists!=null&&before!=AIAnimationHandler.AnimationWeaponState.Fists)
            {
                try{anim.SetupWeaponTransition(fists,br.Old!=null?br.Old:fists,false,false);how=" transition";}
                catch(Exception ex)
                {
                    how=" (transition: "+ex.Message+")";
                    try{anim.SetupWeaponAnimation(fists);how+=" set";}catch(Exception ex2){how+=" (set: "+ex2.Message+")";}
                }
            }
            try{anim.ToggleCombat(true);}catch(Exception){}
            return " (pose "+before+" -> "+anim.WeaponState+how+(fists==null?"; it has no fists weapon":"")+")";
        }
        catch(Exception ex){return " (fists pose: "+ex.Message+")";}
    }
    // A second later: still not the fists' pose - set straight away.
    private void CheckFistsPose(Brawler br)
    {
        br.AnimChecked=true;
        try
        {
            var anim=AnimOf(br.Npc);if(anim==null)return;
            var state=anim.WeaponState;string how="";
            if(state!=AIAnimationHandler.AnimationWeaponState.Fists&&br.Fists!=null)
            {
                try{anim.SetupWeaponAnimation(br.Fists);how=" -> set: "+anim.WeaponState;}catch(Exception ex){how=" (set: "+ex.Message+")";}
            }
            if(fightReports++<60)Bootstrap.Write("NPC FIGHT "+br.Npc.name+" pose after 1 s: "+state+how+" slot="+br.Inv.CurrentWeaponSlot+" weapon="+(br.Inv.CurrentWeapon!=null?br.Inv.CurrentWeapon.name:"none")+" transition="+anim.InWeaponTransition);
        }
        catch(Exception ex){if(fightReports++<60)Bootstrap.Write("NPC FIGHT pose check: "+ex.Message);}
    }
    // The game's AI of it: close combat (a copy of its archetype; the
    // shared one stays as it was for the other enemies).
    private string GameAiMelee(Brawler br)
    {
        var b=br.Ai!;
        bool first=!fightAiTried;
        if(first){fightAiTried=true;try{File.WriteAllText(FightMarker,"npc fight");}catch(Exception){}fightMarkerUntil=Time.realtimeSinceStartup+8;}
        try
        {
            var arch=b.myselfArchetypesParameters;br.Original=arch;br.OriginalFollow=b.forceFollowTarget;
            AIEnemyArchetype? clone=null;
            if(arch!=null)clone=UnityEngine.Object.Instantiate(arch).TryCast<AIEnemyArchetype>();
            if(clone!=null)
            {
                clone.name=arch!.name+" (fists, XIII VR)";
                clone.canMelee=true;clone.canFollowTarget=true;clone.canFire=false;clone.canAim=false;clone.canSnipe=false;clone.canHeavyFire=false;clone.canThrowGrenade=false;
                clone.canCoverToFire=false;clone.canCoverToAim=false;clone.canCoverToSnipe=false;clone.canCamp=false;
                clone.range=AIEnemyArchetype.AIEnemyRange.Melee;clone.idealRangeInMeter=1;
                b.myselfArchetypesParameters=clone;br.Clone=clone;
            }
            var use=clone??arch;bool melee=false,run=false;var added=new List<string>();
            var list=b.m_functions;
            if(list!=null)
            {
                foreach(var f in list)
                {
                    if(f==null)continue;
                    if(clone!=null&&(f.m_archetype==null||arch==null||f.m_archetype.Pointer==arch.Pointer))f.m_archetype=clone;
                    if(f.TryCast<AIEnemyMeleeHit>()!=null)melee=true;
                    if(f.TryCast<AIEnemyRunToTarget>()!=null){run=true;br.Run=f;}
                }
                if(use!=null)
                {
                    if(!melee){list.Add(new AIEnemyMeleeHit(b,use));added.Add("melee hit");}
                    if(!run){var r=new AIEnemyRunToTarget(b,use);list.Add(r);br.Run=r;added.Add("run to target");}
                }
            }
            b.forceFollowTarget=true;
            string interrupt="";try{b.Interrupt(AIStateChangeReason.GotHit);}catch(Exception ex){interrupt=", interrupt: "+ex.Message;}
            return " (game AI: "+(clone!=null?"close-combat copy of "+arch!.name:"archetype unchanged")+(added.Count>0?", added "+string.Join(" and ",added):"")+interrupt+")";
        }
        catch(Exception ex){return " (game AI: "+ex.Message+")";}
    }
    // Back to its own archetype (armed again, e.g. a checkpoint).
    private static void RestoreAi(Brawler br)
    {
        try
        {
            var b=br.Ai;if(b==null||br.Clone==null||br.Original==null)return;
            if(b.myselfArchetypesParameters!=null&&b.myselfArchetypesParameters.Pointer==br.Clone.Pointer)b.myselfArchetypesParameters=br.Original;
            var list=b.m_functions;if(list!=null)foreach(var f in list)if(f!=null&&f.m_archetype!=null&&f.m_archetype.Pointer==br.Clone.Pointer)f.m_archetype=br.Original;
            b.forceFollowTarget=br.OriginalFollow;
        }
        catch(Exception){}
    }
    // ---- Every frame (from WeaponHands.Tick while the player has control) ----
    internal void TickBrawl(CameraRig rig)
    {
        if(fightMarkerUntil>0&&Time.realtimeSinceStartup>fightMarkerUntil){fightMarkerUntil=0;try{File.Delete(FightMarker);}catch(Exception){}}
        if(brawlers.Count==0)return;
        try
        {
            brawlRig=rig;brawlControlFrame=Time.frameCount;float now=Time.time;var head=rig.HeadPosition;lastHead=head;brawlGone.Clear();
            foreach(var pair in brawlers)
            {
                var br=pair.Value;var npc=br.Npc;
                if(npc==null||!npc.IsAlive||!npc.IsConscious||npc.isRagdoll||br.Inv==null||!disarmed.ContainsKey(br.Inv.Pointer)){brawlGone.Add(pair.Key);continue;}
                if(npc.isHeldByPlayer){CancelPunch(br,now);brawlGone.Add(pair.Key);continue;}
                if(!br.AnimChecked&&now>=br.AnimCheckAt)CheckFistsPose(br);
                var at=npc.transform.position;var to=head-at;to.y=0;float distance=to.magnitude;
                var forward=npc.transform.forward;forward.y=0;
                float dot=distance>1e-3f&&forward.sqrMagnitude>1e-6f?Vector3.Dot(forward.normalized,to/distance):0;
                bool held=Holding(pair.Key);
                if(held)CancelPunch(br,now);
                if(now>=br.NextSight){br.NextSight=now+.15f;br.Sight=ClearBrawlPath(br,npc.transform.position+Vector3.up*1.25f,head-Vector3.up*.2f,.02f);}
                bool canAttack=!held&&br.Sight&&dot>.72f&&AttackSlot(br);
                if(br.BlockedFor>.25f&&br.Plan.Act==BrawlAct.StepIn)CancelPunch(br,now);
                bool struck=br.Struck&&!held;if(!held)br.Struck=false;
                var before=br.Plan.Act;
                float move=br.Plan.Step(now,distance,held,struck,(float)brawlRandom.NextDouble(),(float)brawlRandom.NextDouble(),canAttack);
                if(br.Plan.Act!=before&&br.PlanReports++<24&&fightReports<200)Bootstrap.Write("NPC FIGHT "+npc.name+" "+br.Plan.Act+(struck?" (hit)":"")+" at "+distance.ToString("F2")+" m");
                if(br.Plan.SwingStarted)Swing(br,now,br.Plan.LeftFist);
                else if(br.Plan.Act!=BrawlAct.Swing){br.ContactSpent=true;br.HasPreviousFist=false;br.SwingStart=-10;}
                if(!br.Sight)move=br.Plan.Act==BrawlAct.Close?move*.6f:0;
                if(!held)Face(br,to);
                Step(br,distance>1e-3f?to/distance:Vector3.zero,held?0:move,held?0:br.Plan.Side,dot);
            }
            foreach(var id in brawlGone)EndBrawl(id,true);
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextBrawlError){nextBrawlError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC FIGHT: "+ex.Message);}}
    }
    private static void Face(Brawler br,Vector3 to)
    {
        if(to.sqrMagnitude<1e-6f)return;
        if(br.Plan.Act==BrawlAct.Swing&&(Time.time-br.SwingStart)*br.Style.PunchSpeed>=BrawlMath.WindUp)return;
        // 0.1.153: side on, the lead fist's shoulder towards the player (a boxer's stance).
        var dir=Quaternion.Euler(0,(br.Style.LeadLeft?1:-1)*br.Style.Blade*Mathf.Clamp01(br.Guard),0)*to.normalized;
        // Native AI is paused; there is a single owner of rotation.
        try{var t=br.Npc.transform;t.rotation=Quaternion.RotateTowards(t.rotation,Quaternion.LookRotation(dir,Vector3.up),200f*Time.deltaTime);}catch(Exception){}
    }
    // 0.1.146: its arms drawn by the mod
    // over the game's animation - both fists up in front of the chin (a
    // guard), and the punching one thrown at the player's head and back
    // (BrawlMath.Punch). Two-bone reach: the upper arm turns so the elbow
    // lies where the fist can reach the point (elbow down and out), then the
    // forearm turns the wrist onto it.
    private void BrawlPose(Body b,Brawler br,bool right,Quaternion yaw,float now)
    {
        var rig=b.Rig;var upper=right?rig.rightShoulder:rig.leftShoulder;var lower=right?rig.rightElbow:rig.leftElbow;var end=right?rig.rightWrist:rig.leftWrist;
        if(upper==null||lower==null||end==null)return;
        var fwd=yaw*Vector3.forward;var side=yaw*Vector3.right*(right?1:-1);
        var head=rig.Head.position;var chin=head-Vector3.up*.06f+fwd*.05f;
        // 0.1.153: its own guard - the lead fist out in front and a little
        // low, the rear one by the chin, higher or wider as it likes, and both
        // never quite still (the lead one pumping, the rear one bobbing).
        var st=br.Style;bool lead=right!=st.LeadLeft;float bob=now*st.BobRate+st.Phase;
        var guard=chin+side*(.12f+st.GuardWide)-Vector3.up*(.10f-st.GuardHigh)+fwd*(.22f+st.GuardForward);
        if(lead)guard+=fwd*(.07f+.03f*MathF.Sin(bob*1.3f)*st.BobSize)-Vector3.up*.03f+side*.02f;
        else guard+=-fwd*.04f+Vector3.up*(.03f+.015f*MathF.Sin(bob*.9f+1.3f)*st.BobSize)-side*.02f;
        // 0.1.147: blocking: both fists up before the face.
        if(br.Plan.Act==BrawlAct.Block)guard=chin+side*.07f+Vector3.up*.06f+fwd*.17f;
        var target=guard;
        float t=(now-br.SwingStart)*st.PunchSpeed,reach=BrawlMath.Punch(t,out bool pulling);
        var pole=upper.position-Vector3.up*.30f+side*.25f-fwd*.05f;
        bool punching=br.Left!=right;
        if(reach>0&&punching)
        {
            var at=br.SwingAim;if((at-head).sqrMagnitude>9)at=head+fwd*1.2f;
            var start=t<=BrawlMath.HitDelay+BrawlMath.Hold?guard-fwd*.10f+side*(br.Hook?.08f:.03f):guard;
            target=Vector3.Lerp(start,at,reach);
            // 0.1.148: a hook comes round from the side, the elbow up and out.
            if(br.Hook){float arc=BrawlMath.HookArc(reach);target+=side*(BrawlMath.HookWidth*arc)+Vector3.up*(.05f*arc);pole=upper.position+side*.40f+Vector3.up*.05f-fwd*.10f;}
        }
        // 0.1.148: cocked back before it goes (a hook: out to the side too).
        else if(pulling&&punching)target=guard-fwd*(.10f*BrawlMath.Cock(t))+side*((br.Hook?.08f:.03f)*BrawlMath.Cock(t));
        // The other fist tucked to the chin while one punches.
        else if(!punching&&(reach>0||pulling))target=guard-fwd*.05f+Vector3.up*.03f-side*.03f;
        if(br.Plan.Act==BrawlAct.Feint&&lead)
        {float u=Mathf.Clamp01((now-br.Plan.Since)/.32f);target+=fwd*(.10f*MathF.Sin(u*MathF.PI));}
        float weight=Mathf.Clamp01(br.Guard);
        ArmReach.TwoBone(upper,lower,end,target,pole,weight);
        // Align the knuckles with the forearm, not the inherited pistol-grip wrist.
        var fist=right?b.FistR:b.FistL;
        var guardDirection=(fwd*.65f+Vector3.up*.75f-side*.10f).normalized;
        var direction=Vector3.Slerp(guardDirection,(end.position-lower.position).normalized,punching?reach:0);
        var back=Vector3.Slerp(side,Vector3.up,punching?reach*.85f:0);
        fist?.Orient(direction,back,weight);
    }
    // 0.1.147: its own AI paused while it fights: no running at the player,
    // no melee of the game's; its navigation agent held (the mod's steps go
    // through it, so never through a wall).
    private static string PauseAi(Brawler br)
    {
        string how="";
        try{if(br.Ai!=null){br.AiWasEnabled=br.Ai.enabled;br.Ai.enabled=false;br.AiPaused=true;how+=" (its AI paused while it fights)";}}catch(Exception ex){how+=" (AI: "+ex.Message+")";}
        try{var a=br.Npc.NavMeshAgent;if(a!=null&&a.enabled&&a.isOnNavMesh){br.AgentSaved=true;br.AgentStopped=a.isStopped;br.AgentRotation=a.updateRotation;a.ResetPath();a.isStopped=true;a.updateRotation=false;a.velocity=Vector3.zero;how+=" (its agent moves it itself: "+a.updatePosition+")";}}catch(Exception){}
        // 0.1.148: the mod moves it; a walk that moved it too would double its steps.
        try
        {
            Animator? anim=null;try{anim=br.Ai?.m_animatorHandler?.m_animator;}catch(Exception){}
            if(anim==null)anim=br.Npc.CharacterAnimator;
            br.Animator=anim;
            if(anim!=null&&anim.applyRootMotion){anim.applyRootMotion=false;br.RootMotion=true;how+=" (its root motion held off)";}
            // 0.1.153: its animations at its own pace (no two walk in step).
            if(anim!=null&&br.AnimSpeed<0){br.AnimSpeed=anim.speed;anim.speed=br.AnimSpeed*br.Style.Pace;}
        }
        catch(Exception){}
        return how;
    }
    private static void ResumeAi(Brawler br)
    {
        try{if(br.RootMotion&&br.Animator!=null)br.Animator.applyRootMotion=true;}catch(Exception){}
        br.RootMotion=false;
        try{if(br.AnimSpeed>=0&&br.Animator!=null)br.Animator.speed=br.AnimSpeed;}catch(Exception){}
        br.AnimSpeed=-1;
        // Navigation may exist on a rig without an AI handler; restore it independently.
        try{var a=br.Npc.NavMeshAgent;if(br.AgentSaved&&a!=null&&a.enabled&&a.isOnNavMesh){a.isStopped=br.AgentStopped;a.updateRotation=br.AgentRotation;a.velocity=Vector3.zero;}}catch(Exception){}
        br.AgentSaved=false;
        if(!br.AiPaused)return;br.AiPaused=false;
        try{if(br.Ai!=null)br.Ai.enabled=br.AiWasEnabled;}catch(Exception){}
    }
    // Its step along the line to the player (m/s; - away), and its legs.
    // 0.1.153: and sideways (m/s, + to its right as it faces the player): round the player, weaving.
    private void Step(Brawler br,Vector3 toPlayer,float speed,float sideways,float facing)
    {
        float dt=Math.Min(Time.deltaTime,.05f);if(dt<=0)return;
        var npc=br.Npc;var right=Vector3.Cross(Vector3.up,toPlayer);
        Vector3 velocity=Vector3.zero;bool wanted=Math.Abs(speed)+Math.Abs(sideways)>.05f;
        try
        {
            var agent=npc.NavMeshAgent;
            if(wanted&&agent!=null&&agent.enabled&&agent.isOnNavMesh)
            {
                var delta=(toPlayer*speed+right*sideways)*dt;
                var before=agent.nextPosition;MoveOnNavMesh(npc,delta);velocity=(agent.nextPosition-before)/dt;
            }
            // No valid navigation: stand and guard. Never translate directly through world geometry.
        }
        catch(Exception){}
        velocity.y=0;br.ActualSpeed=velocity.magnitude;
        br.BlockedFor=wanted&&br.ActualSpeed<.05f?br.BlockedFor+dt:0;
        if(br.BlockedFor>.25f){br.Plan.Interrupt(Time.time);br.ContactSpent=true;br.SwingStart=-10;}
        var local=Quaternion.Inverse(npc.transform.rotation)*velocity;
        Legs(br,br.ActualSpeed>.04f,local.z,local.x);
    }
    // 0.1.148:
    // the game's animator handler of an enemy is run by its AI, and with the
    // AI paused nothing ran it, so the walk asked of it never reached the
    // animator. Now the mod runs it every frame (after asking for the walk or
    // the stand) and sets the speed after it; if the animator is still not in
    // the walk (or the stand) a moment later, the mod cross-fades into it.
    private void Legs(Brawler br,bool walking,float forward,float sideways=0)
    {
        // Procedural feet use a stable native idle underlay; an armed walk must not fight the IK.
        if(br.ProceduralFeet){walking=false;forward=sideways=0;}
        T5AI.AIAnimatorHandler? h=null;try{h=br.Ai?.m_animatorHandler;}catch(Exception){}
        float now=Time.time;
        if(h==null)
        {
            if(!br.LegsReported&&walking){br.LegsReported=true;if(fightReports++<60)Bootstrap.Write("NPC FIGHT "+br.Npc.name+" legs: no animator handler of the game on it");}
            return;
        }
        if(walking!=br.Walking||!br.WalkSet)
        {
            br.Walking=walking;br.WalkSet=true;br.WalkSince=now;br.NextLegCheck=now+.35f;
            try{if(walking)h.PlayWalkAnimation(false);else h.PlayIdleAnimation(false);}catch(Exception){}
            // The state it asked for (the game's own names; its constants are never read by the mod).
            try{var n=h.m_fullBodyAnimationNameToPlay;if(string.IsNullOrEmpty(n))n=h.m_currentLegState;br.LegState=n??"";}catch(Exception){br.LegState="";}
        }
        if(br.AiPaused&&!br.HandlerFailed)
        {
            try{h.Update();}
            catch(Exception ex){br.HandlerFailed=true;if(fightReports++<60)Bootstrap.Write("NPC FIGHT "+br.Npc.name+" legs: the game's animator handler: "+ex.Message+" (the mod cross-fades them itself)");}
        }
        try{h.SetVelocityAnimatorParameters(Math.Clamp(forward/1.5f,-1,1),Math.Clamp(sideways/1.5f,-1,1));}catch(Exception){}
        if(now>=br.NextLegCheck){br.NextLegCheck=now+.5f;FadeLegs(br,h,walking,false);}
        if(!br.LegsReported&&walking&&now-br.WalkSince>1f){br.LegsReported=true;if(fightReports++<60)Bootstrap.Write("NPC FIGHT "+br.Npc.name+" legs: "+FadeLegs(br,h,walking,true));}
    }
    // Into the game's walk (or stand) state on the layer that has it; with
    // report, what the animator is doing (for the log).
    private static string FadeLegs(Brawler br,T5AI.AIAnimatorHandler h,bool walking,bool report)
    {
        try
        {
            var a=br.Animator;if(a==null)try{a=h.m_animator;}catch(Exception){}
            if(a==null)return "no animator";
            string key=br.LegState;
            int layer=-1,hash=0;
            if(!string.IsNullOrEmpty(key))
            {
                int k=Animator.StringToHash(key);
                for(int l=0;l<a.layerCount&&layer<0;l++)if(a.HasState(l,k)){layer=l;hash=k;}
            }
            string state="";bool faded=false;
            if(layer>=0)
            {
                var info=a.GetCurrentAnimatorStateInfo(layer);
                bool there=info.shortNameHash==hash||info.fullPathHash==hash;
                if(!there&&!a.IsInTransition(layer)&&!report){a.CrossFadeInFixedTime(hash,.2f,layer);faded=true;}
                state=" layer "+layer+" ("+a.GetLayerName(layer)+") "+(there?"in it":"not in it")+(faded?", cross-faded":"");
            }
            if(!report)return "";
            string leg="";try{leg=" handler leg state="+h.m_currentLegState+" to play="+h.m_fullBodyAnimationNameToPlay;}catch(Exception){}
            var parameters=new List<string>();
            try{foreach(var p in a.parameters){if(parameters.Count>=16)break;parameters.Add(p.name+(p.type==AnimatorControllerParameterType.Float?"="+a.GetFloat(p.nameHash).ToString("F2"):p.type==AnimatorControllerParameterType.Bool?"="+a.GetBool(p.nameHash):""));}}catch(Exception){}
            return (walking?"walk":"stand")+" '"+key+"'"+(layer<0?" not found on its animator":state)+";"+leg+"; root motion "+(br.RootMotion?"held off":"off")+"; parameters "+string.Join(", ",parameters)+(br.HandlerFailed?"; the handler failed":"");
        }
        catch(Exception ex){return "legs: "+ex.Message;}
    }
    private static void NavMeshAgentRef(NPC npc,out UnityEngine.AI.NavMeshAgent? agent){agent=null;try{agent=npc.NavMeshAgent;}catch(Exception){}}
    private void Swing(Brawler br,float now,bool left)
    {
        br.Left=left;br.Swings++;br.SwingStart=now;br.SwingAt=now+BrawlMath.HitDelay/br.Style.PunchSpeed;
        br.Hook=br.Swings>1&&brawlRandom.NextDouble()<br.Style.Hook;
        br.SwingAim=lastHead-Vector3.up*(br.Swings%3==0?.38f:.04f);
        br.ContactSpent=false;br.HasPreviousFist=false;
        // No native melee animation/event: it could add invisible damage and overwrite the guard.
    }
    // The punch lands on the player (the game's damage), or misses.
    private void Land(Brawler br,CameraRig rig,Vector3 head,float distance,float dot)
    {
        var npc=br.Npc;
        if(br.ContactSpent||Holding(npc.Pointer)||brawlControlFrame!=Time.frameCount||!BrawlMath.Lands(distance,dot))
        {
            br.Missed++;
            if(br.Missed<=3&&fightReports++<60)Bootstrap.Write("NPC FIGHT "+npc.name+" swings and misses ("+distance.ToString("F2")+" m, facing "+dot.ToString("F2")+")");
            return;
        }
        br.ContactSpent=true;
        var state=PlayerStateOf(rig.PlayerRoot);
        if(state==null){if(fightReports++<60)Bootstrap.Write("NPC FIGHT "+npc.name+" lands a punch, but the player's health was not found");return;}
        float damage=BrawlMath.Damage(state.MaxHealth,WeaponOptions.NpcPunchDamage.Value);
        var chest=npc.transform.position+Vector3.up*1.35f;
        var at=br.ContactPoint;
        var direction=at-chest;if(direction.sqrMagnitude<1e-6f)direction=npc.transform.forward;direction.Normalize();
        OwnerInfo owner;
        if(br.HasOwner)owner=br.Owner;
        else{var faction=default(NPC.Faction);try{faction=npc.faction;}catch(Exception){}owner=new OwnerInfo(npc.gameObject.GetInstanceID(),false,faction,false,false,-1);}
        float before=state.currentHealth;string result="";
        ownPunch=true;
        try
        {
            string weaponId="wpn_fists";try{if(br.Fists!=null&&!string.IsNullOrEmpty(br.Fists.identifier))weaponId=br.Fists.identifier;}catch(Exception){}
            state.ReceiveDamage(damage,new DamageInfo(weaponId,DamageType.Melee),1f,playerCollider!,at,owner,chest,direction,NPC.DamageArea.Body);
        }
        catch(Exception ex){result=" (damage: "+ex.Message+")";}
        finally{ownPunch=false;}
        br.Landed++;
        FistSound(rig.PlayerRoot,at);
        rig.PunchHaptics(true);rig.PunchHaptics(false);
        if(br.Landed<=6&&fightReports++<60)Bootstrap.Write("NPC FIGHT "+npc.name+" punched the player ("+(br.Left?"left":"right")+" fist): damage "+damage.ToString("F1")+" health "+before.ToString("F0")+" -> "+state.currentHealth.ToString("F0")+"/"+state.MaxHealth.ToString("F0")+result);
    }
    private PlayerState? PlayerStateOf(Transform? root)
    {
        if(root==null)return null;
        try
        {
            if(playerState==null||playerStateRoot==null||root.Pointer!=playerStateRoot.Pointer)
            {
                playerStateRoot=root;
                playerState=root.GetComponentInChildren(Il2CppType.Of<PlayerState>(),true)?.TryCast<PlayerState>()
                    ??root.GetComponentInParent(Il2CppType.Of<PlayerState>())?.TryCast<PlayerState>();
                playerCollider=root.GetComponentInChildren(Il2CppType.Of<CharacterController>(),true)?.TryCast<Collider>()
                    ??root.GetComponentInChildren(Il2CppType.Of<Collider>(),false)?.TryCast<Collider>();
            }
            return playerState;
        }
        catch(Exception){playerState=null;return null;}
    }
    // The player's own fists' impact on flesh (the sound the player hears when
    // punching an enemy).
    private void FistSound(Transform? root,Vector3 at)
    {
        try
        {
            var fists=PlayerFists(root);
            string path=FistEvent;SurfaceDetailsSFX? detail=null;
            if(fists!=null)
            {
                var table=fists.surfaceHitVisualAudioInfo;
                if(!string.IsNullOrWhiteSpace(fists.overrideFmodEvent))path=fists.overrideFmodEvent;
                else if(table!=null&&!string.IsNullOrWhiteSpace(table.fmodEvent))path=table.fmodEvent;
                detail=table?.ObtainSFXSurfaceDetails(SurfaceDetection.SurfaceTypes.Flesh);
            }
            PropStudioSound.Play(path,at,detail?.paramToAmount??noSoundParameters);
        }
        catch(Exception ex){if(Time.realtimeSinceStartup>=nextBrawlError){nextBrawlError=Time.realtimeSinceStartup+5;Bootstrap.Warn("NPC FIGHT sound: "+ex.Message);}}
    }
    private MeleeComponent? PlayerFists(Transform? root)
    {
        if(root==null)return playerFists;
        if(playerFists!=null&&playerFistsRoot!=null&&root.Pointer==playerFistsRoot.Pointer)return playerFists;
        if(Time.realtimeSinceStartup<nextFistsLook)return null;
        nextFistsLook=Time.realtimeSinceStartup+2;
        foreach(var obj in root.GetComponentsInChildren(Il2CppType.Of<MeleeComponent>(),true))
        {
            var m=obj.TryCast<MeleeComponent>();
            if(m?.baseEquipable?.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Fist){playerFists=m;playerFistsRoot=root;return m;}
        }
        return null;
    }
    private void EndBrawl(IntPtr id,bool report)
    {
        if(!brawlers.TryGetValue(id,out var br))return;
        brawlers.Remove(id);br.ContactSpent=true;
        if(bodies.TryGetValue(id,out var body)){Release(body);bodies.Remove(id);}
        bool alive=false;try{alive=br.Npc!=null&&br.Npc.IsAlive&&br.Npc.IsConscious;}catch(Exception){}
        if(alive)RestoreAi(br);
        try{if(br.Npc!=null){ResumeAi(br);if(!alive&&br.Ai!=null)br.Ai.enabled=false;}}catch(Exception){}
        if(report&&fightReports++<60)Bootstrap.Write("NPC FIGHT "+(br.Npc!=null?br.Npc.name:"an enemy")+" stops fighting ("+(alive?"armed again":"down")+"): "+br.Swings+" swings, "+br.Landed+" landed, "+br.Missed+" missed; the game's own punches "+br.GameMelees+" ("+br.GameHurts+" hurt)");
    }
    private void EndAllBrawls(){foreach(var id in new List<IntPtr>(brawlers.Keys))EndBrawl(id,false);brawlers.Clear();}
    // ---- The game's own melee of a brawler, and its damage on the player ----
    private static bool NpcUseMelee(Combatant __instance)
    {
        var c=Current;if(c==null||__instance==null)return true;
        if(c.Holding(__instance.Pointer))return false;
        // Queued native animation events can survive disabling AI. Only our swept punch deals damage.
        if(c.brawlers.ContainsKey(__instance.Pointer))return false;
        return true;
    }
    private static void PlayerHurt(float damage,Vector3 hitPosition)
    {
        var c=Current;if(c==null||c.ownPunch||!(damage>0)||c.brawlers.Count==0)return;
        try{c.GameHurt(damage,hitPosition);}catch(Exception){}
    }
    private void GameHurt(float damage,Vector3 at)
    {
        float now=Time.time;Brawler? by=null;float best=2.5f;
        foreach(var br in brawlers.Values)
        {
            if(br.Npc==null||now-br.LastGameMelee>1.5f)continue;
            var d=br.Npc.transform.position-lastHead;d.y=0;float m=d.magnitude;
            if(m<best){best=m;by=br;}
        }
        if(by==null)return;
        by.LastGameHurt=now;by.GameHurts++;
        if(!float.IsFinite(at.sqrMagnitude)||(at-lastHead).sqrMagnitude>4)at=lastHead-Vector3.up*.3f;
        FistSound(brawlRig?.PlayerRoot,at);
        brawlRig?.PunchHaptics(true);brawlRig?.PunchHaptics(false);
        if(by.GameHurts<=3&&fightReports++<60)Bootstrap.Write("NPC FIGHT the game's own punch of "+by.Npc.name+" hurt the player ("+damage.ToString("F1")+"): the fist sound added");
    }
}
