using System;using System.Linq;using System.Numerics;using XiiiXR;
class QualityMenuTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Tick(float x=0,float y=0,bool trigger=false)=>QualityMenu.Tick(true,new StickSample(true,new Vector2(x,y),false),new HandControls(true,trigger?HandControls.Trigger:0,0,0));
 static void Main()
 {
  LocomotionOptions.Load(new BepInEx.Configuration.ConfigFile());
  EnemyOptions.Load(new BepInEx.Configuration.ConfigFile());
  {
   // 0.1.116: a crash guard of 0.1.115 may have switched smarter enemies off; the rebuilt feature is switched back on once.
   var guarded=new BepInEx.Configuration.ConfigFile();guarded.Bind("VR","SmarterEnemies",false,"");
   EnemyOptions.Load(guarded);Check(EnemyOptions.Smarter.Value&&EnemyOptions.Reenabled&&EnemyOptions.Revision.Value==EnemyOptions.CurrentRevision,"smarter enemies not switched back on for the rebuilt version");
   EnemyOptions.Smarter.Value=false;EnemyOptions.Load(guarded);Check(!EnemyOptions.Smarter.Value&&!EnemyOptions.Reenabled,"smarter enemies switched back on again after the player turned them off");
   EnemyOptions.Load(new BepInEx.Configuration.ConfigFile());Check(EnemyOptions.Smarter.Value&&!EnemyOptions.Reenabled,"fresh config: smarter enemies not on by default or reported as re-enabled");
   Console.WriteLine("PASS: smarter enemies switched back on once for the rebuilt version, never against the player's later choice.");
  }
  Check(!QualityMenu.StartupReady(),"starts before a game camera exists");
  UnityEngine.Camera.allCameras=new[]{new UnityEngine.Camera()};Check(QualityMenu.StartupReady(),"frontend camera does not allow auto-start without player root");
  var oldConfig=new BepInEx.Configuration.ConfigFile();oldConfig.Bind("VR","AutoStartVR",false,"");oldConfig.Bind("VR","RenderScale",.95f,"");
  QualityOptions.Load(oldConfig);Check(QualityOptions.AutoStart.Value&&QualityOptions.RenderScale.Value==.95f,"old config did not enable auto-start or lost resolution");
  QualityOptions.AutoStart.Value=false;QualityOptions.Load(oldConfig);Check(!QualityOptions.AutoStart.Value,"later explicit auto-start opt-out overwritten on each launch");
  QualityOptions.Load(new BepInEx.Configuration.ConfigFile());Check(QualityOptions.AutoStart.Value,"fresh default does not auto-start");
  // 0.1.182: 100% by default; the old default 75% is moved to 100% once, a later own choice of 75% stays.
  Check(QualityOptions.RenderScale.Value==1f,"fresh resolution is not 100%");
  var oldDefault=new BepInEx.Configuration.ConfigFile();oldDefault.Bind("VR","RenderScale",.75f,"");
  QualityOptions.Load(oldDefault);Check(QualityOptions.RenderScale.Value==1f,"the old 75% default was not moved to 100%");
  QualityOptions.RenderScale.Value=.75f;QualityOptions.Load(oldDefault);Check(QualityOptions.RenderScale.Value==.75f,"a later own 75% was moved to 100% again");
  QualityOptions.Load(oldConfig);Check(QualityOptions.RenderScale.Value==.95f,"an own resolution (95%) was changed");
  // 0.1.239: an old config's world scale (it never changed anything before 0.1.238) is moved to 120% once; a later own choice stays.
  var oldScale=new BepInEx.Configuration.ConfigFile();oldScale.Bind("VR","WorldScale",.8f,"");
  QualityOptions.Load(oldScale);Check(MathF.Abs(QualityOptions.WorldScaleValue-1.2f)<1e-4f,"an old world scale not moved to 120%");
  QualityOptions.WorldScale!.Value=.9f;QualityOptions.Load(oldScale);Check(MathF.Abs(QualityOptions.WorldScaleValue-.9f)<1e-4f,"a later own world scale moved to 120% again");
  QualityOptions.Load(new BepInEx.Configuration.ConfigFile());
  var display=new UnityEngine.XR.XRDisplaySubsystem();QualityMenu.Attach(display);
  Check(display.Scale==1f&&display.Sets==1,"fresh scale not applied once (100% by default since 0.1.182)");
  for(int i=0;i<100;i++)QualityMenu.UpdateDisplay(display);Check(display.Sets==1,"reallocates XR targets every frame");
  QualityMenu.Chord(true,true,true);Check(!QualityMenu.Open,"startup held chord opens menu");QualityMenu.Chord(true,false,true);QualityMenu.Chord(true,true,true);Check(QualityMenu.Open,"quality chord did not open");
  Tick();Tick(1);QualityMenu.UpdateDisplay(display);Check(display.Scale==1f,"unconfirmed slider reallocates textures");
  var channels=new RightControlChannels();
  // WeaponHands disarms shooting on EVERY menu frame. That must not consume UI input.
  for(int i=0;i<20;i++){channels.Sample(true,0);QualityMenu.Tick(true,new StickSample(true,Vector2.Zero,false),channels.Menu);channels.DisarmGameplay();}
  channels.Sample(true,HandControls.Trigger);QualityMenu.Tick(true,new StickSample(true,Vector2.Zero,false),channels.Menu);channels.DisarmGameplay();
  Check((channels.Gameplay.Held&HandControls.Trigger)==0,"menu apply fires gun");
  QualityMenu.UpdateDisplay(display);Check(display.Scale==1.1f&&QualityOptions.RenderScale.Value==1.1f,"confirmed resolution not applied/saved");
  Check(!QualityMenu.Open&&QualityMenu.AppliedRevision==1,"apply leaves locomotion locked in quality menu");
  Check(QualityMenu.Text.Contains("Применено:"),"new target dimensions not confirmed");
  channels.Sample(true,HandControls.Trigger);Check((channels.Gameplay.Held&HandControls.Trigger)==0,"held trigger fires on menu close");
  channels.Sample(true,0);channels.Sample(true,HandControls.Trigger);Check((channels.Gameplay.Down&HandControls.Trigger)!=0,"trigger cannot rearm after release");
  Tick(trigger:true);QualityMenu.UpdateDisplay(display);Check(display.Sets==2,"held trigger repeats apply");
  QualityMenu.Toggle();Tick();Tick(y:-1);Tick();Tick(trigger:true);Check(!QualityOptions.Collisions.Value,"collision switch does not toggle");
  Tick();Tick(y:-1);Tick();Tick(1);Check(LocomotionOptions.Teleport.Value,"teleport option did not change");Tick();Tick(trigger:true);Check(LocomotionOptions.Teleport.Value,"confirm changed teleport back to slide");
  Tick();Tick(y:-1);Tick();Tick(1);Check(LocomotionOptions.SnapTurn.Value,"snap option did not change");
  Tick();Tick(y:-1);Tick();Tick(1);Check(LocomotionOptions.SnapAngle.Value==45,"snap angle not adjustable");
  Tick();Tick(y:1);Tick();Tick(-1);Tick();Tick(y:-1);Tick();Tick(1);Check(LocomotionOptions.TurnSpeed.Value==90,"smooth speed not adjustable");
  Check(LocomotionOptions.SnapAngle.Value==45,"speed overwrote independent snap angle");
  QualityMenu.Chord(false,false,false);Check(!QualityMenu.Open,"tracking loss leaves menu open");
  QualityMenu.Toggle();Tick();Tick(-1);Tick();display.Fail=true;Tick(trigger:true);QualityMenu.UpdateDisplay(display);Check(QualityOptions.RenderScale.Value==1.1f&&display.Scale==1.1f,"failed resolution applies corrupt config/targets");
  Check(QualityMenu.Clamp(float.NaN)==1f&&QualityMenu.Clamp(10)==1.5f&&QualityMenu.Clamp(-1)==.5f,"quality bounds invalid");
  display.Fail=false;display.IgnoreResize=true;QualityMenu.Close();QualityMenu.Toggle();Tick();Tick(1);QualityMenu.Apply();QualityMenu.UpdateDisplay(display);
  UnityEngine.Time.realtimeSinceStartup+=6;QualityMenu.UpdateDisplay(display);
  Check(QualityMenu.Text.Contains("Размер не подтверждён"),"non-resizing provider falsely reports success");
  QualityMenu.Close();QualityMenu.Toggle();Tick();
  for(int n=0;n<5;n++){Tick(y:-1);Tick();}
  bool manual=true;UiLanguage.ReadManual=()=>manual;UiLanguage.WriteManual=v=>manual=v;
  Tick(trigger:true);Check(!manual,"manual reload setting cannot be disabled");Tick();Tick(1);Check(manual,"manual reload setting cannot be restored");Tick();Tick(y:-1);Tick();
  UiLanguage.ReadCode=()=>"en-US";Check(QualityMenu.Text.Contains("Manual reload:"),"settings do not follow game language");
  UiLanguage.ReadCode=()=>"fr";Check(QualityMenu.Text.Contains("Rechargement manuel"),"French settings missing");UiLanguage.ReadCode=()=>"ru";
  foreach(var expectedAction in new[]{"recenter","reset","calibrate","haptics"})
  {Tick(trigger:true);Check(QualityMenu.TakeAction()==expectedAction,"wrong recovery action");Tick(trigger:true);Check(QualityMenu.TakeAction()=="","held trigger repeated recovery");Tick();Tick(y:-1);Tick();}
  Tick(trigger:true);Check(!QualityOptions.Hints.Value,"hints toggle not saved");Tick();Tick(1);Check(QualityOptions.Hints.Value,"hints cannot be restored");Tick();Tick(y:-1);Tick();
  Check(!QualityOptions.ThrowArc.Value&&QualityMenu.Text.Contains("Броски: жест"),"throw gesture is not the default");
  Tick(trigger:true);Check(QualityOptions.ThrowArc.Value,"throw arc option not saved");Tick();Tick(-1);Check(!QualityOptions.ThrowArc.Value,"throw gesture cannot be restored");Tick();Tick(y:-1);Tick();
  // 0.1.224: no smarter enemies row; the setting itself stays on (enemies unchanged).
  Check(EnemyOptions.Smarter.Value&&!QualityMenu.Text.Contains("Умные враги")&&!QualityMenu.Text.Contains("Smarter enemies"),"smarter enemies row still in the menu or the setting off");
  // 0.1.120: stationary machine gun steering row (handles/inverted by default).
  Check(QualityOptions.MountedHandles!.Value&&QualityMenu.Text.Contains("> Стационарный пулемёт: рукоятки (инверсия)"),"mounted gun row missing or not handles by default");
  Tick(trigger:true);Check(!QualityOptions.MountedHandles.Value&&QualityMenu.Text.Contains("Стационарный пулемёт: по направлению рук"),"mounted gun pointing cannot be chosen");
  Tick();Tick(1);Check(QualityOptions.MountedHandles.Value,"mounted gun handles cannot be restored");
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Stationäres MG: Griffe (invertiert)"),"mounted gun row not translated");UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  // 0.1.124: weapon in the hand while the grip is held (default), toggled by grip presses, or always.
  Check(QualityOptions.WeaponGrip!.Value==0&&QualityMenu.Text.Contains("> Оружие в руке: пока держишь грипп"),"weapon grip row missing or not hold by default");
  Tick(trigger:true);Check(QualityOptions.WeaponGrip.Value==1&&QualityMenu.Text.Contains("Оружие в руке: нажатие гриппа (взять/отпустить)"),"toggle grip cannot be chosen");
  Tick();Tick(1);Check(QualityOptions.WeaponGrip.Value==2&&QualityMenu.Text.Contains("Оружие в руке: всегда"),"always in hand cannot be chosen");
  Tick();Tick(1);Check(QualityOptions.WeaponGrip.Value==0,"grip modes do not wrap back to hold");
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Waffe in der Hand: Grip halten"),"weapon grip row not translated");UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  Check(QualityOptions.LeftHanded!.Value==false&&QualityMenu.Text.Contains("> Ведущая рука: правша"),"handedness row missing or not right-handed by default");
  Tick(trigger:true);Check(QualityOptions.LeftHanded.Value&&QualityMenu.Text.Contains("Ведущая рука: левша"),"left-handed cannot be chosen");
  Tick();Tick(1);Check(!QualityOptions.LeftHanded.Value,"right-handed cannot be restored");
  // 0.1.146: the pistol places on the belt and under the arms, each on/off.
  Tick();Tick(y:-1);Tick();
  Check(QualityOptions.BeltPistols!.Value&&QualityMenu.Text.Contains("> Пистолеты на поясе: "),"belt pistols row missing or off by default");
  Tick(trigger:true);Check(!QualityOptions.BeltPistols.Value&&!HolsterLayout.Candidates("pistol").Contains(HolsterSlot.BeltRight),"belt pistol places cannot be switched off");
  Tick();Tick(1);Check(QualityOptions.BeltPistols.Value&&HolsterLayout.Candidates("pistol").Contains(HolsterSlot.BeltRight),"belt pistol places cannot be switched on");
  Tick();Tick(y:-1);Tick();
  Check(QualityOptions.ArmpitPistols!.Value&&QualityMenu.Text.Contains("> Пистолеты под мышками: "),"armpit pistols row missing");
  Tick(trigger:true);Check(!QualityOptions.ArmpitPistols.Value&&!HolsterLayout.Candidates("uzi").Contains(HolsterSlot.ArmpitLeft),"armpit pistol places cannot be switched off");
  Tick();Tick(1);Check(QualityOptions.ArmpitPistols.Value,"armpit pistol places cannot be switched on");
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Pistolen am Gürtel")&&QualityMenu.Text.Contains("Pistolen unter den Armen"),"pistol place rows not translated");UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  // 0.1.160: a two-handed gun in one hand turned toward the other hand (a gun stock): 15 degrees by default, 0-45 in steps of 5.
  Check(QualityMenu.GunTurnDegrees==15&&QualityMenu.Text.Contains("> Двуручное в одной руке: 15° к другой руке")&&QualityMenu.Adjustable(QualityMenu.GunTurnRow),"gun turn row missing, not 15 by default or without arrows");
  Tick(1);Check(QualityMenu.GunTurnDegrees==20,"gun turn cannot be raised");
  for(int n=0;n<6;n++){Tick();Tick(-1);}
  Check(QualityMenu.GunTurnDegrees==0&&QualityMenu.Text.Contains("Двуручное в одной руке: прямо"),"gun turn cannot be set straight or went below 0");
  for(int n=0;n<12;n++){Tick();Tick(1);}
  Check(QualityMenu.GunTurnDegrees==45,"gun turn not held at 45");
  QualityMenu.Click(QualityMenu.GunTurnRow,-1);Check(QualityMenu.GunTurnDegrees==40,"ray arrow does not lower the gun turn");
  QualityMenu.Click(QualityMenu.GunTurnRow,0);Check(QualityMenu.GunTurnDegrees==45,"ray click on the row does not step the gun turn");
  QualityMenu.Click(QualityMenu.GunTurnRow,0);Check(QualityMenu.GunTurnDegrees==0,"ray click does not wrap the gun turn to straight");
  QualityOptions.GunTurn!.Value=15;
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Zweihandwaffe in einer Hand: 15° zur anderen Hand"),"gun turn row not translated");UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  // 0.1.233: the world scale (100% by default, 50-150% in steps of 5) and the weapons' inertia (100%, off to 200% in steps of 25).
  // 0.1.239: 120% by default.
  Check(MathF.Abs(QualityOptions.WorldScaleValue-1.2f)<1e-4f&&QualityMenu.Text.Contains("> Масштаб мира: 120%")&&QualityMenu.Adjustable(QualityMenu.WorldScaleRow),"world scale row missing, not 120% by default or without arrows");
  Tick(-1);Check(MathF.Abs(QualityOptions.WorldScaleValue-1.15f)<1e-4f&&QualityMenu.Text.Contains("Масштаб мира: 115%"),"world scale cannot be lowered");
  for(int n=0;n<15;n++){Tick();Tick(-1);}
  Check(MathF.Abs(QualityOptions.WorldScaleValue-.5f)<1e-4f,"world scale not held at 50%");
  QualityMenu.Click(QualityMenu.WorldScaleRow,1);Check(MathF.Abs(QualityOptions.WorldScaleValue-.55f)<1e-4f,"ray arrow does not raise the world scale");
  QualityOptions.WorldScale!.Value=1.5f;QualityMenu.Click(QualityMenu.WorldScaleRow,0);Check(MathF.Abs(QualityOptions.WorldScaleValue-.5f)<1e-4f,"ray click does not wrap the world scale");
  QualityOptions.WorldScale.Value=7f;Check(QualityOptions.WorldScaleValue==1.5f,"a world scale outside 50-150% used");
  QualityOptions.WorldScale.Value=1f;
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Weltgröße: 100%"),"world scale row not translated");UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  QualityOptions.WeaponInertia=new BepInEx.Configuration.ConfigEntry<float>(1f);
  Check(QualityMenu.Text.Contains("> Инерция оружия: 100%")&&QualityMenu.Adjustable(QualityMenu.InertiaRow),"weapon inertia row missing, not 100% or without arrows");
  for(int n=0;n<4;n++){Tick(-1);Tick();}
  Check(QualityOptions.WeaponInertia.Value==0&&QualityMenu.Text.Contains("Инерция оружия: выкл"),"weapon inertia cannot be switched off");
  for(int n=0;n<9;n++){Tick(1);Tick();}
  Check(QualityOptions.WeaponInertia.Value==2f&&QualityMenu.Text.Contains("Инерция оружия: 200%"),"weapon inertia not held at 200%");
  QualityMenu.Click(QualityMenu.InertiaRow,0);Check(QualityOptions.WeaponInertia.Value==0,"ray click does not wrap the weapon inertia to off");
  QualityOptions.WeaponInertia.Value=1f;
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Waffenträgheit: 100%"),"weapon inertia row not translated");UiLanguage.ReadCode=()=>"ru";
  Console.WriteLine("PASS: 0.1.233 world scale (50-150%, 120% by default since 0.1.239, moved there once) and weapon inertia (off-200%, 100% by default) rows in VR SETTINGS, with arrows, translated.");
  Tick();Tick(y:-1);Tick();
  // 0.1.234: the cutscene camera (steady by default) and the weapon places editor.
  Check(QualityOptions.CutsceneMode==0&&QualityMenu.Row==QualityMenu.CutsceneRow&&QualityMenu.Text.Contains("> Камера в роликах: экран (стоит на месте)")&&!QualityMenu.Adjustable(QualityMenu.CutsceneRow),"cutscene camera row missing or not the still screen by default");
  Tick(trigger:true);Check(QualityOptions.CutsceneMode==1&&QualityMenu.Text.Contains("Камера в роликах: устойчивая (смотрите сами)"),"the steady cutscene camera cannot be chosen");
  Tick();Tick(1);Check(QualityOptions.CutsceneMode==2&&QualityMenu.Text.Contains("Камера в роликах: как в фильме (поворачивает взгляд)"),"the film's cutscene camera cannot be chosen");
  Tick();Tick(1);Check(QualityOptions.CutsceneMode==0,"the cutscene camera does not wrap back to the still screen");
  Tick();Tick(-1);Check(QualityOptions.CutsceneMode==2,"the stick left does not step the cutscene camera back");QualityOptions.CutsceneCamera!.Value=1;
  UiLanguage.ReadCode=()=>"de";Check(QualityMenu.Text.Contains("Kamera in Zwischensequenzen: ruhig"),"cutscene camera row not translated");QualityOptions.CutsceneCamera!.Value=0;UiLanguage.ReadCode=()=>"ru";
  Tick();Tick(y:-1);Tick();
  Check(QualityMenu.Row==QualityMenu.PlacesRow&&QualityMenu.Text.Contains("> Места оружия: двигать лучом")&&!QualityMenu.Adjustable(QualityMenu.PlacesRow),"weapon places row missing");
  Tick(1);Tick();Check(!QualityMenu.EditingPlaces&&QualityMenu.Row==QualityMenu.PlacesRow,"the stick sideways opened the places editor or left the row");
  Tick(trigger:true);Check(QualityMenu.EditingPlaces&&QualityMenu.Open,"the places editor does not open");
  // While it is open the stick and the trigger belong to it.
  Tick();Tick(y:-1);Tick();Tick(trigger:true);Tick();Check(QualityMenu.EditingPlaces&&QualityMenu.Row==QualityMenu.PlacesRow&&QualityMenu.TakeAction()=="","the rows moved or confirmed under the places editor");
  HolsterPlaces.Set(HolsterSlot.Belly,new Vector3(0,.05f,0));Check(QualityMenu.Text.Contains("Места оружия: двигать лучом (1 сдвинуто)"),"moved places not counted on the row");HolsterPlaces.Set(HolsterSlot.Belly,Vector3.Zero);
  QualityMenu.EndPlaces();Check(!QualityMenu.EditingPlaces&&QualityMenu.Open&&QualityMenu.Row==QualityMenu.PlacesRow,"back from the places editor not on its row");
  Tick(trigger:true);Tick();Check(!QualityMenu.EditingPlaces,"the trigger held from the editor reopened it");
  QualityMenu.Click(QualityMenu.PlacesRow,1);Check(QualityMenu.EditingPlaces,"a ray click on the places row does not open the editor");
  QualityMenu.Close();Check(!QualityMenu.EditingPlaces,"closing the settings leaves the places editor open");
  QualityMenu.Show();Check(!QualityMenu.EditingPlaces,"the settings reopen in the places editor");
  for(int n=0;n<QualityMenu.PlacesRow;n++){Tick();Tick(y:-1);}Tick();
  Check(QualityMenu.Row==QualityMenu.PlacesRow,"rows not reached again");
  UiLanguage.ReadCode=()=>"pl";Check(QualityMenu.Text.Contains("Miejsca broni: przesuń promieniem"),"weapon places row not translated");UiLanguage.ReadCode=()=>"ru";
  Console.WriteLine("PASS: 0.1.234 cutscene camera row (a still screen by default, steady, the film's) and weapon places row opening its editor, which has the controllers until back; translated.");
  // 0.1.245: the forearms shown (the default) or the hands only, cut at the watch.
  Tick();Tick(y:-1);Tick();
  Check(QualityMenu.Row==QualityMenu.ForearmRow&&QualityOptions.ForearmsOn&&QualityMenu.Text.Contains("> Предплечья: видны"),"forearms row missing or not shown by default");
  Tick(-1);Tick();Check(!QualityOptions.ForearmsOn&&QualityOptions.Forearms!.Value==false&&QualityMenu.Text.Contains("> Предплечья: только кисти (обрезаны у часов)"),"the hands only cannot be chosen with the stick");
  QualityMenu.Click(QualityMenu.ForearmRow,0);Check(QualityOptions.ForearmsOn,"a ray click on the row does not switch the forearms back");
  QualityMenu.Click(QualityMenu.ForearmRow,-1);Check(!QualityOptions.ForearmsOn,"the ray's left arrow does not choose the hands only");
  UiLanguage.ReadCode=()=>"fr";Check(QualityMenu.Text.Contains("Avant-bras: mains seules (coupées à la montre)"),"forearms row not translated");UiLanguage.ReadCode=()=>"ru";
  QualityMenu.Click(QualityMenu.ForearmRow,1);Check(QualityOptions.ForearmsOn&&QualityMenu.Row==QualityMenu.ForearmRow&&QualityMenu.ForearmRow==QualityMenu.RowCount-2,"the forearms not back, or the row not just above Close");
  Console.WriteLine("PASS: 0.1.245 forearms row (shown by default, or the hands only cut at the watch): the stick, a ray click and the ray's arrows; translated.");
  Tick();Tick(y:-1);Tick();
  Check(!QualityMenu.Text.Contains("Кошка"),"grapple grip rows still in the menu");
  QualityMenu.Tick(true,new StickSample(true,Vector2.Zero,false),new HandControls(true,HandControls.A,0,0));Check(!QualityMenu.Open,"A cannot confirm close in settings");
  // 0.1.101: controller-ray rows of the VR settings page in the game menu.
  QualityMenu.Show();Tick();bool collisions=QualityOptions.Collisions.Value;
  QualityMenu.Click(1,0);Check(QualityOptions.Collisions.Value!=collisions&&QualityMenu.Row==1,"ray click does not toggle collisions");
  QualityMenu.Click(1,0);Check(QualityOptions.Collisions.Value==collisions,"second ray click does not toggle back");
  QualityMenu.Click(0,1);Check(QualityMenu.Text.Contains("> Разрешение: "+System.MathF.Round((QualityOptions.RenderScale.Value+.1f)*100)+"%"),"right arrow does not raise resolution");
  QualityMenu.PointerOwnsTrigger=true;Tick(trigger:true);Check(QualityMenu.Open&&QualityMenu.TakeAction()=="","trigger on the ray also confirmed the row");
  QualityMenu.PointerOwnsTrigger=false;Tick(trigger:true);Check(QualityMenu.Open,"trigger held from the ray confirmed after leaving the page");Tick();
  QualityMenu.Hover(6);QualityMenu.Click(6,0);Check(QualityMenu.TakeAction()=="recenter","ray click on recenter");
  QualityMenu.Click(QualityMenu.RowCount-1,0);Check(!QualityMenu.Open,"ray click on Close");
  Console.WriteLine("PASS: quality modifier rearm; deliberate scale apply; no per-frame reallocation; persisted scale; held trigger; collision toggle; tracking-loss close; allocation failure rollback; bounds.");
  Console.WriteLine("Mock display. XR render-target reallocations and menu readability still need the game/headset.");
 }
}
namespace BepInEx.Configuration
{
 internal sealed class ConfigEntry<T>{internal T Value;internal ConfigEntry(T v){Value=v;}}
 internal sealed class ConfigFile{readonly System.Collections.Generic.Dictionary<string,object> items=new();internal ConfigEntry<T> Bind<T>(string a,string b,T v,string d){string key=a+"/"+b;if(!items.ContainsKey(key))items[key]=new ConfigEntry<T>(v);return (ConfigEntry<T>)items[key];}}
}
namespace UnityEngine
{
 internal sealed class Transform{internal string name="";internal Transform? parent {get;set;}}
 internal sealed class Camera{internal Transform transform=new();internal static Camera[] allCameras=Array.Empty<Camera>();}
 internal static class Time{internal static float realtimeSinceStartup=1;}
 internal sealed class RenderTexture{internal int width,height;}
}
namespace UnityEngine.XR
{
 internal sealed class XRDisplaySubsystem
 {internal float Scale,ReportedScale;internal int Sets;internal bool Fail,IgnoreResize;
 internal float scaleOfAllRenderTargets{get=>Scale;set{Sets++;if(Fail&&value!=Scale)throw new Exception("allocation test");Scale=value;if(!IgnoreResize)ReportedScale=value;}}
 internal UnityEngine.RenderTexture GetRenderTextureForRenderPass(int p)=>new(){width=(int)(4000*ReportedScale),height=(int)(3800*ReportedScale)};}
}
namespace XiiiXR{internal static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s){}}}
