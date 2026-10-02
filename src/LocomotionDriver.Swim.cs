using System;
using System.Globalization;
using Il2CppInterop.Runtime;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
// 0.1.170. The 0.1.168 log: the strokes and
// the stick reached the game and the character went where he looked, but at
// about 0.4 m/s instead of about 2.4 m/s (0.1.148, the same beach), 5-6 m
// deep; he did not come up and drowned. The swim code is the same as in
// 0.1.148; the log did not say what held him. Now, every 2 s in water while
// swimming: what the game was given, what speed the game itself asked for,
// how fast the character really moved, and what it touched (above: a
// ceiling or the pier's deck; sides: a wall, posts, rocks; below: the
// bottom), and whether a hand held a ladder. A swim much slower than the
// game asked for is written as SWIM SLOW.
internal sealed partial class LocomotionDriver
{
    private CharacterController? swimBody;private IntPtr swimBodyOwner;
    private Vector3 swimFrom;private Vector2 swimGiven;private string swimPath="";
    private float swimSince=-1,swimPushSum,swimAskedSum,swimStrokeMax;
    private int swimFrames,swimAbove,swimSides,swimBelow,swimHand,swimReports;private bool swimReportFailed;
    internal const int SwimReportLimit=300;
    partial void SwimGiven(Vector2 given,string path){swimGiven=given;swimPath=path;}
    partial void SwimReport()
    {
        if(swimReportFailed)return;
        try
        {
            var ch=character;
            if(!inWater||ch==null||!Allowed(true)){swimSince=-1;return;}
            float now=Time.realtimeSinceStartup;var at=ch.transform.position;
            if(swimSince<0){StartSwimSpan(now,at);return;}
            if(swimBody==null||swimBodyOwner!=ch.Pointer)
            {swimBodyOwner=ch.Pointer;swimBody=ch.GetComponentInChildren(Il2CppType.Of<CharacterController>(),true)?.TryCast<CharacterController>();}
            swimFrames++;swimPushSum+=Math.Min(1,swimGiven.magnitude);swimStrokeMax=Math.Max(swimStrokeMax,stroke.Forward);
            var v=ch.velocity;swimAskedSum+=new Vector2(v.x,v.z).magnitude;
            if(swimBody!=null)
            {
                var f=swimBody.collisionFlags;
                if((f&CollisionFlags.Above)!=0)swimAbove++;if((f&CollisionFlags.Sides)!=0)swimSides++;if((f&CollisionFlags.Below)!=0)swimBelow++;
            }
            if(InteractionDriver.Current?.ClimbingActive==true)swimHand++;
            float span=now-swimSince;if(span<2)return;
            int n=Math.Max(1,swimFrames);float push=swimPushSum/n,asked=swimAskedSum/n;
            var moved=at-swimFrom;float across=new Vector2(moved.x,moved.z).magnitude/span,up=moved.y/span;
            float speed=0;try{speed=ch.swimSpeed;}catch(Exception){speed=0;}
            if(push>.25f&&swimReports<SwimReportLimit)
            {
                swimReports++;
                bool slow=speed>0&&across<.35f*push*speed;
                string Share(int k)=>(100*k/n).ToString(CultureInfo.InvariantCulture)+"%";
                string text=(slow?"SWIM SLOW ":"SWIM MOVE ")+(submerged?"under":"surface")
                    +" given="+push.ToString("F2",CultureInfo.InvariantCulture)+" (stick "+state.Move.X.ToString("F2",CultureInfo.InvariantCulture)+","+state.Move.Y.ToString("F2",CultureInfo.InvariantCulture)
                    +", strokes "+swimStrokeMax.ToString("F2",CultureInfo.InvariantCulture)+", "+swimPath+", gaze pitch "+swimPitch.ToString("F0",CultureInfo.InvariantCulture)+")"
                    +" game asked "+asked.ToString("F2",CultureInfo.InvariantCulture)+" m/s of its "+speed.ToString("F1",CultureInfo.InvariantCulture)
                    +", moved "+across.ToString("F2",CultureInfo.InvariantCulture)+" m/s across "+(up>=0?"+":"")+up.ToString("F2",CultureInfo.InvariantCulture)+" m/s up"
                    +"; touching above "+Share(swimAbove)+" sides "+Share(swimSides)+" below "+Share(swimBelow)+(swimBody==null?" (no body found)":"")
                    +"; hand on a ladder "+Share(swimHand)+" at ("+at.x.ToString("F1",CultureInfo.InvariantCulture)+", "+at.y.ToString("F1",CultureInfo.InvariantCulture)+", "+at.z.ToString("F1",CultureInfo.InvariantCulture)+")"
                    // 0.1.185: the game's own camera pitch (levelled in the water) and its acceleration in the water.
                    +"; game camera pitch "+GamePitchText(ch)+", swim acceleration "+SwimAccelerationText(ch);
                Bootstrap.Write(text);
            }
            StartSwimSpan(now,at);
        }
        catch(Exception ex){swimReportFailed=true;Bootstrap.Warn("SWIM report off (swimming continues): "+ex.Message);}
    }
    // 0.1.185: the game swims along its own
    // camera's pitch, and VR never turns that camera up or down - it keeps
    // whatever pitch the mission started with. The first mission starts with
    // XIII lying on the beach, and the logs of that mission show the game
    // swimming at 0.4-0.6 m/s of its 3 with the stick full forward, at times
    // well (after something else had reset that pitch). In the water that
    // pitch is set level: the game swims level, as in the other missions (the
    // look still steers up and down, as before).
    private bool pitchLevelFailed;private int pitchLevelReports;private float pitchLevelled=float.NaN;
    internal const float LevelTolerance=.5f;
    partial void LevelGamePitch()
    {
        if(pitchLevelFailed)return;
        var ch=character;if(ch==null||!Allowed(true))return;
        try
        {
            var e=ch.cameraInputTargetRot.eulerAngles;float x=e.x>180?e.x-360:e.x;
            if(Math.Abs(x)<LevelTolerance)return;
            ch.cameraInputTargetRot=Quaternion.Euler(0,e.y,e.z);
            if(pitchLevelReports<20&&!(Math.Abs(x-pitchLevelled)<1))
            {pitchLevelReports++;pitchLevelled=x;Bootstrap.Write("SWIM the game's own camera was pitched "+x.ToString("F0",CultureInfo.InvariantCulture)+" degrees ("+(x>0?"down":"up")+"; VR never turns it): set level in the water, the game swims level (the look steers up and down)");}
        }
        catch(Exception ex){pitchLevelFailed=true;Bootstrap.Warn("SWIM the game's camera pitch left as it is: "+ex.Message);}
    }
    private static string GamePitchText(CustomCharacterController ch)
    {
        try{float x=ch.cameraInputTargetRot.eulerAngles.x;if(x>180)x-=360;return x.ToString("F0",CultureInfo.InvariantCulture);}
        catch(Exception){return "?";}
    }
    private static string SwimAccelerationText(CustomCharacterController ch)
    {try{return ch.swimAcceleration.ToString("F1",CultureInfo.InvariantCulture);}catch(Exception){return "?";}}
    private void StartSwimSpan(float now,Vector3 at)
    {
        swimSince=now;swimFrom=at;swimPushSum=swimAskedSum=swimStrokeMax=0;swimFrames=swimAbove=swimSides=swimBelow=swimHand=0;
    }
}
