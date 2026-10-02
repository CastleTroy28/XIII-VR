using System;
using System.Linq;
using XiiiXR;
using UnityEngine;
using PlayMagic.Weapons;
using N=System.Numerics;
class PunchDriverTests
{
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static void Main()
    {
        ContactRig.Current=null;NpcHitReactions.Current!.Hits=0;var f=new Fixture();f.Warm();f.Swing();Check(f.Melee.Hits==1&&f.Rig.Pulses==1&&f.Melee.ImpactSounds==1,"real contact not dispatched exactly once");
        Check(NpcHitReactions.Current.Hits==1,"the NPC's skeleton reaction not called exactly once: "+NpcHitReactions.Current.Hits);
        // 0.1.141: a fist blow takes 1/8..1/5 of the NPC's full health; the game's damage is restored.
        Check(f.Melee.LastDamage>=240f/8-.01f&&f.Melee.LastDamage<=240f/5+.01f&&f.Melee.MeleeDamage==20,"fist blow share "+f.Melee.LastDamage+" / damage restored "+f.Melee.MeleeDamage);
        Check(NpcHitReactions.Guards==1&&Math.Abs(NpcHitReactions.GuardShare-f.Melee.LastDamage)<.01f,"0.1.148: the game's damage of the punch not guarded to its share ("+NpcHitReactions.Guards+", "+NpcHitReactions.GuardShare+")");
        Check(f.Melee.Comics==1&&f.Melee.playerArmsAnimationControl.Swings==1,"comic or swing cue missing/duplicated");
        Check(f.Inventory.playerUsedMeleeForce&&f.Melee.currentMeleeDamageMultiplier==3,"native attribution/multiplier restore failed");
        f.Swing();Check(f.Melee.Hits==1,"moving through another collider repeated the hit");
        f=new Fixture();f.Warm();var captured=f;f.Melee.OnHit=(_,_)=>captured.Rig.Pulses=0;
        f.Swing();Check(f.Npc.CanDamage&&f.Rig.Pulses==1,"surviving NPC/native pain feedback erases physical impact");
        f=new Fixture();for(int i=0;i<12;i++)f.Tick(N.Vector3.Zero,N.Vector3.Zero,true);f.Swing();Check(f.Melee.Hits==1,"held grip at weapon change cannot rearm");
        f=new Fixture();f.Warm();f.NpcCollider.isTrigger=true;f.Swing();Check(f.Melee.Hits==1,"native trigger hurtbox ignored");
        f=new Fixture();f.Warm();Physics.Hits=new[]{new RaycastHit{collider=f.NpcCollider,distance=0}};f.Swing();Check(f.Melee.Hits==1,"initial NPC contact lost");
        f=new Fixture();f.Warm();var glass=new DamageAction();Physics.Hits=new[]{new RaycastHit{collider=new Collider{Damage=glass},distance=.01f}};
        f.Swing();Check(f.Melee.Hits==1&&glass.ignoreNonLethalDamage,"glass did not receive native damage or leaked nonlethal setting");
        NpcHitReactions.Current.Hits=0;f=new Fixture();f.Warm();Physics.Hits=Array.Empty<RaycastHit>();f.Swing();Check(f.Melee.Hits==0&&PropImpactAudio.PropNpc==0&&PropImpactAudio.World==0&&NpcHitReactions.Current.Hits==0,"empty-air swing damaged NPC/played impact/made an NPC react");
        // 0.1.139: an NPC already down is pushed (a reaction), not damaged.
        NpcHitReactions.Current.Hits=0;f=new Fixture();f.Warm();f.Npc.IsAlive=false;f.Npc.CanDamage=false;f.Swing();Check(f.Melee.Hits==0&&NpcHitReactions.Current.Hits==1,"a body on the floor not pushed once / damaged");
        f=new Fixture();f.Warm();Physics.Hits=new[]{new RaycastHit{collider=f.NpcCollider,distance=.03f},new RaycastHit{collider=new Collider(),distance=.01f}};f.Swing();Check(f.Melee.Hits==0&&f.Melee.Comics==1&&f.Melee.ImpactSounds==1&&f.Rig.Pulses==1&&PropImpactAudio.World==1&&PropImpactAudio.PropNpc==0,"wall should stop damage and produce one impact feedback");
        f=new Fixture();f.Warm();Physics.Hits=Enumerable.Repeat(new RaycastHit{collider=f.NpcCollider,distance=.01f},64).ToArray();f.Swing();Check(f.Melee.Hits==0,"saturated query should not guess nearest collider");
        f=new Fixture();f.Warm();f.Npc.CanDamage=false;f.Swing();Check(f.Melee.Hits==0,"invulnerable NPC damaged");
        f=new Fixture();f.Warm();f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Pistol;f.Swing();Check(f.Melee.Hits==0,"armed gun produced fist damage");
        f=new Fixture();f.Warm();for(int i=0;i<25;i++)f.Tick(new N.Vector3(0,0,i*.03f),N.Vector3.Zero,true);Check(f.Melee.Hits==0,"stick locomotion caused punch");
        f=new Fixture();f.Warm();for(int i=0;i<30;i++)f.Tick(new N.Vector3(0,0,i*.013f),new N.Vector3(0,0,i*.013f),true);Check(f.Melee.Hits==0&&f.Rig.Pulses==0,"gentle 1.3 m/s touching caused punch damage/impact");
        f=new Fixture();f.Warm();f.Rig.Valid=false;f.Swing();Check(f.Melee.Hits==0,"tracking loss damaged NPC");
        f=new Fixture();f.Warm();f.Allowed=false;f.Swing();Check(f.Melee.Hits==0,"menu/input lock damaged NPC");
        f=new Fixture();f.Warm();f.Melee.Throw=true;f.Swing();Check(f.Melee.currentMeleeDamageMultiplier==3,"exception leaked temporary damage multiplier");
        Check(Bootstrap.Warnings>0,"native failure not contained/reported");
        f=new Fixture();ContactRig.Current=new ContactRig();f.Warm();Physics.Hits=new[]{new RaycastHit{collider=new Collider(),distance=.01f}};f.Swing();Check(f.Melee.Hits==0&&Physics.LastOrigin.N.Z<.02f,"raw controller behind wall seeded a punch beyond constrained hand");ContactRig.Current=null;
        f=new Fixture();var carried=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
        WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(carried.Rig.World)};f.Warm();f.FastSwing();
        Check(f.Melee.Hits==1&&f.Rig.Pulses==1&&WeaponHands.Current.Broken==1&&PropImpactAudio.PropNpc==1,"prop physical contact did not damage, vibrate and consume the item");WeaponHands.Current=null;
        // 0.1.108: a prop moved at 2.5 m/s into a wall/NPC is a touch, not a blow.
        f=new Fixture();var touched=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
        WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(touched.Rig.World)};f.Warm();f.Swing();
        Check(f.Melee.Hits==0&&f.Rig.Pulses==0&&WeaponHands.Current.Broken==0&&PropImpactAudio.PropNpc==0&&PropImpactAudio.World==0,"held prop broke/hit on a slow touch");
        f.Swing();f.FastSwing();Check(WeaponHands.Current.Broken<=1,"prop broken twice");WeaponHands.Current=null;
        // 0.1.150: a shovel's blow: the NPC stays up (at most a third of its health), the shovel is worn, not broken; the game's own hit listeners (they break it) not called.
        {
            f=new Fixture();var shovel=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;f.Weapon.identifier="wpn_ms_shovel";int listeners=0;f.Melee.OnHit=(_,_)=>listeners++;
            NpcHitReactions.Current!.Hits=0;WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(shovel.Rig.World)};f.Warm();f.FastSwing();
            Check(f.Melee.Hits==1&&WeaponHands.Current.Broken==0&&WeaponHands.Current.WornOnEnemy==1&&listeners==0&&NpcHitReactions.Current.Hits==1,"shovel: hits="+f.Melee.Hits+" broken="+WeaponHands.Current.Broken+" worn="+WeaponHands.Current.WornOnEnemy+" listeners="+listeners);
            Check(f.Melee.LastDamage>=240f/5-.01f&&f.Melee.LastDamage<=240f/3+.01f&&f.Melee.MeleeDamage==20,"shovel share "+f.Melee.LastDamage);
            WeaponHands.Current=null;
        }
        Check(!PunchMotion.PropSwing(2.9f)&&PunchMotion.PropSwing(3f)&&!PunchMotion.PropSwing(float.NaN),"prop swing threshold");
        f=new Fixture();var stopped=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Pistol;
        WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(stopped.Rig.World),Safe=()=>Vector3.zero};f.Warm();
        Physics.Hits=new[]{new RaycastHit{collider=new Collider(),distance=.01f}};f.Swing();
        Check(f.Melee.Hits==0&&Physics.LastOrigin.N.Z<.02f,"desired weapon pose seeds damage behind a blocked surface");WeaponHands.Current=null;
        // 0.1.172: a gun copy swung in the hand hits with its other end (the stock) too, not only the muzzle.
        {
            f=new Fixture();var stock=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Shotgun;
            WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(stock.Rig.World+new N.Vector3(5,0,0)),Other=()=>new Vector3(stock.Rig.World)};
            Physics.Only=o=>o.N.X<1;f.Warm();f.Swing();
            Check(f.Melee.Hits==1,"the stock of a gun in the hand did not hit (hits="+f.Melee.Hits+")");
            Physics.Only=null;WeaponHands.Current=null;
        }
        // 0.1.196: a gun swung by its barrel (a club) hits like a shovel: at most a third, at least a fifth of the NPC's health;
        // the same gun held by its handle: at most a fifth. Neither is worn or broken.
        foreach(bool club in new[]{true,false})
        {
            f=new Fixture();var clubbed=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Shotgun;f.Weapon.identifier="wpn_shotgun";
            NpcHitReactions.Current!.Hits=0;WeaponHands.Current=new WeaponHands{Club=club,Tip=()=>new Vector3(clubbed.Rig.World)};f.Warm();f.FastSwing();
            Check(f.Melee.Hits==1&&WeaponHands.Current.Broken==0&&WeaponHands.Current.Worn==0,"a gun blow (club="+club+") hits="+f.Melee.Hits+" broken="+WeaponHands.Current.Broken);
            if(club)Check(f.Melee.LastDamage>=240f/5-.01f&&f.Melee.LastDamage<=240f/3+.01f,"a club's share "+f.Melee.LastDamage+" not a shovel's");
            else Check(f.Melee.LastDamage<=240f/5+.01f,"a gun by its handle hit as hard as a club: "+f.Melee.LastDamage);
            WeaponHands.Current=null;
        }
        Console.WriteLine("PASS: 0.1.196 a gun swung by its barrel (a club) takes a fifth to a third of an enemy's health a blow, as a shovel; by its handle at most a fifth; neither worn nor broken.");
        // 0.1.202: each part of a long thing
        // has its own nearest hit - one part meeting a wall first does not stop another part striking an enemy;
        // a wall in the way of the same part still does.
        {
            var wall=new Collider();
            foreach(bool sameWay in new[]{false,true})
            {
                f=new Fixture();var longOne=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Shotgun;f.Weapon.identifier="wpn_shotgun";NpcHitReactions.Current!.Hits=0;
                WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(longOne.Rig.World),GameMelee=true,Shape=new[]{new ContactSphere(new N.Vector3(5,0,0),.05f),new ContactSphere(N.Vector3.Zero,.05f)}};
                Physics.PerOrigin=o=>sameWay?new[]{new RaycastHit{collider=wall,distance=.005f},new RaycastHit{collider=longOne.NpcCollider,distance=.03f}}
                    :o.N.X>1?new[]{new RaycastHit{collider=wall,distance=.005f}}:new[]{new RaycastHit{collider=longOne.NpcCollider,distance=.03f}};
                f.Warm();f.FastSwing();
                if(sameWay)Check(f.Melee.Hits==0&&PropImpactAudio.World>=1,"an enemy struck through a wall in the same part's way (hits="+f.Melee.Hits+")");
                else Check(f.Melee.Hits==1&&NpcHitReactions.Current.Hits==1,"another part's wall touch stopped a long thing's blow on an enemy (hits="+f.Melee.Hits+")");
            }
            Physics.PerOrigin=null;WeaponHands.Current=null;
        }
        Console.WriteLine("PASS: 0.1.202 a long thing's parts each meet their own nearest: a wall touched by one part does not stop another part's blow on an enemy; a wall in that part's way does.");
        // 0.1.205: the
        // club stopped at his guard touches him (the sweeps cannot see it) while another part touches the
        // world - the enemy it touches is struck, not the wall; with nothing touching him the wall's knock stays.
        {
            var wall=new Collider();
            foreach(bool touching in new[]{true,false})
            {
                f=new Fixture();var longOne=f;f.Weapon.slot=PlayerEquipableInventory.ActiveEquipmentSlot.Shotgun;f.Weapon.identifier="wpn_shotgun";NpcHitReactions.Current!.Hits=0;PropImpactAudio.World=0;
                WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(longOne.Rig.World),GameMelee=true,Shape=new[]{new ContactSphere(new N.Vector3(5,0,0),.05f),new ContactSphere(N.Vector3.Zero,.05f)}};
                Physics.PerOrigin=o=>o.N.X>1?new[]{new RaycastHit{collider=wall,distance=.005f}}:Array.Empty<RaycastHit>();
                PunchDriver.TestTouch=touching?longOne.NpcCollider:null;
                f.Warm();f.FastSwing();
                if(touching)Check(f.Melee.Hits==1&&NpcHitReactions.Current.Hits==1,"a wall touched by one part took the blow on the enemy the club touches (hits="+f.Melee.Hits+")");
                else Check(f.Melee.Hits==0&&PropImpactAudio.World>=1,"a wall touch without an enemy lost its knock");
            }
            Physics.PerOrigin=null;WeaponHands.Current=null;PunchDriver.TestTouch=null;
        }
        Console.WriteLine("PASS: 0.1.205 an enemy the club touches (stopped at his guard) is struck even when another part touches a wall; a wall alone still knocks.");
        // 0.1.173: a blow with a gun on an enemy: the fists' comic picture and body thud (the gun's melee has none).
        {
            f=new Fixture();var gunHit=f;f.Tick(N.Vector3.Zero,N.Vector3.Zero,false);int fistComics=f.Melee.Comics,fistSounds=f.Melee.ImpactSounds;PropImpactAudio.Bodies=0;
            var gunMelee=new MeleeComponent();var gun=new Equipable{slot=PlayerEquipableInventory.ActiveEquipmentSlot.Shotgun,Melee=gunMelee};gunMelee.baseEquipable=gun;gun.transform.Parent=f.Rig.PlayerRoot;
            f.Inventory.currentEquipable=gun;WeaponHands.Current=new WeaponHands{Tip=()=>new Vector3(gunHit.Rig.World)};
            f.Warm();f.Swing();
            Check(gunMelee.Hits==1&&f.Melee.Comics==fistComics+1&&gunMelee.Comics==0&&PropImpactAudio.Bodies==1&&f.Melee.ImpactSounds==fistSounds+1,
                "gun blow: hits="+gunMelee.Hits+" fist comics="+(f.Melee.Comics-fistComics)+" gun comics="+gunMelee.Comics+" body sounds="+PropImpactAudio.Bodies);
            WeaponHands.Current=null;
        }
        // 0.1.141: a weapon thrown into an NPC: a little damage (1/16..1/10 of its health), it reacts; the game's damage restored.
        NpcHitReactions.Current.Hits=0;f=new Fixture();f.Warm();int before=f.Melee.Hits;
        bool took=f.Driver.Thrown(f.NpcCollider,new RaycastHit{collider=f.NpcCollider,point=new Vector3(0,1,1)},new Vector3(0,0,8),"pistol",null);
        Check(took&&f.Melee.Hits==before+1&&f.Melee.LastDamage>=240f/16-.01f&&f.Melee.LastDamage<=240f/10+.01f&&f.Melee.MeleeDamage==20&&f.Melee.currentMeleeDamageMultiplier==3&&NpcHitReactions.Current.Hits==1,
            "thrown weapon: took="+took+" hits="+(f.Melee.Hits-before)+" share="+f.Melee.LastDamage+" restored="+f.Melee.MeleeDamage+" reactions="+NpcHitReactions.Current.Hits);
        f.Npc.IsAlive=false;before=f.Melee.Hits;Check(f.Driver.Thrown(f.NpcCollider,new RaycastHit{collider=f.NpcCollider},new Vector3(0,0,8),"pistol",null)&&f.Melee.Hits==before,"thrown weapon damaged a body on the floor");
        Check(!f.Driver.Thrown(new Collider(),new RaycastHit(),new Vector3(0,0,8),"pistol",null),"thrown weapon hit something that is not an NPC");
        Console.WriteLine("PASS: a shovel blow takes at most a third of the NPC's health, the NPC reacts, the shovel is worn (not broken) and the game's breaking listeners are skipped.");
        Console.WriteLine("PASS: production PunchDriver dispatches native damage once on contact; held props break/hit only on a real swing (>=3 m/s), not on a touch; air/walls/invulnerability/weapon slot/locomotion/tracking/locks prevent damage; native multiplier restored on exceptions.");
        Console.WriteLine("Mock physics and native melee receiver; actual NPC reactions and sound still need in-game validation.");
    }
    sealed class Fixture
    {
        internal readonly PunchDriver Driver=new();internal readonly CameraRig Rig=new();internal readonly PlayerEquipableInventory Inventory=new();
        internal readonly MeleeComponent Melee=new();internal readonly Equipable Weapon=new();internal readonly PlayMagic.AI.NPC Npc=new();internal readonly Collider NpcCollider=new BoxCollider();internal bool Allowed=true;private float time;
        internal Fixture()
        {
            PropImpactAudio.PropNpc=PropImpactAudio.World=0;Time.realtimeSinceStartup=0;Weapon.transform.Parent=Rig.PlayerRoot;Weapon.Melee=Melee;Melee.baseEquipable=Weapon;Inventory.currentEquipable=Weapon;
            NpcCollider.Npc=Npc;Physics.Hits=new[]{new RaycastHit{collider=NpcCollider,distance=.01f},new RaycastHit{collider=NpcCollider,distance=.02f}};
        }
        internal void Tick(N.Vector3 world,N.Vector3 raw,bool grip)
        {time+=.01f;Time.realtimeSinceStartup=time;Rig.World=world;Rig.Raw=raw;Rig.RightControls=new HandControls{Valid=true,Held=grip?HandControls.Grip:0};Driver.Tick(Rig,Inventory,Allowed);}
        internal void Warm(){Tick(N.Vector3.Zero,N.Vector3.Zero,false);for(int i=0;i<12;i++)Tick(N.Vector3.Zero,N.Vector3.Zero,true);}
        internal void Swing(){for(int i=1;i<=10;i++)Tick(new N.Vector3(0,0,i*.025f),new N.Vector3(0,0,i*.025f),true);}
        internal void FastSwing(){for(int i=1;i<=10;i++)Tick(new N.Vector3(0,0,i*.045f),new N.Vector3(0,0,i*.045f),true);}
    }
}
namespace XiiiXR
{
    sealed class PropImpactAudio:IDisposable
    {
        internal static int PropNpc,World;
        internal void Prepare(){}
        internal static int Knocks;internal bool Knock(Vector3 point,float gain,string why){Knocks++;return true;}
        internal static int Bodies;internal void Body(MeleeComponent fists,RaycastHit hit,Vector3 origin){Bodies++;fists.SurfaceHitFX(hit,origin);}
        internal void Surface(MeleeComponent m,Equipable selected,bool held,bool npc,RaycastHit hit,Vector3 origin)
        {if(held&&npc&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental)PropNpc++;if(!npc)World++;m.SurfaceHitFX(hit,origin);}
        public void Dispose(){}
    }
    class InteractionDriver{internal static InteractionDriver? Current=>null;internal bool HandOccupied(bool right)=>false;}
    // 0.1.139: the NPC's skeleton reaction (counted).
    sealed class NpcHitReactions{internal static NpcHitReactions? Current=new();internal int Hits;internal bool Grabbing(bool right)=>false;internal void Hit(PlayMagic.AI.NPC npc,UnityEngine.Collider? c,UnityEngine.Vector3 point,UnityEngine.Vector3 d,float speed,bool held,UnityEngine.Vector3 from){Hits++;}internal static int Guards;internal static float GuardShare;internal static void PunchBegins(PlayMagic.AI.NPC npc,float share){Guards++;GuardShare=share;}internal static string PunchEnds(PlayMagic.AI.NPC npc)=>"";}
    readonly record struct ContactSphere(N.Vector3 Offset,float Radius);
    static class ContactWorld{internal static Vector3 U(N.Vector3 p)=>new(p);}
    // The game's PunchDriver.Touch (not compiled here): an enemy touching the stopped club, as a test sets it.
    internal sealed partial class PunchDriver
    {
        internal static Collider? TestTouch;
        partial void TouchingNpc(ContactSphere[] shape,Vector3 oldPosition,Quaternion oldRotation,Vector3 safePosition,Quaternion safeRotation,Vector3 desiredPosition,Quaternion desiredRotation,Transform root,MeleeComponent melee,ref Touch touch)
        {
            if(TestTouch==null)return;
            touch.Found=true;touch.Hit=new RaycastHit{collider=TestTouch,distance=.2f};touch.From=safePosition;touch.Delta=new Vector3(new N.Vector3(0,0,.2f));touch.Probe=1;
        }
    }
    static class NativeHandMesh{internal static bool Finite(N.Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);}
    readonly record struct PoseValue(N.Vector3 Position,N.Quaternion Rotation);
    struct HandControls{internal const ulong Grip=4;internal bool Valid;internal ulong Held;}
    static class WeaponOptions{internal static Flag PhysicalPunches=new();internal sealed class Flag{internal bool Value=true;}}
    static class Bootstrap{internal static int Warnings;internal static void Warn(string s){Warnings++;}internal static void Write(string s){}}
    static class FriendlyHit{internal static int Checks;internal static bool Check(PlayMagic.AI.NPC? npc,UnityEngine.Transform? root,string how){Checks++;return false;}}
    static class GloveVisual{internal static Quaternion Rotation(PoseValue p,bool right)=>new(p.Rotation);}
    sealed class ContactRig{internal static ContactRig? Current;internal bool ResolveHand(bool r,bool a,ref Vector3 p,ref Quaternion q,bool closedFist=false){p=new Vector3(N.Vector3.Zero);return true;}}
    sealed class WeaponHands{internal int Broken,Worn,WornOnEnemy;internal void BreakHeldProp(Equipable e){Broken++;}internal void WearHeldProp(Equipable e,bool enemy){Worn++;if(enemy)WornOnEnemy++;}internal bool GameMelee;internal bool GameMeleeHand(bool right)=>GameMelee;internal bool Club;internal bool Clubbing(bool right)=>Club;internal bool LongStick=>false;internal bool TryLongStickEnd(out Vector3 end){end=default;return false;}internal bool OffhandOccupied=>false;internal static WeaponHands? Current;internal Func<Vector3> Tip=()=>default;internal bool TryMeleeTip(out Vector3 p){p=Tip();return true;}
        internal Func<Vector3>? Safe;internal Vector3 MeleeSafeTip=>Safe?.Invoke()??Tip();internal bool LeftPistolVisible=>false;internal bool MeleeHand(bool right)=>right;internal bool PunchBlocked(bool right)=>!right&&OffhandOccupied;internal bool TryMeleeTip(bool right,out Vector3 tip)=>TryMeleeTip(out tip);internal Vector3 SafeMeleeTip(bool right)=>MeleeSafeTip;internal ContactSphere[]? Shape;internal ContactSphere[]? MeleeShape=>Shape;internal Func<Vector3>? Other;internal bool TryMeleeOtherEnd(bool right,out Vector3 end){end=Other?.Invoke()??default;return Other!=null;}
        internal bool TryMeleePose(out Vector3 p,out Quaternion q,out Vector3 safe,out Quaternion sr){safe=p=Tip();sr=q=Quaternion.identity;return true;}}
    sealed class CameraRig
    {
        internal Transform PlayerRoot=new();internal bool Valid=true;internal HandControls RightControls,LeftControls=new(){Valid=false};internal N.Vector3 World,Raw;internal int Pulses;
        internal bool SampleWorldHands(out PoseValue l,out PoseValue r,out bool validLeft){l=r=new PoseValue(World,N.Quaternion.Identity);validLeft=false;return Valid;}
        internal bool PhysicalHand(bool right,out N.Vector3 p){p=Raw;return Valid;}
        internal static Vector3 UnityPosition(PoseValue p)=>new(p.Position);internal void PunchHaptics(bool r){Pulses++;}
    }
}
namespace Il2CppInterop.Runtime
{
    static class Il2CppType{internal static Type Of<T>()=>typeof(T);}
    static class Cast{internal static T? TryCast<T>(this object o)where T:class=>o as T;}
}
namespace Il2CppSystem.Collections.Generic{class List<T>:System.Collections.Generic.List<T>{}}
enum DamageType{Melee=16}
class IDamageReceiver{internal bool CanReceiveDamage()=>true;}
class PlayerEquipableInventory
{
    internal void RemoveAndSwitch(Equipable e){}
    internal enum ActiveEquipmentSlot{Fist=20,Pistol=21,Shotgun=24,Enviromental=34,Knife=32}
    internal Equipable? currentEquipable;internal bool isInTransit=false,playerUsedMeleeForce=false;
}
namespace PlayMagic.AI
{
    class NPC{internal bool IsAlive=true,IsConscious=true;internal bool isHeldByPlayer=>false;internal float MaxHealth=240;internal int GetInstanceID()=>1;internal float CurrentHP=>100;internal float GetModifiedDamageByBodyPart(float damage,int area,DamageType type)=>damage;internal int GetColliderArea(UnityEngine.Collider c)=>0;internal bool CanDamage=true;internal string name="test NPC";internal bool CanReceiveDamage()=>CanDamage;internal IDamageReceiver GetDamageReceiver()=>new();}
}
namespace PlayMagic.Weapons
{
    class DestructableWeaponPart{internal void Shatter(Equipable e){}}
    class Equipable
    {
        internal PlayerEquipableInventory.ActiveEquipmentSlot slot=PlayerEquipableInventory.ActiveEquipmentSlot.Fist;internal string identifier="";
        internal bool isEquipableSetUp=true;internal GameObject gameObject=new();internal Transform transform=new();internal MeleeComponent? Melee;
        internal object? GetComponent(Type t)=>Melee;internal int GetInstanceID()=>1;
    }
    class MeleeComponent
    {
        internal Equipable? baseEquipable;internal float MeleeDamage=20,currentMeleeDamageMultiplier=3,baseMeleeDamageMultiplier=1;
        internal float _MeleeDamage_k__BackingField{get=>MeleeDamage;set=>MeleeDamage=value;}internal float LastDamage;
        internal int Hits,ImpactSounds;internal bool Throw;internal Action<Equipable,bool>? OnHit=null;internal Action<RaycastHit,Vector3>? OnSuccessful=null;
        internal PlayerArmsAnimationControl playerArmsAnimationControl=new();
        private static int nextPointer;internal readonly IntPtr Pointer=(IntPtr)System.Threading.Interlocked.Increment(ref nextPointer);
        internal int Comics; internal void SpawnMuzzleFlash(RaycastHit h,Vector3 p){Comics++;}
        internal void SurfaceHitFX(RaycastHit h,Vector3 p){ImpactSounds++;}
        internal bool ValidateIfDamageable(Collider c)=>true;
        internal void ApplyDamageTo(Il2CppSystem.Collections.Generic.List<IDamageReceiver> receivers,IDamageReceiver receiver,RaycastHit h,Vector3 p)
        {if(Throw)throw new InvalidOperationException("test native failure");Hits++;LastDamage=MeleeDamage*currentMeleeDamageMultiplier;receivers.Add(receiver);}
    }
}
namespace UnityEngine
{
    class GameObject{internal bool activeInHierarchy=true;}
    class Transform{internal object[] GetComponentsInChildren(Type t,bool inactive)=>Array.Empty<object>();internal string name="NPC";internal Transform? parent=>Parent;internal Transform? Parent;internal bool IsChildOf(Transform other)=>ReferenceEquals(Parent,other)||ReferenceEquals(this,other);internal Vector3 InverseTransformPoint(Vector3 p)=>p;}
    class BoxCollider:Collider{}
    class SphereCollider:Collider{}
    class CapsuleCollider:Collider{}
    class MeshCollider:Collider{internal bool convex=false;}
    class Collider{internal string name=>"test body";internal bool enabled=true,isTrigger=false;internal T? TryCast<T>() where T:class=>this as T;static int next;readonly int id=++next;internal int GetInstanceID()=>id;internal Transform transform=new();internal PlayMagic.AI.NPC? Npc;internal DamageAction? Damage;internal object? GetComponentInParent(Type t)=>t==typeof(DamageAction)?Damage:t==typeof(PlayMagic.AI.NPC)?Npc:null;internal Vector3 ClosestPoint(Vector3 p)=>p;}
    struct RaycastHit{internal Collider? collider;internal float distance;internal Vector3 point;}
    enum QueryTriggerInteraction{Ignore,Collide}
    static class Physics{internal static Vector3 LastOrigin;internal static Func<Vector3,bool>? Only;internal static Func<Vector3,RaycastHit[]>? PerOrigin;internal static RaycastHit[] Hits=Array.Empty<RaycastHit>();internal static int SphereCastNonAlloc(Vector3 o,float r,Vector3 d,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> buffer,float m,int mask,QueryTriggerInteraction q){LastOrigin=o;if(Only!=null&&!Only(o))return 0;var list=PerOrigin?.Invoke(o)??Hits;int count=Math.Min(buffer.Length,list.Length);for(int i=0;i<count;i++)buffer[i]=list[i];return count;}}
    static class Time{internal static float realtimeSinceStartup;}
    readonly struct Vector3
    {
        internal readonly N.Vector3 N;internal Vector3(N.Vector3 n){N=n;}internal Vector3(float x,float y,float z){N=new(x,y,z);}
        internal float x=>N.X;internal float y=>N.Y;internal float z=>N.Z;
        internal static Vector3 zero=>new(0,0,0);internal static Vector3 forward=>new(0,0,1);internal static float Dot(Vector3 a,Vector3 b)=>System.Numerics.Vector3.Dot(a.N,b.N);internal float magnitude=>N.Length();internal float sqrMagnitude=>N.LengthSquared();
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.N+b.N);public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.N-b.N);public static Vector3 operator/(Vector3 a,float b)=>new(a.N/b);public static Vector3 operator*(Vector3 a,float b)=>new(a.N*b);
    }
    readonly struct Quaternion{internal static Quaternion identity=>new(System.Numerics.Quaternion.Identity);internal static Quaternion Inverse(Quaternion q)=>new(System.Numerics.Quaternion.Inverse(q.N));internal readonly N.Quaternion N;internal Quaternion(N.Quaternion n){N=n;}public static Vector3 operator*(Quaternion q,Vector3 p)=>new(System.Numerics.Vector3.Transform(p.N,q.N));}
}

namespace Il2CppInterop.Runtime.InteropTypes.Arrays{class Il2CppStructArray<T>{private readonly T[] data;internal Il2CppStructArray(int length){data=new T[length];}internal int Length=>data.Length;internal T this[int i]{get=>data[i];set=>data[i]=value;}}}

class PlayerArmsAnimationControl {internal int Swings; internal void PlaySmashSound(){Swings++;}}

class DamageAction:IDamageReceiver{internal bool ignoreNonLethalDamage=true;internal T? TryCast<T>() where T:class=>this as T;}
class DestructableObject{internal DamageAction? healthTracker=>null;}
