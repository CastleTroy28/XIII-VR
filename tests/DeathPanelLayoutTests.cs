using System;using XiiiXR;
class DeathPanelLayoutTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  Check(DeathPanelLayout.Hit(-DeathPanelLayout.ButtonX,DeathPanelLayout.ButtonY)==1,"Continue centre not hit");
  Check(DeathPanelLayout.Hit(DeathPanelLayout.ButtonX,DeathPanelLayout.ButtonY)==2,"Quit centre not hit");
  Check(DeathPanelLayout.Hit(0,DeathPanelLayout.ButtonY)==0,"gap between the buttons presses one");
  Check(DeathPanelLayout.Hit(-DeathPanelLayout.ButtonX,120)==0&&DeathPanelLayout.Hit(DeathPanelLayout.ButtonX,0)==0,"title/subtitle press a button");
  Check(DeathPanelLayout.Hit(float.NaN,DeathPanelLayout.ButtonY)==0,"invalid ray presses a button");
  // Buttons lie inside the panel and do not overlap.
  Check(DeathPanelLayout.ButtonX-DeathPanelLayout.ButtonWidth*.5f>0&&DeathPanelLayout.ButtonX+DeathPanelLayout.ButtonWidth*.5f<DeathPanelLayout.Width*.5f
   &&DeathPanelLayout.ButtonY-DeathPanelLayout.ButtonHeight*.5f>-DeathPanelLayout.Height*.5f,"buttons outside the panel or overlapping");
  Check(DeathPanelLayout.OnPanel(0,0)&&!DeathPanelLayout.OnPanel(DeathPanelLayout.Width,0),"panel bounds");
  // (started, present, dead, worldCamera)
  Check(DeathPanelLayout.Overlay(true,true,false,false),"native death screen with world camera off not covered");
  Check(DeathPanelLayout.Overlay(false,true,true,true),"player dead with the death screen present not covered (native flag missed)");
  Check(DeathPanelLayout.Overlay(false,false,true,false),"player dead, world camera off, no component found: not covered");
  Check(!DeathPanelLayout.Overlay(true,true,false,true),"living player with the world camera on is covered");
  Check(!DeathPanelLayout.Overlay(false,true,false,false)&&!DeathPanelLayout.Overlay(false,false,false,false),"idle death screen component / camera off without death covers the view");
  Check(!DeathPanelLayout.Overlay(false,false,true,true),"dying animation (world still drawn, no death screen) covered");
  // 0.1.153: a failed level with the player alive (a blow to Jones).
  Check(DeathPanelLayout.Overlay(false,true,false,false,true),"failed level screen (player alive, world camera off) not covered");
  Check(!DeathPanelLayout.Overlay(false,true,false,true,true),"failed level with the world still drawn covered");
  Check(!DeathPanelLayout.Overlay(false,false,false,false,true),"failed level before its screen covered");
  // The panel's layer: unnamed and empty first, then the emptiest; never 0-7 (5 is the game's UI).
  var used=new int[32];var named=new bool[32];for(int i=0;i<32;i++)named[i]=true;
  for(int i=0;i<32;i++)used[i]=10;used[5]=0;used[17]=3;used[12]=3;
  Check(DeathPanelLayout.QuietLayer(used,named)==17,"emptiest named layer not chosen (or a layer under 8)");
  used[9]=0;Check(DeathPanelLayout.QuietLayer(used,named)==9,"empty named layer not chosen");
  named[20]=false;used[20]=0;Check(DeathPanelLayout.QuietLayer(used,named)==20,"unnamed empty layer not preferred");
  named[20]=false;used[20]=4;Check(DeathPanelLayout.QuietLayer(used,named)==9,"a used unnamed layer beat an empty named one");
  Console.WriteLine("PASS: VR death panel: Continue/Quit hit areas, no press in gaps/text/invalid rays, layout inside the panel; black overlay while the native death screen runs with the world camera off or the player is dead with the screen present / camera off; never in normal gameplay; also a failed level with the player alive; the panel on the emptiest layer 8-31, never the UI layer.");
 }
}
