using System;using XiiiXR;
class RevolverReloadTests
{
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);}
 static void Main()
 {
  var s=new RevolverReloadState();float t=0;
  RevolverAction Step(bool b=false,bool down=false,bool held=false,bool pouch=false,bool socket=false,float up=0,float velocity=0)=>s.Step(t+=.05f,.05f,b,down,held,pouch,socket,up,velocity);
  Check(Step(b:true)==RevolverAction.Open,"B does not open");s.Applied(RevolverAction.Open);
  for(int i=0;i<10;i++)Check(Step(up:0)==RevolverAction.None,"horizontal gun spills rounds");
  RevolverAction result=RevolverAction.None;for(int i=0;i<4;i++)result=Step(up:1);
  Check(result==RevolverAction.Empty,"muzzle up does not empty");s.Applied(result);
  Check(Step(up:1)==RevolverAction.None,"repeated empty transaction");
  Check(Step(down:true,held:true,pouch:true)==RevolverAction.Take,"pouch does not supply");s.Applied(RevolverAction.Take,3);
  Check(Step(held:true,socket:true)==RevolverAction.Load,"loader socket rejected");s.Applied(RevolverAction.Load);
  Check(!s.Holding&&s.HeldRounds==0,"loaded ammo still refundable");
  Check(Step(velocity:.8f)==RevolverAction.None,"swing closes too early");Check(Step(velocity:0)==RevolverAction.Close,"sharp stop not detected");s.Applied(RevolverAction.Close);
  Check(!s.Open,"cylinder remains open");
  s.Applied(RevolverAction.Open);s.Applied(RevolverAction.Empty);s.Applied(RevolverAction.Take,0);
  Check(s.Holding&&s.HeldRounds==0,"empty reserve cannot create empty loader");Check(Step()==RevolverAction.Drop,"release does not drop");s.Applied(RevolverAction.Drop);Check(!s.Holding,"loader retained");
  Console.WriteLine("PASS revolver opening, gravity gesture, exact-once transitions, partial/empty loader, load, drop and snap-close gesture. No headset/native asset validation.");
 }
}
