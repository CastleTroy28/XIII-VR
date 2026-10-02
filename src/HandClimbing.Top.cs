using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
// 0.1.171. The hands pulled the body up only as far as
// the top rung and it hung there. Now hands pulling up near the ladder's top
// (the feet within 1.3 m of it) find the floor beyond the ladder and the body
// goes straight up until the feet clear it, then over onto it (LadderTopMath).
// The hands let go; a hand takes hold again only after its grip is released.
internal sealed partial class HandClimbing
{
 private readonly float[] top={float.NaN,float.NaN};private readonly Vector3[] middle=new Vector3[2];
 private readonly bool[] afterTop=new bool[2];
 private bool overTop,overEdge;private Vector3 landing;private float overSince,upSince,upFrom;private string landingName="";
 private readonly Il2CppStructArray<RaycastHit> hits=new(16);
 // The top of the ladder a hand took: its climb volume, or the ladder's parts near the hand (a parent may hold many ladders).
 private void Measure(int i,Transform found,ClimbVolume? volume,Collider touched,Vector3 hand)
 {
  top[i]=float.NaN;middle[i]=hand;
  try
  {
   bool has=false;var box=new Bounds();
   void Add(Bounds b){if(!has){box=b;has=true;}else box.Encapsulate(b);}
   var source=volume!=null?volume.transform:found;
   foreach(var o in source.GetComponentsInChildren(Il2CppType.Of<Collider>(),true))
   {
    var c=o.TryCast<Collider>();if(c==null||!c.enabled)continue;var b=c.bounds;
    if(volume==null){var d=b.center-hand;d.y=0;if(d.magnitude>1f)continue;}
    Add(b);
   }
   if(!has)Add(touched.bounds);
   top[i]=box.max.y;middle[i]=box.center;
  }
  catch(Exception){top[i]=float.NaN;}
 }
 private void TryOverTop(float pullUp)
 {
  if(character==null||player==null)return;
  int i=ladder[1]!=null&&!onFan[1]&&float.IsFinite(top[1])?1:ladder[0]!=null&&!onFan[0]&&float.IsFinite(top[0])?0:-1;if(i<0)return;
  if(ladder[1]!=null&&ladder[0]!=null&&float.IsFinite(top[0])&&float.IsFinite(top[1]))i=top[0]>top[1]?0:1;
  var feet=character.transform.position;
  if(!LadderTopMath.Ready(feet.y,top[i],pullUp))return;
  float now=Time.realtimeSinceStartup;if(now<nextTopTry)return;nextTopTry=now+.5f;
  // Away from the climber: from the feet to the ladder, level.
  var away=middle[i]-feet;away.y=0;
  if(away.sqrMagnitude<.0025f){away=character.transform.forward;away.y=0;}
  away.Normalize();
  foreach(float reach in new[]{.45f,.75f,1.05f})
  {
   var from=new Vector3(feet.x,top[i]+1.2f,feet.z)+away*reach;
   int n=Physics.RaycastNonAlloc(from,Vector3.down,hits,3f,~0,QueryTriggerInteraction.Ignore);
   float nearest=float.MaxValue;RaycastHit? found=null;
   for(int k=0;k<Math.Min(n,hits.Length);k++)
   {
    var h=hits[k];var c=h.collider;
    if(c==null||c.transform.IsChildOf(player)||ladder[i]!=null&&c.transform.IsChildOf(ladder[i]))continue;
    if(h.distance<nearest){nearest=h.distance;found=h;}
   }
   if(found is not RaycastHit hit)continue;
   if(!LadderTopMath.Landing(ContactWorld.V(feet),ContactWorld.V(middle[i]),ContactWorld.V(away),ContactWorld.V(hit.point),ContactWorld.V(hit.normal),top[i]))continue;
   overTop=true;overEdge=false;landing=hit.point;overSince=upSince=now;upFrom=feet.y;landingName=hit.collider.name;
   Drop(0);Drop(1);afterTop[0]=afterTop[1]=true;
   Bootstrap.Write("CLIMB over the top of the ladder onto "+landingName+" ("+(landing.y-feet.y).ToString("F2")+" m up, "+reach.ToString("F2")+" m beyond)");
   return;
  }
  if(now>=nextTopReport){nextTopReport=now+5;Bootstrap.Write("CLIMB at the top of the ladder: no floor found beyond it (the hands keep holding)");}
 }
 private float nextTopTry,nextTopReport;
 private void TickOverTop()
 {
  if(character==null){overTop=false;velocity=Vector3.zero;return;}
  float now=Time.realtimeSinceStartup;var feet=character.transform.position;
  bool wasOver=overEdge;
  var v=LadderTopMath.Velocity(ContactWorld.V(feet),ContactWorld.V(landing),ref overEdge,out bool done);
  if(done){overTop=false;velocity=Vector3.zero;Bootstrap.Write("CLIMB over the top: on "+landingName+" after "+(now-overSince).ToString("F1")+" s");return;}
  // Something above holds the body back, or it takes too long: give up (the game's own movement takes over).
  if(!overEdge){if(feet.y>upFrom+.05f){upFrom=feet.y;upSince=now;}else if(now-upSince>.4f){Stop("the body did not rise (something above?)");return;}}
  if(overEdge&&!wasOver)upSince=now;
  if(now-overSince>LadderTopMath.Total||!overEdge&&now-overSince>LadderTopMath.UpMax){Stop("it took too long");return;}
  velocity=ContactWorld.U(v);
 }
 private void Stop(string why){overTop=false;velocity=Vector3.zero;Bootstrap.Write("CLIMB over the top stopped: "+why);}
}
