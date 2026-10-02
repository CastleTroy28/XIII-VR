using System;
using UnityEngine;
using Q=System.Numerics.Quaternion;
namespace XiiiXR;
internal sealed class ContactRig : IDisposable
{
    internal static ContactRig? Current;
    private readonly CameraRig rig;
    private readonly ContactWorld world=new();
    private readonly ContactSolver gun=new(),leftGun=new();
    private ContactSphere[]? leftGunShape;
    private readonly ContactSolver[] hands={new(),new()};
    private ContactSphere[]? weaponOnlyShape;
    private ContactSphere[] gunShape=Array.Empty<ContactSphere>();
    private string profile="";
    private ContactSphere[]? sourceShape;
    private bool supporting;
    private ContactPose leftPose;
    private bool leftSolid;
    private readonly int[] handFrame={-100,-100};
    private readonly ContactPose[] handPose=new ContactPose[2];
    private readonly ContactSphere[]?[] handShape=new ContactSphere[]?[2];
    private readonly ContactPose[] carryFrom=new ContactPose[2];private readonly bool[] carryValid=new bool[2];
    private readonly ContactPose[] steady=new ContactPose[2];private readonly bool[] steadyValid=new bool[2];private readonly int[] steadyFrame={-1,-1};
    // 0.1.174: the steadying
    // on what a thing rests on let go and took hold again at its 2 cm edge
    // every frame or two; now it takes hold within 2 cm and lets go only past
    // 4.5 cm. And the two hands no longer push each other in turn: only the
    // other hand (the left one for a right-hander) yields to the main one, and
    // resting on it, it is steadied on it too.
    internal const float SteadyOn=.02f,SteadyOff=.045f;
    private readonly ContactPose[] handSteady=new ContactPose[2];private readonly bool[] handSteadyValid=new bool[2];private readonly int[] handSteadyFrame={-1,-1};
    private int ammunitionRevision=-1;
    private string ammunitionProfile="";private ContactSphere[]? ammunitionShape,ammunitionShapeRight,ammunitionShapeRightFrom;
    private bool failed;
    private float nextContactReport;
    internal bool GunSafe=>!QualityOptions.Collisions.Value||(!failed&&gun.Safe);
    // 0.1.146: a still copy held in a hand collides with the world and with
    // the gun in the other hand (the game's weapon or the other copy), and
    // the two knock against each other with a dull metallic sound.
    private readonly ContactSolver[] copySolvers={new(),new()};
    private readonly ContactSphere[]?[] copyShapes=new ContactSphere[]?[2];
    private readonly ContactPose[] copyPoses=new ContactPose[2];
    private readonly int[] copyFrame={-100,-100};private readonly string[] copyProfiles={"",""};
    private int gunFrame=-100;
    // 0.1.200: the collider that last stopped the game's weapon, and when; how far it was held off.
    private Collider? gunObstacle;private int gunObstacleFrame=-100;private float gunGap;
    internal float GunGap=>gunGap;
    internal bool TryGunObstacle(int frames,out Collider? obstacle)
    {
        obstacle=null;
        if(gunObstacle==null||Time.frameCount-gunObstacleFrame>frames)return false;
        try{if(!gunObstacle.enabled)return false;}catch(Exception){gunObstacle=null;return false;}
        obstacle=gunObstacle;return true;
    }
    private readonly ContactPose[] copySteady=new ContactPose[2];private readonly bool[] copySteadyValid=new bool[2];private readonly int[] copySteadyFrame={-1,-1};
    private readonly GunKnock knock=new();
    internal ContactRig(CameraRig camera){rig=camera;Current=this;}
    private bool Ready()
    {
        var player=rig.PlayerRoot;world.IgnoredRoot=GripCarry.Current?.BodyRoot;
        if(world.Player!=player){world.Player=player;ResetGun();ResetLeftGun();Array.Clear(handShape,0,2);foreach(var hand in hands)hand.Reset();failed=false;}
        return !failed&&QualityOptions.Collisions.Value&&player!=null&&!rig.Scripted;
    }
    private ContactPose Seed(Q rotation)=>new(ContactWorld.V(rig.HeadPosition)-System.Numerics.Vector3.UnitY*.18f,rotation);
    internal bool ResolveGun(string type,Vector3 primaryGrip,Vector3 supportGrip,bool support,ref Vector3 p,ref Quaternion q,ContactSphere[]? modelShape=null)
    {
        if(!QualityOptions.Collisions.Value){ResetGun();return true;}
        if(!Ready()){world.WeaponShape=null;return !failed;}
        try
        {
            if(profile!=type||supporting!=support||sourceShape!=modelShape)
            {
                if(profile!=type)gun.Reset();profile=type;supporting=support;
                sourceShape=modelShape;var weapon=modelShape??ContactSolver.Weapon(type);weaponOnlyShape=weapon;gunShape=new ContactSphere[weapon.Length+(support?2:1)];Array.Copy(weapon,gunShape,weapon.Length);
            }
            // Attached palms move with the stopped weapon, so their envelopes
            // participate in the SAME sweep instead of separating from the grip.
            gunShape[gunShape.Length-(support?2:1)]=new ContactSphere(ContactWorld.V(primaryGrip),type=="pistol"?.032f:.055f);
            if(support)gunShape[gunShape.Length-1]=new ContactSphere(ContactWorld.V(supportGrip),.055f);
            world.IncludeWeapon=false;
            // Dual pistols may touch/cross each other; keep their world sweeps.
            // 0.1.120: the free left hand (and the hook in it) no longer pushes
            // the held gun, nor the gun the hand: the two pushed each other in
            // turn every frame, and touching a gun to reload shook the gun or
            // the hand ("CONTACT weapon ... obstacle=virtual"). Only a second
            // gun in the left hand still counts.
            world.PeerShape=WeaponHands.Current?.DualActive==true?null:leftSolid?leftGunShape:null;
            world.PeerPose=leftPose;
            // 0.1.147: the game's weapon
            // is not stopped by a copy in the other hand - only the copy yields
            // to it (one way; the two pushed each other in turn every frame).
            var desired=new ContactPose(ContactWorld.V(p),new Q(q.x,q.y,q.z,q.w));
            world.LastObstacle=null;
            var result=gun.Solve(desired,Seed(desired.Rotation),gunShape,world);
            // 0.1.200: what stopped the weapon (an enemy's body: a club swung against it hits it).
            if(gun.Blocked&&world.LastObstacle!=null){gunObstacle=world.LastObstacle;gunObstacleFrame=Time.frameCount;}
            gunGap=System.Numerics.Vector3.Distance(desired.Position,result.Position);
            Report("weapon",gun,desired,result);
            p=ContactWorld.U(result.Position);q=new Quaternion(result.Rotation.X,result.Rotation.Y,result.Rotation.Z,result.Rotation.W);
            world.WeaponPose=result;world.WeaponShape=gun.Safe?weaponOnlyShape:null;gunFrame=Time.frameCount;
            if(gun.Safe&&weaponOnlyShape!=null)knock.Moved(2,result,Time.realtimeSinceStartup);
            KnockCheck();
            return gun.Safe;
        }
        catch(Exception ex){Fail(ex);return false;}
        finally{world.PeerShape=null;}
    }
    // A still copy in hand s (root pose: its fitted origin), against the
    // world and the other hand's gun.
    internal bool ResolveCopy(int s,string profile,ContactSphere[]? shape,ref Vector3 p,ref Quaternion q)
    {
        if(!QualityOptions.Collisions.Value||shape==null||shape.Length==0){ResetCopy(s);return true;}
        if(!Ready())return !failed;
        long timer=FramePerformance.Begin();
        try
        {
            world.IncludeWeapon=false;world.Access=false;world.PeerShape=null;
            int o=1-s;
            // The copy yields to the game's weapon; of two copies the left one
            // yields to the right one (never both: they shook).
            if(gunFrame>=Time.frameCount-1&&world.WeaponShape!=null){world.PeerShape=world.WeaponShape;world.PeerPose=world.WeaponPose;}
            else if(s==0&&copyFrame[o]>=Time.frameCount-1&&copyShapes[o]!=null){world.PeerShape=copyShapes[o];world.PeerPose=copyPoses[o];}
            var desired=new ContactPose(ContactWorld.V(p),new Q(q.x,q.y,q.z,q.w));
            var solver=copySolvers[s];
            // Its first pose is swept out from the body: not against the other gun.
            if(!solver.Ready)world.PeerShape=null;
            var peer=world.PeerShape;var peerPose=world.PeerPose;
            var result=solver.Solve(desired,Seed(desired.Rotation),shape,world);
            // 0.1.147: resting against the other gun it is steadied on it (it
            // follows that gun's motion exactly; millimetre jitter filtered),
            // like a hand resting on the held gun.
            if(peer!=null&&solver.Safe&&ContactSolver.Near(shape,result,peer,peerPose,copySteadyValid[s]?SteadyOff:SteadyOn))
            {
                float dt=copySteadyFrame[s]==Time.frameCount?0:Time.unscaledDeltaTime;copySteadyFrame[s]=Time.frameCount;
                result=ContactSolver.Steady(peerPose,result,ref copySteady[s],ref copySteadyValid[s],dt);
            }
            else copySteadyValid[s]=false;
            Report(s==0?"left copy":"right copy",solver,desired,result);
            p=ContactWorld.U(result.Position);q=new Quaternion(result.Rotation.X,result.Rotation.Y,result.Rotation.Z,result.Rotation.W);
            copyShapes[s]=solver.Safe?shape:null;copyPoses[s]=result;copyFrame[s]=Time.frameCount;copyProfiles[s]=profile;
            if(solver.Safe)knock.Moved(s,result,Time.realtimeSinceStartup);
            KnockCheck();
            return solver.Safe;
        }
        catch(Exception ex){Fail(ex);return false;}
        finally{world.PeerShape=null;FramePerformance.End(timer,0);}
    }
    internal void ResetCopy(int s){copySolvers[s].Reset();copyShapes[s]=null;copyFrame[s]=-100;copySteadyValid[s]=false;knock.Forget(s);}
    // The two held guns (a copy and the game's weapon, or two copies)
    // touching now and not a moment ago: a knock, louder the faster they met.
    private void KnockCheck()
    {
        int frame=Time.frameCount;
        bool c0=copyFrame[0]>=frame-1&&copyShapes[0]!=null,c1=copyFrame[1]>=frame-1&&copyShapes[1]!=null,g=gunFrame>=frame-1&&world.WeaponShape!=null&&WeaponHands.Current?.DualActive!=true;
        ContactSphere[]? a=null,b=null;ContactPose pa=default,pb=default;int ia=-1,ib=-1;string na="",nb="";
        if(c0&&c1){a=copyShapes[0];pa=copyPoses[0];ia=0;na=copyProfiles[0];b=copyShapes[1];pb=copyPoses[1];ib=1;nb=copyProfiles[1];}
        else if((c0||c1)&&g){int c=c0?0:1;a=copyShapes[c];pa=copyPoses[c];ia=c;na=copyProfiles[c];b=world.WeaponShape;pb=world.WeaponPose;ib=2;nb=WeaponHands.Current?.Profile??"";}
        if(a==null||b==null){knock.Apart();return;}
        bool touching=ContactSolver.Touching(a,pa,b,pb,knock.Touching?GunKnock.Release:GunKnock.Contact,out var point);
        float speed=knock.Step(touching,ia,ib,point,Time.realtimeSinceStartup);
        if(speed>0)
        {
            WeaponImpactAudio.Current?.Knock(ContactWorld.U(point),speed,na,nb);
            rig.ResistanceHaptics(Math.Clamp(speed/GunKnock.FullSpeed,.25f,.8f),true);rig.ResistanceHaptics(Math.Clamp(speed/GunKnock.FullSpeed,.25f,.8f),false);
            if(knock.Reports++<20)Bootstrap.Write("GUN KNOCK "+na+" against "+nb+" at "+speed.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" m/s");
        }
    }
    internal bool ResolveLeftGun(ref Vector3 p,ref Quaternion q,ContactSphere[]? shape)
    {
        if(!QualityOptions.Collisions.Value){leftGun.Reset();return true;}
        if(!Ready())return !failed;
        leftGunShape=shape??leftGunShape??ContactSolver.Weapon("pistol");
        world.IncludeWeapon=false;world.Access=false;world.PeerShape=null;
        var desired=new ContactPose(ContactWorld.V(p),new Q(q.x,q.y,q.z,q.w));
        var result=leftGun.Solve(desired,Seed(desired.Rotation),leftGunShape,world);
        p=ContactWorld.U(result.Position);q=new Quaternion(result.Rotation.X,result.Rotation.Y,result.Rotation.Z,result.Rotation.W);leftPose=result;leftSolid=leftGun.Safe;world.IncludeWeapon=false;return leftGun.Safe;
    }
    internal void ResetLeftGun(){leftGun.Reset();leftSolid=false;}
    internal bool ResolveHand(bool right,bool attached,ref Vector3 p,ref Quaternion q,bool closedFist=false)
    {
        int index=right?1:0;
        if(!QualityOptions.Collisions.Value){hands[index].Reset();return true;}
        // 0.1.128: a hand holding a still copy does not depend on the game weapon's collision.
        if(attached){handShape[index]=null;hands[index].Reset();return WeaponHands.Current?.CopyInHand(right)==true||(!right&&WeaponHands.Current?.LeftPistolVisible==true?leftGun.Safe:GunSafe);}
        if(!Ready())return !failed;
        long timer=FramePerformance.Begin();
        try
        {
            // Reload access uses the magazine/bolt targets; the coarse whole-gun
            // envelope would otherwise block a hand before reaching either.
            world.IgnoredRoot=InteractionDriver.Current?.HeldRoot(right)??BodyGrabState.Root(right)??GripCarry.Current?.BodyRoot;
            // 0.1.117: a hand laying a long crossbow bolt along the rail must
            // not be pushed off the crossbow's own stock.
            // 0.1.119: during a hand reload of a gun whose collision is its mesh
            // cells (M16, crossbow, M60), the left hand is not pushed off the
            // gun: the installed magazine's cells stayed solid and blocked the
            // new one ("invisible collision").
            // 0.1.120: the gun is not pushed by this hand (see ResolveGun).
            // 0.1.122: the free left hand collides with the held gun again (one
            // way: it stops at the gun, the gun does not move away), except
            // during a hand reload of a mesh-cell gun (the magazine's cells
            // blocked the new one). While touching, the hand is carried by
            // the gun's motion, so it rests on the gun instead of shaking.
            // 0.1.127: not against a grenade or knife in the right hand (its box kept
            // the left hand from the grenade's pin).
            // 0.1.128: the hand that is free of the game's weapon (the right hand when the left holds it).
            world.IncludeWeapon=(WeaponHands.Current?.FreeHandCollides(right)??!right)&&WeaponHands.Current?.ReloadContactFree!=true&&WeaponHands.Current?.Profile is not ("grenade" or "knife")&&world.WeaponShape!=null;world.Access=false;
            world.AccessRadius=(WeaponHands.Current?.Profile is "ak47" or "sniper" or "m16") ? .09f : .050f;
            var weapons=WeaponHands.Current;
            // 0.1.133: the reloading hand (the right one for a gun in the left hand).
            if(weapons!=null&&right==weapons.ReloadRight&&weapons.TryReloadAccess(p,q,out var access,out var axis)){world.Access=true;world.AccessPoint=ContactWorld.V(access);world.AccessAxis=ContactWorld.V(axis);world.AccessAmmunition=weapons.ReloadHandHolding(right);}
            var shape=closedFist?ContactSolver.Fist:ContactSolver.Hand;
            // 0.1.92: the grappling hook in the left hand is part of that hand's shape.
            // 0.1.195: in the hand holding it (either one).
            if(GrappleVr.Current?.Side==(right?1:0)&&GrappleVr.Current.TryHandShape(out var hookSpheres))
            {var merged=new ContactSphere[shape.Length+hookSpheres.Length];Array.Copy(shape,merged,shape.Length);Array.Copy(hookSpheres,0,merged,shape.Length,hookSpheres.Length);shape=merged;}
            if(weapons?.ReloadHandHolding(right)==true)
            {
                var geometry=ReloadGripGeometry.Get(weapons.Profile);
                if(geometry!=null)
                {
                    if(ammunitionShape==null||ammunitionProfile!=weapons.Profile||ammunitionRevision!=ReloadGripGeometry.Revision)
                    {
                        ammunitionProfile=weapons.Profile;ammunitionRevision=ReloadGripGeometry.Revision;
                        var a=geometry.Min;var b=geometry.Max;var center=(a+b)*.5f;
                        var direction=geometry.Forward; // thumb-side for magazines, forward for shell
                        bool alongX=Math.Abs(direction.X)>.8f;
                        float extent=alongX?b.X-a.X:b.Z-a.Z;
                        float radius=Math.Max(.01f,Math.Max(b.Y-a.Y,alongX?b.Z-a.Z:b.X-a.X)*.5f);
                        int count=Math.Clamp((int)Math.Ceiling(extent/Math.Max(.016f,radius)),1,14);
                        // Folded hand plus narrow ammunition chain; no giant
                        // sphere enclosing the full length of an AK magazine.
                        ammunitionShape=new ContactSphere[2+count];
                        ammunitionShape[0]=new ContactSphere(new System.Numerics.Vector3(0,0,-.038f),.029f);
                        ammunitionShape[1]=new ContactSphere(new System.Numerics.Vector3(0,-.008f,.005f),.027f);
                        for(int i=0;i<count;i++)
                        {
                            var offset=center+direction*(extent*((i+.5f)/count-.5f));
                            ammunitionShape[2+i]=new ContactSphere(offset,radius,true);
                        }
                        if(geometry.Sections.Count>0)
                        {
                            var parts=new System.Collections.Generic.List<ContactSphere>{ammunitionShape[0],ammunitionShape[1]};
                            foreach(var section in geometry.Sections)
                            {
                                var extent3=section.max-section.min;var middle=(section.min+section.max)*.5f;
                                // Partition along depth so a curved AK magazine does not
                                // become a single oversized sphere around empty space.
                                float r=Math.Max(.006f,Math.Max(extent3.X,extent3.Y)*.5f);
                                int slices=Math.Clamp((int)Math.Ceiling(extent3.Z/r),1,24);
                                for(int j=0;j<slices;j++)parts.Add(new ContactSphere(middle+System.Numerics.Vector3.UnitZ*(extent3.Z*((j+.5f)/slices-.5f)),MathF.Sqrt(r*r+MathF.Pow(extent3.Z/slices*.5f,2)),true));
                            }
                            ammunitionShape=parts.ToArray();
                        }
                    }
                    // 0.1.133: held in the right hand: the left hand's shape mirrored.
                    if(right)
                    {
                        if(ammunitionShapeRight==null||ammunitionShapeRight.Length!=ammunitionShape.Length||!ReferenceEquals(ammunitionShapeRightFrom,ammunitionShape))
                        {
                            ammunitionShapeRight=new ContactSphere[ammunitionShape.Length];ammunitionShapeRightFrom=ammunitionShape;
                            for(int i=0;i<ammunitionShape.Length;i++){var c=ammunitionShape[i];ammunitionShapeRight[i]=c with{Offset=new System.Numerics.Vector3(-c.Offset.X,c.Offset.Y,c.Offset.Z)};}
                        }
                        shape=ammunitionShapeRight;
                    }
                    else shape=ammunitionShape;
                }
            }
            var desired=new ContactPose(ContactWorld.V(p),new Q(q.x,q.y,q.z,q.w));
            if(world.IncludeWeapon&&world.WeaponShape!=null)
            {
                if(carryValid[index]&&hands[index].Ready&&ContactSolver.Near(shape,hands[index].Previous,world.WeaponShape,carryFrom[index],.03f))
                    hands[index].Carry(carryFrom[index],world.WeaponPose);
                carryFrom[index]=world.WeaponPose;carryValid[index]=true;
            }
            else carryValid[index]=false;
            // 0.1.174: only the other hand yields to the main one (never both in turn).
            bool yieldsToHand=right==WeaponHands.LeftHanded;
            world.PeerShape=yieldsToHand&&handFrame[1-index]>=Time.frameCount-1?handShape[1-index]:null;world.PeerPose=handPose[1-index];
            var handPeer=world.PeerShape;var handPeerPose=world.PeerPose;
            if(right&&leftSolid){world.PeerShape=leftGunShape;world.PeerPose=leftPose;}
            else if(GrappleVr.Current?.Side==(right?0:1)&&world.PeerShape==null&&GrappleVr.Current.TryDeviceShape(out var hookShape,out var hookPose)){world.PeerShape=hookShape;world.PeerPose=hookPose;}
            var result=hands[index].Solve(desired,Seed(desired.Rotation),shape,world);
            // 0.1.124: resting on the held gun, the hand is steadied on it.
            if(world.IncludeWeapon&&world.WeaponShape!=null&&hands[index].Safe&&ContactSolver.Near(shape,result,world.WeaponShape,world.WeaponPose,steadyValid[index]?SteadyOff:SteadyOn))
            {
                float dt=steadyFrame[index]==Time.frameCount?0:Time.unscaledDeltaTime;steadyFrame[index]=Time.frameCount;
                result=ContactSolver.Steady(world.WeaponPose,result,ref steady[index],ref steadyValid[index],dt);
                handSteadyValid[index]=false;
            }
            else
            {
                steadyValid[index]=false;
                // 0.1.174: resting on the other hand (or the gun in it), steadied on it.
                if(handPeer!=null&&hands[index].Safe&&ContactSolver.Near(shape,result,handPeer,handPeerPose,handSteadyValid[index]?SteadyOff:SteadyOn))
                {
                    float dt=handSteadyFrame[index]==Time.frameCount?0:Time.unscaledDeltaTime;handSteadyFrame[index]=Time.frameCount;
                    result=ContactSolver.Steady(handPeerPose,result,ref handSteady[index],ref handSteadyValid[index],dt);
                }
                else handSteadyValid[index]=false;
            }
            Report(right?"right":"left",hands[index],desired,result);
            p=ContactWorld.U(result.Position);q=new Quaternion(result.Rotation.X,result.Rotation.Y,result.Rotation.Z,result.Rotation.W);
            handFrame[index]=Time.frameCount;handPose[index]=result;handShape[index]=hands[index].Safe?shape:null;return hands[index].Safe;
        }
        catch(Exception ex){Fail(ex);return false;}
        finally{world.IncludeWeapon=false;world.PeerShape=null;world.Access=false;FramePerformance.End(timer,0);}
    }
    internal void ResetGun(){gun.Reset();profile="";sourceShape=null;world.WeaponShape=null;gunFrame=-100;knock.Forget(2);}
    internal void ResetHand(bool right){int i=right?1:0;hands[i].Reset();handShape[i]=null;carryValid[i]=false;steadyValid[i]=false;handSteadyValid[i]=false;}
    private void Report(string part,ContactSolver solver,ContactPose desired,ContactPose result)
    {
        float gap=System.Numerics.Vector3.Distance(desired.Position,result.Position);
        if(Time.realtimeSinceStartup<nextContactReport||(!solver.Recovered&&(gap<.12f||!solver.Blocked)))return;
        nextContactReport=Time.realtimeSinceStartup+3;
        Bootstrap.Write("CONTACT "+part+" gap="+gap.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" recovered="+solver.Recovered+" safe="+solver.Safe+" obstacle="+(world.LastObstacle==null?"virtual/unknown":world.LastObstacle.name));
    }
    private void Fail(Exception ex){failed=true;world.WeaponShape=null;Bootstrap.Warn("COLLISION query failed; affected visual hidden until rebind or collisions disabled: "+ex);}
    public void Dispose(){world.Dispose();if(Current==this)Current=null;}
}
