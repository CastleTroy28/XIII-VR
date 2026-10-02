using System;
using System.Numerics;
using XiiiXR;
internal static class LocomotionStateTests
{
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    private static StickSample S(float x,float y) => new(true,new Vector2(x,y));
    private static void Near(float a,float b,string message) => Check(MathF.Abs(a-b)<.0001f,message);
    public static void Main()
    {
        var c = new LocomotionState();
        c.Sample(S(0,1),S(1,0),true,.2f);
        Check(c.Move == Vector2.Zero && c.Turn == 0,"deflected sticks active on startup");
        c.Sample(S(0,0),S(0,0),true,.2f);
        c.Sample(S(0,1),S(1,0),true,.2f);
        Near(c.Move.Y,1,"forward sign/magnitude"); Near(c.Turn,1,"right turn sign");
        Check(!c.Jump.Held && !c.Crouch.Held,"horizontal turn triggered vertical action");
        c.Sample(S(-1,-1),S(-1,0),true,.2f);
        Near(c.Move.Length(),1,"diagonal speed exceeded straight speed"); Check(c.Move.X<0 && c.Move.Y<0 && c.Turn<0,"left/backward signs");
        c.Sample(S(.05f,-.05f),S(0,0),true,.2f); Check(c.Move == Vector2.Zero && c.Turn == 0,"center drift");
        c.Sample(S(0,.6f),S(0,0),true,.2f); Near(c.Move.Y,.5f,"analog half speed");
        c.Sample(S(0,0),S(0,1),true,.2f);
        Check(c.Jump.Down && c.Jump.Held && !c.Crouch.Held && c.Turn == 0,"up must jump only");
        c.Sample(S(0,0),S(0,.8f),true,.2f); Check(!c.Jump.Down && c.Jump.Held,"held up repeated jump edge");
        c.Sample(S(0,0),S(.9f,.1f),true,.2f); Check(c.Jump.Up && c.Turn == 0,"vertical gesture changed into turn without neutral");
        c.Sample(S(0,0),S(0,1),true,.2f); Check(!c.Jump.Down && !c.Jump.Held,"jump rearmed without neutral");
        c.Sample(S(0,0),S(0,0),true,.2f); c.Sample(S(0,0),S(0,-1),true,.2f);
        Check(c.Crouch.Down && c.Crouch.Held && !c.Jump.Held && c.Turn == 0,"down must crouch only");
        c.Sample(S(0,0),S(0,-.5f),true,.2f); Check(c.Crouch.Held && !c.Crouch.Down,"crouch hysteresis lost");
        c.Sample(S(0,0),S(0,0),true,.2f); Check(c.Crouch.Up && !c.Crouch.Held,"crouch release missing");
        c.Sample(S(0,0),S(.8f,.8f),true,.2f); Check(!c.Jump.Held && !c.Crouch.Held && c.Turn == 0,"ambiguous diagonal triggered action");
        Console.WriteLine("PASS: analog walking, radial deadzone, diagonal speed, turn signs, directional separation, gesture edges/hysteresis.");

        c.Sample(S(0,1),S(0,0),true,.2f); c.Sample(default,default,true,.2f);
        Check(c.Move == Vector2.Zero && c.Turn == 0 && !c.Jump.Held && !c.Crouch.Held,"disconnect retained input");
        c.Sample(S(0,1),S(0,1),true,.2f); Check(c.Move == Vector2.Zero && !c.Jump.Held,"reconnect accepted held sticks");
        c.Sample(S(0,0),S(0,0),true,.2f); c.Sample(S(0,1),S(1,0),true,.2f);
        c.Sample(S(0,1),S(1,0),false,.2f); Check(c.Move == Vector2.Zero && c.Turn == 0,"pause/lock did not stop input");
        c.Sample(S(0,1),S(1,0),true,.2f); Check(c.Move == Vector2.Zero && c.Turn == 0,"menu resume accepted held sticks");
        c.Sample(S(0,0),S(0,0),true,.2f); c.Sample(S(float.NaN,1),S(float.PositiveInfinity,0),true,.2f);
        Check(c.Move == Vector2.Zero && c.Turn == 0,"nonfinite values propagated");
        float at72=0,at120=0;
        for(int i=0;i<72;i++) at72+=LocomotionState.TurnDegrees(1,75,1f/72);
        for(int i=0;i<120;i++) at120+=LocomotionState.TurnDegrees(1,75,1f/120);
        Near(at72,at120,"turn speed depends on refresh rate"); Near(at72,75,"degrees/second scaling");
        Near(LocomotionState.TurnDegrees(1,75,10),3.75f,"hitch created a large turn");
        Near(LocomotionState.TurnDegrees(1,75,float.NaN),0,"invalid frame delta propagated");
        Console.WriteLine("PASS: disconnect/reconnect, pause/lock neutral rearming, nonfinite input rejection, 72/120 Hz turn rate and hitch cap.");

        var sprint=new LocomotionState();
        var pressed=new StickSample(true,Vector2.Zero,true);
        sprint.Sample(pressed,S(0,0),true,.2f);Check(!sprint.Sprint.Held,"held click sprints on startup");
        sprint.Sample(S(0,0),S(0,0),true,.2f);
        sprint.Sample(new StickSample(true,new Vector2(0,1),true),S(0,0),true,.2f);
        Check(sprint.Sprint.Down && sprint.Sprint.Held && sprint.Move.Y==1,"left click does not sprint while moving");
        sprint.Sample(pressed,S(0,0),true,.2f);Check(sprint.Sprint.Held && !sprint.Sprint.Down,"hold repeats sprint edge");
        sprint.Sample(S(0,0),pressed,true,.2f);Check(sprint.Sprint.Up && !sprint.Sprint.Held,"right click triggered sprint or missing left release");
        sprint.Sample(pressed,S(0,0),true,.2f);sprint.Sample(default,default,true,.2f);
        Check(!sprint.Sprint.Held && sprint.Sprint.Up,"disconnect sticks in sprint");
        sprint.Sample(pressed,S(0,0),true,.2f);Check(!sprint.Sprint.Held,"reconnect accepts held click");
        sprint.Sample(S(0,0),S(0,0),true,.2f);sprint.Sample(pressed,S(0,0),true,.2f);
        sprint.Sample(pressed,S(0,0),false,.2f);sprint.Sample(pressed,S(0,0),true,.2f);
        Check(!sprint.Sprint.Held,"pause/wheel/cinematic resumes held sprint");
        for(int axis=0;axis<5;axis++)
        {
            Check(StickSample.AxisClick(1UL<<(32+axis),axis),"axis click mapping missing");
            Check(!StickSample.AxisClick((1UL<<7)|(1UL<<2),axis),"grip/A interpreted as stick click");
        }
        Check(!StickSample.AxisClick(ulong.MaxValue,-1)&&!StickSample.AxisClick(ulong.MaxValue,5),"invalid axis accepted");
        Console.WriteLine("PASS: left stick sprint press/hold/release; no right-click sprint; neutral rearm after startup/loss/locks; dynamic OpenVR axis button mask.");
    }
}
