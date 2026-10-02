// Production MuzzleEffects.cs with simulated Unity/native effect objects.
using System;using System.Reflection;using XiiiXR;using UnityEngine;using PlayMagic.Weapons;
class MuzzleEffectsTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object? Call(string method,params object?[] args)=>typeof(MuzzleEffects).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
    static object Begin(VFXComponent fx)
    {object?[] args={fx,null};Call("Begin",args);return args[1]!;}
    static void Spawn(VFXComponent fx,object state,ParticleSystem ps)
    {
        object?[] args={fx,fx.Original,false,null};Call("BeginParticle",args);
        Check(ReferenceEquals(args[1],WeaponHands.Current!.Anchor)&&(bool)args[2]!,"native particle origin/parent not redirected");
        Check(ReferenceEquals(args[3],state),"particle scope missing");Call("EndParticle",ps,args[3]);
    }
    static VFXComponent Make(Equipable weapon)=>new(){baseEquipable=weapon,muzzleTransform=new Transform(),muzzlePositionAfterIK=new Vector3(99,98,97),muzzleRotationAfterIK=new Quaternion(1,2,3,4)};
    static void Main()
    {
        using var module=new MuzzleEffects();
        var weapon=new Equipable();var hand=new WeaponHands {Weapon=weapon,Anchor=new Transform {position=new Vector3(1,2,3),rotation=new Quaternion(0,0,0,1)}};WeaponHands.Current=hand;
        var fx=Make(weapon);fx.Original=fx.muzzleTransform;var savedP=fx.muzzlePositionAfterIK;var savedQ=fx.muzzleRotationAfterIK;
        var state=Begin(fx);Check(state!=null&&fx.muzzleTransform==hand.Anchor&&fx.muzzlePositionAfterIK.Equals(hand.Anchor.position),"flash cache not redirected before spawn");
        var particle=new ParticleSystem();Spawn(fx,state!,particle);
        // Simulate native ShowMuzzleFlash's later reparent to desktop body.
        particle.transform.SetParent(new Transform(),false);particle.transform.position=new Vector3(20,20,20);
        Call("End",state);
        Check(particle.transform.parent==hand.Anchor&&particle.transform.position.Equals(hand.Anchor.position),"native post-spawn body reparent not corrected");
        Check(fx.muzzleTransform==fx.Original&&fx.muzzlePositionAfterIK.Equals(savedP)&&fx.muzzleRotationAfterIK.Equals(savedQ),"original flash references/cache not restored");
        Check(hand.Shown==1,"native flash notification missing");Call("FinalizeFlash",null,state);Check(hand.Shown==1,"finalizer duplicates flash");
        Check(fx.Original.position.Equals(default(Vector3)),"source weapon bone moved");
        module.Cancel();Check(particle.Stops==1&&particle.transform.parent==null&&!particle.Destroyed,"pool particle not released intact");
        var error=new Exception("simulated native error");state=Begin(fx);
        Check(ReferenceEquals(Call("FinalizeFlash",error,state),error)&&fx.muzzleTransform==fx.Original,"exception path loses exception or cache restore");
        var other=Make(new Equipable());object?[] ignored={other,null};Call("Begin",ignored);Check(ignored[1]==null,"NPC or unselected weapon was intercepted");
        hand.Allowed=false;ignored=new object?[]{fx,null};Call("Begin",ignored);Check(ignored[1]==null,"paused/untracked weapon was intercepted");hand.Allowed=true;
        state=Begin(fx);particle=new ParticleSystem();Spawn(fx,state,particle);Call("End",state);
        // Pool reuses an instance for another owner: do not move/stop it.
        var newOwner=new Transform();particle.transform.SetParent(newOwner,true);module.Cancel();Check(particle.Stops==0&&particle.transform.parent==newOwner,"reused particle was stopped");
        state=Begin(fx);particle=new ParticleSystem();Spawn(fx,state,particle);Call("End",state);particle.Alive=false;module.Tick();
        Check(particle.transform.parent==null&&particle.Stops==0,"expired pool instance was left under disposable weapon");
        state=Begin(fx);particle=new ParticleSystem();Spawn(fx,state,particle);Call("End",state);particle.Destroyed=true;module.Tick();
        module.Dispose();
        Console.WriteLine("PASS: production native-flash scope; origin/cache routing; post-native reparent; source-bone preservation; exactly-once finalization; exceptions; NPC/disabled exclusion; pool reuse/expiry/cancel/destroyed cleanup.");
        Console.WriteLine("Simulated particle/native APIs; no Unity particle rendering or in-game screenshot verification.");
    }
}
namespace UnityEngine
{
    public class Object
    {
        static int serial;public IntPtr Pointer=(IntPtr)(++serial);public string name="test";public bool Destroyed;
        public static bool operator ==(Object? a,Object? b){bool an=ReferenceEquals(a,null)||a.Destroyed,bn=ReferenceEquals(b,null)||b.Destroyed;return an||bn?an==bn:ReferenceEquals(a,b);}
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? other)=>ReferenceEquals(this,other);public override int GetHashCode()=>Pointer.GetHashCode();
    }
    public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
    public struct Quaternion {public float x,y,z,w;public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
    public class Transform:Object {public Transform? parent;public Vector3 position;public Quaternion rotation;public void SetParent(Transform? p,bool keep){parent=p;}public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}}
    public enum ParticleSystemStopBehavior {StopEmittingAndClear}
    public class ParticleSystem:Object {public Transform transform=new();public bool Alive=true;public int Stops;public bool IsAlive(bool children)=>Alive;public void Stop(bool children,ParticleSystemStopBehavior mode){Alive=false;Stops++;}}
    public static class Time {public static float realtimeSinceStartup=1;}
}
namespace PlayMagic.Weapons
{
    public class Equipable:UnityEngine.Object {}
    public class VFXComponent:UnityEngine.Object
    {
        public Equipable baseEquipable=null!;public Transform muzzleTransform=null!,Original=null!;public Vector3 muzzlePositionAfterIK;public Quaternion muzzleRotationAfterIK;
        public void ShowMuzzleFlash(Equipable equipable){}public ParticleSystem SpawnParticle(object particles,Transform spawnTransform,int camLayer,bool attachParticleToSpawnTransform)=>new();
    }
}
namespace HarmonyLib
{
    public sealed class Harmony {public Harmony(string id){}public void UnpatchSelf(){}public void Patch(MethodInfo method,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null,HarmonyMethod? finalizer=null){if(method==null)throw new Exception("missing native method");}}
    public sealed class HarmonyMethod {public HarmonyMethod(Type type,string name){}}
    public static class AccessTools {public static MethodInfo DeclaredMethod(Type type,string name)=>type.GetMethod(name)!;}
}
namespace XiiiXR
{
    internal class WeaponHands {internal static WeaponHands? Current;internal Equipable Weapon=null!;internal Transform Anchor=null!;internal bool Allowed=true;internal int Shown;internal bool TryGetEffectMuzzle(Equipable weapon,out Transform anchor){anchor=Anchor;return Allowed&&weapon==Weapon;}internal void NativeFlashShown(Transform anchor){Shown++;}}
    internal static class Bootstrap {internal static void Write(string text){}internal static void Warn(string text){}}
}
