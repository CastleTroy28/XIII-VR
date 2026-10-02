using System;
namespace XiiiXR;
// 0.1.137: which sound a weapon (or a dropped magazine) makes landing on a
// surface. surface: the game's SurfaceDetection.SurfaceTypes value (-1:
// unknown); roofed: a ceiling above the spot (indoors).
internal enum DropSound{None,Room,Metal,Glass,Carpet,Ground,Soft,Water}
internal static class WeaponImpactMath
{
    internal const float MinSpeed=.6f,FullSpeed=4.5f,MinGap=.08f;
    internal static DropSound Pick(int surface,bool roofed,bool terrain)
    {
        switch(surface)
        {
            case 8:return DropSound.Water;
            case 9:case 10:case 11:case 14:case 15:case 17:case 38:case 39:case 41:case 42:return DropSound.Soft;
            case 16:case 19:case 33:case 34:case 35:case 36:return DropSound.Carpet;
            case 4:case 5:case 6:case 20:case 40:return DropSound.Metal;
            case 7:return DropSound.Glass;
            // Wooden floors and decks: the gun's clatter wherever they are.
            case 2:case 3:return DropSound.Room;
        }
        if(surface>=21&&surface<=32)return DropSound.Metal;
        // Hard (concrete, rock, tile, generic) or unknown: indoors the gun's
        // clatter; outside a duller knock (open ground, snow levels).
        if(terrain&&surface<0)return DropSound.Soft;
        return roofed&&!terrain?DropSound.Room:DropSound.Ground;
    }
    // Louder with the landing speed (m/s, towards the surface); 0: no sound.
    internal static float Volume(float speed)
    {
        if(!float.IsFinite(speed)||speed<MinSpeed)return 0;
        return Math.Clamp(.25f+.75f*(speed-MinSpeed)/(FullSpeed-MinSpeed),.25f,1);
    }
    // The recording is a small pistol: long guns lower and a little louder,
    // a magazine higher and quieter.
    internal static (float gain,float pitch) Weight(string profile)=>profile switch
    {
        "pistol" or "revolver" or "uzi"=>(1f,1f),
        "magazine"=>(.45f,1.45f),
        "m60" or "bazooka" or "harpoon" or "heavy"=>(1.15f,.72f),
        "grenade" or "knife"=>(.6f,1.3f),
        _=>(1.1f,.85f)
    };
    internal static float Pitch(DropSound kind)=>kind==DropSound.Metal?1.18f:kind==DropSound.Glass?1.35f:1;
}
