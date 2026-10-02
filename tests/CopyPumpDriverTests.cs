using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;
// 0.1.185: the production one-hand pump of a shotgun held as a copy (WeaponHands.CopyPump).
class CopyPumpDriverTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static float Jerk(float t,float start,float accel,float hold)
 {
  float u=t-start;if(u<=0)return 0;
  float a=accel,h=hold;
  if(u<h)return -.5f*a*u*u;
  float v=a*h,x=.5f*a*h*h;u-=h;
  if(u<h)return -(x+v*u-.5f*a*u*u);
  x=2*x;u-=h;
  if(u<h)return -x+.5f*a*u*u;
  x=x-.5f*a*h*h;u-=h;
  if(u<h)return -(x-v*u+.5f*a*u*u);
  return 0;
 }
 static void Main()
 {
  var w=new WeaponHands();var up=new Quaternion(System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,-MathF.PI/2));
  var copy=new HolsterCopy{Rotation=up};
  // A shotgun copy in the left hand fired: it waits to be pumped (as the game's shotgun), a trigger buzzes.
  w.Hold(0,"shotgun",byPump:false);w.Shot(0);
  Check(w.Unpumped(0,true)&&w.Buzzes==1,"a shotgun copy fires again without being pumped");
  // Held by its handle: a jerk does nothing.
  float t=0;for(int i=0;i<135;i++){t+=1f/90;w.Hand=new Vector3(0,1+Jerk(t,.2f,30,.09f),0);w.Tick(0,copy,t);}
  Check(w.State(0).NeedsRack&&w.Audio.Count==0,"a shotgun held by its handle pumped by a jerk");
  // Hanging by its pump: held still, nothing; a jerk down and back up pumps it.
  w.ByPump(0,true);t=0;
  for(int i=0;i<90;i++){t+=1f/90;w.Hand=new Vector3(0,1,0);w.Tick(0,copy,t);}
  Check(w.State(0).NeedsRack&&w.Audio.Count==0,"a shotgun copy held still by its pump pumped itself");
  bool back=false,shifted=false;
  for(int i=0;i<135;i++){t+=1f/90;w.Hand=new Vector3(0,1+Jerk(t,1.2f,30,.09f),0);w.Tick(0,copy,t);back|=copy.Back;shifted|=w.Shift(0,up).y>.01f;}
  Check(!w.State(0).NeedsRack&&w.Audio.Contains(ReloadAction.RackBack)&&w.Audio.Contains(ReloadAction.Chamber),"a jerk down and up does not pump the shotgun copy");
  Check(back&&!copy.Back&&shifted&&w.Shift(0,up).sqrMagnitude==0,"the copy's pump not drawn back / the gun not sliding on the pump / left back");
  Check(w.Haptics.Contains(ReloadAction.RackBack)&&w.Haptics.Contains(ReloadAction.Chamber)&&w.HapticsRight.TrueForAll(r=>!r),"the pump not felt in the left hand");
  Check(!w.Unpumped(0,true),"a pumped shotgun copy still does not fire");
  // The right hand the same way.
  w.Hold(1,"shotgun",byPump:true);w.Shot(1);t=0;w.Audio.Clear();
  for(int i=0;i<135;i++){t+=1f/90;w.Hand=new Vector3(0,1+Jerk(t,.2f,30,.09f),0);w.Tick(1,copy,t);}
  Check(!w.State(1).NeedsRack&&w.Audio.Contains(ReloadAction.Chamber),"the right hand's shotgun copy not pumped");
  // Other kinds and the automatic reload are left alone.
  w.Hold(0,"ak47",byPump:true);w.Shot(0);Check(!w.Unpumped(0,true)&&!w.State(0).NeedsRack,"an AK copy waits for a pump");
  // 0.1.194: the double-barrelled shotgun as a copy is never pumped.
  w.Hold(0,"shotgun",byPump:true,breaks:true);w.Shot(0);Check(!w.Unpumped(0,true)&&!w.State(0).NeedsRack,"a double-barrelled copy waits for a pump");
  WeaponOptions.ManualReload.Value=false;w.Hold(0,"shotgun",byPump:false);w.Shot(0);Check(!w.Unpumped(0,true),"with the automatic reload a shotgun copy waits for a pump");
  Console.WriteLine("PASS: 0.1.194 a double-barrelled copy is not pumped; 0.1.185 a shotgun held as a copy: fired, it waits for a pump; hanging by its pump in either hand a jerk down and back up pumps it (its pump drawn back, the gun sliding on it, sounds and vibration in that hand), held still or by the handle it does not; other kinds and the automatic reload untouched.");
 }
}
namespace XiiiXR
{
 internal sealed partial class WeaponHands
 {
  internal static WeaponHands? Current;internal WeaponHands(){Current=this;}
  internal Vector3 Hand;internal int Buzzes;internal readonly List<ReloadAction> Audio=new(),Haptics=new();internal readonly List<bool> HapticsRight=new();
  private readonly int[] copyKey={-1,-1};private readonly string[] copyProfile={"",""};private readonly bool[] copyForeEnd=new bool[2];private int next=10;
  private readonly Dictionary<int,ManualReloadState> states=new();
  internal void Hold(int s,string p,bool byPump,bool breaks=false){copyKey[s]=next++;copyProfile[s]=p;copyForeEnd[s]=byPump;copyBreaks[s]=breaks;}
  // 0.1.194: a double-barrelled shotgun held as a copy (not pumped).
  private readonly bool[] copyBreaks=new bool[2];private bool CopyBreaks(int s)=>copyBreaks[s];private bool CopyClub(int s)=>false;
  internal void ByPump(int s,bool b)=>copyForeEnd[s]=b;
  internal ManualReloadState State(int s)=>CopyState(s,true)!;
  internal void Shot(int s)=>CopyShotPumped(s);
  internal bool Unpumped(int s,bool down)=>CopyUnpumped(s,down);
  internal void Tick(int s,HolsterCopy c,float now)=>TickCopyPump(s,c,now);
  internal Vector3 Shift(int s,Quaternion aim)=>CopyPumpShift(s,aim);
  private ManualReloadState? CopyState(int s,bool create){if(!states.TryGetValue(copyKey[s],out var st)){if(!create)return null;st=new ManualReloadState(copyProfile[s]=="shotgun");states[copyKey[s]]=st;}return st;}
  private readonly CameraRig rig=new();private ReloadAudio? reloadAudio=new();
  private static string Side(int s)=>s==0?"left":"right";
  private static System.Numerics.Vector3 ToN(Vector3 v)=>v.N;
 }
 internal readonly record struct PoseValue(Vector3 Position);
 internal class CameraRig
 {
  internal Vector3 HeadPosition=>default;internal Quaternion HeadRotation=>Quaternion.identity;
  internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool valid){var h=WeaponHands.Current!.Hand;l=new(h);r=new(h);valid=true;return true;}
  internal static Vector3 UnityPosition(PoseValue p)=>p.Position;
  internal void ResistanceHaptics(float a,bool right=false){WeaponHands.Current!.Buzzes++;}
  internal void ReloadHaptics(ReloadAction a,bool right=false){WeaponHands.Current!.Haptics.Add(a);WeaponHands.Current!.HapticsRight.Add(right);}
 }
 internal class ReloadAudio{internal void Play(ReloadAction a,string p,Vector3 at,Vector3 h,Quaternion q){WeaponHands.Current!.Audio.Add(a);}}
 internal class HolsterCopy{internal Quaternion Rotation;internal Vector3 Position=>default;internal bool HasCycle=>true;internal bool Back;internal void LockBack(bool b)=>Back=b;}
 internal class Config{internal bool Value=true;}internal static class WeaponOptions{internal static Config ManualReload=new();}
 internal static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}
}
namespace UnityEngine
{
 internal readonly struct Vector3
 {
  internal readonly System.Numerics.Vector3 N;internal Vector3(float x,float y,float z){N=new(x,y,z);}internal Vector3(System.Numerics.Vector3 n){N=n;}
  internal float y=>N.Y;internal float sqrMagnitude=>N.LengthSquared();
  internal static Vector3 zero=>default;internal static Vector3 forward=>new(0,0,1);
  public static Vector3 operator*(Vector3 a,float b)=>new(a.N*b);
 }
 internal readonly struct Quaternion
 {
  internal readonly System.Numerics.Quaternion Q;internal Quaternion(System.Numerics.Quaternion q){Q=q;}
  internal static Quaternion identity=>new(System.Numerics.Quaternion.Identity);
  public static Vector3 operator*(Quaternion q,Vector3 v)=>new(System.Numerics.Vector3.Transform(v.N,q.Q));
 }
}
