using System;
using System.Runtime.InteropServices;
using Valve.VR;
namespace XiiiXR;
internal sealed class SteamInput
{
    private readonly CVRInput input;
    private readonly ulong[,] handles=new ulong[2,7];
    private readonly ulong[] devices=new ulong[2];
    private readonly VRActiveActionSet_t[] sets=new VRActiveActionSet_t[1];
    private readonly uint digitalSize=(uint)Marshal.SizeOf<InputDigitalActionData_t>(),analogSize=(uint)Marshal.SizeOf<InputAnalogActionData_t>();
    private bool ready,reported;
    private static readonly ulong[] bits={HandControls.Trigger,HandControls.Grip,HandControls.A,HandControls.B,0};
    private readonly float[] nextReport=new float[2];
    internal SteamInput(IntPtr table,string path)
    {
        input=new CVRInput(table);Check(input.SetActionManifestPath(path),"manifest");
        ulong set=0;Check(input.GetActionSetHandle(SteamInputManifest.Set,ref set),"set");sets[0].ulActionSet=set;
        for(int h=0;h<2;h++)
        {
            string side=h==0?"left":"right";Check(input.GetInputSourceHandle("/user/hand/"+side,ref devices[h]),side);
            string[] names={"trigger","grip","a","b","click","stick"};
            for(int n=0;n<6;n++){ulong v=0;Check(input.GetActionHandle(SteamInputManifest.Set+"/in/"+side+"_"+names[n],ref v),names[n]);handles[h,n]=v;}
            ulong vibration=0;Check(input.GetActionHandle(SteamInputManifest.Set+"/out/"+side,ref vibration),"haptic");handles[h,6]=vibration;
        }
        Bootstrap.Write("STEAM INPUT action manifest attached; Touch buttons + sticks + real vibration actions");
    }
    private static void Check(EVRInputError error,string call){if(error!=EVRInputError.None)throw new InvalidOperationException("Steam Input "+call+": "+error);}
    internal void Update()
    {
        var error=input.UpdateActionState(sets,(uint)Marshal.SizeOf<VRActiveActionSet_t>());ready=error==EVRInputError.None;
        if(!ready&&!reported){reported=true;Bootstrap.Warn("STEAM INPUT update="+error+"; legacy fallback where available");}
    }
    internal bool Read(int hand,out ulong buttons,out StickSample stick)
    {
        buttons=0;stick=default;if(!ready)return false;
        var axis=new InputAnalogActionData_t();
        if(input.GetAnalogActionData(handles[hand,5],ref axis,analogSize,devices[hand])!=EVRInputError.None||!axis.bActive)return false;
        bool click=false;
        for(int n=0;n<5;n++)
        {
            var state=new InputDigitalActionData_t();
            if(input.GetDigitalActionData(handles[hand,n],ref state,digitalSize,devices[hand])!=EVRInputError.None||!state.bActive)return false;
            if(n==4)click=state.bState;else if(state.bState)buttons|=bits[n];
        }
        stick=new StickSample(true,new System.Numerics.Vector2(axis.x,axis.y),click);return true;
    }
    internal bool Vibrate(bool right,float amplitude,float duration)
    {
        if(!ready)return false;int h=right?1:0;
        var error=input.TriggerHapticVibrationAction(handles[h,6],0,duration,150,Math.Clamp(amplitude,0,1),devices[h]);
        if(error!=EVRInputError.None||duration>=.15f||UnityEngine.Time.realtimeSinceStartup>=nextReport[h])
        {nextReport[h]=UnityEngine.Time.realtimeSinceStartup+2;Bootstrap.Write("HAPTIC ACTION side="+(right?"R":"L")+" amplitude="+amplitude+" duration="+duration+" result="+error+"; motor response must be checked in headset");}
        return error==EVRInputError.None;
    }
}
