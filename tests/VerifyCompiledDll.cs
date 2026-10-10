using System; using System.Linq; using System.Collections.Generic; using Mono.Cecil;
class Verify
{
 static IEnumerable<MethodReference> Calls(MethodDefinition method) => method.HasBody ? method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>() : Array.Empty<MethodReference>();
 static void Require(bool value,string message) { if(!value) throw new Exception(message); Console.WriteLine("PASS: "+message); }
 static void Main(string[] args)
 {
  // Arguments (optional): the built plugin, the game's BepInEx folder (its interop folder).
  using var p=ModuleDefinition.ReadModule(args.Length>0?args[0]:"xiii-xr/XIII.XRBootstrap.dll");
  var plugin=p.Types.Single(t=>t.Name=="Plugin");
  var info=plugin.CustomAttributes.Single(a=>a.AttributeType.Name=="BepInPlugin");
  Require((string)info.ConstructorArguments[2].Value=="0.1.257","compiled plugin reports version 0.1.257");
  var sceneHands=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="OnSceneChanged")).Any(x=>x.Name=="Clear"),"scene change clears weapon state");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="RegisterHand")).Any(x=>x.Name=="Clear"),"new native skeleton invalidates cached grip anchors");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="TickReloadProps")).Any(x=>x.Name=="get_Valid"),"reload props recover when scene destroys pouch");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="Build")).Any(x=>x.DeclaringType.Name=="PropFitPolicy"&&x.Name=="GripKey"),"native item chooses distinct prop grip key");
  Require(!Calls(sceneHands.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="Euler"),"native wrist orientation is not replaced by hardcoded outfit-independent Euler angles");
  var sampledHand=p.Types.Single(x=>x.Name=="NativeHandVisual");
  var handRefresh=Calls(sampledHand.Methods.Single(x=>x.Name=="Refresh")).ToList();
  Require(handRefresh.FindIndex(x=>x.Name=="EnsureBakeTarget")>=0&&handRefresh.FindIndex(x=>x.Name=="EnsureBakeTarget")<handRefresh.FindIndex(x=>x.Name=="BakePose"),"hand refresh repairs missing pose buffer before deformation");
  var bakeTarget=sampledHand.Methods.Single(x=>x.Name=="EnsureBakeTarget");
  Require(Calls(bakeTarget).Any(x=>x.Name=="set_hideFlags")&&Calls(bakeTarget).Any(x=>x.Name=="ResetRenderPose"),"hand pose buffer is retained and recovered buffers invalidate the pose cache");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="get_Valid")).Any(x=>x.Name=="get_RenderResourcesValid")&&Calls(sampledHand.Methods.Single(x=>x.Name=="get_RenderResourcesValid")).Any(x=>x.DeclaringType.Name=="NativeSkinSnapshot"&&x.Name=="get_Valid"),"lost private skin renderer invalidates visible hand binding");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="DisposeSkinResources"),"protected hand meshes have explicit owner disposal");
  Require(Calls(p.Types.Single(x=>x.Name=="NativeSkinSnapshot").Methods.Single(x=>x.IsConstructor)).Any(x=>x.Name=="DontDestroyOnLoad"),"private pose bones survive streamed scenes until owner disposal");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="TryWeaponGrip")).Any(x=>x.Name=="get_weaponSwitchInProgress")&&Calls(sampledHand.Methods.Single(x=>x.Name=="TryWeaponGrip")).Any(x=>x.Name=="get_isInTransit"),"authored grip waits for native weapon transition to finish");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="Refresh")).Any(x=>x.DeclaringType.Name=="HandSkinPose"&&x.Name=="Complete"),"hand bake uses complete authored skin or propagated helper bones");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="get_CurrentFpsRigReference")&&Calls(sampledHand.Methods.Single(x=>x.Name=="get_Valid")).Any(x=>x.Name=="get_m_fpsMesh"),"current outfit explicitly chooses and invalidates hand source");
  Require(Calls(sampledHand.Methods.Single(x=>x.Name=="TryWeaponGrip")).Any(x=>x.DeclaringType.Name=="GripPoseStability"&&x.Name=="Observe"),"paired wrist/skin capture waits for stable rendered samples");
  var knifeHands=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="KnifeButton"),"knife trigger feeds only the gesture-driven native press");
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="PropLaunched")).Any(x=>x.Name=="KnifeLaunch"),"native knife launch is aimed by the throw gesture");
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="KnifeAim"),"knife aim follows controller, not the sideways model");
  var fight186=p.Types.Single(x=>x.Name=="NpcHitReactions");
  Require(Calls(fight186.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="PunchContact")
   &&Calls(fight186.Methods.Single(x=>x.Name=="PunchContact")).Any(x=>x.Name=="Hits"&&x.DeclaringType.Name=="BrawlContactMath")
   &&Calls(fight186.Methods.Single(x=>x.Name=="PunchContact")).Any(x=>x.Name=="ClearBrawlPath"),"brawler damage follows posed fist sweep and solid-world obstruction checks");
  Require(!Calls(fight186.Methods.Single(x=>x.Name=="Swing")).Any(x=>x.Name=="PlayMeleeAttackAnimation")
   &&Calls(sceneHands.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="SuspendBrawls"),"no native melee animation double damage; loss of control cancels pending punches");
  Require(Calls(fight186.Methods.Single(x=>x.Name=="BrawlPose")).Any(x=>x.Name=="Orient"&&x.DeclaringType.Name=="NpcFist")
   &&Calls(fight186.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="Apply"&&x.DeclaringType.Name=="NpcFootwork")
   &&Calls(fight186.Methods.Single(x=>x.Name=="Release")).Any(x=>x.Name=="Release"&&x.DeclaringType.Name=="NpcFootwork"),"calibrated wrists and planted feet participate in render and release lifecycle");
  // 0.1.117: guns collide by their mesh cells; crossbow scope built from its mesh tube.
  var weaponVisual=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(weaponVisual.Methods.SelectMany(Calls).Any(x=>x.Name=="TightContact")&&Calls(weaponVisual.Methods.Single(x=>x.Name=="MeshCellShape")).Any(x=>x.Name=="Cells"),"guns still collide as bounding-box spheres");
  Require(Calls(weaponVisual.Methods.Single(x=>x.Name=="BuildTubeScope")).Any(x=>x.DeclaringType.Name=="ScopeGeometry"&&x.Name=="Find")&&Calls(weaponVisual.Methods.Single(x=>x.Name=="TickScope")).Any(x=>x.Name=="Scoped"),"crossbow scope not built from its mesh");
  Require(Calls(weaponVisual.Methods.Single(x=>x.Name=="PrepareReload")).Any(x=>x.DeclaringType.Name=="ReloadBones"&&x.Name=="Find"),"reload bones not matched by ReloadBones");
  // 0.1.118: grenade by hand — pin with the left trigger, thrown by the swing through the prop launch path.
  var grenadeTick=Calls(knifeHands.Methods.Single(x=>x.Name=="TickGrenade")).Select(x=>x.Name).ToList();
  Require(grenadeTick.Contains("PullPin")&&grenadeTick.Contains("Sample")&&grenadeTick.Contains("LaunchProp")&&grenadeTick.Contains("set_pinPulled")&&grenadeTick.Contains("PinClick"),"grenade not pinned and thrown by hand");
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickGrenade")&&Calls(knifeHands.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="get_GrenadeOwned"),"grenade hand throw not ticked or the button throw not blocked");
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="PropLaunched")).Any(x=>x.Name=="set_velocity"),"thrown grenade/prop velocity not set");
  // 0.1.119: mounted gun — ticked; hands hidden; trigger allowed without a tracked copy; both grips; view compensation.
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="MountedGunVr"),"mounted gun not ticked");
  Require(Calls(p.Types.Single(x=>x.Name=="WristHud").Methods.Single(x=>x.Name=="PoseGlove")).Any(x=>x.Name=="get_HidesHands"),"VR hands not hidden on the mounted gun");
  Require(Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="get_TurretTarget")&&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="InteractPress"),"mounted gun not taken with both grips / left by letting go");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="CompensateCarryTurn")).Any(x=>x.Name=="get_Mounted"),"VR view turns with the mounted gun's seat");
  var mountedGun=p.Types.Single(x=>x.Name=="MountedGunVr");
  Require(Calls(mountedGun.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="Yaw")&&Calls(mountedGun.Methods.Single(x=>x.Name=="Haptics")).Any(x=>x.Name=="ShotHaptics"),"mounted gun not aimed by the controllers or no firing vibration");
  // 0.1.120: handles (lever) steering from tracking-space hands; the game's arms cut at the elbow, drawn before culling.
  Require(Calls(mountedGun.Methods.Single(x=>x.Name=="Lever")).Any(x=>x.Name=="PhysicalHand")&&Calls(mountedGun.Methods.Single(x=>x.Name=="Lever")).Any(x=>x.Name=="Lever"&&x.DeclaringType.Name=="MountedAimMath"),"mounted gun handles not steered from the real hands");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Prepare")).Any(x=>x.Name=="Render"&&x.DeclaringType.Name=="MountedGunVr"),"mounted gun arms not drawn before culling");
  // 0.1.121: frame-rate fixes (half rate + reprojection doubled everything while walking).
  Require(Calls(p.Types.Single(x=>x.Name=="ContactSolver").Methods.Single(x=>x.Name=="Solve")).Any(x=>x.Name=="Clear"),"gun/hand collision solve without the broad free-space check");
  foreach(var (type,method) in new[]{("FrontendMenu","TickCore"),("EnemyAi","Tick"),("StoryVideo","Tick"),("DeathScreenVr","Tick"),("WaterSplash","Tick"),("MenuKeyboard","Tick"),("VrSettingsPage","Scan"),("WristHud","Discover"),("CameraRig","Discover")})
   Require(Calls(p.Types.Single(x=>x.Name==type).Methods.Single(x=>x.Name==method)).Any(x=>x.Name=="Due"&&x.DeclaringType.Name=="SceneScan"||x.Name=="Refresh"&&x.DeclaringType.Name.StartsWith("SceneFind")),type+"."+method+" searches the scene without the one-search-per-frame gate");
  Require(!p.Types.Single(x=>x.Name=="EnemyAi").Methods.Any(x=>x.Name=="Chosen"),"per-frame enemy choice counter still installed");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="TickScope")).Any(x=>x.Name=="Viewing"),"scope camera renders without an eye at the eyepiece");
  var perfTick=Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).ToList();
  Require(perfTick.Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="SceneScan")&&perfTick.Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="CompositorTiming")&&perfTick.Any(x=>x.Name=="Parts"),"PERF line without mod CPU, searches or compositor timing");
  Require(p.Types.Single(x=>x.Name=="WeaponHands").Fields.Any(x=>x.Name=="poseCallFrame")&&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="RenderPose")).Count(x=>x.Name=="ResolveGun")==1,"weapon pose not computed once per frame part");
  Require(!p.Types.SelectMany(t=>t.Methods).SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Any(x=>x.Contains("just kidding")),"death screen joke still in the plugin");
  // 0.1.122: grey mission, grenade launcher reload, hand/gun contact, outline hint, crossbow bolt/scope, muzzle flash point.
  Require(Calls(p.Types.Single(x=>x.Name=="StoryColorEffect").Methods.Single(x=>x.Name=="SampleColor")).Any(x=>x.Name=="Applied"&&x.DeclaringType.Name=="StoryVolumeMath"),"level volume saturation greys plain gameplay");
  var weaponHandsType=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(weaponHandsType.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Contains("AllowStartReload")&&weaponHandsType.Methods.Single(x=>x.Name=="AllowStartReload").Parameters.Any(x=>x.Name=="isPrimary"),"grenade launcher reload still blocked with the magazine reload");
  Require(Calls(p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveHand")).Any(x=>x.Name=="Carry"),"hand touching the gun not carried with it");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="UpdateGlow")).Any(x=>x.Name=="Show"&&x.DeclaringType.Name=="ReloadOutline"),"reload hint is not the part outline");
  var visualType=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(Calls(visualType.Methods.Single(x=>x.Name=="PrepareReload")).Any(x=>x.Name=="LoadedRelation")&&Calls(visualType.Methods.Single(x=>x.Name=="RefreshAnimation")).Any(x=>x.Name=="BakeRestored"&&x.Parameters.Count==6),"crossbow bolt not placed from a loaded pose");
  Require(Calls(visualType.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="Plausible"&&x.DeclaringType.Name=="MuzzleMath"),"muzzle flash not at the gun's own muzzle point");
  var mountedArms=p.Types.Single(x=>x.Name=="MountedArms");
  Require(Calls(mountedArms.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="set_forceRenderingOff")&&!mountedArms.Methods.SelectMany(Calls).Any(x=>x.Name=="set_enabled"&&x.DeclaringType.Name=="SkinnedMeshRenderer"),"game arms hidden by their enabled flag (the game's own state) instead of forceRenderingOff");
  Require(Calls(mountedArms.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="set_forceRenderingOff")&&Calls(mountedArms.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="set_cullingMode"),"game arms or their animation not restored after the mounted gun");
  // 0.1.123: crossbow fires after a hand reload, string drawn by the bolt, scope lens at the eyepiece, white outline without the insert hint, mounted gun without hand markers.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReload")).Any(x=>x.Name=="ReleaseNativeFire")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="VirtualButton")).Any(x=>x.Name=="ReleaseNativeFire"),"hand reload leaves the game waiting for its own reload");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="ReleaseNativeFire")).Any(x=>x.Name=="CancelWaitAfterFire")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="ReleaseNativeFire")).Any(x=>x.Name=="OnUnequipOrReload"),"fire wait not ended like the game's reload/unequip");
  Require(Calls(visualType.Methods.Single(x=>x.Name=="PrepareReload")).Any(x=>x.Name=="PrepareString")&&Calls(visualType.Methods.Single(x=>x.Name=="RefreshAnimation")).Any(x=>x.Name=="StringPose")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderReload")).Any(x=>x.Name=="StringDrawFor"),"crossbow string not drawn back by the bolt");
  Require(Calls(visualType.Methods.Single(x=>x.Name=="BuildTubeScope")).Any(x=>x.Name=="Eyepiece"),"crossbow scope lens not measured at the eyepiece");
  var outlineType=p.Types.Single(x=>x.Name=="ReloadOutline");
  Require(outlineType.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Contains("Hidden/Internal-Colored"),"outline still depends on Standard emission (drawn black)");
  Require(!Calls(weaponHandsType.Methods.Single(x=>x.Name=="UpdateGlow")).Any(x=>x.Name=="get_ReloadPort"),"hint where the magazine goes still shown");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="UpdateMarkers")).Any(x=>x.Name=="get_HidesHands"),"hand marker balls shown on the mounted gun");
  Require(p.Types.Single(x=>x.Name=="MountedGunVr").Fields.Any(x=>x.Name=="settling"),"mounted gun handles not centred where the hands come to rest");
  // 0.1.124: weapons held by the grip and hung on the body; crossbow launch after a hand reload; steady hand on the gun; mounted gun on both triggers.
  var tickCalls=Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList();
  Require(tickCalls.Contains("TickHolsters")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="RenderHolsters"),"body weapons not updated/drawn");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PutAway")).Any(x=>x.Name=="TrySelectSlot")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Take")).Any(x=>x.Name=="TrySelectSlot"),"putting away/taking a weapon does not switch the game's weapon");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PutAway")).Any(x=>x.Name=="From"&&x.DeclaringType.Name=="HolsterCopy"),"no still copy of the weapon put away");
  Require(Calls(p.Types.Single(x=>x.Name=="GameUiControls").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TakenByGrip"),"wheel choice with the grip not held by it");
  Require(Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="PickupTarget")).Any(x=>x.Name=="get_MainNearHolster"||x.Name=="get_RightNearHolster"),"grip at a body weapon also picks up what the hand points at");
  Require(Calls(p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveHand")).Any(x=>x.Name=="Steady"),"hand resting on the gun not steadied (shakes)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="ResetNativeReload")).Any(x=>x.Name=="CancelReload")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReload")).Count(x=>x.Name=="ResetNativeReload")>=2,"hand reload leaves the game's reload pending (crossbow shot without a bolt)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="VirtualButton")).Any(x=>x.Name=="Pressed"&&x.DeclaringType.Name=="MountedTriggers"),"mounted gun fires on one trigger");
  Require(p.Types.Single(x=>x.Name=="QualityMenu").Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Contains("Weapon in hand"),"no weapon grip setting");
  // 0.1.125: crossbow drawn again after the bolt is inserted; pointing grab with outline; knife/grenade placed by their middle.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReload")).Any(x=>x.Name=="RequestRedraw")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RequestRedraw")).Any(x=>x.Name=="TrySelectSlot")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickRedraw"),"crossbow not drawn again after a hand reload (its animator stays empty)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="WeaponPointing")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakePointed")).Any(x=>x.Name=="PickUpInto")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PickUpInto")).Any(x=>x.Name=="TryPickupItem"),"no pointing grab of weapons in the world");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponPointing").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Show"&&x.DeclaringType.Name=="ReloadOutline"),"pointed weapon has no outline");
  Require(Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="PickupTarget")).Any(x=>x.Name is "get_PointedWeapon" or "PointedWeaponBy"),"pointed weapon also picked up by the game's own ray");
  Require(Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="CenterPlaced"),"knife/grenade placed by their far fitted origin (below the belt)");
  // 0.1.126: two hands with weapons, both counts on the watch, walk-over ammunition only, left-hand grenade, grip lever.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="TickCopy")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderHolsters")).Any(x=>x.Name=="RenderHandCopies"),"a hand cannot hold a weapon copy");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeCopy")).Any(x=>x.Name=="TakeHand"&&x.DeclaringType.Name=="BodyHolsters")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="HandOver")).Any(x=>x.Name=="SwitchTo"),"hand weapon not taken from the body / not handed to the other hand");
  Require(Calls(p.Types.Single(x=>x.Name=="WristHud").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="JuggleAmmo"),"watch does not show the left hand's rounds");
  Require(weaponHandsType.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Contains("BeginInteractionCheckAndPickUp")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="AllowWeaponPickup")).Any(x=>x.Name=="AddWeaponAmmoToPool"),"walking over a weapon still picks it up (ammunition only wanted)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickLeftGrenade")).Any(x=>x.Name=="PullPin")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickLeftThrow")).Any(x=>x.Name=="LaunchProp")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenade")).Any(x=>x.Name=="TickLeftThrow"),"no left-hand grenade throw");
  Require(weaponHandsType.Methods.Single(x=>x.Name=="CheckGrenadeAmmo").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="grenadeAmmo"),"grenade count check reads the right hand's weapon");
  // 0.1.127: the crossbow stays in the hand while drawn again; no left-hand contact with a held grenade/knife.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="UpdateSelection")).Any(x=>x.Name=="get_Redrawing")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="get_Redrawing"),"crossbow disappears while drawn again");
  Require(Calls(p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveHand")).Count(x=>x.Name=="get_Profile")>=2,"left hand still collides with a held grenade");
  // 0.1.128: walk-over held back at the weapon pickup itself; either hand holds the game's weapon (mirrored), its trigger fires it; fore-end hold.
  var weaponStrings=weaponHandsType.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).ToList();
  Require(weaponStrings.Contains("TryPickupItem")&&weaponStrings.Contains("PickupItem")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="WalkOverTry")).Any(x=>x.Name=="AllowWeaponPickup"),"walking over a weapon: its pickup is not held back");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="get_PrimaryLeft")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="GunAim")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="GunAim")).Any(x=>x.Name=="HandAim")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="HandAim")).Any(x=>x.Name=="Mirrored"&&x.DeclaringType.Name=="ControllerAim"),"the game's weapon cannot be held in the left hand (mirrored)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="VirtualButton")).Any(x=>x.Name=="get_PrimaryLeft"),"a gun in the left hand does not fire on the left trigger");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TryPoseHand")).Any(x=>x.Name=="TryPoseCopyHand")&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="HandProfileFor"),"the left hand does not wrap the grip (mirrored hold)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGameWeapon")).Any(x=>x.Name=="ForeEnd"&&x.DeclaringType.Name=="HandRoles")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="Apply"&&x.DeclaringType.Name=="HandMirror"),"a weapon let go by the handle does not stay in the fore-end hand");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="SwitchTo")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="SwitchTo")).Any(x=>x.Name=="DemoteGame")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="PromoteCopies"),"a copy's trigger does not bring it into play");
  Require(Calls(p.Types.Single(x=>x.Name=="FingerPoseMath").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="MirrorDeformation"),"no mirrored finger hold");
  Require(Calls(p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveHand")).Any(x=>x.Name=="FreeHandCollides"),"the right hand does not collide with a gun held in the left");
  // 0.1.129: grenade pin drawn out of the grenade (into the other hand's fingers), click without ticking, empty hand after a throw, rest of the grenades on the chest.
  var pinTick=Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenade")).ToList();
  Require(pinTick.Any(x=>x.Name=="set_PinPulled"&&x.DeclaringType.Name=="WeaponVisual")&&pinTick.Any(x=>x.Name=="BakePin")&&pinTick.Any(x=>x.Name=="Add"&&x.DeclaringType.Name=="GrenadePins"),"the grenade pin stays on the grenade when pulled");
  Require(!pinTick.Any(x=>x.Name=="StartTimerSound")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenadeAfter")).Any(x=>x.Name=="StopClick"),"the grenade fuse keeps ticking after the pin");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenadeAfter")).Any(x=>x.Name=="TrySelectSlot")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickGrenadeAfter"),"the right hand keeps a grenade after the throw");
  Require(Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Twin")&&Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Left"),"the rest of the grenades not shown on the chest");
  Require(Calls(p.Types.Single(x=>x.Name=="NativeSkinSnapshot").Methods.Single(x=>x.Name=="Bake")).Any(x=>x.Name=="Collapse"),"hidden bones (pulled pin) still baked");
  // 0.1.130: both hands fire at once (a copy fires by itself), left weapon offset, melee with either hand, catching, fore-end + other weapon, another weapon of an owned kind.
  var copyFire=Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyFire")).ToList();
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="CopyFire")&&copyFire.Any(x=>x.Name=="FireBullet")&&copyFire.Any(x=>x.Name=="ExecuteFireTask"),"a weapon in the other hand does not fire by itself");
  Require(weaponHandsType.Methods.Single(x=>x.Name=="HitPoint").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="copyShooting")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="BeforeProjectile")).Any(x=>x.Name=="CopyShotBy")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="AimLaunch")).Any(x=>x.Name=="CopyShotBy"),"a copy's shot aims along the camera, not the copy");
  Require((Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="HandleSided")||Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="PrimaryHandPoint")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PrimaryHandPoint")).Any(x=>x.Name=="HandleSided"))&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TryPoseHand")).Any(x=>x.Name=="LeftHandle"),"left-hand weapon not offset into the palm");
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="MeleeHand")&&Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="PunchBlocked"),"only the right hand hits with a weapon");
  Require(!Calls(weaponHandsType.Methods.Single(x=>x.Name=="HandFreeForWeapon")).Any(x=>x.Name=="get_isInTransit")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="TrySelectSlot"),"a weapon tossed up cannot be caught while the game switches");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PickUpInto")).Any(x=>x.Name=="TakeAnother")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeAnother")).Any(x=>x.Name=="AddLoose"),"a ground weapon of an owned kind takes the owned one off the body");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeWeaponAt")).Any(x=>x.Name=="DemoteGame")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="Apply"&&x.DeclaringType.Name=="HandMirror"),"a weapon hanging by its fore-end is lost when the free hand takes another");
  // 0.1.131: the left hand's gun and weapons held beside the game's one reload by themselves; a copy held with both hands.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickHandReload")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyFire")).Any(x=>x.Name=="StartHandReload")&&!Calls(weaponHandsType.Methods.Single(x=>x.Name=="LeftHandReload")).Any(x=>x.Name=="StartReload"),"the left hand's gun waits for the game's reload animation (never comes)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="TickCopySupport")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="Solve"&&x.DeclaringType.Name=="GripMath"),"a copy cannot be held with both hands (passed hand to hand instead)");
  // 0.1.132: a weapon firing beside the game's one has its sound, kick and slide cycle; its still has the slide closed.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyFire")).Any(x=>x.Name=="CopyShotSound")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyShotSound")).Any(x=>x.Name=="CreateInstance"),"a weapon firing beside the game's one is silent");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyFire")).Any(x=>x.Name=="Shot"&&x.DeclaringType.Name=="HolsterCopy")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderHandCopies")).Any(x=>x.Name=="Animate"),"a weapon firing beside the game's one has no shot animation");
  Require(Calls(p.Types.Single(x=>x.Name=="HolsterCopy").Methods.Single(x=>x.Name=="From")).Any(x=>x.Name=="BakeStillMechanism"),"a still copy keeps the slide where the game left it (open)");
  // 0.1.133: no stray copy of a weapon being let go; a copy with no game weapon in the hands comes into play; a scoped copy raised to the eye; the left hold mirrored across the weapon's middle; the right hand reloads the left hand's gun; the grenade explodes once.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeWeaponAt")).Any(x=>x.Name=="get_GameKey")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PickUpInto")).Any(x=>x.Name=="get_GameKey")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Take")).Any(x=>x.Name=="Leaving"),"a hand takes a copy of the weapon the other hand is letting go");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="SwitchTo")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="ScopeRaised")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="ScopeRaised")).Any(x=>x.Name=="SwitchTo"),"a copy stays a copy with no game weapon in the hands / a scoped copy has no scope");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="HandleMiddleX")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="LeftHandle")).Any(x=>x.Name=="MirrorCenter"),"the left hand's hold is mirrored across the weapon's box, not its middle");
  var reloadTick=Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReload")).ToList();
  Require(reloadTick.Any(x=>x.Name=="get_ReloadRight")&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="ReloadHandHolding")&&!Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReloadProps")).Any(x=>x.Name=="get_ReloadRight"),"the gun in the left hand cannot be reloaded by hand (or the pouch moves)");
  // 0.1.134: another weapon of an owned kind taken from the ground comes into play like any weapon (it becomes the owned one).
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="SwapLoose")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="SwapLoose")).Any(x=>x.Name=="SwapWithOwner")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="SwapLoose")).Any(x=>x.Name=="SetAmmo")&&Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="SwapWithOwner")).Any(x=>x.Name=="Swap"&&x.DeclaringType.Name=="HolsterAssignment"),"a weapon taken from the ground stays a copy (no hand reload, no underbarrel)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="AllowWeaponPickup")).Any(x=>x.Name=="HasEquipableInSlot"),"walking over a weapon not owned yet loses its rounds");
  // 0.1.135: a weapon let go keeps the hand's turn and bounces; the left hand's long-gun hold is mirrored across its handle.
  var holsterCopy=p.Types.Single(x=>x.Name=="HolsterCopy");
  Require(Calls(holsterCopy.Methods.Single(x=>x.Name=="Fall")).Any(x=>x.Name=="Bounce"&&x.DeclaringType.Name=="ThrowMath")&&Calls(holsterCopy.Methods.Single(x=>x.Name=="Fall")).Any(x=>x.Name=="AngleAxis")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="Spin"&&x.DeclaringType.Name=="ThrowMath")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PutAway")).Any(x=>x.Name=="Drop"&&x.Parameters.Count==6),"a weapon let go falls without turning or bouncing");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="HandleMiddleX")).Any(x=>x.Name=="HandleMiddle"),"the left hand's long gun is mirrored across its box, not its handle");
  // 0.1.136: no automatic reload of a weapon in a hand with the manual reload on; the shotgun pumped with one hand by its fore-end; caught by the fore-end.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="StartHandReload")).Any(x=>x.Name=="AutoHandReload")&&!weaponHandsType.Methods.Any(x=>x.Name=="RightHandReload"),"weapons held in both hands reload by themselves with the manual reload on");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReload")).Any(x=>x.Name=="TickOneHandPump")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickOneHandPump")).Any(x=>x.Name=="InertialRack")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickOneHandPump")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="InertialPump"),"the shotgun cannot be pumped with one hand");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeWeapon")).Any(x=>x.Name=="ForeEndCatch")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeWeapon")).Any(x=>x.Name=="HoldByForeEnd")&&weaponHandsType.Methods.Single(x=>x.Name=="PromoteCopies").Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld&&i.Operand is FieldReference f&&f.Name=="foreEndOnly"),"a long gun cannot be caught by its fore-end");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="PlausibleForeEnd"),"a stray native fore-end sample puts the hand beside the gun");
  // 0.1.137: a weapon / magazine landing makes the surface's sound.
  Require(Calls(p.Types.Single(x=>x.Name=="HolsterCopy").Methods.Single(x=>x.Name=="Fall")).Any(x=>x.Name=="Hit"&&x.DeclaringType.Name=="WeaponImpactAudio")&&Calls(p.Types.Single(x=>x.Name=="ReloadDrops").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Hit")&&Calls(p.Types.Single(x=>x.Name=="WeaponImpactAudio").Methods.Single(x=>x.Name=="Surface")).Any(x=>x.Name=="ObtainSurfaceTypeFromRaycastHit"),"a thrown weapon lands silently");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PinClick")).Any(x=>x.Name=="CreateInstance")&&!Calls(weaponHandsType.Methods.Single(x=>x.Name=="PinClick")).Any(x=>x.Name=="StartTimerSound")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenadeAfter")).Any(x=>x.Name=="SettleThrown"),"the grenade's fuse sound runs on to a second explosion");
  // 0.1.138: a crossbow drawn again stays in the left hand; the shotgun let go at the handle is pumped by the pump hand; one grenade per throw, no ticking, a brisk throw; the mounted gun turns the other way sideways.
  Require(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore").Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldfld&&i.Operand is FieldReference f&&f.Name=="redrawStage"),"the left hand's gun moves to the right hand while it is drawn again");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickOneHandPump")).Any(x=>x.Name=="EndRacking")&&p.Types.Single(x=>x.Name=="InertialPump").Methods.Any(x=>x.Name=="JerkAxis"),"the shotgun let go at the handle is still racked by the grip / a level gun is not pumped by a jerk");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PropFireTask")).Any(x=>x.Name=="SpentDuplicate")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="ThrowHandleProjectile")).Any(x=>x.Name=="SpentDuplicate")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PropLaunched")).Any(x=>x.Name=="RemoveProjectile")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PropLaunched")).Any(x=>x.Name=="QuietFuse"),"a throw launches a second grenade");
  Require(weaponHandsType.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Any(i=>i.Operand is string str&&str=="HandleProjectile")&&weaponHandsType.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Any(i=>i.Operand is string str2&&str2=="StartExplosionSound")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="GrenadeExploding")).Any(x=>x.Name=="FuseVolume"),"the thrown grenade still ticks / a second launch is not guarded");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenade")).Any(x=>x.Name=="LivelyThrow")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickLeftThrow")).Any(x=>x.Name=="LivelyThrow")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="LivelyThrow")).Any(x=>x.Name=="Lively"&&x.DeclaringType.Name=="GrenadeThrow"),"the grenade still flies at the hand's speed only");
  Require(Calls(mountedGun.Methods.Single(x=>x.Name=="Lever")).Any(x=>x.Name=="SideLever")&&Calls(mountedGun.Methods.Single(x=>x.Name=="Lever")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="Smooth"),"the mounted gun still turns with the hands sideways / is not smoothed");
  // 0.1.139: NPCs react to punches with their skeleton, are pushed back, can be disarmed.
  var npcHits=p.Types.Single(x=>x.Name=="NpcHitReactions");
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Count(x=>x.Name=="Hit"&&x.DeclaringType.Name=="NpcHitReactions")>=3,"a punch does not make the NPC react");
  Require(Calls(npcHits.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="add_willRenderCanvases")&&Calls(npcHits.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="remove_willRenderCanvases"),"NPC reactions not drawn after the game's animation (before skinning)");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="HitReactionState")&&Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="AngleAxis")&&Calls(npcHits.Methods.Single(x=>x.Name=="Hit")).Any(x=>x.Name=="Plan"&&x.DeclaringType.Name=="HitReactionMath"),"the NPC skeleton is not moved by the planned reaction");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Disarm")).Any(x=>x.Name=="TryDropWeapon")&&Calls(npcHits.Methods.Single(x=>x.Name=="Disarm")).Any(x=>x.Name=="EquipWeapon")&&Calls(npcHits.Methods.Single(x=>x.Name=="Knock")).Any(x=>x.Name=="MoveOnNavMesh")&&Calls(npcHits.Methods.Single(x=>x.Name=="MoveOnNavMesh")).Any(x=>x.Name=="set_nextPosition")&&Calls(npcHits.Methods.Single(x=>x.Name=="MoveOnNavMesh")).Any(x=>x.Name=="Move"&&x.DeclaringType.Name=="NavMeshAgent"),"an NPC cannot be disarmed / pushed back");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Prepare")).Any(x=>x.Name=="BeforeCameras")&&p.Types.Single(x=>x.Name=="WeaponOptions").Fields.Any(x=>x.Name=="NpcDisarm")&&p.Types.Single(x=>x.Name=="WeaponOptions").Fields.Any(x=>x.Name=="NpcReactions"),"NPC reaction options / fallback missing");
  // 0.1.140: no endless fuse ticking; the view stays put on a mounted gun; easier/ hand-first pickups; long guns on three places; revolver hold; shovel like the mop; scope cross lined up.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGrenadeAfter")).Any(x=>x.Name=="TickFuses")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickFuses")).Any(x=>x.Name=="SilenceGameFuse")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickFuses")).Any(x=>x.Name=="Due"&&x.DeclaringType.Name=="GrenadeFuseLedger")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickFuses")).Any(x=>x.Name=="getInstanceList")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="PropFireTask")).Any(x=>x.Name=="SilenceGameFuse"),"the grenade's fuse sound can tick on for ever");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="ReadBodyAnchor")).Any(x=>x.Name=="HoldMountView"),"the view swings round with the mounted gun");
  Require(p.Types.Single(x=>x.Name=="WeaponPointing").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="Mirrored"&&x.DeclaringType.Name=="ControllerAim")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="TickPickupTake")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickPickupTake")).Any(x=>x.Name=="TakeWeapon"),"a picked-up weapon goes to its body place / the left hand points with its glove");
  Require(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip").Body.Instructions.Any(i=>i.Operand is string str&&str.Contains("no sample of the game's right hand")),"a weapon in the left hand without a right-hand sample hangs at its origin");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="CreateScope")).Any(x=>x.Name=="get_PairRollDegrees")&&p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="LongHandle"&&x.DeclaringType.Name=="PropFitPolicy"),"scope cross not lined up / the shovel not a long tool");
  // 0.1.141: blows by strength (fists 5..8, a gun 4..6); a hit NPC is stunned (no shooting/walking); an enemy's gun grabbed by the barrel; thrown weapons hit.
  var punchType=p.Types.Single(x=>x.Name=="PunchDriver");
  Require(Calls(punchType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Hits"&&x.DeclaringType.Name=="MeleeDamageMath")&&Calls(punchType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="set__MeleeDamage_k__BackingField")&&Calls(punchType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Grabbing"),"a punch still takes the game's fixed share (three blows)");
  Require(npcHits.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Any(i=>i.Operand is string str&&str=="UseWeapon")&&npcHits.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Any(i=>i.Operand is string str2&&str2=="OnAnimatorMove")&&Calls(npcHits.Methods.Single(x=>x.Name=="Hit")).Any(x=>x.Name=="StunFor")&&Calls(npcHits.Methods.Single(x=>x.Name=="StunFor")).Any(x=>x.Name=="SetStun"),"a hit NPC goes on shooting / walking");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Draw")).Any(x=>x.Name=="ApplyGrabs")&&Calls(npcHits.Methods.Single(x=>x.Name=="Draw")).Any(x=>x.Name=="TickStuns")&&Calls(npcHits.Methods.Single(x=>x.Name=="Late")).Any(x=>x.Name=="Draw")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickGrab")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickHandPickup")&&Calls(npcHits.Methods.Single(x=>x.Name=="DisarmGrabbed")).Any(x=>x.Name=="Disarm"),"an enemy's gun cannot be grabbed by the barrel and knocked out of its hands");
  Require(Calls(p.Types.Single(x=>x.Name=="HolsterCopy").Methods.Single(x=>x.Name=="Fall")).Any(x=>x.Name=="StrikeNpc")&&Calls(punchType.Methods.Single(x=>x.Name=="Thrown")).Any(x=>x.Name=="ThrownHits")&&Calls(punchType.Methods.Single(x=>x.Name=="Thrown")).Any(x=>x.Name=="Hit"&&x.DeclaringType.Name=="NpcHitReactions")&&weaponHandsType.Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="Thrown")&&weaponHandsType.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Any(i=>i.Operand is FieldReference fr&&fr.Name=="StruckNpc"),"a weapon thrown into an NPC does not hit it");
  // 0.1.142: left-hand handguns in the palm again; the left Y reloads the left hand's gun (the revolver by hand); the mounted gun's view turns with it; shoulder reach; no hidden second weapon on a place; the shovel's other hand on its stick.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="LeftHandle")).Any(x=>x.Name=="HandleX"&&x.DeclaringType.Name=="LeftHoldMath"),"the left hand's pistol is mirrored with the old shift across the measured middle (1 cm off)");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickRevolver")).Any(x=>x.Name=="get_LeftRevolverManual")&&weaponHandsType.Methods.Single(x=>x.Name=="RenderRevolver").Parameters.Count==2&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="get_LeftRevolverManual")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickLeftCopyReload"),"the revolver in the left hand cannot be reloaded / the left Y does not reload");
  Require(p.Types.Single(x=>x.Name=="GameUiControls").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="get_LeftYReloads")&&weaponHandsType.Methods.Single(x=>x.Name=="TickReload").Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="get_LeftControls"),"the left Y still selects the next weapon with a gun in the left hand");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="HoldMountView")).Any(x=>x.Name=="TryHeading")&&p.Types.Single(x=>x.Name=="QualityOptions").Fields.Any(x=>x.Name=="MountedViewTurns"),"the mounted gun's view does not turn with the gun");
  var bodyHolsters=p.Types.Single(x=>x.Name=="BodyHolsters");
  Require(Calls(bodyHolsters.Methods.Single(x=>x.Name=="TryGrab")).Any(x=>x.Name=="GrabRadiusOf")&&Calls(bodyHolsters.Methods.Single(x=>x.Name=="NearestWithin")).Any(x=>x.Name=="SnapRadiusOf")&&Calls(bodyHolsters.Methods.Single(x=>x.Name=="NearestWithin")).Any(x=>x.Name=="MoveTo"),"the places over the shoulders take a weapon from as close as the others / a gun cannot take an occupied shoulder");
  Require(!Calls(bodyHolsters.Methods.Single(x=>x.Name=="Settle")).Any(x=>x.Name=="Group")&&!Calls(bodyHolsters.Methods.Single(x=>x.Name=="NearestWithin")).Any(x=>x.Name=="Group")&&Calls(bodyHolsters.Methods.Single(x=>x.Name=="Settle")).Any(x=>x.Name=="Bump")&&Calls(bodyHolsters.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Discard")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TakeAnother")).Any(x=>x.Name=="MakeRoomForLoose"),"another weapon of a kind still hides behind the owned one on its place");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="MirrorQ")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="HandProfileFor")).Any(x=>x.Name=="get_LongGripOrigin"),"the shovel's other hand hangs beside the stick");
  // 0.1.143: NPC bones found without the game's rig reference (by name / humanoid); a hit NPC without bones is still stunned; the gun grab by the drawn gun too, with a log of why not; rope climbing x2.
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="BodyOf")).Any(x=>x.Name=="BonesOf")&&Calls(npcHits.Methods.Single(x=>x.Name=="BonesOf")).Any(x=>x.Name=="Of"&&x.DeclaringType.Name=="NpcBones")&&p.Types.Single(x=>x.Name=="NpcBones").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="Match"&&x.DeclaringType.Name=="NpcBoneNames")&&p.Types.Single(x=>x.Name=="NpcBones").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="GetBoneTransform"),"an NPC without the game's rig reference has no skeleton reaction");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Hit")).Any(x=>x.Name=="StandingSkeleton")&&!Calls(npcHits.Methods.Single(x=>x.Name=="Hit")).Any(x=>x.Name=="get_RigReference"),"an NPC without bones is not stunned");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="TryGrab")).Any(x=>x.Name=="MeshDistance")&&Calls(npcHits.Methods.Single(x=>x.Name=="TryGrab")).Any(x=>x.Name=="BonesOf")&&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="HandOccupied")).Any(x=>x.Name=="Grabbing"),"the hand goes through an enemy's gun without holding it");
  Require(Calls(p.Types.Single(x=>x.Name=="GrappleVr").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="ApplyClimbSpeed")&&Calls(p.Types.Single(x=>x.Name=="GrappleVr").Methods.Single(x=>x.Name=="ApplyClimbSpeed")).Any(x=>x.Name=="set_upwardsDownwardsVelocity"),"the rope is climbed at the game's own speed");
  // 0.1.144: the shovel's stick in the closed hands; the mounted gun turns the way the hands move and the view turns by the gun's own turn.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="Miss"&&x.DeclaringType.Name=="LongHandleMath")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="LongHandleContact")&&weaponHandsType.Methods.Single(x=>x.Name=="HandProfileFor").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="longGripClosed"),"the shovel's stick still lies beside the hands");
  var mountedType=p.Types.Single(x=>x.Name=="MountedGunVr");
  Require(!Calls(mountedType.Methods.Single(x=>x.Name=="TryHeading")).Any(x=>x.Name=="get_forward")&&mountedType.Methods.Single(x=>x.Name=="Apply").Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld&&i.Operand is FieldReference f&&f.Name=="appliedYaw"),"the mounted gun's view does not turn by the gun's own turn");
  // 0.1.145: an arm hit carries into the body behind it and the whole body leans; a disarmed enemy fights with its fists (its punches hurt, with the fists' flesh sound); a gun snatched from an enemy stays in the hand when the grip lets go.
  var hitMath=p.Types.Single(x=>x.Name=="HitReactionMath");
  Require(hitMath.Methods.Where(x=>x.Name=="Plan").SelectMany(x=>Calls(x)).Any(x=>x.Name=="BodyBehind")&&hitMath.Methods.Where(x=>x.Name=="Plan").SelectMany(x=>Calls(x)).Any(x=>x.Name=="ClassifyBody"),"a punch on an enemy's forearm does not reach its body");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="set_position")&&Calls(npcHits.Methods.Single(x=>x.Name=="BodyOf")).Any(x=>x.Name=="TopChild"),"the enemy's whole body does not lean with the blow");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Disarm")).Any(x=>x.Name=="MakeBrawler")&&Calls(npcHits.Methods.Single(x=>x.Name=="MakeBrawler")).Any(x=>x.Name=="BodyOf")&&Calls(npcHits.Methods.Single(x=>x.Name=="FistsPose")).Any(x=>x.Name=="SetupWeaponTransition"),"a disarmed enemy still stands holding an invisible gun");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Land")).Any(x=>x.Name=="ReceiveDamage"&&x.DeclaringType.Name=="PlayerState")&&Calls(npcHits.Methods.Single(x=>x.Name=="FistSound")).Any(x=>x.Name=="Play"&&x.DeclaringType.Name=="PropStudioSound")&&Calls(npcHits.Methods.Single(x=>x.Name=="PunchContact")).Any(x=>x.Name=="Land")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickBrawl"),"a disarmed enemy's punches do not hurt the player");
  Require(npcHits.Methods.Single(x=>x.IsConstructor&&!x.IsStatic).Body.Instructions.Any(i=>i.Operand is string s&&s=="PlayerHurt")&&npcHits.Methods.Single(x=>x.IsConstructor&&!x.IsStatic).Body.Instructions.Any(i=>i.Operand is string s&&s=="NpcUseMelee"),"the game's own punches of a disarmed enemy are not heard");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="TickHandPickup")).Any(x=>x.Name=="KeepSnatched")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickGameWeapon")).Any(x=>x.Name=="KeptSnatched")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="KeptSnatched"),"a gun snatched from an enemy falls when the grip lets go");
  // 0.1.146: the crossbow rail holds the hand; left-handed mirroring; two held guns collide and knock; pistol places switchable; NPC reactions every frame with a fists guard and punch; shots push bodies; a deliberate grab; the mounted gun's view raised.
  Require(Calls(p.Types.Single(x=>x.Name=="ManualReloadState").Methods.Single(x=>x.Name=="Step")).Any(x=>x.Name=="RailSplit")&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="RestOnRail"),"the hand still sinks through the crossbow's rail");
  Require(Calls(p.Types.Single(x=>x.Name=="WristHud").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="set_AmmoFace")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="LeftHandedDraw")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TickReloadProps")).Any(x=>x.Name=="get_LeftHanded"),"left-handed: the watches, the pouch or the wheel weapon not mirrored");
  var contactRig=p.Types.Single(x=>x.Name=="ContactRig");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyCollides")).Any(x=>x.Name=="ResolveCopy")&&Calls(contactRig.Methods.Single(x=>x.Name=="KnockCheck")).Any(x=>x.Name=="Knock"&&x.DeclaringType.Name=="WeaponImpactAudio")&&Calls(p.Types.Single(x=>x.Name=="HolsterCopy").Methods.Single(x=>x.Name=="From")).Any(x=>x.Name=="HeldShape"),"a second gun in the hand still has no collision / no knock");
  Require(p.Types.Single(x=>x.Name=="QualityOptions").Fields.Any(x=>x.Name=="BeltPistols")&&p.Types.Single(x=>x.Name=="QualityOptions").Fields.Any(x=>x.Name=="ArmpitPistols")&&Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="Sync")).Any(x=>x.Name=="Replace"),"the pistol places cannot be switched off");
  Require(Calls(p.Types.Single(x=>x.Name=="Bootstrap").Methods.Single(x=>x.Name=="LateUpdate")).Any(x=>x.Name=="FromLateUpdate")&&Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="BrawlPose")&&Calls(npcHits.Methods.Single(x=>x.Name=="BrawlPose")).Any(x=>x.Name=="TwoBone")&&Calls(npcHits.Methods.Single(x=>x.Name=="FistsPose")).Any(x=>x.Name=="AnimOf"),"NPC reactions only at the canvas moment / a disarmed enemy without its guard");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="PushBodies")).Any(x=>x.Name=="Shot"&&x.DeclaringType.Name=="NpcHitReactions")&&Calls(npcHits.Methods.Single(x=>x.Name=="Shot")).Any(x=>x.Name=="AddForceAtPosition"),"a shot body lying still does not move");
  Require(npcHits.Methods.Single(x=>x.Name=="TickGrab").Body.Instructions.Any(i=>i.Operand is string s&&s.Contains("a fist, not a grab"))&&p.Types.Single(x=>x.Name=="NpcBones").Methods.Where(x=>x.HasBody).SelectMany(x=>Calls(x)).Any(x=>x.Name=="JointName"),"an enemy's gun snatched by a swinging fist / a sound effect taken for the head");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="HoldMountView")).Any(x=>x.Name=="get_MountRaise"),"the mounted gun's view is not raised");
  // 0.1.147: guns touching do not shake (one way, steadied); the belt mirrored for a left-hander; enemies drawn by the mod from their bent bones; a fist fight on the spot; bodies on the ground woken before a push; no disarm by a punch unless switched on.
  Require(!contactRig.Methods.Single(x=>x.Name=="ResolveGun").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="copyShapes")&&Calls(contactRig.Methods.Single(x=>x.Name=="ResolveCopy")).Any(x=>x.Name=="Steady"),"two guns touching still push each other in turn (they shook)");
  Require(Calls(p.Types.Single(x=>x.Name=="AmmoPouch").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="set_localScale"),"the belt is only moved to the right for a left-hander, not mirrored");
  var overlay=p.Types.Single(x=>x.Name=="NpcSkinOverlay");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="BeforeCameras")).Any(x=>x.Name=="Draw"&&x.DeclaringType.Name=="NpcSkinOverlay")&&Calls(overlay.Methods.Single(x=>x.Name=="Draw")).Any(x=>x.Name=="BakeMesh")&&Calls(overlay.Methods.Single(x=>x.Name=="Draw")).Any(x=>x.Name=="set_forceRenderingOff")&&Calls(npcHits.Methods.Single(x=>x.Name=="BodyOf")).Any(x=>x.Name=="Create"&&x.DeclaringType.Name=="NpcSkinOverlay"),"a reacting enemy is still drawn by the game in its animated pose");
  Require(Calls(p.Types.Single(x=>x.Name=="NpcBones").Methods.Single(x=>x.Name=="Of")).Any(x=>x.Name=="get_bones")&&Calls(p.Types.Single(x=>x.Name=="NpcBones").Methods.Single(x=>x.Name=="Of")).Any(x=>x.Name=="ByName"),"the reactions are not put on the bones the enemy is drawn with");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="TickBrawl")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="BrawlPlan")&&Calls(npcHits.Methods.Single(x=>x.Name=="PauseAi")).Any(x=>x.Name=="set_enabled")&&!npcHits.Methods.Any(x=>x.Name=="Chase"),"a disarmed enemy still runs at the player instead of fighting");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="PushDown")).Any(x=>x.Name=="WakeRagdoll")&&Calls(npcHits.Methods.Single(x=>x.Name=="Shot")).Any(x=>x.Name=="WakeRagdoll")&&Calls(npcHits.Methods.Single(x=>x.Name=="WakeRagdoll")).Any(x=>x.Name=="ActivateRagdollBehaviour"),"a body on the ground is pushed while its animation or its sleep holds it");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Hit")).Any(x=>x.Name=="get_Value")&&npcHits.Methods.Single(x=>x.Name=="Hit").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="NpcPunchDisarm"),"a punch still knocks the enemy's gun out");
  // 0.1.148: a brawler's legs walk (the game's animator handler run by the mod), its fists clenched, its punches fast with the body behind them, five punches at least to knock out.
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Legs")).Any(x=>x.Name=="Update"&&x.DeclaringType.Name=="AIAnimatorHandler")&&Calls(npcHits.Methods.Single(x=>x.Name=="Legs")).Any(x=>x.Name=="PlayWalkAnimation")&&Calls(npcHits.Methods.Single(x=>x.Name=="FadeLegs")).Any(x=>x.Name=="CrossFadeInFixedTime")&&Calls(npcHits.Methods.Single(x=>x.Name=="PauseAi")).Any(x=>x.Name=="set_applyRootMotion"),"a paused enemy's legs are asked to walk but nothing runs the game's animator handler");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="Clench"&&x.DeclaringType.Name=="NpcFist")&&Calls(npcHits.Methods.Single(x=>x.Name=="Release")).Any(x=>x.Name=="Release"&&x.DeclaringType.Name=="NpcFist")&&Calls(p.Types.Single(x=>x.Name=="NpcFist").Methods.Single(x=>x.Name=="Create")).Any(x=>x.Name=="get_bindposes"),"a fighting enemy's fists are not clenched");
  Require(Calls(npcHits.Methods.Single(x=>x.Name=="BrawlPose")).Any(x=>x.Name=="HookArc")&&Calls(npcHits.Methods.Single(x=>x.Name=="BrawlPose")).Any(x=>x.Name=="Cock")&&Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Count(x=>x.Name=="Punch"&&x.DeclaringType.Name=="BrawlMath"||x.Name=="Reach"&&x.DeclaringType.Name=="Brawler")>=2&&Calls(npcHits.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="set_position"),"the enemy's punch has no cock, hook or lunge");
  var punchDriver=p.Types.Single(x=>x.Name=="PunchDriver");var punchCalls=Calls(punchDriver.Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList();
  int guardBegins=punchCalls.IndexOf("PunchBegins"),guardedDamage=guardBegins<0?-1:punchCalls.FindIndex(guardBegins,n=>n=="ApplyDamageTo"),guardEnds=guardedDamage<0?-1:punchCalls.FindIndex(guardedDamage,n=>n=="PunchEnds");
  Require(punchCalls.Contains("Share")&&guardBegins>=0&&guardedDamage>guardBegins&&guardEnds>guardedDamage,"a punch's damage is not held to a fifth of full health around the game's damage");
  Require(npcHits.Methods.Where(x=>x.Name.Contains("PatchGuard")).SelectMany(Calls).Any(x=>x.Name=="Patch")&&npcHits.Methods.Single(x=>x.Name=="PatchGuard").Body.Instructions.Any(i=>i.Operand is string s&&s=="Knockout")&&npcHits.Methods.Single(x=>x.Name=="GuardKnockout").ReturnType.Name=="Boolean"&&Calls(npcHits.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="PatchGuard"),"the game may still knock an enemy out with the first punch to the head");
  // 0.1.149: rare freezes are told apart in the log; the game runs and is played without Windows focus in VR; knives and throwable things are held by the grip and thrown by letting go in a swing, no trigger; a grip that took nothing says why.
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="FreezeWatch")&&Calls(p.Types.Single(x=>x.Name=="Bootstrap").Methods.Single(x=>x.Name=="Write")).Any(x=>x.Name=="Note"&&x.DeclaringType.Name=="FreezeWatch")&&Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="FreezeWatch"),"freezes are not watched");
  Require(Calls(p.Types.Single(x=>x.Name=="FreezeWatch").Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="CollectionCount"&&x.DeclaringType.FullName=="Il2CppSystem.GC")&&Calls(p.Types.Single(x=>x.Name=="FreezeWatch").Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="get_isFocused"),"a freeze is not told from the game's memory clean-up or a lost focus");
  var focus=p.Types.Single(x=>x.Name=="WindowFocus");
  Require(Calls(focus.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="set_runInBackground")&&Calls(p.Types.Single(x=>x.Name=="Bootstrap").Methods.Single(x=>x.Name=="Update")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="WindowFocus"),"the game still stops without Windows focus in VR");
  foreach(var (type,method) in new[]{("LocomotionDriver","Allowed"),("WeaponHands","CanControl"),("InteractionDriver","Allowed"),("GripCarry","TickBody")})
   Require(!p.Types.Single(x=>x.Name==type).Methods.Where(x=>x.Name==method).SelectMany(Calls).Any(x=>x.Name=="get_isFocused")&&p.Types.Single(x=>x.Name==type).Methods.Where(x=>x.Name==method).SelectMany(Calls).Any(x=>x.Name=="get_Playable"),type+"."+method+" still stops without Windows focus");
  Require(!p.Types.SelectMany(t=>t.Methods.Concat(t.NestedTypes.SelectMany(n=>n.Methods))).SelectMany(Calls).Any(x=>x.DeclaringType.Name=="KnifeThrowGesture"&&x.Name=="Sample"),"a knife or a bottle is still thrown with the trigger gesture");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="TickGameWeapon")).Any(x=>x.Name=="KnifeGrip")&&Calls(sceneHands.Methods.Single(x=>x.Name=="KnifeGrip")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="ThrowGrip")&&Calls(sceneHands.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="TickCopyKnife")&&Calls(sceneHands.Methods.Single(x=>x.Name=="StartCopyKnifeThrow")).Any(x=>x.Name=="TrySelectSlot"),"the knife is not held by the grip and thrown by letting go (either hand)");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="UpdatePropThrow")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="ThrowGrip")&&Calls(sceneHands.Methods.Single(x=>x.Name=="UpdatePropThrow")).Any(x=>x.Name=="LaunchProp"),"a bottle is not held by the grip and thrown by letting go");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="SampleSwings")&&Calls(sceneHands.Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList() is var swingCalls&&swingCalls.IndexOf("SampleSwings")<swingCalls.IndexOf("TickHolsters"),"the hand's swing is not sampled before the grip is looked at");
  Require(Calls(p.Types.Single(x=>x.Name=="LooseProps").Methods.Single(x=>x.Name=="Step")).Any(x=>x.Name=="ReportMiss"),"a grip that takes nothing does not say why");
  // 0.1.150: the view recentered when the mounted gun is taken; a broom / shovel lasts several blows; a left-hander takes things, keys and medkits with the left hand (its menu chord on the right hand); a bottle's throw and the game's own drop never both act on one release.
  var mountView=p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="HoldMountView");
  Require(mountView.Body.Instructions.Any(i=>i.Operand is string s&&s.StartsWith("MOUNTED GUN view recentered"))&&mountView.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld&&i.Operand is FieldReference f&&f.Name=="recenter"),"taking the mounted gun does not recenter the view");
  var durableCalls=Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList();
  Require(durableCalls.Contains("DurableProp")&&durableCalls.Contains("PropHits")&&durableCalls.Contains("WearHeldProp")&&durableCalls.Contains("BreakHeldProp")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="WearHeldProp")).Any(x=>x.Name=="BreakHeldProp"),"a broom or a shovel still breaks on its first blow");
  var mainInteraction=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(Calls(mainInteraction.Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="get_MainControls")&&Calls(mainInteraction.Methods.Single(x=>x.Name=="PrepareRay")).Any(x=>x.Name=="get_MainRight")&&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="DropPosition")).Any(x=>x.Name=="get_LeftHanded"),"a left-hander still takes things and opens doors with the right hand");
  foreach(var (type,method) in new[]{("GloveVisual","Pose"),("WheelItems","Render"),("WheelItems","Tick"),("KeyUnlockGesture","Tick"),("GameUiControls","Tick"),("VrPromptLabels","Source"),("WristHud","PoseGlove")})
   Require(Calls(p.Types.Single(x=>x.Name==type).Methods.Single(x=>x.Name==method)).Any(x=>x.Name=="get_LeftHanded"),"left-handed: "+type+"."+method+" still right-handed");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Count(x=>x.Name=="HandLedGrip")>=2&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="MirrorQ"),"a thing held in the left hand is not held mirrored");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="UpdatePropThrow")).Any(x=>x.Name=="Forget"&&x.DeclaringType.Name=="GripCarry")&&!Calls(weaponHandsType.Methods.Single(x=>x.Name=="UpdatePropThrow")).Any(x=>x.Name=="TryDropCurrentEquipableAndAmmo"),"a bottle's throw and the game's own drop both act on one release");
  // 0.1.151: the mod's memory cleaned in small steps; things held while the grip is held, by either hand; a wider disarm; "Don't hit your bro"; ceiling fans.
  var pacer=p.Types.Single(x=>x.Name=="ModGcPacer");
  Require(Calls(pacer.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Collect"&&x.DeclaringType.FullName=="System.GC")&&Calls(pacer.Methods.Single(x=>x.Name=="Start")).Any(x=>x.Name=="set_LatencyMode")&&Calls(p.Types.Single(x=>x.Name=="FreezeWatch").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="ModGcPacer")&&Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="ModGcPacer"),"the mod's memory is not cleaned in small steps");
  var carryType=p.Types.Single(x=>x.Name=="GripCarry");
  Require(Calls(carryType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_GripMode")&&p.Types.Single(x=>x.Name=="CarryGripState").Methods.Single(x=>x.Name=="Sample").Parameters.Count==7,"a thing is not held only while the grip is held");
  var sampleCalls=Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="Sample")).ToList();
  Require(sampleCalls.Count(x=>x.Name=="Sample"&&x.DeclaringType.Name=="InteractionState")>=2&&sampleCalls.Any(x=>x.Name=="ThingTakenBy")&&sampleCalls.Any(x=>x.Name=="PressedBy")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="LeftHandedDraw")).Any(x=>x.Name=="ThingToPressingHand"),"a thing cannot be taken with the other hand's grip");
  Require(p.Types.Single(x=>x.Name=="NpcHitReactions").Methods.Single(x=>x.Name=="TryGrab").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="rightWrist"||i.Operand is MethodReference m&&m.Name=="get_rightWrist"),"an enemy's gun hand does not count as its gun");
  var friendlyPunch=p.Types.Single(x=>x.Name=="PunchDriver");
  // 0.1.174: "Don't hit your bro" removed: no level ends for a blow to a friend, no setting for it.
  Require(!p.Types.Any(x=>x.Name=="FriendlyHit")&&!Calls(friendlyPunch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.DeclaringType.Name=="FriendlyHit")&&!p.Types.Single(x=>x.Name=="QualityMenu").Methods.SelectMany(m=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<string>():Array.Empty<string>()).Contains("Don't hit your bro"),"\"Don't hit your bro\" is still there");
  var climbType=p.Types.Single(x=>x.Name=="HandClimbing");var climbCalls=Calls(climbType.Methods.Single(x=>x.Name=="Tick")).ToList();
  Require(climbCalls.Any(x=>x.Name=="TryGrab"&&x.DeclaringType.Name=="CeilingFans")&&climbCalls.Any(x=>x.Name=="Anchor")&&climbCalls.Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="CeilingFans")&&Calls(p.Types.Single(x=>x.Name=="Bootstrap").Methods.Single(x=>x.Name=="LateUpdate")).Any(x=>x.Name=="LateTick"&&x.DeclaringType.Name=="CeilingFans")&&Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="Look")).Any(x=>x.Name=="ConsumeTurn"),"a ceiling fan cannot be ridden");
  // 0.1.152: a knife copy held like the right hand's knife (mirrored in the left hand), the holds kept between games; the left-hander's pouch outline on the belt; a long stick timed by its end.
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="TryPoseCopyHand")).Any(x=>x.Name=="HeldCopy")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="KnifeHold")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="HandProfileFor")).Any(x=>x.Name=="HeldCopy"),"a knife in the left hand still lies through the hand");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="LoadGrips")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="SaveGrips")&&weaponHandsType.Methods.Any(x=>Calls(x).Any(c=>c.Name=="RememberGrip")),"the weapon holds are not kept between games");
  Require(Calls(p.Types.Single(x=>x.Name=="ReloadOutline").Methods.Single(x=>x.Name=="Show")).Any(x=>x.Name=="get_determinant"),"a mirrored reload outline (the left-hander's pouch) is still misplaced");
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TryLongStickEnd"),"a shovel is still timed by the hand, not its end");
  // 0.1.153: a failed level (a blow to a friend) gets the black death screen with its own panel only; one panel on death; things in the left hand mirrored with the hand; knife and grenade copies follow the hand; livelier brawlers with clenched fists; the other hand's grip takes any thing.
  var deathVr153=p.Types.Single(x=>x.Name=="DeathScreenVr");var deathScan153=Calls(deathVr153.Methods.Single(x=>x.Name=="Scan")).ToList();
  Require(deathScan153.Any(x=>x.Name=="Observe"&&x.DeclaringType.Name=="LevelFailWatch")&&deathScan153.Any(x=>x.Name=="get_Active"&&x.DeclaringType.Name=="LevelFailWatch")&&p.Types.Single(x=>x.Name=="DeathPanelLayout").Methods.Single(x=>x.Name=="Overlay").Parameters.Count==5
   &&Calls(p.Types.Single(x=>x.Name=="LevelFailWatch").Methods.Single(x=>x.Name=="Install")).Any(x=>x.Name=="Patch")&&deathVr153.Methods.Where(x=>x.IsConstructor).SelectMany(Calls).Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="LevelFailWatch"),"a failed level (not a death) still shows the game's crooked screen");
  Require(!deathVr153.Methods.Any(x=>x.Name=="FreeLayer")&&Calls(deathVr153.Methods.Single(x=>x.Name=="Show")).Any(x=>x.Name=="QuietLayer")&&Calls(deathVr153.Methods.Single(x=>x.Name=="QuietLayer")).Any(x=>x.Name=="QuietLayer"&&x.DeclaringType.Name=="DeathPanelLayout"),"the death panel is still on the UI layer (a second window over it)");
  Require(!Calls(deathVr153.Methods.Single(x=>x.Name=="UpdateTexts")).Any(x=>x.DeclaringType.Name=="FriendlyHit"),"the failed level still speaks of a blow to a friend");
  var eyeSave=Calls(p.Types.Single(x=>x.Name=="EyeCapture").Methods.Single(x=>x.Name=="Save")).ToList();
  Require(eyeSave.Any(x=>x.Name=="AsSpan")&&!eyeSave.Any(x=>x.Name=="op_Implicit"&&x.ReturnType.FullName=="System.Byte[]"),"an eye capture still copies its pixels one by one (a 0.7 s freeze)");
  var fingerMath=p.Types.Single(x=>x.Name=="FingerPoseMath");
  Require(Calls(fingerMath.Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="OtherProceduralPose")&&fingerMath.Methods.Any(x=>x.Name=="HeldPose")&&!fingerMath.Methods.Single(x=>x.Name=="ChairSignature").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="rimRevision"),"a chair or an ashtray in the left hand is not held as the right hand holds it");
  Require(Calls(weaponHandsType.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="MirrorGeometry")&&Calls(weaponHandsType.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="Rotation"&&x.DeclaringType.Name=="GloveVisual"),"a thing / a knife or a grenade in the left hand is not mirrored with the hand");
  var brawlHits=p.Types.Single(x=>x.Name=="NpcHitReactions");
  Require(Calls(brawlHits.Methods.Single(x=>x.Name=="MakeBrawler")).Any(x=>x.Name=="From"&&x.DeclaringType.Name=="BrawlStyle")&&brawlHits.Methods.Any(x=>x.Name=="Step"&&x.Parameters.Count==5)&&Calls(p.Types.Single(x=>x.Name=="NpcFist").Methods.Single(x=>x.Name=="Clench")).Any(x=>x.Name=="Slerp"),"brawlers still all alike, fists not clenched");
  Require(Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="OffPickupTarget")).Any(x=>x.Name=="OtherTarget"),"the other hand's grip still takes only things to hit with");
  // 0.1.154: a ceiling fan lit and taken by pointing a hand at it (the player carried to the blade); a long stick swung in one hand armed by the still hand.
  var fansType=p.Types.Single(x=>x.Name=="CeilingFans");
  Require(Calls(fansType.Methods.Single(x=>x.Name=="Aim")).Any(x=>x.Name=="RayHit"&&x.DeclaringType.Name=="FanMath")&&Calls(p.Types.Single(x=>x.Name=="HandClimbing").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Pull"&&x.DeclaringType.Name=="FanMath")
   &&Calls(p.Types.Single(x=>x.Name=="HandClimbing").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="CeilingFans"&&x.Parameters.Count==8),"a ceiling fan still lights up only when the hand is in its blades");
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Sample"&&x.DeclaringType.Name=="PunchMotion"&&x.Parameters.Count==5),"a shovel in one hand is still never armed");
  // 0.1.155: the menus' pointer on the hand whose trigger was pulled last (either trigger clicks); the VR SETTINGS button keeps the menu list in its room.
  var menuPointer=p.Types.Single(x=>x.Name=="VrMenuPointer");var menuPointerCalls=Calls(menuPointer.Methods.Single(x=>x.Name=="Tick")).ToList();
  Require(menuPointerCalls.Any(x=>x.Name=="get_MenuPointerControls")&&menuPointerCalls.Any(x=>x.Name=="PointerRotation")&&menuPointerCalls.Any(x=>x.Name=="DisarmMenuTriggers")&&!menuPointerCalls.Any(x=>x.Name=="get_MenuRightControls")
   &&Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="SamplePointerHand")).Any(x=>x.Name=="get_PointerLeft")&&p.Types.Single(x=>x.Name=="VrTracking").Methods.Single(x=>x.Name=="get_MenuLeftControls").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="leftChannels")
   &&Calls(p.Types.Single(x=>x.Name=="DeathScreenVr").Methods.Single(x=>x.Name=="Point")).Any(x=>x.Name=="get_MenuPointerControls")&&Calls(p.Types.Single(x=>x.Name=="VrSettingsPage").Methods.Single(x=>x.Name=="Point")).Any(x=>x.Name=="get_MenuPointerControls"),"the menu pointer is still on the right hand only");
  Require(Calls(p.Types.Single(x=>x.Name=="VrSettingsPage").Methods.Single(x=>x.Name=="Ensure")).Any(x=>x.Name=="KeepRoom")&&Calls(p.Types.Single(x=>x.Name=="VrSettingsPage").Methods.Single(x=>x.Name=="KeepRoom")).Any(x=>x.Name=="Plan"&&x.DeclaringType.Name=="MenuFitMath"),"the VR SETTINGS button still pushes Quit out of the menu");
  // 0.1.156: a knife copy drawn unmirrored and in its held pose; its left throw kept on the body; the stealth arcs follow the head.
  var still=p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="StillParts");
  Require(Calls(still).Any(x=>x.Name=="Scale")&&Calls(still).Any(x=>x.Name=="BakePose")&&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="PutAway")).Any(x=>x.Name=="HeldPoseOf"),"a knife copy is still drawn mirrored or in the pose it was put away in");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="TickCopyKnifeThrow")).Any(x=>x.Name=="TransformPoint")&&Calls(p.Types.Single(x=>x.Name=="SpotIndicators").Methods.Single(x=>x.Name=="After")).Any(x=>x.Name=="Corrected"&&x.DeclaringType.Name=="SpotMath")
   &&p.Types.Single(x=>x.Name=="CameraRig").Methods.Where(x=>x.IsConstructor).SelectMany(Calls).Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="SpotIndicators"),"the left knife's late throw or the stealth arcs still ignore the body / the head");
  // 0.1.156: the left hand's weapon pushes doors; a stick in both hands: held by either grip, strikes an enemy it was stopped at, dropped by the mod when the game keeps it; a grenade copy held in the fist.
  Require(Calls(p.Types.Single(x=>x.Name=="PhysicalDoors").Methods.Single(x=>x.Name=="Step")).Any(x=>x.Name=="TryMeleeTip"&&x.Parameters.Count==2)&&Calls(p.Types.Single(x=>x.Name=="PhysicalDoors").Methods.Single(x=>x.Name=="Step")).Any(x=>x.Name=="MeleeHand"),"a door still ignores the weapon in the left hand");
  Require(Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Either"&&x.DeclaringType.Name=="CarryGripState")&&Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TouchingNpc")
   &&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="CheckDrop")).Any(x=>x.Name=="PutDownNow")&&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="TakeOutOfHands")).Any(x=>x.Name=="DismissProp"),"a shovel in both hands still drops or misses, or stays in the air when let go");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="GrenadeContact"),"a grenade copy still floats over the pointing hand");
  // 0.1.157: an aim calibration that is no forward grip is refused (and a saved one dropped); Shift+F5 starts one, not F5 alone.
  var aimType=p.Types.Single(x=>x.Name=="ControllerAim");
  Require(Calls(aimType.Methods.Single(x=>x.Name=="Calibrate")).Any(x=>x.Name=="PlausibleCalibration")&&Calls(aimType.Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="PlausibleCalibration"),"a calibration with the controller held any way is still kept");
  Require(p.Types.Single(x=>x.Name=="Bootstrap").Methods.SelectMany(Calls).Count(x=>x.Name=="GetKey"&&x.DeclaringType.Name=="Input")>=2,"F5 alone still starts an aim calibration");
  // 0.1.158: a thing let go of is put down at once where it is drawn, falling straight (not the game's thrown drop, not shoved by the body); the menu chord in either order, Escape's refusal written and the pause menu opened directly.
  var carry158=p.Types.Single(x=>x.Name=="GripCarry");
  Require(Calls(carry158.Methods.Single(x=>x.Name=="ReleaseItem")).Any(x=>x.Name=="PutDown")&&Calls(carry158.Methods.Single(x=>x.Name=="PutDownNow")).Any(x=>x.Name=="TryHeldPropCenter")
   &&Calls(carry158.Methods.Single(x=>x.Name=="IgnorePlayer")).Any(x=>x.Name=="IgnoreCollision")&&Calls(carry158.Methods.Single(x=>x.Name=="Restore")).Any(x=>x.Name=="IgnoreCollision")
   &&Calls(carry158.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Settle")&&!carry158.Methods.SelectMany(Calls).Any(x=>x.Name=="SetPositionAndDropObject"),"a thing let go of still waits in the hand, then is thrown from the player");
  var ui158=p.Types.Single(x=>x.Name=="GameUiControls");
  Require(Calls(ui158.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Sample"&&x.DeclaringType.Name=="MenuChord")&&Calls(ui158.Methods.Single(x=>x.Name=="OpenGameMenu")).Any(x=>x.Name=="TryOpen"&&x.DeclaringType.Name=="GamePause")
   &&Calls(p.Types.Single(x=>x.Name=="EscapeKey").Methods.Single(x=>x.Name=="SendKey")).Any(x=>x.Name=="BringForward")&&Calls(p.Types.Single(x=>x.Name=="GamePause").Methods.Single(x=>x.Name=="TryOpen")).Any(x=>x.Name=="TriggerPauseSequence"),"the menu chord still fails silently");
  // 0.1.159: the sound and the hit direction strip follow the head; the grenade copy held with the game's own hold; the left grenade throw kept on the body, its heading toward the look.
  var rig159=p.Types.Single(x=>x.Name=="CameraRig");var ctor159=rig159.Methods.Where(x=>x.IsConstructor).SelectMany(Calls).ToList();
  Require(ctor159.Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="HeadListener")&&ctor159.Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="DamageIndicators")
   &&Calls(rig159.Methods.Single(x=>x.Name=="RenderCanvases")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="DamageIndicators")
   &&Calls(p.Types.Single(x=>x.Name=="HeadListener").Methods.Single(x=>x.Name=="After")).Any(x=>x.Name=="setListenerAttributes")
   &&Calls(p.Types.Single(x=>x.Name=="DamageIndicators").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Strip"&&x.DeclaringType.Name=="SpotMath"),"the sound or the hit strip still ignore the head's turn");
  var hands159=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(hands159.Methods.Single(x=>x.Name=="LoadGrips")).Any(x=>x.Name=="get_GrenadeHold")&&Calls(hands159.Methods.Single(x=>x.Name=="StartLeftThrow")).Any(x=>x.Name=="InverseTransformPoint")
   &&Calls(hands159.Methods.Single(x=>x.Name=="TickLeftThrow")).Any(x=>x.Name=="TransformPoint")&&Calls(hands159.Methods.Single(x=>x.Name=="StartLeftThrow")).Any(x=>x.Name=="AimedThrow")
   &&Calls(hands159.Methods.Single(x=>x.Name=="TickGrenade")).Any(x=>x.Name=="AimedThrow")&&Calls(hands159.Methods.Single(x=>x.Name=="AimedThrow")).Any(x=>x.Name=="TowardLook"),"the left grenade still upside down, or its throw still left from the right hand's side");
  // 0.1.160: a two-handed gun in one hand turned toward the other hand (a gun stock), by the VR setting.
  var hands160=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(hands160.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="GunAim")&&Calls(hands160.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="GunAim")
   &&Calls(hands160.Methods.Single(x=>x.Name=="TickCopySupport")).Any(x=>x.Name=="GunAim")&&Calls(hands160.Methods.Single(x=>x.Name=="GunAim")).Any(x=>x.Name=="get_GunTurnDegrees")
   &&Calls(hands160.Methods.Single(x=>x.Name=="GunAim")).Any(x=>x.Name=="ForeEnd"),"a two-handed gun in one hand is still not turned toward the other hand");
  // 0.1.161: a wooden knock for the broom; a broom with no physics body falls flat; a dropped gun never below the floor.
  Require(Calls(p.Types.Single(x=>x.Name=="PropImpactAudio").Methods.Single(x=>x.Name=="Surface")).Any(x=>x.Name=="Knock")&&Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Knock")
   &&Calls(p.Types.Single(x=>x.Name=="PropImpactAudio").Methods.Single(x=>x.Name=="Surface")).Any(x=>x.Name=="WoodenProp"),"the broom still strikes without a sound");
  var carry161=p.Types.Single(x=>x.Name=="GripCarry");
  Require(Calls(carry161.Methods.Single(x=>x.Name=="Place")).Any(x=>x.Name=="Lay")&&Calls(carry161.Methods.Single(x=>x.Name=="Lay")).Any(x=>x.Name=="Level"&&x.DeclaringType.Name=="LayFlatMath")
   &&Calls(carry161.Methods.Single(x=>x.Name=="Lay")).Any(x=>x.Name=="TryHeldPropHead")&&Calls(carry161.Methods.Single(x=>x.Name=="Settle")).Any(x=>x.Name=="TickFalls"),"a broom let go of still hangs in the air");
  var copy161=p.Types.Single(x=>x.Name=="HolsterCopy");
  Require(Calls(copy161.Methods.Single(x=>x.Name=="Fall")).Any(x=>x.Name=="CheckFloor")&&Calls(copy161.Methods.Single(x=>x.Name=="CheckFloor")).Any(x=>x.Name=="FloorUnder"),"a dropped gun still falls through the floor");
  // 0.1.162: the crossbow string's cocked shape only from a cocked string; scene searches kept between rare runs; the mission start spread over frames.
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="PrepareString")).Count(x=>x.Name=="Cocked"&&x.DeclaringType.Name=="CrossbowStringMath")>=2,"an uncocked string shape is still taken for the cocked one");
  foreach(var (type,method) in new[]{("StoryVideo","Tick"),("DeathScreenVr","Scan"),("EnemyAi","Scan"),("WeaponPointing","Scan"),("CameraRig","DiscoverStoryScenes"),("WristHud","Discover"),("InteractionDriver","GrappleAssist")})
   Require(!Calls(p.Types.Single(x=>x.Name==type).Methods.Single(x=>x.Name==method)).Any(x=>x.Name is "FindObjectsOfTypeAll" or "FindObjectsOfType"),type+"."+method+" still searches the whole scene each time");
  var find162=p.Types.Single(x=>x.Name=="SceneFind`1");
  Require(Calls(find162.Methods.Single(x=>x.Name=="Refresh"&&x.Parameters.Count==2)).Any(x=>x.Name=="Due"&&x.DeclaringType.Name=="SceneScan"),"kept scene searches bypass the one-search-per-frame gate");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="CheckScene")).Any(x=>x.Name=="SceneChanged"&&x.DeclaringType.Name=="SceneScan")
   &&Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="SceneHooks"),"kept searches not renewed after a scene change, or the game's hooks not installed");
  Require(Calls(p.Types.Single(x=>x.Name=="ReloadAudio").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Make")&&!Calls(p.Types.Single(x=>x.Name=="ReloadAudio").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="createSound"),"all reload recordings still made in one frame");
  Require(Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="HalfRate")
   &&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="End"&&x.DeclaringType.Name=="StepClock")
   &&Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Prepare")).Any(x=>x.Name=="End"&&x.DeclaringType.Name=="StepClock"),"no log of the half-rate hold or of the slow steps");
  // 0.1.163: the rope swing goes where the head looks (turned into the character's frame on both input paths).
  var loco163=p.Types.Single(x=>x.Name=="LocomotionDriver");
  Require(Calls(loco163.Methods.Single(x=>x.Name=="Move")).Any(x=>x.Name=="RopeSwingInBody")&&Calls(loco163.Methods.Single(x=>x.Name=="RunningAxis")).Any(x=>x.Name=="RopeSwingInBody")
   &&Calls(loco163.Methods.Single(x=>x.Name=="RopeSwingInBody")).Any(x=>x.Name=="InBodyFrame"&&x.DeclaringType.Name=="HeadingMath"),"the rope swing still goes along the character's forward, not where the player looks");
  // 0.1.164: taking a gun no longer reads bone names per vertex (40-90 ms each time); cameras timed; graphics settings logged.
  var visual164=p.Types.Single(x=>x.Name=="WeaponVisual");
  var perVertex=visual164.Methods.Where(m=>m.Name.Contains("g__Matches")||m.Name.Contains("g__Match|")||m.Name.Contains("g__Round")).ToList();
  Require(perVertex.Count>=3&&perVertex.All(m=>!Calls(m).Any(c=>c.Name is "get_name" or "IsChildOf")),"the per-vertex bone checks still read the game's bones ("+string.Join(",",perVertex.Select(m=>m.Name))+")");
  Require(Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="CameraTiming")
   &&Calls(p.Types.Single(x=>x.Name=="TrackedCamera").Methods.Single(x=>x.Name=="OnPostRender")).Any(x=>x.Name=="Add"&&x.DeclaringType.Name=="CameraTiming")
   &&Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Discover")).Any(x=>x.Name=="Level"&&x.DeclaringType.Name=="GraphicsReport"),"camera times or graphics settings not in the log");
  // 0.1.165: the memory each part of the mod allocates, the holster step's parts, a pickup's outline shape per kind of weapon.
  Require(Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="AllocParts")
   &&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="TickHolsters")).Any(x=>x.Name=="End"&&x.DeclaringType.Name=="StepClock")
   &&p.Types.Single(x=>x.Name=="WeaponPointing").Fields.Any(x=>x.Name=="bakedByKind"),"no allocation or holster step log, or a pickup's outline still built per pickup");
  // 0.1.166: when drawing starts and ends in each frame, and how much of the world the mod knows, in the PERF line.
  Require(Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Frames"&&x.DeclaringType.Name=="CameraTiming")
   &&Calls(p.Types.Single(x=>x.Name=="FramePerformance").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Report"&&x.DeclaringType.Name=="WorldStats")
   &&Calls(p.Types.Single(x=>x.Name=="TrackedCamera").Methods.Single(x=>x.Name=="OnPreCull")).Any(x=>x.Name=="PreCull"&&x.DeclaringType.Name=="CameraTiming"),"frame drawing times or world counts not in the log");
  // 0.1.166: at a ladder, what the game was given for its movement is written (LADDER).
  Require(Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="Move")).Any(x=>x.Name=="Ladder")
   &&Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="Ladder")).Any(x=>x.Name=="HasClimbVolumeDetected"),"no ladder log line");
  // 0.1.167: a hostage who dies or is knocked out in the hands is let go of at once.
  Require(Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="get_actorStatus")&&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="get_bodyNPC"),"a dead hostage still stays in the hands");
  // 0.1.168: an enemy the player disarmed is taken hostage from any side; its fight and stun end first.
  var carry168=p.Types.Single(x=>x.Name=="GripCarry");var hits168=p.Types.Single(x=>x.Name=="NpcHitReactions");
  Require(carry168.Methods.Single(x=>x.Name=="HostageAllowed").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="Disarmed")
   &&Calls(carry168.Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="BeforeHostage")&&Calls(carry168.Methods.Single(x=>x.Name=="TryTriggerHostage")).Any(x=>x.Name=="BeforeHostage")
   &&Calls(hits168.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="LinkHostages")&&Calls(hits168.Methods.Single(x=>x.Name=="TakenHostage")).Any(x=>x.Name=="EndBrawl"),"a disarmed enemy still only taken hostage from behind");
  // 0.1.169: a grip press near a body place that took nothing is written (how far off).
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="NearestShown"),"no log of a missed holster grab");
  // 0.1.170: in water the ladder check never runs (the swim is written instead: SWIM MOVE/SLOW with what the character touched); under water a hand does not take hold of a ladder.
  var loco170=p.Types.Single(x=>x.Name=="LocomotionDriver");
  Require(Calls(loco170.Methods.Single(x=>x.Name=="Move")).Any(x=>x.Name=="SwimGiven")
   &&loco170.Methods.Single(x=>x.Name=="Ladder").Body.Instructions.Take(8).Any(i=>i.Operand is FieldReference f&&f.Name=="inWater")
   &&Calls(loco170.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="SwimReport")
   &&Calls(loco170.Methods.Single(x=>x.Name=="SwimReport")).Any(x=>x.Name=="get_collisionFlags")
   &&Calls(p.Types.Single(x=>x.Name=="HandClimbing").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_Submerged"),"the ladder check still runs in water, no swim log, or a hand takes a ladder under water");
  // 0.1.171: a weapon on the ground: each hand's own target, taken by reaching it too, a press a moment after the aim slipped, a wider look on a press, a tick in the hand; the other hand's grip at a pointed weapon takes no key; ladder: the stick as it is on the game's ladder, over the top with the hands.
  var point171=p.Types.Single(x=>x.Name=="WeaponPointing");var hands171=p.Types.Single(x=>x.Name=="WeaponHands");var climb171=p.Types.Single(x=>x.Name=="HandClimbing");
  Require(Calls(point171.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Best")&&Calls(point171.Methods.Single(x=>x.Name=="Best")).Any(x=>x.Name=="SqrDistance")
   &&Calls(hands171.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="Takeable")&&Calls(hands171.Methods.Single(x=>x.Name=="TickHolstersCore")).Any(x=>x.Name=="ResistanceHaptics")
   &&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="OffPickupTarget")).Any(x=>x.Name=="PointedWeaponBy"),"taking a weapon from the ground still needs exact pointing, or the other hand's grip takes a key instead");
  Require(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="Move").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="onLadder")
   &&Calls(climb171.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickOverTop")&&Calls(climb171.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TryOverTop")
   &&Calls(climb171.Methods.Single(x=>x.Name=="TryOverTop")).Any(x=>x.Name=="Landing")&&Calls(climb171.Methods.Single(x=>x.Name=="TickOverTop")).Any(x=>x.Name=="Velocity"&&x.DeclaringType.Name=="LadderTopMath"),"the ladder stick still turned by the head, or no way over the top");
  // 0.1.172: a gun in close combat hits with its whole shape (stock, barrel), a copy with its other end too.
  var hands172=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(hands172.Methods.Single(x=>x.Name=="get_MeleeShape")).Any(x=>x.Name=="Firearm")
   &&Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TryMeleeOtherEnd"),"a gun still hits only with its muzzle point");
  // 0.1.173: a blow with a gun or a thing on an enemy gets the fists' thud and comic; the left stick keeps moving into and out of the water.
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Body"&&x.DeclaringType.Name=="PropImpactAudio")
   &&Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="UpdateWater")).Any(x=>x.Name=="WaterChanged")
   &&!Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="UpdateWater")).Any(x=>x.Name=="Reset"&&x.DeclaringType.Name=="LocomotionState"),"no body sound/comic for weapon blows, or the stick still stops at the water");
  // 0.1.174: a gun in the water sinks (the water is no floor); a thrown gun hits with its ends too and never hangs on a wall; a wider pistol support reach with a press grace; a shorter shotgun pump stroke; steadying with hysteresis and one-way hands; OpenComposite (OpenXR) recognised.
  var copy174=p.Types.Single(x=>x.Name=="HolsterCopy");
  Require(Calls(copy174.Methods.Single(x=>x.Name=="Surface"&&x.Parameters.Count==6)).Any(x=>x.Name=="IsWater")&&copy174.Methods.Single(x=>x.Name=="Fall").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="waterY")
   &&Calls(copy174.Methods.Single(x=>x.Name=="StrikeNpc")).Count(x=>x.Name=="StrikeNpcFrom")>=3,"a gun still floats on the water or a thrown gun hits only with its middle");
  Require(p.Types.Single(x=>x.Name=="PistolSupport").Fields.Any(x=>x.Name=="sincePress")&&p.Types.Single(x=>x.Name=="ManualReloadState").Fields.Any(x=>x.Name=="maxBack")
   &&p.Types.Single(x=>x.Name=="ContactRig").Fields.Any(x=>x.Name=="handSteadyValid")&&Calls(p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveHand")).Any(x=>x.Name=="get_LeftHanded"),"pistol support, pump stroke or contact steadying unchanged");
  var vr174=p.Types.Single(x=>x.Name=="OpenVrTracking");
  Require(vr174.Methods.Where(x=>x.IsConstructor&&x.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&t=="VR_IsInterfaceVersionValid")
   &&vr174.Methods.Single(x=>x.Name=="TryAttachActions").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="get_OpenComposite"),"OpenComposite not recognised");
  // 0.1.175: each tutorial hint once per game (TutorialController.ShowMessage hooked).
  Require(weaponHandsType.Methods.Where(m=>m.IsConstructor&&m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&t=="ShowMessage")
   &&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TutorialShow")).Any(x=>x.Name=="Allow"&&x.DeclaringType.Name=="TutorialOnce"),"a tutorial hint still shown again and again");
  // 0.1.178: under OpenComposite the log says which interface versions the OpenXR package's openvr_api.dll adapted.
  Require(vr174.Methods.Where(x=>x.IsConstructor&&x.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&t=="XIIIVR_ShimReport"),"the OpenXR shim's report is not logged");
  // 0.1.179: the whole tutorial step (the weapon wheel it opens) once; a weapon is not taken through walls or doors.
  Require(weaponHandsType.Methods.Where(m=>m.IsConstructor&&m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&t=="ExecuteInteraction")
   &&Calls(weaponHandsType.Methods.Single(x=>x.Name=="TutorialStep")).Any(x=>x.Name=="AllowStep"&&x.DeclaringType.Name=="TutorialOnce"),"the weapon wheel tutorial step still runs again");
  var pointing179=p.Types.Single(x=>x.Name=="WeaponPointing");
  Require(Calls(pointing179.Methods.Single(x=>x.Name=="Best")).Any(x=>x.Name=="Visible")&&Calls(pointing179.Methods.Single(x=>x.Name=="Clear"&&x.Parameters.Count==3)).Any(x=>x.Name=="RaycastNonAlloc")
   &&Calls(pointing179.Methods.Single(x=>x.Name=="Visible")).Any(x=>x.Name=="Seen"&&x.DeclaringType.Name=="PointingMath"),"a weapon is still taken through walls");
  // 0.1.180: one install for every runtime: the setting reaches the runtime switch before VR starts; its report is logged whatever the runtime.
  Require(Calls(p.Types.Single(x=>x.Name=="Plugin").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="Load"&&x.DeclaringType.Name=="RuntimeOptions")
   &&Calls(p.Types.Single(x=>x.Name=="RuntimeOptions").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="SetEnvironmentVariable"),"the VR runtime setting does not reach the runtime switch");
  Require(vr174.Methods.Where(x=>x.IsConstructor&&x.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference m&&m.Name=="TryGetExport")
   &&!vr174.Methods.Where(x=>x.IsConstructor&&x.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&t.Contains("OpenXR shim report")),"the runtime switch report still logged only under OpenComposite");
  // 0.1.181: the mod's own OpenXR first (Unity's OpenXR plugin + xiii_openxr.dll), the OpenVR way as the fallback; the rig reads either through VrTracking.
  var boot181=p.Types.Single(x=>x.Name=="Bootstrap");
  Require(Calls(boot181.Methods.Single(x=>x.Name=="StartXR")).Any(x=>x.Name=="StartOpenXr")&&Calls(boot181.Methods.Single(x=>x.Name=="StartXR")).Any(x=>x.Name=="StartOpenVr")
   &&boot181.Methods.Single(x=>x.Name=="StartOpenXr").Body.Instructions.Any(i=>i.Operand is string t&&t=="OpenXR Display"),"the own OpenXR is not started first");
  var loader181=p.Types.Single(x=>x.Name=="OpenXrLoader");
  Require(new[]{"main_LoadOpenXRLibrary","NativeConfig_SetProcAddressPtrAndLoadStage1","session_InitializeSession","session_CreateSessionIfNeeded","session_BeginSession","XO_Hook","XO_Locate","XO_Sync","XO_Vibrate"}.All(n=>loader181.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is string t&&t==n)),"the OpenXR loader misses a native call");
  Require(p.Types.Single(x=>x.Name=="OpenXrTracking").BaseType?.Name=="VrTracking"&&p.Types.Single(x=>x.Name=="OpenVrTracking").BaseType?.Name=="VrTracking"
   &&p.Types.Single(x=>x.Name=="CameraRig").Fields.Single(x=>x.Name=="tracking").FieldType.Name=="VrTracking","the rig still reads only OpenVR");
  // 0.1.182: resolution 100% by default (the old 75% moved once); the cutscene bars widened and no longer take the view up for down.
  Require(p.Types.Single(x=>x.Name=="QualityOptions").Methods.Single(x=>x.Name=="Load").Body.Instructions.Any(i=>i.Operand is string t&&t=="RenderScaleDefaultsVersion")
   &&Calls(p.Types.Single(x=>x.Name=="CinematicMask").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Widen"),"resolution default or cutscene bars unchanged");
  // 0.1.183: pistols in both hands reload each with its own hand: its button drops the magazine, the grip struck against the chest puts a full one in.
  var hands183=p.Types.Single(x=>x.Name=="WeaponHands");var tick183=hands183.Methods.Single(x=>x.Name=="TickChestReload");
  Require(Calls(hands183.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TickChestReload")&&Calls(tick183).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="ChestStrike")
   &&Calls(tick183).Any(x=>x.Name=="ChestEject")&&Calls(tick183).Any(x=>x.Name=="ChestInsert")&&Calls(hands183.Methods.Single(x=>x.Name=="ChestInsert")).Any(x=>x.Name=="QuickLoad")
   &&Calls(hands183.Methods.Single(x=>x.Name=="StartHandReload")).Any(x=>x.Name=="ChestReloads")&&Calls(hands183.Methods.Single(x=>x.Name=="AllowStockReload")).Any(x=>x.Name=="get_ChestGame")
   &&Calls(hands183.Methods.Single(x=>x.Name=="TickHandReload")).Any(x=>x.Name=="QuickLoad"),"pistols in both hands still cannot reload each with its own hand");
  var copy183=p.Types.Single(x=>x.Name=="HolsterCopy");
  Require(Calls(copy183.Methods.Single(x=>x.Name=="From")).Any(x=>x.Name=="PrepareReload")&&Calls(copy183.Methods.Single(x=>x.Name=="ShowMagazine")).Any(x=>x.Name=="SetTriangles")
   &&Calls(copy183.Methods.Single(x=>x.Name=="Clone")).Any(x=>x.Name=="Keep"&&x.DeclaringType.Name=="ReloadMesh")
   &&p.Types.Single(x=>x.Name=="WristHud").Methods.SelectMany(Calls).Any(x=>x.Name=="get_WatchReloadLabel"),"a pistol held as a copy still keeps its magazine drawn when it is out (or the watch does not say what to do)");
  // 0.1.188: in the water the game's own camera pitch is set level (the first mission starts with it pitched); weapons made ready for the hands before they are taken; a shotgun copy pumped by a jerk; the hook kept in the hand until the game's is drawn.
  var loco185=p.Types.Single(x=>x.Name=="LocomotionDriver");
  Require(loco185.Methods.SelectMany(Calls).Any(x=>x.Name=="LevelGamePitch")&&Calls(loco185.Methods.Single(x=>x.Name=="LevelGamePitch")).Any(x=>x.Name=="set_cameraInputTargetRot")
   &&!loco185.Methods.Any(x=>x.Name=="SwimVelocity"&&x.Parameters.Count!=2)&&!p.Types.Any(x=>x.Name=="SwimPace"),"the game's pitch still left as the mission started it (or the 0.1.184 swim still in)");
  var holsters185=p.Types.Single(x=>x.Name=="BodyHolsters");var hands185=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(holsters185.Methods.Single(x=>x.Name=="BuildMissing")).Any(x=>x.Name=="FromKept")&&Calls(holsters185.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="Keep")
   &&Calls(hands185.Methods.Single(x=>x.Name=="PickUpInto")).Any(x=>x.Name=="Rush")&&Calls(hands185.Methods.Single(x=>x.Name=="Take")).Any(x=>x.Name=="BuildNow")
   &&Calls(hands185.Methods.Single(x=>x.Name=="TickHolstersCore")).Count(x=>x.Name=="BuildMissing")>=2,"weapons still built only when taken, one every 1.5 s");
  Require(Calls(hands185.Methods.Single(x=>x.Name=="TickCopy")).Any(x=>x.Name=="TickCopyPump")&&Calls(hands185.Methods.Single(x=>x.Name=="CopyFire")).Any(x=>x.Name=="CopyUnpumped")
   &&Calls(hands185.Methods.Single(x=>x.Name=="CopyHandPose")).Any(x=>x.Name=="CopyPumpShift"),"a shotgun held as a copy still cannot be pumped");
  var grapple185=p.Types.Single(x=>x.Name=="GrappleVr");
  Require(Calls(grapple185.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Drawn")&&Calls(grapple185.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="BuildPreview"),"the hook still gone from the hand while the game's is not drawn");
  // 0.1.188: a tiny small medkit on the left forearm's cut, a large one on the right's; the other hand's grip takes it into that hand (held by the grip, its trigger heals).
  var arms186=p.Types.Single(x=>x.Name=="ArmMedkits");var items186=p.Types.Single(x=>x.Name=="WheelItems");var ui186=p.Types.Single(x=>x.Name=="GameUiControls");
  Require(Calls(ui186.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="ArmMedkits")
   &&Calls(p.Types.Single(x=>x.Name=="WristHud").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Render"&&x.DeclaringType.Name=="ArmMedkits")
   &&Calls(arms186.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="TryCropEnd")&&Calls(arms186.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="PoseSmall")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="TryCropEnd")).Any(x=>x.Name=="TryCropWorld")
   &&Calls(arms186.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="HandTakesItem")&&Calls(arms186.Methods.Single(x=>x.Name=="Take")).Any(x=>x.Name=="TakeFromArm")
   &&items186.Methods.Single(x=>x.Name=="Tick").Body.Instructions.Any(i=>i.Operand is string t&&t.Contains("back on the forearm"))
   &&Calls(ui186.Methods.Single(x=>x.Name=="get_PendingConsumable")).Any(x=>x.Name=="get_ArmSide")
   &&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="HandOccupied")).Any(x=>x.Name=="ArmHeld")
   &&p.Types.Single(x=>x.Name=="NativeHandVisual").Methods.Single(x=>x.Name=="Build").Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="set_CropRest"),"no medkits to take from the forearms' cuts");
  // 0.1.117: grenades are held like knives/props (hand on the controller), and thrown along the controller.
  var poseCalls=Calls(knifeHands.Methods.Single(x=>x.Name=="RenderPose")).Select(x=>x.Name).ToList();
  Require(poseCalls.Contains("HandLedGrip")&&poseCalls.Count(n=>n=="Rotation")>=2,"grenade hand not aligned with the controller");
  // 0.1.149: the throw from the hand's path in tracking space, by letting go of the grip.
  Require(Calls(knifeHands.Methods.Single(x=>x.Name=="SampleSwings")).Any(x=>x.Name=="SampleRightRelative")&&Calls(knifeHands.Methods.Single(x=>x.Name=="SampleSwings")).Any(x=>x.Name=="SampleLeftRelative")&&Calls(knifeHands.Methods.Single(x=>x.Name=="SwingThrow")).Any(x=>x.DeclaringType.Name=="SwingRelease"&&x.Name=="Release"),"knife gesture uses tracking-space hand motion");
  var carryInteraction=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(!Calls(carryInteraction.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="get_HidesLeft")&&Calls(carryInteraction.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="get_NativeDropInput"),"right grip is not globally consumed by left-hand carry");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="UpdateMarkers")).Any(x=>x.Name=="get_HidesLeft"),"hidden carried left hand also hides tracking marker");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="VirtualButton")).Any(x=>x.Name=="get_HoldingBody")&&Calls(sceneHands.Methods.Single(x=>x.Name=="AllowButtonMelee")).Any(x=>x.Name=="get_HoldingBody"),"native carry actions do not require a tracked fist weapon visual");
  Require(Calls(sceneHands.Methods.Single(x=>x.Name=="TickReload")).Any(x=>x.Name=="get_HidesLeft")&&Calls(sceneHands.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="get_HidesLeft"),"occupied offhand cannot start reload/support gestures");
  var eyeFx=p.Types.Single(x=>x.Name=="StoryColorEffect");
  Require(!eyeFx.Methods.Single(x=>x.Name=="Install").Body.Instructions.Any(i=>i.Operand is string s&&s=="OnGraphStop"),"never detour the IL2CPP shared empty OnGraphStop implementation");
  Require(!eyeFx.Fields.Any(x=>x.FieldType.FullName=="UnityEngine.RenderTexture"),"story color owns no shared eye image/history");
  Require(!eyeFx.Methods.SelectMany(Calls).Any(x=>x.Name is "set_targetTexture" or "SetStereoViewMatrix" or "SetStereoProjectionMatrix" or "Submit"),"story color cannot redirect cameras or overwrite stereo matrices");
  Require(!p.Types.Single(x=>x.Name=="TrackedCamera").Methods.Any(x=>x.Name=="OnRenderImage"),"image effect no longer depends on injected OnRenderImage registration");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="Image")).Any(x=>x.Name=="TryGetValue")&&Calls(eyeFx.Methods.Single(x=>x.Name=="Image")).Any(x=>x.Name=="Blit"),"native effect hook is ownership scoped and preserves passthrough");
  Require(!eyeFx.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name is "ColorGradingRenderer" or "PostProcessRenderContext"),"color effect is independent of native volume/LUT baking pipeline");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="ReleaseTemporary"),"scoped final eye target released after each callback");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="Probe")).Any(x=>x.Name=="ReadPixels"),"actual GPU grade and flash are verified with synthetic inputs at runtime");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="TimelineFlash")).Any(x=>x.DeclaringType.Name=="StoryEffectState"&&x.Name=="Pulse"),"Timeline-only flash has a bounded independent envelope");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="IsCurrentActiveSceneFlashback"),"flashback checkpoints use native active-scene identity");
  Require(Calls(eyeFx.Methods.Single(x=>x.Name=="SampleColor")).Any(x=>x.DeclaringType.Name=="StoryVolumeColor"&&x.Name=="Read"),"native animated saturation reaches the per-eye color adapter");
  var volumeReader=p.Types.Single(x=>x.Name=="StoryVolumeColor");
  Require(!volumeReader.Methods.SelectMany(Calls).Any(x=>x.Name is "set_enabled" or "set_value" or "UpdateSettings" or "FindObjectsOfTypeAll"),"volume adapter neither changes native rendering/settings nor globally scans Unity objects");
  Require(eyeFx.Methods.Single(x=>x.Name=="KeepEnabled").Body.ExceptionHandlers.Any(),"effect sampling errors cannot suspend head tracking");
  var loose=p.Types.Single(x=>x.Name=="LooseProps");
  Require(!loose.Methods.SelectMany(Calls).Any(x=>x.Name is "SetParent"),"held loose props are not parented to the controller");
  Require(Calls(loose.Methods.Single(x=>x.Name=="Release")).Any(x=>x.Name=="set_useGravity"),"loose release restores gravity");
  Require(!loose.Methods.SelectMany(Calls).Any(x=>x.Name=="Discover"),"loose props never scan scene renderers");
  var worldProps=p.Types.Single(x=>x.Name=="WorldPropBodies");
  Require(!worldProps.Methods.SelectMany(Calls).Any(x=>x.Name is "AddComponent" or "Prepare" or "FindObjectsOfTypeAll"),"props use existing bodies only; no mesh extraction/cooking");
  var climb=p.Types.Single(x=>x.Name=="HandClimbing");
  Require(!climb.Methods.SelectMany(Calls).Any(x=>x.Name is "set_position" or "set_enabled"),"climbing keeps native collision controller enabled and does not teleport");
  // 0.1.188: the render HMD, not the frozen desktop arm bone, places a held NPC.
  var carry188=p.Types.Single(x=>x.Name=="GripCarry");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Prepare")).Any(x=>x.DeclaringType.Name=="GripCarry"&&x.Name=="RenderBody"),"head anchor runs after the render head pose is sampled");
  Require(carry188.Methods.Single(x=>x.Name=="RenderBody").Body.Instructions.Any(x=>x.Operand is FieldReference f&&f.Name=="HeadPosition")&&Calls(carry188.Methods.Single(x=>x.Name=="RenderBody")).Any(x=>x.Name=="ApplyBodySize"),"carry uses live HMD and actual skeleton pivot");
  Require(Calls(carry188.Methods.Single(x=>x.Name=="ReleaseBody")).Any(x=>x.Name=="BeforeBodyRelease"),"release clears carry anchor lifecycle");
  Require(Calls(carry188.Methods.Single(x=>x.Name=="BeginBody")).Any(x=>x.Name=="RememberBodySize"),"pickup captures world size before native TakeBody/TakeHostage");
  Require(Calls(carry188.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="InstallBodyAnchor"),"native drop completion installs authored scale restoration");
  Require(Calls(carry188.Methods.Single(x=>x.Name=="EndSizeRestore")).Any(x=>x.DeclaringType.Name=="CarryBodyScale"&&x.Name=="Restore"),"native drop scale reset is followed by exact authored restoration");
  var interact188=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(Calls(interact188.Methods.Single(x=>x.Name=="EndRay")).Any(x=>x.Name=="SkipCarriedNpc")&&Calls(interact188.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="LinkCarryInteraction"),"native free-hand pickup integration is installed");
  Require(Calls(p.Types.Single(x=>x.Name=="ContactSolver").Methods.Single(x=>x.Name=="Solve")).Any(x=>x.Name=="EndSolve"),"physics candidate cache cannot outlive a solve");
  Require(Calls(p.Types.Single(x=>x.Name=="Bootstrap").Methods.Single(x=>x.Name=="StopXR")).Any(x=>x.DeclaringType.Name=="RenderBudget"&&x.Name=="Release"),"XR shutdown restores the viewport cap");
  var carry=p.Types.Single(x=>x.Name=="GripCarry");
  Require(!carry.Methods.SelectMany(Calls).Any(x=>x.Name=="set_bodySpawnBone"),"carrying retains native NPC/arm attachment");
  Require(Calls(carry.Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="get_LeftControls")&&!Calls(carry.Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.DeclaringType.Name=="ScoopGesture"),"left grip directly drives native hostage acquisition without a gesture");
  Require(Calls(carry.Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="Knockout"),"released hostage uses native knockout after drop transition");
  // 0.1.191: a held hostage is never killed by a hit (his life raised for the one hit, left a sliver), and falls lifeless by his shooter once let go of;
  // he is held pressed against the player, the hand on his neck; Russian back among the game's voice languages (selectable only).
  var begin191=carry.Methods.Single(x=>x.Name=="BeginHostageDamage");var end191=carry.Methods.Single(x=>x.Name=="EndHostageDamage");
  Require(Calls(begin191).Any(x=>x.Name=="set_currentHP")&&Calls(begin191).Any(x=>x.Name=="set_receivedDamagesAreAlwaysLethal")
   &&Calls(end191).Any(x=>x.Name=="Applied"&&x.DeclaringType.Name=="HostageShieldMath")&&Calls(end191).Any(x=>x.Name=="Left"&&x.DeclaringType.Name=="HostageShieldMath")&&Calls(end191).Any(x=>x.Name=="set_currentHP")
   &&Calls(carry.Methods.Single(x=>x.Name=="FinishRelease")).Any(x=>x.Name=="Die")&&Calls(carry.Methods.Single(x=>x.Name=="TickBody")).Any(x=>x.Name=="FinishRelease")
   &&Calls(carry.Methods.Single(x=>x.Name=="ReleaseBody")).Any(x=>x.Name=="MortalRelease"),"a hostage still dies in the hands (or does not die when let go of wounded to death)");
  var voice191=p.Types.Single(x=>x.Name=="AudioLanguages").Methods.Single(x=>x.Name=="Install").Body.Instructions.Where(i=>i.Operand is string).Select(i=>(string)i.Operand).ToList();
  Require(voice191.Contains("GetAllAudioLanguages")&&voice191.Contains("GetLocalizedBank")&&Calls(p.Types.Single(x=>x.Name=="Plugin").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="Install"&&x.DeclaringType.Name=="AudioLanguages")
   &&!p.Types.Single(x=>x.Name=="AudioLanguages").Methods.SelectMany(Calls).Any(x=>x.Name is "SetAudioLanguage" or "set_CurrentAudioLanguage"),"Russian voice not selectable (or chosen for the player)");
  // 0.1.192: the chosen Russian voice really plays: a Russian row in the game's bank table (the level's banks, switching away),
  // the chosen voice used when the game names none, and every voice family left with only the chosen language's bank after a switch.
  var voice192=p.Types.Single(x=>x.Name=="AudioLanguages");MethodDefinition V(string n)=>voice192.Methods.Single(x=>x.Name==n);
  Require(voice191.Contains("UnloadLocalizedBanks")&&voice191.Contains("LoadBanks")
   &&Calls(V("Table")).Any(x=>x.Name==".ctor"&&x.DeclaringType.Name=="LanguageFmod")&&Calls(V("Table")).Any(x=>x.Name=="get_languageDictionary")&&Calls(V("Table")).Any(x=>x.Name=="RussianTable")
   &&Calls(V("RussianBank")).Any(x=>x.Name=="Effective")&&Calls(V("BeforeBank")).Any(x=>x.Name=="Table")
   &&Calls(V("Reconcile")).Any(x=>x.Name=="Swaps")&&Calls(V("Reconcile")).Any(x=>x.Name=="UnloadBank"&&x.DeclaringType.Name=="RuntimeManager")&&Calls(V("Reconcile")).Any(x=>x.Name=="LoadBank"&&x.DeclaringType.Name=="RuntimeManager")&&Calls(V("Reconcile")).Any(x=>x.Name=="set_Item")
   &&Calls(V("AfterSwitch")).Any(x=>x.Name=="Reconcile")&&Calls(V("AfterLoad")).Any(x=>x.Name=="Reconcile")&&Calls(V("CurrentCode")).Any(x=>x.Name=="get_CurrentAudioLanguageCode")
   &&!voice192.Methods.SelectMany(Calls).Any(x=>x.Name is "SetAudioLanguage" or "set_CurrentAudioLanguage" or "SetLanguageAndCode"),"the chosen Russian voice does not replace the English one (or Russian is chosen for the player)");
  // 0.1.193: a hostage let go of drops at once (the game's drop completion right away: no fists, no knock-out animation),
  // clear of the player; the grappling hook on the hidden left arm's wrist stays visible (the grapple shows it in the hand).
  var drop193=carry.Methods.Single(x=>x.Name=="DropAtOnce"&&x.HasBody);
  Require(Calls(carry.Methods.Single(x=>x.Name=="ReleaseBody")).Any(x=>x.Name=="DropAtOnce")
   &&Calls(drop193).Any(x=>x.Name=="HandleBodyDropAnimationComplete"&&x.DeclaringType.Name is "PlayerHostageController" or "PlayerCarryAIController")&&Calls(drop193).Any(x=>x.Name=="SetBodyAnimation")
   &&Calls(drop193).Any(x=>x.Name=="Invoke"&&x.DeclaringType.Name=="NPCDelegate")&&Calls(drop193).Any(x=>x.Name=="ClearOfPlayer")
   &&!Calls(drop193).Any(x=>x.Name is "ReleaseBody" or "TrySelectSlot")
   &&Calls(carry.Methods.Single(x=>x.Name=="ClearOfPlayer")).Any(x=>x.Name=="DropPush"),"a hostage let go of still stands up and is knocked out before falling");
  var visible193=p.Types.Single(x=>x.Name=="FirstPersonVisibility");
  Require(Calls(visible193.Methods.Single(x=>x.Name=="Collect")).Any(x=>x.Name=="OwnVisibility")&&Calls(visible193.Methods.Single(x=>x.Name=="OwnVisibility")).Any(x=>x.Name=="Gadget"),"the grappling hook hidden with the left arm (gone from the hand once fired)");
  // 0.1.194: crossbows shoot where their sight looks: no hip spread, the bolt launched to reach the sighted point despite gravity/drag.
  var hands194=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H(string n)=>hands194.Methods.Single(x=>x.Name==n);
  bool Str(TypeDefinition t,string v)=>t.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string o&&o==v);
  Require(Str(hands194,"AimedBolt")&&Calls(H("AimedBolt")).Any(x=>x.Name=="StraightBolt")
   &&Calls(H("StraightBolt")).Any(x=>x.Name=="Launch"&&x.DeclaringType.Name=="BallisticMath")&&Calls(H("StraightBolt")).Any(x=>x.Name=="set_velocity")
   &&Calls(H("SupportedSpread")).Any(x=>x.Name=="Multiplier"&&x.DeclaringType.Name=="SpreadPolicy"),"the crossbow bolt still falls below the sighted point (or spreads)");
  // The silenced pistol fitted as the plain one (its silencer left out of the fit).
  var visual194=p.Types.Single(x=>x.Name=="WeaponVisual");MethodDefinition W(string n)=>visual194.Methods.Single(x=>x.Name==n);
  Require(Calls(W("Build")).Any(x=>x.Name=="TrimAttachment")&&Calls(W("TrimAttachment")).Any(x=>(x.Name=="WithoutAttachment"||x.Name=="Split")&&x.DeclaringType.Name=="WeaponGeometry")&&Calls(W("TrimAttachment")).Any(x=>x.Name=="get_boneWeights"),"the silenced pistol still fitted smaller and shifted (its silencer in the fit)");
  // The Uzi: magazine and top knob by hand; the Uzi and the game's two pistols reload against the chest.
  Require(Calls(H("GamePistolIn")).Any(x=>x.Name=="ChestMagazine")&&Calls(p.Types.Single(x=>x.Name=="HolsterCopy").Methods.Single(x=>x.Name=="From")).Any(x=>x.Name=="ChestMagazine")
   &&Calls(H("ChestReloads")).Any(x=>x.Name=="DualChest")&&Calls(H("ChestAmmo")).Any(x=>x.Name=="DualLeftAmmo")&&Calls(H("ChestButt")).Any(x=>x.Name=="DualLeftButt")
   &&Calls(H("AllowStockReload")).Any(x=>x.Name=="get_DualChestOn")&&Calls(H("AllowStockReload")).Any(x=>x.Name=="OwnsAmmo")
   &&Calls(H("FireHand")).Any(x=>x.Name=="DualBlocksFire")&&Calls(H("RenderLeftPistol")).Any(x=>x.Name=="PoseDualChest")&&Calls(H("TickDual")).Any(x=>x.Name=="get_DualChestOn")
   &&Calls(H("ReleaseDualFire")).Any(x=>x.Name=="CancelWaitAfterFire"),"the game's two pistols (or the Uzi) not reloaded against the chest");
  // The double-barrelled shotgun: laid along its barrels, drawn held still, opened/loaded/shut by hand.
  Require(Calls(W("Build")).Any(x=>x.Name=="PrepareBreak")&&Calls(W("PrepareBreak")).Any(x=>x.Name=="Axis"&&x.DeclaringType.Name=="BreakRig")&&Calls(W("PrepareBreak")).Any(x=>x.Name=="Chambers"&&x.DeclaringType.Name=="BreakRig")
   &&Calls(W("RefreshAnimation")).Any(x=>x.Name=="BakeBreak")&&Calls(W("BakeBreak")).Any(x=>x.Name=="BakePose")&&Calls(W("get_GameWorldToFitted")).Any(x=>x.Name=="BreakCorrection")
   &&Calls(H("TickReload")).Any(x=>x.Name=="TickBreak")&&Calls(H("RenderReload")).Any(x=>x.Name=="RenderBreak")
   &&Calls(H("TickBreak")).Any(x=>x.Name=="OpenGun")&&Calls(H("TickBreak")).Any(x=>x.Name=="Flick")&&Calls(H("TickBreak")).Any(x=>x.Name=="Insert")&&Calls(H("TickBreak")).Any(x=>x.Name=="EjectBreakCase")&&Calls(H("TickBreak")).Any(x=>x.Name=="SetMagazine")
   &&Calls(H("Shot")).Any(x=>x.Name=="get_BreakReady")&&Calls(H("SelectedShotSound")).Any(x=>x.Name=="BreakGun")&&Calls(H("TickCopyPump")).Any(x=>x.Name=="CopyBreaks"),"the double-barrelled shotgun still askew, pumped, or not reloaded by hand");
  // 0.1.195: the Uzi's top handle drawn back with its bolt; the zipline hook and the grappling hook in either hand.
  // 0.1.198: the knob is the "aim" bone itself (the gun's mesh showed it): it moves with the mechanism, no extra pieces.
  Require(Calls(W("Build")).Any(x=>x.Name=="PrepareKnob")&&W("PrepareKnob").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="LiveRelative")&&Calls(W("PrepareKnob")).Any(x=>x.Name=="DumpMesh")&&!Calls(W("PrepareKnob")).Any(x=>x.DeclaringType.Name=="KnobMath")
,"the Uzi's top knob drawn from extra pieces (it is the gun's own moving part)");
  Require(Str(p.Types.Single(x=>x.Name=="MechanismMath"),"uzi_aim_bnd"),"the Uzi's mechanism moves its aim bone (the knob)");
  Require(Str(p.Types.Single(x=>x.Name=="ReloadBones"),"_aim_bnd_jnt"),"the Uzi's reload bolt is its aim bone (the knob)");
  Require(true
   &&p.Types.Single(x=>x.Name=="WeaponMechanism").Methods.Single(x=>x.Name=="BakeOwned").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="ExtraVertices"),"the Uzi's top handle not drawn back (only the part inside moves)");
  var zip195=p.Types.Single(x=>x.Name=="ZiplineVr");MethodDefinition Z(string n)=>zip195.Methods.Single(x=>x.Name==n);
  var drv195=p.Types.Single(x=>x.Name=="InteractionDriver");var items195=p.Types.Single(x=>x.Name=="WheelItems");MethodDefinition I(string n)=>items195.Methods.Single(x=>x.Name==n);
  var grapple195=p.Types.Single(x=>x.Name=="GrappleVr");
  Require(Str(zip195,"MovePlayerToOtherPoint")&&Str(zip195,"Started")&&Calls(Z("Started")).Any(x=>x.Name=="Begin")&&Calls(Z("Begin")).Any(x=>x.Name=="CollectGameParts")
   &&Calls(Z("Render")).Any(x=>x.Name=="set_enabled")&&Calls(Z("End")).Any(x=>x.Name=="set_enabled")&&Calls(Z("TryHeld")).Any(x=>x.Name=="TryCanonicalWorld")&&Calls(Z("TryHeld")).Any(x=>x.Name=="PoseMatrix")&&Calls(Z("SampleHold")).Any(x=>x.Name=="TryCanonicalFromRig")&&Calls(Z("Tick")).Any(x=>x.Name=="SampleHold")&&Calls(Z("SampleHold")).Any(x=>x.Name=="SaveHold")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="HidesHand")&&Calls(drv195.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.DeclaringType.Name=="ZiplineVr"&&x.Name=="Tick"),"the zipline ride does not hold the hand on the cable (or shows the game's third arm)");
  Require(Calls(drv195.Methods.Single(x=>x.Name=="TryZipline")).Any(x=>x.Name=="PingRaycastHittable")&&Calls(drv195.Methods.Single(x=>x.Name=="TryZipline")).Any(x=>x.Name=="ZiplineAction")
   &&Calls(I("Commit")).Any(x=>x.Name=="Kind"&&x.DeclaringType.Name=="HandTools")&&Calls(I("Tick")).Any(x=>x.Name=="TickPass")&&Calls(I("Tick")).Any(x=>x.Name=="TryZipline")
   &&Calls(I("TickPass")).Any(x=>x.Name=="CanTakeTool")&&Calls(I("TickPass")).Any(x=>x.Name=="Reaches"&&x.DeclaringType.Name=="HandTools")
   &&Calls(H("Tick")).Any(x=>x.Name=="KeepWeaponOffTool"),"the zipline hook not held, passed between hands, or pointed at a cable");
  Require(Calls(grapple195.Methods.Single(x=>x.Name=="Placement")).Any(x=>x.Name=="MirrorAcrossHand"&&x.DeclaringType.Name=="HandTools")&&Calls(grapple195.Methods.Single(x=>x.Name=="Placement")).Any(x=>x.Name=="LeftPlaced")&&Calls(grapple195.Methods.Single(x=>x.Name=="ShowPreview")).Any(x=>x.Name=="Placement")&&Calls(grapple195.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Placement")
   &&Calls(drv195.Methods.Single(x=>x.Name=="TryUseTool")).Any(x=>x.Name=="RefreshFrom"),"the grappling hook not mirrored into the right hand (or not fired from there)");
  // 0.1.196: a long gun held by its barrel (stock up) in either hand, swung like a club.
  var club196=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition C(string n)=>club196.Methods.Single(x=>x.Name==n);
  Require(Calls(C("TickGameWeapon")).Any(x=>x.Name=="TrackBarrelHand")&&Calls(C("TickGameWeapon")).Count(x=>x.Name=="StartClub")>=2&&Calls(C("TickGameWeapon")).Any(x=>x.Name=="AtBarrelFront")
   &&Calls(C("TrackBarrelHand")).Any(x=>x.Name=="BarrelGrab")&&Calls(C("BarrelGrab")).Any(x=>x.Name=="NearerFront"&&x.DeclaringType.Name=="ClubMath")
   &&Calls(C("RenderPose")).Any(x=>x.Name=="PoseClub")&&Calls(C("RenderPose")).Any(x=>x.Name=="BarrelGrab")
   &&Calls(C("PoseClub")).Any(x=>x.Name=="LongHandleContact")&&Calls(C("PoseClub")).Any(x=>x.Name=="get_Turn"&&x.DeclaringType.Name=="ClubMath")&&Calls(C("PoseClub")).Any(x=>x.Name=="Blend")
   &&Calls(C("TryPoseHand")).Any(x=>x.Name=="TryPoseClubHand")&&Calls(C("HandProfileFor")).Any(x=>x.Name=="Clubbing"),"a long gun cannot be held by its barrel (or not drawn stock up in the fist)");
  Require(Calls(C("GameMeleeHand")).Any(x=>x.Name=="get_Clubbed")&&Calls(C("TryLongStickEnd")).Any(x=>x.Name=="get_Clubbed")&&Calls(C("GameMeleeTip")).Any(x=>x.Name=="get_ClubStockEnd")
   &&Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Clubbing")
   &&Calls(C("DemoteGame")).Any(x=>x.Name=="KeepCopyClub")&&Calls(C("TryPoseCopyHand")).Any(x=>x.Name=="CopyClub"),"a gun held by its barrel does not hit like a club (its whole shape, by the stock's end)");
  // 0.1.197: the hook fired from the right hand mirrors the rope; the zipline hook held as the game holds it; the riding hand not drawn.
  var loco197=p.Types.Single(x=>x.Name=="LocomotionDriver");var grapple197=p.Types.Single(x=>x.Name=="GrappleVr");
  Require(Calls(grapple197.Methods.Single(x=>x.Name=="Up")).Any(x=>x.Name=="get_ClimbStick")&&Calls(grapple197.Methods.Single(x=>x.Name=="Down")).Any(x=>x.Name=="get_ClimbStick")
   &&Calls(loco197.Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="Pressed"&&x.DeclaringType.Name=="RopeRelease")&&Calls(loco197.Methods.Single(x=>x.Name=="get_RopeSwing")).Any(x=>x.Name=="get_RightHanded")
   &&Calls(C("VirtualButton")).Any(x=>x.Name=="get_RopeRightHanded")&&Calls(p.Types.Single(x=>x.Name=="VrPromptLabels").Methods.Single(x=>x.Name=="Source"&&x.Parameters.Count>=1&&x.Parameters[0].ParameterType.Name=="InputActions")).Any(x=>x.Name=="get_RightHanded"),"the rope of a hook fired from the right hand not mirrored (right stick climbs, left stick swings, R3 lets go)");
  Require(Calls(p.Types.Single(x=>x.Name=="WheelItems").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="TryHeld")&&Calls(p.Types.Single(x=>x.Name=="HeldItemVisual").Methods.Single(x=>x.Name=="PoseMatrix")).Any(x=>x.Name=="Decompose"&&x.DeclaringType.Name=="ItemPlacement"),"the zipline hook not held as the game's arm holds it");
  // 0.1.198: the barrel deeper in the curled fingers; the zipline hook held by its handle as a pistol by its grip.
  var tool198=p.Types.Single(x=>x.Name=="WeaponHands");var items198=p.Types.Single(x=>x.Name=="WheelItems");var held198=p.Types.Single(x=>x.Name=="HeldItemVisual");
  Require(Calls(tool198.Methods.Single(x=>x.Name=="PoseClub")).Any(x=>x.Name=="get_FistShift")&&Calls(tool198.Methods.Single(x=>x.Name=="get_FistShift")).Any(x=>x.Name=="Deeper"&&x.DeclaringType.Name=="ClubMath")
   &&tool198.Methods.Single(x=>x.Name=="get_FistShift").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="BarrelGripForward")
   &&Str(p.Types.Single(x=>x.Name=="QualityOptions"),"BarrelGripForwardMm")&&Str(p.Types.Single(x=>x.Name=="QualityOptions"),"BarrelGripUpMm"),"the barrel not moved deeper into the curled fingers (or not set in the VR settings)");
  Require(held198.Methods.Any(m=>Calls(m).Any(x=>x.Name=="Find"&&x.DeclaringType.Name=="GripBarMath"))&&held198.Methods.Any(m=>Calls(m).Any(x=>x.Name=="GetTriangles"))
   &&Calls(items198.Methods.Single(x=>x.Name=="TryToolHand")).Any(x=>x.Name=="TryToolHold")&&Calls(items198.Methods.Single(x=>x.Name=="TryToolHand")).Any(x=>x.Name=="Scaled"&&x.DeclaringType.Name=="GripBarMath")
   &&Calls(items198.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="PoseRoot")&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="TryToolHand")
   &&Calls(tool198.Methods.Single(x=>x.Name=="TryToolHold")).Any(x=>x.Name=="Hold"&&x.DeclaringType.Name=="GripBarMath")&&Calls(tool198.Methods.Single(x=>x.Name=="TryToolHold")).Any(x=>x.Name=="get_FistShift")
   &&Calls(tool198.Methods.Single(x=>x.Name=="TryToolHold")).Any(x=>x.Name=="TryPistolHold"),"the zipline hook not held by its handle as a pistol by its grip");
  // 0.1.199: the hook's handle is its round rubber grip with the hook beside it (not a flat frame arm, not the shaft).
  var bar199=p.Types.Single(x=>x.Name=="GripBarMath");
  Require(Calls(bar199.Methods.Single(x=>x.Name=="Find")).Any(x=>x.Name=="Handle")&&Calls(bar199.Methods.Single(x=>x.Name=="Find")).Any(x=>x.Name=="Beside"),"a frame arm or the shaft taken for the zipline hook's handle");
  // 0.1.200: a thin barrel gripped tighter; a chair timed by its far end; the death screen's ray over its panel; the hook's handle nearer the palm.
  var hands200=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition W2(string n)=>hands200.Methods.Single(x=>x.Name==n);
  Require(Calls(W2("StartClub")).Any(x=>x.Name=="MeasureBarrel")&&Calls(W2("MeasureBarrel")).Any(x=>x.Name=="BarrelThickness")&&Calls(W2("HandProfileFor")).Any(x=>x.Name=="ClubHandProfile")
   &&Calls(W2("ClubHandProfile")).Any(x=>x.Name=="WrapCurl")&&Calls(W2("ClubHandProfile")).Any(x=>x.Name=="WrapProfile")&&Calls(W2("PoseClub")).Any(x=>x.Name=="ClubShift")
   &&Calls(p.Types.Single(x=>x.Name=="FingerPoseMath").Methods.Single(x=>x.Name=="HeldPose")).Any(x=>x.Name=="HeldAmount"),"a thin barrel (the M4's) not gripped tighter");
  Require(Calls(p.Types.Single(x=>x.Name=="PunchDriver").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="FarthestPart"&&x.DeclaringType.Name=="PunchMotion"),"a chair timed by the hand (a touch, not a blow)");
  Require(Calls(p.Types.Single(x=>x.Name=="MenuBeam").Methods.Single(x=>x.Name=="Make")).Any(x=>x.Name=="set_sortingOrder"),"the menu ray drawn under the death panel");
  var punch200=p.Types.Single(x=>x.Name=="PunchDriver");
  Require(Calls(punch200.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="StoppedByNpc")&&Calls(punch200.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="RetargetHurtbox")
   &&punch200.Methods.Single(x=>x.Name=="StoppedByNpc").HasBody&&Calls(punch200.Methods.Single(x=>x.Name=="StoppedByNpc")).Any(x=>x.Name=="TryGunObstacle")
   &&Calls(punch200.Methods.Single(x=>x.Name=="RetargetHurtbox")).Any(x=>x.Name=="ValidateIfDamageable")
   &&p.Types.Single(x=>x.Name=="ContactRig").Methods.Single(x=>x.Name=="ResolveGun").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="gunObstacle"),"a gun held by its barrel swung against an enemy's body does not hurt it");
  Require(Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="MakeRoomForLoose")).Any(x=>x.Name=="LooseToFree")&&Calls(W2("TakeAnother")).Any(x=>x.Name=="AddAmmo"),"one more weapon of a kind not picked up while two others are on the body");
  // 0.1.202: a gun with a hand-made shape hits whole (by its barrel: its stock and barrel), each part of a long thing meets its own nearest.
  Require(Calls(W2("get_MeleeShape")).Any(x=>x.Name=="HandMadeShape")&&Calls(W2("HandMadeShape")).Any(x=>x.Name=="Weapon"&&x.DeclaringType.Name=="ContactSolver")
   &&Str(punch200,"VR PUNCH a part's sweep met "),"a gun held by its barrel hits only with its stock's end (or a long thing's wall touch drops its blow)");
  Require(Calls(W2("TryToolHold")).Any(x=>x.Name=="get_ToolShift")&&Str(p.Types.Single(x=>x.Name=="QualityOptions"),"ZiplineGripUpMm"),"the zipline hook's handle not moved nearer the palm");
  // 0.1.203: a door whose own interaction runs story events opens from closed by it (the bank's guards); a card held to the reader opens it.
  var doors203=p.Types.Single(x=>x.Name=="PhysicalDoors");MethodDefinition D3(string n)=>doors203.Methods.Single(x=>x.Name==n);
  Require(Calls(D3("Bind")).Any(x=>x.Name=="Find"&&x.DeclaringType.Name=="DoorStoryEvents")&&Calls(D3("OpenStory")).Any(x=>x.Name=="TryToggle")
   &&Calls(D3("Step")).Count(x=>x.Name=="OpenStory")==2&&Calls(D3("Move")).Any(x=>x.Name=="Delta")&&Calls(D3("TryToggle")).Any(x=>x.Name=="PingRaycastHittable")
   &&Calls(p.Types.Single(x=>x.Name=="DoorStoryEvents").Methods.Single(x=>x.Name=="Find")).Any(x=>x.Name=="get_manualInteractions")
   &&Calls(p.Types.Single(x=>x.Name=="DoorStoryEvents").Methods.Single(x=>x.Name=="Find")).Any(x=>x.Name=="get_events")
   &&Str(doors203," opened from closed by its own interaction (its events: "),"a story door (the bank's hostage door) opened by the hand alone runs none of its events");
  // 0.1.205: police, FBI and guards hold fire at a hostage's holder (late ones told of it); every bullet at him into the hostage; a club touching an enemy wins over a wall.
  var carry205=p.Types.Single(x=>x.Name=="GripCarry");MethodDefinition G5(string n)=>carry205.Methods.Single(x=>x.Name==n);
  Require(Calls(G5("HostageDecision")).Any(x=>x.Name=="TellOfHostage")&&Calls(G5("TellOfHostage")).Any(x=>x.Name=="ObserveHostageController")&&Calls(G5("TellOfHostage")).Any(x=>x.Name=="HandlePlayerTakingHostage")
   &&Calls(G5("TellOfHostage")).Any(x=>x.Name=="SetHostageState")&&Str(carry205,"DoesCurrentTargetHaveHostage")&&Calls(G5("HostageDecision")).Any(x=>x.Name=="get_hasFiredWhileHavingHostage")
   &&Calls(G5("ShieldPlayer")).Any(x=>x.Name=="Facing"),"police, FBI and guards shoot a hostage's holder (or a bullet past the hostage hits him)");
  var punch205=p.Types.Single(x=>x.Name=="PunchDriver");
  Require(Calls(punch205.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="ReportNearMiss")&&Calls(punch205.Methods.Single(x=>x.Name=="ReportNearMiss")).Any(x=>x.Name=="NearEnemy")
   &&Str(p.Types.Single(x=>x.Name=="ContactFilter"),"collider player ("),"a club stopped at an enemy's guard loses its blow to a wall (or invisible walls stop weapons)");
  // 0.1.206: a held thing rearms once carried off its last touch (not only by pulling back along that stroke).
  var motion206=p.Types.Single(x=>x.Name=="PunchMotion").Methods.Single(x=>x.Name=="Sample"&&x.Parameters.Count==5);
  Require(motion206.Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-.20f)<1e-6f)&&motion206.Body.Instructions.Any(i=>i.Operand is float g&&Math.Abs(g-.5f)<1e-6f)
   &&Calls(motion206).Any(x=>x.Name=="Length")&&Str(p.Types.Single(x=>x.Name=="PunchDriver")," m/s but not rearmed since its last touch (pull back, or carry it off that touch)"),"a chair after a touch strikes again only when pulled back along that touch's stroke");
  // 0.1.207: with an eye at the scope the aim is steadied as if holding the breath.
  Require(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Where(m=>m.HasBody).SelectMany(Calls).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="ScopeSteadyMath")
   &&Str(p.Types.Single(x=>x.Name=="WeaponOptions"),"ScopeSteadiness")&&Str(p.Types.Single(x=>x.Name=="WeaponHands")," at the eye: the aim steadied as if holding the breath (strength "),"the scope sways with every tremor of the hand at the eye");
  // 0.1.208: a touch presses a control by what it runs, not only by a button name (the alarm power boxes, unnamed lift buttons).
  var touch208=p.Types.Single(x=>x.Name=="TouchButtons");var reader208=p.Types.Single(x=>x.Name=="TouchControlReader").Methods.Single(x=>x.Name=="Read"&&x.Parameters.Count==5);
  Require(Calls(touch208.Methods.Single(x=>x.Name=="Resolve")).Any(x=>x.Name=="Read"&&x.DeclaringType.Name=="TouchControlReader")&&Calls(touch208.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Note")
   &&Calls(reader208).Any(x=>x.Name=="GetRaycastHittableType")&&Calls(reader208).Any(x=>x.Name=="get_manualInteractions")&&Calls(reader208).Any(x=>x.Name=="get_events")&&Calls(reader208).Any(x=>x.Name=="get_eventTrigger")
   &&Calls(reader208).Any(x=>x.Name=="Classify"&&x.DeclaringType.Name=="TouchControlMath")&&Calls(reader208).Any(x=>x.Name=="get_doorRaycastTargets")
   &&p.Types.Single(x=>x.Name=="TouchControlReader").Methods.Where(m=>m.HasBody).SelectMany(Calls).Any(x=>x.Name=="Read"&&x.DeclaringType.Name=="DoorMotion")
   &&Str(touch208,"TOUCH BUTTON nothing pressed at ")&&Str(p.Types.Single(x=>x.Name=="TouchControlMath"),"AlarmActivator"),"a touch presses only interactions named as buttons (the alarm power boxes and some lifts never)");
  // 0.1.210: the start screen through the game's any-button input; the game's own wheel (its tutorial) taken over; menus without Windows focus in VR.
  var keyboard210=p.Types.Single(x=>x.Name=="MenuKeyboard");var ui210=p.Types.Single(x=>x.Name=="GameUiControls");
  Require(Calls(keyboard210.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Pulse")&&!Str(keyboard210,"GetAnyButtonDown")
   &&Calls(ui210.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="AdoptGameWheel")&&Calls(ui210.Methods.Single(x=>x.Name=="OpenGameMenu")).Any(x=>x.Name=="CloseGameWheel")&&Calls(ui210.Methods.Single(x=>x.Name=="CloseGameWheel")).Any(x=>x.Name=="CloseWheel")
   &&Calls(ui210.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_Playable")&&Calls(p.Types.Single(x=>x.Name=="VrMenuPointer").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_Playable"),
   "the start screen needs Windows focus, or the wheel the game's tutorial opens cannot be closed");
  // 0.1.212: door and breakable prompts show only the game's icon; key, card and lockpick locks keep their text.
  var labels212=p.Types.Single(x=>x.Name=="VrPromptLabels");var apply212=labels212.Methods.Single(x=>x.Name=="Apply");
  Require(Calls(apply212).Any(x=>x.Name=="IconOnly")&&Calls(apply212).Any(x=>x.Name=="EnablePCBackground")&&Calls(apply212).Any(x=>x.Name=="SetInputHint")
   &&Calls(labels212.Methods.Single(x=>x.Name=="IconOnly")).Any(x=>x.Name=="get_DoorTarget")
   &&!labels212.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string t&&(t=="Push or pull it with your hand"||t=="Hit it with your hand or a weapon")),"door and grate prompts still show text");
  // 0.1.213: a hostage taken by pointing a free hand at him and holding its grip; the start screen gets Enter at the press.
  var carry213=p.Types.Single(x=>x.Name=="GripCarry");MethodDefinition C3(string n)=>carry213.Methods.Single(x=>x.Name==n);
  Require(Calls(C3("PointHostage")).Any(x=>x.Name=="TakeHostage")&&Calls(C3("Pointed")).Any(x=>x.Name=="RaycastNonAlloc")&&Calls(C3("Pointed")).Any(x=>x.Name=="HostageAllowed")
   &&Calls(C3("Pointed")).Any(x=>x.Name=="FirstNpc")&&Calls(C3("TickBody")).Any(x=>x.Name=="PointHostage")&&Calls(C3("HostagePrompt")).Any(x=>x.Name=="TogglePrompt")
   &&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="FilterHints")).Any(x=>x.Name=="HostagePrompt")
   &&Str(p.Types.Single(x=>x.Name=="VrPromptLabels"),"Hold right Grip"),"a hostage still has to be touched with the left hand");
  // 0.1.214: right A ends the weapon wheel tutorial (its hint names right A); the next knife at once after a throw; the landing mark of throws.
  var hands214=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H4(string n)=>hands214.Methods.Single(x=>x.Name==n);
  var tutorial214=p.Types.Single(x=>x.Name=="WheelTutorial");MethodDefinition uiTick214=ui210.Methods.Single(x=>x.Name=="Tick");
  Require(Calls(uiTick214).Any(x=>x.Name=="Ends"&&x.DeclaringType.Name=="WheelTutorialMath")&&Calls(uiTick214).Any(x=>x.Name=="End"&&x.DeclaringType.Name=="WheelTutorial")
   &&Calls(tutorial214.Methods.Single(x=>x.Name=="End")).Any(x=>x.Name=="set_forceOpenWeaponWheel")&&Calls(tutorial214.Methods.Single(x=>x.Name=="End")).Any(x=>x.Name=="SetInputLock")
   &&Calls(labels212.Methods.Single(x=>x.Name=="Source")).Any(x=>x.Name=="get_LabelsA")&&Calls(H4("TutorialStep")).Any(x=>x.Name=="Started"),"the weapon wheel tutorial still ends only with the menu chord");
  Require(Calls(H4("TickHolstersCore")).Any(x=>x.Name=="KnifeRetake")&&Calls(H4("KnifeRetake")).Any(x=>x.Name=="Retake")&&Calls(H4("ThrowGameKnife")).Any(x=>x.Name=="KnifeReady")&&Calls(H4("TickKnifeThrow")).Any(x=>x.Name=="KnifeReady")
   &&Calls(p.Types.Single(x=>x.Name=="HolsterLayout").Methods.Single(x=>x.Name=="GrabRadiusOf")).Any(x=>x.Name=="OnChest"),"the next knife still waits for the hand to be emptied after a throw");
  Require(Calls(H4("KnifeGrip")).Any(x=>x.Name=="AimKnifeLanding")&&Calls(H4("TickCopyKnife")).Any(x=>x.Name=="AimKnifeLanding")&&Calls(H4("UpdatePropThrow")).Any(x=>x.Name=="AimPropLanding")
   &&Calls(H4("TickGrenade")).Any(x=>x.Name=="AimGrenadeLanding")&&Calls(H4("TickLeftGrenade")).Any(x=>x.Name=="AimGrenadeLanding")&&Calls(H4("Tick")).Any(x=>x.Name=="TickLanding")
   &&Calls(H4("ShowLanding")).Any(x=>x.Name=="Land")&&Calls(H4("LandingCast")).Any(x=>x.Name=="SphereCastNonAlloc")&&Calls(H4("LaunchProp")).Any(x=>x.Name=="FreezeLanding")&&Calls(H4("KnifeLaunch")).Any(x=>x.Name=="WatchKnifeFlight")
   &&Calls(H4("AimGrenadeLanding")).Any(x=>x.Name=="Peek"),"throws show no landing mark");
  // 0.1.215: keys on the stick click, the controller icons, the VR controls page, the bazooka by hand and its aim dot.
  var interaction215=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(Calls(interaction215.Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="Sample"&&x.DeclaringType.Name=="InteractionState"&&x.Parameters.Count==5)
   &&Calls(interaction215.Methods.Single(x=>x.Name=="LockAimed")).Any(x=>x.Name=="get_conditionalResolved")
   &&Calls(hands214.Methods.Single(x=>x.Name=="VirtualButton")).Any(x=>x.Name=="Taken"&&x.DeclaringType.Name=="LockStick")
   &&Calls(p.Types.Single(x=>x.Name=="LocomotionDriver").Methods.Single(x=>x.Name=="Inject")).Any(x=>x.Name=="Taken"&&x.DeclaringType.Name=="LockStick")
   &&Str(labels212,"Left/Right Grip")&&Str(labels212,"Right stick click")&&Calls(labels212.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="TypeIcon")&&Calls(labels212.Methods.Single(x=>x.Name=="Apply")).Any(x=>x.Name=="Show"&&x.DeclaringType.Name=="PromptIcons"),
   "keys still on grip + A, or hints without the controller icon");
  var icons215=p.Types.Single(x=>x.Name=="PromptIcons");
  Require(Calls(icons215.Methods.Single(x=>x.Name=="SpriteFor")).Any(x=>x.Name=="Paint")&&Calls(icons215.Methods.Single(x=>x.Name=="SpriteFor")).Any(x=>x.Name=="CreateSprite")
   &&Calls(icons215.Methods.Single(x=>x.Name=="TypeIcon")).Any(x=>x.Name=="set_overrideSprite"),"the controller icon is not drawn into the game's hint");
  var page215=p.Types.Single(x=>x.Name=="VrSettingsPage");
  Require(Calls(page215.Methods.Single(x=>x.Name=="Ensure")).Any(x=>x.Name=="Copy")&&Calls(page215.Methods.Single(x=>x.Name=="RenderControls")).Any(x=>x.Name=="Body")
   &&Calls(page215.Methods.Single(x=>x.Name=="OpenFrom")).Any(x=>x.Name=="Show"&&x.DeclaringType.Name=="ControlsSheet"),"no VR controls page");
  Require(Calls(H4("Tick")).Any(x=>x.Name=="TickBazooka")&&Calls(H4("TickBazookaCore")).Any(x=>x.Name=="Take"&&x.DeclaringType.Name=="BazookaReloadMath")
   &&Calls(H4("TickBazookaCore")).Any(x=>x.Name=="Insert"&&x.DeclaringType.Name=="BazookaReloadMath")&&Calls(H4("InsertRocket")).Any(x=>x.Name=="FireSound")
   &&Calls(H4("InsertRocket")).Any(x=>x.Name=="TryRemoveAmmo")&&Calls(H4("ShowAimDot")).Any(x=>x.Name=="RaycastNonAlloc")
   &&Calls(H4("AllowStockReload")).Any(x=>x.Name=="get_BazookaManual")&&Calls(H4("TakeRocket")).Any(x=>x.Name=="get_projectileUsedByPlayer"),"the bazooka still reloads the game's way, or has no aim dot");
  // 0.1.216: a hard fist in an enemy's back knocks him out (the game's knockout, its takedown rule); a pointed hostage needs the grip held still.
  var punch216=p.Types.Single(x=>x.Name=="PunchDriver");MethodDefinition P6(string n)=>punch216.Methods.Single(x=>x.Name==n);
  Require(Calls(P6("Tick")).Any(x=>x.Name=="BackKnockout")&&Calls(P6("BackKnockout")).Any(x=>x.Name=="Knockout"&&x.DeclaringType.Name=="NPC")&&Calls(P6("BackKnockout")).Any(x=>x.Name=="CanBeStealthAttacked")
   &&Calls(P6("BackKnockout")).Any(x=>x.Name=="FromBehind")&&Calls(P6("BackKnockout")).Any(x=>x.Name=="Hard")
   &&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="PointHostage")).Any(x=>x.Name=="HoldStep"),"a hard punch in the back does not knock out, or a punch takes the hostage");
  // 0.1.217: no hand ball for the hand riding the zipline; the rocket held in the fist; the tube's own mouth; the hand-loaded rocket drawn in the tube.
  var rig217=p.Types.Single(x=>x.Name=="CameraRig");var hands217=p.Types.Single(x=>x.Name=="WeaponHands");var visual217=p.Types.Single(x=>x.Name=="WeaponVisual");
  MethodDefinition H7(string n)=>hands217.Methods.Single(x=>x.Name==n);
  Require(Calls(rig217.Methods.Single(x=>x.Name=="UpdateMarkers")).Count(x=>x.Name=="HidesHand"&&x.DeclaringType.Name=="ZiplineVr")==2
   &&Calls(H7("TickBazookaCore")).Any(x=>x.Name=="RocketInTube")&&Calls(H7("TickBazookaCore")).Any(x=>x.Name=="RocketHold")&&Calls(H7("RocketHold")).Any(x=>x.Name=="TryToolHold")&&Calls(H7("RocketHold")).Any(x=>x.Name=="HoldBar")
   &&Calls(H7("BazookaMouth")).Any(x=>x.Name=="get_TubeMouth")&&Calls(visual217.Methods.Single(x=>x.Name=="MeasureTube")).Any(x=>x.Name=="Mouth")
   &&Calls(visual217.Methods.Single(x=>x.Name=="RocketInTube")).Any(x=>x.Name=="Keep")&&Calls(visual217.Methods.Single(x=>x.Name=="RocketInTube")).Any(x=>x.Name=="set_Hidden")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="TryRocketHand")&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="PoseHeldRocket")
   &&Calls(p.Types.Single(x=>x.Name=="NativeSkinSnapshot").Methods.Single(x=>x.Name=="Bake")).Any(x=>x.Name=="op_Multiply"),"a ball where the zipline hand was, the rocket not in the fist, the glow ahead of the tube, or the hand-loaded rocket not drawn");
  // 0.1.218: the player's allies are left alone: no hostage, punch, thrown hit, gun grab, body grab or hit reaction.
  bool AllyGuard(string type,string method)=>p.Types.Single(x=>x.Name==type).Methods.Where(x=>x.Name==method).SelectMany(Calls).Any(x=>x.DeclaringType.Name=="NpcAllies"&&(x.Name=="Ally"||x.Name=="AllyCollider"));
  Require(AllyGuard("GripCarry","HostageAllowed")&&AllyGuard("GripCarry","TickBody")&&AllyGuard("PunchDriver","Tick")&&AllyGuard("PunchDriver","Thrown")&&AllyGuard("NpcHitReactions","Hit")
   &&AllyGuard("NpcHitReactions","TryGrab")&&AllyGuard("BodyGrab","Grab")&&Calls(p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="HostageAllowed")).Any(x=>x.Name=="GameAllowsHostage")
   &&Calls(p.Types.Single(x=>x.Name=="NpcAllies").Methods.Single(x=>x.Name=="Ally")).Any(x=>x.Name=="get_canBeHurtByPlayer")&&Calls(p.Types.Single(x=>x.Name=="NpcAllies").Methods.Single(x=>x.Name=="Ally")).Any(x=>x.Name=="get_canBeTakenHostageEvenIfCannotBeHurt"),"an ally can be taken hostage, punched, grabbed or made to react");
  // 0.1.219: the bazooka's grips held as a pistol's, fingers closed round them; the left hand mirrored across the handle.
  var hands219=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H9(string n)=>hands219.Methods.Single(x=>x.Name==n);
  // (0.1.224: the front grip where the game's hands are, not a closed hand's grip point: see below.)
  Require(Calls(H9("NativeGrip")).Any(x=>x.Name=="BazookaGrip")&&Calls(H9("BazookaHold")).Any(x=>x.Name=="get_FrontGripBar")
   &&Calls(H9("HandProfileFor")).Any(x=>x.Name=="BazookaGripHand")&&Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="FindGripBars")).Any(x=>x.Name=="GripBar"),"the bazooka's grips held with flat hands, or the left hand off the handle");
  // 0.1.221: the other hand's grip takes magazines, rounds and rockets from the belt (the trigger racks); the belt's
  // holster place yields while the gun wants rounds; the bazooka's support hand placed in its own frame on the
  // front grip (its bracket trimmed), the handle left to the game's hold; the bazooka mirrored across its tube.
  var hands221=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H1(string n)=>hands221.Methods.Single(x=>x.Name==n);
  var gripBit=p.Types.Single(x=>x.Name=="HandControls").Fields.Single(x=>x.Name=="Grip").Constant;
  var visual221=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(hands221.Fields.Any(x=>x.Name=="AmmoButton"&&x.HasConstant&&Equals(x.Constant,gripBit))
   &&Calls(H1("TickReload")).Any(x=>x.Name==".ctor"&&x.DeclaringType.Name.StartsWith("Nullable"))&&Calls(H1("HandFreeForWeapon")).Any(x=>x.Name=="PouchTakes")
   &&Calls(H1("HandFree")).Any(x=>x.Name=="PouchTakesNow")&&Calls(H1("PunchBlocked")).Any(x=>x.Name=="PouchTakesNow")
   &&Calls(H1("PouchTakes")).Any(x=>x.Name=="WantsSupply")
   &&!Calls(H1("BazookaGrip")).Any(x=>x.Name=="get_RearGripBar")&&Calls(H1("NativeGrip")).Any(x=>x.Name=="get_SymmetryX")
   &&visual221.Fields.Any(x=>x.Name=="FrontTrim")&&Calls(visual221.Methods.Single(x=>x.Name=="FindGripBars")).Any(x=>x.Name=="GripBar")
   &&Str(p.Types.Single(x=>x.Name=="ControlsSheet"),"Left Grip at the belt pouch, then into the gun"),"magazines still taken with the trigger, the belt's holster taking the grip, or the bazooka's support hand off its grip");
  // 0.1.222: a bottle grabbed and thrown in one motion: the grip let go in a swing while the game was still drawing it throws it once it is in the hand; the carry neither drops it meanwhile nor forgets the press that took it.
  var hands222=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H2(string n)=>hands222.Methods.Single(x=>x.Name==n);
  var carry222=p.Types.Single(x=>x.Name=="GripCarry").Methods.Single(x=>x.Name=="Tick");
  Require(Calls(H2("SampleSwings")).Any(x=>x.Name=="NoteGrips")&&Calls(H2("NoteGrips")).Any(x=>x.Name=="SwingThrow")&&Calls(H2("NoteGrips")).Any(x=>x.Name=="LetGo"&&x.DeclaringType.Name=="GripLetGo")
   &&Calls(H2("UpdatePropThrow")).Any(x=>x.Name=="NoteLateThrow")&&Calls(H2("UpdatePropThrow")).Any(x=>x.Name=="LateThrow")&&Calls(H2("NoteLateThrow")).Any(x=>x.Name=="SwungSincePress")
   &&Calls(H2("LateThrow")).Any(x=>x.Name=="CanStart")&&Calls(H2("LateThrow")).Any(x=>x.Name=="LaunchProp")&&Calls(H2("LateThrow")).Any(x=>x.Name=="Forget")
   &&Calls(carry222).Any(x=>x.Name=="AwaitsThrow")&&Calls(carry222).Any(x=>x.Name=="Lost"),"a bottle grabbed and let go in one swing stays in the hand (a second press needed to throw it)");
  // 0.1.223: the grip works bolts too; the M60's box goes in at its place and its open cover is pressed shut by a hand;
  // the bazooka's front grip held as the game holds its handle (moved, mirrored), the handle's fingers closed on the trigger.
  var hands223=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H3(string n)=>hands223.Methods.Single(x=>x.Name==n);
  Require(Calls(H3("TickReload")).Any(x=>x.Name=="PushCover")&&Calls(H3("PushCover")).Any(x=>x.Name=="Step"&&x.DeclaringType.Name=="CoverPush")&&Calls(H3("PushCover")).Any(x=>x.Name=="PushCoverShut")
   &&Calls(H3("PushCover")).Any(x=>x.Name=="set_CoverPushDegrees")&&Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="TickCover")).Any(x=>x.Name=="get_CoverPushDegrees")
   &&Calls(H3("BazookaHold")).Any(x=>x.Name=="get_TriggerGripBar")&&Str(hands223,"bazooka_trigger")
   &&Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="FindGripBars")).Any(x=>x.Name=="TriggerGripBone")
   &&Str(p.Types.Single(x=>x.Name=="ControlsSheet"),"Left Grip at the bolt or pump, pull back")&&!Str(p.Types.Single(x=>x.Name=="ControlsSheet"),"Left trigger at the bolt, pull back (pump: Left Grip)"),
   "bolts still worked with the trigger, the M60 box or cover the old way, or the bazooka's hands as before");
  // 0.1.224: no "Smarter enemies" row in VR SETTINGS; the enemies themselves unchanged (still ticked, still guarded).
  var menu224=p.Types.Single(x=>x.Name=="QualityMenu");
  Require(!Str(menu224,"Smarter enemies")&&!menu224.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is FieldReference f&&f.DeclaringType.Name=="EnemyOptions")
   &&Calls(p.Types.Single(x=>x.Name=="EnemyAi").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_Value"),"the smarter enemies row is still in VR SETTINGS, or the enemies no longer follow their setting");
  // 0.1.226: the left hand on the handle in the left hand; the rocket held with the fingers and thumb opened round its tube.
  // 0.1.227: each hand closed round its grip (the middle of the fingers' curls on the grip's line, each finger onto
  // it, the thumb clear of it), not at a place a game's hand once had; the rocket's tube along the fingers' line
  // through their curls; why a two-hand support hand is not drawn on the front grip is written to the log.
  var hands225=p.Types.Single(x=>x.Name=="WeaponHands");MethodDefinition H5(string n)=>hands225.Methods.Single(x=>x.Name==n);
  var tube227=p.Types.Single(x=>x.Name=="BazookaTubeMath");var fingers227=p.Types.Single(x=>x.Name=="FingerPoseMath");MethodDefinition F7(string n)=>fingers227.Methods.Single(x=>x.Name==n);
  Require(Calls(H5("BazookaGrip")).Any(x=>x.Name=="BazookaHold")&&Calls(H5("BazookaLeftHandle")).Any(x=>x.Name=="BazookaHold")
   &&Calls(H5("BazookaHold")).Any(x=>x.Name=="GripFit"&&x.DeclaringType.Name=="NativeHandVisual")&&Calls(H5("BazookaHold")).Any(x=>x.Name=="OnGrip")&&Calls(H5("BazookaHold")).Any(x=>x.Name=="DrawnChannel")
   &&!tube227.Fields.Any(x=>x.Name is "RightOnHandle" or "LeftOnFront" or "RightOnFront" or "LeftOnHandle")&&!tube227.Methods.Any(x=>x.Name=="get_LeftTurn")
   &&Calls(H5("TryPoseHand")).Any(x=>x.Name=="BazookaLeftHandle")&&Calls(H5("TryPoseHand")).Any(x=>x.Name=="SupportMiss")
   &&Calls(H5("PrimaryHandPoint")).Any(x=>x.Name=="BazookaLeftHandle")&&hands225.Methods.Where(m=>m.Name=="RenderPose").SelectMany(Calls).Any(x=>x.Name=="PrimaryHandPoint")
   &&!Calls(H5("BazookaHold")).Any(x=>x.Name=="TryPistolHold"||x.Name=="LongHandleContact"||x.Name=="MoveHold"||x.Name=="TryWeaponGrip")
   &&Calls(F7("FitGrip")).Any(x=>x.Name=="GripChannel")&&Calls(F7("FitGrip")).Any(x=>x.Name=="Clearance")&&Calls(F7("GripChannel")).Any(x=>x.Name=="FingerCircle")
   &&Calls(F7("HeldPose")).Any(x=>x.Name=="GripFit")&&Calls(F7("HeldPose")).Any(x=>x.Name=="ThumbAmount")
   &&Calls(H5("RocketHold")).Any(x=>x.Name=="RocketWrap")&&Calls(H5("RocketWrap")).Any(x=>x.Name=="GripFit")&&Calls(H5("RocketHold")).Any(x=>x.Name=="TryToolHold")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="RocketGripProfile")
   &&Calls(H5("BazookaGripHand")).Any(x=>x.Name=="get_PrimaryLeft"),"the bazooka's hands at a game hand's old place (off the grips), or the rocket's tube off the fingers' curls");
  // 0.1.228: the bazooka fitted without its rocket at its usual scale (a second one, its rocket held further back,
  // came out a third bigger and the hands off its handles); the thumb wraps round the handle too; the hold worked
  // out when the hands are drawn before the gun; why a bazooka hand is not on its handle is written to the log.
  var visual228=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(Calls(visual228.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="TrimRocket")&&Calls(visual228.Methods.Single(x=>x.Name=="TrimRocket")).Any(x=>x.Name=="RocketBone")
   &&Calls(visual228.Methods.Single(x=>x.Name=="TrimRocket")).Any(x=>x.Name=="WithoutAttachment")&&p.Types.Single(x=>x.Name=="WeaponGeometry").Fields.Any(x=>x.Name=="BazookaScale")
   &&Calls(visual228.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="Fit"&&x.DeclaringType.Name=="WeaponGeometry"&&x.Parameters.Count>=4)
   &&visual228.Methods.Single(x=>x.Name=="Build").Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-1.1f/.402614f)<1e-4f)
   &&Calls(H5("TryPoseHand")).Count(x=>x.Name=="NativeGrip")>=1&&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="NoteSupportGlove")
   &&Str(hands225,"BAZOOKA the hand holding it is not drawn on its handle: "),"the bazooka fitted by its length with its rocket (its size and handles changing with the rocket's place)");
  // 0.1.229: the bazooka drawn still as taken (its fit and grips were measured so), only its rocket moving with the
  // game (a shot, a reload or its empty pose moved the whole drawn bazooka off the hands); a hold report every few seconds.
  var snapshot229=p.Types.Single(x=>x.Name=="NativeSkinSnapshot");
  Require(Calls(visual228.Methods.Single(x=>x.Name=="RefreshAnimation")).Any(x=>x.Name=="BakeStill")&&Calls(visual228.Methods.Single(x=>x.Name=="RefreshAnimation")).Any(x=>x.Name=="get_HeldStill")
   &&Calls(snapshot229.Methods.Single(x=>x.Name=="BakeStill")).Any(x=>x.Name=="get_Initial")&&Calls(snapshot229.Methods.Single(x=>x.Name=="BakeStill")).Any(x=>x.Name=="Sample")
   &&Calls(visual228.Methods.Single(x=>x.Name=="MeasureTube")).Any(x=>x.Name=="BindRelation")&&Calls(visual228.Methods.Single(x=>x.Name=="LivePoses")).Any(x=>x.Name=="get_HeldStill")
   &&Calls(H5("TickBazooka")).Any(x=>x.Name=="BazookaHoldReport")&&Str(hands225,"BAZOOKA HOLD ")&&Calls(H5("BazookaHoldReport")).Any(x=>x.Name=="StillDrift")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="NoteHandDrawn")
   &&Calls(H5("TryPoseHand")).Any(x=>x.Name=="WeaponState")&&Calls(H5("WeaponState")).Any(x=>x.Name=="ControlGate")&&Calls(H5("TryPoseHand")).Any(x=>x.Name=="NoteHold"),
   "the bazooka drawn as the game animates it (off the hands on its grips), or no hold report");
  // 0.1.230: a story Timeline that is no Cutscene (the last mission's memory opening) is skipped too; in a memory (a playable
  // flashback) nobody is taken hostage, punched or grabbed (the game's own hostage rule there; Kim was taken by the VR rule).
  var rig229=p.Types.Single(x=>x.Name=="CameraRig");MethodDefinition R9(string n)=>rig229.Methods.Single(x=>x.Name==n);
  var allies229=p.Types.Single(x=>x.Name=="NpcAllies");
  Require(Calls(R9("SkipStoryInput")).Any(x=>x.Name=="StoryDirector")&&Calls(R9("SkipStoryInput")).Count(x=>x.Name=="StartFastForward")>=2
   &&Calls(R9("StoryDirector")).Any(x=>x.Name=="get_VirtualCameraGameObject")&&Calls(R9("StoryDirector")).Any(x=>x.Name=="FindObjectsOfType")&&Calls(R9("Skippable")).Any(x=>x.Name=="Skippable"&&x.DeclaringType.Name=="StorySkipPolicy")
   &&Calls(R9("TickStoryFastForward")).Any(x=>x.Name=="Finished")&&Calls(R9("Finished")).Any(x=>x.Name=="HeldAtEnd")
   &&Calls(allies229.Methods.Single(x=>x.Name=="Ally")).Any(x=>x.Name=="get_InFlashback")&&Calls(allies229.Methods.Single(x=>x.Name=="GameAllowsHostage")).Any(x=>x.Name=="get_InFlashback")
   &&Calls(allies229.Methods.Single(x=>x.Name=="get_InFlashback")).Any(x=>x.Name=="get_IsInFlashback"&&x.DeclaringType.Name=="GameManager"),
   "a story timeline that is no Cutscene cannot be skipped, or someone in a memory can be taken hostage");
  // 0.1.231: the hand on the bazooka's handle moved up it until the index fingertip is level with the trigger (its bone measured).
  Require(Calls(H5("BazookaHold")).Any(x=>x.Name=="RaiseToTrigger")&&Calls(H5("BazookaHold")).Any(x=>x.Name=="IndexPad"&&x.DeclaringType.Name=="NativeHandVisual")
   &&Calls(H5("BazookaHold")).Any(x=>x.Name=="get_TriggerPoint")&&Calls(H5("BazookaHold")).Any(x=>x.Name=="get_TriggerGripTop")
   &&Calls(visual228.Methods.Single(x=>x.Name=="FindGripBars")).Any(x=>x.Name=="TriggerBone")&&Calls(visual228.Methods.Single(x=>x.Name=="FindGripBars")).Any(x=>x.Name=="TopAlong"),
   "the bazooka's handle hand left low on the handle (its index below the trigger)");
  // 0.1.233: the world scale (the eye distance drawn divided by it) and the weapons' inertia in VR SETTINGS.
  var rig233=p.Types.Single(x=>x.Name=="CameraRig");var menu233=p.Types.Single(x=>x.Name=="QualityMenu");
  Require(Calls(rig233.Methods.Single(x=>x.Name=="get_EyeLeft")).Any(x=>x.Name=="ScaledEye")&&Calls(rig233.Methods.Single(x=>x.Name=="get_EyeRight")).Any(x=>x.Name=="ScaledEye")
   &&Calls(rig233.Methods.Single(x=>x.Name=="get_EyeLeft")).Any(x=>x.Name=="get_WorldScaleValue")&&Str(menu233,"World scale")&&Str(menu233,"Weapon inertia")
   &&Calls(p.Types.Single(x=>x.Name=="WeaponOptions").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="set_WeaponInertia")
   &&Calls(p.Types.Single(x=>x.Name=="QualityOptions").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="Bind"),"no world scale or weapon inertia in VR SETTINGS");
  // 0.1.234: the steady cutscene view (the film camera no longer turns the view) and the weapon places editor (VR SETTINGS).
  var rig234=p.Types.Single(x=>x.Name=="CameraRig");var page234=p.Types.Single(x=>x.Name=="VrSettingsPage");var editor234=p.Types.Single(x=>x.Name=="HolsterPlaceEditor");
  Require(Calls(rig234.Methods.Single(x=>x.Name=="ReadCinematicPose")).Any(x=>x.Name=="View"&&x.DeclaringType.Name=="CutsceneView")&&Calls(rig234.Methods.Single(x=>x.Name=="ReadCinematicPose")).Any(x=>x.Name=="ReadFilmPose")
   &&Calls(rig234.Methods.Single(x=>x.Name=="ReadCinematicPose")).Any(x=>x.Name=="get_CutsceneMode")&&Calls(rig234.Methods.Single(x=>x.Name=="ReadCinematicPose")).Any(x=>x.Name=="View"&&x.DeclaringType.Name=="CutsceneScreen")
   &&Calls(p.Types.Single(x=>x.Name=="CinematicMask").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="get_CinemaRotation")&&Calls(p.Types.Single(x=>x.Name=="FrontendMenu").Methods.Single(x=>x.Name=="RenderCore")).Any(x=>x.Name=="get_CinemaRotation")&&Calls(rig234.Methods.Single(x=>x.Name=="SampleCameraMode")).Any(x=>x.Name=="Reset"&&x.DeclaringType.Name=="CutsceneView")
   &&Str(menu233,"Cutscene camera"),"the film camera still turns the view in cutscenes, or no cutscene camera row");
  Require(Calls(page234.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="HolsterPlaceEditor")&&Calls(page234.Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="Render"&&x.DeclaringType.Name=="HolsterPlaceEditor")
   &&Calls(editor234.Methods.Single(x=>x.Name=="TickCore")).Any(x=>x.Name=="MoveTo"&&x.DeclaringType.Name=="HolsterPlaces")&&Calls(editor234.Methods.Single(x=>x.Name=="TickCore")).Any(x=>x.Name=="Pick")
   &&Calls(editor234.Methods.Single(x=>x.Name=="EndDrag")).Any(x=>x.Name=="Save")&&Calls(p.Types.Single(x=>x.Name=="BodyHolsters").Methods.Single(x=>x.Name=="Torso")).Any(x=>x.Name=="Torso"&&x.DeclaringType.Name=="HolsterPlaces")
   &&Calls(p.Types.Single(x=>x.Name=="HolsterLayout").Methods.Single(x=>x.Name=="Pose"&&x.Parameters.Count==3)).Any(x=>x.Name=="Invoke")
   &&Calls(p.Types.Single(x=>x.Name=="QualityOptions").Methods.Single(x=>x.Name=="Load")).Any(x=>x.Name=="set_Offset"||x.Name=="Load"&&x.DeclaringType.Name=="HolsterPlaces")
   &&Str(menu233,"Weapon places")&&Str(p.Types.Single(x=>x.Name=="UiLanguage"),"МЕСТА ОРУЖИЯ"),"the weapon places cannot be moved with the ray in VR SETTINGS");
  // 0.1.235: the world scale also moves the eyes Unity's OpenXR plugin renders from (xiii_openxr.dll XO_SetViewScale).
  var xrLoader235=p.Types.Single(x=>x.Name=="OpenXrLoader");
  Require(Calls(p.Types.Single(x=>x.Name=="OpenXrTracking").Methods.Single(x=>x.Name=="RefreshPoses")).Any(x=>x.Name=="ApplyWorldScale")
   &&Calls(xrLoader235.Methods.Single(x=>x.Name=="ApplyWorldScale")).Any(x=>x.Name=="ViewScale"&&x.DeclaringType.Name=="PoseMath")
   &&Str(xrLoader235,"XO_SetViewScale")&&Str(xrLoader235,"XO_Views")&&Calls(xrLoader235.Methods.Single(x=>x.Name=="Initialize")).Any(x=>x.Name=="Optional"),
   "the world scale reaches only the mod's own eye matrices, not the eyes Unity's OpenXR plugin renders from");
  // 0.1.236: the start screen moved on as the game does on a button (no key, no window in front needed); the window really in front (not Unity's isFocused), brought there by joining the input of the window in front.
  var keys236=p.Types.Single(x=>x.Name=="MenuKeyboard");var focus236=p.Types.Single(x=>x.Name=="StartupFocus");
  Require(Calls(keys236.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Advance")&&Calls(keys236.Methods.Single(x=>x.Name=="Advance")).Any(x=>x.Name=="set_m_state")
   &&Calls(keys236.Methods.Single(x=>x.Name=="Advance")).Any(x=>x.Name=="TryPlayWithDelay")&&Calls(keys236.Methods.Single(x=>x.Name=="Advance")).Any(x=>x.Name=="get_outroSplashscreen")
   &&Calls(focus236.Methods.Single(x=>x.Name=="BringForward")).Any(x=>x.Name=="AttachThreadInput")&&Calls(focus236.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="GameInFront")
   &&Calls(p.Types.Single(x=>x.Name=="WindowFocus").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="GameInFront"),
   "the start screen waits for a key Windows will not take (the game started behind Steam), or the focus is judged by Unity's isFocused");
  // 0.1.237: STEREO CHECK compares the eyes' pictures. 0.1.238: each eye drawn from its own place (the camera at the eye of the pass); the eye height no longer changed by the world scale.
  var tracked238=p.Types.Single(x=>x.Name=="TrackedCamera");
  Require(Calls(tracked238.Methods.Single(x=>x.Name=="ApplyEyes")).Any(x=>x.Name=="PassPose")&&Calls(tracked238.Methods.Single(x=>x.Name=="PassPose")).Any(x=>x.Name=="get_stereoActiveEye")
   &&Calls(tracked238.Methods.Single(x=>x.Name=="PassPose")).Any(x=>x.Name=="get_EyeLeft")&&Calls(tracked238.Methods.Single(x=>x.Name=="PassPose")).Any(x=>x.Name=="get_EyesPerPassOn")
   &&!Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="ReadBodyAnchor")).Any(x=>x.Name=="get_WorldScaleValue"),
   "both eyes drawn from the middle of the head (no depth), or the eye height changed by the world scale");
  Require(Calls(p.Types.Single(x=>x.Name=="StereoCheck").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_WorldScaleValue")
   &&p.Types.Single(x=>x.Name=="StereoCheck").NestedTypes.Any(t=>t.Methods.Any(m=>Calls(m).Any(x=>x.Name=="Measure"&&x.DeclaringType.Name=="StereoDisparity"))),
   "the eyes' pictures are never compared");
  // 0.1.239: the world scale 120% by default, moved there once for everyone. 0.1.257: 100%, a scale left at 120% moved there once.
  Require(Str(p.Types.Single(x=>x.Name=="QualityOptions"),"WorldScaleDefaultsVersion")&&p.Types.Single(x=>x.Name=="QualityOptions").Fields.Any(f=>f.Name=="WorldScaleDefault"&&f.HasConstant&&Math.Abs((float)f.Constant-1f)<1e-6f)
   &&p.Types.Single(x=>x.Name=="QualityOptions").Fields.Any(f=>f.Name=="WorldScaleDefaults"&&f.HasConstant&&(int)f.Constant==257),"the world scale is not 100% by default");
  // 0.1.240: F9 while VR runs is ignored.
  Require(Str(p.Types.Single(x=>x.Name=="Bootstrap"),"START ignored (F9): VR is already running (it starts by itself; F10 stops it)"),"F9 starts VR again over the running session");
  // 0.1.241: the revolver's cylinder and the double-barrel stay open until shut: their flicks measured in the room (not against the head).
  var hands241=p.Types.Single(x=>x.Name=="WeaponHands");
  Require(Calls(hands241.Methods.Single(x=>x.Name=="TickRevolver")).Any(x=>x.Name=="SampleTrackedHand")&&Calls(hands241.Methods.Single(x=>x.Name=="TickBreak")).Any(x=>x.Name=="SampleTrackedHand")
   &&!Calls(hands241.Methods.Single(x=>x.Name=="TickRevolver")).Any(x=>x.Name=="get_HeadPosition"),"the revolver or the double-barrel shuts by itself when the head moves");
  // 0.1.242: a magazine changed with rounds left keeps the chambered round (no bolt or slide to work).
  var manual242=p.Types.Single(x=>x.Name=="ManualReloadState");
  Require(Calls(manual242.Methods.Single(x=>x.Name=="Inserted")).Any(x=>x.Name=="get_ChamberKept")&&Calls(manual242.Methods.Single(x=>x.Name=="Detach")).Any(x=>x.Name=="set_ChamberKept"),"a magazine changed with rounds left still asks for the bolt");
  // 0.1.243: the plain pistol fitted without its hidden silencer (drawn 22 cm, not 17); the revolver drawn 37 cm, its hand-made offsets grown with it.
  var visual243=p.Types.Single(x=>x.Name=="WeaponVisual");MethodDefinition V3(string n)=>visual243.Methods.Single(x=>x.Name==n);
  Require(Calls(V3("TrimAttachment")).Any(x=>x.Name=="Split"&&x.DeclaringType.Name=="WeaponGeometry")&&Calls(V3("TrimAttachment")).Any(x=>x.Name=="Shown"&&x.DeclaringType.Name=="WeaponGeometry")
   &&Calls(V3("Grown")).Any(x=>x.Name=="get_RevolverGrowth")&&Calls(V3("get_CylinderSocket")).Any(x=>x.Name=="Grown")&&Calls(V3("EjectCasings")).Any(x=>x.Name=="Grown")&&Calls(V3("PoseCylinder")).Any(x=>x.Name=="Grown")
   &&p.Types.Single(x=>x.Name=="EquipmentProfile").Methods.Single(x=>x.Name=="Length"&&x.Parameters.Count==1).Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-.37f)<1e-6f),"the plain pistol still drawn small (its hidden silencer in the fit), or the revolver still 28 cm");
  // 0.1.244: a blow breaks what the game breaks when used (the sanctuary's vent); the Uzi drawn 35 cm.
  var punch244=p.Types.Single(x=>x.Name=="PunchDriver");var touch244=p.Types.Single(x=>x.Name=="TouchButtons");
  Require(punch244.Methods.Concat(punch244.NestedTypes.SelectMany(t=>t.Methods)).Any(m=>Calls(m).Any(x=>x.Name=="Strike"&&x.DeclaringType.Name=="InteractionDriver"))
   &&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="Strike")).Any(x=>x.Name=="Strike"&&x.DeclaringType.Name=="TouchButtons")
   &&Calls(touch244.Methods.Single(x=>x.Name=="Strike")).Any(x=>x.Name=="PingRaycastHittable")&&Calls(touch244.Methods.Single(x=>x.Name=="Strike")).Any(x=>x.Name=="set_Injecting")
   &&Calls(p.Types.Single(x=>x.Name=="TouchControlReader").Methods.Single(x=>x.Name=="Read"&&x.Parameters.Count==5)).Any(x=>x.Name=="Breaks")
   &&p.Types.Single(x=>x.Name=="EquipmentProfile").Methods.Single(x=>x.Name=="Length"&&x.Parameters.Count==1).Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-.35f)<1e-6f),"a fist or a weapon still does not break the sanctuary's vent (or the Uzi still 46 cm)");
  // 0.1.245: story movies on a still screen; the forearms optional; a hand that took a weapon picks nothing else up; a selected weapon's own model hidden at once.
  var rig245=p.Types.Single(x=>x.Name=="CameraRig");
  Require(Calls(rig245.Methods.Single(x=>x.Name=="PrepareUiPose")).Any(x=>x.Name=="PoseMovieScreen")&&Calls(rig245.Methods.Single(x=>x.Name=="PoseMovieScreen")).Any(x=>x.Name=="set_CinemaRotation")
   &&Calls(p.Types.Single(x=>x.Name=="StoryVideo").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="get_CinemaRotation")&&!Calls(p.Types.Single(x=>x.Name=="StoryVideo").Methods.Single(x=>x.Name=="Render")).Any(x=>x.Name=="get_HeadRotation")
   &&Calls(p.Types.Single(x=>x.Name=="GloveVisual").Methods.Single(x=>x.Name=="BindNative")).Any(x=>x.Name=="get_ForearmsOn")&&Calls(p.Types.Single(x=>x.Name=="NativeHandVisual").Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="ForearmCut")
   &&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="PickupTarget")).Any(x=>x.Name=="HandTaken")&&Calls(p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Single(x=>x.Name=="OffPickupTarget")).Any(x=>x.Name=="HandTaken")
   &&p.Types.Single(x=>x.Name=="WeaponHands").Methods.Any(m=>Calls(m).Any(x=>x.Name=="ScanSoon"&&x.DeclaringType.Name=="FirstPersonVisibility"))
   &&Str(p.Types.Single(x=>x.Name=="UiLanguage"),"Предплечья"),"movies still follow the head, the forearms not optional, one grip takes a weapon and a thing, or a selected weapon's own model shown");
  // 0.1.246: a door the game had not set up: its interaction found by what it animates; its missing door info does not stop the hands.
  var doors246=p.Types.Single(x=>x.Name=="PhysicalDoors");MethodDefinition D6(string n)=>doors246.Methods.Single(x=>x.Name==n);
  Require(Calls(D6("DoorActions")).Any(x=>x.Name=="AnimatingActions")&&doors246.Methods.Concat(doors246.NestedTypes.SelectMany(t=>t.Methods)).Any(m=>Calls(m).Any(x=>x.Name=="Animates"&&x.DeclaringType.Name=="DoorStoryEvents"))
   &&Calls(D6("Discover")).Any(x=>x.Name=="DoorActions")&&Calls(D6("DoorInfo")).Any(x=>x.Name=="UpdateDoorInfo")&&Calls(D6("DoorInfo")).Any(x=>x.Name=="get_m_doorsInfo")
   &&!Calls(D6("Move")).Any(x=>x.Name=="UpdateDoorInfo")&&!Calls(D6("Commit")).Any(x=>x.Name=="UpdateDoorInfo"),"a door with an empty list of interactions still not opened by the hand (or its missing door info stops the hands)");
  // 0.1.247: a door listing only another interaction: the player's own is used (it moves the leaf, or the game targets it).
  var doors247=p.Types.Single(x=>x.Name=="PhysicalDoors");
  Require(Calls(doors247.Methods.Single(x=>x.Name=="DoorActions")).Any(x=>x.Name=="AnimatingActions")&&Calls(doors247.Methods.Single(x=>x.Name=="Unlocked")).Any(x=>x.Name=="IsActorValid")
   &&p.Types.Single(x=>x.Name=="InteractionDriver").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stsfld&&i.Operand is FieldReference f&&f.Name=="GameTarget"&&f.DeclaringType.Name=="PhysicalDoors")
   &&doors247.Methods.Single(x=>x.Name=="Discover").Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldsfld&&i.Operand is FieldReference f&&f.Name=="GameTarget"),"a door listing only another interaction still not opened by the hand");
  // 0.1.248: an aim dot (VR SETTINGS): where the gun's shot goes, from its muzzle along it.
  var hands248=p.Types.Single(x=>x.Name=="WeaponHands");var dot248=p.Types.Single(x=>x.Name=="AimDot");
  Require(Calls(hands248.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.Name=="ShowGunDot")&&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="RenderLeftPistol")).Any(x=>x.Name=="ShowGunDot")
   &&Calls(hands248.Methods.Single(x=>x.Name=="ShowGunDot")).Any(x=>x.Name=="get_AimDotMode")&&Calls(hands248.Methods.Single(x=>x.Name=="ShowGunDot")).Any(x=>x.Name=="get_ShotMask")
   &&Calls(dot248.Methods.Single(x=>x.Name=="Show")).Any(x=>x.Name=="RaycastNonAlloc")&&Calls(hands248.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="HideUnlessShown")
   &&Str(p.Types.Single(x=>x.Name=="UiLanguage"),"Точка прицела"),"no aim dot for the guns");
  // 0.1.249: the shotguns drawn 1.08 m long (the hand on them as on the other guns).
  Require(p.Types.Single(x=>x.Name=="EquipmentProfile").Methods.Single(x=>x.Name=="Length"&&x.Parameters.Count==1).Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-1.08f)<1e-6f)
   &&p.Types.Single(x=>x.Name=="ContactSolver").Methods.Single(x=>x.Name=="Weapon").Body.Instructions.Any(i=>i.Operand is float f&&Math.Abs(f-1.08f)<1e-6f),"the shotguns still drawn 0.95 m (the hand on them small)");
  // 0.1.257: the AK, the M16 and the crossbows drawn 10% larger (the hand on them kept its size, its fist on the grip).
  Require(p.Types.Single(x=>x.Name=="EquipmentProfile").Fields.Any(f=>f.Name=="RifleGrowth"&&f.HasConstant&&Math.Abs((float)f.Constant-1.1f)<1e-6f)
   &&Calls(p.Types.Single(x=>x.Name=="EquipmentProfile").Methods.Single(x=>x.Name=="Length"&&x.Parameters.Count==1)).Any(x=>x.Name=="Growth")
   &&Calls(p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="Hand"&&x.DeclaringType.Name=="GrownGrip"),"the AK, the M16 and the crossbows still at the game's size (or the hand on them grown with them)");
  // 0.1.256: the belt's pouch in the middle of the front, the shells on both hips; the belly's long gun in front of
  // the pouch (27 cm ahead), the belt pistols outside the shells (25 cm out).
  {var layout256=p.Types.Single(x=>x.Name=="HolsterLayout").Methods.Single(x=>x.Name=="Pose"&&x.Parameters.Count==3).Body.Instructions;
   Require(p.Types.Single(x=>x.Name=="AmmoPouch").Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is string o&&o.Contains("the pouch in front, the shells on both hips"))&&layout256.Any(i=>i.Operand is float f&&Math.Abs(f-.27f)<1e-6f)
    &&layout256.Any(i=>i.Operand is float g&&Math.Abs(g+.25f)<1e-6f)&&layout256.Any(i=>i.Operand is float h&&Math.Abs(h-.25f)<1e-6f)&&!layout256.Any(i=>i.Operand is float k&&Math.Abs(k-.17f)<1e-6f),"the belt's pouch not in front, or a weapon place still in it");}
  // 0.1.255: the ammunition belt a 3D model (BeltModel), its pouch on the left hip, the reserve's shells in their loops.
  var pouch255=p.Types.Single(x=>x.Name=="AmmoPouch");
  Require(Calls(pouch255.Methods.Single(x=>x.Name=="SetShellCount")).Any(x=>x.Name=="Build"&&x.DeclaringType.Name=="BeltModelMath")&&Calls(pouch255.Methods.Single(x=>x.Name=="NearShell")).Any(x=>x.Name=="AtShell")
   &&Calls(pouch255.Methods.Single(x=>x.Name=="Near")).Any(x=>x.Name=="AtPouch")&&!p.Types.Any(x=>x.Name=="AmmoPouchGeometry")
   &&!p.Types.Single(x=>x.Name=="ToneMeshVisual").Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Canvas"||x.DeclaringType.Name=="RectTransform"),"the old built belt still worn (or the new one's shells not the reserve's)");
  // 0.1.254: the 3D watch (the classic mod's model) on each wrist, its LCD face a picture drawn when its numbers change, no UI.
  var glove254=p.Types.Single(x=>x.Name=="GloveVisual");var watch254=p.Types.Single(x=>x.Name=="WatchVisual");
  Require(Calls(glove254.Methods.Single(x=>x.Name=="RefreshDisplay")).Any(x=>x.Name=="Draw"&&x.DeclaringType.Name=="WatchFacePixels")&&Calls(glove254.Methods.Single(x=>x.Name=="RefreshDisplay")).Any(x=>x.Name=="SetFace")
   &&Calls(glove254.Methods.Single(x=>x.Name=="BindNative")).Any(x=>x.Name=="Fit"&&x.DeclaringType.Name=="WatchVisual")&&Calls(glove254.Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="ShowFace")
   &&Calls(watch254.Methods.Single(x=>x.Name=="Fit")).Any(x=>x.Name=="Build"&&x.DeclaringType.Name=="WatchModelMath")&&Calls(watch254.Methods.Single(x=>x.Name=="SetFace")).Any(x=>x.Name=="SetPixels32")
   &&!watch254.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Canvas"||x.DeclaringType.Name=="RectTransform")&&!p.Types.Any(x=>x.Name=="HandMeshGeometry"&&x.Methods.Any(m=>m.Name=="BuildWatch")),"the old built watch still on the wrists (or the 3D watch's face not drawn / placed as UI)");
  // 0.1.253: a thing to take is never a door (either grip takes it, its hint says so); a grip press at a thing that took nothing says why.
  var hints253=p.Types.Single(x=>x.Name=="InteractionHints");var driver253=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(Calls(hints253.Methods.Single(x=>x.Name=="IsDoor")).OfType<GenericInstanceMethod>().Any(g=>g.Name=="Of"&&g.GenericArguments.Any(a=>a.Name=="PickableItem"))
   &&Calls(driver253.Methods.Single(x=>x.Name=="get_DoorTarget")).Any(x=>x.Name=="IsDoor"&&x.DeclaringType.Name=="InteractionHints")
   &&Calls(driver253.Methods.Single(x=>x.Name=="PickupTarget")).Any(x=>x.Name=="Blocked")&&Str(driver253,": not taken ("),"a bottle among the lockers still taken with Grip + A only (and its hint still Grip + A)");
  // 0.1.252: the tube scope's picture on the eyepiece's own glass; the measured reticle turn the plain crossbow's only.
  var visual252=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(Calls(visual252.Methods.Single(x=>x.Name=="BuildTubeScope")).Any(x=>x.Name=="FindGlass"&&x.DeclaringType.Name=="ScopeGeometry")&&Calls(visual252.Methods.Single(x=>x.Name=="BuildTubeScope")).Any(x=>x.Name=="OnGlass")
   &&Calls(visual252.Methods.Single(x=>x.Name=="CreateScope")).Any(x=>x.Name=="get_ModelKey"),"the tactical crossbow's picture still small in front of its glass, or its cross still turned 6 degrees clockwise");
  // 0.1.251: the crossbows' sights searched at the size they were measured at; the harpoon gun's bands drawn like a string; each crossbow its own files.
  var visual251=p.Types.Single(x=>x.Name=="WeaponVisual");MethodDefinition V251(string n)=>visual251.Methods.Single(x=>x.Name==n);
  Require(Calls(V251("BuildTubeScope")).Count(x=>x.Name=="ToTuned"&&x.DeclaringType.Name=="ScopeGeometry")>=2&&Calls(V251("BuildTubeScope")).Count(x=>x.Name=="FromTuned"&&x.DeclaringType.Name=="ScopeGeometry")>=2
   &&visual251.Methods.Any(x=>x.Name=="get_ModelKey")&&Calls(V251("Build")).Any(x=>x.Name=="ModelKey"&&x.DeclaringType.Name=="EquipmentProfile")
   &&p.Types.Single(x=>x.Name=="EquipmentProfile").Fields.Any(f=>f.Name=="ScopeTunedLength"&&f.HasConstant&&Math.Abs((float)f.Constant-.72f)<1e-6f),"the tactical crossbow's sight still searched at its new size (a narrow picture hidden inside the scope)");
  Require(Calls(V251("PrepareString")).Any(x=>x.Name=="StringBone"&&x.DeclaringType.Name=="CrossbowStringMath")&&Calls(V251("PrepareString")).Any(x=>x.Name=="get_StringFile")&&Calls(V251("get_StringFile")).Any(x=>x.Name=="ModelFile")
   &&Calls(V251("get_BoltFile")).Any(x=>x.Name=="ModelFile")&&Calls(V251("PrepareReload")).Any(x=>x.Name=="DumpModel")&&Calls(V251("DumpModel")).Any(x=>x.Name=="DumpMesh"),"the harpoon gun's bands still the game's (slack after a harpoon put in by hand)");
  // 0.1.250: the crossbows drawn by their model (the hand on the harpoon gun as on the other guns); the forearm's cut closed by one flat cap.
  var profile250=p.Types.Single(x=>x.Name=="EquipmentProfile");var mesh250=p.Types.Single(x=>x.Name=="NativeHandMesh");
  Require(Calls(p.Types.Single(x=>x.Name=="WeaponVisual").Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="Fit"&&x.DeclaringType.Name=="WeaponGeometry"&&x.Parameters.Count==5)
   &&Calls(p.Types.Single(x=>x.Name=="WeaponGeometry").Methods.Single(x=>x.Name=="Fit")).Any(x=>x.Name=="Length"&&x.DeclaringType.Name=="EquipmentProfile"&&x.Parameters.Count==2)
   &&Calls(profile250.Methods.Single(x=>x.Name=="Length"&&x.Parameters.Count==2)).Any(x=>x.Name=="CrossbowLength")
   &&profile250.Fields.Any(f=>f.Name=="HarpoonLength"&&f.HasConstant&&Math.Abs((float)f.Constant-1.03f)<1e-6f),"the harpoon gun still drawn as small as the crossbow (the hand on it at 0.69)");
  Require(Calls(mesh250.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="CloseCut")&&Calls(mesh250.Methods.Single(x=>x.Name=="CloseCut")).Any(x=>x.Name=="Outline")&&Calls(mesh250.Methods.Single(x=>x.Name=="CloseCut")).Any(x=>x.Name=="CapPoint")
   &&Calls(p.Types.Single(x=>x.Name=="NativeHandVisual").Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="get_CapSource"),"the forearm's cut still a jagged fan of its rim (gaps and streaks through a sleeve of layers)");
  // 0.1.220: a turned lockpick runs the game's lockpicking time on its HUD before the lock opens.
  var key220=p.Types.Single(x=>x.Name=="KeyUnlockGesture");MethodDefinition K0(string n)=>key220.Methods.Single(x=>x.Name==n);
  Require(Calls(K0("Tick")).Any(x=>x.Name=="BeginPicking")&&Calls(K0("Tick")).Any(x=>x.Name=="TickPicking")&&Calls(K0("BeginPicking")).Any(x=>x.Name=="ToggleLockpickingHud")&&Calls(K0("BeginPicking")).Any(x=>x.Name=="get_lockpickTime")
   &&Calls(K0("BeginPicking")).Any(x=>x.Name=="get_InstantLockPick")&&Calls(K0("TickPicking")).Any(x=>x.Name=="UpdateLockpickingHud")&&Calls(K0("TickPicking")).Any(x=>x.Name=="Complete")&&Calls(K0("TickPicking")).Any(x=>x.Name=="GenerateNoise")
   &&Calls(K0("StopPicking")).Any(x=>x.Name=="ToggleLockpickingHud")&&Calls(K0("Cancel")).Any(x=>x.Name=="StopPicking")&&Calls(K0("PickSound")).Any(x=>x.Name=="Start"&&x.DeclaringType.Name=="NativeItemCue"),"a lockpick opens the lock at the turn, without the game's lockpicking time");
  var key203=p.Types.Single(x=>x.Name=="KeyUnlockGesture");MethodDefinition K3(string n)=>key203.Methods.Single(x=>x.Name==n);
  Require(Calls(K3("Tick")).Any(x=>x.Name=="Card"&&x.DeclaringType.Name=="UnlockGestureMath")&&Calls(K3("Tick")).Any(x=>x.Name=="CardTouches")
   &&Calls(K3("CardTouches")).Any(x=>x.Name=="WorldProbe")&&Calls(K3("Intercept")).Any(x=>x.Name=="CollectReader")
   &&Str(p.Types.Single(x=>x.Name=="VrPromptLabels"),"Hold card to reader")&&Str(p.Types.Single(x=>x.Name=="UiLanguage"),"Приложите карту"),"the card still has to be swiped (not held to the reader)");
  var heldMotion=p.Types.Single(x=>x.Name=="HeldPropMotion");
  Require(Calls(heldMotion.Methods.Single(x=>x.Name=="Follow")).Any(x=>x.Name=="SweepTest")&&Calls(heldMotion.Methods.Single(x=>x.Name=="Follow")).Any(x=>x.Name=="ComputePenetration"),"held prop translation and rotation check native collisions");
  var doors=p.Types.Single(x=>x.Name=="PhysicalDoors");
  Require(Calls(doors.Methods.Single(x=>x.Name=="Unlocked")).Any(x=>x.Name=="IsInteractionBlocked"),"physical doors respect native locks");
  Require(Calls(doors.Methods.Single(x=>x.Name=="Commit")).Any(x=>x.Name=="DoorInfo")&&Calls(doors.Methods.Single(x=>x.Name=="DoorInfo")).Any(x=>x.Name=="UpdateDoorInfo"),"door release updates native navigation state");
  Require(!doors.Methods.SelectMany(Calls).Any(x=>x.Name=="UpdateOcclusionPortal"),"doors never call native optional-portal dereference that cancelled grips");
  var throwing=p.Types.Single(x=>x.Name=="WeaponHands").Methods.Single(x=>x.Name=="LaunchProp");
  Require(Calls(throwing).Any(x=>x.Name=="Begin"&&(x.DeclaringType.Name=="ThrowingComponent"||x.DeclaringType.Name=="FireComponent"||x.DeclaringType.Name=="EquipableComponent"))&&!Calls(throwing).Any(x=>x.Name=="HandleProjectile"),"prop throw initializes native use instead of bypassing Begin");
  var aimed=p.Types.Single(x=>x.Name=="WeaponHands").Methods.Where(x=>x.Name=="UpdatePropThrow"||x.Name=="SwingThrow").ToArray();
  Require(aimed.Length==2&&aimed.All(m=>Calls(m).Any(x=>x.DeclaringType.Name=="ControllerAim")),"throw trajectory follows controller independently of sideways mesh");
  var blink=p.Types.Single(x=>x.Name=="StoryBlink");
  Require(!blink.Fields.Any(x=>x.FieldType.FullName=="UnityEngine.RenderTexture"),"blink retains animation parameters only, never a shared eye image");
  Require(!blink.Fields.Any(x=>x.FieldType.FullName=="UnityEngine.Material"),"blink cannot replay desktop Sobel or distortion materials");
  Require(Calls(blink.Methods.Single(x=>x.Name=="Sample")).Any(x=>x.Name=="get_EyeUpperEyeLid"),"blink reads Timeline eyelid values only");
  var handAdapter=p.Types.Single(x=>x.Name=="NativeHandVisual");
  Require(!handAdapter.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:Enumerable.Empty<Mono.Cecil.Cil.Instruction>()).Any(i=>i.Operand is string s&&s=="player_arm"),"hand binding no longer requires beach outfit mesh name");
  var visualMap=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(Calls(visualMap.Methods.Single(x=>x.Name=="get_GameWorldToFitted")).Any(x=>x.Name=="GripCorrection"),"stable weapon grips use the same frozen root as rendered geometry");
  Require(Calls(p.Types.Single(x=>x.Name=="NativeHandVisual").Methods.Single(x=>x.Name=="TryWeaponGrip")).Any(x=>x.Name=="get_AnchorKey"),"grip cache separates concrete visuals and stable mode");
  Require(!p.Types.Single(x=>x.Name=="FocusMarkers").Methods.SelectMany(Calls).Any(x=>x.Name=="Set"&&x.DeclaringType.Name=="FocusHUDElement"),"objective indicators do not use desktop widget presentation");
  var weapons=p.Types.Single(x=>x.Name=="WeaponHands");
  // 0.1.107: VR death screen uses the native screen's own buttons; the lockpick fist turns the hand.
  var deathVr=p.Types.Single(x=>x.Name=="DeathScreenVr");
  var deathCalls=deathVr.Methods.SelectMany(Calls).Select(x=>x.Name).ToList();
  Require(deathCalls.Contains("OnRetryButtonPressed")&&deathCalls.Contains("OnQuitButtonPressed")&&deathCalls.Contains("get_isStarted"),"death screen panel does not drive the native death screen");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="DeathScreenVr"),"death screen overlay not ticked");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="WaterSplash"),"water splashes not ticked");
  Require(Calls(p.Types.Single(x=>x.Name=="CameraRig").Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Tick"&&x.DeclaringType.Name=="EnemyAi"),"smarter enemies not ticked");
  // 0.1.115: 0.1.114 closed the game at the main menu by touching the AI's
  // shared values before the game had set its AI classes up.
  var enemyAi=p.Types.Single(x=>x.Name=="EnemyAi");
  var enemyMethods=enemyAi.Methods.Concat(enemyAi.NestedTypes.SelectMany(t=>t.Methods)).ToList();
  var enemyTick=Calls(enemyAi.Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList();
  Require(enemyTick.IndexOf("Gameplay")>=0&&enemyTick.IndexOf("Settled")>enemyTick.IndexOf("Gameplay")&&enemyTick.IndexOf("Scan")>enemyTick.IndexOf("Settled")&&!enemyTick.Any(n=>n.StartsWith("Apply")),"smarter enemies act outside settled gameplay");
  // 0.1.116: the AI's "shared values" are constants compiled into the game;
  // reading them (static field accessors of T5AI) closed the game.
  var t5aiStatics=p.Types.SelectMany(t=>t.Methods.Concat(t.NestedTypes.SelectMany(n=>n.Methods))).SelectMany(m=>Calls(m).Select(c=>(m,c))).Where(x=>x.c.DeclaringType.Namespace=="T5AI"&&!x.c.HasThis&&(x.c.Name.StartsWith("get_")||x.c.Name.StartsWith("set_"))).Select(x=>x.m.DeclaringType.Name+"."+x.m.Name+"->"+x.c.Name).ToList();
  Require(t5aiStatics.Count==0,"T5AI static values never read or written: "+string.Join(",",t5aiStatics));
  var visitCalls=Calls(enemyAi.Methods.Single(x=>x.Name=="Visit")).Select(x=>x.Name).ToList();
  Require(visitCalls.IndexOf("NativeReady")>=0&&visitCalls.IndexOf("NativeReady")<visitCalls.IndexOf("TryCast")&&enemyTick.IndexOf("Visit")>enemyTick.IndexOf("Settled"),"searching enemies touched before the game set its search AI up");
  Require(visitCalls.Contains("LongerSearch")&&visitCalls.Contains("SlowIdle")&&visitCalls.Contains("Hunch")&&visitCalls.Count(n=>n=="Probe")==3,"search improvements missing or not guarded on first use");
  // 0.1.117: tactics hooks only after the game set each action class up; no pursue flag.
  var tacticsCalls=Calls(enemyAi.Methods.Single(x=>x.Name=="PatchTactics")).Select(x=>x.Name).ToList();
  Require(tacticsCalls.IndexOf("NativeReady")>=0&&tacticsCalls.IndexOf("NativeReady")<tacticsCalls.IndexOf("Probe")&&!tacticsCalls.Contains("Patch"),"tactics hooks installed before the game set the AI up or outside the crash guard");
  Require(Calls(enemyAi.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="PatchTactics")&&!enemyMethods.SelectMany(Calls).Any(x=>x.Name=="set_canFollowTarget"),"cover tactics not installed, or enemies still made to charge (pursue flag)");
  Require(Calls(enemyAi.Methods.Single(x=>x.Name=="Weigh")).Any(x=>x.Name=="set_Score")&&Calls(enemyAi.Methods.Single(x=>x.Name=="ChargeScore")).Any(x=>x.Name=="HasWeapon"),"action scores not weighed, or unarmed enemies held back from their only attack");
  var scanCalls=Calls(enemyAi.Methods.Single(x=>x.Name=="Scan")).Select(x=>x.Name).ToList();
  Require(scanCalls.IndexOf("NativeReady")>=0&&scanCalls.IndexOf("NativeReady")<scanCalls.IndexOf("Probe")&&!scanCalls.Contains("FindObjectsOfTypeAll"),"enemy list read before the game set the AI up");
  Require(Calls(enemyAi.Methods.Single(x=>x.Name=="NativeClassReady")).Any(x=>x.Name=="get_InitializedAndNoError")&&!Calls(enemyAi.Methods.Single(x=>x.Name=="NativeClassReady")).Any(x=>x.Name.Contains("class_init")),"native AI class readiness not read from IL2CPP's class record");
  Require(Calls(enemyAi.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="Recover"),"a run that closed while touching the AI does not turn smarter enemies off");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="FireHand")).Any(x=>x.Name=="Begin"&&x.DeclaringType.Name=="FireComponent"),"dual triggers dispatch to individual native fire components");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="TickDual")).Count(x=>x.Name=="FireHand")==2,"both hands have independent fire dispatch");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="AimLaunch")).Any(x=>x.Name=="IsLeft"),"late projectile aim routes by owning hand");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="BeginFireTask")).Any(x=>x.Name=="WriteMuzzle"),"native delayed fire task refreshes its own muzzle");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="SelectedShotSound")).Any(x=>x.Name=="PlayCue"),"shotgun native combined sound replaced with selected isolated shot");
  Require(!p.Types.Any(x=>x.Name=="ReloadSoundPcm"),"old synthesized reload generator removed from DLL");
  var sound=p.Types.Single(x=>x.Name=="ReloadAudio");
  Require(!sound.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="ReloadSoundPcm"),"reload audio has no synthesized fallback");
  var t=p.Types.Single(t=>t.Name=="Bootstrap");
  Require(!t.Methods.Any(m=>m.Name=="LogNode"),"LogNode removed from shipped DLL");
  Require(!p.GetTypeReferences().Any(r=>r.FullName.Contains("XRNodeState")),"shipped DLL has no XRNodeState reference");
  var report=t.Methods.Single(m=>m.Name=="ReportState");
  Require(report.Body.ExceptionHandlers.Any(),"diagnostics have their own exception boundary");
  var visited=new HashSet<string>();
  void Walk(MethodDefinition m)
  {
   if(!visited.Add(m.FullName)) return;
   foreach(var call in Calls(m))
   {
    if(call.Name=="StopXR" || call.Name=="Destroy" || call.Name=="Stop") throw new Exception("Diagnostics can stop XR: "+call.FullName);
    var local=p.Types.SelectMany(x=>x.Methods).FirstOrDefault(x=>x.FullName==call.FullName);
    if(local!=null) Walk(local);
   }
  }
  Walk(report); Require(true,"diagnostic call graph does not stop or destroy XR");
  var start=t.Methods.Single(m=>m.Name=="StartXR");
  var calls=Calls(start).ToList();
  // 0.1.181: the descriptors are read in CreateSubsystems (called by the start paths after the class init).
  calls.AddRange(Calls(t.Methods.Single(m=>m.Name=="CreateSubsystems")));
  Require(calls.FindIndex(x=>x.Name=="il2cpp_runtime_class_init")>=0 && calls.FindIndex(x=>x.Name=="il2cpp_runtime_class_init")<calls.FindIndex(x=>x.Name=="get_s_IntegratedDescriptors"),"native subsystem manager initialization still precedes descriptor lookup");
  Require(!p.GetTypeReferences().Any(r=>r.FullName.Contains("RegressionHarness")),"shipped DLL excludes simulated test APIs");
  var tracker=p.Types.Single(x=>x.Name=="OpenVrTracking");
  var trackerCalls=tracker.Methods.SelectMany(Calls).ToArray();
  Require(!trackerCalls.Any(x=>new[]{"Init","Shutdown","WaitGetPoses","Submit"}.Contains(x.Name)),"tracking client does not initialize, shut down, submit, or wait on Unity-owned OpenVR session");
  var driver=p.Types.Single(x=>x.Name=="TrackedCamera");
  foreach(string hidden in new[]{"Configure","GetBasePose","ApplyEyes","SetEye","SetProjection","ResetStereo","Restore","Release"}) Require(driver.Methods.Single(x=>x.Name==hidden).CustomAttributes.Any(x=>x.AttributeType.Name=="HideFromIl2CppAttribute"),"managed helper excluded from IL2CPP injection: "+hidden);
  Require(!Calls(driver.Methods.Single(x=>x.Name=="Restore")).Any(x=>x.Name=="ResetStereoViewMatrices" || x.Name=="ResetStereoProjectionMatrices"),"per-pass pose restore keeps stereo matrices alive");
  var restoreCalls=Calls(driver.Methods.Single(x=>x.Name=="Restore")).Select(x=>x.Name).ToArray();
  Require(restoreCalls.Contains("set_localPosition") && restoreCalls.Contains("set_localRotation") && !restoreCalls.Contains("SetPositionAndRotation"),"camera restores local pose instead of feeding stale world coordinates into its parent");
  var rig=p.Types.Single(x=>x.Name=="CameraRig");
  var uiPresentation=p.Types.Single(x=>x.Name=="FrontendMenu");
  foreach(string method in new[]{"Tick","Render"})Require(Calls(uiPresentation.Methods.Single(x=>x.Name==method)).Any(x=>x.DeclaringType.Name=="OptionalWork"&&x.Name=="Run"),"optional UI cannot escape to head tracking: "+method);
  var rigTickCalls=Calls(rig.Methods.Single(x=>x.Name=="Tick")).ToList();
  Require(rigTickCalls.Any(x=>x.DeclaringType.Name=="StoryColorEffect"&&x.Name=="Tick"),"effect carrier is activated before rendering, not only inside a render callback");
  Require(rigTickCalls.FindIndex(x=>x.Name=="CheckScene")<rigTickCalls.FindIndex(x=>x.Name=="Ready")&&rigTickCalls.Any(x=>x.DeclaringType.Name=="TrackingRecovery"&&x.Name=="Ready"),"rig observes scene changes before bounded retry gate");
  Require(Calls(rig.Methods.Single(x=>x.Name=="Fail")).Any(x=>x.DeclaringType.Name=="TrackingRecovery"&&x.Name=="Suspend")&&Calls(rig.Methods.Single(x=>x.Name=="CheckScene")).Any(x=>x.DeclaringType.Name=="TrackingRecovery"&&x.Name=="SceneChanged"),"tracking failure is transient and scene resets its recovery gate");
  Require(Calls(rig.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.DeclaringType.Name=="TrackingRecovery"&&x.Name=="Stop"),"explicit XR shutdown cannot retry itself");
  var frame=p.Types.Single(x=>x.Name=="CinematicFrame");
  foreach(string callback in new[]{"Populate","Rebuilt"})Require(!Calls(frame.Methods.Single(x=>x.Name==callback)).Any(x=>x.Name is "Remember" or "Clear" && x.DeclaringType.Name=="CinematicFrame"),"canvas callback does not mutate hierarchy: "+callback);
  Require(Calls(frame.Methods.Single(x=>x.Name=="ObjectName")).Any(x=>x.DeclaringType.FullName=="UnityEngine.Object"&&x.Name=="op_Equality"),"frame asset-name reads use native Unity lifetime check");
  foreach(string method in new[]{"Populate","Rebuilt","Clear","Inspect","AssetName","ObjectName"})Require(frame.Methods.Single(x=>x.Name==method).Body.ExceptionHandlers.Any(),"stale frame exception isolated: "+method);
  foreach(string callback in new[]{"OnPreCull","OnPreRender"})
  {
      var order=Calls(driver.Methods.Single(x=>x.Name==callback)).ToList();
      int guard=order.FindIndex(x=>x.Name=="GuardStereoEffects");
      int eyes=order.FindIndex(x=>x.Name=="ApplyEyes");
      Require(guard>=0&&eyes>guard,"stereo effect guard precedes eye rendering: "+callback);
  }
  Require(Calls(rig.Methods.Single(x=>x.Name=="GuardStereoEffects")).Any(x=>x.DeclaringType.Name=="CameraEffects"&&x.Name=="BeforeRender"),"tracked cameras use production effect guard");
  foreach(string consumer in new[]{"Prepare","SampleWorldHands","TryWorldHeadRotation"})
      Require(Calls(rig.Methods.Single(x=>x.Name==consumer)).Any(x=>x.Name=="ReadBodyAnchor"),"head/hands/heading share body anchor: "+consumer);
  Require(Calls(rig.Methods.Single(x=>x.Name=="ReadBodyAnchor")).Any(x=>x.DeclaringType.Name=="BodyAnchor" && x.Name=="Sample"),"camera anchor uses tested body-yaw/pivot math");
  Require(Calls(driver.Methods.Single(x=>x.Name=="SetProjection")).Any(x=>x.Name=="SetStereoProjectionMatrix"),"native eye projection is assigned to the stereo camera");
  var align=rig.Methods.Single(x=>x.Name=="AlignCollisionBody");
  Require(Calls(align).Any(x=>x.DeclaringType.Name=="CharacterController"&&x.Name=="Move"),"body alignment uses native collision sweep");
  Require(!Calls(align).Any(x=>x.DeclaringType.Name=="Transform"&&x.Name.StartsWith("set_")),"body alignment never teleports transform");
  Require(Calls(align).Any(x=>x.Name=="ConsumeBodyMove"),"actual collision displacement compensated in anchor");
  var capture=p.Types.Single(x=>x.Name=="EyeCapture");
  var saveCalls=Calls(capture.Methods.Single(x=>x.Name=="Save")).ToList();
  Require(saveCalls.Count(x=>x.Name=="GetRenderTextureForRenderPass")==2,"capture reads two XR render-pass textures");
  Require(!p.GetTypeReferences().Any(x=>x.FullName=="UnityEngine.ScreenCapture"),"capture has no desktop ScreenCapture fallback");

  var visual=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(!weapons.Methods.Any(x=>x.Name=="RestoreWeapon"),"old animated weapon transform restoration removed");
  Require(!Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.DeclaringType.Name=="Transform" && (x.Name.StartsWith("set_") || x.Name=="SetPositionAndRotation")),"weapon pose moves render copy without mutating original gun transforms");
  Require(!visual.Methods.SelectMany(Calls).Any(x=>x.Name=="Instantiate" || x.DeclaringType.Name=="FireComponent"),"render-only copy clones no game behaviour or fire components");
  Require(Calls(visual.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="BakeMesh"),"skinned geometry copied without duplicate game animators");
  Require(Calls(visual.Methods.Single(x=>x.Name=="RefreshAnimation")).Any(x=>x.Name=="BakeMesh"),"native skinned animation refreshed in render-only copy");
  Require(visual.Methods.Single(x=>x.Name=="RefreshAnimation").Body.ExceptionHandlers.Any(),"native animation failure has static snapshot fallback");
  Require(Calls(visual.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="RestoreAnimation"),"native culling overrides restored on weapon cleanup");
  var feedbackSites=weapons.Methods.Where(x=>Calls(x).Any(c=>c.DeclaringType.Name=="ShotFeedback" && c.Name=="Fire")).Select(x=>x.Name).ToArray();
  Require(feedbackSites.SequenceEqual(new[]{"Shot"}),"recoil/haptic feedback originates only from actual projectile launch hook");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="Shot")).Any(x=>x.Name=="ShotHaptics"),"actual shots queue controller feedback");
  Require(Calls(tracker.Methods.Single(x=>x.Name=="TickHaptics")).Any(x=>x.Name=="TriggerHapticPulse"),"queued feedback reaches native OpenVR haptic API");
  Require(!trackerCalls.Any(x=>new[]{"Sleep","Delay"}.Contains(x.Name)),"haptics never block the Unity thread");
  foreach(string hook in new[]{"Inject","HitPoint","ForwardRay","RefreshMuzzle"}) Require(weapons.Methods.Single(x=>x.Name==hook).Body.ExceptionHandlers.Any(),"weapon hook exception isolation: "+hook);
  Require(Calls(weapons.Methods.Single(x=>x.Name=="StopOwnedFire")).Any(x=>x.Name=="StopAutoFireCoroutine"),"VR-owned automatic fire has explicit release cleanup");
  var reload=weapons.Methods.Single(x=>x.Name=="TickReload");
  Require(!Calls(reload).Any(x=>x.Name=="ReloadPrimary")&&Calls(reload).Any(x=>x.Name=="TryRemoveAmmo")&&Calls(weapons.Methods.Single(x=>x.Name=="SetMagazine")).Any(x=>x.Name=="SetAmmo"),"physical reload transfers native reserve and magazine without automatic ReloadPrimary");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="BindReload")).Any(x=>x.Name=="TryGetValue"),"magazine/chamber state survives weapon selection");
  var extract=visual.Methods.Single(x=>x.Name=="PrepareReload");
  var extraction=extract.Body.Instructions.ToArray();
  var readable=Array.FindIndex(extraction,i=>i.Operand is MethodReference m&&m.Name=="get_isReadable");
  var weights=Array.FindIndex(extraction,i=>i.Operand is MethodReference m&&m.Name=="get_boneWeights");
  Require(readable>=0&&weights>readable&&!extraction.Skip(readable+1).Take(4).Any(i=>i.OpCode.FlowControl==Mono.Cecil.Cil.FlowControl.Cond_Branch),"reload extraction does not reject valid weights using isReadable flag");
  var weaponCtor=weapons.Methods.Single(x=>x.Name==".ctor").Body.Instructions.Select(x=>x.Operand).OfType<string>().ToArray();
  Require(new[]{"StartIdleTimer","CanStartIdleBreakTimer","IdleBreakerAnimationStart"}.All(weaponCtor.Contains),"local idle flourish entry points are patched");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="Shot")).Any(x=>x.DeclaringType.Name=="ManualReloadState"&&x.Name=="OnShot"),"actual-shot hook arms manual shotgun pump");
  Require(!Calls(visual.Methods.Single(x=>x.Name=="ReloadPose")).Any(x=>x.Name=="PoseAmmunition"),"camera callbacks do not overwrite constrained held-ammo pose");
  Require(Calls(reload).Any(x=>x.Name=="ReloadHaptics")&&!Calls(reload).Any(x=>x.Name=="PunchHaptics"),"reload feedback uses dedicated non-grip-dependent cues");
  Require(weaponCtor.Contains("AllowReloadSound")&&weapons.Methods.Single(x=>x.Name=="AllowReloadSound").Body.ExceptionHandlers.Any(),"native reload sound suppression is installed with failure isolation");
  var ownedMechanism=p.Types.Single(x=>x.Name=="WeaponMechanism");
  Require(Calls(ownedMechanism.Methods.Single(x=>x.Name=="BakeOwned")).Any(x=>x.Name=="BakePose")&&!ownedMechanism.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Transform"&&x.Name.StartsWith("set_")),"mechanical closed pose writes only private skin snapshots");
  var locomotion=p.Types.Single(x=>x.Name=="LocomotionDriver");
  Require(!locomotion.Methods.SelectMany(Calls).Any(x=>new[]{"StartXR","StopXR","Init","Shutdown","WaitGetPoses","Submit","SetPositionAndRotation"}.Contains(x.Name)),"locomotion uses native input instead of teleporting transforms or owning XR");
  foreach(string hook in new[]{"Move","Look","Inject"}) Require(locomotion.Methods.Single(x=>x.Name==hook).Body.ExceptionHandlers.Any(),"locomotion hook exception isolation: "+hook);
  var stop=Calls(t.Methods.Single(x=>x.Name=="StopXR")).ToList();
  Require(stop.FindIndex(x=>x.DeclaringType.Name=="LocomotionDriver" && x.Name=="Dispose") < stop.FindIndex(x=>x.DeclaringType.Name=="CameraRig" && x.Name=="Dispose"),"explicit stop disposes locomotion before head tracking");
  Require(stop.FindIndex(x=>x.DeclaringType.Name=="WeaponHands" && x.Name=="Dispose") < stop.FindIndex(x=>x.DeclaringType.Name=="LocomotionDriver" && x.Name=="Dispose"),"explicit stop disposes weapons before movement and tracking");
  Require(Calls(locomotion.Methods.Single(x=>x.Name=="Move")).Any(x=>x.DeclaringType.Name=="HeadingMath" && x.Name=="InBodyFrame"),"head-relative conversion present in shipped movement hook");
  using var game=ModuleDefinition.ReadModule(System.IO.Path.Combine(args.Length>1?args[1]:"analysis_xiii/refs","interop","_XIII.dll"));
  var provider=game.Types.Single(x=>x.Name=="CharacterControllerInputProvider");
  foreach(string name in new[]{"CalculatePlayerMovement","GetLookInput"}) Require(provider.Methods.Count(x=>x.Name==name && x.Parameters.Count==0 && x.ReturnType.FullName=="UnityEngine.Vector2")==1,"game movement hook signature exists: "+name);
  var input=game.Types.Single(x=>x.Name=="GameInputManager");
  foreach(string name in new[]{"GetButton_Internal","GetButtonDown_Internal","GetButtonUp_Internal"}) Require(input.Methods.Count(x=>x.Name==name && x.IsStatic && x.ReturnType.FullName=="System.Boolean" && string.Join(",",x.Parameters.Select(z=>z.Name))=="button,playerID")==1,"game button hook signature exists: "+name);
  var interaction=p.Types.Single(x=>x.Name=="InteractionDriver");
  Require(stop.FindIndex(x=>x.DeclaringType.Name=="InteractionDriver" && x.Name=="Dispose") < stop.FindIndex(x=>x.DeclaringType.Name=="WeaponHands" && x.Name=="Dispose"),"interaction is disposed before weapon and camera resources");
  foreach(string hook in new[]{"BeginRay","EndRay","FinalizeRay","Inject","HideCrosshairs"}) Require(interaction.Methods.Single(x=>x.Name==hook).Body.ExceptionHandlers.Any(),"interaction hook exception isolation: "+hook);
  Require(Calls(interaction.Methods.Single(x=>x.Name=="FinalizeRay")).Any(x=>x.Name=="Restore"),"ray parameters restored from exception finalizer");
  Require(!interaction.Methods.SelectMany(Calls).Any(x=>new[]{"PickupItem","TryPickupItem","AddItem","TriggerAction"}.Contains(x.Name)),"interaction uses native ray/button pipeline without forcing inventory or quest actions");
  var keyGate=p.Types.Single(x=>x.Name=="KeyUnlockGesture");
  Require(Calls(keyGate.Methods.Single(x=>x.Name=="Intercept")).Any(x=>x.Name=="HasItemToResolveConditional"),"gesture requires owned native key/card");
  Require(Calls(keyGate.Methods.Single(x=>x.Name=="Complete")).Any(x=>x.Name=="ResolveConditional")&&Calls(keyGate.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="Complete")&&!keyGate.Methods.SelectMany(Calls).Any(x=>x.Name=="set_conditionalResolved"||x.Name=="TriggerInteractions"),"gesture resumes native conditional without forcing unlocked state");
  Require(Calls(interaction.Methods.Single(x=>x.Name=="TryUseTool")).Any(x=>x.Name=="IsRaycastHittablePingValid")&&Calls(interaction.Methods.Single(x=>x.Name=="TryUseTool")).Any(x=>x.Name=="IsInteractionBlocked"),"wheel tool respects native target permission");
  var touch=p.Types.Single(x=>x.Name=="TouchButtons");
  Require(Calls(touch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="PingRaycastHittable")&&!touch.Methods.SelectMany(Calls).Any(x=>x.Name=="TriggerInteractions"),"touch buttons use native ping validation");
  Require(Calls(touch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TryButtonContacts")&&!touch.Methods.SelectMany(Calls).Any(x=>x.Name=="TryMeleeTip"),"button contact uses stopped rendered weapon instead of unconstrained melee tip");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="TryButtonContacts")).Any(x=>x.Name=="get_FittedToWorld"),"armed button palm uses the rendered grip frame");
  var ctor=interaction.Methods.Single(x=>x.Name==".ctor");
  var strings=ctor.Body.Instructions.Select(x=>x.Operand).OfType<string>().ToArray();
  Require(strings.Contains("PingRaycastHittable")&&!strings.Contains("ResolveConditional"),"key intercepted before coroutine; completion method not hooked");
  Require(!Calls(keyGate.Methods.Single(x=>x.Name=="Intercept")).Any(x=>new[]{"ResolveConditional","TrySelectSlot","Begin","Remove"}.Contains(x.Name)),"drawing key starts no native use, selection or consumption");
  var keyPreview=p.Types.Single(x=>x.Name=="KeyPreviewFactory");
  Require(Calls(keyGate.Methods.Single(x=>x.Name=="Intercept")).Any(x=>x.DeclaringType.Name=="KeyPreviewFactory"&&x.Name=="Create"),"key gesture uses model recovery for migrated inventory");
  Require(!keyPreview.Methods.SelectMany(Calls).Any(x=>new[]{"TrySelectSlot","AddToActive","CreateInstance","Instantiate","Remove","ResolveConditional"}.Contains(x.Name)),"key model recovery cannot equip, grant, consume or unlock");
  Require(keyPreview.Methods.SelectMany(Calls).Any(x=>x.Name=="get_CurrentFpsRigReference")&&keyPreview.Methods.SelectMany(Calls).Any(x=>x.Name=="get_pickupPrefabs"),"meshless car key recovers detached native rig/pickup model");
  Require(Calls(keyPreview.Methods.Single(x=>x.Name=="TryRoot")).Any(x=>x.Name=="CreateFromRenderers"),"detached recovery creates render-only visual");
  Require(!keyPreview.Methods.SelectMany(Calls).Any(x=>x.Name.StartsWith("FindObjects",StringComparison.Ordinal)),"key recovery does not search the whole scene");
  Require(interaction.Methods.Any(x=>x.Name=="DeferDoorOpen")&&Calls(interaction.Methods.Single(x=>x.Name=="DeferDoorOpen")).Any(x=>x.Name=="get_Pointer"),"unlock event suppression scoped to exact action");
  Require(Calls(doors.Methods.Single(x=>x.Name=="TryToggle")).Any(x=>x.Name=="PingRaycastHittable"),"fast door open/close contact uses native ping");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="PropRimContact"),"ashtray uses actual hand pose contact");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="get_PropGripThickness"),"pinch receives the measured rim thickness");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="NativeGrip")).Any(x=>x.Name=="get_PropRimHandRotation"),"pinch orientation and translation both reach the rendered attachment");
  var trayVisual=p.Types.Single(x=>x.Name=="WeaponVisual");
  Require(Calls(trayVisual.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="MeasureAshtrayRim"),"render copy measures the actual ashtray rim");
  var fingersMath=p.Types.Single(x=>x.Name=="FingerPoseMath");
  Require(Calls(fingersMath.Methods.Single(x=>x.Name=="ProceduralPose")).Any(x=>x.Name=="RimPose")&&Calls(fingersMath.Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="ProceduralPose"),"rendered ashtray fingers use the solved pinch");
  Require(p.Types.Single(x=>x.Name=="NativeHandVisual").Methods.SelectMany(Calls).Any(x=>x.Name=="SetRimPadCloud"),"pinch samples the active outfit skin");
  Require(strings.Contains("DoUpdate") && strings.Contains("ExecuteRaycast"),"both inlined native ray entry points are patched");
  var ray=game.Types.Single(x=>x.Name=="RaycastSystem");
  foreach(string method in new[]{"DoUpdate","ExecuteRaycast"}) Require(ray.Methods.Count(x=>x.Name==method && x.Parameters.Count==0 && x.ReturnType.FullName=="System.Void")==1,"native ray hook signature: "+method);
  foreach(string prop in new[]{"rayOriginTransform","rayCastForwardOverride","overrideRaycastForward"}) Require(ray.Properties.Any(x=>x.Name==prop && x.GetMethod!=null && x.SetMethod!=null),"native ray state supports scoped override: "+prop);
  Require(Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.DeclaringType.Name=="PistolSupport"),"pistol stabilization included in shipped weapon hook");
  var shot=t.Methods.Single(x=>x.Name=="CaptureBothEyes"); Require(shot.Body.ExceptionHandlers.Any(),"screenshot failures are isolated");
  var ui=p.Types.Single(x=>x.Name=="GameUiControls");
  var nativeWheel=game.Types.Single(x=>x.Name=="InventoryWheel");
  foreach(string method in new[]{"EvaluateOpenCloseInput","EvaluateDefaultWheelInput","GetChoosingDirection","HoverOption"})
      Require(nativeWheel.Methods.Count(x=>x.Name==method)==1,"unambiguous native wheel entry: "+method);
  Require(Calls(ui.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="HoverOption") && !ui.Methods.SelectMany(Calls).Any(x=>x.Name=="ChooseSliceBasedOnAngle"),"wheel hover does not directly equip; native CloseWheel owns confirmation");
  Require(Calls(ui.Methods.Single(x=>x.Name=="Close")).Any(x=>x.Name=="CloseWheel"),"VR wheel confirmation uses native close/equip path");
  var pointer=p.Types.Single(x=>x.Name=="VrMenuPointer");
  Require(pointer.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="ExecuteEvents"&&x.Name=="ExecuteHierarchy"),"menu pointer dispatches native UI hierarchy events");
  Require(!ui.Methods.SelectMany(Calls).Any(x=>x.Name=="TrySelectSlot"||x.Name=="TryUsingConsumable"),"removed inventory shortcuts cannot force equipment state");
  Require(!p.GetTypeReferences().Any(t=>t.FullName=="UnityEngine.LineRenderer"),"pointer never loads stripped LineRenderer");
  var quality=p.Types.Single(t=>t.Name=="QualityMenu");
  Require(Calls(quality.Methods.Single(m=>m.Name=="UpdateDisplay")).Any(m=>m.Name=="Close"),"successful render-scale application releases quality menu lock");
  var logo=p.Types.Single(t=>t.Name=="StartupLogos");
  Require(logo.Methods.SelectMany(Calls).Any(m=>m.Name=="GoToMainMenu"),"logo skip uses native transition");
  Require(!logo.Methods.SelectMany(Calls).Any(m=>m.DeclaringType.Name=="CutscenePlayer"),"logo skip preserves story movies");
  var items=p.Types.Single(x=>x.Name=="WheelItems");
  Require(!Calls(items.Methods.Single(x=>x.Name=="Commit")).Any(x=>x.Name=="TrySelectSlot"||x.Name=="TryUsingConsumable")&&Calls(items.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="TryUsingConsumable"),"medkit preview is separate from deliberate native use");
  Require(!Calls(t.Methods.Single(x=>x.Name=="Update")).Any(x=>x.DeclaringType.Name=="LocomotionDriver"&&x.Name=="Toggle"),"F4 can no longer silently disable movement");
  var held=p.Types.Single(x=>x.Name=="HeldItemVisual");
  Require(Calls(held.Methods.Single(x=>x.Name=="Build")).Any(x=>x.DeclaringType.Name=="HeldItemMeshSources"&&x.Name=="Find"),"held item visual uses explicit native mesh references and usable LODs");
  Require(!held.Methods.SelectMany(Calls).Any(x=>x.Name=="TrySelectSlot"||x.Name=="TryUsingConsumable"||x.Name=="Heal"||x.Name=="SetInputLock"||x.Name=="SetAxisLock"),"held item renderer cannot equip, heal or lock gameplay");
  Require(Calls(items.Methods.Single(x=>x.Name=="Commit")).Any(x=>x.DeclaringType.Name=="HeldItemVisual"&&x.Name=="Create"),"medkit selection creates original item mesh preview");
  Require(held.Methods.SelectMany(Calls).Any(x=>x.Name=="get_sharedMaterials"),"medkit visual uses original game materials");
  var menuKeyboard=p.Types.Single(x=>x.Name=="MenuKeyboard");
  Require(pointer.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="MenuKeyboard"&&x.Name=="Tick")&&pointer.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="MenuPrompts"&&x.Name=="Hit"),"menu pointer connects startup keys and footer hit handling");
  Require(Calls(menuKeyboard.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.Name=="Release"),"native key is released on VR shutdown");
  Require(Calls(rig.Methods.Single(x=>x.Name=="EnsureMenuCamera")).Any(x=>x.Name=="DontDestroyOnLoad"),"frontend XR camera survives scene transitions");
  var esc=p.Types.Single(x=>x.Name=="EscapeKey");
  Require(esc.NestedTypes.Single(x=>x.Name=="InputEvent").ClassSize==40,"Escape INPUT layout is 40 bytes for Windows x64");
  Require(Calls(esc.Methods.Single(x=>x.Name=="SendKey")).Any(x=>x.Name=="OwnForeground") && Calls(esc.Methods.Single(x=>x.Name=="OwnForeground")).Any(x=>x.Name=="GetForegroundWindow") && !ui.Methods.SelectMany(Calls).Any(x=>x.Name=="Sleep"),"Escape dispatch is foreground guarded and nonblocking");
  Require(Calls(rig.Methods.Single(x=>x.Name=="Fail")).Any(x=>x.Name=="CancelTransient"),"tracking failure releases owned menu/input state");
  Require(Calls(rig.Methods.Single(x=>x.Name=="SampleCameraMode")).Any(x=>x.Name=="get_isWatchingCutsceneOrInFlashBack") && Calls(rig.Methods.Single(x=>x.Name=="Prepare")).Any(x=>x.Name=="ReadCinematicPose")
    && Calls(rig.Methods.Single(x=>x.Name=="ReadFilmPose")).Any(x=>x.Name=="GetBasePose"),"cinematic mode reads the authored output transform including animation after Cinemachine");
  var movie=p.Types.Single(x=>x.Name=="StoryVideo");
  Require(!movie.Methods.SelectMany(Calls).Any(x=>new[]{"Play","Stop","Pause","Prepare","set_time","set_frame"}.Contains(x.Name)),"story video adapter never advances/skips/seeks native playback");
  Require(Calls(movie.Methods.Single(x=>x.Name=="ReleasePlayer")).Any(x=>x.Name=="set_renderMode"),"story video restores borrowed render mode");
  Require(!rig.Methods.SelectMany(Calls).Concat(weapons.Methods.SelectMany(Calls)).Any(x=>x.Name.StartsWith("SetInputLock")||x.Name.StartsWith("SetAxisLock")),"scene recovery never forces native story locks off");
  var weaponTickCalls=Calls(weapons.Methods.Single(x=>x.Name=="Tick")).Select(x=>x.Name).ToList();
  Require(weaponTickCalls.IndexOf("FindPlayer")<weaponTickCalls.IndexOf("TickReloadProps"),"weapon scene binding is validated before old-scene reload effects");
  Require(Calls(interaction.Methods.Single(x=>x.Name=="BeginRay")).Any(x=>x.DeclaringType.FullName=="UnityEngine.Object" && x.Name=="op_Equality"),"interaction ray creation checks Unity destroyed-object equality");
  var wrist=p.Types.Single(x=>x.Name=="WristHud");
  Require(!p.Types.Single(x=>x.Name=="WorldCanvas").Methods.SelectMany(Calls).Any(x=>x.Name=="SetParent"),"native HUD canvas pinning preserves game hierarchy");
  Require(Calls(driver.Methods.Single(x=>x.Name=="ApplyEyes")).Any(x=>x.DeclaringType.Name=="WristHud" && x.Name=="Render"),"HUD pinning updates after render camera pose");
  Require(visual.Methods.SelectMany(Calls).Any(x=>x.Name=="get_weaponAnimator"),"weapon animation uses the weapon's explicit animator reference");
  var mechanism=p.Types.Single(x=>x.Name=="WeaponMechanism");
  Require(!mechanism.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Transform" && x.Name.StartsWith("set_")),"mechanism fallback never writes source skeleton transforms");
  Require(!mechanism.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Animator" || x.DeclaringType.Name=="FireComponent"),"mechanism fallback cannot invoke animator events or duplicate native shots");
  var fx=p.Types.Single(x=>x.Name=="MuzzleEffects");
  var nativeFx=game.Types.Single(x=>x.Name=="VFXComponent");
  Require(nativeFx.Methods.Count(x=>x.Name=="ShowMuzzleFlash" && x.Parameters.Count==1)==1,"native ShowMuzzleFlash hook signature exists");
  Require(nativeFx.Methods.Count(x=>x.Name=="SpawnParticle" && string.Join(",",x.Parameters.Select(z=>z.Name))=="particles,spawnTransform,camLayer,attachParticleToSpawnTransform" && x.ReturnType.Name=="ParticleSystem")==1,"native SpawnParticle hook signature and argument names match");
  Require(Calls(fx.Methods.Single(x=>x.Name=="FinalizeFlash")).Any(x=>x.Name=="Complete"),"muzzle effect scope is finalized on exception");
  Require(!fx.Methods.SelectMany(Calls).Any(x=>x.Name=="Destroy" || x.Name=="Instantiate" || x.DeclaringType.Name=="FireComponent"),"muzzle router neither destroys pooled particles nor generates shots/clones");
  var unbindCalls=Calls(weapons.Methods.Single(x=>x.Name=="Unbind")).ToList();
  Require(unbindCalls.FindIndex(x=>x.DeclaringType.Name=="MuzzleEffects" && x.Name=="Cancel")<unbindCalls.FindIndex(x=>x.DeclaringType.Name=="WeaponVisual" && x.Name=="Suspend"),"pooled muzzle particles detach before cached weapon suspension");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="TryGetEffectMuzzle")).Any(x=>x.Name=="get_MuzzleAnchor"),"native flash routes through the tracked render weapon muzzle");
  Require(Calls(tracker.Methods.Single(x=>x.Name=="ReadButtons")).Any(x=>x.Name=="AxisClick"),"stick clicks use the detected OpenVR axis button");
  var sprintAction=game.Types.Single(x=>x.Name=="InputActions").Fields.Single(x=>x.Name=="Movement_Sprint");
  Require((int)sprintAction.Constant==3,"native sprint action is present in supplied game metadata");
  Require(locomotion.Fields.Any(x=>x.Name=="sprintAction") && Calls(locomotion.Methods.Single(x=>x.Name=="FindPlayer")).Any(x=>x.Name=="GetToggleSprint"),"sprint input registered; native sprint toggle setting readable");
  Require(Calls(wrist.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_currentCounter") && Calls(wrist.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="get_magazineAmmo"),"right watch reads current native ammunition counter");
  Require(Calls(wrist.Methods.Single(x=>x.Name=="Discover")).Any(x=>x.Name=="get_notificationInstance"),"dossier suppression targets individual native popup cards");
  Require(!wrist.Methods.SelectMany(Calls).Any(x=>new[]{"ShowCollectable","AddAmmo","SetAmmo","ReceiveText","StopAllCoroutines"}.Contains(x.Name)),"watch renderer does not mutate ammo, objectives or collectable events");
  Require(!wrist.Methods.Any(x=>x.Name=="PosePanel"),"old independently posed wrist Canvas removed");
  Require(Calls(ui.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.DeclaringType.Name=="ObjectivesState" && x.Name=="Sample"),"left X objective state connected to live menu driver");
  var glove=p.Types.Single(x=>x.Name=="GloveVisual");
  Require(!glove.Methods.SelectMany(Calls).Any(x=>x.Name=="Instantiate" || x.DeclaringType.Name=="FireComponent" || x.DeclaringType.Name=="Animator"),"tracked hand roots duplicate no native animators or gameplay components");
  var nativeHand=p.Types.Single(x=>x.Name=="NativeHandVisual");
  Require(Calls(nativeHand.Methods.Single(x=>x.Name=="Refresh")).Any(x=>x.Name=="BakePose" && x.DeclaringType.Name=="NativeSkinSnapshot"),"native hands retain original skinned deformation in a normalized snapshot");
  Require(Calls(nativeHand.Methods.Single(x=>x.Name=="Build")).Any(x=>x.Name=="get_sharedMaterials"),"native hands borrow actual game materials");
  Require(nativeHand.Methods.Where(m=>Calls(m).Any(x=>x.DeclaringType.Name=="Transform"&&x.Name.StartsWith("set_"))).All(m=>m.Name=="Refresh"),"native hand transform writes limited to owned render mesh in Refresh");
  var snapshot=p.Types.Single(x=>x.Name=="NativeSkinSnapshot");
  Require(!snapshot.Methods.SelectMany(Calls).Any(x=>x.Name=="Instantiate" || x.DeclaringType.Name=="Animator"),"snapshot creates no native gameplay behaviours or animators");
  Require(snapshot.Methods.Where(m=>Calls(m).Any(x=>x.DeclaringType.Name=="Transform"&&x.Name.StartsWith("set_"))).Select(m=>m.Name).SequenceEqual(new[]{"SetOwned"}),"all snapshot transform writes isolated to private bones");
  var ownCall=snapshot.Methods.Single(x=>x.Name=="Bake").Body.Instructions;
  Require(ownCall.Any(i=>i.Operand is FieldReference f && f.Name=="owned") && !ownCall.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f && (f.Name=="Bones"||f.Name=="source")),"snapshot bake positions owned bones, never source bones");
  Require(Calls(nativeHand.Methods.Single(x=>x.Name=="Build")).Any(x=>x.DeclaringType.Name=="NativeHandMath"&&x.Name=="Frame"),"crop uses tested neutral frame independent of renderer scale");
  Require(Calls(nativeHand.Methods.Single(x=>x.Name=="TryWeaponGrip")).Any(x=>x.Name=="get_GameWorldToFitted"),"hands and weapons share fitted coordinate system");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="TryPoseHand")).Any(x=>x.Name=="get_SupportHeld"),"left hand attachment gated by live support grip state");
  // 0.1.225: the primary grip through PrimaryHandPoint (the bazooka's left hand on its handle).
  Require(Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).Count(x=>x.Name=="NativeGrip")+Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).Count(x=>x.Name=="PrimaryHandPoint")==2&&Calls(weapons.Methods.Single(x=>x.Name=="PrimaryHandPoint")).Any(x=>x.Name=="NativeGrip"),"both primary and support grips drive tracked weapon pose");
  Require(!nativeHand.Methods.SelectMany(Calls).Any(x=>x.Name=="Instantiate"||x.DeclaringType.Name=="Animator"&&x.Name!="IsInTransition"&&!x.Name.StartsWith("get_")),"native hand adapter only reads animator state; never clones or drives gameplay animation");
  var punch=p.Types.Single(x=>x.Name=="PunchDriver");
  Require(Calls(punch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.DeclaringType.Name=="PropImpactAudio"&&x.Name=="Surface"),"physical contacts use scoped prop NPC impact sound routing");
  var soundGate=weapons.Methods.Single(x=>x.Name=="SurfaceSound");
  Require(soundGate.ReturnType.FullName=="System.Boolean"&&Calls(soundGate).Any(x=>x.DeclaringType.Name=="PropImpactAudio"&&x.Name=="AllowNativeSound"),"native impact prefix suppresses only the explicitly replaced contact sound");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="Dispose")).Any(x=>x.DeclaringType.Name=="PunchDriver"&&x.Name=="Dispose"),"weapon shutdown releases cached impact audio");
  var propAudio=p.Types.Single(x=>x.Name=="PropImpactAudio");
  Require(propAudio.Methods.Single(x=>x.Name=="Surface").Body.ExceptionHandlers.Any(x=>x.HandlerType==Mono.Cecil.Cil.ExceptionHandlerType.Finally),"native sound scope is restored on VFX exceptions");
  Require(Calls(propAudio.Methods.Single(x=>x.Name=="Surface")).Any(x=>x.Name=="get_overrideFmodEvent")&&Calls(propAudio.Methods.Single(x=>x.Name=="Surface")).Any(x=>x.Name=="ObtainSFXSurfaceDetails"),"NPC prop sound honors native item override and surface parameters");
  var chairAudio=p.Types.Single(x=>x.Name=="ChairImpactClip");
  Require(!chairAudio.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name is "AudioSource" or "AudioClip"),"chair recording uses game FMOD output, not inactive Unity audio");
  Require(Calls(chairAudio.Methods.Single(x=>x.Name=="Play"&&x.Parameters.Count==4)).Any(x=>x.Name=="set3DAttributes")&&Calls(chairAudio.Methods.Single(x=>x.Name=="Play"&&x.Parameters.Count==4)).Any(x=>x.Name=="getChannelGroup"),"chair impact has a contact position and routes through game master bus");
  Require(Calls(punch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="ApplyDamageTo"&&x.DeclaringType.Name=="MeleeComponent"),"physical contact uses native melee damage pipeline");
  Require(!punch.Methods.Where(x=>x.Name!="BackKnockout").SelectMany(Calls).Any(x=>new[]{"ReceiveDamage","set_Health","Die","Knockout","DoMeleeAttack","TryHit"}.Contains(x.Name))&&!Calls(punch.Methods.Single(x=>x.Name=="BackKnockout")).Any(x=>new[]{"ReceiveDamage","set_Health","Die","DoMeleeAttack","TryHit"}.Contains(x.Name)),"punch driver neither writes health nor performs remote/native duplicate area attacks (only the knockout from behind uses the game's Knockout)");
  Require(Calls(punch.Methods.Single(x=>x.Name=="Tick")).Any(x=>x.Name=="PhysicalHand"),"punch speed derives from physical tracking, not world locomotion");
  Require(!Calls(weapons.Methods.Single(x=>x.Name=="UpdateSocket")).Any(x=>x.Name=="CreatePrimitive"),"support grip marker is invisible");
  Require(Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).Any(x=>x.DeclaringType.Name=="WeaponInertia"&&x.Name=="Step"),"inertia feeds the actual render and firing pose");
  var rigid=p.Types.Single(x=>x.Name=="RigidMeshVisual");
  Require(!rigid.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Canvas"||x.DeclaringType.Name=="RectTransform"),"watch geometry has no UI batching path");
  Require(Calls(rigid.Methods.Single(x=>x.Name==".ctor")).Any(x=>x.Name=="SetParent"),"watch and digits are children of the same tracked hand");
  Require(Calls(glove.Methods.Single(x=>x.Name=="Pose")).Count(x=>x.Name=="SetPositionAndRotation")==1,"one hand pose owns case and digit placement");
  Require(Calls(rig.Methods.Single(x=>x.Name=="UpdateMarkers")).Any(x=>x.Name=="get_RightHandVisible"),"controller markers hidden when glove is visible");
  var contact=p.Types.Single(x=>x.Name=="ContactWorld");
  Require(!contact.Methods.SelectMany(Calls).Any(x=>x.DeclaringType.Name=="Rigidbody"||x.Name=="IgnoreCollision"),"contact adapter never pushes player or changes native collision pairs");
  var contactCtor=contact.Methods.Single(x=>x.IsConstructor);
  var enable=contactCtor.Body.Instructions.First(x=>x.Operand is MethodReference r&&r.Name=="set_enabled");
  Require(enable.Previous.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4_0,"owned query collider is disabled immediately");
  var gunPose=Calls(weapons.Methods.Single(x=>x.Name=="RenderPose")).ToList();
  int contactIndex=gunPose.FindIndex(x=>x.Name=="ResolveGun");
  Require(contactIndex>=0&&contactIndex<gunPose.FindIndex(x=>x.DeclaringType.Name=="WeaponVisual"&&x.Name=="Pose")&&contactIndex<gunPose.FindIndex(x=>x.Name=="WriteMuzzle"),"collision correction precedes visual placement and firing muzzle");
  Require(Calls(glove.Methods.Single(x=>x.Name=="Pose")).Any(x=>x.Name=="ResolveHand"),"tracked native hand uses collision-constrained pose");
  Require(Calls(nativeHand.Methods.Single(x=>x.Name=="Build")).Any(x=>x.DeclaringType.Name=="WristFitMath"&&x.Name=="Fit"),"wristband derives fit from actual native skin");
  Require(Calls(ui.Methods.Single(x=>x.Name=="get_BlocksGameplay")).Any(x=>x.DeclaringType.Name=="QualityMenu"&&x.Name=="get_Open"),"quality menu blocks VR gameplay commands");

 }
}
