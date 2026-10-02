using System;
using System.Numerics;
namespace XiiiXR;
// Do not freeze a draw/prop-use transition as the permanent equipment grip.
internal sealed class GripPoseStability
{
 private string key="";
 private Matrix4x4[]? candidate;
 private Matrix4x4 anchor;
 private float since;
 private int frame=-1,samples;
 internal void Reset(){candidate=null;key="";frame=-1;samples=0;}
 internal bool Observe(string identity,Matrix4x4 fitted,Matrix4x4[] pose,bool[] hand,float now,int renderedFrame,bool settled)
 {
  if(!settled){Reset();return false;}
  if(frame==renderedFrame)return false;
  frame=renderedFrame;
  bool changed=key!=identity||candidate==null||candidate.Length!=pose.Length||!Close(anchor,fitted,.006f);
  if(!changed)for(int i=0;i<pose.Length;i++)if(hand[i]&&!Close(candidate![i],pose[i],.003f)){changed=true;break;}
  if(changed){key=identity;candidate=(Matrix4x4[])pose.Clone();anchor=fitted;since=now;samples=1;return false;}
  samples++;
  return samples>=3&&now-since>=.15f;
 }
 private static bool Close(Matrix4x4 a,Matrix4x4 b,float distance)
 {
  if(Vector3.DistanceSquared(a.Translation,b.Translation)>distance*distance)return false;
  foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})
  {
   var x=Vector3.TransformNormal(axis,a);var y=Vector3.TransformNormal(axis,b);
   if(!float.IsFinite(y.LengthSquared())||Math.Abs(x.Length()-y.Length())>.02f*Math.Max(.001f,x.Length()))return false;
   if(Vector3.Dot(Vector3.Normalize(x),Vector3.Normalize(y))<.997f)return false;
  }
  return true;
 }
}
