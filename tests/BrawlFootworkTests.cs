using System;using System.Numerics;using XiiiXR;
class BrawlFootworkTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  foreach(bool lead in new[]{true,false})foreach(int fps in new[]{30,72,120})foreach(float speed in new[]{.6f,1.65f,1.9f})
  {
   var p=new BrawlFootwork();var l=new Vector3(-.17f,.1f,lead?.18f:-.18f);var r=new Vector3(.17f,.1f,lead?-.18f:.18f);
   var lastL=l;var lastR=r;int steps=0;
   for(int i=0;i<fps*6;i++)
   {
    float t=i/(float)fps;var root=new Vector3(0,0,Math.Max(0,t-1)*speed);
    int prior=p.Moving;p.Advance(t,root,Quaternion.Identity,l,r,lead,t<1?0:speed,1);
    Check(p.Feet[0].X<0&&p.Feet[1].X>0,"feet crossed stance sides");
    if(prior==-1&&p.Moving>=0)steps++;
    bool movedL=Vector3.Distance(lastL,p.Feet[0])>1e-5f,movedR=Vector3.Distance(lastR,p.Feet[1])>1e-5f;
    Check(!movedL||!movedR,"both feet sliding at once");
    Check(Vector3.Distance(lastL,p.Feet[0])<.30f&&Vector3.Distance(lastR,p.Feet[1])<.30f,"foot jumps");
    var saved=p.Feet[0];p.Advance(t,root,Quaternion.Identity,l,r,lead,speed,1);Check(Vector3.Distance(saved,p.Feet[0])<1e-6f,"second render changes the foot");
    lastL=p.Feet[0];lastR=p.Feet[1];
   }
   Check(steps>8,"feet do not step while root moves");
   p.Reset();p.Advance(10,new(0,0,10),Quaternion.Identity,new(-.17f,.1f,10),new(.17f,.1f,10),lead,0,1);
   Check(p.Feet[0].Z==10,"teleport/reset drags feet");
  }
  Console.WriteLine("PASS BrawlFootwork: mirrored leads, planted support foot, alternating steps, bounded motion, duplicate render idempotence, reset; 30/72/120 fps.");
 }
}
