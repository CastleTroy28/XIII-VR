using System;using System.Numerics;using XiiiXR;
class InertialPumpTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 // Hand path along world y at 90 Hz: returns the ManualReloadState actions seen.
 static (bool rack,bool chamber,float maxTravel) Run(Vector3 axis,Func<float,float> y,float seconds,ManualReloadState state,InertialPump pump)
 {
  bool rack=false,chamber=false;float max=0;var g=new Vector3(0,-9.81f,0);
  for(int i=0;i<=(int)(seconds*90);i++)
  {
   float t=i/90f;float travel=pump.Step(new Vector3(0,y(t),0),axis,t,state.FullTravel,g);max=Math.Max(max,travel);
   var a=state.InertialRack(travel);if(a==ReloadAction.RackBack)rack=true;if(a==ReloadAction.Chamber)chamber=true;
  }
  return (rack,chamber,max);
 }
 static float Jerk(float t,float start,float accel,float hold)
 {
  // Down with accel for hold seconds, then braking at the same rate, then back up the same way.
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
  var up=Vector3.UnitY;
  // Muzzle up, after a shot: holding still (or tilting) never pumps it.
  {
   var s=new ManualReloadState(shotgun:true);s.OnShot();var p=new InertialPump();
   var r=Run(up,t=>0,2,s,p);Check(!r.rack&&!r.chamber&&r.maxTravel==0&&s.NeedsRack,"a still gun pumped itself");
   var d=new ManualReloadState(shotgun:true);d.OnShot();var q=new InertialPump();
   var down=Run(-up,t=>0,2,d,q);Check(!down.rack&&down.maxTravel==0,"muzzle down: the gun's weight alone opened it");
  }
  // A jerk down and back up: open (spent shell out), closed (next round in).
  {
   var s=new ManualReloadState(shotgun:true);s.OnShot();var p=new InertialPump();
   var r=Run(up,t=>Jerk(t,.2f,30,.09f),1.5f,s,p);
   Check(r.rack&&r.chamber&&!s.NeedsRack&&!s.BlocksFire,"a jerk down and up did not pump it: rack="+r.rack+" chamber="+r.chamber+" max="+r.maxTravel);
  }
  // A slow move down and up (no jerk) does nothing.
  {
   var s=new ManualReloadState(shotgun:true);s.OnShot();var p=new InertialPump();
   var r=Run(up,t=>-.15f*MathF.Sin(MathF.PI*Math.Clamp(t-.2f,0,1)),1.5f,s,p);
   Check(!r.rack&&s.NeedsRack,"a slow move pumped it (max "+r.maxTravel+")");
  }
  // 0.1.138: a level gun (let go at the handle from a two-hand hold): the
  // same jerk down and back up pumps it; held still or moved slowly it does not.
  {
   var level=Vector3.Normalize(new Vector3(0,.1f,1));
   var s=new ManualReloadState(shotgun:true);s.OnShot();var p=new InertialPump();
   var r=Run(level,t=>Jerk(t,.2f,30,.09f),1.5f,s,p);
   Check(r.rack&&r.chamber&&!s.NeedsRack,"a level gun: a jerk down and up did not pump it: rack="+r.rack+" chamber="+r.chamber+" max="+r.maxTravel);
   var still=new ManualReloadState(shotgun:true);still.OnShot();var q=new InertialPump();
   var rs=Run(level,t=>0,2,still,q);Check(!rs.rack&&rs.maxTravel==0,"a level gun held still pumped itself");
   var slow=new ManualReloadState(shotgun:true);slow.OnShot();var w=new InertialPump();
   var rw=Run(level,t=>-.15f*MathF.Sin(MathF.PI*Math.Clamp(t-.2f,0,1)),1.5f,slow,w);Check(!rw.rack,"a level gun moved slowly pumped it (max "+rw.maxTravel+")");
   var ax=InertialPump.JerkAxis(Vector3.UnitY,new Vector3(0,-9.81f,0));Check(Vector3.Distance(ax,Vector3.UnitY)<1e-4f,"muzzle up: axis changed");
   var lv=InertialPump.JerkAxis(Vector3.UnitZ,new Vector3(0,-9.81f,0));Check(lv.Y>.6f&&lv.Z>.7f&&Math.Abs(lv.Length()-1)<1e-4f,"level: not tilted up "+lv);
  }
  // A grip still racking it (from the two-hand hold) is let go: the inertia takes over.
  {
   var s=new ManualReloadState(shotgun:true);s.OnShot();
   s.Step(0,false,false,false,false,false,false,Vector3.Zero,Vector3.Zero,Vector3.Zero,Vector3.Zero,Vector3.UnitZ,0,1,true,Vector3.Zero,false,null,0);
   bool was=s.Racking;s.EndRacking();
   Check(!s.Racking&&s.InertialRack(s.FullTravel)==ReloadAction.RackBack&&s.InertialRack(0)==ReloadAction.Chamber,"after the grip racking ended the inertia did not pump it (racking was "+was+")");
  }
  // Not after a shot: no pump at all; InertialRack ignores it.
  {
   var s=new ManualReloadState(shotgun:true);var p=new InertialPump();
   var r=Run(up,t=>Jerk(t,.2f,30,.09f),1.5f,s,p);Check(!r.rack&&!r.chamber&&s.RackTravel==0,"pumped without a shot");
   var pistol=new ManualReloadState();pistol.OnShot();Check(pistol.InertialRack(1)==ReloadAction.None,"a pistol pumped");
  }
  // Open, then only half closed: still blocked; closed later: chambered.
  {
   var s=new ManualReloadState(shotgun:true);s.OnShot();
   Check(s.InertialRack(s.FullTravel)==ReloadAction.RackBack&&s.InertialRack(s.FullTravel*.5f)==ReloadAction.None&&s.BlocksFire&&s.InertialRack(0)==ReloadAction.Chamber&&!s.BlocksFire,"open/half/closed sequence");
  }
  Console.WriteLine("PASS: one-hand shotgun pump: still/tilted/slow hand never pumps; a jerk down and back up opens (shell out) and closes (round in); only after a shot; half-closed stays blocked; a level gun pumps the same way and a grip racking it from the two-hand hold hands over. Hand feel needs the headset.");
 }
}
