using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace XiiiXR;
// 0.1.213: a hostage is taken by pointing at him, no touch: a free hand aims
// its controller at an enemy he can be taken from (from behind, by the game's
// rule or the VR one), the game's hostage icon shows with "Hold left/right
// Grip" for that hand, and that hand's grip takes him; he is held by that
// hand while its grip is held. Taking him by touching him with the left hand
// still works.
// 0.1.216: the grip held a moment with the hand still (HostagePointMath.HoldStep):
// closed and swung at once it is a punch (in his back: a knockout).
internal sealed partial class GripCarry
{
 private readonly Il2CppStructArray<RaycastHit> pointHits=new(32);
 private readonly List<(float distance,HostagePointMath.Hit kind,int index)> pointList=new();
 private int pointedSide=-1;private bool promptForced;
 // The hand pointing at a hostage now (0 left, 1 right), -1 none.
 internal int HostagePointSide=>carrier==null&&releasing==null?pointedSide:-1;
 partial void PointHostage(bool allowed,ref bool taken)
 {
  int shown=pointedSide;pointedSide=-1;
  var hostages=handler?.playerHostageController;
  if(!allowed||hostages==null||!rig.SampleWorldHands(out var left,out var right,out bool lv)){holdSide=-1;return;}
  bool pressL=Pressed(false),pressR=Pressed(true);
  PlayMagic.AI.NPC? npcL=null,npcR=null;string whyL="",whyR="";
  bool okL=lv&&Free(false)&&Pointed(false,left,hostages,pressL||holdSide==0,out npcL,out whyL);
  bool okR=Free(true)&&Pointed(true,right,hostages,pressR||holdSide==1,out npcR,out whyR);
  float now=Time.realtimeSinceStartup;
  if(holdSide>=0)
  {
   var npc=holdSide==1?npcR:npcL;var hand=HandLocal(holdSide==1?right:left);
   var step=HostagePointMath.HoldStep(Held(holdSide==1),holdSide==1?okR:okL,npc!=null&&npc.Pointer==holdNpc,now-holdAt,(hand-holdFrom).magnitude);
   if(step==HostagePointMath.Hold.Take&&npc!=null)
   {
    int s=holdSide;holdSide=-1;
    BeginBody(hostages,npc);bodyRight=s==1;bodyButton=HandControls.Grip;
    BeforeHostage(npc);hostages.TakeHostage(npc,npc.transform.parent,false);
    string why=s==1?whyR:whyL;
    Bootstrap.Write("HOSTAGE "+npc.name+" taken by pointing ("+(s==1?"right":"left")+" grip held"+(why.Length>0?"; "+why:"")+")");
    taken=true;return;
   }
   if(step==HostagePointMath.Hold.Wait){pointedSide=holdSide;return;}
   Bootstrap.Write("HOSTAGE not taken: the "+(holdSide==1?"right":"left")+" grip "+(!Held(holdSide==1)?"opened":(hand-holdFrom).magnitude>HostagePointMath.StillReach?"swung (a punch)":"no longer pointing at him")+" before "+HostagePointMath.HoldTime.ToString("F1")+" s");
   holdSide=-1;
  }
  int pressed=okR&&pressR?1:okL&&pressL?0:-1;
  int side=HostagePointMath.Side(okL,okR,pressed,shown);
  if(side<0)return;
  if(side==pressed)
  {
   var npc=side==1?npcR:npcL;if(npc==null)return;
   holdSide=side;holdNpc=npc.Pointer;holdAt=now;holdFrom=HandLocal(side==1?right:left);pointedSide=side;
   return;
  }
  pointedSide=side;
  if(shown!=side)Bootstrap.Write("HOSTAGE pointed at with the "+(side==1?"right":"left")+" hand: its grip takes him");
 }
 private bool Pressed(bool right){var c=right?rig.RightControls:rig.LeftControls;return c.Valid&&(c.Down&HandControls.Grip)!=0;}
 private bool Held(bool right){var c=right?rig.RightControls:rig.LeftControls;return c.Valid&&(c.Held&HandControls.Grip)!=0;}
 // The grip closed on a pointed hostage: which hand, on whom, when, where (the player's own space).
 private int holdSide=-1;private IntPtr holdNpc;private float holdAt;private Vector3 holdFrom;
 private Vector3 HandLocal(PoseValue pose){var at=CameraRig.UnityPosition(pose);return player!=null?player.InverseTransformPoint(at):at;}
 private static bool Free(bool right)=>WeaponHands.Current?.HandFree(right)!=false&&InteractionDriver.Current?.HandOccupied(right)!=true&&GameUiControls.Current?.ItemHeldOn(right)!=true;
 private bool Pointed(bool right,PoseValue pose,PlayerHostageController hostages,bool pressed,out PlayMagic.AI.NPC? npc,out string why)
 {
  npc=null;why="";
  var origin=CameraRig.UnityPosition(pose);var q=right?ControllerAim.Rotation(pose):ControllerAim.Mirrored(pose);var direction=q*Vector3.forward;
  int n=Physics.RaycastNonAlloc(origin,direction,pointHits,HostagePointMath.Reach,~0,QueryTriggerInteraction.Collide);
  pointList.Clear();
  for(int i=0;i<Math.Min(n,pointHits.Length);i++)
  {
   var c=pointHits[i].collider;if(c==null)continue;
   HostagePointMath.Hit kind;
   if(player!=null&&c.transform.IsChildOf(player))kind=HostagePointMath.Hit.Player;
   else if(c.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())!=null)kind=HostagePointMath.Hit.Npc;
   else if(c.isTrigger||c.attachedRigidbody!=null&&!c.attachedRigidbody.isKinematic)kind=HostagePointMath.Hit.Passable;
   else kind=HostagePointMath.Hit.World;
   pointList.Add((pointHits[i].distance,kind,i));
  }
  int k=HostagePointMath.FirstNpc(pointList);if(k<0)return false;
  var hit=pointHits[pointList[k].index];var collider=hit.collider;
  npc=collider.GetComponentInParent(Il2CppType.Of<PlayMagic.AI.NPC>())?.TryCast<PlayMagic.AI.NPC>();if(npc==null)return false;
  var target=collider.GetComponentInParent(Il2CppType.Of<NPCHittable>())?.TryCast<IRaycastHittable>();
  target??=npc.GetComponentInChildren(Il2CppType.Of<NPCHittable>(),true)?.TryCast<IRaycastHittable>();
  if(target==null)return false;
  return HostageAllowed(hostages,target,npc,hit.point,out why,pressed);
 }
 // The game's hostage icon while a hand points at one (its text: VrPromptLabels).
 internal void HostagePrompt(PlayerHUDControl hud)
 {
  try
  {
   if(hud==null||!hud.GetOwner().IsPlayer)return;
   if(HostagePointSide>=0)
   {
    promptForced=true;
    if(!hud.IsPromptDisplayed()||hud.promptGroup==null||hud.promptGroup.currentPrimaryType!=HUDInteractionPrompt.PromptType.HostagePickup)
     hud.TogglePrompt(HUDInteractionPrompt.PromptType.HostagePickup,HUDInteractionPrompt.PromptType.Hidden);
   }
   else if(promptForced)
   {
    promptForced=false;
    if(hud.promptGroup!=null&&hud.promptGroup.currentPrimaryType==HUDInteractionPrompt.PromptType.HostagePickup&&InteractionDriver.Current?.BodyTarget!=true)hud.HidePrompt();
   }
  }
  catch(Exception ex){promptForced=false;Bootstrap.Warn("HOSTAGE prompt: "+ex.Message);}
 }
}
