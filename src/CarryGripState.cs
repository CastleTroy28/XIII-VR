namespace XiiiXR;
// The game's things taken into the hand (bottles, chairs, the ashtray, a
// broom, a shovel) and the grip of the hand holding them. Acquisition can
// finish after the grip is let go (the game's weapon-switch animation).
// 0.1.151, by the
// "Weapon in hand" setting:
//  Hold   (0) - held while the grip is held; let go: it is dropped (once it
//               is in the hand, even if the grip was let go before that);
//  Toggle (1) - the press that took it keeps it; the next press holds it
//               until that grip is let go;
//  Always (2) - the old way: a tap keeps it, a deliberate hold lets go on release.
internal sealed class CarryGripState
{
 private bool pressed,releasePending,eligible,second;private float began;
 internal void Reset(){pressed=releasePending=eligible=second=false;}
 internal void Sample(bool down,bool held,bool up,float now,bool owns,bool allowed,int mode=2)
 {
  if(!allowed){if(owns)releasePending=true;pressed=false;return;}
  if(mode==0)
  {
   if(down){pressed=true;began=now;eligible=true;}
   if(!held)pressed=false;
   releasePending=owns&&!held;
   if(eligible&&!owns&&!pressed&&now-began>5)eligible=false;
   return;
  }
  if(mode==1)
  {
   if(down){if(owns)second=true;else eligible=true;pressed=true;began=now;releasePending=false;}
   if(pressed&&(up||!held)){pressed=false;if(second&&owns){releasePending=true;second=false;}}
   if(eligible&&!owns&&!pressed&&now-began>5){eligible=false;second=false;}
   return;
  }
  if(down){pressed=true;began=now;eligible=true;releasePending=false;}
  if(pressed&&(up||!held)){if(now-began>=.30f)releasePending=true;pressed=false;}
  if(eligible&&!pressed&&!owns&&now-began>5){eligible=releasePending=false;}
 }
 internal bool CanAdopt=>eligible;
 // 0.1.156: two hands on it: the grip is held while either one holds.
 internal static HandControls Either(HandControls a,HandControls b)
 {
  ulong grip=HandControls.Grip;
  ulong held=a.Held|(b.Valid?b.Held&grip:0);
  ulong up=(a.Up&~grip)|((held&grip)==0&&((a.Up|(b.Valid?b.Up:0))&grip)!=0?grip:0);
  return new HandControls(a.Valid||b.Valid,held,a.Down,up);
 }
 internal bool ShouldRelease(bool owns,bool transitioning)=>owns&&releasePending&&!transitioning;
 internal void Released(){Reset();}
}
