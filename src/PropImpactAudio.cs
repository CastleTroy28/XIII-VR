using System;
using Il2CppInterop.Runtime;
using PlayMagic.Weapons;
using UnityEngine;
namespace XiiiXR;
// Only an earned physical contact with an NPC enters this audio override.
// Damage, pain voices, particles and the world's native surface sounds remain
// native. A missing Flesh entry must not turn a prop impact into silence.
internal sealed class PropImpactAudio : IDisposable
{
    private readonly ChairImpactClip chair=new();
    // 0.1.161: a wooden knock for wooden long things (WoodKnock).
    private readonly ChairImpactClip knock=new(()=>WoodKnock.Wav,"wood knock",.85f,16);
    private readonly System.Random pitch=new();private float nextKnock;private bool reportedKnock;
    private readonly Il2CppSystem.Collections.Generic.List<SurfaceDetailsSFX.ParameterToAmount> empty=new();
    private static MeleeComponent? mutedSurface;
    private float nextWarning;
    internal void Prepare()=>chair.Prepare();
    // A knock (strength 0..1.5), a little higher or lower each time; not twice within 60 ms.
    internal bool Knock(Vector3 point,float gain,string why)
    {
        float now=Time.realtimeSinceStartup;if(now<nextKnock)return false;nextKnock=now+.06f;
        bool ok=knock.Play(point,gain,.9f+(float)pitch.NextDouble()*.22f,true);
        if(ok&&!reportedKnock){reportedKnock=true;Bootstrap.Write("PROP WOOD KNOCK played ("+why+"; the game's own broom sound is silent)");}
        return ok;
    }
    internal static bool AllowNativeSound(MeleeComponent source)=>mutedSurface==null||source.Pointer!=mutedSurface.Pointer;
    internal void Surface(MeleeComponent melee,Equipable selected,bool heldObject,bool npc,RaycastHit hit,Vector3 origin)
    {
        bool replace=heldObject&&npc&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental;
        var previous=mutedSurface;
        try
        {
            if(replace)mutedSurface=melee;
            melee.SurfaceHitFX(hit,origin);
        }
        catch(Exception ex){Warn("surface effects: "+ex.Message);}
        finally{mutedSurface=previous;}
        // 0.1.161: a wooden thing knocks - on a wall or on an enemy (louder on the wall).
        bool wooden=heldObject&&selected.slot==PlayerEquipableInventory.ActiveEquipmentSlot.Enviromental&&MeleeDamageMath.WoodenProp(selected.identifier);
        if(wooden&&Knock(hit.point,npc?.8f:1.1f,npc?"a blow on an enemy":"a blow on a wall"))return;
        if(!replace)return;
        try
        {
            if(IsChair(selected.identifier)&&chair.Play(hit.point))return;
            // The damage component can fall back to fists. Sound must still
            // come from the held prop's own component when one exists.
            var sound=selected.GetComponent(Il2CppType.Of<MeleeComponent>())?.TryCast<MeleeComponent>()??melee;
            var table=sound.surfaceHitVisualAudioInfo;
            string path=!string.IsNullOrWhiteSpace(sound.overrideFmodEvent)?sound.overrideFmodEvent:table?.fmodEvent??"";
            // These props have audible world material variants but no body variant.
            // Use the neutral material variant until recordings are chosen.
            var detail=table?.ObtainSFXSurfaceDetails(SurfaceDetection.SurfaceTypes.Generic);
            if(string.IsNullOrWhiteSpace(path))
            {
                // Last-resort body thud from the game's always-used fist event;
                // never substitute a chair recording for bottles or other props.
                path="event:/SFX/WPN/Sfx_FistImpact";
                var fistTable=melee.surfaceHitVisualAudioInfo;
                detail=fistTable?.ObtainSFXSurfaceDetails(SurfaceDetection.SurfaceTypes.Flesh);
            }
            PropStudioSound.Play(path,hit.point,detail?.paramToAmount??empty);
            Bootstrap.Write("PROP NPC SOUND item="+selected.identifier+" event="+path+" surface="+(detail==null?"default":"configured"));
        }
        catch(Exception ex){Warn("NPC sound: "+ex.Message);}
    }
    // 0.1.173: the thud of a blow on a body, from the fists' own surface effects
    // (a gun's or a thing's melee has none for a body); once per blow.
    private float nextBody;private bool reportedBody;
    internal void Body(MeleeComponent fists,RaycastHit hit,Vector3 origin)
    {
        float now=Time.realtimeSinceStartup;if(now<nextBody)return;nextBody=now+.05f;
        try
        {
            fists.SurfaceHitFX(hit,origin);
            if(!reportedBody){reportedBody=true;Bootstrap.Write("PROP BODY SOUND a blow with a gun or a thing on an enemy: the fists' thud and comic picture");}
        }
        catch(Exception ex){Warn("body sound: "+ex.Message);}
    }
    internal static bool IsChair(string? id)=>id!=null&&(id.Equals("wpn_ms_chair",StringComparison.OrdinalIgnoreCase)
        ||id.StartsWith("wpn_ms_chair_",StringComparison.OrdinalIgnoreCase));
    private void Warn(string message)
    {if(Time.realtimeSinceStartup<nextWarning)return;nextWarning=Time.realtimeSinceStartup+5;Bootstrap.Warn("PROP IMPACT "+message);}
    public void Dispose(){chair.Dispose();knock.Dispose();}
}
