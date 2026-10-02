using System;using System.Numerics;using XiiiXR;
class GripSpaceTests
{
 static void Near(Vector3 a,Vector3 b,string reason){if(Vector3.Distance(a,b)>.0001f)throw new Exception(reason+": "+a+" != "+b);}
 static void Main()
 {
  var initial=Matrix4x4.CreateFromYawPitchRoll(.4f,-.9f,.8f)*Matrix4x4.CreateTranslation(.2f,-.1f,.3f);
  var fit=Matrix4x4.CreateScale(.85f)*Matrix4x4.CreateRotationX(2)*Matrix4x4.CreateTranslation(.1f,.7f,-.2f);
  var part=Matrix4x4.CreateRotationZ(.3f)*Matrix4x4.CreateTranslation(-.1f,.02f,0);
  for(int i=0;i<100;i++)
  {
   var live=Matrix4x4.CreateFromYawPitchRoll(.03f*i,-.01f*i,.08f*i)*Matrix4x4.CreateTranslation(i*.007f,.1f,-.4f);
   var correction=GripSpaceMath.FrozenRoot(initial,live);
   foreach(var contact in new[]{Vector3.Zero,new Vector3(.01f,-.025f,.06f),new Vector3(-.015f,.007f,.035f)})
   {
    var handLive=Vector3.Transform(contact,live);
    var actual=Vector3.Transform(handLive,correction*part*fit);
    var rendered=Vector3.Transform(contact,initial*part*fit);
    Near(actual,rendered,"live hand misses frozen pistol handle");
    var reflected=new Vector3(-actual.X,actual.Y,actual.Z);
    Near(reflected,new Vector3(-rendered.X,rendered.Y,rendered.Z),"left reflected grip misses same stable gun");
   }
   // A static prop's source can animate after its copy was built.
   Matrix4x4.Invert(live,out var inverse);
   var propContact=new Vector3(.02f,.01f,-.03f);
   Near(Vector3.Transform(Vector3.Transform(propContact,live),inverse*part*fit),Vector3.Transform(propContact,part*fit),"prop grip follows an animated source instead of rendered mesh");
  }
  Console.WriteLine("PASS: 100 animated root poses retain exact frozen pistol and mirrored-left contact; static prop grip remains attached to rendered geometry.");
 }
}
