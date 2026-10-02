using System;
using PlayMagic;
using UnityEngine;
namespace XiiiXR;
// 0.1.166: at a
// ladder (the game's climb volume found, or climbing), once a second and on
// every change of the character's state, what the game was given for its
// movement and why: the stick, the mod's path, the game's own checks.
internal sealed partial class LocomotionDriver
{
    private float nextLadderReport;private int ladderState=-1;private string ladderPath="";
    partial void Ladder(CharacterControllerInputProvider provider,Vector2 given,string path)
    {
        try
        {
            // 0.1.170: never in water (the swim is not touched by the ladder check).
            if(inWater)return;
            var ch=character;if(ch==null)return;
            var st=ch.CurrentPlayerState;bool climbing=st==CustomCharacterController.PlayerStates.Climbing;
            bool volume=ch.HasClimbVolumeDetected();
            if(!climbing&&!volume){if(ladderState>=0)Bootstrap.Write("LADDER left (state "+st+")");ladderState=-1;return;}
            float now=Time.realtimeSinceStartup;int s=(int)st;
            if(s==ladderState&&path==ladderPath&&now<nextLadderReport)return;
            ladderState=s;ladderPath=path;nextLadderReport=now+1;
            float headYaw=0,bodyYaw=HeadingMath.Yaw(new System.Numerics.Quaternion(ch.transform.rotation.x,ch.transform.rotation.y,ch.transform.rotation.z,ch.transform.rotation.w),0)*180/MathF.PI;
            if(rig.TryWorldHeadRotation(out var head))headYaw=HeadingMath.Yaw(head,0)*180/MathF.PI;
            Bootstrap.Write("LADDER state="+st+" volume="+volume+" path="+path+" given=("+given.x.ToString("F2")+","+given.y.ToString("F2")+") stick=("+state.Move.X.ToString("F2")+","+state.Move.Y.ToString("F2")+")"
                +" canDirection="+provider.CanProcessInputDirection()+" facing="+ch.IsFacingTowardsClimbVolume()+" advancing="+ch.IsAdvancingTowardClimbVolume()
                +" head="+headYaw.ToString("F0")+" body="+bodyYaw.ToString("F0")+" scripted="+rig.Scripted+" locked="+GameInputManager.IsInputLocked(playerId)+"/"+GameInputManager.IsAxisLocked(playerId)
                +" handClimb="+(InteractionDriver.Current?.ClimbingActive==true)+" rope="+(GrappleVr.Current?.OnRope==true));
        }
        catch(Exception ex){if(now0<Time.realtimeSinceStartup){now0=Time.realtimeSinceStartup+30;Bootstrap.Warn("LADDER report: "+ex.Message);}}
    }
    private float now0;
}
