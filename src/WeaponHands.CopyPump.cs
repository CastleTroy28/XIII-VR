using System;
using UnityEngine;
namespace XiiiXR;
// 0.1.185:
// the one-hand pump (0.1.136, InertialPump) was only for the game's own
// shotgun hanging by its pump. A shotgun held as a copy (the other hand's
// weapon is the game's) is pumped the same way now: hanging by its pump in
// either hand, a jerk down and back up pumps it (the gun slides on the pump
// in the hand, its sounds and the vibration). And as the game's shotgun with
// the manual reload, it takes the next shell only once pumped after a shot.
internal sealed partial class WeaponHands
{
    private readonly InertialPump[] copyPump={new(),new()};
    private readonly bool[] copyPumping=new bool[2];
    private readonly float[] copyPumpTravel=new float[2];
    private readonly int[] copyPumpKey={-1,-1};
    private readonly bool[] copyUnpumpedReported=new bool[2];
    private static bool PumpedByHand(string p)=>p=="shotgun"&&WeaponOptions.ManualReload.Value;
    private void StopCopyPump(int s){copyPumping[s]=false;copyPumpTravel[s]=0;}
    // Every frame for a copy in hand s.
    private void TickCopyPump(int s,HolsterCopy copy,float now)
    {
        bool hanging=PumpedByHand(copyProfile[s])&&!CopyBreaks(s)&&copyForeEnd[s]&&!CopyClub(s);
        if(!hanging||copyPumpKey[s]!=copyKey[s])
        {
            if(copyPumping[s]&&copy.HasCycle)copy.LockBack(false);
            StopCopyPump(s);copyPumpKey[s]=copyKey[s];
            if(!hanging)return;
        }
        var state=CopyState(s,true);
        if(state==null||!state.NeedsRack&&!state.Pulled){if(copyPumping[s]&&copy.HasCycle)copy.LockBack(false);StopCopyPump(s);return;}
        if(!rig.SampleWorldHands(out var l,out var r,out bool leftValid)||s==0&&!leftValid){StopCopyPump(s);return;}
        var at=CameraRig.UnityPosition(s==0?l:r);var axis=copy.Rotation*Vector3.forward;
        if(!copyPumping[s])
        {
            copyPumping[s]=true;state.EndRacking();
            copyPump[s].Reset(state.Pulled?state.FullTravel:state.RackTravel);
            Bootstrap.Write("ONE-HAND PUMP shotgun (held as a copy) hangs by its pump in the "+Side(s)+" hand: jerk down and back up; travel="+state.RackTravel.ToString("F3"));
        }
        float travel=copyPump[s].Step(ToN(at),ToN(axis),now,state.FullTravel,new System.Numerics.Vector3(0,-9.81f,0));
        var action=state.InertialRack(travel);
        copyPumpTravel[s]=state.RackTravel;
        if(copy.HasCycle)copy.LockBack(state.RackTravel>state.FullTravel*.5f);
        if(action==ReloadAction.None)return;
        if(action==ReloadAction.RackBack)state.TakeSpentCase();
        if(action==ReloadAction.Chamber){copyUnpumpedReported[s]=false;if(copy.HasCycle)copy.LockBack(false);}
        reloadAudio??=new ReloadAudio();reloadAudio.Play(action,"shotgun",copy.Position,rig.HeadPosition,rig.HeadRotation);
        rig.ReloadHaptics(action,s==1);
        Bootstrap.Write("ONE-HAND PUMP shotgun (held as a copy) "+action+" by the "+Side(s)+" hand (a jerk: the gun's weight moved it along the pump)");
    }
    // The gun slides on the pump held in the hand (forward by how far the pump is back).
    private Vector3 CopyPumpShift(int s,Quaternion aim)=>copyForeEnd[s]&&copyPumping[s]?aim*Vector3.forward*copyPumpTravel[s]:Vector3.zero;
    // Before a copy's shot: a shotgun waiting to be pumped does not fire.
    private bool CopyUnpumped(int s,bool down)
    {
        if(!PumpedByHand(copyProfile[s])||CopyBreaks(s))return false;
        var state=CopyState(s,false);if(state==null||!state.NeedsRack)return false;
        if(down)
        {
            rig.ResistanceHaptics(.3f,s==1);
            if(!copyUnpumpedReported[s]){copyUnpumpedReported[s]=true;Bootstrap.Write("COPY FIRE "+Side(s)+" hand shotgun: not pumped since its shot (let it hang by its pump and jerk it, as the game's shotgun)");}
        }
        return true;
    }
    // 0.1.194: a double-barrelled shotgun is not pumped.
    private void CopyShotPumped(int s){if(PumpedByHand(copyProfile[s])&&!CopyBreaks(s))CopyState(s,true)?.OnShot();}
}
