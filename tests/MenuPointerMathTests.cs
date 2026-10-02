using System;
using System.Numerics;
using XiiiXR;
class MenuPointerMathTests
{
    static void Check(bool value,string text){if(!value)throw new Exception(text);}
    static void Main()
    {
        var panel=new Vector3(0,1.6f,1.7f);var origin=new Vector3(.3f,1.2f,.4f);
        var direction=Vector3.Normalize(panel-origin);
        Check(MenuRayMath.Plane(origin,direction,panel,Vector3.UnitZ,out var p,out float d)&&Vector3.Distance(p,panel)<1e-5f&&d>0,"controller ray misses center");
        var yaw=Quaternion.CreateFromAxisAngle(Vector3.UnitY,1.2f);
        Check(MenuRayMath.Plane(Vector3.Transform(origin,yaw),Vector3.Transform(direction,yaw),Vector3.Transform(panel,yaw),Vector3.Transform(Vector3.UnitZ,yaw),out p,out _)
            &&Vector3.Distance(p,Vector3.Transform(panel,yaw))<1e-5f,"turned menu has a different pointer coordinate space");
        Check(!MenuRayMath.Plane(origin,-direction,panel,Vector3.UnitZ,out _,out _),"behind-controller menu selectable");
        Check(!MenuRayMath.Plane(origin,Vector3.UnitY,panel,Vector3.UnitZ,out _,out _),"parallel ray selectable");
        var state=new UiPointerState();
        Check(!state.Sample(true,true,11).Down,"held trigger selects on startup");state.Sample(true,false,11);
        Check(state.Sample(true,true,11).Down,"fresh trigger does not press button");
        for(int i=0;i<80;i++)Check(!state.Sample(true,true,11).Down,"held trigger repeats button press");
        var release=state.Sample(true,false,11);Check(release.Up&&release.Click,"release on original target does not click");
        Check(!state.Sample(true,false,11).Click,"released trigger repeats click");
        state.Sample(true,true,11);release=state.Sample(true,false,22);Check(release.Up&&!release.Click,"release on another button activates it");
        state.Sample(true,true,11);release=state.Sample(false,true,11);Check(release.Up&&!release.Click,"tracking loss activates button");
        Check(!state.Sample(true,true,11).Down,"held trigger selects after tracking recovery");
        state.Sample(true,false,0);Check(!state.Sample(true,true,0).Down,"empty space receives press");Check(!state.Sample(true,true,11).Down,"drag-in with held trigger selects button");
        state.Sample(true,false,11);state.Sample(true,true,11);Check(!state.Sample(true,false,11,true).Click,"slider drag fires a click");
        Console.WriteLine("PASS: controller-to-panel coordinates, rotated/behind/parallel rays; release-to-arm; single press/click; off-target release; focus/tracking cancel; no held-trigger re-entry click; drag suppression.");
    }
}
