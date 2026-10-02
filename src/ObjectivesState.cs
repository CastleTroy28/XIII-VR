namespace XiiiXR;
internal sealed class ObjectivesState
{
    private bool armed;
    internal bool Open { get; private set; }
    internal void Sample(bool pressed,bool allowed)
    {
        if(!allowed){Close();return;}
        if(!pressed){armed=true;return;}
        if(!armed)return;
        armed=false;Open=!Open;
    }
    internal void Close(){Open=false;armed=false;}
}
