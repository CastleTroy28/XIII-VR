using System;using System.Collections.Generic;using System.Linq;using XiiiXR;using UnityEngine;
class PhysicalWorldTests
{
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static void Main()
 {
  var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
  var col=new Collider();col.gameObject.name="bottle";var body=new Rigidbody();body.gameObject=col.gameObject;col.attachedRigidbody=body;
  Physics.Overlap=new[]{col};rig.R=new PoseValue(new Vector3(1,0,0));body.position=new Vector3(1,0,0);col.Center=body.position;
  rig.RightControls=new(true,HandControls.Grip,HandControls.Grip,0);loose.Tick(rig,true,doors);
  Check(loose.Holding(true)&&!body.useGravity&&body.isKinematic&&body.collisionDetectionMode==CollisionDetectionMode.ContinuousSpeculative,"near grip fails to own dynamic body with collisions");
  rig.R=new PoseValue(new Vector3(1.04f,0,0));rig.RightControls=new(true,HandControls.Grip,0,0);loose.Tick(rig,true,doors);
  Check(body.position.x>1&&body.velocity.magnitude<=6.01f,"held object doesn't physically follow bounded hand velocity");
  Physics.SweepDistance=.005f;float blockedAt=body.position.x;
  rig.R=new PoseValue(new Vector3(1.14f,0,0));loose.Tick(rig,true,doors);
  Check(body.position.x<=blockedAt+.0031f,"held prop crosses blocking surface");Physics.SweepDistance=-1;
  rig.RightControls=new(true,0,0,HandControls.Grip);loose.Tick(rig,true,doors);
  Check(!loose.Holding(true)&&body.useGravity&&body.velocity.x>0&&body.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"release loses throw/gravity/continuous collision");
  rig.RightControls=new(true,HandControls.Grip,HandControls.Grip,0);loose.Tick(rig,true,doors);loose.Tick(rig,false,doors);
  Check(!loose.Holding(true)&&body.useGravity&&body.velocity.sqrMagnitude==0,"lost tracking/menu throws or retains body");
  body.isKinematic=true;loose.Tick(rig,true,doors);Check(!loose.Holding(true),"kinematic scenery grabbed");body.isKinematic=false;
  col.gameObject.Extra=new PickableItem();loose.Tick(rig,true,doors);Check(!loose.Holding(true),"native mission pickup stolen");col.gameObject.Extra=null;
  WeaponHands.Current=new WeaponHands{RightFree=false};loose.Tick(rig,true,doors);Check(!loose.Holding(true),"armed hand grabs loose prop");WeaponHands.Current=null;
  GripCarry.Current=new GripCarry();rig.L=new PoseValue(col.Center);rig.R=new PoseValue(new Vector3(2,0,0));
  rig.LeftControls=new(true,HandControls.Grip,HandControls.Grip,0);rig.RightControls=new(true,0,0,0);loose.Tick(rig,true,doors);
  Check(!loose.Holding(false),"occupied left hostage hand grabs a second object");
  rig.R=new PoseValue(col.Center);rig.RightControls=new(true,HandControls.Grip,HandControls.Grip,0);loose.Tick(rig,true,doors);
  Check(loose.Holding(true),"left hostage carry blocks free right-hand prop grip");
  loose.Tick(rig,false,doors);GripCarry.Current=null;rig.LeftControls=new(true,0,0,0);
  var nativeProps=new WorldPropBodies();body.name="jar_02";body.isKinematic=true;body.drag=100;body.mass=100;
  Check(nativeProps.Resolve(col)==body&&!body.isKinematic&&body.drag<1&&body.mass<=8,"named native prop remains frozen/heavy");
  nativeProps.Dispose();Check(body.isKinematic&&body.drag==100&&body.mass==100,"native prop state not restored on shutdown");body.isKinematic=false;
  loose.Dispose();Physics.Overlap=Array.Empty<Collider>();
  var action=new RaycastAction{Blocked=true};var tool=new CustomAnimationTool();var pivot=new Transform();
  var dc=new Collider{Center=new Vector3(1,0,0)};
  tool.customAnimationState=new CustomAnimationState{objTransform=pivot,positiveState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=90},negativeState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=-90}};
  tool.meshColliders.Add(dc);var door=new PlayMagic.AI.Door{doorReferences=new[]{tool},doorRaycastTargets=new[]{action}};Resources.Items=new ObjectBase[]{door};dc.gameObject.Extra=door;Physics.Overlap=new[]{dc};
  rig.RightControls=new(true,HandControls.Grip,HandControls.A,0);rig.R=new PoseValue(new Vector3(1,0,0));doors.Tick(rig,true,actor,loose);
  Check(!doors.Holding(true),"locked door grabbed");action.Blocked=false;doors.Tick(rig,true,actor,loose);Check(doors.Holding(true),"unlocked nearby hinge not grabbed");
  rig.RightControls=new(true,HandControls.Grip,0,0);rig.R=new PoseValue(new Vector3(1,0,-.08f));doors.Tick(rig,true,actor,loose);
  Check(pivot.localEulerAngles.y>0&&door.Updates>0,"grip movement doesn't open door/update native state");
  float opened=pivot.localEulerAngles.y;rig.R=new PoseValue(new Vector3(1,0,-.02f));doors.Tick(rig,true,actor,loose);
  Check(pivot.localEulerAngles.y<opened,"pull back fails to close same hinge");
  rig.RightControls=new(true,0,0,HandControls.Grip);doors.Tick(rig,true,actor,loose);Check(!doors.Holding(true)&&door.Commits>0,"release doesn't commit native door state");
  rig.RightControls=new(true,0,0,0);rig.R=new PoseValue(new Vector3(1,0,.12f));doors.Tick(rig,true,actor,loose);
  rig.R=new PoseValue(new Vector3(1,0,.04f));doors.Tick(rig,true,actor,loose);float pushed=pivot.localEulerAngles.y;
  rig.R=new PoseValue(new Vector3(1,0,.09f));doors.Tick(rig,true,actor,loose);Check(Math.Abs(pivot.localEulerAngles.y-pushed)<.001f,"door follows withdrawing hand without grip");
  doors.Cancel();tool.customAnimationState.currentStateValue=0;rig.RightControls=new(true,0,0,0);rig.R=new PoseValue(new Vector3(1,0,.09f));doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;rig.R=new PoseValue(new Vector3(1,0,.04f));doors.Tick(rig,true,actor,loose);Check(action.Pings==1,"fast empty hand contact does not call native door action");
  Time.realtimeSinceStartup+=.02f;rig.R=new PoseValue(new Vector3(1,0,.02f));doors.Tick(rig,true,actor,loose);Check(action.Pings==1,"contact repeats native toggle while still touching");
  doors.Dispose();
  var cabinet=new PhysicalDoors();tool.name="animation_02";dc.transform.name="glass_mesh";dc.transform.Components=new Component[]{tool};Resources.Items=new ObjectBase[]{tool};dc.gameObject.Extra=tool;Physics.Overlap=new[]{dc};
  rig.R=new PoseValue(new Vector3(1,0,0));rig.RightControls=new(true,HandControls.Grip,HandControls.A,0);
  cabinet.Tick(rig,true,actor,loose);Check(cabinet.Holding(true),"cabinet without AI Door cannot be grabbed");
  float cabinetStart=pivot.localEulerAngles.y;rig.R=new PoseValue(new Vector3(1,0,-.09f));rig.RightControls=new(true,HandControls.Grip,0,0);
  cabinet.Tick(rig,true,actor,loose);Check(pivot.localEulerAngles.y>cabinetStart,"cabinet grip does not rotate authored leaf");cabinet.Dispose();
  TestDeepCabinet();TestQueueCabinet();
  foreach(float state in new[]{0f,.5f,1f,-1f})foreach(bool right in new[]{false,true})TestStrike(state,right,false,false,false,false);
  TestStrike(1,true,true,false,false,false);TestStrike(1,true,false,true,false,false);TestStrike(1,true,false,false,true,false);
  TestStrike(1,true,false,false,false,true);
  // 0.1.246: a door whose own list of interactions is empty (the game had not set it up): its interaction found by what it animates.
  foreach(bool animates in new[]{true,false})TestUnlisted(animates);
  // 0.1.247: a door listing only one interaction ("- all": an AI's, switched off or blocked), the player's own beside it.
  foreach(var (listedKind,playerKind,found,locked) in new[]{("ai","animates",true,false),("off","animates",true,false),("blocked","animates",true,false),("ai","targeted",true,false),("ai","none",false,false),("blocked","animates",true,true)})TestPlayerOnly(listedKind,playerKind,found,locked);
  Console.WriteLine("PASS: 0.1.247 a door listing only another interaction (an AI's, switched off, or blocked): the player's own (it moves the leaf, or the game targets it) is used, the hand opens it as Grip+A does; a lock not yet opened keeps it shut; with no interaction for the player it stays shut.");
  Console.WriteLine("PASS: 0.1.246 a door the game had not set up (its list of interactions and its door info empty): the interaction that moves its leaf is found, a strike opens it, its missing door info neither stops nor breaks it; with no such interaction it stays shut.");
  // 0.1.156: the weapon in the left hand strikes a door too.
  TestStrike(1,false,true,false,false,false);
  // 0.1.203: story doors (their own interaction runs authored events) open from closed by that interaction.
  foreach(bool grip in new[]{false,true})foreach(bool story in new[]{false,true})TestStoryDoor(grip,story,false);
  TestStoryDoor(true,true,true);TestStoryDoor(false,true,false,true);
  Console.WriteLine("PASS: story doors (the bank's hostage door): from closed a grip pull or a slow push first has to travel 3 degrees, then the door's own native interaction opens it once (its events run), the hand lets go of the native swing; an ordinary door and an open story door still follow the hand; a story door its interaction does not open goes back to the hand.");
  Console.WriteLine("PASS: production loose-prop grip/follow/throw/tracking-loss, collision restoration, armed/kinematic/mission exclusions; native-door lock, grab, open/reverse/release/navigation; fast-contact toggle at closed/partial/open/negative states, either hand, armed right hand, blocked/animating rejection, persistent-contact latch and simultaneous-hand debounce; reversible cabinet hinges and drawers with either grip; native curve and endpoint synchronization; eased continuation; target-resting pose and reversed queue order; scripted/complex queue exclusions. Unity physics/native actions are simulated.");
 }
 static void TestQueueCabinet()
 {
  foreach(bool slide in new[]{false,true})foreach(bool right in new[]{false,true})
  {
   var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
   var pivot=new Transform();var c=new Collider{Center=new Vector3(1,0,0)};c.transform.parent=pivot;pivot.Children.Add(c.transform);
   var curve=new TransformValuesCurve{trans=pivot,effect=slide?TransformValuesCurve.Effect.PositionZ:TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=slide?-.4f:100};
   var element=new AnimationElement();element.valueCurveList.Add(curve);
   var q=new CustomAnimation{objTransform=pivot};q.animationQueue.Add(element);curve.trans=new Transform();element.transform=new Transform();
   var tool=new CustomAnimationTool{StateMode=false};tool.customAnimationQueue.Add(q);tool.meshColliders.Add(c);
   pivot.Components=new Component[]{tool};Physics.Overlap=new[]{c};Resources.Items=Array.Empty<ObjectBase>();
   void Hands(float z){rig.R=new PoseValue(right?new Vector3(1,0,z):new Vector3(-1,0,0));rig.L=new PoseValue(right?new Vector3(-1,0,0):new Vector3(1,0,z));}
   rig.RightControls=new(true,right?HandControls.Grip:0,0,0);rig.LeftControls=new(true,right?0:HandControls.Grip,0,0);
   Hands(0);doors.Tick(rig,true,actor,loose);Check(doors.Holding(right),"reversible cabinet not gripped");
   int sounds=ChairImpactClip.Played;Time.realtimeSinceStartup+=5;
   Hands(-.12f);doors.Tick(rig,true,actor,loose);Check(ChairImpactClip.Played==sounds+1,"door recording not played when the hand starts moving it");
   float opened=slide?-pivot.localPosition.z:pivot.localEulerAngles.y;
   Check(opened>.01f&&!q.animationPlaying,"reversible grip is frozen or fights native animation");
   Check(Math.Abs(curve.currentValue-(slide?pivot.localPosition.z:pivot.localEulerAngles.y))<.001f,"native curve out of sync");
   Hands(-.02f);doors.Tick(rig,true,actor,loose);Check((slide?-pivot.localPosition.z:pivot.localEulerAngles.y)<opened,"reversible cabinet cannot close");
   Check(ChairImpactClip.Played==sounds+1,"door recording restarted during one continuous movement");
   doors.Cancel();rig.RightControls=rig.LeftControls=new(true,0,0,0);
   Hands(.09f);doors.Tick(rig,true,actor,loose);Time.realtimeSinceStartup+=.02f;Hands(.04f);doors.Tick(rig,true,actor,loose);
   Check(tool.Starts==1,"cabinet fast strike does not dispatch native animation");
   doors.Dispose();
   int finished=tool.Finishes;var motion=DoorMotion.Read(tool).Single();motion.Set(motion.PositiveDelta,tool);motion.Finish(tool);
   Check(q.flipSequence&&q.animationWasCompleted&&tool.Finishes==finished+1,"open queue endpoint not committed for native close");
   motion.Set(0,tool);Check(!q.flipSequence,"closed queue endpoint cannot open again");
   curve.curve=new AnimationCurve();motion.Set(motion.PositiveDelta*.75f,tool);q.animationPlaying=true;motion.ResumeNative();
   Check(Math.Abs(element.evaluationTime-.5f)<.001f&&Math.Abs(element.animationStartTimestamp-49.5f)<.001f,"partial cabinet snaps instead of native eased continuation");q.animationPlaying=false;
   curve.restingPosition=1;var reversed=DoorMotion.Read(tool).Single();Check(reversed.Closed==curve.maxValue&&reversed.PositiveDelta==-motion.PositiveDelta,"target resting pose lost");
   q.animationQueueOrder=2;reversed.Set(reversed.PositiveDelta,tool);Check(q.flipSequence,"single reversed queue order flips direction incorrectly");
   q.queueSettings=1;Check(!DoorMotion.Read(tool).Any(),"looping machinery claimed as a cabinet");q.queueSettings=3;
   tool.animationTrigger=1;Check(!DoorMotion.Read(tool).Any(),"scripted OnEnable animation claimed");tool.animationTrigger=2;
   element.valueCurveList.Add(new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationX,maxValue=50});
   Check(!DoorMotion.Read(tool).Any(),"complex multi-axis script claimed");
  }
 }
 static void TestDeepCabinet()
 {
  foreach(bool right in new[]{false,true})
  {
   var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
   var pivot=new Transform();var leaf=new Collider{Center=new Vector3(1,0,0)};leaf.transform.parent=pivot;pivot.Children.Add(leaf.transform);
   var state=new CustomAnimationState{objTransform=new Transform(),positiveState=new TransformValuesCurve{trans=pivot,effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=100}};
   var tool=new CustomAnimationTool{customAnimationState=state};tool.meshColliders.Add(leaf);
   // Five unnamed ancestors and a controller sibling: old depth/name filter missed it.
   var root=pivot;for(int i=0;i<5;i++){var next=new Transform();root.parent=next;next.Children.Add(root);root=next;}
   var controller=new Transform{parent=root,Components=new Component[]{tool}};root.Children.Add(controller);
   Physics.Overlap=new[]{leaf};Resources.Items=Array.Empty<ObjectBase>();
   void Hand(float z){rig.R=new PoseValue(right?new Vector3(1,0,z):new Vector3(-1,0,0));rig.L=new PoseValue(right?new Vector3(-1,0,0):new Vector3(1,0,z));}
   rig.RightControls=new(true,right?HandControls.Grip:0,0,0);rig.LeftControls=new(true,right?0:HandControls.Grip,0,0);
   Hand(0);doors.Tick(rig,true,actor,loose);Check(doors.Holding(right),"deep unnamed cabinet with grip alone not found");
   Hand(-.1f);doors.Tick(rig,true,actor,loose);float angle=pivot.localEulerAngles.y;
   Check(angle>0&&state.objTransform.localEulerAngles.y==0,"cabinet rotates container instead of authored curve target");
   Hand(-.02f);doors.Tick(rig,true,actor,loose);Check(pivot.localEulerAngles.y<angle,"cabinet cannot reverse while held");
   rig.LeftControls=rig.RightControls=new(true,0,0,HandControls.Grip);doors.Tick(rig,true,actor,loose);Check(!doors.Holding(right),"cabinet stays grabbed after grip release");
   doors.Dispose();loose.Dispose();
  }
 }
 static void TestStoryDoor(bool grip,bool story,bool open,bool dud=false)
 {
  DoorStoryEvents.Story=story?"ActivateBehaviorTree x2":"";
  var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
  var state=new CustomAnimationState{objTransform=new Transform(),currentStateValue=open?1:0,
   positiveState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=90},
   negativeState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=-90}};
  state.objTransform.localEulerAngles=new Vector3(0,open?90:0,0);
  var tool=new CustomAnimationTool{customAnimationState=state,customAnimationToolState=!open};
  var action=new RaycastAction();action.OnPing=()=>{if(dud)return;state.currentStateValue=open?0:1;state.animationPlaying=true;};
  var door=new PlayMagic.AI.Door{doorReferences=new[]{tool},doorRaycastTargets=new[]{action}};
  var col=new Collider{Center=new Vector3(1,0,0)};col.gameObject.Extra=door;tool.meshColliders.Add(col);
  Physics.Overlap=new[]{col};Resources.Items=new ObjectBase[]{door};
  rig.LeftControls=new(true,0,0,0);rig.L=new PoseValue(new Vector3(-1,0,0));
  rig.RightControls=grip?new(true,HandControls.Grip,HandControls.Grip,0):new(true,0,0,0);
  string label=" grip="+grip+" story="+story+" open="+open;
  float z=grip?0:.09f;Time.realtimeSinceStartup+=1;rig.R=new PoseValue(new Vector3(1,0,z));doors.Tick(rig,true,actor,loose);
  if(grip){Check(doors.Holding(true),"door not grabbed"+label);rig.RightControls=new(true,HandControls.Grip,0,0);}
  float start=state.objTransform.localEulerAngles.y;
  // An open door is pulled back towards closed.
  void Step(){Time.realtimeSinceStartup+=.02f;z+=open?.003f:-.003f;rig.R=new PoseValue(new Vector3(1,0,z));doors.Tick(rig,true,actor,loose);}
  // A brush (half a degree): a closed story door waits, any other follows the hand.
  for(int i=0;i<3;i++)Step();
  bool waits=story&&!open;
  Check(action.Pings==0,"door toggled natively on a brush"+label);
  Check(waits?state.objTransform.localEulerAngles.y==start:state.objTransform.localEulerAngles.y!=start,(waits?"closed story door moved by the hand":"door no longer follows the hand")+label);
  for(int i=0;i<25&&action.Pings==0;i++)Step();
  if(waits)
  {
   Check(action.Pings==1,"closed story door never opened by its own interaction"+label);
   Check(state.objTransform.localEulerAngles.y==start,"story door nudged by the hand before its native swing"+label);
   Check(!doors.Holding(true),"the hand keeps hold of the native swing"+label);
  }
  else Check(action.Pings==0&&state.objTransform.localEulerAngles.y!=start,"door natively toggled instead of following the hand"+label);
  for(int i=0;i<5;i++)Step();
  Check(action.Pings==(waits?1:0),"story door toggled twice"+label);
  if(dud)
  {
   // Its interaction did not swing it open: the next push moves it by hand, no second native toggle.
   Time.realtimeSinceStartup+=1;z=.4f;rig.R=new PoseValue(new Vector3(1,0,z));doors.Tick(rig,true,actor,loose);
   Time.realtimeSinceStartup+=1;z=.09f;rig.R=new PoseValue(new Vector3(1,0,z));doors.Tick(rig,true,actor,loose);
   for(int i=0;i<25;i++)Step();
   Check(action.Pings==1&&state.objTransform.localEulerAngles.y!=start,"a story door its interaction did not open stays shut (or is toggled again)"+label);
  }
  doors.Dispose();loose.Dispose();DoorStoryEvents.Story="";
 }
 static void TestPlayerOnly(string listedKind,string playerKind,bool found,bool locked)
 {
  var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
  var state=new CustomAnimationState{objTransform=new Transform(),currentStateValue=0,
   positiveState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=90},
   negativeState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=-90}};
  var tool=new CustomAnimationTool{customAnimationState=state,customAnimationToolState=true};
  var all=new RaycastAction{Moves=tool,Active=listedKind!="off",ForPlayer=listedKind!="ai",Blocked=listedKind=="blocked"};
  var player=new RaycastAction{Moves=playerKind=="animates"?tool:null,Locked=locked};
  player.OnPing=()=>{state.currentStateValue=1;};all.OnPing=()=>{state.currentStateValue=1;};
  var door=new PlayMagic.AI.Door{doorReferences=new[]{tool},doorRaycastTargets=new[]{all}};
  var col=new Collider{Center=new Vector3(1,0,0)};col.gameObject.Extra=door;tool.meshColliders.Add(col);
  if(playerKind=="targeted")player.gameObject.Extra=door;
  Physics.Overlap=new[]{col};Resources.Items=new ObjectBase[]{door,all,player};
  PhysicalDoors.GameTarget=playerKind=="none"?null:()=>player;
  rig.LeftControls=rig.RightControls=new(true,0,0,0);rig.L=new PoseValue(new Vector3(-1,0,0));
  string label=" listed="+listedKind+" player="+playerKind+" locked="+locked;
  Time.realtimeSinceStartup+=1;rig.R=new PoseValue(new Vector3(1,0,.09f));doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;rig.R=new PoseValue(new Vector3(1,0,.04f));doors.Tick(rig,true,actor,loose);
  bool opens=found&&!locked;
  Check(player.Pings+all.Pings==(opens?1:0)&&state.currentStateValue==(opens?1:0),"a door with the player's own interaction beside the listed one: wrong toggle (player "+player.Pings+", listed "+all.Pings+")"+label);
  if(opens)Check(player.Pings==1,"the listed interaction used instead of the player's own"+label);
  doors.Dispose();loose.Dispose();PhysicalDoors.GameTarget=null;
 }
 static void TestUnlisted(bool animates)
 {
  var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
  var state=new CustomAnimationState{objTransform=new Transform(),currentStateValue=0,
   positiveState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=90},
   negativeState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=-90}};
  var tool=new CustomAnimationTool{customAnimationState=state,customAnimationToolState=true};
  var action=new RaycastAction{Moves=animates?tool:null};
  action.OnPing=()=>{state.currentStateValue=1;};
  var door=new PlayMagic.AI.Door{doorReferences=new[]{tool},m_doorsInfo=null};
  var col=new Collider{Center=new Vector3(1,0,0)};col.gameObject.Extra=door;tool.meshColliders.Add(col);
  Physics.Overlap=new[]{col};Resources.Items=new ObjectBase[]{door,action};
  rig.LeftControls=rig.RightControls=new(true,0,0,0);rig.L=new PoseValue(new Vector3(-1,0,0));
  Time.realtimeSinceStartup+=1;rig.R=new PoseValue(new Vector3(1,0,.09f));doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;rig.R=new PoseValue(new Vector3(1,0,.04f));doors.Tick(rig,true,actor,loose);
  Check(action.Pings==(animates?1:0),"a door with an empty list of interactions: wrong native toggles ("+action.Pings+", animates="+animates+")");
  Check(state.currentStateValue==(animates?1:0)&&door.Updates==0,"opened wrongly, or the missing door info read");
  // Grabbed and moved by the grip: the leaf follows without the door info.
  doors.Dispose();doors=new PhysicalDoors();
  rig.RightControls=new(true,HandControls.Grip,HandControls.Grip,0);state.currentStateValue=0;state.objTransform.localEulerAngles=Vector3.zero;
  Time.realtimeSinceStartup+=1;rig.R=new PoseValue(new Vector3(1,0,0));doors.Tick(rig,true,actor,loose);
  Check(doors.Holding(true)==animates,"grip on a door with an empty list: held="+doors.Holding(true)+" animates="+animates);
  rig.RightControls=new(true,HandControls.Grip,0,0);
  for(int i=0;i<10;i++){Time.realtimeSinceStartup+=.02f;rig.R=new PoseValue(new Vector3(1,0,-.003f*(i+1)));doors.Tick(rig,true,actor,loose);}
  Check((state.objTransform.localEulerAngles.y!=0)==animates&&door.Updates==0&&doors.Holding(true)==animates,"the hand does not move it, or the missing door info broke it");
  doors.Dispose();loose.Dispose();
 }
 static void TestStrike(float value,bool right,bool armed,bool blocked,bool playing,bool both)
 {
  var rig=new CameraRig();var loose=new LooseProps();var doors=new PhysicalDoors();var actor=new Actor();
  var state=new CustomAnimationState{objTransform=new Transform(),currentStateValue=value,animationPlaying=playing,
   positiveState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=90},
   negativeState=new TransformValuesCurve{effect=TransformValuesCurve.Effect.RotationY,minValue=0,maxValue=-90}};
  state.objTransform.localEulerAngles=new Vector3(0,value*90,0);
  var tool=new CustomAnimationTool{customAnimationState=state,customAnimationToolState=value==0};
  var action=new RaycastAction{Blocked=blocked};
  // Simulate the native toggle, including an instantaneous animation. The
  // adapter must dispatch exactly once, not set the state or rotate the leaf.
  action.OnPing=()=>{state.currentStateValue=Math.Abs(state.currentStateValue)<.03f?1:0;};
  var door=new PlayMagic.AI.Door{doorReferences=new[]{tool},doorRaycastTargets=new[]{action}};
  var col=new Collider{Center=new Vector3(1,0,0)};col.gameObject.Extra=door;tool.meshColliders.Add(col);
  Physics.Overlap=new[]{col};Resources.Items=new ObjectBase[]{door};
  rig.LeftControls=rig.RightControls=new(true,0,0,0);
  var weapon=armed?(right?new WeaponHands{RightFree=false,UseTip=true}:new WeaponHands{LeftArmed=true,UseTip=true}):null;WeaponHands.Current=weapon;
  void Hands(float z)
  {
   var close=new Vector3(1,0,z);var far=new Vector3(-1,0,0);
   rig.L=new PoseValue(!right||both?close:far);rig.R=new PoseValue(right||both?close:far);
   if(weapon!=null){weapon.Tip=close;if(right)rig.R=new PoseValue(close+new Vector3(0,0,.18f));else rig.L=new PoseValue(close+new Vector3(0,0,.18f));}
  }
  Time.realtimeSinceStartup+=1;Hands(.09f);doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;Hands(.04f);doors.Tick(rig,true,actor,loose);
  bool allowed=!blocked&&!playing;
  Check(action.Pings==(allowed?1:0),"wrong native toggle count: "+value+" right="+right+" armed="+armed+" blocked="+blocked+" playing="+playing+" both="+both);
  Check(state.currentStateValue==(allowed?(value==0?1:0):value),"strike did not close/open via native action");
  Check(state.objTransform.localEulerAngles.y==value*90,"strike physically nudged leaf after native dispatch");
  Time.realtimeSinceStartup+=.02f;Hands(.02f);doors.Tick(rig,true,actor,loose);
  Check(action.Pings==(allowed?1:0),"one contact immediately toggled twice");
  Time.realtimeSinceStartup+=.8f;doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;Hands(.001f);doors.Tick(rig,true,actor,loose);
  Check(action.Pings==(allowed?1:0),"contact repeats after cooldown without separation: "+value+" right="+right+" armed="+armed+" both="+both);
  Time.realtimeSinceStartup+=.02f;Hands(.24f);doors.Tick(rig,true,actor,loose);
  Time.realtimeSinceStartup+=.02f;Hands(.04f);doors.Tick(rig,true,actor,loose);
  Check(action.Pings==(allowed?2:0),"withdrawing and striking again fails to rearm");
  doors.Dispose();loose.Dispose();WeaponHands.Current=null;
 }
}
namespace Il2CppInterop.Runtime{static class Il2CppType{internal static Type Of<T>()=>typeof(T);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
 class Il2CppReferenceArray<T>{readonly T[] a;internal Il2CppReferenceArray(int n){a=new T[n];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}
 class Il2CppStructArray<T>{readonly T[] a;internal Il2CppStructArray(int n){a=new T[n];}internal int Length=>a.Length;internal T this[int i]{get=>a[i];set=>a[i]=value;}}
}
namespace UnityEngine
{
 class ObjectBase{internal string name="test";internal T? TryCast<T>() where T:class=>this as T;internal int GetInstanceID()=>GetHashCode();}
 class Object:ObjectBase{internal static void Destroy(ObjectBase o){}}
 class Scene{internal bool IsValid()=>true;}
 class GameObject:ObjectBase
 {
  internal Component? Extra;internal bool activeInHierarchy=>true;internal Scene scene=new();internal Transform transform=new();
  internal Component? GetComponent(Type t)=>Extra!=null&&t.IsInstanceOfType(Extra)?Extra:null;
  internal Component AddComponent(Type t){var c=(Component)Activator.CreateInstance(t)!;c.gameObject=this;return c;}
 }
 class Component:ObjectBase
 {
  internal GameObject gameObject=new();internal Transform transform=>gameObject.transform;internal bool enabled=true;
  internal Component? GetComponent(Type t)=>gameObject.GetComponent(t);internal Component? GetComponentInParent(Type t)=>GetComponent(t);
  internal Component[] GetComponentsInChildren(Type t,bool include)=>Array.Empty<Component>();
 }
 class Transform:ObjectBase
 {
  internal readonly List<Transform> Children=new();internal Component[] Components=Array.Empty<Component>();
  internal GameObject gameObject=>new();internal int childCount=>Children.Count;internal Transform? parent;internal Transform GetChild(int i)=>Children[i];
  internal Component[] GetComponents(Type t)=>Components.Where(c=>t.IsInstanceOfType(c)).ToArray();internal Quaternion rotation=>Quaternion.identity;
  internal Vector3 position=Vector3.zero,localEulerAngles,localPosition;internal Vector3 TransformVector(Vector3 v)=>v;internal bool IsChildOf(Transform t)=>ReferenceEquals(this,t)||(parent?.IsChildOf(t)??false);
  internal Component[] GetComponentsInChildren(Type t,bool include)=>Resources.Items.OfType<Component>().Where(x=>t.IsInstanceOfType(x)).ToArray();
 }
 struct Vector3
 {
  internal float x,y,z;internal Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  internal static Vector3 zero=>new();internal static Vector3 right=>new(1,0,0);internal static Vector3 up=>new(0,1,0);internal static Vector3 forward=>new(0,0,1);
  internal float sqrMagnitude=>x*x+y*y+z*z;internal float magnitude=>MathF.Sqrt(sqrMagnitude);internal Vector3 normalized=>magnitude>0?this/magnitude:zero;
  public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator*(Vector3 a,float n)=>new(a.x*n,a.y*n,a.z*n);public static Vector3 operator/(Vector3 a,float n)=>a*(1/n);
  internal static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  internal static Vector3 ClampMagnitude(Vector3 v,float max)=>v.magnitude>max?v.normalized*max:v;internal static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*t;
 }
 struct Quaternion
 {
  internal static Quaternion identity=>new();internal static Quaternion Inverse(Quaternion q)=>q;internal static Quaternion RotateTowards(Quaternion a,Quaternion b,float angle)=>b;
  public static Quaternion operator*(Quaternion a,Quaternion b)=>new();public static Vector3 operator*(Quaternion q,Vector3 v)=>v;
  internal void ToAngleAxis(out float a,out Vector3 axis){a=0;axis=Vector3.up;}
 }
 static class Mathf{internal const float Deg2Rad=MathF.PI/180;internal static float DeltaAngle(float a,float b){float d=(b-a)%360;if(d>180)d-=360;if(d< -180)d+=360;return d;}}
 struct Bounds{internal Vector3 size=>new(.1f,.1f,.1f);internal Vector3 extents=>size*.5f;internal Vector3 center=>Vector3.zero;}
 class Collider:Component{internal bool isTrigger=>false;internal Rigidbody? attachedRigidbody;internal Bounds bounds=>new();internal Vector3 Center;}
 class MeshCollider:Collider{internal bool convex=true;}
 class BoxCollider:Collider{internal Vector3 center=Vector3.zero,size=Vector3.zero;}
 class Renderer:Component{internal bool isPartOfStaticBatch=>false;internal Bounds bounds=>new();}
 class MeshRenderer:Renderer{}
 class Mesh:Component{internal Bounds bounds=>new();}
 class MeshFilter:Component{internal Mesh? sharedMesh=>null;}
 class Joint:Component{}
 enum RigidbodyConstraints{None}enum RigidbodyInterpolation{None,Interpolate}enum CollisionDetectionMode{Discrete,ContinuousDynamic,ContinuousSpeculative}enum ForceMode{Impulse}enum QueryTriggerInteraction{Ignore,Collide}
 class Rigidbody:Component
 {
  internal float drag,angularDrag;internal bool isKinematic,useGravity=true;internal float mass=1;internal RigidbodyConstraints constraints=RigidbodyConstraints.None;
  internal CollisionDetectionMode collisionDetectionMode;internal RigidbodyInterpolation interpolation;internal Vector3 position,velocity,angularVelocity;internal Quaternion rotation=Quaternion.identity;
  internal bool SweepTest(Vector3 d,out RaycastHit hit,float distance,QueryTriggerInteraction q){hit=new RaycastHit{distance=Physics.SweepDistance};return Physics.SweepDistance>=0;}
  internal void WakeUp(){}internal bool IsSleeping()=>false;internal void AddForceAtPosition(Vector3 v,Vector3 p,ForceMode m){velocity+=v;}
 }
 struct RaycastHit{internal float distance;internal Collider collider=>new();internal Vector3 point;}
 static class Physics
 {
  internal static float SweepDistance=-1;
  internal static bool ComputePenetration(Collider a,Vector3 p,Quaternion q,Collider b,Vector3 bp,Quaternion bq,out Vector3 direction,out float distance){direction=Vector3.zero;distance=0;return false;}
  internal static Collider[] Overlap=Array.Empty<Collider>();
  internal static int OverlapSphereNonAlloc(Vector3 p,float radius,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> a,int mask,QueryTriggerInteraction q){for(int i=0;i<Overlap.Length;i++)a[i]=Overlap[i];return Overlap.Length;}
  internal static int SphereCastNonAlloc(Vector3 p,float radius,Vector3 dir,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> a,float distance,int mask,QueryTriggerInteraction q)=>0;
 }
 static class Time{internal static float unscaledDeltaTime=>.02f;internal static float realtimeSinceStartup=1;}
 static class Resources{internal static ObjectBase[] Items=Array.Empty<ObjectBase>();internal static ObjectBase[] FindObjectsOfTypeAll(Type t)=>Items.Where(x=>t.IsInstanceOfType(x)).ToArray();}
}
class PickableItem:Component{}class Projectile:Component{}
class PlayerEquipableInventory{internal enum ActiveEquipmentSlot{Enviromental,Fist}}
namespace PlayMagic.Weapons{class Equipable:Component{internal PlayerEquipableInventory.ActiveEquipmentSlot slot=>PlayerEquipableInventory.ActiveEquipmentSlot.Fist;}}
namespace PlayMagic.AI
{
 class NPC:Component{}
 class Door:Component
 {
  internal CustomAnimationTool[] doorReferences=Array.Empty<CustomAnimationTool>();internal RaycastAction[] doorRaycastTargets=Array.Empty<RaycastAction>();internal bool WaitForSpecialDoorSetup=>false;internal int Updates,Commits;
  internal RaycastAction? m_raycastAction=null;internal object[]? m_doorsInfo=new object[1];
  internal void UpdateDoorInfo(CustomAnimationTool t,bool end){if(m_doorsInfo==null||m_doorsInfo.Length==0)throw new IndexOutOfRangeException();Updates++;if(end)Commits++;}
 }
}
interface IInteractionActor{}class Actor:IInteractionActor{}
class RaycastAction:Component{internal CustomAnimationTool? Moves;internal bool Active=true,ForPlayer=true,Locked;internal enum InteractionConditionals{Nothing,Key}internal InteractionConditionals conditional=>Locked?InteractionConditionals.Key:InteractionConditionals.Nothing;internal bool isActiveAndEnabled=>Active;internal bool Blocked;internal int Pings;internal Action? OnPing;internal bool IsActorValid(IInteractionActor a)=>ForPlayer;internal bool IsRaycastPingValid(IInteractionActor a,RaycastHit h)=>!Blocked;internal void PingRaycastHittable(IInteractionActor a,RaycastHit h,out bool valid){Pings++;valid=!Blocked;if(valid)OnPing?.Invoke();}internal bool IsInteractionBlocked(IInteractionActor a)=>Blocked;internal bool GetConditionState()=>!Blocked&&!Locked;}
class TransformValuesCurve{internal enum Effect{PositionX=0,PositionY=1,PositionZ=2,RotationX=3,RotationY=4,RotationZ=5}internal Effect effect;internal Transform? trans;internal int restingPosition=0;internal AnimationCurve? curve=null;internal float minValue,maxValue,currentValue;}
class OcclusionPortal{internal bool open;}
class CustomAnimationState{internal OcclusionPortal? occlusionPortal=>null;internal Transform? objTransform;internal TransformValuesCurve? positiveState,negativeState;internal bool animationPlaying;internal float currentStateValue,previewValue,previousPreviewValue,currentValueOnStart;internal int stateTarget;internal void UpdateOcclusionPortal()=>throw new Exception("missing optional portal");}
class CustomAnimationTool:Component{internal bool StateMode=true;internal bool IsState()=>StateMode;internal int animationTrigger=2,Starts,Finishes;internal int animationType=>StateMode?1:0;internal List<CustomAnimation> customAnimationQueue=new();internal void StartCustomAnimationTool(IInteractionActor a,float speed){Starts++;}internal void CheckAnimationQueueComplete(CustomAnimation q){Finishes++;}internal CustomAnimationState customAnimationState=new();internal List<Collider> meshColliders=new();internal bool customAnimationToolState;internal Action<CustomAnimationTool,bool>? OnAnimationStarted=>null;internal void TriggerStateAnimationFinished(CustomAnimationState s){}}
namespace XiiiXR
{
 struct PoseValue{internal Vector3 Position;internal PoseValue(Vector3 p){Position=p;}}
 class CameraRig
 {
  internal Transform PlayerRoot=new();internal PoseValue L=new(new Vector3(-1,0,0)),R=new(new Vector3(1,0,0));internal HandControls LeftControls=new(true,0,0,0),RightControls;
  internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool lv){l=L;r=R;lv=true;return true;}internal static Vector3 UnityPosition(PoseValue p)=>p.Position;internal void PunchHaptics(bool r){}
 }
 static class ControllerAim{internal static Quaternion Rotation(PoseValue p)=>Quaternion.identity;}
 static class ContactWorld{internal static System.Numerics.Vector3 V(Vector3 v)=>new(v.x,v.y,v.z);internal static Vector3 U(System.Numerics.Vector3 v)=>new(v.X,v.Y,v.Z);}
 static class ColliderSurface{internal static bool TryClosest(Collider c,Vector3 p,out Vector3 point){point=c.Center;return true;}}
 class ContactFilter{internal bool Excluded(Collider c)=>false;}
 class WeaponHands{internal static WeaponHands? Current;internal bool RightFree=true,UseTip;internal Vector3 Tip;internal bool HandFree(bool r)=>r?RightFree:!LeftArmed;internal bool TryMeleeTip(out Vector3 v){v=Tip;return UseTip;}internal bool LeftArmed;internal bool MeleeHand(bool r)=>r?!RightFree:LeftArmed;internal bool TryMeleeTip(bool r,out Vector3 v){v=Tip;return UseTip&&(r||LeftArmed);}}
 class GripCarry{internal static GripCarry? Current=null;internal bool HidesLeft=true;internal Transform? BodyRoot=>new Transform();}
 static class DoorStoryEvents{internal static bool Animates(RaycastAction a,CustomAnimationTool t)=>a.Moves==t;internal static string Story="";internal static string Find(RaycastAction[] a,CustomAnimationTool t,PlayMagic.AI.Door? d,Vector3 p,out string story){story=Story;return Story.Length>0?"door motion, "+Story:"door motion";}}
 class ChairImpactClip:System.IDisposable{internal static int Played;internal ChairImpactClip(System.Func<byte[]> w,string n,float g,float d){}internal bool Play(Vector3 p){Played++;return true;}internal void Stop(){}public void Dispose(){}}
 static class DoorMoveSound{internal static byte[] Wav()=>new byte[0];}
 class PunchingBags:System.IDisposable{internal bool TryHit(Collider c,Vector3 p,Vector3 v,Transform? player)=>false;internal void Report(Collider c){}internal void Tick(){}public void Dispose(){}}
 static class Bootstrap{internal static void Write(string s){}internal static void Warn(string s)=>throw new Exception(s);}
}

namespace XiiiXR{
 class MovablePropMesh:IDisposable{internal bool Prepare(Renderer r)=>!r.isPartOfStaticBatch;public void Dispose(){}}
}

class CustomAnimation{internal float lowestElementOffset;internal int queueSettings=3,animationQueueOrder=0,currentIndex,cycleCount;internal Transform? objTransform=null;internal bool animationPlaying,animationWasCompleted,flipSequence;internal List<AnimationElement> animationQueue=new();}
class AnimationElement{internal Transform? transform=null;internal List<TransformValuesCurve> valueCurveList=new();internal float duration=1,delay=0,animationStartTimestamp,animationElapsedTime,evaluationTime,pauseCompensation,currentDelay;}

namespace UnityEngine{class AnimationCurve{internal float Evaluate(float t)=>t*t;}}
namespace PlayMagic{static class SafeTime{internal static float adjustedTimeSinceLevelLoad=>50;}}
