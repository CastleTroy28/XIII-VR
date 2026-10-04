using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.XR;
namespace XiiiXR;
internal static class QualityOptions
{
    internal static ConfigEntry<float> RenderScale=null!;
    internal static ConfigEntry<bool> AdaptiveSupersampling=null!;
    internal static ConfigEntry<bool> Collisions=null!,AutoStart=null!,SkipLogos=null!,Hints=null!,ThrowArc=null!,PhysicalCrouch=null!;
    // 0.1.91: placement of the grappling hook in the left hand, millimetres in
    // the hand frame, adjustable in the VR settings while holding the hook.
    internal static ConfigEntry<int> GrappleAlong=null!,GrappleUp=null!,GrappleForward=null!;
    internal static ConfigEntry<int>? BarrelGripForward,BarrelGripUp,ZiplineGripForward,ZiplineGripUp;
    // 0.1.142: climbing / going down the rope twice as fast.
    internal static ConfigEntry<float>? GrappleClimbSpeed;
    // 0.1.120: stationary machine gun steered by its handles (inverted, like
    // a real one) or by where the controllers point.
    internal static ConfigEntry<bool>? MountedHandles;
    // 0.1.142: the view on a mounted gun turns with it (a seat on the turret).
    internal static ConfigEntry<bool>? MountedViewTurns;
    // 0.1.146: the view
    // on a mounted gun raised above it.
    internal static ConfigEntry<float>? MountedViewRaise;
    internal static float MountRaise{get{float v=MountedViewRaise?.Value??.30f;return float.IsFinite(v)?Math.Clamp(v,0,.6f):.30f;}}
    // 0.1.124: the weapon in the right hand while the grip is held (0), taken
    // and put away by grip presses (1) or always in the hand (2, the old way);
    // body places mirrored for a left-hander.
    internal static ConfigEntry<int>? WeaponGrip;
    internal static ConfigEntry<bool>? LeftHanded;
    // 0.1.160: a two-handed gun in one hand turned toward the other hand (degrees).
    internal static ConfigEntry<int>? GunTurn;
    // 0.1.146: the pistol places on the
    // belt at the sides and under the arms, each switched on or off.
    internal static ConfigEntry<bool>? BeltPistols,ArmpitPistols;
    // 0.1.151: the mod's memory cleaned in small steps (ModGcPacer).
    internal static ConfigEntry<bool>? GcSteps;
    // 0.1.214: the mark where a throw lands (WeaponHands.Landing).
    internal static ConfigEntry<bool>? ThrowLanding;
    internal const int RenderScaleDefaults=182;
    internal static void Load(ConfigFile c)
    {
        // 0.1.182: 1 = exactly the
        // size the headset's own software recommends (Pimax Play, SteamVR, the
        // Meta app); the old default 0.75 is moved to 1 once.
        RenderScale=c.Bind("VR","RenderScale",1f,"XR texture width/height multiplier of the size the headset's software recommends (Pimax Play, SteamVR, the Meta app...), 0.50-1.50; 1 = exactly that size. Game menu > VR SETTINGS (or F2). Higher values cost GPU time.");
        AdaptiveSupersampling=c.Bind("VR","AdaptiveSupersampling",true,"0.1.188: in demanding scenes, temporarily reduce supersampling above 100% to recover the headset refresh rate. Never goes below its recommended resolution. Deferred rendering uses infrequent target-size changes; with no GPU timing, restores on level or explicit resolution change. Does not alter RenderScale. false = fixed resolution.");
        var scaleDefaults=c.Bind("VR","RenderScaleDefaultsVersion",0,"Internal one-time move of the old 0.75 resolution default to 1.0.");
        if(scaleDefaults.Value<RenderScaleDefaults){if(MathF.Abs(RenderScale.Value-.75f)<.001f)RenderScale.Value=1f;scaleDefaults.Value=RenderScaleDefaults;}
        Hints=c.Bind("VR","InteractionHints",true,"Show original gameplay interaction hints, including doors, cabinets and key/card locks, with VR button labels.");
        ThrowArc=c.Bind("VR","ThrowAimArc",false,"false (0.1.149): knives, bottles, ashtrays are held by the grip and thrown by letting go of it during a swing (letting go without a swing puts a knife back on the chest, drops a bottle). true: while the grip holds it the flight path is shown; letting go throws where the controller points.");
        ThrowLanding=c.Bind("VR","ThrowLandingMarker",true,"0.1.214: while a knife, a grenade with its pin out, a bottle or an ashtray is ready to be thrown, a ring marks where it would land (during a swing: where letting go now throws it; otherwise a plain throw along the controller); once thrown, where it lands. false = no ring. Config file only.");
        GrappleAlong=c.Bind("VR","GrappleGripAlongMm",10,"Grappling hook in the left hand: shift along the knuckles (negative = toward the little finger), mm. Config file only.");
        GrappleUp=c.Bind("VR","GrappleGripUpMm",0,"Grappling hook in the left hand: shift toward the back of the hand (negative = into the palm), mm.");
        GrappleForward=c.Bind("VR","GrappleGripForwardMm",20,"Grappling hook in the left hand: shift toward the fingertips (negative = toward the wrist), mm.");
        // 0.1.198: a gun held by its barrel, and the zipline hook's handle, deeper in the closed hand.
        BarrelGripForward=c.Bind("VR","BarrelGripForwardMm",-28,"A gun held by its barrel, and the zipline hook's handle: shift in the closed hand toward the fingertips (negative = toward the wrist, deeper into the curled fingers), mm. Config file only.");
        BarrelGripUp=c.Bind("VR","BarrelGripUpMm",-4,"A gun held by its barrel, and the zipline hook's handle: shift toward the back of the hand (negative = toward the palm), mm. Config file only.");
        // 0.1.200: the hook's handle on top of that, nearer the palm.
        ZiplineGripForward=c.Bind("VR","ZiplineGripForwardMm",0,"The zipline hook's handle (after BarrelGripForwardMm): shift toward the fingertips (negative = toward the wrist), mm. Config file only.");
        ZiplineGripUp=c.Bind("VR","ZiplineGripUpMm",8,"The zipline hook's handle (after BarrelGripUpMm): shift toward the palm and the back of the hand (positive; negative = out toward the thumb's curl), mm. Config file only.");
        GrappleClimbSpeed=c.Bind("VR","GrappleClimbSpeed",2f,"Grappling hook: how fast the left stick climbs and goes down the rope, times the game's own speed (0.5-4; 2 = twice as fast).");
        MountedHandles=c.Bind("VR","MountedGunHandles",true,"Stationary machine gun: true = steer it by the handles like a real one (hands right: barrel left, hands down: barrel up); false = the barrel turns where the controllers point. Game menu > VR SETTINGS.");
        MountedViewTurns=c.Bind("VR","MountedGunViewTurns",true,"Stationary machine gun: true = the view is fixed to the gun and turns with it about its pivot (like a seat on the turret; it does not tilt with the barrel); false = the view stays where it was when the gun was taken.");
        MountedViewRaise=c.Bind("VR","MountedGunViewRaiseMeters",.30f,"Stationary machine gun: the view is raised this much above where you stand (0-0.6 m), so that you see over the gun when shooting down.");
        WeaponGrip=c.Bind("VR","WeaponGrip",0,"Weapon in the right hand: 0 = while the right grip is held (let go at a body place: it hangs there; elsewhere: it drops to the floor and stays in the wheel), 1 = a grip press takes it and the next press puts it away, 2 = always in the hand (old). Game menu > VR SETTINGS.");
        LeftHanded=c.Bind("VR","LeftHanded",false,"Left-handed: everything mirrored - the body places of the weapons (belly rifle muzzle to the right, first pistol on the left hip), the ammunition pouch on the right of the belt, the ammunition watch on the left wrist and health on the right, a gun chosen in the wheel comes to the left hand (the left trigger fires it, the right hand reloads it). Game menu > VR SETTINGS.");
        GunTurn=c.Bind("VR","TwoHandedGunTurnDegrees",15,"0.1.160: a two-handed gun (rifle, shotgun, Uzi, crossbow...) held in one hand is turned this many degrees toward the other hand - to the right in the right hand, to the left in the left - so that the other hand reaches its fore-end (a gun stock holds the controllers apart). 0-45. Game menu > VR SETTINGS.");
        BeltPistols=c.Bind("VR","PistolPlacesOnBelt",true,"Pistols, revolvers and Uzis can hang on the belt at the sides. Game menu > VR SETTINGS.");
        ArmpitPistols=c.Bind("VR","PistolPlacesUnderArms",true,"Pistols, revolvers and Uzis can hang under the arms. Game menu > VR SETTINGS.");
        HolsterLayout.BeltPistols=()=>BeltPistols?.Value!=false;HolsterLayout.ArmpitPistols=()=>ArmpitPistols?.Value!=false;
        GcSteps=c.Bind("VR","ModGcSteps",true,"0.1.151: the mod's own memory (the .NET runtime in the game) is cleaned in small steps of a few MB (a few ms each) instead of piling up tens of MB and stopping the game for 150-450 ms. false = the runtime's own way.");
        PhysicalCrouch=c.Bind("VR","PhysicalCrouch",true,"Crouch in the game when you crouch/sit down for real (35 cm below your standing head height; stand up to rise).");
        Collisions=c.Bind("VR","HandWeaponCollisions",true,"Kinematic hand/weapon collision against existing solid world colliders.");
        AutoStart=c.Bind("VR","AutoStartVR",true,"Start VR automatically as soon as the initial menu cameras are ready. Set false for manual F9 startup.");
        SkipLogos=c.Bind("VR","SkipStartupLogos",true,"Skip publisher startup logos through the game's own main-menu transition. Story videos are preserved.");
        var startupDefaults=c.Bind("VR","StartupDefaultsVersion",0,"Internal one-time migration of old disabled auto-start defaults.");
        if(startupDefaults.Value<22){AutoStart.Value=true;startupDefaults.Value=22;}
        Bootstrap.Write("AUTO START configured="+AutoStart.Value+"; waits for initial game/menu cameras; F9 remains available.");
    }
}
internal static class QualityMenu
{
    internal static string TakeAction(){var a=action;action="";return a;}
    private static string action="";
    internal static bool Open {get;private set;}
    private static bool chordArmed,axisArmed,triggerArmed;
    private static int row;
    // 0.1.224: no "Smarter enemies" row (its setting stays as it is: on, config file only).
    private const int Rows=19;
    private static readonly bool[] adjusted=new bool[Rows];
    private static float pending=1f;
    private static bool applyRequested;
    // status: English source text (translated when shown); statusResolution: append the size.
    private static string status="";private static bool statusResolution,resolutionFailed;private static int resolutionWidth,resolutionHeight;
    private static float nextResolution;
    private static bool verifying;
    private static int oldWidth,oldHeight;
    private static float verifyUntil;
    internal static int AppliedRevision {get;private set;}
    internal static void Show(){if(!Open)Toggle();}
    internal static void Toggle(){Open=!Open;if(Open){pending=QualityOptions.RenderScale.Value;row=0;status="";statusResolution=false;Array.Clear(adjusted,0,adjusted.Length);}axisArmed=triggerArmed=false;}
    internal static void Close(){Open=false;axisArmed=triggerArmed=false;}
    internal static void Chord(bool valid,bool clicked,bool modifier)
    {
        if(!valid){chordArmed=false;Close();return;}
        if(!clicked)chordArmed=true;
        else if(chordArmed){chordArmed=false;if(modifier)Toggle();}
    }
    internal static void Tick(bool valid,StickSample stick,HandControls right)
    {
        if(!Open)return;
        if(!valid||!stick.Valid||!right.Valid){Close();return;}
        var v=stick.Value;
        if(v.LengthSquared()<.09f)axisArmed=true;
        else if(axisArmed&&v.LengthSquared()>.36f)
        {
            axisArmed=false;
            if(Math.Abs(v.Y)>Math.Abs(v.X))row=(row+(v.Y>0?Rows-1:1))%Rows;
            else if(row==0)pending=Clamp(pending+(v.X>0?.10f:-.10f));
            else if(row<6||row>=10&&row<Rows-1){Adjust(v.X>0?1:-1,false);adjusted[row]=true;}
        }
        bool trigger=(right.Held&(HandControls.Trigger|HandControls.A))!=0;
        // While the controller ray is on the VR page the trigger clicks rows
        // there; it must not also confirm (or confirm on leaving the page).
        if(PointerOwnsTrigger&&(right.Held&HandControls.Trigger)!=0)triggerArmed=false;
        else if(!trigger)triggerArmed=true;
        else if(triggerArmed)
        {
            triggerArmed=false;
            Confirm(false);
        }
    }
    // 0.1.101: the VR settings page in the game menu (VrSettingsPage) also
    // drives these rows with the controller ray.
    internal static bool PointerOwnsTrigger {get;set;}
    internal static int Row=>row;
    internal const int RowCount=Rows;
    internal static void Hover(int r){if(Open&&r>=0&&r<Rows)row=r;}
    // side: -1 left arrow, +1 right arrow, 0 the row itself.
    internal static void Click(int r,int side)
    {
        if(!Open||r<0||r>=Rows)return;row=r;
        if(side!=0&&r==0){pending=Clamp(pending+side*.10f);return;}
        if(side!=0&&(r<6||r>=10&&r<Rows-1)){Adjust(side,false);adjusted[r]=true;return;}
        Confirm(true);
    }
    internal static bool Adjustable(int r)=>r==0||r==4||r==GunTurnRow;
    internal const int GunTurnRow=17;
    internal const int GunTurnMax=45,GunTurnStep=5;
    internal static int GunTurnDegrees=>Math.Clamp(QualityOptions.GunTurn?.Value??15,0,GunTurnMax);
    private static void Confirm(bool pointer)
    {
        if(row==0)Apply();
        else if(row==Rows-1)Close();
        else if(row>=6&&row<=9){action=row==6?"recenter":row==7?"reset":row==8?"calibrate":"haptics";status=row==8?"In 3 seconds: look ahead and hold your right hand forward":"Done";statusResolution=false;}
        else if(pointer&&row==4)Adjust(1,false);
        else if(pointer||!adjusted[row])Adjust(1,true);
        else {status="Saved";statusResolution=false;}
    }
    private static void Adjust(int direction,bool toggle)
    {
        if(row==10)QualityOptions.Hints.Value=toggle?!QualityOptions.Hints.Value:direction>0;
        if(row==12&&QualityOptions.MountedHandles!=null){QualityOptions.MountedHandles.Value=toggle?!QualityOptions.MountedHandles.Value:direction>0;Bootstrap.Write("VR SETTINGS mounted gun="+(QualityOptions.MountedHandles.Value?"handles":"pointing"));}
        if(row==13&&QualityOptions.WeaponGrip!=null){int v=Math.Clamp(QualityOptions.WeaponGrip.Value,0,2);QualityOptions.WeaponGrip.Value=toggle?(v+1)%3:(v+direction+3)%3;Bootstrap.Write("VR SETTINGS weapon grip="+(QualityOptions.WeaponGrip.Value==0?"hold":QualityOptions.WeaponGrip.Value==1?"toggle":"always"));}
        if(row==14&&QualityOptions.LeftHanded!=null){QualityOptions.LeftHanded.Value=toggle?!QualityOptions.LeftHanded.Value:direction<0;Bootstrap.Write("VR SETTINGS left-handed="+QualityOptions.LeftHanded.Value);}
        if(row==15&&QualityOptions.BeltPistols!=null){QualityOptions.BeltPistols.Value=toggle?!QualityOptions.BeltPistols.Value:direction>0;Bootstrap.Write("VR SETTINGS pistols on the belt="+QualityOptions.BeltPistols.Value);}
        if(row==16&&QualityOptions.ArmpitPistols!=null){QualityOptions.ArmpitPistols.Value=toggle?!QualityOptions.ArmpitPistols.Value:direction>0;Bootstrap.Write("VR SETTINGS pistols under the arms="+QualityOptions.ArmpitPistols.Value);}
        if(row==GunTurnRow&&QualityOptions.GunTurn!=null){int v=GunTurnDegrees;QualityOptions.GunTurn.Value=toggle?(v>=GunTurnMax?0:v+GunTurnStep):Math.Clamp(v+direction*GunTurnStep,0,GunTurnMax);Bootstrap.Write("VR SETTINGS two-handed gun in one hand turned "+QualityOptions.GunTurn.Value+" degrees toward the other hand");}
        if(row==11){QualityOptions.ThrowArc.Value=toggle?!QualityOptions.ThrowArc.Value:direction>0;Bootstrap.Write("VR SETTINGS throw="+(QualityOptions.ThrowArc.Value?"arc":"gesture"));}
        if(row==1)QualityOptions.Collisions.Value=toggle?!QualityOptions.Collisions.Value:direction>0;
        if(row==2)LocomotionOptions.Teleport.Value=toggle?!LocomotionOptions.Teleport.Value:direction>0;
        if(row==3)LocomotionOptions.SnapTurn.Value=toggle?!LocomotionOptions.SnapTurn.Value:direction>0;
        Bootstrap.Write("VR SETTINGS row="+row+" movement="+(LocomotionOptions.Teleport.Value?"teleport":"slide")+" turn="+(LocomotionOptions.SnapTurn.Value?"snap":"smooth"));
        status="Saved";statusResolution=false;
        if(row==5)UiLanguage.Manual=toggle?!UiLanguage.Manual:direction>0;
        if(row==4)
        {
            if(LocomotionOptions.SnapTurn.Value)LocomotionOptions.SnapAngle.Value=Math.Clamp(LocomotionOptions.SnapAngle.Value+direction*15,15,90);
            else LocomotionOptions.TurnSpeed.Value=Math.Clamp(LocomotionOptions.TurnSpeed.Value+direction*15,15,180);
        }
    }
    internal static void Apply()
    {if(Open){applyRequested=true;status="Applying...";statusResolution=false;Bootstrap.Write("QUALITY requested scale="+Clamp(pending));}}
    internal static float Clamp(float v)=>float.IsFinite(v)?MathF.Round(Math.Clamp(v,.5f,1.5f)*100)/100:1f;
    internal static void Attach(XRDisplaySubsystem display)
    {display.scaleOfAllRenderTargets=Clamp(QualityOptions.RenderScale.Value);nextResolution=0;verifying=false;}
    internal static void UpdateDisplay(XRDisplaySubsystem display)
    {
        if(applyRequested)
        {
            applyRequested=false;float previous=display.scaleOfAllRenderTargets;
            try
            {
                oldWidth=oldHeight=0;
                try{var old=display.GetRenderTextureForRenderPass(0);if(old!=null){oldWidth=old.width;oldHeight=old.height;}}catch{}
                float requested=Clamp(pending);
                display.scaleOfAllRenderTargets=requested;QualityOptions.RenderScale.Value=requested;
                Close();AppliedRevision++;
                verifying=Math.Abs(previous-requested)>.001f;verifyUntil=Time.realtimeSinceStartup+5;
                status=verifying?"Waiting for the new size...":"Already set";statusResolution=false;nextResolution=0;
                Bootstrap.Write("QUALITY set scale="+requested+" previous="+previous+" old="+oldWidth+"x"+oldHeight);
            }
            catch(Exception ex){verifying=false;try{display.scaleOfAllRenderTargets=previous;}catch{}status="Could not apply";statusResolution=false;Bootstrap.Warn("QUALITY change failed: "+ex);}
        }
        if(Time.realtimeSinceStartup<nextResolution)return;nextResolution=Time.realtimeSinceStartup+1;
        try
        {
            var texture=display.GetRenderTextureForRenderPass(0);
            if(texture!=null)
            {
                resolutionWidth=texture.width;resolutionHeight=texture.height;resolutionFailed=false;
                if(verifying&&oldWidth>0&&(texture.width!=oldWidth||texture.height!=oldHeight))
                {verifying=false;status="Applied:";statusResolution=true;Bootstrap.Write("QUALITY verified targets="+texture.width+"x"+texture.height);}
            }
        }
        catch{resolutionFailed=true;}
        if(verifying&&Time.realtimeSinceStartup>=verifyUntil)
        {verifying=false;status="Saved. Size not confirmed — restart the game.";statusResolution=false;Bootstrap.Warn("QUALITY targets unchanged/unavailable after 5s; saved scale applies on next game start.");}
    }
    private static string Line(int index,string text)=>(row==index?"> ":"  ")+text+"\n";
    // 0.1.103: one layout for every language; texts via UiLanguage.L.
    private static string L(string english)=>UiLanguage.L(english);
    private static string OnOff(bool value)=>L(value?"on":"off");
    private static string Resolution=>resolutionWidth>0?resolutionWidth+" × "+resolutionHeight+" "+L("per eye"):resolutionFailed?L("Resolution not available yet"):"";
    internal static string Text=>L("VR SETTINGS")+"\n\n"
        +Line(0,L("Resolution")+": "+MathF.Round(pending*100)+"%")
        +Line(1,L("Collisions")+": "+OnOff(QualityOptions.Collisions.Value))
        +Line(2,L("Movement")+": "+L(LocomotionOptions.Teleport.Value?"teleport":"slide"))
        +Line(3,L("Turning")+": "+L(LocomotionOptions.SnapTurn.Value?"snap":"smooth"))
        +Line(4,LocomotionOptions.SnapTurn.Value?L("Angle")+": "+LocomotionOptions.SnapAngle.Value+"°":L("Speed")+": "+LocomotionOptions.TurnSpeed.Value+L("°/s"))
        +Line(5,L("Manual reload")+": "+OnOff(UiLanguage.Manual))
        +Line(6,L("Recenter"))+Line(7,L("Reset hands"))+Line(8,L("Calibrate right hand (3 s)"))+Line(9,L("Test vibration"))
        +Line(10,L("Interaction hints")+": "+OnOff(QualityOptions.Hints.Value))
        +Line(11,L("Throwing")+": "+L(QualityOptions.ThrowArc.Value?"hold + flight path":"gesture (draw back, snap)"))
        +Line(12,L("Mounted gun")+": "+L(QualityOptions.MountedHandles?.Value!=false?"handles (inverted)":"pointing"))
        +Line(13,L("Weapon in hand")+": "+L(Math.Clamp(QualityOptions.WeaponGrip?.Value??0,0,2) switch{0=>"hold grip",1=>"grip press (take/let go)",_=>"always"}))
        +Line(14,L("Dominant hand")+": "+L(QualityOptions.LeftHanded?.Value==true?"left-handed":"right-handed"))
        +Line(15,L("Pistols on the belt")+": "+OnOff(QualityOptions.BeltPistols?.Value!=false))
        +Line(16,L("Pistols under the arms")+": "+OnOff(QualityOptions.ArmpitPistols?.Value!=false))
        +Line(GunTurnRow,L("Two-handed gun in one hand")+": "+(GunTurnDegrees==0?L("straight"):GunTurnDegrees+"° "+L("toward the other hand")))
        +Line(Rows-1,L("Close"))
        +"\n"+Resolution+"\n"+L("Right controller: point and pull the trigger (‹ › adjust).")+"\n"+L("Left stick: select / adjust. A: confirm. B: close.")+"\n"
        +(LocomotionOptions.Teleport.Value?L("Teleport: left stick forward, aim with the left hand, release. Back to cancel.")+"\n":"")
        +(status.Length==0?"":L(status)+(statusResolution?" "+Resolution:""));
    internal static bool StartupReady()=>Camera.allCameras.Length>0;
}
