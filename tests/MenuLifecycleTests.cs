// Runs real GameUiControls.cs against simulated game/window-facing objects.
// Linux deliberately does not execute SendInput. Native game UI is not simulated.
using System;
using System.Collections.Generic;
using System.Reflection;
using XiiiXR;
using UnityEngine;
class MenuLifecycleTests
{
    static void Check(bool b,string text) { if(!b) throw new Exception(text); }
    static CameraRig rig=null!;static GameUiControls ui=null!;static InventoryWheel wheel=null!;
    static PlayerHUDControl MakeHud(Transform root)
    { return new PlayerHUDControl {inventory=new PlayerEquipableInventory {transform=root},inventoryWheel=new InventoryWheel()}; }
    static void Setup(bool startHeld=false)
    {
        ui?.Dispose();QualityMenu.Close();LocomotionOptions.Load(new BepInEx.Configuration.ConfigFile());QualityOptions.Load(new BepInEx.Configuration.ConfigFile());Application.isFocused=true;Time.realtimeSinceStartup=1;PauseMenuControl.HackGameIsPaused=false;GameInputManager.Locked=false;
        rig=new CameraRig {PlayerRoot=new Transform(),HeadTrackingValid=true};
        var hud=MakeHud(rig.PlayerRoot);Resources.Items=new UnityEngine.Object[]{hud};wheel=hud.inventoryWheel;
        ui=new GameUiControls(rig);Step(startHeld,false,0,0);
    }
    static void Step(bool left,bool right,float x=0,float y=0,bool valid=true,float dt=.02f,bool r3=false,ulong extra=0,ulong rightExtra=0)
    {
        Time.realtimeSinceStartup+=dt;
        rig.LeftControls=new HandControls(valid,(right?(HandControls.A|HandControls.Grip):0)|extra,0,0);
        rig.RightControls=new HandControls(valid,(left?HandControls.A:0)|rightExtra,0,0);
        rig.LeftStick=new StickSample(valid,new System.Numerics.Vector2(x,y),false);rig.RightStick=new StickSample(valid,System.Numerics.Vector2.Zero,r3);ui.Tick();
    }
    static void Main()
    {
        Setup();Step(true,false,rightExtra:HandControls.Grip);Check(!ui.WheelOpen,"grip+A opened wheel");
        Step(true,false);Check(!ui.WheelOpen,"releasing grip with A held opens wheel");
        Step(false,false);Step(true,false);Check(ui.WheelOpen,"bare A cannot rearm wheel");
        Step(true,false,rightExtra:HandControls.Grip);Check(!ui.WheelOpen&&wheel.Equips==0,"adding grip commits wheel instead of cancelling");
        // 0.1.124: the grip on a chosen weapon takes it into the hand (held by the grip).
        Setup();Step(false,false);Step(true,false,1,0);Check(ui.WheelOpen,"wheel not open for the grip test");
        Step(true,false,1,0,rightExtra:HandControls.Grip);Check(!ui.WheelOpen&&wheel.Equips==1&&WeaponHands.Current!.Taken==1,"grip on a chosen weapon does not take it into the hand");
        Step(true,false,1,0,rightExtra:HandControls.Grip);Check(wheel.Equips==1&&WeaponHands.Current!.Taken==1,"held grip takes again");
        Setup(true);Step(true,false);Check(wheel.OpenCount==0,"held right A opens on startup");
        Step(false,false);Step(true,false,1,0);Check(wheel.OpenCount==1 && ui.BlocksGameplay,"right A failed to open/block locomotion");
        for(int i=0;i<20;i++)Step(true,false,1,0);
        Check(wheel.OpenCount==1 && wheel.HoverCount>0 && wheel.Equips==0,"holding wheel equips prematurely");
        Step(false,false);Check(wheel.Equips==1 && wheel.CloseCount==1 && !ui.WheelOpen,"release does not confirm exactly once");
        Check(wheel.deselectOnZeroCursorOffset,"native wheel setting not restored");
        Step(false,false);Check(wheel.Equips==1,"idle repeats selection");
        Setup();Step(true,false);Step(false,false);Check(wheel.Equips==0,"neutral stick must cancel without selection");
        Setup();Step(true,false,0,1);Step(true,true,0,1);Check(wheel.CloseCount==1 && wheel.Equips==0,"grip+X must cancel owned wheel");
        Step(true,false);Check(wheel.OpenCount==1,"cancel reopens while right A held");
        Step(false,false);Step(true,false);Check(wheel.OpenCount==2,"release does not rearm after cancel");
        Application.isFocused=false;Step(true,false);Check(!ui.WheelOpen && wheel.Equips==0,"focus loss selects or leaves owned wheel open");
        Application.isFocused=true;Step(true,false);Check(wheel.OpenCount==2,"focus regain reopens held A");
        Step(false,false);Step(true,false);Step(true,false,0,0,false);Check(!ui.WheelOpen,"controller loss leaves wheel open");
        Setup();Step(true,false,1,0);rig.Scripted=true;Step(true,false);Check(!ui.WheelOpen && wheel.Equips==0,"cinematic transition selects weapon");
        Setup();Step(true,false,1,0);var old=wheel;
        rig.PlayerRoot=new Transform();var next=MakeHud(rig.PlayerRoot);Resources.Items=new UnityEngine.Object[]{next};wheel=next.inventoryWheel;
        Step(true,false,0,0,true,1.1f);Check(old.CloseCount==1 && old.Equips==0 && wheel.OpenCount==0,"chapter restart retains old wheel or held action");
        Step(false,false);Step(true,false,-1,0);Step(false,false);Check(wheel.Equips==1,"new chapter cannot use wheel");
        Setup();Step(true,false);rig.RightStick=new StickSample(true,System.Numerics.Vector2.UnitX,false);ui.Tick();
        Check(wheel.HoverCount==0,"right stick still selects weapons");
        rig.LeftStick=new StickSample(true,System.Numerics.Vector2.UnitX,false);ui.Tick();Check(wheel.HoverCount>0,"left stick cannot select");
        Setup();wheel.OpenWheel(false);Application.isFocused=false;Step(false,false);Check(wheel.IsOpen,"VR driver closed a wheel it did not own");
        // 0.1.210: the game's weapon wheel tutorial opens the wheel itself and locks the controls until it closes.
        Setup();Step(false,false);wheel.OpenWheel(false);Step(false,false);Check(wheel.IsOpen&&GameInputManager.Locked,"the game's wheel closed while nobody held A");
        Step(true,false,1,0);Check(ui.WheelOpen&&ui.BlocksGameplay&&wheel.HoverCount>0&&wheel.OpenCount==1,"holding A does not take over the wheel the game opened (the left stick cannot choose)");
        Step(false,false);Check(!wheel.IsOpen&&wheel.Equips==1&&wheel.CloseCount==1&&!GameInputManager.Locked,"letting go of A does not choose in the game's wheel (the tutorial stays stuck)");
        Check(wheel.deselectOnZeroCursorOffset,"native wheel setting not restored after the game's wheel");
        // 0.1.213: the menu chord types Escape first (the game answers it and ends its tutorial); only when it cannot be typed is the game's wheel closed.
        Setup();EscapeKey.Downs=0;Step(false,false);wheel.OpenWheel(false);Step(false,true);
        Check(EscapeKey.Downs==1&&wheel.IsOpen,"the menu chord does not type Escape for the wheel the game opened");
        Setup();EscapeKey.Downs=0;EscapeKey.Accept=false;GamePause.Opens=0;Step(false,false);wheel.OpenWheel(false);Step(false,true);
        Check(!wheel.IsOpen&&wheel.Equips==0&&!GameInputManager.Locked&&GamePause.Opens==0,"with Escape refused the menu chord does not close the wheel the game opened");EscapeKey.Accept=true;
        // 0.1.214: right A ends the game's weapon wheel tutorial (its hint names right A); the game closes its wheel.
        Setup();WheelTutorial.Ends=0;Step(false,false);WheelTutorial.Forced=true;wheel.OpenWheel(false);Step(false,false);Check(WheelTutorial.Ends==0,"the tutorial ended without right A");
        Step(true,false,1,0);Check(WheelTutorial.Ends==1&&wheel.HoverCount==0&&wheel.CloseCount==0,"right A does not end the weapon wheel tutorial, or the mod takes its wheel over");
        Check(WheelTutorial.LastOpen,"the tutorial's open wheel not reported to its end");
        wheel.CloseWheel(true,false);Step(true,false,1,0);Step(true,false,1,0);Check(wheel.OpenCount==1&&WheelTutorial.Ends==1,"the A that ended the tutorial opens the wheel while still held");
        Step(false,false);Step(true,false,1,0);Check(wheel.OpenCount==2&&ui.WheelOpen,"after the tutorial right A does not open the wheel again");
        Setup(true);WheelTutorial.Ends=0;WheelTutorial.Forced=true;wheel.OpenWheel(false);Step(true,false);Check(WheelTutorial.Ends==0,"an A held from before the tutorial ended it");
        Step(false,false);WheelTutorial.Forced=true;Step(true,false);Check(WheelTutorial.Ends==1,"a fresh right A press does not end the tutorial");
        Setup();WheelTutorial.Ends=0;Step(false,false);WheelTutorial.Forced=true;Step(true,false);Check(WheelTutorial.Ends==1&&!WheelTutorial.LastOpen&&wheel.OpenCount==0,"right A before the tutorial's wheel opened does not end it, or the mod opens a wheel");
        WheelTutorial.Forced=false;
        Check(WheelTutorialMath.Labels(true,99)&&WheelTutorialMath.Labels(false,.5f)&&!WheelTutorialMath.Labels(false,WheelTutorialMath.HintWindow+.1f)&&!WheelTutorialMath.Labels(false,-1),"the tutorial hint's labels");
        // In VR the wheel works without Windows focus (Virtual Desktop); outside VR it does not.
        Setup();WindowFocus.InVr=true;Application.isFocused=false;Step(false,false);Step(true,false,1,0);Check(ui.WheelOpen,"in VR the wheel does not open without Windows focus");
        Step(false,false);Check(wheel.Equips==1,"in VR the wheel does not choose without Windows focus");WindowFocus.InVr=false;Application.isFocused=true;
        Setup();Step(false,false,extra:HandControls.A);Check(ui.ObjectivesOpen && ui.BlocksGameplay,"X failed to open tasks/block gameplay");
        Step(false,false,extra:HandControls.A);Check(ui.ObjectivesOpen,"held X repeated");
        Step(false,false);Step(false,true);Check(!ui.ObjectivesOpen,"grip+X failed to close tasks");
        Step(false,false);Step(false,false,extra:HandControls.A);Step(true,false);Check(!ui.ObjectivesOpen && ui.WheelOpen,"wheel did not close task panel");
        Setup();Step(false,false,extra:HandControls.A);rig.Scripted=true;Step(false,false,extra:HandControls.A);Check(!ui.ObjectivesOpen,"cutscene left tasks open");
        rig.Scripted=false;Step(false,false,extra:HandControls.A);Check(!ui.ObjectivesOpen,"held X reopened after cutscene");
        Setup();Step(false,false,r3:true,extra:HandControls.Grip);Step(false,false,r3:false,extra:HandControls.Grip);Step(false,false,r3:true,extra:HandControls.Grip);
        Check(!QualityMenu.Open&&!ui.ObjectivesOpen,"removed left grip+R3 chord still opens the VR settings");
        QualityMenu.Show();Step(false,false);
        Check(QualityMenu.Open&&ui.BlocksGameplay&&!ui.ObjectivesOpen,"VR settings page opened tasks or did not block controls");
        Step(true,false,r3:false,extra:HandControls.Grip);Check(!ui.WheelOpen,"quality menu allows wheel/gadget shortcut");
        Step(false,true);Check(!QualityMenu.Open,"grip+X failed to close quality without Escape");
        Step(false,false);Step(false,false,extra:HandControls.A);Check(ui.ObjectivesOpen,"ordinary X tasks lost after quality menu");
        // 0.1.158: the menu chord in either order: the grip first, or X first and the grip within half a second.
        Setup();EscapeKey.Downs=0;Step(false,false,extra:HandControls.Grip);Step(false,false,extra:HandControls.Grip|HandControls.A);
        Check(EscapeKey.Downs==1,"grip then X did not type Escape");
        Step(false,false,extra:HandControls.Grip|HandControls.A);Check(EscapeKey.Downs==1,"held chord typed Escape again");
        Step(false,false);Step(false,false,extra:HandControls.A);Check(ui.ObjectivesOpen,"X alone did not open the tasks");
        Step(false,false,dt:.2f,extra:HandControls.A|HandControls.Grip);Check(EscapeKey.Downs==2&&!ui.ObjectivesOpen,"X then the grip within half a second did not close the tasks and type Escape");
        Step(false,false);Step(false,false,extra:HandControls.A);Step(false,false,dt:.6f,extra:HandControls.A);Step(false,false,extra:HandControls.A|HandControls.Grip);
        Check(EscapeKey.Downs==2,"the grip long after X typed Escape");
        // Escape cannot be typed (another window in front): in play the game's pause menu opens directly; not in the main menu.
        Step(false,false);EscapeKey.Accept=false;GamePause.Opens=0;Step(false,false,extra:HandControls.Grip);Step(false,false,extra:HandControls.Grip|HandControls.A);
        Check(GamePause.Opens==1&&EscapeKey.Downs==2,"refused Escape did not open the pause menu directly");
        Step(false,false);rig.Frontend=true;Step(false,false,extra:HandControls.Grip);Step(false,false,extra:HandControls.Grip|HandControls.A);
        Check(GamePause.Opens==1,"pause menu opened directly from the main menu");rig.Frontend=false;EscapeKey.Accept=true;
        // 0.1.150: a left-hander's left grip + X opens doors; the menu chord is the right grip + A
        // (not in the open wheel: there the grip still takes the weapon).
        WeaponHands.LeftHanded=true;
        Setup();QualityMenu.Show();Step(false,false);
        Step(false,true);Check(QualityMenu.Open,"left-handed: left grip+X closed the VR settings (it opens doors)");
        Step(false,false);Step(true,false,rightExtra:HandControls.Grip);Check(!QualityMenu.Open,"left-handed: right grip+A does not close the VR settings");
        Setup();Step(false,false,extra:HandControls.A);Check(ui.ObjectivesOpen,"left-handed: left X does not open tasks");
        Step(false,false,rightExtra:HandControls.Grip);Step(true,false,rightExtra:HandControls.Grip);Check(!ui.ObjectivesOpen&&!ui.WheelOpen,"left-handed: right grip+A does not close tasks");
        // 0.1.158: left-handed, right A a moment before the grip: the wheel it opened closes (nothing taken), Escape.
        Setup();EscapeKey.Downs=0;Step(false,false);Step(true,false);Check(ui.WheelOpen,"left-handed: wheel for the late chord");
        Step(true,false,dt:.15f,rightExtra:HandControls.Grip);Check(!ui.WheelOpen&&wheel.Equips==0&&EscapeKey.Downs==1,"left-handed: A then the grip did not close the wheel and type Escape");
        Setup();EscapeKey.Downs=0;Step(false,false,rightExtra:HandControls.Grip);Step(true,false,rightExtra:HandControls.Grip);Check(EscapeKey.Downs==1&&!ui.WheelOpen,"left-handed: grip then A did not type Escape");
        Setup();Step(false,false);Step(true,false,1,0);Check(ui.WheelOpen,"left-handed: wheel");
        Step(true,false,1,0,rightExtra:HandControls.Grip);Check(!ui.WheelOpen&&wheel.Equips==1,"left-handed: the grip in the open wheel no longer takes the weapon");
        WeaponHands.LeftHanded=false;
        Setup();Step(false,false,r3:true);Check(!ui.ObjectivesOpen&&!ui.BlocksGameplay,"alternate fire click opens tasks");
        Setup();var inv=((PlayerHUDControl)Resources.Items[0]).inventory;
        Step(false,false,extra:HandControls.B);for(int i=0;i<30;i++)Step(false,false,extra:HandControls.B);
        Check(inv.Next==1,"held Y repeats weapon selection");Step(false,false);
        Step(false,false,extra:HandControls.Grip|HandControls.B);
        Check(inv.Medkits==0&&inv.Gadgets==0&&inv.Next==1,"removed grip+Y still selects or uses items");
        Step(false,false,extra:HandControls.B);Check(inv.Next==1,"modifier release turns held Y into next weapon");
        Step(false,false);Step(false,false,extra:HandControls.A);Check(!ui.WheelOpen,"bare left X opens selector");
        Step(false,false,extra:HandControls.Grip|HandControls.A);Check(!ui.WheelOpen,"adding grip to held X opens selector");
        Step(false,false);Step(true,false,1,0);Check(ui.WheelOpen,"right A does not open selector after left X");
        ui.Items.Hovered=true;Step(false,false);Check(!ui.WheelOpen&&wheel.Equips==0&&ui.PendingConsumable,"hovered medkit release equips gun instead of previewing item");
        Step(false,false,extra:HandControls.B);Check(!ui.PendingConsumable&&inv.Next==2,"next weapon does not cancel pending medkit");
        Step(false,false);Application.isFocused=false;Step(false,false,extra:HandControls.B);Application.isFocused=true;Step(false,false,extra:HandControls.B);
        Check(inv.Next==2,"focus recovery repeats held Y");Step(false,false);Step(false,false,extra:HandControls.B);Check(inv.Next==3,"Y cannot rearm");
        // 0.1.142: with a gun in the left hand the left Y reloads it, it does not select the next weapon.
        WeaponHands.Current!.LeftYReloads=true;Step(false,false);Step(false,false,extra:HandControls.B);Check(inv.Next==3,"left Y with a gun in the left hand selected the next weapon");
        Step(false,false,extra:HandControls.B);WeaponHands.Current.LeftYReloads=false;Step(false,false,extra:HandControls.B);Check(inv.Next==3,"left Y held through the reload selected the next weapon when the gun left the hand");
        WeaponHands.Current.LeftYReloads=false;Step(false,false);
        GameInputManager.Locked=true;Step(false,false);Step(false,false,extra:HandControls.B);Check(inv.Next==3,"input-locked menu allows weapon selection");
        rig.Frontend=true;Check(ui.PointerMenuOpen&&ui.BlocksGameplay,"initial menu lacks pointer context/gameplay suppression");
        rig.MovieActive=true;rig.Scripted=true;Check(!ui.PointerMenuOpen&&ui.BlocksGameplay,"movie input leaks into frontend menu");
        rig.MovieActive=false;rig.Scripted=false;rig.Frontend=false;GameInputManager.Locked=false;
        Step(false,false);Step(true,false);Check(ui.WheelOpen,"setup before scene reset");
        ui.Items.Hovered=true;ui.Items.Commit();
        ui.OnSceneChanged();Check(!ui.WheelOpen&&!ui.PendingConsumable&&!ui.ObjectivesOpen&&!QualityMenu.Open,"scene retains wheel/item/UI lock");
        Step(false,false);Check(ui.Hud!=null&&!ui.BlocksGameplay,"scene cannot reacquire live native HUD");
        ui.Dispose();
        Console.WriteLine("PASS: 0.1.214 right A ends the game's weapon wheel tutorial (a fresh press, not one held from before; also before its wheel opened); the A held on opens no wheel until let go; the tutorial hint names right A while it runs.");
        Console.WriteLine("PASS: 0.1.210 the wheel the game opens itself (its weapon wheel tutorial) is taken over by holding A: the left stick chooses, letting go confirms and unlocks; the menu chord types Escape for it, or closes it when Escape cannot be typed; in VR the wheel works without Windows focus.");
        Console.WriteLine("PASS: 0.1.158 menu chord in either order (button first, grip within 0.5 s; left-handed the wheel that A opened closes); refused Escape opens the pause menu directly in play, not in the main menu.");
        Console.WriteLine("PASS: left-handed: the menu chord moves to the right grip + A (the left grip + X opens doors), the grip in the open wheel still takes the weapon.");
        Console.WriteLine("PASS: equipment chords execute once, modifier release is latched, native wheel preserved, focus/lock guard.");
        Console.WriteLine("PASS: real menu driver startup/neutral/hold/release; hover never equips; single confirmation; Esc cancels; focus/tracking/cinematic loss; chapter rebind; keyboard wheel ownership.");
        Console.WriteLine("Simulated APIs; Windows Escape dispatch and native InventoryWheel behavior need in-game verification.");
    }
}
namespace UnityEngine
{
    public class Object
    {static int next;public IntPtr Pointer=(IntPtr)(++next); public string name="test";public T? TryCast<T>() where T:class=>this as T;}
    public sealed class GameObject:Object {public bool activeInHierarchy=true;}
    public sealed class Transform:Object {public Transform? parent {get;set;} public GameObject gameObject=new();public bool IsChildOf(Transform t)=>ReferenceEquals(this,t);}
    public static class Mathf{public const float Deg2Rad=MathF.PI/180;public static float Sin(float a)=>MathF.Sin(a);public static float Cos(float a)=>MathF.Cos(a);}
    public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
    public static class Time {public static float realtimeSinceStartup;private static int frames;public static int frameCount=>++frames;}
    public static class Application {public static bool isFocused=true;}
    public static class Resources {public static Object[] Items=Array.Empty<Object>();public static Object[] FindObjectsOfTypeAll(Type type)=>Items;}
}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Type Of<T>()=>typeof(T);}}
namespace HarmonyLib
{
    public sealed class Harmony {public Harmony(string id){}public void UnpatchSelf(){}public void Patch(MethodInfo method,HarmonyMethod prefix) {if(method==null)throw new Exception("missing hook");}}
    public sealed class HarmonyMethod {public HarmonyMethod(Type type,string name){}}
    public static class AccessTools {public static MethodInfo DeclaredMethod(Type type,string name)=>type.GetMethod(name)!;}
}
public struct OwnerInfo {public bool IsInvalid=>false;public bool IsPlayer=>true;public int Id=>0;}
public class PlayerEquipableInventory:UnityEngine.Object
{
 public Transform transform=null!;public bool isInTransit,IsInventoryBlocked;public int Next,Medkits,Gadgets,Uses;public ActiveEquipmentSlot currentSlot=(ActiveEquipmentSlot)(-1);
 public enum ActiveEquipmentSlot{MedkitS,MedkitL,C4,MicroSpy,Zipline,Gadget}
 public void SelectNext(){Next++;}public bool HasStacksForConsumable(ActiveEquipmentSlot s)=>s==ActiveEquipmentSlot.MedkitS||s==ActiveEquipmentSlot.MedkitL;
 public void TryUsingConsumable(ActiveEquipmentSlot s){Uses++;}public bool HasEquipableInSlot(ActiveEquipmentSlot s)=>s==ActiveEquipmentSlot.C4;
 public bool TrySelectSlot(ActiveEquipmentSlot s,bool a,bool b,bool c,bool d,bool e){if(a||b||d||e)throw new Exception("selection bypasses native conditions");currentSlot=s;if(s==ActiveEquipmentSlot.MedkitS||s==ActiveEquipmentSlot.MedkitL)Medkits++;else Gadgets++;return true;}
}
public class PlayerHUDControl:UnityEngine.Object {public PlayerEquipableInventory inventory=null!;public InventoryWheel inventoryWheel=null!;public OwnerInfo GetOwner()=>new();}
public class WeaponInventoryWheelElement{public bool CanHover()=>true;}
public class InventoryWheel:UnityEngine.Object
{
    public WeaponInventoryWheelElement[] wheelSlices={new(),new(),new()};public float anglePerSlice=120;
    public bool IsOpen,deselectOnZeroCursorOffset=true;public Vector3 cursorOffset;public int hoveringOption=-1,OpenCount,CloseCount,HoverCount,Equips;
    public void OpenWheel(bool reminder){IsOpen=true;OpenCount++;GameInputManager.Locked=true;hoveringOption=-1;}
    public void CloseWheel(bool confirm,bool blur){IsOpen=false;CloseCount++;if(confirm)Equips++;GameInputManager.Locked=false;}
    public void HoverOption(){HoverCount++;hoveringOption=2;}
    public void EvaluateOpenCloseInput(bool ignoreLock){} public void EvaluateDefaultWheelInput(bool ignoreLock){} public void GetChoosingDirection(){}
}
public static class GameInputManager {public static bool Locked;public static bool IsInputLocked(int id)=>Locked;}
public static class PauseMenuControl {public static bool HackGameIsPaused,BlockTogglePause;}
namespace XiiiXR
{
    internal sealed class CameraRig {internal Transform? PlayerRoot;internal bool HeadTrackingValid,Scripted,Frontend,MovieActive;internal HandControls LeftControls,RightControls;internal HandControls MenuRightControls=>RightControls;internal int Disarms;internal void DisarmTrigger(){Disarms++;}internal StickSample RightStick,LeftStick;}
    internal sealed class WheelItems {
        internal bool Pending {get;private set;} internal bool LeftTool=>false;internal bool LeftHeld=>false;internal bool RightHeld=>false;internal bool HandTool=>false;internal bool GrappleTool=>false;internal int ArmSide=>-1;internal bool Hovered {get;set;} internal string Notice=>Pending?"Малая аптечка":"";
        internal WheelItems(CameraRig c){}internal void Bind(InventoryWheel? w){Clear();}internal void Hover(){}
        internal bool Commit(){if(!Hovered)return false;Pending=true;Hovered=false;return true;}
        internal void ClearHover(){Hovered=false;}internal void Clear(){Pending=false;Hovered=false;}internal void Tick(bool allowed){}
    }
    internal sealed class ZiplineVr{internal static ZiplineVr? Current=>null;internal bool HandBusy(bool right)=>false;}
    internal static class Bootstrap {internal static void Write(string s){}internal static void Warn(string s){}}
    internal static class WindowFocus {internal static bool InVr;internal static bool Playable=>InVr||UnityEngine.Application.isFocused;}
    internal sealed class ArmMedkits{internal ArmMedkits(CameraRig c,WheelItems w){}internal void Tick(bool a,PlayerEquipableInventory? i,InventoryWheel? w){}internal void Reset(){}}
}

namespace BepInEx.Configuration {
 internal sealed class ConfigEntry<T>{internal T Value;internal ConfigEntry(T v){Value=v;}}
 internal sealed class ConfigFile{internal ConfigEntry<T> Bind<T>(string a,string b,T v,string d)=>new(v);}
}
namespace UnityEngine {
 internal sealed class Camera{internal Transform transform=new();internal static Camera[] allCameras=>Array.Empty<Camera>();}
 internal sealed class RenderTexture{internal int width=>3000;internal int height=>2900;}
}
namespace UnityEngine.XR {internal sealed class XRDisplaySubsystem{internal float scaleOfAllRenderTargets {get;set;} internal UnityEngine.RenderTexture GetRenderTextureForRenderPass(int p)=>new();}}

namespace XiiiXR{static class CycleIcons{internal static void Request(){}}}

namespace XiiiXR{static class FramePerformance{internal static long Begin()=>0;internal static void Scope(string s,long t){}}}
namespace XiiiXR{internal sealed class WeaponHands{internal static WeaponHands? Current=new();internal static WeaponGripMode GripMode=>WeaponGripMode.Hold;internal int Taken;internal void TakenByGrip(){Taken++;}internal bool LeftYReloads;internal static bool LeftHanded;}}
namespace XiiiXR
{
    static class EscapeKey{internal static bool Accept=true;internal static int Downs;internal static string LastRefusal="";internal static bool Send(bool down){if(!down)return true;if(!Accept){LastRefusal="test refusal";return false;}Downs++;LastRefusal="";return true;}}
    static class WheelTutorial{internal static bool Forced,LastOpen;internal static int Ends;internal static string End(bool open,int player){Ends++;Forced=false;LastOpen=open;return "test";}}
    static class GamePause{internal static int Opens;internal static bool TryOpen(out string how){Opens++;how="";return true;}}
}
