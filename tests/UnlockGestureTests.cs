using System;using System.Numerics;using XiiiXR;
class UnlockGestureTests {
 static void Check(bool x,string s){if(!x)throw new Exception(s);}
 static void Main(){var g=new UnlockGestureMath();var q=Quaternion.Identity;var p=new Vector3(1,1,1);
 Check(!g.Sample(false,p,q,p,0),"start opens key");
 Check(!g.Sample(false,p,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,.9f),p,1),"counterclockwise opens key");
 Check(!g.Sample(false,p,Quaternion.CreateFromAxisAngle(Vector3.UnitY,-.9f),p,2),"yaw opens key");
 Check(g.Sample(false,p,Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-.9f),p,3),"clockwise does not open");g.Reset();
 Check(!g.Sample(false,p+Vector3.One,q,p,0),"remote key starts");
 g.Sample(true,p,q,p,0);Check(!g.Sample(true,p+Vector3.UnitY*.12f,q,p,1),"upward card swipe accepted");Check(g.Sample(true,p-Vector3.UnitY*.12f,q,p,2),"downward card swipe refused");
 g.Reset();g.Sample(true,p,q,p,0);Check(!g.Sample(true,p-new Vector3(.2f,.12f,0),q,p,1),"sideways swipe accepted");
 g.Reset();g.Sample(true,p,q,p,0);g.Sample(true,p+Vector3.One,q,p,1);Check(!g.Sample(true,p-Vector3.UnitY*.12f,q,p,2),"leave reader retains gesture origin");
 // 0.1.203: holding the card to the reader opens it (no swipe); a brush does not; leaving restarts the hold.
 g.Reset();Check(!g.Card(true,p,p,10),"card opens at the first touch");Check(!g.Card(true,p,p,10.1f),"card opens before the hold");
 Check(g.Card(true,p,p,10.1f+UnlockGestureMath.CardHold),"card held to the reader does not open");
 g.Reset();g.Card(true,p,p,20);g.Card(false,p+Vector3.UnitZ*.2f,p,20.1f);Check(!g.Card(true,p,p,20.2f),"a brush and a new touch add up");
 Check(!g.Card(true,p,p,20.3f),"the new touch opens before its own hold");Check(g.Card(true,p,p,20.36f),"the new touch never opens");
 g.Reset();for(int i=0;i<50;i++)Check(!g.Card(false,p,p,30+i*.05f),"card opens without a touch or swipe");
 g.Reset();g.Card(false,p,p,40);Check(g.Card(false,p-Vector3.UnitY*.12f,p,40.5f),"the old swipe no longer opens");
 Console.WriteLine("PASS: key twist direction, pitch/yaw rejection, reader proximity, downward swipe, sideways/remote rejection, rearm after leaving reader; card held to the reader for "+UnlockGestureMath.CardHold+" s opens it, a brush does not, leaving restarts the hold, the swipe still opens.");}
}
