using System;using XiiiXR;
class MeleeDamageMathTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(MeleeDamageMath.Strength(1)==0&&MeleeDamageMath.Strength(5)==1&&MeleeDamageMath.Strength(9)==1&&Math.Abs(MeleeDamageMath.Strength(3.25f)-.5f)<1e-4f&&MeleeDamageMath.Strength(float.NaN)==0,"strength");
  Check(MeleeDamageMath.Hits(0,false)==8&&MeleeDamageMath.Hits(1,false)==5,"fists: 8 light .. 5 hard blows");
  Check(MeleeDamageMath.Hits(0,true)==6&&MeleeDamageMath.Hits(1,true)==5,"a gun in the hand: 6 .. 5 blows (0.1.148)");
  Check(MeleeDamageMath.Hits(float.NaN,false)==8&&MeleeDamageMath.Hits(3,true)==5,"strength clamped");
  Check(MeleeDamageMath.AreaFactor(1)>1&&MeleeDamageMath.AreaFactor(2)==1&&MeleeDamageMath.AreaFactor(4)<1&&MeleeDamageMath.AreaFactor(0)==1&&MeleeDamageMath.AreaFactor(3)==1,"head more, arms like the body (0.1.142), legs less");
  Check(MeleeDamageMath.ThrownHits(0)==16&&MeleeDamageMath.ThrownHits(20)==10,"thrown weapons do a little");
  // damage = base x multiplier x body modifier = maxHealth / hits x area factor
  float m=MeleeDamageMath.Multiplier(300,100,1.5f,6,1,1);Check(Math.Abs(100*m*1.5f-50)<.01f,"multiplier "+m);
  Check(MeleeDamageMath.Multiplier(0,100,1,6,1,.7f)==.7f&&MeleeDamageMath.Multiplier(300,0,1,6,1,.7f)==.7f&&MeleeDamageMath.Multiplier(float.NaN,1,1,6,1,.7f)==.7f,"fallback");
  // 0.1.148: five blows at least from full health, the head included: no blow over a fifth of it.
  Check(Math.Abs(MeleeDamageMath.Share(145,5,1.4f)-29)<1e-3f&&Math.Abs(MeleeDamageMath.Share(145,8,1)-18.125f)<1e-3f&&Math.Abs(MeleeDamageMath.Share(145,5,1)-29)<1e-3f,"share capped at a fifth");
  Check(MeleeDamageMath.Share(0,5,1)==0&&MeleeDamageMath.Share(145,0,1)==0&&MeleeDamageMath.Share(float.NaN,5,1)==0&&Math.Abs(MeleeDamageMath.Share(100,8,float.NaN)-12.5f)<1e-3f,"share of nothing");
  {
   float hp=145;int blows=0;
   while(MeleeDamageMath.Standing(hp,MeleeDamageMath.Share(145,5,1.4f))){hp-=MeleeDamageMath.Share(145,5,1.4f);blows++;}
   Check(blows+1>=5,"hardest head punches from full health: down after "+(blows+1));
  }
  Check(MeleeDamageMath.Standing(145,29)&&!MeleeDamageMath.Standing(29,29)&&!MeleeDamageMath.Standing(29.4f,29)&&!MeleeDamageMath.Standing(float.NaN,29)&&!MeleeDamageMath.Standing(145,0),"standing after a blow");
  // The game's own damage held to the share (it multiplied a head punch into a knockout).
  Check(Math.Abs(MeleeDamageMath.Held(410,29,1)-29)<1e-3f&&MeleeDamageMath.Held(20,29,1)==20&&Math.Abs(MeleeDamageMath.Held(100,29,2)-14.5f)<1e-3f&&Math.Abs(MeleeDamageMath.Held(float.NaN,29,0)-580)<1e-2f,"held damage");
  // Blows to take an NPC down from full health, torso: fists 5..8, gun 5..6.
  for(float s=0;s<=1.001f;s+=.25f)
  {
   int fists=(int)Math.Ceiling(1/(1/MeleeDamageMath.Hits(s,false))-1e-4f),gun=(int)Math.Ceiling(MeleeDamageMath.Hits(s,true)-1e-4f);
   Check(fists>=5&&fists<=8&&gun>=5&&gun<=6,"blows at strength "+s+": fists "+fists+" gun "+gun);
  }
  // 0.1.150: the broom and the shovel: 5 (light) .. 3 (hard) blows, never fewer than 3; the chair and bottles stay one-blow things.
  Check(MeleeDamageMath.PropHits(0)==5&&MeleeDamageMath.PropHits(1)==3&&MeleeDamageMath.PropHits(float.NaN)==5&&MeleeDamageMath.PropHits(7)==3,"prop blows");
  Check(MeleeDamageMath.DurableProp("wpn_ms_broom")&&MeleeDamageMath.DurableProp("wpn_ms_shovel")&&MeleeDamageMath.DurableProp("WPN_MS_Mop")
   &&!MeleeDamageMath.DurableProp("wpn_ms_chair")&&!MeleeDamageMath.DurableProp("wpn_ms_bottle_03")&&!MeleeDamageMath.DurableProp("wpn_ms_ashtray")&&!MeleeDamageMath.DurableProp("wpn_combat_knife")&&!MeleeDamageMath.DurableProp(null)&&!MeleeDamageMath.DurableProp(""),"long-handled props told apart");
  Check(Math.Abs(MeleeDamageMath.Share(145,3,1.4f,MeleeDamageMath.MinPropBlows)-145f/3)<1e-3f&&Math.Abs(MeleeDamageMath.Share(145,5,1,MeleeDamageMath.MinPropBlows)-29)<1e-3f&&Math.Abs(MeleeDamageMath.Share(145,3,1.4f)-29)<1e-3f,"a prop's share capped at a third");
  for(float s=0;s<=1.001f;s+=.25f)
  {
   float hp=145,share=MeleeDamageMath.Share(145,MeleeDamageMath.PropHits(s),1.4f,MeleeDamageMath.MinPropBlows);int blows=1;
   while(MeleeDamageMath.Standing(hp,share)){hp-=share;blows++;}
   Check(blows>=3&&blows<=MeleeDamageMath.PropDurability,"shovel blows to the head at strength "+s+": "+blows);
  }
  Console.WriteLine("PASS: long-handled props (broom, shovel): 5 (light) .. 3 (hard) blows, never fewer than 3, down before the prop's 5th blow; chairs, bottles, ashtrays and knives not counted as them.");
  Console.WriteLine("PASS: melee damage: fists take 8 (light) .. 5 (hard) blows of the NPC's full health, a gun in the hand 6 .. 5, never fewer than 5 from full health (a blow at most a fifth; the game's own knockout of a punch held off before that), head x1.4, arms x1, legs x0.8; a thrown weapon 16 .. 10; the game's body modifier cancelled.");
 }
}
