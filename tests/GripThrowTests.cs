using System;using System.Numerics;using XiiiXR;
class GripThrowTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var forward=Vector3.UnitZ;var look=Vector3.UnitZ;
  // A swing forward, the grip opening at its fastest: thrown along it (a little toward the controller).
  {
   var g=new SwingRelease();float t=0;var p=Vector3.Zero;
   for(int i=0;i<30;i++){t+=.011f;g.Sample(t,p);}                       // held still
   for(int i=0;i<12;i++){t+=.011f;p+=new Vector3(0,.004f,.045f);g.Sample(t,p);}   // ~4 m/s forward, a little up
   var r=g.Release(t,forward,look);
   Check(r.Throw&&r.Speed>3.5f&&r.Direction.Z>.9f&&r.Direction.Y>0,"a forward swing throws: "+r.Speed+" "+r.Direction+" "+r.Reason);
   Check(g.Velocity(t).Z>3.5f,"hand velocity");
  }
  // Letting go slowly: no throw ("slow"); drawing back: none ("not forward").
  {
   var g=new SwingRelease();float t=0;var p=Vector3.Zero;
   for(int i=0;i<30;i++){t+=.011f;p+=new Vector3(0,0,.002f);g.Sample(t,p);}
   var r=g.Release(t,forward,look);Check(!r.Throw&&r.Reason=="slow","slow let go: "+r.Reason);
   var b=new SwingRelease();t=0;p=Vector3.Zero;
   for(int i=0;i<12;i++){t+=.011f;p-=new Vector3(0,0,.045f);b.Sample(t,p);}
   r=b.Release(t,forward,look);Check(!r.Throw&&r.Reason=="not forward","drawing back: "+r.Reason);
  }
  // A throw forward of where one looks counts even with the controller turned aside (an underhand toss).
  {
   var g=new SwingRelease();float t=0;var p=Vector3.Zero;
   for(int i=0;i<12;i++){t+=.011f;p+=new Vector3(0,.01f,.04f);g.Sample(t,p);}
   var r=g.Release(t,-Vector3.UnitY,look);Check(r.Throw,"forward of the look: "+r.Reason);
   Check(!new SwingRelease().Release(t,Vector3.Zero,look).Throw,"no aim");
  }
  // Holding by the grip.
  {
   Func<bool> swing=()=>true,none=()=>false;
   var h=new ThrowGrip();h.Took(true,WeaponGripMode.Hold);
   Check(h.Step(WeaponGripMode.Hold,true,false,none)==ThrowGripStep.None,"held");
   Check(h.Step(WeaponGripMode.Hold,false,false,swing)==ThrowGripStep.Throw,"hold: let go in a swing throws");
   Check(h.Step(WeaponGripMode.Hold,false,false,swing)==ThrowGripStep.None,"once");
   h.Step(WeaponGripMode.Hold,true,true,none);
   Check(h.Step(WeaponGripMode.Hold,false,false,none)==ThrowGripStep.Release,"hold: let go without a swing lets it go");
   h.Took(false,WeaponGripMode.Hold);
   Check(h.Step(WeaponGripMode.Hold,false,false,swing)==ThrowGripStep.None,"hold: came without the grip, not held yet");
   // Toggle: the press that took it keeps it; the next press holds it until the grip opens.
   var t=new ThrowGrip();t.Took(true,WeaponGripMode.Toggle);
   Check(t.Step(WeaponGripMode.Toggle,true,false,swing)==ThrowGripStep.None&&t.Step(WeaponGripMode.Toggle,false,false,swing)==ThrowGripStep.None,"toggle: the take's own release keeps it");
   Check(t.Step(WeaponGripMode.Toggle,true,true,none)==ThrowGripStep.None&&t.Step(WeaponGripMode.Toggle,false,false,none)==ThrowGripStep.Release,"toggle: press and let go without a swing lets it go");
   t.Took(true,WeaponGripMode.Toggle);t.Step(WeaponGripMode.Toggle,false,false,swing);t.Step(WeaponGripMode.Toggle,true,true,none);
   Check(t.Step(WeaponGripMode.Toggle,false,false,swing)==ThrowGripStep.Throw,"toggle: press, swing, let go throws");
   // Always in the hand: only a swing throws.
   var a=new ThrowGrip();a.Took(true,WeaponGripMode.Always);
   Check(a.Step(WeaponGripMode.Always,false,false,swing)==ThrowGripStep.None,"always: nothing without a press");
   a.Step(WeaponGripMode.Always,true,true,none);Check(a.Step(WeaponGripMode.Always,false,false,none)==ThrowGripStep.None,"always: let go without a swing keeps it");
   a.Step(WeaponGripMode.Always,true,true,none);Check(a.Step(WeaponGripMode.Always,false,false,swing)==ThrowGripStep.Throw,"always: press, swing, let go throws");
  }
  Console.WriteLine("PASS: throwables held by the grip: letting go at the fastest of a forward swing throws along it (forward of the controller or the look), letting go slowly or drawing back does not; hold / toggle / always-in-hand grip modes (the take's own release keeps it in toggle, a slow let-go keeps it in always).");
 }
}
