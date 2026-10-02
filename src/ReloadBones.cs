using System;
using System.Collections.Generic;
namespace XiiiXR;
// Which bones of a gun's rig are its root, its ammunition (magazine / shell /
// crossbow bolt), its bolt or slide, and the shotgun's tube. Names come from
// the game's rigs (see WEAPON BONES in the log).
// 0.1.117: M16 and crossbow added. The M16's rig has not been seen in a log
// yet, so its names are matched by the usual patterns, and a rig whose root
// is not called wpn_<profile> gets the bone whose name prefixes the most
// others (wpn_harpoon_gun_SH_BND_JNT for the crossbow).
internal static class ReloadBones
{
    internal readonly record struct Found(int Root,int Ammo,int Bolt,int Receiver,int Cover=-1);
    internal static Found Find(string profile,IReadOnlyList<string?> names)
    {
        int root=-1,ammo=-1,bolt=-1,receiver=-1,cover=-1,boltRank=int.MaxValue;
        for(int i=0;i<names.Count;i++)
        {
            var raw=names[i];if(string.IsNullOrEmpty(raw))continue;
            string name=raw.ToLowerInvariant();
            if(name=="wpn_"+profile+"_bnd_jnt"||profile=="sniper"&&name=="wpn_sniper_rifle_sh_bnd_jnt")root=i;
            if(name=="wpn_shotgun_magazine_bnd_jnt")receiver=i;
            if(Ammunition(profile,name))ammo=i;
            if(EquipmentProfile.Lidded(profile)&&Cover(name))cover=i;
            int rank=BoltRank(profile,name);
            if(rank<boltRank){boltRank=rank;bolt=i;}
        }
        // 0.1.194: the double-barrelled shotgun's rig (wpn_shotgun_hunting).
        if(root<0&&(EquipmentProfile.ManualFallback(profile)||profile=="shotgun"))root=CommonRoot(names);
        // 0.1.120: the game's crossbow rig has no root bone: the grip carries the gun.
        if(root<0&&EquipmentProfile.ManualFallback(profile))
            for(int i=0;i<names.Count;i++)if(names[i] is string n&&Ends(n.ToLowerInvariant(),"_grip_bnd_jnt","_grip_sh_bnd_jnt")){root=i;break;}
        return new Found(root,ammo,bolt,receiver,cover);
    }
    // 0.1.119: M60 top cover ("wpn_m60_lid"), not the ammunition box's own lid.
    internal static bool Cover(string name)=>Ends(name,"_lid_bnd_jnt","_lid_sh_bnd_jnt","_cover_bnd_jnt","_cover_sh_bnd_jnt")&&!name.Contains("ammobox",StringComparison.Ordinal)&&!name.Contains("box_lid",StringComparison.Ordinal);
    // M60: the box carries its belt and the rounds on it.
    internal static bool AmmunitionPart(string profile,string name)=>EquipmentProfile.Lidded(profile)
        &&(name.Contains("ammobelt",StringComparison.Ordinal)||name.Contains("ammobox",StringComparison.Ordinal)||name.Contains("_bullet",StringComparison.Ordinal))
        // 0.1.194: the Uzi's magazine base plate and top round go with its magazine (not the spare magazine).
        ||profile=="uzi"&&(name.Contains("magazine_buttom",StringComparison.Ordinal)||name.Contains("_bullet_",StringComparison.Ordinal))&&!name.Contains("extra",StringComparison.Ordinal);
    private static bool Ends(string name,params string[] endings){foreach(var e in endings)if(name.EndsWith(e,StringComparison.Ordinal))return true;return false;}
    internal static bool Ammunition(string profile,string name)=>profile switch
    {
        // 0.1.194: the double-barrelled shotgun's shell for the hand (its reload's own).
        "shotgun"=>Ends(name,"_shell_bnd_jnt","_shell_middle_sh_bnd_jnt","_shell_middle_bnd_jnt"),
        "sniper"=>Ends(name,"_magazine_bnd_jnt","_magazine_sh_bnd_jnt"),
        "m16"=>Ends(name,"_magazine_bnd_jnt","_magazine_sh_bnd_jnt","_mag_bnd_jnt","_mag_sh_bnd_jnt"),
        // 0.1.119: the game's crossbow rig: wpn_crossbow_arrow_a_BND_JNT (not the arrow holder).
        "m60"=>Ends(name,"_ammobox_bnd_jnt","_ammobox_sh_bnd_jnt"),
        "crossbow"=>Ends(name,"_arrow_bnd_jnt","_arrow_sh_bnd_jnt","_bolt_bnd_jnt","_bolt_sh_bnd_jnt")||name.Contains("_arrow_",StringComparison.Ordinal)&&!name.Contains("holder",StringComparison.Ordinal)&&Ends(name,"_bnd_jnt"),
        _=>Ends(name,"_magazine_bnd_jnt")
    };
    // Lower is better; int.MaxValue = not a bolt.
    internal static int BoltRank(string profile,string name)
    {
        switch(profile)
        {
            case "pistol":return Ends(name,"_slide_bnd_jnt")?0:int.MaxValue;
            // 0.1.198: the Uzi's
            // U-shaped top cocking knob is its "aim" bone (wpn_uzi_aim_BND_JNT:
            // the knob, its stem and its slider plate, its mesh shows); "cover"
            // is the small ejection port cover on its right side.
            case "uzi":return Ends(name,"_aim_bnd_jnt","_aim_sh_bnd_jnt")?0:int.MaxValue;
            case "ak47":return Ends(name,"_ejection_port_bnd_jnt")?0:int.MaxValue;
            case "sniper":return Ends(name,"_champer_sh_bnd_jnt")?0:int.MaxValue;
            case "shotgun":return Ends(name,"_forestock_bnd_jnt")?0:int.MaxValue;
            // 0.1.119: M60 charging handle ("wpn_m60_handle"), not the sight's or carry handle.
            case "m60":return Ends(name,"_handle_bnd_jnt","_handle_sh_bnd_jnt")&&!name.Contains("aim",StringComparison.Ordinal)&&!name.Contains("top",StringComparison.Ordinal)?0:int.MaxValue;
            case "m16":
                if(!Ends(name,"_bnd_jnt"))return int.MaxValue;
                // 0.1.119: the game's M16 rig names its charging handle "m16_handle".
                if(name.Contains("charging",StringComparison.Ordinal)||Ends(name,"_handle_bnd_jnt","_handle_sh_bnd_jnt")&&!name.Contains("aim",StringComparison.Ordinal))return 0;
                if(Ends(name,"_bolt_bnd_jnt","_bolt_sh_bnd_jnt"))return 1;
                if(name.Contains("ejection",StringComparison.Ordinal))return 2;
                return int.MaxValue;
            default:return int.MaxValue;
        }
    }
    internal static int CommonRoot(IReadOnlyList<string?> names)
    {
        int best=-1,bestCount=0,bestLength=int.MaxValue;
        for(int i=0;i<names.Count;i++)
        {
            var raw=names[i];if(string.IsNullOrEmpty(raw))continue;string name=raw.ToLowerInvariant();
            string prefix=name.EndsWith("_sh_bnd_jnt",StringComparison.Ordinal)?name[..^11]:name.EndsWith("_bnd_jnt",StringComparison.Ordinal)?name[..^8]:"";
            if(prefix.Length==0)continue;
            int count=0;
            for(int j=0;j<names.Count;j++)if(j!=i&&names[j] is string other&&other.ToLowerInvariant().StartsWith(prefix+"_",StringComparison.Ordinal))count++;
            if(count>bestCount||count==bestCount&&count>0&&prefix.Length<bestLength){best=i;bestCount=count;bestLength=prefix.Length;}
        }
        return best;
    }
}
