using System;using System.Collections.Generic;using XiiiXR;using UnityEngine;using FMOD;
class PropStudioSoundTests {
 static void Check(bool x,string s){if(!x)throw new Exception(s);}
 static void Main(){var p=new Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount>{new(){parameter="Surface",amount=2}};
 PropStudioSound.Play("event:/hit",new(1,2,3),p);Check(State.Started==1&&State.Released==1&&State.Position.z==3&&State.Value==2,"spatial event not started/released");
 State.BadParameter=true;PropStudioSound.Play("event:/hit",new(),p);Check(State.Started==2&&State.Released==2,"missing parameter stops sound");
 State.BadPosition=true;bool threw=false;try{PropStudioSound.Play("event:/hit",new(),p);}catch(InvalidOperationException){threw=true;}Check(threw&&State.Started==2&&State.Released==3,"position failure unreported/leaks event");
 State.BadPosition=false;State.BadStart=true;threw=false;try{PropStudioSound.Play("event:/hit",new(),p);}catch(InvalidOperationException){threw=true;}Check(threw&&State.Released==4,"start error unreported/leaks event");
 Console.WriteLine("PASS: actual FMOD event adapter positions and starts once, tolerates missing parameters, reports start/position errors, always releases.");}
}
class State{internal static int Started,Released;internal static Vector3 Position;internal static float Value;internal static bool BadParameter,BadPosition,BadStart;}
class SurfaceDetailsSFX{internal class ParameterToAmount{internal string parameter="";internal float amount;}}
namespace UnityEngine{internal record struct Vector3(float x,float y,float z);}
namespace Il2CppSystem.Collections.Generic{class List<T>:System.Collections.Generic.List<T>{}}
namespace FMOD{enum RESULT{OK,ERR};}
namespace FMOD.Studio{class EventInstance{internal RESULT set3DAttributes(Vector3 p){State.Position=p;return State.BadPosition?RESULT.ERR:RESULT.OK;}internal RESULT setParameterByName(string n,float v,bool i){State.Value=v;return State.BadParameter?RESULT.ERR:RESULT.OK;}internal RESULT start(){if(State.BadStart)return RESULT.ERR;State.Started++;return RESULT.OK;}internal RESULT release(){State.Released++;return RESULT.OK;}}}
namespace FMODUnity{static class RuntimeManager{internal static FMOD.Studio.EventInstance CreateInstance(string p)=>new();}static class RuntimeUtils{internal static Vector3 To3DAttributes(Vector3 p)=>p;}}
namespace XiiiXR{static class Bootstrap{internal static void Warn(string s){}}}
