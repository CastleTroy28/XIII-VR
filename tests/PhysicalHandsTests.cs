using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using XiiiXR;
class PhysicalHandsTests
{
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static void Near(Vector3 a,Vector3 b,string s,float eps=.0001f)=>Check(Vector3.Distance(a,b)<eps,s);
    static void Main()
    {
        var q=Quaternion.CreateFromYawPitchRoll(.4f,.7f,-.8f);
        foreach(var scale in new[]{Vector3.Zero,new Vector3(0,1,1),new Vector3(1,0,1),new Vector3(1,1,0),new Vector3(0,0,.2f),new Vector3(-.2f,.2f,.2f),new Vector3(.234934f),Vector3.One})
        {
            var m=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(2,3,4);
            SnapshotPoseMath.Decompose(m,q,out var p,out var r,out var s);
            var rebuilt=Matrix4x4.CreateScale(s)*Matrix4x4.CreateFromQuaternion(r)*Matrix4x4.CreateTranslation(p);
            foreach(var axis in new[]{Vector3.Zero,Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})Near(Vector3.Transform(axis,m),Vector3.Transform(axis,rebuilt),"zero/partial/reflected bone was changed");
        }
        bool rejected=false;try{var m=Matrix4x4.Identity;m.M12=.5f;SnapshotPoseMath.Decompose(m,q,out _,out _,out _);}catch(InvalidOperationException){rejected=true;}Check(rejected,"sheared bone accepted");
        foreach(bool right in new[]{true,false})
        {
            string side=right?"R":"L";var names=new List<string>();var rest=new List<Matrix4x4>();
            for(int f=1;f<=4;f++)for(int j=1;j<=3;j++)
            {names.Add($"{side}_Finger_{f:00}_{j:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation((right?1:-1)*(-.035f+(f-1)*.021f),0,.055f+(j-1)*.03f));}
            for(int j=1;j<=3;j++){names.Add($"{side}_Thumb_01_{j:00}SHJnt");rest.Add(Matrix4x4.CreateTranslation(right?-.055f:.055f,-.01f,.025f+(j-1)*.023f));}
            var fingers=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
            var rim=fingers.RimContact("prop_wpn_ms_ashtray");
            Check(NativeHandMesh.Finite(rim)&&rim.Length()<.20f,"rim contact outside hand");
            Near(rim,fingers.RimContact("prop_wpn_ms_ashtray"),"cached rim contact changed");
            foreach(string profile in new[]{"medkit_s","medkit_l"})
            {
                var item=fingers.Pose(0,0,profile);var triggered=fingers.Pose(1,1,profile);
                for(int i=0;i<item.Length;i++)Near(item[i].Translation,triggered[i].Translation,"medkit returns to gun/fist pose on trigger");
                for(int f=0;f<5;f++)for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(item[f*3+j-1].Translation,item[f*3+j].Translation)-Vector3.Distance(rest[f*3+j-1].Translation,rest[f*3+j].Translation))<1e-5,"medkit grip stretched a bone");
                for(int f=0;f<5;f++)
                {
                    for(int j=1;j<3;j++)Check(MedkitGeometry.SegmentClear(item[f*3+j-1].Translation,item[f*3+j].Translation,profile=="medkit_l"),"finger bone crosses medkit case");
                    int last=f*3+2;Matrix4x4.Invert(rest[last],out var inverse);
                    var tip=rest[last].Translation+(rest[last].Translation-rest[last-1].Translation)*.7f;
                    tip=Vector3.Transform(Vector3.Transform(tip,inverse),item[last]);
                    Check(MedkitGeometry.SegmentClear(item[last].Translation,tip,profile=="medkit_l"),"distal fingertip penetrates medkit");
                }
                Check(item[2].Translation.Y<-.002f||item[5].Translation.Y<-.002f,"medkit grip remained entirely flat");
            }
            foreach(string profile in new[]{"pistol","ak47","shotgun"})
            {
                var size=profile=="pistol"?new Vector3(.022f,.11f,.035f):profile=="ak47"?new Vector3(.03f,.22f,.065f):new Vector3(.019f,.019f,.06f);
                ReloadGripGeometry.Set(profile,ReloadGripMath.Fit(profile,-size*.5f,size*.5f));
                var held=fingers.Pose(1,1,"reload_"+profile);var released=fingers.Pose(0,0,"reload_"+profile);
                for(int i=0;i<held.Length;i++)Near(held[i].Translation,released[i].Translation,"ammo grip falls back to trigger pose");
                for(int f=0;f<5;f++)for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(held[f*3+j-1].Translation,held[f*3+j].Translation)-Vector3.Distance(rest[f*3+j-1].Translation,rest[f*3+j].Translation))<1e-5,"ammo grip stretches a finger");
                Check(held.All(m=>NativeHandMesh.Finite(m.Translation)),"ammo grip has nonfinite joints");
                if(profile=="ak47")for(int f=0;f<4;f++)Check(held[f*3+2].Translation.Y<rest[f*3+2].Translation.Y-.008f,"rifle finger stayed straight: "+f);
                var geometry=ReloadGripGeometry.Get(profile)!;var pivot=Vector3.UnitZ*NativeHandMesh.WristZ;
                // 0.1.133: the right hand holds the ammunition mirrored (the left hand's geometry across x).
                Vector3 Sd(Vector3 v)=>right?new Vector3(-v.X,v.Y,v.Z):v;
                for(int f=0;f<5;f++)for(int j=1;j<3;j++)Check(geometry.SegmentClear(Sd(held[f*3+j-1].Translation+pivot),Sd(held[f*3+j].Translation+pivot)),"ammo phalanx collision profile="+profile+" right="+right+" finger="+f+" joint="+j+" a="+(held[f*3+j-1].Translation+pivot)+" b="+(held[f*3+j].Translation+pivot));
                if(profile=="pistol"&&!right)for(int f=0;f<5;f++)
                {
                    int last=f*3+2;Matrix4x4.Invert(rest[last],out var inv);
                    var tip=rest[last].Translation+(rest[last].Translation-rest[last-1].Translation)*.7f;
                    tip=Vector3.Transform(Vector3.Transform(tip,inv),held[last])+pivot;
                    Check(geometry.SegmentClear(held[last].Translation+pivot,tip),"pistol distal bone penetrates magazine");
                    Check(Vector3.Distance(tip,Vector3.Clamp(tip,geometry.Min,geometry.Max))<.009f,
                        "pistol fingertip remains beyond pad contact distance: "+f);
                }
            }
            var open=fingers.Pose(0,0,"");var fist=fingers.Pose(1,1,"");
            Check(Math.Abs(fist[1].Translation.X-fist[4].Translation.X)<Math.Abs(rest[1].Translation.X-rest[4].Translation.X),"closed fingers retain bind-pose gaps");
            Check(fist[14].Translation.Y<-.025f,"thumb remains above palm instead of crossing folded fingers");
            for(int f=0;f<4;f++)
            {
                Check(open[f*3+2].Translation.Y<-.008f&&open[f*3+2].Translation.Y>fist[f*3+2].Translation.Y,"idle fingers must be partly curled");
                Check(fist[f*3+2].Translation.Y<-.015f,"finger did not curl toward palm");
                for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(fist[f*3+j-1].Translation,fist[f*3+j].Translation)-.03f)<1e-5,"finger segment stretched");
            }
            var support=new FingerPoseMath(names.ToArray(),rest.ToArray(),right);
            Check(!support.Capture("revolver",rest.ToArray(),true,true),"retarget allowance accepted a flat dominant weapon grip");
            var fallback=support.Pose(1,.45f,"revolver");
            Check(fallback[5].Translation.Y<-.015f,"rejected native grip leaves dominant hand open");
            Check(support.Capture("revolver",fallback,true,true),"closed fallback anatomy invalid");
            support.Forget("revolver");Check(!support.Has("revolver"),"new visual retains previous fingers");
            Check(support.Capture("shotgun",rest.ToArray(),true),"settled native support hand rejected merely for straight fingers");
            var nativeSupport=support.Pose(1,0,"shotgun");
            for(int i=0;i<rest.Count;i++)Near(nativeSupport[i].Translation,rest[i].Translation,"native support pose replaced by generic curl");
            var outfitPose=rest.Select(x=>x*Matrix4x4.CreateScale(1.25f)).ToArray();
            Check(support.Capture("ak47",outfitPose,true),"uniform outfit retarget rejected");
            var outfitResult=support.Pose(1,0,"ak47");
            for(int i=0;i<rest.Count;i++)Near(outfitResult[i].Translation,outfitPose[i].Translation,"outfit retarget pose lost");
            var invalidSupport=rest.ToArray();invalidSupport[3]=Matrix4x4.CreateScale(0)*invalidSupport[3];
            Check(!support.Capture("ak47",invalidSupport,true),"settled pose bypasses collapsed bone guard");
            Check(!fingers.Capture("pistol",rest.ToArray()),"open palm cached as grip");Check(fingers.Capture("pistol",fist),"closed authored grip not captured");
            var trigger0=fingers.Pose(1,0,"pistol");var trigger1=fingers.Pose(1,1,"pistol");
            for(int i=0;i<rest.Count;i++)Near(trigger0[i].Translation,trigger1[i].Translation,"native trigger pose overwritten by procedural curl");
            Check(!fingers.Capture("fists",fingers.Pose(.45f,.45f,"")),"half-open guard cached as full fist");
            var pointing=(Matrix4x4[])fist.Clone();
            // Strong PIP flexion alone used to accept a fist with an extended MCP.
            int pointingIndex=0;
            var knuckle=rest[pointingIndex].Translation;
            var unbend=Matrix4x4.CreateTranslation(-knuckle)*Matrix4x4.CreateRotationX(-72*MathF.PI/180)*Matrix4x4.CreateTranslation(knuckle);
            for(int j=0;j<3;j++)pointing[pointingIndex+j]*=unbend;
            Check(!fingers.Capture("fists",pointing),"extended knuckle cached as full fist after weapon switch");
            var malformed=(Matrix4x4[])fist.Clone();malformed[1].Translation+=new Vector3(.025f,.015f,0);
            Check(!fingers.Capture("bad-length",malformed),"menu pose with stretched finger cached");
            malformed=(Matrix4x4[])fist.Clone();malformed[3]=Matrix4x4.CreateScale(0)*malformed[3];
            Check(!fingers.Capture("collapsed",malformed),"zero scale transition cached as weapon grip");
            Check(fingers.Capture("fists",fist),"native fist capture failed");
            var full=fingers.Pose(1,1,"");var relaxed=fingers.Pose(0,0,"");var half=fingers.Pose(.5f,.5f,"");
            for(int i=0;i<rest.Count;i++){Near(full[i].Translation,fist[i].Translation,"native closed fist not reproduced");}
            Check(relaxed[2].Translation.Y<-.008f&&Vector3.Distance(relaxed[2].Translation,full[2].Translation)>.02f,"captured fist does not relax on release");
            for(int f=0;f<4;f++)for(int j=1;j<3;j++)Check(Math.Abs(Vector3.Distance(half[f*3+j-1].Translation,half[f*3+j].Translation)-.03f)<1e-5,"native interpolation stretches finger");
            for(int swap=0;swap<30;swap++)
            {
                _=fingers.Pose(0,0,swap%2==0?"ak47":"shotgun");
                var held=fingers.Pose(0,0,"pistol");Check(held[5].Translation.Y<-.015f,"weapon switch lost saved grip");
                Check(!fingers.Capture("pistol",rest.ToArray()),"open transition overwrote saved grip");
            }
            var restElbow=new Vector3(.08f,.10f,-.22f);
            var straight=ArmIkMath.ForearmSwing(restElbow,new Vector3(0,0,-.25f));
            Near(Vector3.Normalize(Vector3.Transform(restElbow,straight)),-Vector3.UnitZ,"bent bind forearm was not straightened");
            Check(ArmIkMath.ForearmWeight(-.05f)==1 && ArmIkMath.ForearmWeight(-.20f)==1,"forearm is curved by position-varying bend instead of rigid rotation");
            var wrist=new Vector3(right?.4f:-.4f,1.15f,.4f);var shoulder=new Vector3(right?.18f:-.18f,1.4f,0);
            var elbow=ArmIkMath.Elbow(shoulder,wrist,new Vector3(right?1:-1,-1,0),.25f);
            Check(Math.Abs(Vector3.Distance(wrist,elbow)-.25f)<1e-5,"forearm changed length");
            var rot=ArmIkMath.ForearmRotation(new Vector3(0,0,-.25f),new Vector3(.15f,-.2f,0),-.25f);
            Check(2*MathF.Acos(Math.Clamp(Math.Abs(rot.W),0,1))<=35*MathF.PI/180+.0001f,"estimated elbow folds wrist beyond limit");
            Check(ArmIkMath.ForearmRotation(new Vector3(0,0,-.25f),Vector3.UnitY,0)==Quaternion.Identity,"wrist/watch seam bent");
        }
        var punch=new PunchMotion();float now=0;
        for(int i=0;i<15;i++){now+=.01f;Check(!punch.Sample(new Vector3(0,0,i*.02f),now,true,true),"held grip at startup punches");}
        punch.Reset();now=0;punch.Sample(Vector3.Zero,now,false,true);
        for(int i=0;i<12;i++){now+=.01f;Check(!punch.Sample(Vector3.Zero,now,true,true),"stationary fist punches");}
        bool struck=false;
        for(int i=1;i<9;i++){now+=.01f;if(punch.Sample(new Vector3(0,0,i*.025f),now,true,true)){Check(!struck,"same swing hit twice");struck=true;punch.Contact(now);}}
        Check(struck,"physical punch did not arm");
        for(int i=0;i<10;i++){now+=.01f;Check(!punch.Sample(new Vector3(0,0,.2f+i*.025f),now,true,true),"sweep repeats after contact");}
        punch.Sample(Vector3.Zero,now+.1f,false,false);Check(!punch.Sample(Vector3.UnitZ,now+.12f,true,true),"tracking recovery punches");
        punch.Reset();punch.Sample(Vector3.Zero,0,false,true);
        for(int i=1;i<500;i++)Check(!punch.Sample(Vector3.Zero,i*.01f,true,true),"stick-only motion created physical speed");
        HandImpact.Hit(true,Vector3.UnitZ,1);
        Check(HandImpact.Offset(true,1.05f).Z<-.02f&&HandImpact.Offset(false,1.05f)==Vector3.Zero,"impact does not recoil only the contacting hand");
        Check(HandImpact.Offset(true,1.11f)==Vector3.Zero,"impact does not settle");
        HandImpact.Clear(true);Check(HandImpact.Offset(true,1.05f)==Vector3.Zero,"impact survives reset");
        punch.Reset();now=0;punch.Sample(Vector3.Zero,now,false,true);
        // Squeeze while already starting the jab; no artificial pause required.
        for(int i=1;i<=4;i++){now+=.01f;punch.Sample(new Vector3(0,0,i*.025f),now,true,true);}
        now+=.01f;Check(punch.Sample(new Vector3(0,0,.108f),now,true,true),"recent fast swing lost during contact deceleration");punch.Contact(now);
        for(int i=0;i<21;i++){now+=.01f;Check(!punch.Sample(new Vector3(0,0,.108f),now,true,true),"stationary hand repeats punch after cooldown");}
        for(int i=1;i<=3;i++){now+=.01f;Check(!punch.Sample(new Vector3(0,0,.108f-i*.025f),now,true,true),"retraction itself is a second hit");}
        struck=false;for(int i=1;i<=4;i++){now+=.01f;struck|=punch.Sample(new Vector3(0,0,.033f+i*.025f),now,true,true);}
        Check(struck,"second jab cannot rearm by withdrawal");
        // 0.1.206: a held thing (its grip never lets go) touched by a slow stroke, or along an old line,
        // rearms once carried well off the touch on another line - not by going on along the stroke.
        {
            var held=new PunchMotion();now=0;held.Sample(Vector3.Zero,now,true,true);
            for(int i=1;i<=6;i++){now+=.01f;held.Sample(Vector3.Zero,now,true,true);}
            for(int i=1;i<=3;i++){now+=.01f;held.Sample(new Vector3(i*.03f,0,0),now,true,true);}
            held.Contact(now);var at=new Vector3(.09f,0,0);
            for(int i=1;i<=30;i++){now+=.01f;Check(!held.Sample(at+new Vector3(Math.Min(i,12)*.01f,0,0),now,true,true),"a held thing pushed on along its stroke rearmed and struck again");}
            bool again=false;for(int i=1;i<=30&&!again;i++){now+=.01f;again=held.Sample(at+new Vector3(.12f,0,i*.012f),now,true,true);}
            Check(!again,"a held thing struck while only being carried off its touch");
            for(int i=1;i<=4&&!again;i++){now+=.01f;again=held.Sample(at+new Vector3(.12f,-i*.03f,.36f),now,true,true);}
            Check(again,"a held thing never rearms after a touch unless pulled back along that stroke");
        }
        punch.Reset();now=0;punch.Sample(Vector3.Zero,now,false,true);
        for(int i=1;i<=4;i++){now+=.01f;punch.Sample(new Vector3(0,0,i*.025f),now,true,true);}
        for(int i=1;i<=20;i++){now+=.01f;punch.Sample(new Vector3(0,0,.1f+i*.005f),now,true,true);}
        now+=.01f;Check(!punch.Sample(new Vector3(0,0,.208f),now,true,true),"expired fast swing causes delayed touch damage");
        // 0.1.154: a shovel in one hand: its end shakes with the hand's tremor a
        // metre out, yet the swing arms when the hand holds still, and the end's
        // fast stroke (a third of a metre a frame) strikes.
        foreach(bool steadyHand in new[]{false,true})
        {
            var stick=new PunchMotion();now=0;bool hit=false;
            Vector3 End(int i)=>new Vector3((i%2==0?1:-1)*.009f,0,1);Vector3 Hand(int i)=>new Vector3((i%2==0?1:-1)*.0005f,0,0);
            for(int i=0;i<20;i++){now+=.011f;hit|=stick.Sample(End(i),now,true,true,steadyHand?Hand(i):null);}
            Check(!hit,"a shaking stick end strikes by itself");
            for(int i=1;i<=4;i++){now+=.011f;hit|=stick.Sample(new Vector3(i*.33f,0,1),now,true,true,steadyHand?new Vector3(i*.03f,0,0):null);}
            Check(hit==steadyHand,steadyHand?"a one-handed shovel swing never arms (its end is never still)":"without the hand the tremor at the end arms");
        }
        float pistolLag=0,shotgunLag=0,supportLag=0;
        foreach(string profile in new[]{"pistol","shotgun","ak47"})foreach(bool support in new[]{false,true})
        {
            var inertia=new WeaponInertia();inertia.Step(Vector3.Zero,Quaternion.Identity,.01f,profile,support,1);
            var target=Quaternion.CreateFromAxisAngle(Vector3.UnitY,.25f);
            var result=inertia.Step(new Vector3(.02f,0,0),target,.01f,profile,support,1);
            float lag=2*MathF.Acos(Math.Clamp(Math.Abs(Quaternion.Dot(result.rotation,target)),0,1));
            Near(result.position,new Vector3(.02f,0,0),"walking introduced translational lag");
            Check(lag>0&&lag<.26f,"weapon inertia absent or excessive");
            if(profile=="pistol"&&!support)pistolLag=lag;if(profile=="shotgun"){if(support)supportLag=lag;else shotgunLag=lag;}
            for(int i=0;i<300;i++)result=inertia.Step(new Vector3(.02f,0,0),target,.01f,profile,support,1);
            Near(result.position,new Vector3(.02f,0,0),"inertia never settles");
            result=inertia.Step(new Vector3(10,0,0),target,.01f,profile,support,1);Near(result.position,new Vector3(10,0,0),"teleport left weapon behind");
        }
        Check(shotgunLag>pistolLag&&supportLag<shotgunLag,"weapon weight/support ordering wrong");
        // 0.1.200: a held thing is timed by its part farthest from the hand (a chair's far legs), not a short one (a bottle).
        {
            var chair=new[]{new System.Numerics.Vector3(0,0,.05f),new System.Numerics.Vector3(.1f,.2f,.3f),new System.Numerics.Vector3(0,.4f,.6f),new System.Numerics.Vector3(float.NaN,0,0)};
            var hand=new System.Numerics.Vector3(0,0,0);
            if(PunchMotion.FarthestPart(chair,chair.Length,hand)!=2)throw new Exception("a chair not timed by its far end");
            if(PunchMotion.FarthestPart(chair,2,hand)!=1)throw new Exception("the parts' count ignored");
            var bottle=new[]{new System.Numerics.Vector3(0,0,.05f),new System.Numerics.Vector3(0,0,.2f)};
            if(PunchMotion.FarthestPart(bottle,bottle.Length,hand)!=-1||PunchMotion.FarthestPart(chair,chair.Length,new System.Numerics.Vector3(float.NaN,0,0))!=-1)throw new Exception("a short thing timed by its end");
            Console.WriteLine("PASS: 0.1.200 a held thing (a chair) is timed by its part farthest from the hand (25 cm or more), a bottle by the hand.");
        }
        Console.WriteLine("PASS: zero/partial/reflected skin bones; shear rejection; finger curl/length/release and repeated weapon swaps; forearm IK/wrist seam; physical-only punch arming and one hit per swing; a one-handed long stick armed by the still hand, timed by its end; differentiated bounded inertia/support/recenter.");
        Console.WriteLine("Synthetic geometry and math; actual native assets, contact damage and headset feel require an in-game test.");
    }
}
