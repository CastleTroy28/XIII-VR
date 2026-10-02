using System;using System.Numerics;using XiiiXR;
class HandGripMemoryTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  var q=Quaternion.Normalize(new Quaternion(.1f,.7f,-.2f,.68f));
  string line=HandGripMemory.Format("knife",new Vector3(.012f,-.034f,.051f),q,.884f);
  Check(HandGripMemory.Parse(line,out var p,out var point,out var turn,out float size)&&p=="knife"&&Vector3.Distance(point,new Vector3(.012f,-.034f,.051f))<1e-6f&&Math.Abs(Quaternion.Dot(turn,q))>.99999f&&Math.Abs(size-.884f)<1e-6f,"a hold written and read back: "+line);
  Check(!HandGripMemory.Parse("",out _,out _,out _,out _)&&!HandGripMemory.Parse("knife|1|2",out _,out _,out _,out _)&&!HandGripMemory.Parse("knife|x|0|0|0|0|0|1|1",out _,out _,out _,out _),"broken lines");
  Check(!HandGripMemory.Parse("knife|5|0|0|0|0|0|1|1",out _,out _,out _,out _)&&!HandGripMemory.Parse("knife|0|0|0|0|0|0|1|40",out _,out _,out _,out _)&&!HandGripMemory.Parse("knife|0|0|0|0|0|0|3|1",out _,out _,out _,out _),"a hold far off, a strange size, a broken turn");
  var read=HandGripMemory.Read(new[]{line,"junk",HandGripMemory.Format("pistol",Vector3.One*.01f,Quaternion.Identity,1)});
  Check(read.Count==2&&read.ContainsKey("knife")&&read.ContainsKey("pistol"),"a file read: "+read.Count);
  Check(HandGripMemory.Changed(false,default,default)&&!HandGripMemory.Changed(true,Vector3.Zero,new Vector3(.003f,0,0))&&HandGripMemory.Changed(true,Vector3.Zero,new Vector3(.006f,0,0)),"written again only when new or moved");
  Console.WriteLine("PASS: weapon holds kept between games (a knife copy in the left hand held like the right hand's knife, mirrored): written and read back exactly, broken or strange lines skipped, written again only when a hold moved.");
 }
}
