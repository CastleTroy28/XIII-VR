using System;using System.Linq;using System.Numerics;using XiiiXR;
class FreezeDetectorTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static float t;static int frame;static int gc,modGc;static float head;
 static FrameSample F(float dt,float realGap=-1,float stick=0,bool allowed=true,Vector2 root=default,bool blocked=false,bool focused=true,string? taker=null,float timeScale=1,bool menu=false,float modMs=1,int scenes=3,bool sameHead=false,bool headValid=true,int skip=0,bool gameplay=true)
 {
  t+=realGap<0?dt:realGap;frame+=1+skip;if(!sameHead)head+=.001f;
  return new FrameSample(t,dt,new Vector3(head,1.6f,0),Quaternion.Identity,headValid,stick,allowed,root,blocked,timeScale,menu,gc,modGc,modMs,scenes,focused,taker,frame,gameplay);
 }
 static void Main()
 {
  // A long frame: its cause, reported once the frames after it are seen.
  {
   var d=new FreezeDetector();t=0;frame=0;gc=5;modGc=1;
   for(int i=0;i<20;i++)Check(d.Step(F(.011f)).Count==0,"steady frames: nothing");
   gc=6;var e=d.Step(F(1.2f));Check(e.Count==0,"a long frame waits for the frames after it");
   var all=new System.Collections.Generic.List<FreezeEvent>();
   for(int i=0;i<FreezeDetector.AfterFrames;i++)all.AddRange(d.Step(F(.011f)));
   Check(all.Count==1&&all[0].Kind=="FREEZE"&&Math.Abs(all[0].Seconds-1.2f)<1e-3f&&all[0].Detail.Contains("garbage collection")&&all[0].Detail.Contains("frames after it"),"freeze with the game's clean-up: "+string.Join("|",all));
   // The mod's code most of a long frame.
   e=d.Step(F(.4f,modMs:300));for(int i=0;i<FreezeDetector.AfterFrames;i++)e.AddRange(d.Step(F(.011f)));
   Check(e.Count==1&&e[0].Detail.Contains("mod's own code took 300 ms")&&!e[0].Detail.Contains("garbage"),"the mod's code: "+string.Join("|",e));
   // The loop stood still (Windows paused it): real time long, the game counted little.
   e=d.Step(F(.011f,realGap:1.3f));for(int i=0;i<FreezeDetector.AfterFrames;i++)e.AddRange(d.Step(F(.011f)));
   Check(e.Count==1&&e[0].Detail.Contains("Windows paused it")&&Math.Abs(e[0].Seconds-1.3f)<1e-3f,"a paused loop: "+string.Join("|",e));
   // Nothing known: the game or the driver; a cascade of slow frames after it.
   e=d.Step(F(.5f));e.AddRange(d.Step(F(.2f)));e.AddRange(d.Step(F(.1f)));for(int i=0;i<FreezeDetector.AfterFrames;i++)e.AddRange(d.Step(F(.011f)));
   Check(e.Count==1&&e[0].Detail.Contains("the game itself")&&e[0].Detail.Contains("cascade"),"unknown with a cascade: "+string.Join("|",e));
   // Loading screens and cutscenes: long frames are normal there.
   d.Step(F(.011f,gameplay:false));e=d.Step(F(.8f,gameplay:false));for(int i=0;i<FreezeDetector.AfterFrames;i++)e.AddRange(d.Step(F(.011f,gameplay:false)));
   Check(e.Count==0,"a loading screen's long frame: "+string.Join("|",e));
   d.Step(F(.011f));
   // Frames the watch did not see: no judgement across them.
   Check(d.Step(F(.011f,realGap:3,skip:40)).Count==0&&d.Step(F(.011f)).Count==0,"skipped frames are not a freeze");
  }
  // Many slow frames together: a stutter (once, then quiet for a while).
  {
   var d=new FreezeDetector();t=0;frame=0;
   for(int i=0;i<10;i++)d.Step(F(.011f));
   var e=new System.Collections.Generic.List<FreezeEvent>();
   for(int i=0;i<12;i++)e.AddRange(d.Step(F(.09f)));
   Check(e.Count==1&&e[0].Kind=="STUTTER"&&e[0].Detail.Contains("slow frames"),"stutter: "+string.Join("|",e));
   for(int i=0;i<10;i++)e.AddRange(d.Step(F(.09f)));
   Check(e.Count==1,"stutter reported again at once");
  }
  // The head pose stood still (the runtime), lost; resting headset told apart.
  {
   var d=new FreezeDetector();t=0;frame=0;
   d.Step(F(.011f));
   for(int i=0;i<80;i++)Check(d.Step(F(.011f,sameHead:true)).Count==0,"while frozen nothing yet");
   var e=d.Step(F(.011f));Check(e.Count==1&&e[0].Kind=="TRACKING FROZE"&&e[0].Seconds>.8f,"tracking froze: "+string.Join("|",e));
   for(int i=0;i<1000;i++)d.Step(F(.011f,sameHead:true));
   e=d.Step(F(.011f));Check(e.Count==1&&e[0].Kind=="HEADSET STILL","a headset put down: "+string.Join("|",e));
   for(int i=0;i<30;i++)d.Step(F(.011f,headValid:false));
   e=d.Step(F(.011f));Check(e.Count==1&&e[0].Kind=="TRACKING LOST","tracking lost: "+string.Join("|",e));
  }
  // Pushing the stick without moving: stalled, or against a wall; walking is fine.
  {
   var d=new FreezeDetector();t=0;frame=0;var p=Vector2.Zero;
   for(int i=0;i<90;i++){p+=new Vector2(0,.02f);Check(d.Step(F(.011f,stick:.9f,root:p)).Count==0,"walking: nothing");}
   for(int i=0;i<90;i++)d.Step(F(.011f,stick:.9f,root:p));
   p+=new Vector2(0,.02f);var e=d.Step(F(.011f,stick:.9f,root:p));
   Check(e.Count==1&&e[0].Kind=="MOVE STALL"&&e[0].Seconds>.9f&&e[0].Detail.Contains("nothing in its way"),"a stall: "+string.Join("|",e));
   for(int i=0;i<90;i++)d.Step(F(.011f,stick:.9f,root:p,blocked:true));
   e=d.Step(F(.011f,stick:0,root:p));Check(e.Count==1&&e[0].Detail.Contains("against something"),"against a wall: "+string.Join("|",e));
   for(int i=0;i<90;i++)d.Step(F(.011f,stick:.9f,allowed:false,root:p));
   Check(d.Step(F(.011f,stick:0,root:p)).Count==0,"moving not allowed (a lock, a menu): no stall");
  }
  // The window lost focus to another program; the game's clock stopped.
  {
   var d=new FreezeDetector();t=0;frame=0;
   d.Step(F(.011f));
   for(int i=0;i<40;i++)d.Step(F(.011f,focused:false,taker:"PimaxClient ('Pimax Play')"));
   var e=d.Step(F(.011f));Check(e.Count==1&&e[0].Kind=="FOCUS LOST"&&e[0].Detail.Contains("PimaxClient")&&e[0].Seconds>.4f,"focus lost: "+string.Join("|",e));
   for(int i=0;i<40;i++)d.Step(F(.011f,timeScale:0));
   e=d.Step(F(.011f));Check(e.Count==1&&e[0].Kind=="TIME STOPPED","time stopped: "+string.Join("|",e));
   for(int i=0;i<40;i++)d.Step(F(.011f,timeScale:0,menu:true));
   Check(d.Step(F(.011f)).Count==0,"a menu's pause: nothing");
  }
  Console.WriteLine("PASS: freezes told apart: a long frame (real time) with its cause - the game's memory clean-up, the mod's code, a paused loop (Windows focus), else the game or the driver - and a cascade after it; many slow frames together (a stutter, once); a frozen, lost or resting head pose; a stick pushed without moving (against a wall told apart, locks not counted); the window's focus lost to a named program; the game's clock stopped outside the menus.");
 }
}
