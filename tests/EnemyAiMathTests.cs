using System;using System.Linq;using System.Numerics;using XiiiXR;
class EnemyAiMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var npc=new Vector3(0,0,0);var player=new Vector3(10,3,0);
  var straight=EnemyAiMath.Hunch(npc,player,0);Check(Vector3.Distance(straight,Vector3.UnitX)<1e-4f,"hunch not towards the player: "+straight);
  foreach(float n in new[]{-1f,-.4f,.3f,1f})
  {
   var d=EnemyAiMath.Hunch(npc,player,n);float angle=MathF.Acos(Math.Clamp(Vector3.Dot(d,Vector3.UnitX),-1,1))*180/MathF.PI;
   Check(MathF.Abs(d.Length()-1)<1e-4f&&d.Y==0&&angle<=EnemyAiMath.HunchNoiseDegrees+.01f,"hunch outside the noise cone or not horizontal: "+angle);
  }
  Check(EnemyAiMath.Hunch(npc,new Vector3(100,0,0),0)==Vector3.Zero,"hunch across the whole level");
  Check(EnemyAiMath.Hunch(npc,new Vector3(.1f,0,0),0)==Vector3.Zero&&EnemyAiMath.Hunch(npc,new Vector3(float.NaN,0,0),0)==Vector3.Zero,"degenerate hunch");
  // 0.1.116: per-enemy state only.
  Check(MathF.Abs(EnemyAiMath.SlowIdle(10,1)-(10-(1-1/EnemyAiMath.IdleTime)))<1e-5f,"idle timer not slowed");
  Check(EnemyAiMath.SlowIdle(.1f,1)==0&&EnemyAiMath.SlowIdle(0,1)==0&&EnemyAiMath.SlowIdle(5,0)==5&&EnemyAiMath.SlowIdle(5,-1)==5,"idle timer bounds");
  Check(EnemyAiMath.SlowIdle(10,30)==EnemyAiMath.SlowIdle(10,1),"a long pause takes back more than one second at once");
  Check(float.IsNaN(EnemyAiMath.SlowIdle(float.NaN,1))&&EnemyAiMath.SlowIdle(3,float.NaN)==3,"idle timer NaN handling");
  float t=0;for(int i=0;i<100;i++){t+=.25f;t=EnemyAiMath.SlowIdle(t,.25f);}
  Check(MathF.Abs(t-25/EnemyAiMath.IdleTime)<.01f,"idle timer does not grow "+EnemyAiMath.IdleTime+"x slower: "+t);
  Check(EnemyAiMath.LongerSearch(10)==10*EnemyAiMath.SearchLonger&&EnemyAiMath.SearchLonger>1,"search not longer");
  Check(EnemyAiMath.LongerSearch(0)==0&&EnemyAiMath.LongerSearch(-1)==-1&&EnemyAiMath.LongerSearch(EnemyAiMath.MaxSearchDuration)==EnemyAiMath.MaxSearchDuration&&float.IsNaN(EnemyAiMath.LongerSearch(float.NaN))&&float.IsPositiveInfinity(EnemyAiMath.LongerSearch(float.PositiveInfinity)),"odd search durations changed");
  // 0.1.117: cover weighs more, charging less; only viable (positive) scores change.
  Check(EnemyAiMath.Weigh(100,EnemyAiMath.Tactic.Cover)==160&&EnemyAiMath.Weigh(100,EnemyAiMath.Tactic.Charge)==45&&EnemyAiMath.Weigh(100,EnemyAiMath.Tactic.Move)==125,"tactic weights");
  Check(EnemyAiMath.Weigh(0,EnemyAiMath.Tactic.Cover)==0&&EnemyAiMath.Weigh(-5,EnemyAiMath.Tactic.Cover)==-5&&EnemyAiMath.Weigh(int.MinValue,EnemyAiMath.Tactic.Charge)==int.MinValue,"non-viable actions changed");
  Check(EnemyAiMath.Weigh(1,EnemyAiMath.Tactic.Charge)==1&&EnemyAiMath.Weigh(int.MaxValue,EnemyAiMath.Tactic.Cover)==int.MaxValue,"a viable charge became impossible or a score overflowed");
  Check(EnemyAiMath.CoverWeight>1&&EnemyAiMath.ChargeWeight<1&&EnemyAiMath.MoveWeight>1,"tactics would favour charging");
  Check(EnemyAiMath.Tactics.Count(t=>t.tactic==EnemyAiMath.Tactic.Cover)>=3&&EnemyAiMath.Tactics.Any(t=>t.name=="AIEnemyFollowTargetAndFire"&&t.tactic==EnemyAiMath.Tactic.Charge)&&EnemyAiMath.Tactics.All(t=>t.name.StartsWith("AIEnemy")),"tactics table");
  Console.WriteLine("PASS: smarter enemies weigh cover x1.6, charging x0.45, repositioning x1.25; non-viable actions untouched; a lone charge stays possible.");
  // 0.1.115: never at the menus / films / scripted scenes / without a player; only after settling.
  Check(EnemyAiMath.Gameplay(false,false,false,true,true),"running gameplay rejected");
  Check(!EnemyAiMath.Gameplay(true,false,false,true,true)&&!EnemyAiMath.Gameplay(false,true,false,true,true)&&!EnemyAiMath.Gameplay(false,false,true,true,true)
   &&!EnemyAiMath.Gameplay(false,false,false,false,true)&&!EnemyAiMath.Gameplay(false,false,false,true,false),"AI touched at the menu, in a film, a scripted scene or without a player");
  float since=-1;
  Check(!EnemyAiMath.Settled(ref since,10,true)&&since==10,"settle timer not started");
  Check(!EnemyAiMath.Settled(ref since,10+EnemyAiMath.SettleSeconds-.1f,true),"acted before settling");
  Check(EnemyAiMath.Settled(ref since,10+EnemyAiMath.SettleSeconds,true),"never settles");
  Check(!EnemyAiMath.Settled(ref since,20,false)&&since<0&&!EnemyAiMath.Settled(ref since,21,true),"leaving gameplay does not restart the settle time");
  Check(!EnemyAiMath.Settled(ref since,float.NaN,true)&&since<0,"NaN time settles");
  since=50;Check(!EnemyAiMath.Settled(ref since,5,true)&&since==5,"clock going back settles at once");
  Console.WriteLine("PASS: smarter enemies act only in running gameplay after "+EnemyAiMath.SettleSeconds+" s (never at the menu, in films, scripted scenes or without a player).");
  Console.WriteLine("PASS: smarter enemies: search hunch towards the player within ±25 degrees (horizontal, within 60 m, never degenerate); idle timer grows 2.5x slower while searching; searches last 1.5x longer (odd durations untouched).");
 }
}
