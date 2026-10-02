using System;
using XiiiXR;
class StoryColorTests
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0001f,why);
    static void Main()
    {
        var s=new StoryEffectState();s.Observe(false,0);Near(s.White(0),0,"normal scene unexpectedly white");
        s.Trigger(1);s.Trigger(1.1f);Near(s.White(1.125f),.5f,"duplicate event restarts flash");
        s.Transition(true,1.3f);s.Observe(true,1.3f); // active subscene switches here
        Near(s.White(1.3f),1,"subscene change erases flash");Check(s.Memory,"memory not monochrome");
        Near(s.White(1.525f),.5f,"release envelope");Near(s.White(2),0,"flash stuck");Check(s.Memory,"playable flashback loses grade");
        s.Trigger(3);s.Transition(false,3.3f);s.Observe(false,3.3f);
        Near(s.White(3.3f),1,"return flash absent");Near(s.White(4),0,"return never clears");Check(!s.Memory,"beach remains grayscale");
        s.Reset();s.Observe(true,10);Check(s.Memory,"checkpoint inside memory not detected");Near(s.White(10),0,"checkpoint invents flash");
        s.Observe(false,11);Near(s.White(11),1,"missed hook not recovered from native state");
        s.Trigger(20);s.White(24);Near(s.White(25),0,"interrupted transition stuck white");
        s.Reset();Near(s.White(25),0,"reset retains flash");Check(!s.Memory,"reset retains memory");
        s.Pulse(30);s.Pulse(30.1f);Near(s.White(30.075f),.5f,"Timeline duplicate retriggers pulse");
        Near(s.White(30.15f),1,"Timeline flash does not reach white");
        Near(s.White(30.375f),.5f,"Timeline flash fade");
        Near(s.White(31),0,"Timeline-only flash waits indefinitely for a state event");
        Check(!s.Memory,"Timeline-only flash invents a flashback scene");
        s.Observe(true,32);s.Pulse(33);Near(s.White(34),0,"Timeline flash in memory remains white");
        Check(s.Memory,"Timeline pulse clears memory grade");
        s.Reset();Near(s.White(34),0,"reset leaves Timeline pulse");
        for(int i=0;i<30;i++)
        {
            float r=(i%7)/6f,g=(i%5)/4f,b=(i%3)/2f;
            StoryColorLut.Sample(r,g,b,false,0,out float x,out float y,out float z);
            Near(x,r,"identity red");Near(y,g,"identity green");Near(z,b,"identity blue");
            StoryColorLut.Sample(r,g,b,true,0,out x,out y,out z);
            Near(x,y,"gray RG differs");Near(y,z,"gray GB differs");
            StoryColorLut.Sample(r,g,b,true,1,out x,out y,out z);
            Near(x,1,"flash red");Near(y,1,"flash green");Near(z,1,"flash blue");
        }
        StoryColorLut.Sample(1,0,0,true,0,out float red,out _,out _);
        StoryColorLut.Sample(0,1,0,true,0,out float green,out _,out _);
        Check(green>red+.1f,"grayscale loses relative luminance/detail");
        StoryColorLut.Sample(1,0,0,.5f,0,out float halfR,out float halfG,out float halfB);
        Check(halfR<1&&halfR>red&&halfG>0&&halfG<red,"native volume saturation does not interpolate");
        Near(halfG,halfB,"partial saturation changes hue asymmetrically");
        // Distinct eye inputs remain distinct after the same pointwise grade.
        StoryColorLut.Sample(.1f,.2f,.3f,true,0,out float left,out _,out _);
        StoryColorLut.Sample(.8f,.6f,.2f,true,0,out float right,out _,out _);
        Check(Math.Abs(left-right)>.2f,"grade collapses different eye inputs");
        Console.WriteLine("PASS: sequence and independent Timeline flashes, duplicate suppression and cancellation; state/checkpoint sync; fractional native saturation; linear luminance/identity/white LUT; distinct eye inputs remain distinct. GPU behavior is separately probed in game.");
    }
}
