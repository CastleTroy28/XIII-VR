using System;
using System.Collections.Generic;
using UnityEngine;
namespace XiiiXR;
// Adapters for the game's authored hinge/slider. No Rigidbody or scene scan.
internal sealed class DoorMotion
{
 internal CustomAnimationState? State;internal CustomAnimation? Queue;
 internal TransformValuesCurve Curve=null!;internal Transform Pivot=null!;
 internal int Axis;internal bool Sliding;internal float Closed,Low,High,PositiveDelta;
 private bool resumePending;private float resumeValue;
 internal bool Playing=>State?.animationPlaying??Queue?.animationPlaying??false;
 internal float Offset
 {
  get{var p=Sliding?Pivot.localPosition:Pivot.localEulerAngles;float x=Axis==0?p.x:Axis==1?p.y:p.z;return Sliding?x-Closed:Mathf.DeltaAngle(Closed,x);}
 }
 internal float Value=>State?.currentStateValue??Offset/PositiveDelta;
 internal static IEnumerable<DoorMotion> Read(CustomAnimationTool tool)
 {
  if(tool.IsState())
  {
   var s=tool.customAnimationState;var c=s?.positiveState;var p=c?.trans??s?.objTransform;
   if(s==null||c==null||p==null)yield break;
   var m=Create(c,p);if(m==null)yield break;m.State=s;
   var n=s.negativeState;
   if(n!=null&&n.effect==c.effect){float d=m.Sliding?n.maxValue-m.Closed:Mathf.DeltaAngle(m.Closed,n.maxValue);m.Low=Math.Min(m.Low,d);m.High=Math.Max(m.High,d);}
   yield return m;yield break;
  }
  // Only reversible, interaction-driven leaf animations. Loops, cinematics
  // and multi-stage machinery retain their native scripted motion.
  if((int)tool.animationTrigger!=2||tool.customAnimationQueue==null)yield break;
  foreach(var q in tool.customAnimationQueue)
  {
   if(q==null||(int)q.queueSettings!=3||(int)q.animationQueueOrder>3||q.animationQueue==null||q.animationQueue.Count!=1)continue;
   var e=q.animationQueue[0];if(e?.valueCurveList==null)continue;
   TransformValuesCurve? curve=null;bool complex=false;
   foreach(var c in e.valueCurveList)
   {
    if(c==null||Math.Abs(c.maxValue-c.minValue)<.0001f)continue;
    if(curve!=null){complex=true;break;}curve=c;
   }
   // Native AnimateElements passes q.objTransform by reference.
   var p=q.objTransform??e.transform??curve?.trans;
   if(complex||curve==null||p==null)continue;
   var m=Create(curve,p);if(m==null)continue;m.Queue=q;
   if((int)curve.restingPosition==1)
   {m.Closed=curve.maxValue;m.PositiveDelta=-m.PositiveDelta;m.Low=Math.Min(0,m.PositiveDelta);m.High=Math.Max(0,m.PositiveDelta);}
   yield return m;
  }
 }
 private static DoorMotion? Create(TransformValuesCurve c,Transform p)
 {
  int effect=(int)c.effect;if(effect<0||effect>5)return null;
  bool slide=effect<3;float d=slide?c.maxValue-c.minValue:Mathf.DeltaAngle(c.minValue,c.maxValue);
  if(!float.IsFinite(d)||Math.Abs(d)<(slide?.015f:15)||Math.Abs(d)>(slide?2:180))return null;
  return new DoorMotion{Curve=c,Pivot=p,Axis=effect%3,Sliding=slide,Closed=c.minValue,Low=Math.Min(0,d),High=Math.Max(0,d),PositiveDelta=d};
 }
 internal void Set(float offset,CustomAnimationTool tool)
 {
  var p=Sliding?Pivot.localPosition:Pivot.localEulerAngles;
  float absolute=Closed+offset;if(Axis==0)p.x=absolute;else if(Axis==1)p.y=absolute;else p.z=absolute;
  if(Sliding)Pivot.localPosition=p;else Pivot.localEulerAngles=p;
  float extent=offset>=0?Math.Max(.0001f,High):Math.Max(.0001f,-Low);
  float value=Math.Abs(offset)/extent*(offset*PositiveDelta>=0?1:-1);
  Curve.currentValue=absolute;
  if(State!=null)
  {
   State.currentStateValue=value;State.previewValue=value;State.previousPreviewValue=value;State.currentValueOnStart=value;
   State.animationPlaying=false;State.stateTarget=Math.Abs(value)<.03f?0:value>0?1:-1;
   tool.customAnimationToolState=Math.Abs(value)<.03f;
   if(State.occlusionPortal!=null)State.occlusionPortal.open=Math.Abs(value)>.03f;
  }
  if(Queue!=null)
  {
   var q=Queue;var e=q.animationQueue[0];
   q.animationPlaying=false;q.animationWasCompleted=true;q.currentIndex=0;
   // Native Reversible toggles flipSequence at an endpoint. Keep the next
   // button/strike going toward the opposite endpoint after physical motion.
   q.flipSequence=value>.5f;resumePending=true;resumeValue=Math.Clamp(value,0,1);
   e.animationElapsedTime=0;e.evaluationTime=0;e.pauseCompensation=0;e.currentDelay=0;
  }
 }
 internal void ResumeNative()
 {
  var q=Queue;if(!resumePending||q==null||!q.animationPlaying)return;resumePending=false;
  var e=q.animationQueue[0];float fraction=q.flipSequence?1-resumeValue:resumeValue;
  // Resume a native button/strike from the physically reached angle. Binary
  // inversion handles the authored ease curve without snapping to min/max.
  float lo=0,hi=1;
  for(int i=0;i<16;i++){float mid=(lo+hi)*.5f;if((Curve.curve?.Evaluate(mid)??mid)<fraction)lo=mid;else hi=mid;}
  float t=(lo+hi)*.5f;q.cycleCount=Math.Max(1,q.cycleCount);q.lowestElementOffset=0;
  e.evaluationTime=t;e.animationElapsedTime=t*e.duration;e.pauseCompensation=0;e.currentDelay=e.delay;
  e.animationStartTimestamp=PlayMagic.SafeTime.adjustedTimeSinceLevelLoad-e.animationElapsedTime;
 }
 internal void Finish(CustomAnimationTool tool)
 {
  if(State!=null)tool.TriggerStateAnimationFinished(State);
  else if(Queue!=null)tool.CheckAnimationQueueComplete(Queue);
 }
}
