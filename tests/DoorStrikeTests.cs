using System;using XiiiXR;
class DoorStrikeTests
{
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static void Main(){foreach(float state in new[]{0f,.1f,.5f,1f,-.5f,-1f})
 Check(DoorStrikeMath.FastContact(.02f,.02f,.04f,state,true),"swing rejected for closed/partial/open/negative-side door");
 foreach(var c in new[]{(-.02f,.02f,.04f,1f,true),(.002f,.02f,.04f,1f,true),(.02f,.2f,.04f,1f,true),(.3f,.02f,.04f,1f,true),(.02f,.02f,.12f,1f,true),(.02f,.02f,-.01f,1f,true),(.02f,.02f,.04f,float.NaN,true),(.02f,.02f,.04f,1f,false),(float.NaN,.02f,.04f,1f,true),(.02f,.02f,float.NaN,1f,true)})
 Check(!DoorStrikeMath.FastContact(c.Item1,c.Item2,c.Item3,c.Item4,c.Item5),"withdrawal/slow/lag/teleport/distant/invalid/duplicate accepted");
 Console.WriteLine("PASS: fast contact works for closed, partially open and open doors on either hinge side; reject withdrawal, slow touch, frame stall, teleport, bad gap/state, and cooldown.");}
}
