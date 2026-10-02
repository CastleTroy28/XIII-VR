namespace XiiiXR;
internal readonly record struct UiPointerStep(bool Down,bool Up,bool Click);
internal sealed class UiPointerState
{
    private bool armed,pressed;
    private int target;
    internal UiPointerStep Sample(bool valid,bool held,int hovered,bool dragging=false)
    {
        if(!valid){bool up=pressed;armed=pressed=false;target=0;return new(false,up,false);}
        if(!held)
        {
            bool up=pressed,click=pressed&&target!=0&&target==hovered&&!dragging;
            armed=true;pressed=false;target=0;return new(false,up,click);
        }
        if(!armed)return default;
        armed=false;target=hovered;pressed=hovered!=0;return new(pressed,false,false);
    }
}
