// Pure production math/input tests. No simulated Unity rendering or native weapon hooks.
using System;
using System.Numerics;
using XiiiXR;
internal static class WeaponMathTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Same(Quaternion a, Quaternion b, string message) => Check(MathF.Abs(Quaternion.Dot(a,b)) > 0.99999f, message);
    public static void Main()
    {
        var edges = new ControlEdges();
        ulong t = HandControls.Trigger, g = HandControls.Grip, a = HandControls.A;
        Check(edges.Sample(true,t).Held == 0, "held trigger fired on startup");
        Check(edges.Sample(true,t).Down == 0, "startup trigger armed without release");
        edges.Sample(true,0);
        var s = edges.Sample(true,t|g); Check(s.Down == (t|g) && s.Held == (t|g), "trigger/grip press lost");
        s = edges.Sample(true,t|g); Check(s.Down == 0 && s.Held == (t|g), "hold generated duplicate press");
        s = edges.Sample(true,g); Check(s.Up == t && s.Held == g, "trigger release lost");
        edges.Sample(true,t); s = edges.Sample(false,0);
        Check(!s.Valid && s.Held == 0 && (s.Up&t) != 0, "disconnect retained fire");
        Check(edges.Sample(true,t).Held == 0, "reconnect fired while trigger held");
        edges.Sample(true,0); Check(edges.Sample(true,t).Down == t, "reconnect never rearmed");
        edges.Disarm(); Check(edges.Sample(true,t|a).Held == a, "pause failed to suppress trigger or suppressed interaction");
        Check(edges.Sample(true,t).Down == 0, "pause rearmed without release");
        edges.Sample(true,0); Check(edges.Sample(true,t).Down == t, "release after pause never rearmed");
        Console.WriteLine("PASS: input press/hold/release; held-trigger startup, disconnect/reconnect and pause require release.");

        var grip = new GripMath(); var zero = Vector3.Zero; var support = new Vector3(0,0,0.3f);
        var id = Quaternion.Identity;
        Same(grip.Solve(zero,id,support,support,true,true,true,.18f),id,"two-hand grab snapped aim");
        Check(grip.Held,"nearby support grab failed");
        var turned = grip.Solve(zero,id,new Vector3(.3f,0,0),support,true,false,true,.18f);
        Check(Vector3.Distance(Vector3.Transform(Vector3.UnitZ,turned),Vector3.UnitX)<.0001f,"support hand does not steer gun");
        Same(grip.Solve(zero,id,support,support,true,false,false,.18f),id,"release did not restore primary aim");
        Check(!grip.Held,"grip still held after release");
        grip.Solve(zero,id,new Vector3(.5f,0,0),support,true,true,true,.1f); Check(!grip.Held,"distant hand grabbed");
        grip.Solve(zero,id,support,support,true,false,true,.18f); Check(!grip.Held,"held button auto-grabbed without new press");
        grip.Solve(zero,id,support,support,true,true,true,.18f);
        grip.Solve(zero,id,support,support,false,false,true,.18f); Check(!grip.Held,"tracking loss retained grip");
        Same(grip.Solve(zero,id,zero,zero,true,true,true,.18f),id,"coincident hands corrupted rotation");
        Check(!grip.Held,"coincident hands grabbed");
        grip.Solve(zero,id,new Vector3(float.NaN,0,0),support,true,true,true,.18f); Check(!grip.Held,"NaN pose grabbed");
        grip.Solve(zero,id,new Vector3(0,0,1),new Vector3(0,0,1),true,true,true,.18f); Check(!grip.Held,"implausible separation grabbed");
        var pistolAim = Quaternion.CreateFromYawPitchRoll(.4f,-.3f,.2f);
        var side = new Vector3(-.09f,-.025f,.02f);
        Same(grip.Solve(zero,pistolAim,side,side,true,true,true,.18f),pistolAim,"side-by-side pistol grab snapped");
        grip.Release(); var vertical = GripMath.Look(Vector3.UnitY,Vector3.UnitY);
        Check(float.IsFinite(vertical.W) && Vector3.Distance(Vector3.Transform(Vector3.UnitZ,vertical),Vector3.UnitY)<.0001f,"vertical grip produced invalid frame");
        Console.WriteLine("PASS: two-hand steering and snap-free pistol grip; release, tracking loss, remote/invalid/vertical poses.");
    }
}
