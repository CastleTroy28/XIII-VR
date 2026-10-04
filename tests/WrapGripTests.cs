using System;using System.Linq;using System.Collections.Generic;using System.Numerics;using XiiiXR;
class WrapGripTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static FingerPoseMath Hand(bool right,float scale)
 {
  string side=right?"R":"L";float sign=right?1:-1;
  var names=new List<string>();var rest=new List<Matrix4x4>();
  for(int f=0;f<4;f++)for(int j=0;j<3;j++)
  {
   names.Add($"{side}_Finger_{f+1:00}_{j+1:00}SHJnt");
   float z=(.09f+(f==1?.004f:f==3?-.009f:0)+j*(.030f-f*.0015f))*scale;
   rest.Add(Matrix4x4.CreateRotationZ(.15f*f)*Matrix4x4.CreateTranslation(sign*(-.03f+.02f*f)*scale,0,z));
  }
  for(int j=0;j<3;j++){names.Add($"{side}_Thumb_01_{j+1:00}SHJnt");rest.Add(Matrix4x4.CreateRotationY(-sign*.3f)*Matrix4x4.CreateTranslation(sign*(-.05f-j*.003f)*scale,-.012f*scale,(.043f+j*.027f)*scale));}
  return new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
 }
 static void Main()
 {
  // 0.1.200.
  int cases=0;
  foreach(bool right in new[]{true,false})foreach(float scale in new[]{.85f,1f,1.15f})
  {
   var hand=Hand(right,scale);
   Check(hand.WrapCircle(FingerPoseMath.HeldCurl,out var c0,out float r0)&&r0>.005f&&r0<.04f,"the fingers' circle not found");
   // The fingers closing further lie on a smaller circle.
   Check(hand.WrapCircle(1.1f,out _,out float r1)&&r1<r0-.002f,"the fingers' circle does not narrow as they close");
   float last=0;var report=new List<string>();
   foreach(float radius in new[]{.025f,.016f,.013f,.010f,.008f,.006f})
   {
    float curl=hand.WrapCurl(radius,out var shift);
    report.Add((radius*2000).ToString("F0")+"mm:"+curl.ToString("F2"));
    Check(curl>=FingerPoseMath.HeldCurl&&curl<=FingerPoseMath.MaxWrapCurl+1e-4f,"closure out of range");
    Check(curl>=last-1e-4f,"a thinner thing gripped less closed");last=curl;
    if(radius>=FingerPoseMath.StickRadius)Check(curl==FingerPoseMath.HeldCurl&&shift==Vector3.Zero,"a stick or thicker gripped other than before");
    else
    {
     Check(curl>FingerPoseMath.HeldCurl,"a thin thing not gripped tighter");
     // Narrowed by as much as it is thinner (to a hundredth of a fist's closure).
     hand.WrapCircle(curl,out var ck,out float rk);
     if(curl<FingerPoseMath.MaxWrapCurl)Check(rk<=r0-(FingerPoseMath.StickRadius-radius)+1e-5f,"the fingers' circle not narrowed enough");
     Check(Math.Abs(shift.Y-(ck.Y-c0.Y))<1e-5f&&Math.Abs(shift.Z-(ck.Z-c0.Z))<1e-5f&&shift.X==0&&shift.Length()<=.02f,"the thing's middle not moved with the fingers");
    }
    cases++;
   }
   Console.WriteLine("hand "+(right?"R":"L")+" x"+scale+" circle "+(r0*1000).ToString("F1")+" mm: "+string.Join(" ",report));
  }
  // The closure handed on in the hand's profile.
  Check(FingerPoseMath.WrapProfile(.96f)=="prop_long_handle@96"&&FingerPoseMath.WrapProfile(5)=="prop_long_handle@120"&&FingerPoseMath.WrapProfile(.3f)=="prop_long_handle@50","the closure's profile name");
  Check(Math.Abs(FingerPoseMath.HeldAmount("prop_long_handle@96")-.96f)<1e-5f&&FingerPoseMath.HeldAmount("prop_long_handle")==FingerPoseMath.HeldCurl&&FingerPoseMath.HeldAmount("prop_long_handle@x")==FingerPoseMath.HeldCurl&&FingerPoseMath.HeldAmount("pistol")==FingerPoseMath.HeldCurl,"the closure read from the profile");
  // The hand drawn with it: the fingertips nearer the palm than with a stick's closure.
  var h=Hand(true,1);var stick=h.Pose(1,0,"prop_long_handle");var tight=h.Pose(1,0,"prop_long_handle@120");
  int[] tips={2,5,8,11};
  Check(tips.All(i=>tight[i].Translation.Y<stick[i].Translation.Y-.002f||tight[i].Translation.Z<stick[i].Translation.Z-.002f),"the fingers not drawn closed further");
  Check(Enumerable.Range(12,3).All(i=>tight[i]==stick[i]),"the thumb changed");
  // 0.1.226: a thicker thing (the bazooka's rocket tube): the fingers and the thumb opened, its middle moved out.
  foreach(bool right in new[]{true,false})
  {
   var hand=Hand(right,1);hand.WrapCircle(FingerPoseMath.HeldCurl,out var c0,out float r0);float last=FingerPoseMath.HeldCurl+1e-4f;
   foreach(float radius in new[]{.016f,.020f,.024f,.028f})
   {
    float curl=hand.WrapCurlThick(radius,out var shift);
    Check(curl<=last&&curl>=FingerPoseMath.MinWrapCurl-1e-4f,"a thicker thing gripped more closed: "+radius+" "+curl);last=curl;
    if(radius<=FingerPoseMath.StickRadius)Check(curl==FingerPoseMath.HeldCurl&&shift==Vector3.Zero,"the stick gripped other than before");
    else
    {
     Check(curl<FingerPoseMath.HeldCurl,"a thick tube not gripped more open");
     hand.WrapCircle(curl,out var ck,out float rk);
     if(curl>FingerPoseMath.MinWrapCurl)Check(rk>=r0+(radius-FingerPoseMath.StickRadius)-1e-5f,"the fingers' circle not widened enough");
     Check(shift.X==0&&shift.Length()<=.025f+1e-6f,"the tube's middle moved too far");
    }
   }
   Check(hand.WrapCurl(.03f,out _)==FingerPoseMath.HeldCurl,"the thin-barrel closure changed for a thick one");
  }
  Check(Math.Abs(FingerPoseMath.HeldAmount("prop_long_handle@70")-.70f)<1e-5f&&FingerPoseMath.ThumbAmount("prop_long_handle")==.85f&&FingerPoseMath.ThumbAmount("prop_long_handle@120")==.85f
   &&FingerPoseMath.ThumbAmount("prop_long_handle@70")<.85f*.8f,"the thumb not opened round a thick thing (or opened round a stick)");
  {var h2=Hand(true,1);var stick2=h2.Pose(1,0,"prop_long_handle");var open=h2.Pose(1,0,"prop_long_handle@70");
   Check(Enumerable.Range(12,3).Any(i=>open[i]!=stick2[i]),"the thumb drawn as round a stick round a thick tube");}
  Console.WriteLine("PASS: 0.1.226 a thicker tube (the bazooka's rocket) is held with the fingers and the thumb opened (the circle widened by as much as it is thicker, its middle moved out), a stick and a thin barrel as before.");
  Console.WriteLine("PASS: 0.1.200 a thinner barrel is gripped with the fingers closed further (the circle they lie on narrowed by as much as it is thinner, its middle moved with them; a stick or thicker as before), handed on in the hand's profile, drawn so; "+cases+" cases.");
 }
}
