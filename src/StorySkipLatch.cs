namespace XiiiXR;
internal sealed class StorySkipLatch
{
    private bool armed;
    internal bool Sample(bool valid,bool held)
    {
        if(!valid){armed=false;return false;}
        if(!held){armed=true;return false;}
        if(!armed)return false;
        armed=false;return true;
    }
}
// 0.1.230: which playing Timeline a skip fast-forwards, and when it is done
// (Unity's DirectorWrapMode: Hold 0, Loop 1, None 2; DirectorUpdateMode:
// DSPClock 0, GameTime 1, UnscaledGameTime 2, Manual 3). A story Timeline that
// is no Cutscene (the memory's opening in the last mission) could not be skipped.
internal static class StorySkipPolicy
{
    // Plays to an end (not looping, not run by hand), with time left; not a whole level's ambience.
    internal static bool Skippable(double duration,double time,int wrap,int update)
        =>double.IsFinite(duration)&&double.IsFinite(time)&&duration>.3&&duration<900&&wrap!=1&&update!=3&&duration-time>.25;
    // A Hold Timeline stays "playing" at its last frame: played out.
    internal static bool HeldAtEnd(double duration,double time,int wrap)=>wrap==0&&double.IsFinite(duration)&&time>=duration-.02;
}
