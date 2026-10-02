using System;
using System.Collections.Generic;
using System.IO;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using HarmonyLib;
using T5AI;
using UnityEngine;
namespace XiiiXR;
// 0.1.114: "smarter enemies" (VR setting, on by default) nudges the game's own
// enemy AI (T5AI).
// 0.1.115: nothing of the AI is touched outside running gameplay
// (EnemyAiMath.Settled), before live enemies exist, or before the game itself
// has initialised the AI class concerned (native class state, read without
// running any of the game's code). Each first touch is bracketed by a marker
// file: if a run ever ends inside one, the next start turns the option off.
// 0.1.116: 0.1.114/0.1.115 closed the game when reading the AI's shared tuning
// values: those are constants compiled into the game (the interop exposes
// every constant, even Int32.MaxValue, as a static field) and can neither be
// read safely nor changed. The mod now never touches T5AI statics; it works
// only with each enemy's own state and the game's own methods:
//  - searching enemies get a hunch of the player's direction (±25°),
//  - they play fewer idle animations while searching,
//  - a newly started search lasts longer.
// 0.1.117: the 0.1.116 "pursue" archetype flag made enemies charge the player;
// it is gone. Instead, after the game scores an enemy's possible actions,
// cover actions weigh more and charging weighs less (EnemyAiMath.Tactics), by
// small Harmony postfixes on the game's own Evaluate methods, applied per
// action class only once the game has set that class up. The game still
// chooses; the log counts what it chose.
// 0.1.121: the choice counter (a postfix on every enemy's Evaluate, every
// frame, with a type-name lookup each call) is gone; the log counts the
// weighed scores. The enemy search runs every 2 s.
internal sealed class EnemyAi:IDisposable
{
    private sealed class Watch{internal IntPtr Function;internal float Visit=-1,NextHunch;}
    private const string Assembly="_XIII.dll",Namespace="T5AI";
    private static readonly string[] Steps={"listing enemies","reading enemy setup","lengthening a search","calming a searching enemy","steering a searching enemy","patching AIEnemyBehaviour"};
    private static bool KnownStep(string step)=>Array.IndexOf(Steps,step)>=0||step.StartsWith("patching AIEnemy",StringComparison.Ordinal);
    private static EnemyAi? current;
    private readonly Harmony patches=new("xiii.vr.xrbootstrap.enemyai");
    private readonly HashSet<string> patched=new();
    private int weighed;
    private readonly CameraRig rig;
    private readonly List<AIEnemyBehaviour> enemies=new();
    private readonly Dictionary<int,Watch> watch=new();
    private readonly HashSet<string> reported=new(),ready=new(),probed=new(),waiting=new();
    private readonly System.Random random=new();
    private float gameplaySince=-1,nextScan,nextVisit,nextError,nextReport,nextPatch;private int hunches,longer,calmer;
    private readonly SceneFind<AIEnemyBehaviour> found;
    internal EnemyAi(CameraRig owner)
    {
        rig=owner;current=this;Recover();
        found=new SceneFind<AIEnemyBehaviour>("enemies",12,list=>Probe("listing enemies",()=>
        {
            foreach(var obj in Resources.FindObjectsOfTypeAll(Il2CppType.Of<AIEnemyBehaviour>()))
            {var b=obj.TryCast<AIEnemyBehaviour>();if(b!=null&&b.gameObject.scene.IsValid())list.Add(b);}
        }));
        if(EnemyOptions.Reenabled)Bootstrap.Write("ENEMY AI smarter enemies switched back on for the rebuilt version (0.1.116)");
    }
    internal void Tick()
    {
        try
        {
            if(!EnemyOptions.Smarter.Value){if(enemies.Count>0)Restore();return;}
            float now=Time.realtimeSinceStartup;
            bool gameplay=EnemyAiMath.Gameplay(rig.Frontend,rig.MovieActive,rig.Scripted,rig.GameplayCameraRendering,rig.PlayerRoot!=null);
            // 0.1.162: the tactics hooks one a frame, as soon as the game has
            // set each class up (all eight in one frame stopped the game for
            // about 90 ms a few seconds into a mission).
            if(patched.Count<EnemyAiMath.Tactics.Length&&now>=nextPatch){nextPatch=now+.2f;PatchTactics(1);}
            if(!EnemyAiMath.Settled(ref gameplaySince,now,gameplay)){if(!gameplay)enemies.Clear();return;}
            // 0.1.162: the enemies found are kept; the scene search runs every
            // 12 s since 0.1.164 (and after a scene change), the kept ones are sorted every 2 s.
            bool searched=NativeReady("AIEnemyBehaviour")&&found.Refresh();
            if(searched||now>=nextScan){nextScan=now+2;Scan();}
            if(enemies.Count>0&&now>=nextVisit){nextVisit=now+.25f;Visit(now);}
        }
        catch(Exception ex)
        {
            if(Time.realtimeSinceStartup>=nextError){nextError=Time.realtimeSinceStartup+10;Bootstrap.Warn("ENEMY AI: "+ex.Message);}
        }
    }
    // The native class has been set up by the game (Class::Init done without
    // error). Reads IL2CPP's class record only.
    private static unsafe bool NativeClassReady(string name)
    {
        IntPtr klass=IL2CPP.GetIl2CppClass(Assembly,Namespace,name);
        return klass!=IntPtr.Zero&&UnityVersionHandler.Wrap((Il2CppClass*)klass).InitializedAndNoError;
    }
    private bool NativeReady(string name)
    {
        if(ready.Contains(name))return true;
        bool ok;try{ok=NativeClassReady(name);}catch(Exception){ok=false;}
        if(ok){ready.Add(name);Bootstrap.Write("ENEMY AI native "+name+" ready");}
        else if(waiting.Add(name))Bootstrap.Write("ENEMY AI waiting for the game to set up "+name);
        return ok;
    }
    private static string Marker=>Path.Combine(BepInEx.Paths.ConfigPath,"XIII-XR-enemy-ai.pending");
    private void Recover()
    {
        try
        {
            if(!File.Exists(Marker))return;
            string step=File.ReadAllText(Marker).Trim();File.Delete(Marker);
            if(!KnownStep(step)){Bootstrap.Write("ENEMY AI: marker from an older version ("+step+") removed; that step no longer exists");return;}
            if(EnemyOptions.Smarter.Value)EnemyOptions.Smarter.Value=false;
            Bootstrap.Warn("ENEMY AI: the previous run closed while "+step+"; smarter enemies turned off (VR SETTINGS turns them back on)");
        }
        catch(Exception ex){Bootstrap.Warn("ENEMY AI marker: "+ex.Message);}
    }
    // First time only: log, write the marker, act, remove the marker.
    private void Probe(string step,Action action)
    {
        if(probed.Contains(step)){action();return;}
        Bootstrap.Write("ENEMY AI first "+step);
        try{File.WriteAllText(Marker,step);}catch(Exception){}
        try{action();}
        finally{try{File.Delete(Marker);}catch(Exception){}}
        probed.Add(step);
    }
    private void Scan()
    {
        enemies.Clear();
        if(!NativeReady("AIEnemyBehaviour"))return;
        found.Prune();
        foreach(var b in found.Items)
        {
            try{if(b!=null&&b.isActiveAndEnabled)enemies.Add(b);}
            catch(Exception){}
        }
        WorldStats.Enemies=enemies.Count;
        if(enemies.Count==0)return;
        Probe("reading enemy setup",()=>{foreach(var b in enemies)Report(b);});
        if(watch.Count>256)watch.Clear();
    }
    // Each enemy's own state, 4 times a second.
    private void Visit(float now)
    {
        if(!NativeReady("AIEnemySearch"))return;
        var player=rig.PlayerRoot;if(player==null)return;
        var target=ContactWorld.V(player.position);float gameTime=Time.time;
        foreach(var b in enemies)
        {
            try
            {
                if(b==null)continue;int id=b.GetInstanceID();
                if(!watch.TryGetValue(id,out var w))watch[id]=w=new Watch();
                var function=b.m_currentFunction;IntPtr pointer=function==null?IntPtr.Zero:function.Pointer;
                bool started=pointer!=w.Function;w.Function=pointer;
                float elapsed=w.Visit<0?0:Math.Clamp(gameTime-w.Visit,0,1);w.Visit=gameTime;
                if(function==null)continue;
                var search=function.TryCast<AIEnemySearch>();if(search==null)continue;
                if(started)
                {
                    float duration=search.Duration,longerDuration=EnemyAiMath.LongerSearch(duration);
                    if(longerDuration!=duration){Probe("lengthening a search",()=>search.Duration=longerDuration);longer++;}
                }
                if(elapsed>0&&!search.m_isInIdleBreaker)
                {
                    float since=search.m_timeSinceLastIdle,slower=EnemyAiMath.SlowIdle(since,elapsed);
                    if(slower!=since){Probe("calming a searching enemy",()=>search.m_timeSinceLastIdle=slower);calmer++;}
                }
                if(now>=w.NextHunch)
                {
                    w.NextHunch=now+EnemyAiMath.HunchSeconds*(.75f+.5f*(float)random.NextDouble());
                    var me=b.Myself;if(me==null||!me.IsAlive)continue;
                    var d=EnemyAiMath.Hunch(ContactWorld.V(me.transform.position),target,(float)(random.NextDouble()*2-1));
                    if(d==System.Numerics.Vector3.Zero)continue;
                    Probe("steering a searching enemy",()=>search.SetSearchDirection(ContactWorld.U(d)));hunches++;
                }
            }
            catch(Exception){}
        }
        if(hunches+longer+weighed>0&&now>=nextReport)
        {
            nextReport=now+10;
            Bootstrap.Write("ENEMY AI search: hunches="+hunches+" (towards the player ±"+EnemyAiMath.HunchNoiseDegrees+"°) longerSearches="+longer+" calmerVisits="+calmer+"; tactics scores weighed="+weighed);
            hunches=longer=calmer=weighed=0;
        }
    }
    private void Restore()
    {
        enemies.Clear();watch.Clear();
        Bootstrap.Write("ENEMY AI smarter off (the tactics hooks stay installed but do nothing)");
    }
    // One Harmony postfix per action class, installed once the game has set
    // that class up (an enemy that can use it exists).
    private void PatchTactics(int most)
    {
        foreach(var (name,tactic) in EnemyAiMath.Tactics)
        {
            if(most<=0)return;
            if(patched.Contains(name)||!NativeReady(name))continue;
            patched.Add(name);most--;
            try
            {
                var type=typeof(AIEnemyBehaviour).Assembly.GetType(Namespace+"."+name)??throw new InvalidOperationException("type missing");
                var method=AccessTools.DeclaredMethod(type,"Evaluate")??throw new InvalidOperationException("Evaluate missing");
                string hook=tactic switch{EnemyAiMath.Tactic.Cover=>nameof(CoverScore),EnemyAiMath.Tactic.Charge=>nameof(ChargeScore),_=>nameof(MoveScore)};
                Probe("patching "+name,()=>patches.Patch(method,postfix:new HarmonyMethod(typeof(EnemyAi),hook)));
                Bootstrap.Write("ENEMY AI tactics: "+name+" weighed x"+EnemyAiMath.Weight(tactic)+" ("+tactic+")");
            }
            catch(Exception ex){Bootstrap.Warn("ENEMY AI tactics "+name+": "+ex.Message);}
        }
    }
    private static bool Weighing=>current!=null&&EnemyOptions.Smarter.Value;
    private static void CoverScore(AIEnemyFunction __instance){if(Weighing)Weigh(__instance,EnemyAiMath.Tactic.Cover);}
    private static void MoveScore(AIEnemyFunction __instance){if(Weighing)Weigh(__instance,EnemyAiMath.Tactic.Move);}
    private static void ChargeScore(AIEnemyFunction __instance)
    {
        if(!Weighing)return;
        try
        {
            // Only armed enemies that can fight from cover are held back;
            // melee-only enemies keep their only way to attack.
            var a=__instance.m_archetype;if(a==null||!(a.canCoverToFire||a.canCoverToAim)||!__instance.HasWeapon())return;
            Weigh(__instance,EnemyAiMath.Tactic.Charge);
        }
        catch(Exception){}
    }
    // An override that calls its base Evaluate (e.g. follow-and-fire over
    // follow) runs both postfixes: weigh each action once per frame.
    private static int weighFrame=-1;private static readonly HashSet<IntPtr> weighedNow=new();
    private static void Weigh(AIEnemyFunction f,EnemyAiMath.Tactic tactic)
    {
        try
        {
            int frame=Time.frameCount;if(frame!=weighFrame){weighFrame=frame;weighedNow.Clear();}
            if(!weighedNow.Add(f.Pointer))return;
            int s=f.Score,w=EnemyAiMath.Weigh(s,tactic);if(w!=s){f.Score=w;if(current!=null)current.weighed++;}
        }
        catch(Exception){}
    }
    // Diagnostics: the game's own per-enemy setup, once per archetype/skill.
    private void Report(AIEnemyBehaviour b)
    {
        if(reported.Count>=48)return;
        try
        {
            var a=b.myselfArchetypesParameters;string key=(a!=null?a.name:"?")+"/"+b.skillLevel;
            if(!reported.Add(key))return;
            var skill=b.m_skillLevel;var names=new List<string>();
            if(b.m_functions!=null)foreach(var f in b.m_functions)if(f!=null)names.Add((f.Name??"?")+":"+f.Duration.ToString("F1"));
            Bootstrap.Write("ENEMY AI enemy "+key+": intelligence="+(skill?.m_intelligence.ToString()??"?")+" precision="+(skill?.m_precision.ToString()??"?")
                +(a==null?"":" follow="+a.canFollowTarget+" cover="+a.canCoverToFire+" grenade="+a.canThrowGrenade+" roll="+a.canRoll+" step="+a.canStep+" range="+a.idealRangeInMeter)
                +" functions="+string.Join(",",names));
        }
        catch(Exception){}
    }
    public void Dispose()
    {
        if(current==this)current=null;
        try{patches.UnpatchSelf();}catch(Exception ex){Bootstrap.Warn("ENEMY AI unpatch: "+ex.Message);}
        enemies.Clear();watch.Clear();
    }
}
