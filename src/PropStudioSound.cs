using System;
using FMOD;
using FMODUnity;
using UnityEngine;
namespace XiiiXR;
internal static class PropStudioSound
{
    internal static void Play(string path,Vector3 position,Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> parameters)
    {
        var instance=RuntimeManager.CreateInstance(path);
        try
        {
            Require(instance.set3DAttributes(RuntimeUtils.To3DAttributes(position)),"position");
            foreach(var p in parameters)
            {var result=instance.setParameterByName(p.parameter,p.amount,false);if(result!=RESULT.OK)Bootstrap.Warn("PROP SOUND parameter "+p.parameter+"="+p.amount+" "+result);}
            Require(instance.start(),"start "+path);
        }
        finally{instance.release();}
    }
    private static void Require(RESULT r,string operation){if(r!=RESULT.OK)throw new InvalidOperationException(operation+": "+r);}
}
