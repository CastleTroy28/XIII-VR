using System;using XiiiXR;
class GrenadeFuseLedgerTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var l=new GrenadeFuseLedger();var a=(IntPtr)11;var ha=(IntPtr)101;var hb=(IntPtr)102;
  l.Launch(a,ha,10,4.5f);
  Check(l.Live(a,12)&&l.LiveUntil>=10+4.5f&&l.Due(12).Count==0,"a grenade in flight is not live / its fuse stopped");
  Check(l.Exploded(a,hb,14.5f)&&!l.Exploded((IntPtr)99,hb,14.5f),"explosion of the player's grenade not noted / someone else's taken as ours");
  Check(l.Due(15).Count==0&&l.Live(a,15),"the bang cut off at once");
  var due=l.Due(14.5f+GrenadeFuseLedger.Tail);
  Check(due.Count==2&&due.Contains(ha)&&due.Contains(hb)&&l.Count==0,"the fuse sound not stopped after the bang: "+due.Count);
  // Never exploded: let go after its fuse plus a margin.
  l.Launch(a,ha,20,4.5f);Check(l.Due(20+4.5f+GrenadeFuseLedger.Lost-.1f).Count==0,"stopped too early");
  Check(l.Due(20+4.5f+GrenadeFuseLedger.Lost).Count==1&&l.Count==0,"a grenade that never exploded keeps its fuse");
  // Two grenades: live until the later one's bang tail; one instance listed once.
  l.Launch(a,ha,30,4.5f);l.Launch((IntPtr)12,ha,31,4.5f);l.Exploded(a,ha,34.5f);
  Check(Math.Abs(l.LiveUntil-(31+4.5f+GrenadeFuseLedger.Tail))<1e-4f,"live until: "+l.LiveUntil);
  var d2=l.Due(40);Check(d2.Count==1&&d2[0]==ha,"one instance stopped twice");
  l.Clear();l.Launch(a,IntPtr.Zero,50,float.NaN);Check(l.LiveUntil>50&&l.Due(50+5+GrenadeFuseLedger.Lost).Count==0&&l.Count==0,"no instance / bad delay");
  Console.WriteLine("PASS: grenade fuse ledger: in flight live and untouched; the bang plays its tail ("+GrenadeFuseLedger.Tail+" s), then the fuse sound (launch and bang instances) is stopped; a grenade that never exploded is let go after its fuse + "+GrenadeFuseLedger.Lost+" s; two grenades keep the later one live.");
 }
}
