using System;
namespace XiiiXR;
// 0.1.107: layout of the VR death screen panel (canvas units, 1 unit = 1 mm).
internal static class DeathPanelLayout
{
    internal const float Width=1100,Height=520,ButtonWidth=400,ButtonHeight=96,ButtonY=-150,ButtonX=230;
    // 0 = none, 1 = Continue (left), 2 = Quit (right).
    internal static int Hit(float x,float y)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y)||Math.Abs(y-ButtonY)>ButtonHeight*.5f)return 0;
        if(Math.Abs(x+ButtonX)<=ButtonWidth*.5f)return 1;
        if(Math.Abs(x-ButtonX)<=ButtonWidth*.5f)return 2;
        return 0;
    }
    internal static bool OnPanel(float x,float y)=>Math.Abs(x)<=Width*.5f&&Math.Abs(y)<=Height*.5f;
    // The overlay is up while the native death screen runs and the world
    // camera is off, or whenever the local player is dead and either the
    // death screen is there or the world camera is off. A running world
    // camera with a living player means gameplay is back.
    // 0.1.153: a failed level (the game's own
    // HandleLevelFailed, the player alive) with its screen up and the world
    // camera off is covered the same way.
    internal static bool Overlay(bool started,bool present,bool dead,bool worldCamera,bool failed=false)
        =>(started||present&&dead)&&(!worldCamera||dead)||dead&&!worldCamera||failed&&present&&!worldCamera;
    // 0.1.153: the
    // panel's layer must hold nothing else - every layer the game names may be
    // in use (layer 5, the game's UI, drew its own death screen over ours).
    // Unnamed first, then the emptiest; from 31 down to 8, never 0-7.
    internal static int QuietLayer(int[] used,bool[] named)
    {
        int best=-1,bestCount=int.MaxValue;
        for(int pass=0;pass<2;pass++)
            for(int i=31;i>=8;i--)
            {
                if(pass==0&&named[i])continue;
                int n=used[i];
                if(n<bestCount){best=i;bestCount=n;}
                if(pass==0&&n==0)return i;
            }
        return best<0?31:best;
    }
}
