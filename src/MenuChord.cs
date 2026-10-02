namespace XiiiXR;
// 0.1.158: the menu chord (the grip and A/X of the menu hand) in either
// order: the grip first and then the button (as before), or the button first
// and the grip within Window seconds. Edges are its own, from
// what is held.
internal sealed class MenuChord
{
    internal const float Window=.5f;
    private bool armed,both,buttonWas;private float buttonAt=-10;
    // True on the frame the chord is made; late: the grip came after the button.
    internal bool Sample(bool usable,bool grip,bool button,float now,out bool late)
    {
        late=false;
        bool pressed=button&&!buttonWas;buttonWas=button;
        if(pressed)buttonAt=now;
        bool was=both;both=grip&&button;
        if(!usable){armed=false;return false;}
        if(!button){armed=true;return false;}
        if(!armed||!both||was)return false;
        late=!pressed;
        if(late&&now-buttonAt>Window)return false;
        armed=false;return true;
    }
    internal void Reset(){armed=false;both=false;buttonWas=false;buttonAt=-10;}
}
