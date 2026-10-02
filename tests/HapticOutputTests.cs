using System;using XiiiXR;using UnityEngine.XR;
class HapticOutputTests
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static void Main()
 {
  var output=new HapticOutput();
  Check(output.Send(false,3999)&&InputDevices.Last==XRNode.LeftHand,"left pulse routed to wrong hand");
  Check(InputDevices.Left.Amplitude==1&&InputDevices.Left.Duration>0,"missing amplitude/envelope");
  Check(output.Send(true,65000)&&InputDevices.Right.Amplitude==1,"out-of-range pulse amplitude");
  InputDevices.Left.Valid=false;Check(!output.Send(false,3000),"invalid XR device prevents legacy fallback");
  InputDevices.Left.Valid=true;InputDevices.Left.Accept=false;Check(!output.Send(false,3000),"rejected provider pulse prevents legacy fallback");
  InputDevices.Left.Accept=true;InputDevices.Left.Fail=true;Check(!output.Send(false,3000),"interop exception escaped");
  Check(output.Send(true,3999),"one hand failure disabled both hands");
  InputDevices.Left.Fail=false;Check(!output.Send(false,3000),"broken provider called repeatedly");
  output.Reset();Check(output.Send(false,3000),"manual vibration test cannot retry failed provider");
  Console.WriteLine("PASS: provider device routing, amplitude, invalid/rejected/error fallback and independent-hand recovery. Hardware sensation not tested.");
 }
}
namespace UnityEngine.XR
{
 enum XRNode{LeftHand,RightHand}
 class Device
 {
  internal bool Valid=true,Accept=true,Fail;internal float Amplitude,Duration;internal string name=>"test XR provider";internal bool isValid=>Valid;
  internal bool SendHapticImpulse(uint channel,float amplitude,float duration){if(Fail)throw new Exception("provider failure");Amplitude=amplitude;Duration=duration;return Accept;}
 }
 static class InputDevices{internal static readonly Device Left=new(),Right=new();internal static XRNode Last;internal static Device GetDeviceAtXRNode(XRNode node){Last=node;return node==XRNode.LeftHand?Left:Right;}}
}
namespace XiiiXR{static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}}
