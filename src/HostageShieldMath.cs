using System;
using System.Numerics;
namespace XiiiXR;
// Finite bullet segments against the authored primitive hurtboxes. No world
// AABB approximation, physics sync, scene search or artificial shield radius.
internal static class HostageShieldMath
{
 internal static bool Gangster(int faction)=>faction==8; // NPC.Faction.Terrorist, not aggregate masks or Government
 // 0.1.205: which of a hostage's hurtboxes takes a bullet that missed his
 // shape (0 first): his body, his head, an arm, a leg (NPC.DamageArea:
 // Body 0, Head 1, UpperLimb 2, Unknown 3, LowerLimb 4).
 internal static int ShieldRank(int area)=>area switch{0=>0,1=>1,2=>2,4=>3,_=>4};
 internal static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
 // 0.1.191: a held hostage's life is raised by `guard` for one hit (he cannot
 // die in the hands). The hit he took: the drop of that raised life, else the
 // game's own record of the life lost (a life the game clamped).
 internal static float Applied(float guardedBefore,float after,float hpLost,float guard)
 {
  float d=guardedBefore-after;
  if(float.IsFinite(d)&&d>0&&d<guard*.5f)return d;
  if(float.IsFinite(hpLost)&&hpLost>0&&hpLost<guard*.5f)return hpLost;
  return 0;
 }
 // His life after the hit: never below a sliver while held; mortal: the hit would have killed him.
 internal const float Sliver=1;
 internal static float Left(float before,float applied,out bool mortal)
 {
  if(!float.IsFinite(before))before=Sliver;
  if(!(applied>0)){mortal=false;return before;}
  float left=before-applied;mortal=left<=0;
  return Math.Max(Sliver,Math.Min(before,left));
 }
 internal static bool Sphere(Vector3 from,Vector3 to,Vector3 center,float radius,out float fraction)
 {
  fraction=0;if(!Finite(from)||!Finite(to)||!Finite(center)||!float.IsFinite(radius)||radius<=0)return false;
  var d=to-from;var o=from-center;float a=d.LengthSquared(),c=o.LengthSquared()-radius*radius;
  if(a<1e-10f)return false;if(c<=0)return true;
  float b=Vector3.Dot(o,d),disc=b*b-a*c;if(disc<0)return false;
  fraction=(-b-MathF.Sqrt(disc))/a;return fraction>=0&&fraction<=1;
 }
 internal static bool Capsule(Vector3 from,Vector3 to,Vector3 a,Vector3 b,float radius,out float fraction)
 {
  fraction=float.PositiveInfinity;
  if(!Finite(from)||!Finite(to)||!Finite(a)||!Finite(b)||!float.IsFinite(radius)||radius<=0)return false;
  var ba=b-a;float length=ba.LengthSquared();if(length<1e-10f)return Sphere(from,to,a,radius,out fraction);
  var oa=from-a;var rd=to-from;float dd=rd.LengthSquared();if(dd<1e-10f)return false;
  float along=Vector3.Dot(oa,ba),t=Math.Clamp(along/length,0,1);
  if((oa-t*ba).LengthSquared()<=radius*radius){fraction=0;return true;}
  float br=Vector3.Dot(ba,rd),ro=Vector3.Dot(rd,oa);
  float aa=length*dd-br*br,bb=length*ro-along*br,cc=length*(oa.LengthSquared()-radius*radius)-along*along;
  float disc=bb*bb-aa*cc;
  if(aa>1e-10f&&disc>=0)
  {
   float hit=(-bb-MathF.Sqrt(disc))/aa,y=along+hit*br;
   if(hit>=0&&hit<=1&&y>=0&&y<=length)fraction=hit;
  }
  if(Sphere(from,to,a,radius,out float h))fraction=Math.Min(fraction,h);
  if(Sphere(from,to,b,radius,out h))fraction=Math.Min(fraction,h);
  return fraction<=1;
 }
 internal static bool Box(Vector3 from,Vector3 to,Vector3 center,Vector3 size,out float fraction)
 {
  fraction=0;if(!Finite(from)||!Finite(to)||!Finite(center)||!Finite(size)||size.X<=0||size.Y<=0||size.Z<=0||(to-from).LengthSquared()<1e-10f)return false;
  var o=from-center;var d=to-from;var half=size*.5f;float lo=0,hi=1;
  for(int i=0;i<3;i++)
  {
   float p=i==0?o.X:i==1?o.Y:o.Z,v=i==0?d.X:i==1?d.Y:d.Z,h=i==0?half.X:i==1?half.Y:half.Z;
   if(Math.Abs(v)<1e-8f){if(p< -h||p>h)return false;continue;}
   float s=(-h-p)/v,e=(h-p)/v;if(s>e)(s,e)=(e,s);
   lo=Math.Max(lo,s);hi=Math.Min(hi,e);if(lo>hi)return false;
  }
  fraction=lo;return true;
 }
}
