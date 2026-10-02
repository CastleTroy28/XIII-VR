using System;using XiiiXR;
class MechanismTests
{
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static void Main()
    {
        Check(MechanismMath.Matches("wpn_Pistol_SliderSHJnt","pistol"),"pistol slide binding");
        Check(MechanismMath.Matches("AK47_Bolt","ak47"),"AK bolt binding");
        Check(MechanismMath.Matches("wpn_ak47_ejection_port_BND_JNT","ak47"),"actual AK bolt bone missing");
        Check(MechanismMath.Matches("wpn_shotgun_forestock_BND_JNT","shotgun"),"actual shotgun pump missing");
        Check(MechanismMath.Matches("shotgun_ForeGrip","shotgun"),"shotgun pump binding");
        foreach(var name in new[]{"R_Arm_WristSHJnt","trigger_finger","magazine","weapon_root","barrel","wpn_pistol_slideStop_BND_JNT"})
            Check(!MechanismMath.Matches(name,"pistol"),"unrelated geometry matched: "+name);
        foreach(string profile in new[]{"pistol","shotgun","ak47"})
        {
            bool moved=false;
            for(int i=0;i<1000;i++){float v=MechanismMath.Cycle(i*.001f,profile);Check(v>=0&&v<=1,"mechanism travel unbounded");moved|=v>.9f;}
            Check(moved,"mechanism does not cycle");Check(MechanismMath.Cycle(-1,profile)==0&&MechanismMath.Cycle(float.PositiveInfinity,profile)==0&&MechanismMath.Cycle(2,profile)==0,"mechanism does not return/reset");
        }
        Check(MechanismMath.Cycle(.05f,"shotgun")==0,"pump cycles before post-shot delay");
        // 0.1.194: the Uzi's top cocking knob is drawn by hand only (it does not move when it fires).
        // 0.1.198: the Uzi's top knob is its "aim" bone; "cover" (the ejection port cover) and the rear sight do not move.
        Check(MechanismMath.Matches("wpn_uzi_aim_BND_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_cover_BND_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_rearAim_BND_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_aim_zero_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_magazine_BND_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_BND_JNT","uzi")&&!MechanismMath.Matches("wpn_uzi_safety_BND_JNT","uzi"),"Uzi knob binding");
        for(int i=0;i<300;i++)Check(MechanismMath.Cycle(i*.001f,"uzi")==0,"the Uzi's knob moves when it fires");
        Console.WriteLine("PASS: named mechanical bone filtering; bounded slide/bolt/pump cycle; post-shot pump delay; return to rest; no invented whole-mesh regions.");
    }
}
