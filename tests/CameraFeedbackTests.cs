using System;
using System.Numerics;
using XiiiXR;
internal static class CameraFeedbackTests
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static void Near(Vector3 a,Vector3 b,string message) => Check(Vector3.Distance(a,b)<.00015f,message+" actual="+a+" expected="+b);
    static Vector3 Head(PoseValue anchor,Vector3 relative) => anchor.Position+Vector3.Transform(relative,anchor.Rotation);
    static void Main()
    {
        foreach(int hz in new[]{72,90,120})
        {
            var origin=new BodyAnchor(); var room=new Vector3(.35f,-.08f,-.27f);
            Vector3 root=new(3,1,-2),initialRoot=root;
            var initial=origin.Sample(root,Quaternion.Identity,1.609f,room); var initialHead=Head(initial,room);
            // Three minutes of continuous 75 degrees/sec stick turn + walking.
            for(int i=1;i<=hz*180;i++)
            {
                float time=(float)i/hz,yaw=time*75*MathF.PI/180;
                root=initialRoot+new Vector3(time*.1f,MathF.Sin(time)*.1f,time*.03f);
                var q=Quaternion.CreateFromYawPitchRoll(yaw,.3f*MathF.Sin(time),.2f*MathF.Cos(time));
                var a=origin.Sample(root,q,1.609f,room);
                Near(Head(a,room),initialHead+root-initialRoot,"stick turn or camera effects introduce positional drift at "+hz+"Hz");
                Near(Vector3.Transform(Vector3.UnitY,a.Rotation),Vector3.UnitY,"body tilt leaks into horizon");
                var again=origin.Sample(root,q,1.609f,room);
                Near(again.Position,a.Position,"multiple render/interaction samples change anchor");
            }
        }
        var b=new BodyAnchor(); var head=new Vector3(.4f,0,.2f);var yaw90=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);
        var a0=b.Sample(Vector3.Zero,Quaternion.Identity,1.6f,head);
        var a1=b.Sample(Vector3.Zero,yaw90,1.6f,head);
        Near(Head(a1,head),Head(a0,head),"90-degree turn orbits HMD instead of pivoting at it");
        var moved=head+new Vector3(.2f,-.3f,0);var a2=b.Sample(Vector3.Zero,yaw90,1.1f,moved);
        Near(Head(a2,moved)-Head(a1,head),new Vector3(0,-.8f,-.2f),"room-scale motion or crouch lost");
        b.Reset();var centered=b.Sample(Vector3.Zero,yaw90,1.6f,Vector3.Zero);
        Near(centered.Position,new Vector3(0,1.6f,0),"recenter retains accumulated pivot offset");
        b.Sample(Vector3.Zero,Quaternion.Identity,1.6f,head);
        var teleport=b.Sample(new Vector3(100,0,100),yaw90,1.6f,head);
        Near(teleport.Position,new Vector3(100,1.6f,100),"teleport retains turn compensation");
        // Walk/turn/room movement plus capsule alignment; compensation must not
        // create a second visual movement, and a wall-blocked sweep is not consumed.
        foreach(int hz in new[]{72,90,120})
        {
            var anchor=new BodyAnchor();var root=Vector3.Zero;
            for(int i=0;i<hz*120;i++)
            {
                var yaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,i*.016f);
                var room=new Vector3(.22f*MathF.Sin(i*.011f),-.05f,.3f*MathF.Cos(i*.007f));
                var before=anchor.Sample(root,yaw,1.6f,room);var h=Head(before,room);
                var gap=anchor.BodyGap(yaw,room);var accepted=gap; // unobstructed native sweep
                root+=accepted;anchor.ConsumeBodyMove(accepted);
                var after=anchor.Sample(root,yaw,1.6f,room);
                Near(Head(after,room),h,"capsule alignment moved the camera twice");
                Near(anchor.BodyGap(yaw,room),Vector3.Zero,"camera/capsule separation accumulates after turning");
                var blocked=anchor.Sample(root,yaw,1.6f,room+Vector3.UnitX*.1f);
                var offset=anchor.TurnOffset;anchor.ConsumeBodyMove(Vector3.Zero);
                Near(anchor.TurnOffset,offset,"blocked displacement was consumed, moving camera through a wall");
            }
        }
        Console.WriteLine("PASS: collision-body reconciliation preserves camera position; two minutes of mixed physical and stick turns; blocked moves consume zero.");
        Console.WriteLine("PASS: three-minute turning/walking at 72/90/120 Hz; no orbit/drift/roll; repeated eye reads; room-scale/crouch/recenter/teleport.");

        foreach(string profile in new[]{"pistol","shotgun","ak47"})
        {
            var one=new ShotFeedback();var two=new ShotFeedback();one.Advance(10);
            Check(one.Kickback==0 && one.Pitch==0 && one.Sequence==0,"no shot should mean no recoil");
            Check(one.Fire(10,profile,false) && two.Fire(10,profile,true),"real shot not accepted");
            Check(two.Pitch<one.Pitch*.5f && two.Kickback<one.Kickback*.5f,"support has no recoil benefit");
            float pitch=one.Pitch;
            for(int pellet=0;pellet<16;pellet++) Check(!one.Fire(10+pellet*.0001f,profile,false),"pellet created extra recoil");
            Check(one.Pitch==pitch && one.Sequence==1,"duplicate shot changed feedback");
            one.Advance(10.1f);two.Advance(10.1f);Check(two.Pitch<one.Pitch*.4f,"support recovery is not faster");
            one.Advance(12);Check(one.Pitch<.0001f && one.Kickback<.0001f,"recoil failed to return to tracked aim");
            for(int i=0;i<500;i++) one.Fire(13+i*.026f,profile,false);
            Check(one.Pitch<=12 && one.Kickback<=.10f,"automatic recoil grows unbounded");
            one.Reset();Check(one.Sequence==0 && one.Pitch==0 && one.FlashUntil==0,"weapon switch/stop retains feedback");
        }
        Console.WriteLine("PASS: actual-shot impulses; pellet deduplication; all three profiles; two-hand kick reduction and faster recovery; bounded sustained fire and cleanup.");
        var mechanical=new HapticChannel();mechanical.Mechanism(4,7,3200,.08f);mechanical.CancelShot();
        Check(mechanical.TryPulse(4,7,true,false,out ushort mechanicalUs)&&mechanicalUs==3200,"reload haptic requires grip or is erased by fire cancellation");
        Check(!mechanical.TryPulse(4.09f,7,true,false,out _),"mechanical haptic persists after latch");
        mechanical.Mechanism(5,7,3200,.08f);Check(!mechanical.TryPulse(5,7,false,false,out _),"mechanical haptic ignores focus/tracking loss");
        var pulse=new HapticBurst();pulse.Queue(1,7,6000,.055f);
        Check(pulse.TryPulse(1,7,true,out ushort us) && us==3999,"haptic duration not clamped to OpenVR range");
        Check(!pulse.TryPulse(1.004f,7,true,out _),"pulse spacing below 5 ms");
        Check(pulse.TryPulse(1.007f,7,true,out _),"burst does not continue");
        pulse.Cancel();pulse.Queue(1.008f,7,2000,.055f);
        Check(!pulse.TryPulse(1.008f,7,true,out _),"cancel/requeue bypasses pulse gap");
        Check(pulse.TryPulse(1.015f,7,true,out _),"requeued pulse absent");
        Check(!pulse.TryPulse(1.08f,7,true,out _),"pulse continues after burst expires");
        pulse.Queue(2,7,2000,.055f);Check(!pulse.TryPulse(2,8,true,out _),"pulse sent to replacement controller");
        pulse.Queue(3,7,2000,.055f);Check(!pulse.TryPulse(3,7,false,out _),"focus/tracking/input loss does not cancel haptic");
        Check(!pulse.TryPulse(3.01f,7,true,out _),"haptic resumes after regain without a new shot");
        Console.WriteLine("PASS: nonblocking haptic bursts; OpenVR duration/gap bounds; cancel/requeue gap; expiry/device/focus/tracking loss.");
    }
}
