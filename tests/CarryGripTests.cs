using System;using XiiiXR;
class CarryGripTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
 var s=new CarryGripState();
 s.Sample(true,true,false,0,false,true);s.Sample(false,false,true,.1f,true,true);
 Check(!s.ShouldRelease(true,false),"tap dropped item");
 s.Sample(true,true,false,1,true,true);s.Sample(false,true,false,1.4f,true,true);s.Sample(false,false,true,1.5f,true,true);
 Check(s.ShouldRelease(true,false),"hold release failed");Check(!s.ShouldRelease(true,true),"drop during native transition");
 s.Released();Check(!s.CanAdopt&&!s.ShouldRelease(true,false),"release not reset");
 s.Sample(true,true,false,2,false,true);s.Sample(false,false,true,2.5f,false,true);
 Check(s.CanAdopt&&s.ShouldRelease(true,false),"late acquisition loses release");
 s.Sample(false,false,false,8,false,true);Check(!s.CanAdopt,"stale pickup captures unrelated item later");
 s.Sample(true,true,false,10,true,true);s.Sample(false,false,false,10.1f,true,false);
 Check(s.ShouldRelease(true,false),"focus loss left held prop");s.Reset();
 Check(!s.ShouldRelease(true,false),"scene reset retained release");
 for(int n=0;n<5;n++){s.Sample(true,true,false,20+n,false,true);s.Sample(false,false,true,20.5f+n,true,true);Check(s.ShouldRelease(true,false),"repeat pickup/drop failed");s.Released();}
 // 0.1.151: "hold grip" (0): held while the grip is held, dropped when it is let go - however short the press.
 {
  var h=new CarryGripState();
  h.Sample(true,true,false,0,false,true,0);Check(h.CanAdopt,"hold: the press takes the thing");
  h.Sample(false,true,false,.2f,true,true,0);Check(!h.ShouldRelease(true,false),"hold: dropped while the grip holds it");
  h.Sample(false,true,false,5f,true,true,0);Check(!h.ShouldRelease(true,false),"hold: dropped after a long hold");
  h.Sample(false,false,true,5.1f,true,true,0);Check(h.ShouldRelease(true,false),"hold: kept after the grip let go");Check(!h.ShouldRelease(true,true),"hold: dropped during the game's switch");
  h.Released();
  h.Sample(true,true,false,6,false,true,0);h.Sample(false,false,true,6.1f,false,true,0);Check(h.CanAdopt,"hold: a quick press still takes it");
  h.Sample(false,false,false,6.4f,true,true,0);Check(h.ShouldRelease(true,false),"hold: a thing that came after the grip let go is kept");
  h.Released();h.Sample(true,true,false,7,false,true,0);h.Sample(false,true,false,7.3f,true,true,0);h.Sample(false,true,false,7.4f,true,false,0);
  Check(h.ShouldRelease(true,false),"hold: focus loss keeps it");
 }
 // 0.1.222: the thing gone from the hand for a moment (the game's switch to it) is taken up again by the same press.
 {
  var l=new CarryGripState();
  l.Sample(true,true,false,0,false,true,0);l.Sample(false,true,false,.1f,true,true,0);
  l.Lost();Check(l.CanAdopt&&!l.ShouldRelease(false,false),"lost: the press that took it no longer counts");
  l.Sample(false,false,true,.2f,false,true,0);l.Sample(false,false,false,.4f,true,true,0);Check(l.ShouldRelease(true,false),"lost: back in the hand with the grip open, not let go");
  l.Released();l.Sample(true,true,false,1,false,true,0);l.Lost();l.Sample(false,true,false,7,false,true,0);l.Sample(false,false,true,7.1f,false,true,0);l.Sample(false,false,false,7.2f,false,true,0);
  Check(!l.CanAdopt,"lost: a press long ago still takes things up");
 }
 // Toggle (1): the press that took it keeps it; the next press holds it until let go.
 {
  var t=new CarryGripState();
  t.Sample(true,true,false,0,false,true,1);t.Sample(false,false,true,.1f,true,true,1);Check(!t.ShouldRelease(true,false),"toggle: the taking press dropped it");
  t.Sample(false,false,false,3,true,true,1);Check(!t.ShouldRelease(true,false),"toggle: dropped without a press");
  t.Sample(true,true,false,4,true,true,1);Check(!t.ShouldRelease(true,false),"toggle: dropped on the press itself");
  t.Sample(false,false,true,4.1f,true,true,1);Check(t.ShouldRelease(true,false),"toggle: the second press did not let go");
  t.Released();t.Sample(true,true,false,5,false,true,1);t.Sample(false,true,false,6,true,true,1);t.Sample(false,false,true,6.5f,true,true,1);
  Check(!t.ShouldRelease(true,false),"toggle: a long taking press dropped it");
 }
 // 0.1.156: a thing in both hands: held while either grip holds; let go when both are open.
 {
  ulong G=HandControls.Grip;
  var one=CarryGripState.Either(new HandControls(true,0,0,G),new HandControls(true,G,0,0));
  if((one.Held&G)==0||(one.Up&G)!=0)throw new Exception("the other hand's grip does not keep a two-handed thing");
  var both=CarryGripState.Either(new HandControls(true,0,0,0),new HandControls(true,0,0,G));
  if((both.Held&G)!=0||(both.Up&G)==0)throw new Exception("both grips open: not let go");
  var own=CarryGripState.Either(new HandControls(true,G,G,0),new HandControls(false,0,0,0));
  if((own.Down&G)==0||(own.Held&G)==0||!own.Valid)throw new Exception("the holding hand's own grip lost");
  var c=new CarryGripState();c.Sample(true,true,false,0,false,true,0);
  var h=CarryGripState.Either(new HandControls(true,0,0,G),new HandControls(true,G,0,0));
  c.Sample((h.Down&G)!=0,(h.Held&G)!=0,(h.Up&G)!=0,1,true,true,0);if(c.ShouldRelease(true,false))throw new Exception("a shovel held by the other hand dropped");
  h=CarryGripState.Either(new HandControls(true,0,0,0),new HandControls(true,0,0,G));
  c.Sample((h.Down&G)!=0,(h.Held&G)!=0,(h.Up&G)!=0,2,true,true,0);if(!c.ShouldRelease(true,false))throw new Exception("both grips open: the shovel kept");
 }
 Console.WriteLine("PASS: 'hold grip': a thing is held while the grip is held and dropped when it is let go (also when it came after a quick press); 'grip press': the next press lets go; 0.1.156: a thing in both hands is held while either grip holds it.");
 Console.WriteLine("PASS: tap latch, hold release, native transition deferral, delayed acquisition, timeout, focus/scene cleanup and repeated pickup/drop state.");
 }
}
