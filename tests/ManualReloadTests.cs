using System;using System.Numerics;using XiiiXR;
class ManualReloadTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static readonly Vector3 Port=Vector3.Zero,Bolt=new(0,.03f,.16f),Belt=new(-.3f,-.5f,-.1f);
 static ReloadAction Step(ManualReloadState s,float time=1,bool bd=false,bool bh=false,bool bu=false,bool td=false,bool th=false,bool belt=false,Vector3? hand=null,int rounds=0,int capacity=12,bool gh=false)
  =>s.Step(time,bd,bh,bu,td,th,belt,hand??Port,Port,Port,Bolt,Vector3.UnitY,rounds,capacity,gh);
 static void Insert(ManualReloadState s,int before=0)
 {
  Check(Step(s,th:true,hand:new Vector3(0,-.12f,0),rounds:before)==ReloadAction.None,"insertion began before well");
  Check(Step(s,th:true,hand:new Vector3(.03f,-.12f,0),rounds:before)==ReloadAction.None,"sideways insertion accepted");
  Check(Step(s,th:true,hand:new Vector3(0,-.05f,0),rounds:before)==ReloadAction.Insert,"directed insertion rejected");
  s.Inserted(before);
 }
 static void Main()
 {
  var s=new ManualReloadState();
  var latch=new ManualReloadState();latch.ObserveRounds(0);latch.Detach(0,false);latch.Supply(12);latch.Inserted(0);latch.ObserveRounds(12);
  Check(latch.SlideLocked&&latch.VisualRackTravel==latch.FullTravel&&latch.NeedsRack,"inserting loaded magazine automatically closes empty pistol slide");
  latch.Suspend();Check(latch.SlideLocked,"focus loss releases slide lock");
  var rear=Bolt-Vector3.UnitZ*latch.FullTravel;
  Step(latch,td:true,th:true,hand:rear,rounds:12);Step(latch,hand:rear,rounds:12);
  Check(latch.SlideLocked&&latch.NeedsRack,"touch/release without rear pull closes slide");
  Step(latch,td:true,th:true,hand:rear,rounds:12);
  Check(Step(latch,th:true,hand:rear-Vector3.UnitZ*.010f,rounds:12)==ReloadAction.RackBack,"manual rear pull does not release slide stop");
  Check(Step(latch,hand:rear,rounds:12)==ReloadAction.Chamber&&!latch.SlideLocked&&latch.VisualRackTravel==0,"released manually pulled slide fails to chamber");
  Check(Step(s,bd:true,bh:true)==ReloadAction.None&&s.BlocksFire,"B down auto-reloads/ejects before tap/hold classification");
  Check(Step(s,1.15f,bu:true)==ReloadAction.DropInstalled,"short B did not eject");s.Detach(7,false);
  Check(!s.Installed&&s.NeedsRack&&s.BlocksFire,"removed magazine can fire");
  Check(Step(s,1.2f)==ReloadAction.None,"release repeatedly ejects");
  Check(Step(s,th:true,belt:true,hand:Belt)==ReloadAction.TakeSupply,"held trigger entering pouch ignored");
  for(int i=0;i<30;i++)Check(Step(s,th:true,belt:true,hand:Belt)==ReloadAction.None,"held pouch request repeats without withdrawal/release");
  Step(s);Check(Step(s,td:true,th:true,belt:true,hand:Belt)==ReloadAction.TakeSupply,"left trigger near pouch ignored");s.Supply(12);
  Insert(s);Check(s.Installed&&!s.Holding&&s.NeedsRack&&s.BlocksFire,"fresh magazine fires before racking");
  Step(s,th:true,hand:Bolt);Check(s.NeedsRack,"held insertion trigger racks bolt");
  Step(s,td:true,th:true,hand:Bolt);
  Check(Step(s,th:true,hand:Bolt+new Vector3(.12f,0,-.05f))==ReloadAction.None,"sideways bolt drag chambers round");
  Check(Step(s,th:true,hand:Bolt+new Vector3(0,0,-.05f))==ReloadAction.RackBack&&s.BlocksFire,"backward bolt drag did not latch");
  Check(Step(s)==ReloadAction.Chamber&&!s.BlocksFire&&s.RackTravel==0,"released pistol slide failed to close/chamber");
  for(int i=0;i<30;i++)Check(Step(s,th:true,hand:Bolt)==ReloadAction.None,"repeat chamber commit");
  Step(s,2,bd:true,bh:true);Step(s,2.35f,bh:true);Check(s.Hint,"hold B has no grab indicator");
  Check(Step(s,2.4f,bu:true)==ReloadAction.None&&s.Installed&&!s.Hint,"long B release drops untouched magazine");
  Step(s,3,bd:true,bh:true);Check(Step(s,3.35f,bh:true,td:true,th:true)==ReloadAction.TakeInstalled,"hold B + left trigger fails extraction");s.Detach(5,true);
  Check(!s.DiscardOnly&&s.Holding&&!s.Installed,"extracted magazine cannot be reinserted");
  Check(Step(s,th:true,hand:new Vector3(0,-.12f,0))==ReloadAction.None,"old magazine insertion started");
  Check(Step(s,th:true,hand:new Vector3(0,-.05f,0))==ReloadAction.Insert,"extracted magazine cannot be reinserted");
  s.Inserted(0);Check(s.Suspend()==0&&s.Installed,"inserted magazine refunded twice");s.Detach(5,true);
  Check(s.Suspend()==5&&s.Suspend()==0&&!s.Holding&&!s.Installed,"old magazine refund must occur once");
  s.Supply(8);Check(s.Suspend()==8&&s.Suspend()==0,"fresh magazine not refunded exactly once on interruption");
  s.Supply(4);Check(Step(s,th:false)==ReloadAction.DropHeld,"trigger release does not discard magazine");s.ConsumeHeld();Check(s.Suspend()==0,"deliberate discard refunded");
  s.Supply(0);Insert(s);Check(s.Installed&&s.HeldRounds==0&&s.NeedsRack,"empty reserve cannot supply empty magazine");
  s.Suspend();Check(s.NeedsRack&&s.Installed,"weapon swap/focus loss auto-chambers inserted magazine");
  var shotgun=new ManualReloadState(true);
  Check(Step(shotgun,bd:true,bh:true)==ReloadAction.None&&Step(shotgun,1.1f,bu:true)==ReloadAction.None,"shotgun ejects imaginary magazine");
  shotgun.Supply(1);Insert(shotgun);Check(shotgun.NeedsRack,"empty shotgun skips pump");
  shotgun.Supply(1);Insert(shotgun,1);Check(shotgun.NeedsRack,"second shell bypasses required pump");
  Step(shotgun,td:true,th:true,hand:Bolt,rounds:2);
  Check(!shotgun.Racking,"upper trigger operated pump");
  Step(shotgun,gh:true,hand:Bolt,rounds:2);
  Check(Step(shotgun,gh:true,hand:Bolt-new Vector3(0,0,.10f),rounds:2)==ReloadAction.RackBack&&shotgun.BlocksFire,"pump rear stroke rejected or fires early");
  Check(Step(shotgun,gh:true,hand:Bolt,rounds:2)==ReloadAction.Chamber&&!shotgun.BlocksFire,"pump forward stroke rejected");
  // 0.1.174: a short pump stroke: 3 cm back racks it, 2.5 cm forward from there closes it (not back to where it started).
  {var sg=new ManualReloadState(true);sg.Supply(1);Insert(sg);Step(sg,gh:true,hand:Bolt,rounds:1);
   Check(Step(sg,gh:true,hand:Bolt-new Vector3(0,0,.03f),rounds:1)==ReloadAction.RackBack,"a 3 cm pump stroke does not rack");
   Check(Step(sg,gh:true,hand:Bolt-new Vector3(0,0,.05f),rounds:1)==ReloadAction.None,"pulled further racked twice");
   Check(Step(sg,gh:true,hand:Bolt-new Vector3(.12f,.02f,.025f),rounds:1)==ReloadAction.Chamber,"2.5 cm forward (12 cm to the side) does not close the pump");}
  shotgun.Supply(1);Insert(shotgun,2);Check(!shotgun.NeedsRack,"topping up shotgun incorrectly unchambers it");
  shotgun.OnShot();Check(shotgun.NeedsRack&&shotgun.BlocksFire,"shotgun auto-cycled after a shot");
  Check(Step(shotgun,td:true,th:true,belt:true,hand:Belt,rounds:5,capacity:5)==ReloadAction.None,"full shotgun requests shell");
  var repeat=new ManualReloadState(false,true);
  for(int n=0;n<8;n++)
  {repeat.Detach(0,false);repeat.Supply(30);
   Step(repeat,th:true,hand:new Vector3(.12f,-.03f,0));
   Step(repeat,th:true,hand:new Vector3(0,-.12f,0));
   Check(Step(repeat,th:true,hand:new Vector3(0,-.03f,0))==ReloadAction.Insert,"AK retry after side approach stuck");repeat.Inserted(0);}
  var shortPump=new ManualReloadState(true);shortPump.OnShot();Step(shortPump,gh:true,hand:Bolt);
  Check(Step(shortPump,gh:true,hand:Bolt-Vector3.UnitZ*.045f)==ReloadAction.RackBack,"short physical pump stroke rejected");
  Check(Step(shortPump,gh:true,hand:Bolt)==ReloadAction.Chamber&&shortPump.Racking,"pump grip lost on closure");
  Check(Step(s,float.NaN,bd:true,bh:true)==ReloadAction.None,"nonfinite timestamp admitted");
  Check(SpreadPolicy.Multiplier(true,true,"pistol",.3f)==.3f&&SpreadPolicy.Multiplier(true,true,"ak47",.3f)==.3f&&SpreadPolicy.Multiplier(true,true,"revolver",.3f)==.3f,"supported spread regression");
  Check(SpreadPolicy.Multiplier(false,true,"pistol",.3f)==1&&SpreadPolicy.Multiplier(true,false,"ak47",.3f)==1&&SpreadPolicy.Multiplier(true,true,"shotgun",.3f)==1,"NPC/single hand/shotgun spread changed");
  Check(SpreadPolicy.Multiplier(true,true,"sniper",.3f)==0&&SpreadPolicy.Multiplier(true,false,"sniper",.3f)==.6f&&SpreadPolicy.Multiplier(false,true,"sniper",.3f)==1,"two-hand SVD must shoot where the scope looks");
  Check(SpreadPolicy.Multiplier(true,true,"m16",.3f)==.3f&&SpreadPolicy.Multiplier(true,true,"uzi",.3f)==.3f&&SpreadPolicy.Multiplier(true,false,"m16",.3f)==1,"rifle/SMG two-hand spread");
  var fast=new ManualReloadState();fast.Detach(0,false);fast.Supply(12);
  Check(Step(fast,th:true,hand:new Vector3(0,-.21f,0))==ReloadAction.None,"distant feed inserted");
  Check(Step(fast,th:true,hand:new Vector3(0,.065f,0))==ReloadAction.Insert,"feed crosses socket between frames but is lost");
  fast.ConsumeHeld();fast.Supply(12);
  Step(fast,th:true,hand:new Vector3(.10f,-.21f,0));
  Check(Step(fast,th:true,hand:new Vector3(.10f,.065f,0))==ReloadAction.None,"off-axis swept insert accepted");
  var rifle=new ManualReloadState(false,true);Check(Math.Abs(rifle.FullTravel-.05f)<1e-6,"AK travel not shortened");
  {
   // 0.1.161: an AK held low (a gun stock): its bolt by the belt pouch. The left trigger at the bolt pulls it
   // (never takes a magazine from the pouch), the pull goes on through the pouch, letting go chambers.
   var ak=new ManualReloadState(false,true);ak.Detach(0,false);ak.Supply(30);Insert(ak);
   Check(ak.NeedsRack&&ak.Installed,"AK not waiting for its bolt after a magazine");
   Check(Step(ak,2,td:true,th:true,belt:true,hand:Bolt,rounds:30,capacity:30)==ReloadAction.None&&ak.Racking,"trigger at the bolt by the pouch took a magazine instead of the bolt");
   Check(Step(ak,2.05f,th:true,belt:true,hand:Bolt-Vector3.UnitZ*.025f,rounds:30,capacity:30)==ReloadAction.None&&!ak.Holding&&ak.Racking,"half a pull through the pouch took a magazine");
   Check(Step(ak,2.1f,th:true,belt:true,hand:Bolt-Vector3.UnitZ*.05f,rounds:30,capacity:30)==ReloadAction.RackBack,"AK bolt not back at the pouch");
   Check(Step(ak,2.15f,th:true,belt:true,hand:Bolt-Vector3.UnitZ*.06f,rounds:30,capacity:30)==ReloadAction.None&&!ak.Holding,"held back bolt at the pouch took a magazine");
   Check(Step(ak,2.2f,belt:true,hand:Bolt-Vector3.UnitZ*.06f,rounds:30,capacity:30)==ReloadAction.Chamber&&!ak.NeedsRack&&ak.VisualRackTravel==0,"let go at the pouch: not chambered");
   Check(Step(ak,2.3f,td:true,th:true,belt:true,hand:Bolt,rounds:30,capacity:30)==ReloadAction.TakeSupply,"a chambered AK at the pouch cannot take a spare magazine");
   // A pull let slip (the hand far off): the bolt snaps forward, chambered.
   var slip=new ManualReloadState(false,true);slip.Detach(0,false);slip.Supply(30);Insert(slip);
   Step(slip,3,td:true,th:true,hand:Bolt,rounds:30,capacity:30);Step(slip,3.05f,th:true,hand:Bolt-Vector3.UnitZ*.05f,rounds:30,capacity:30);
   Check(Step(slip,3.1f,th:true,hand:Bolt+new Vector3(.5f,0,0),rounds:30,capacity:30)==ReloadAction.Chamber&&!slip.NeedsRack,"a pulled bolt let slip stays back");
  }
  Check(Math.Abs(shotgun.FullTravel-.105f*.70f)<1e-6,"pump travel not exactly 30 percent shorter");
  {
   // 0.1.117: crossbow — one bolt from the pouch; no magazine, no rack.
   // 0.1.122: laid on the rail at the muzzle end, drawn back until its rear reaches the string.
   var x=new ManualReloadState(false,false,true);var fwd=Vector3.UnitZ;
   ReloadAction X(float t,bool td=false,bool th=false,bool bd=false,bool bh=false,bool bu=false,bool pouch=false,Vector3? tip=null,int rounds=0,bool aligned=true)
    =>x.Step(t,bd,bh,bu,td,th,pouch,tip??new Vector3(0,0,-.3f),Port,Port,Bolt,fwd,rounds,1,false,tip,aligned,null,.40f);
   Vector3 R(float x0,float z)=>Port+new Vector3(x0,0,z);
   x.ObserveRounds(0);Check(!x.NeedsRack&&!x.SlideLocked&&x.Installed&&!x.Active&&!x.BlocksFire,"empty crossbow locked/racked like a pistol");
   Check(X(1,bd:true,bh:true)==ReloadAction.None&&X(1.1f,bu:true)==ReloadAction.None&&x.Installed,"B drops a crossbow 'magazine'");
   Check(X(2,td:true,th:true,pouch:true)==ReloadAction.TakeSupply,"no bolt taken at the pouch");x.Supply(1);
   Check(X(2.1f,th:true,tip:R(0,.05f))==ReloadAction.None&&!x.OnRail,"bolt caught near the string (must start at the muzzle end)");
   Check(X(2.2f,th:true,tip:R(.08f,.30f))==ReloadAction.None&&!x.OnRail,"bolt caught beside the rail");
   Check(X(2.25f,th:true,tip:R(0,.30f),aligned:false)==ReloadAction.None&&!x.OnRail,"bolt caught across the rail");
   Check(X(2.3f,th:true,tip:R(.01f,.30f))==ReloadAction.None&&x.OnRail&&Math.Abs(x.RailOffset-.30f)<1e-4f,"bolt laid on the rail at the muzzle end not caught");
   Check(X(2.4f,th:true,tip:R(.02f,.18f))==ReloadAction.None&&x.OnRail&&Math.Abs(x.RailOffset-.18f)<1e-4f,"bolt not drawn back along the rail");
   // 0.1.146: the rail holds the bolt from below: the hand sinking into the stock keeps it on.
   Check(X(2.42f,th:true,tip:Port+new Vector3(.01f,-.14f,.17f))==ReloadAction.None&&x.OnRail&&Math.Abs(x.RailOffset-.17f)<1e-4f,"bolt pushed down into the stock came off the rail");
   Check(X(2.43f,th:true,tip:Port+new Vector3(0,.12f,.17f))==ReloadAction.None&&!x.OnRail,"bolt lifted off the rail still on it");
   X(2.44f,th:true,tip:R(0,.30f));Check(x.OnRail,"bolt not laid on again");
   Check(X(2.445f,th:true,tip:Port+new Vector3(0,-.25f,.2f))==ReloadAction.None&&!x.OnRail,"bolt far under the crossbow still on the rail");
   X(2.447f,th:true,tip:R(0,.30f));X(2.448f,th:true,tip:R(.02f,.18f));
   Check(X(2.45f,th:true,tip:R(.12f,.18f))==ReloadAction.None&&!x.OnRail,"bolt pulled off the rail still on it");
   X(2.5f,th:true,tip:R(0,.32f));Check(x.OnRail,"bolt cannot be laid on again");
   Check(X(2.6f,th:true,tip:R(0,.01f))==ReloadAction.Insert&&!x.OnRail,"bolt drawn back to the string not loaded");
   x.Inserted(0);Check(!x.NeedsRack&&!x.Holding&&!x.BlocksFire,"loaded crossbow needs a rack or blocks fire");
   Check(X(3,td:true,th:true,pouch:true,rounds:1)==ReloadAction.None,"second bolt taken into a loaded crossbow");
   x.OnShot();Check(!x.NeedsRack&&!x.TakeSpentCase(),"crossbow shot ejects a case or needs a rack");
   ManualReloadState.RailSplit(new Vector3(.03f,-.10f,0),Vector3.UnitZ,out float off,out float below);Check(Math.Abs(off-.03f)<1e-5f&&Math.Abs(below-.10f)<1e-5f,"rail split below");
   ManualReloadState.RailSplit(new Vector3(.03f,.04f,0),Vector3.UnitZ,out off,out below);Check(Math.Abs(off-.05f)<1e-5f&&below==0,"rail split above");
   Console.WriteLine("PASS: crossbow bolt: taken at the pouch, laid on the rail at the muzzle end and drawn back to the string (not caught beside/across the rail or near the string, lost when pulled off or lifted; the rail holds it when the hand sinks into the stock), no magazine drop, no rack, one bolt at a time.");
  }
  {
   // 0.1.119: M60 — B opens the cover; box off/on with the left trigger only while it is open;
   // the cover is closed by taking it; then the charging handle.
   var m=new ManualReloadState(false,false,false,true);
   var box=new Vector3(-.06f,-.08f,.05f);var cover=new Vector3(0,.06f,.10f);var bolt=new Vector3(.05f,.02f,-.05f);var belt=new Vector3(-.3f,-.5f,-.1f);
   ReloadAction M(float t,bool bd=false,bool td=false,bool th=false,bool pouch=false,Vector3? hand=null,int rounds=100,Vector3? tip=null)
    =>m.Step(t,bd,bd,false,td,th,pouch,hand??new Vector3(0,-.3f,-.3f),box,Port,bolt,Vector3.UnitY,rounds,100,false,tip,true,cover);
   Check(M(1,td:true,th:true,hand:box)==ReloadAction.None&&m.Installed,"box taken with the cover closed");
   Check(M(2,bd:true)==ReloadAction.OpenCover&&m.CoverOpen&&m.BlocksFire&&m.Active,"B does not open the cover or the gun fires with it open");
   Check(M(2.1f,bd:true)==ReloadAction.None&&m.CoverOpen,"B while open changes the cover");
   Check(M(3,td:true,th:true,hand:box+new Vector3(0,.01f,0))==ReloadAction.TakeInstalled,"box not taken with the cover open");
   m.Detach(100,true);Check(m.Holding&&!m.Installed&&m.NeedsRack,"taken box not in hand");
   Check(M(3.1f,hand:new Vector3(0,-.4f,-.2f),rounds:0)==ReloadAction.DropHeld,"old box not dropped on release");m.ReturnHeld();
   Check(M(4,td:true,th:true,pouch:true,hand:belt,rounds:0)==ReloadAction.TakeSupply,"no new box at the belt");m.Supply(100);
   M(4.1f,th:true,hand:new Vector3(0,-.2f,0),tip:new Vector3(0,-.12f,0),rounds:0);
   Check(M(4.2f,th:true,hand:new Vector3(0,-.1f,0),tip:new Vector3(0,-.02f,0),rounds:0)==ReloadAction.Insert,"box pushed up not inserted");m.Inserted(0);
   Check(m.Installed&&m.NeedsRack&&m.CoverOpen&&m.BlocksFire,"after the box: cover still open, handle still needed");
   Check(M(5,td:true,th:true,hand:bolt)==ReloadAction.None&&m.CoverOpen,"racked with the cover open");
   Check(M(6,td:true,th:true,hand:cover+new Vector3(.02f,0,0))==ReloadAction.CloseCover&&!m.CoverOpen,"cover not closed by taking it");
   Check(M(6.1f,hand:new Vector3(0,-.3f,-.3f))==ReloadAction.None&&m.NeedsRack&&m.BlocksFire,"fires before the charging handle");
   M(7,td:true,th:true,hand:bolt);
   Check(M(7.1f,th:true,hand:bolt-Vector3.UnitZ*m.FullTravel)==ReloadAction.RackBack,"charging handle not pulled");
   Check(M(7.2f,hand:bolt-Vector3.UnitZ*m.FullTravel)==ReloadAction.Chamber&&!m.NeedsRack&&!m.BlocksFire,"charging handle does not ready the gun");
   m.ObserveRounds(0);Check(!m.SlideLocked&&!m.NeedsRack,"empty belt locks like a pistol slide");
   Console.WriteLine("PASS: M60: B opens the cover; box off/on only while open; cover closed by hand; then the charging handle; no fire until done.");
   // 0.1.223: the box goes in where it is taken from, however turned; the one just taken out not at once; the open cover pressed shut.
   var n=new ManualReloadState(false,false,false,true);
   ReloadAction N(float t,bool bd=false,bool td=false,bool th=false,Vector3? hand=null,int rounds=100,bool aligned=true)
    =>n.Step(t,bd,bd,false,td,th,false,hand??new Vector3(0,-.3f,-.3f),box,Port,bolt,Vector3.UnitY,rounds,100,false,null,aligned,cover);
   N(1,bd:true);Check(N(1.1f,td:true,th:true,hand:box)==ReloadAction.TakeInstalled,"M60 box not taken");n.Detach(100,true);
   Check(N(1.2f,th:true,hand:box+new Vector3(.02f,0,0))==ReloadAction.None&&n.Holding,"the box just taken out goes straight back in");
   N(1.3f,th:true,hand:box+new Vector3(0,-.25f,0));
   Check(N(1.4f,th:true,hand:box+new Vector3(0,-.05f,.04f),aligned:false)==ReloadAction.Insert,"the box brought back to its place (turned any way) does not go in");n.Inserted(0);
   n.Detach(100,false);n.Supply(100);
   Check(N(1.5f,th:true,hand:box+new Vector3(.05f,.05f,0),aligned:false)==ReloadAction.Insert,"a box from the belt brought to its place does not go in");n.Inserted(0);
   Check(n.CoverOpen&&n.PushCoverShut()==ReloadAction.CloseCover&&!n.CoverOpen&&n.PushCoverShut()==ReloadAction.None,"the open cover cannot be pressed shut (or shut twice)");
   n.Detach(0,false);var lid=new ManualReloadState(false,false,false,true);Check(lid.PushCoverShut()==ReloadAction.None,"a closed cover pressed shut");
   Console.WriteLine("PASS: 0.1.223 M60: the box goes in at its own place however it is turned (not the one just taken out until moved away); the open cover pressed shut by a hand.");

  }
  {
   // 0.1.221: the grip takes from the belt (the trigger only racks), and only while the gun wants rounds.
   var g=new ManualReloadState();
   ReloadAction G(float t,bool td=false,bool th=false,bool gd=false,bool gh=false,bool belt=false,Vector3? hand=null,int rounds=12)
    =>g.Step(t,false,false,false,td,th,belt,hand??Port,Port,Port,Bolt,Vector3.UnitY,rounds,12,gh,null,true,null,0,gd,gh);
   Check(G(1,gd:true,gh:true,belt:true,hand:Belt)==ReloadAction.None,"the grip takes a magazine at the belt with one in the gun (the belt's holster place)");G(1.1f);
   g.Detach(12,false);
   Check(G(2,td:true,th:true,belt:true,hand:Belt,rounds:0)==ReloadAction.None,"the trigger still takes a magazine from the belt");G(2.1f,rounds:0);
   Check(G(3,gd:true,gh:true,belt:true,hand:Belt,rounds:0)==ReloadAction.TakeSupply,"the grip at the belt takes no magazine");g.Supply(12);
   Check(G(3.1f,hand:new Vector3(0,-.2f,0),rounds:0)==ReloadAction.DropHeld,"the magazine stays in hand with the grip open");g.ReturnHeld();
   var sh=new ManualReloadState(shotgun:true);
   Check(sh.Step(4,false,false,false,false,false,true,Belt,Port,Port,Bolt,Vector3.UnitY,3,6,true,null,true,null,0,true,true)==ReloadAction.TakeSupply,"the grip takes no shell for a shotgun not full");sh.Supply(1);
   Check(sh.WantsSupply(6,6)==false&&new ManualReloadState(shotgun:true).WantsSupply(6,6)==false&&new ManualReloadState(shotgun:true).WantsSupply(5,6),"a full shotgun wants shells (or one not full does not)");
   // 0.1.223: the grip works the bolt too; the trigger no longer does.
   g.Supply(12);g.Inserted(0);Check(g.NeedsRack,"inserted magazine without a rack");
   G(5,td:true,th:true,hand:Bolt);G(5.05f,th:true,hand:Bolt+new Vector3(0,0,-.05f));G(5.1f,hand:Bolt);
   Check(g.NeedsRack,"the trigger still works the bolt");
   G(6,gd:true,gh:true,hand:Bolt);var back=G(6.05f,gh:true,hand:Bolt+new Vector3(0,0,-.05f));
   Check(back==ReloadAction.RackBack&&G(6.1f,hand:Bolt)==ReloadAction.Chamber&&!g.NeedsRack,"the grip does not work the bolt");
   Console.WriteLine("PASS: 0.1.221 the grip takes magazines and shells at the belt only while the gun wants them; the trigger takes none; the grip open drops it. 0.1.223: the grip works the bolt, the trigger does not.");
  }
  {
   // 0.1.223: the M60's open cover is solid for a hand: pressed from above it follows the hand down and stays; pressed down far enough it shuts.
   var hinge=new Vector3(.03f,.01f,-.13f);var edge=new Vector3(.03f,0,-.47f);var push=new CoverPush();
   Vector3 At(float deg,float r,float x=.03f){float a=deg*MathF.PI/180;return hinge+new Vector3(x-hinge.X,r*MathF.Sin(a),-r*MathF.Cos(a));}
   Check(!push.Step(0,At(110,.3f),hinge,edge,75)&&float.IsNaN(push.Opening)&&!push.Touching(0),"a hand well above the open cover touches it");
   Check(!push.Step(0,At(30,.3f),hinge,edge,75)&&!push.Touching(0),"a hand coming from under the open cover takes hold of it");
   Check(!push.Step(1,At(78,.3f,.03f+.2f),hinge,edge,75)&&!push.Touching(1),"a hand beside the cover touches it");
   Check(!push.Step(0,At(77,.25f),hinge,edge,75)&&push.Touching(0),"a hand on the open cover does not touch it");
   push.Step(0,At(50,.25f),hinge,edge,75);Check(Math.Abs(push.Opening-50)<3,"the cover does not follow the pressing hand down: "+push.Opening);
   push.Step(0,At(70,.25f),hinge,edge,75);Check(Math.Abs(push.Opening-50)<3,"the cover rises with the hand");
   push.Step(0,At(120,.25f),hinge,edge,75);Check(!push.Touching(0)&&Math.Abs(push.Opening-50)<3,"the hand lifted off still holds it, or it springs open");
   push.Step(1,At(52,.3f),hinge,edge,75);Check(push.Touching(1),"the other hand cannot press it too");
   push.Step(1,At(30,.3f),hinge,edge,75);Check(!push.Step(1,At(20,.3f),hinge,edge,75)&&Math.Abs(push.Opening-20)<3,"pressed to 20 degrees");
   Check(push.Step(1,At(10,.3f),hinge,edge,75)&&push.Opening==0&&!push.Touching(1),"pressed down it does not shut");
   push.Reset();Check(float.IsNaN(push.Opening),"opened again it stays pressed down");
   Check(!push.Step(0,new Vector3(float.NaN,0,0),hinge,edge,75)&&!push.Step(2,At(70,.3f),hinge,edge,75),"no tracking or a third hand presses it");
   Console.WriteLine("PASS: 0.1.223 the M60's open cover pressed by a hand: only from above, on it (not beside or from under it), it follows the hand down and stays, either hand, shut at 12 degrees.");
  }
  Console.WriteLine("PASS: tap/hold B classification, left trigger edges, discard-only extraction, directed insertion, bolt/pump travel, no automatic chambering, empty magazine, interruption refund exactly once, old/fresh refund policy, grip-only back/forward pump, per-shot gate and shotgun top-up.");
 }
}
