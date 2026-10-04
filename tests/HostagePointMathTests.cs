using System;using System.Collections.Generic;using XiiiXR;
class HostagePointMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static int First(params (float d,HostagePointMath.Hit k)[] hits)
 {var list=new List<(float,HostagePointMath.Hit,int)>();for(int i=0;i<hits.Length;i++)list.Add((hits[i].d,hits[i].k,i));return HostagePointMath.FirstNpc(list);}
 static void Main()
 {
  var P=HostagePointMath.Hit.Player;var T=HostagePointMath.Hit.Passable;var N=HostagePointMath.Hit.Npc;var W=HostagePointMath.Hit.World;
  Check(First((1.2f,N))==0,"a person straight ahead is not the target");
  Check(First((.1f,P),(.3f,T),(1.1f,N))==2,"the player's own body or a trigger volume blocks the ray");
  Check(First((.8f,W),(1.4f,N))==-1,"a person behind a wall is taken");
  Check(First((1.4f,W),(.9f,N))==1,"a wall behind the person blocks him (hits come unsorted)");
  Check(First((1.6f,N),(1.0f,N),(2f,W))==1,"the nearer of two people is not chosen");
  Check(First((.5f,T),(.7f,W))==-1&&First()==-1,"nothing to point at gives a target");
  Check(First((float.NaN,N),(1f,N))==1,"a broken hit distance is taken");
  // Both hands: the pressed one takes him, else the one already showing, else the right.
  Check(HostagePointMath.Side(true,true,0,1)==0&&HostagePointMath.Side(true,true,1,0)==1,"the hand whose grip went down does not take him");
  Check(HostagePointMath.Side(true,true,-1,0)==0&&HostagePointMath.Side(true,true,-1,1)==1,"the prompt jumps between two pointing hands");
  Check(HostagePointMath.Side(true,true,-1,-1)==1&&HostagePointMath.Side(true,false,-1,-1)==0&&HostagePointMath.Side(false,false,-1,-1)==-1,"default side wrong");
  Check(HostagePointMath.Side(true,false,1,-1)==0&&HostagePointMath.Side(false,true,0,0)==1,"a press of a hand not pointing at him takes him");
  // 0.1.216: the grip held still a moment takes him; closed and swung (a punch in his back) it does not.
  var Wt=HostagePointMath.Hold.Wait;var Tk=HostagePointMath.Hold.Take;var Cn=HostagePointMath.Hold.Cancel;
  Check(HostagePointMath.HoldStep(true,true,true,.1f,.02f)==Wt&&HostagePointMath.HoldStep(true,true,true,HostagePointMath.HoldTime,.05f)==Tk,"a still held grip does not take him after its time");
  Check(HostagePointMath.HoldStep(true,true,true,.1f,HostagePointMath.StillReach+.05f)==Cn,"a fist swung at him (a punch) takes him");
  Check(HostagePointMath.HoldStep(false,true,true,.2f,0)==Cn&&HostagePointMath.HoldStep(true,false,true,.2f,0)==Cn&&HostagePointMath.HoldStep(true,true,false,.2f,0)==Cn,"an opened grip, a hand pointing away or at another takes him");
  Check(HostagePointMath.HoldStep(true,true,true,1,float.NaN)==Cn&&HostagePointMath.HoldTime<=.5f&&HostagePointMath.StillReach>=.08f,"broken hand motion takes him, or the hold is too long / too strict");
  // 0.1.216: a hard punch of either fist in his back knocks him out.
  var fw=new System.Numerics.Vector3(0,0,1);var at=new System.Numerics.Vector3(0,0,0);
  Check(KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(0,1.7f,-.8f))&&KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(.4f,1.7f,-.7f)),"a punch from behind is not from behind");
  Check(!KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(0,1.7f,.8f))&&!KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(.8f,1.7f,0))&&!KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(.8f,1.7f,-.4f)),"from the front or the side counts as behind");
  Check(!KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(0,1.7f,-3))&&!KnockoutMath.FromBehind(System.Numerics.Vector3.Zero,at,new System.Numerics.Vector3(0,1.7f,-.8f))&&!KnockoutMath.FromBehind(fw,at,new System.Numerics.Vector3(float.NaN,0,0)),"out of reach, no facing or a broken head counts");
  Check(KnockoutMath.Hard(KnockoutMath.HardSpeed)&&KnockoutMath.Hard(6)&&!KnockoutMath.Hard(1.5f)&&!KnockoutMath.Hard(float.NaN)&&KnockoutMath.HardSpeed>=2.5f,"a touch or a jab knocks out");
  Check(KnockoutMath.Knocks(true,true,true,true,false)&&!KnockoutMath.Knocks(false,true,true,true,false)&&!KnockoutMath.Knocks(true,false,true,true,false)&&!KnockoutMath.Knocks(true,true,false,true,false)&&!KnockoutMath.Knocks(true,true,true,false,false)&&!KnockoutMath.Knocks(true,true,true,true,true),"a weapon, the front, a light blow, a man down or a hostage knocks out");
  // 0.1.218: allies (the game's Ally characters, or ones the player may neither hurt nor take) are left alone.
  Check(AllyRule.Ally(true,true,true)&&AllyRule.Ally(false,false,false)&&!AllyRule.Ally(false,true,false)&&!AllyRule.Ally(false,false,true),"who is an ally");
  Check(AllyRule.HostageByGame(false,true,false)&&AllyRule.HostageByGame(false,false,true)&&!AllyRule.HostageByGame(false,false,false)&&!AllyRule.HostageByGame(true,true,true),"the game's hostage rule");
  // 0.1.230: in a memory (a playable flashback) everyone is left alone, as the game takes no hostage there (Kim).
  Check(AllyRule.Ally(false,true,false,true)&&AllyRule.Ally(false,true,true,true)&&!AllyRule.Ally(false,true,false,false)&&!AllyRule.HostageByGame(false,true,false,true)&&!AllyRule.HostageByGame(false,true,true,true),"someone in a flashback taken hostage, punched or grabbed");
  Console.WriteLine("PASS: 0.1.230 in a memory (a playable flashback) nobody is taken hostage, punched or grabbed (the game's own rule there; Kim in the last mission).");
  Console.WriteLine("PASS: 0.1.218 the player's allies (the game's Ally characters, or ones the game lets him neither hurt nor take hostage) are left alone; the VR hostage rule only takes those the game's own rule allows.");
  Console.WriteLine("PASS: 0.1.216 a pointed hostage is taken by the grip held still a moment (closed and swung it is a punch); a hard punch of a fist in an enemy's back (60 degrees of straight behind, within reach, 3 m/s) knocks him out, nothing else does.");
  Console.WriteLine("PASS: 0.1.213 the hand's ray finds the nearest person, passing the player's own body, triggers and loose things, stopped by walls; with both hands the pressed one takes him, the shown one keeps the prompt, else the right.");
 }
}
