using System;
using System.Numerics;
using XiiiXR;
class DualGripTests
{
    static void Near(Vector3 a,Vector3 b,string message)
    {if(Vector3.Distance(a,b)>.00002f)throw new Exception(message+": "+a+" != "+b);}
    static void Main()
    {
        var mirror=Matrix4x4.CreateScale(-1,1,1);
        // Left bones have deliberately unrelated local bases, as an imported
        // character may. Matching global skin deformation is the invariant.
        for(int i=0;i<30;i++)
        {
            var rr=Matrix4x4.CreateFromYawPitchRoll(i*.13f,-.4f,.2f)*Matrix4x4.CreateTranslation(-.02f,0,.03f+i*.002f);
            var lr=Matrix4x4.CreateFromYawPitchRoll(-.8f,i*.11f,1.3f)*Matrix4x4.CreateTranslation(.02f,0,.03f+i*.002f);
            var move=Matrix4x4.CreateRotationX(.6f)*Matrix4x4.CreateTranslation(.01f,-.025f,-.018f);
            var posed=rr*move;
            var lp=DualGripMath.MirrorDeformation(lr,rr,posed);
            Matrix4x4.Invert(lr,out var li);
            foreach(var point in new[]{Vector3.Zero,new Vector3(.02f,.01f,.07f),new Vector3(-.01f,-.015f,.13f)})
            {
                var expected=Vector3.Transform(Vector3.Transform(point,move),mirror);
                var actual=Vector3.Transform(Vector3.Transform(point,mirror),li*lp);
                Near(actual,expected,"left skin does not mirror right grip");
            }
            var neutral=DualGripMath.MirrorDeformation(lr,rr,rr);
            Near(neutral.Translation,lr.Translation,"rest wrist moved");
        }
        Console.WriteLine("PASS: dual grip deformation preserves independent left bone axes and reflected palm/finger positions (90 probes).");
    }
}
