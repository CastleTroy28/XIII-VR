using System;
using System.Numerics;
namespace XiiiXR;
// 0.1.114: "smarter enemies" — the game's own AI, nudged (never new behaviour
// the game does not have).
// 0.1.116: the AI's shared tuning values (search waits, step lengths, the
// randomness of combat choices...) turned out to be constants compiled into
// the game's code: they cannot be changed, and reading them closed the game
// (0.1.114/0.1.115). Only each enemy's own state is used now:
//  hunch: a searching enemy heads roughly (±25°) towards where the player is;
//  searching enemies play their idle ("scratch head") animations less often;
//  a search that starts lasts longer before the enemy gives up.
internal static class EnemyAiMath
{
    internal const float IdleTime=2.5f,SearchLonger=1.5f,MaxSearchDuration=300;
    internal const float HunchNoiseDegrees=25,HunchSeconds=3,HunchRange=60;
    // Horizontal direction from the NPC towards the player, turned by noise
    // (-1..1 of HunchNoiseDegrees). Zero when out of range or degenerate.
    internal static Vector3 Hunch(Vector3 npc,Vector3 player,float noise)
    {
        var d=new Vector3(player.X-npc.X,0,player.Z-npc.Z);float length=d.Length();
        if(!float.IsFinite(length)||length<.5f||length>HunchRange)return Vector3.Zero;
        float a=Math.Clamp(float.IsFinite(noise)?noise:0,-1,1)*HunchNoiseDegrees*MathF.PI/180;
        d/=length;
        return new Vector3(d.X*MathF.Cos(a)-d.Z*MathF.Sin(a),0,d.X*MathF.Sin(a)+d.Z*MathF.Cos(a));
    }
    // The enemy's "time since the last idle animation" grows IdleTime times
    // slower: take back that share of the game time that has passed.
    internal static float SlowIdle(float sinceIdle,float elapsed)
    {
        if(!float.IsFinite(sinceIdle)||!float.IsFinite(elapsed)||elapsed<=0||sinceIdle<=0)return sinceIdle;
        return MathF.Max(0,sinceIdle-MathF.Min(elapsed,1)*(1-1/IdleTime));
    }
    // A newly started search lasts SearchLonger times as long (only sensible
    // finite durations; "endless" or odd values are left alone).
    internal static float LongerSearch(float duration)=>float.IsFinite(duration)&&duration>0&&duration<MaxSearchDuration?duration*SearchLonger:duration;
    // 0.1.117: combat tactics. After the game scores each of an enemy's
    // possible actions, cover actions (go to cover and fire / aim / reload /
    // heal from it) weigh more, charging at the player (run to / follow the
    // target) weighs less for armed enemies that can take cover, and moving to
    // a new firing position weighs a little more than standing in the open.
    // Only viable actions (score > 0) are changed; a charge that is the only
    // option stays possible.
    internal enum Tactic{Cover,Charge,Move}
    internal const float CoverWeight=1.6f,ChargeWeight=.45f,MoveWeight=1.25f;
    internal static float Weight(Tactic t)=>t switch{Tactic.Cover=>CoverWeight,Tactic.Charge=>ChargeWeight,_=>MoveWeight};
    internal static int Weigh(int score,Tactic t)
    {
        if(score<=0)return score;
        double w=Math.Round(score*(double)Weight(t));
        return (int)Math.Clamp(w,1,int.MaxValue);
    }
    // The game's action classes and how each is weighed.
    internal static readonly (string name,Tactic tactic)[] Tactics=
    {
        ("AIEnemyCoverToFire",Tactic.Cover),("AIEnemyCoverToAim",Tactic.Cover),("AIEnemyCoverToReload",Tactic.Cover),("AIEnemyCoverToHeal",Tactic.Cover),
        ("AIEnemyRunToTarget",Tactic.Charge),("AIEnemyFollowTarget",Tactic.Charge),("AIEnemyFollowTargetAndFire",Tactic.Charge),
        ("AIEnemyRelocate",Tactic.Move)
    };
    // The game's AI is only touched in running gameplay (never at the menus,
    // in films or scripted scenes, or without a player), and only after
    // SettleSeconds of it, so the game has set its AI up first.
    internal const float SettleSeconds=3;
    internal static bool Gameplay(bool frontend,bool movie,bool scripted,bool gameplayCamera,bool player)=>!frontend&&!movie&&!scripted&&gameplayCamera&&player;
    internal static bool Settled(ref float since,float now,bool gameplay)
    {
        if(!gameplay||!float.IsFinite(now)){since=-1;return false;}
        if(since<0||since>now)since=now;
        return now-since>=SettleSeconds;
    }
}
