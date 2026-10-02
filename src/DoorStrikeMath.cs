using System;
namespace XiiiXR;
internal static class DoorStrikeMath
{
    internal static bool FastContact(float inward,float dt,float gap,float stateValue,bool ready)
        =>ready&&float.IsFinite(inward)&&float.IsFinite(dt)&&float.IsFinite(gap)&&float.IsFinite(stateValue)
            &&dt>=.001f&&dt<=.075f&&inward>0&&inward<.25f&&inward/dt>=.65f&&gap>=0&&gap<=.07f;
}
