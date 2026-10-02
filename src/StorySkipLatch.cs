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
