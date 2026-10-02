using System;using XiiiXR;
class ReloadBonesTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static string[] L(string s)=>s.Split(',');
 static void Main()
 {
  // Rigs as the game logged them (WEAPON BONES).
  var ak=L("wpn_ak47_ejection_port_BND_JNT,wpn_ak47_ejection_port_zero_JNT,wpn_ak47_bullet_BND_JNT,wpn_ak47_magazine_BND_JNT,wpn_ak47_magazine_zero_JNT,wpn_ak47_magazine_release_BND_JNT,wpn_ak47_trigger_BND_JNT,wpn_ak47_BND_JNT");
  var f=ReloadBones.Find("ak47",ak);Check(ak[f.Root]=="wpn_ak47_BND_JNT"&&ak[f.Ammo]=="wpn_ak47_magazine_BND_JNT"&&ak[f.Bolt]=="wpn_ak47_ejection_port_BND_JNT","AK rig: "+f);
  var svd=L("wpn_sniper_bullets_SH_BND_JNT,wpn_sniper_rifle_champer_SH_BND_JNT,wpn_sniper_rifle_knob_side_SH_BND_JNT,wpn_sniper_rifle_magazine_SH_BND_JNT,wpn_sniper_rifle_SH_BND_JNT");
  f=ReloadBones.Find("sniper",svd);Check(svd[f.Root]=="wpn_sniper_rifle_SH_BND_JNT"&&svd[f.Ammo]=="wpn_sniper_rifle_magazine_SH_BND_JNT"&&svd[f.Bolt]=="wpn_sniper_rifle_champer_SH_BND_JNT","SVD rig: "+f);
  var sg=L("wpn_shotgun_trigger_BND_JNT,wpn_shotgun_shell_BND_JNT,wpn_shotgun_shell_zero_JNT,wpn_shotgun_magazine_BND_JNT,wpn_shotgun_forestock_BND_JNT,wpn_shotgun_BND_JNT");
  f=ReloadBones.Find("shotgun",sg);Check(sg[f.Root]=="wpn_shotgun_BND_JNT"&&sg[f.Ammo]=="wpn_shotgun_shell_BND_JNT"&&sg[f.Bolt]=="wpn_shotgun_forestock_BND_JNT"&&sg[f.Receiver]=="wpn_shotgun_magazine_BND_JNT","shotgun rig: "+f);
  var pistol=L("wpn_pistol_BND_JNT,wpn_pistol_magazine_BND_JNT,wpn_pistol_magazine_top_BND_JNT,wpn_pistol_slide_BND_JNT,wpn_pistol_magazineRelease_BND_JNT,wpn_pistol_slideStop_BND_JNT");
  f=ReloadBones.Find("pistol",pistol);Check(pistol[f.Root]=="wpn_pistol_BND_JNT"&&pistol[f.Ammo]=="wpn_pistol_magazine_BND_JNT"&&pistol[f.Bolt]=="wpn_pistol_slide_BND_JNT","pistol rig: "+f);
  // 0.1.117: crossbow (harpoon gun) — the bolt is the "arrow"; root by common prefix.
  var xb=L("wpn_harpoon_gun_SH_BND_JNT,wpn_harpoon_gun_arrow_SH_BND_JNT,wpn_harpoon_gun_line_left_a_SH_BND_JNT,wpn_harpoon_gun_line_right_a_SH_BND_JNT,wpn_harpoon_gun_trigger_SH_BND_JNT,wpn_harpoon_line_left_crv_a_SH_BND_JNT");
  f=ReloadBones.Find("crossbow",xb);Check(xb[f.Root]=="wpn_harpoon_gun_SH_BND_JNT"&&xb[f.Ammo]=="wpn_harpoon_gun_arrow_SH_BND_JNT"&&f.Bolt<0,"crossbow rig: "+f);
  // 0.1.119: the game's own crossbow and M16 rigs (log of 0.1.117).
  var cb=L("wpn_crossbow_arrow_a_BND_JNT,wpn_crossbow_arrow_holder_BND_JNT,wpn_crossbow_front_aim_BND_JNT,wpn_crossbow_grip_BND_JNT,wpn_crossbow_scope_adjust_BND_JNT,wpn_crossbow_string_a_BND_JNT,wpn_crossbow_trigger_BND_JNT");
  f=ReloadBones.Find("crossbow",cb);Check(cb[f.Ammo]=="wpn_crossbow_arrow_a_BND_JNT"&&f.Bolt<0&&cb[f.Root]=="wpn_crossbow_grip_BND_JNT","game crossbow rig (bolt held relative to the grip): "+f);
  var m16real=L("wpn_m16_wrist_SH_JNT,wpn_m16_grip_adjust_JNT,wpn_m16_BND_JNT,m16_trigger_BND_JNT,m16_boltRelease_BND_JNT,m16_aimRear_BND_JNT,m16_handleZero_JNT,m16_handle_BND_JNT,m16_handle_end_JNT,m16_handleLock_BND_JNT,m16_port_BND_JNT,m16_boltZero_JNT,m16_bolt_BND_JNT,m16_mag_BND_JNT,m16_bullets_BND_JNT,m16_magRelease_BND_JNT,m16_grenade_BND_JNT");
  f=ReloadBones.Find("m16",m16real);Check(m16real[f.Root]=="wpn_m16_BND_JNT"&&m16real[f.Ammo]=="m16_mag_BND_JNT"&&m16real[f.Bolt]=="m16_handle_BND_JNT","game M16 rig (charging handle): "+f);
  Check(MechanismMath.Matches("m16_handle_BND_JNT","m16")&&MechanismMath.Matches("m16_bolt_BND_JNT","m16")&&!MechanismMath.Matches("m16_boltRelease_BND_JNT","m16")&&!MechanismMath.Matches("m16_mag_BND_JNT","m16"),"M16 mechanism: handle and bolt move, release lever and magazine do not");
  var m60=L("wpn_m60_ammoBelt_start_JNT,wpn_m60_bullet_a_BND_JNT,wpn_m60_ammoBelt_i_BND_JNT,wpn_m60_ammoBox_lid_BND_JNT,wpn_m60_ammoBox_BND_JNT,wpn_m60_ammoBox_zero_JNT,wpn_m60_aim_handle_BND_JNT,wpn_m60_handle_BND_JNT,wpn_m60_handle_zero_JNT,wpn_m60_lid_BND_JNT,wpn_m60_handle_top_BND_JNT,wpn_m60_trigger_BND_JNT,wpn_m60_BND_JNT");
  f=ReloadBones.Find("m60",m60);
  Check(m60[f.Root]=="wpn_m60_BND_JNT"&&m60[f.Ammo]=="wpn_m60_ammoBox_BND_JNT"&&m60[f.Bolt]=="wpn_m60_handle_BND_JNT"&&m60[f.Cover]=="wpn_m60_lid_BND_JNT","game M60 rig: "+f);
  Check(ReloadBones.AmmunitionPart("m60","wpn_m60_ammobelt_i_bnd_jnt")&&ReloadBones.AmmunitionPart("m60","wpn_m60_bullet_a_bnd_jnt")&&!ReloadBones.AmmunitionPart("m60","wpn_m60_lid_bnd_jnt")&&!ReloadBones.AmmunitionPart("ak47","wpn_ak47_bullet_bnd_jnt"),"M60 box carries belt and rounds");
  Check(MechanismMath.Matches("wpn_m60_handle_BND_JNT","m60")&&!MechanismMath.Matches("wpn_m60_handle_top_BND_JNT","m60")&&!MechanismMath.Matches("wpn_m60_lid_BND_JNT","m60"),"M60 mechanism is the charging handle");
  Check(EquipmentProfile.Manual("m60")&&EquipmentProfile.Lidded("m60")&&EquipmentProfile.ManualFallback("m60")&&!EquipmentProfile.Lidded("m16"),"M60 profile rules");
  // M16 (rig not yet seen): usual names, charging handle preferred over the ejection port.
  var m16=L("wpn_m16_ejection_port_BND_JNT,wpn_m16_magazine_BND_JNT,wpn_m16_magazine_release_BND_JNT,wpn_m16_charging_handle_BND_JNT,wpn_m16_trigger_BND_JNT,wpn_m16_BND_JNT");
  f=ReloadBones.Find("m16",m16);Check(m16[f.Root]=="wpn_m16_BND_JNT"&&m16[f.Ammo]=="wpn_m16_magazine_BND_JNT"&&m16[f.Bolt]=="wpn_m16_charging_handle_BND_JNT","M16 rig: "+f);
  var m4=L("wpn_m4a1_SH_BND_JNT,wpn_m4a1_mag_SH_BND_JNT,wpn_m4a1_bolt_SH_BND_JNT,wpn_m4a1_trigger_SH_BND_JNT");
  f=ReloadBones.Find("m16",m4);Check(m4[f.Root]=="wpn_m4a1_SH_BND_JNT"&&m4[f.Ammo]=="wpn_m4a1_mag_SH_BND_JNT"&&m4[f.Bolt]=="wpn_m4a1_bolt_SH_BND_JNT","M4-style rig: "+f);
  // Older guns keep their exact behaviour: no guessed root.
  f=ReloadBones.Find("ak47",L("wpn_akm_BND_JNT,wpn_akm_magazine_BND_JNT"));Check(f.Root<0,"guessed root for an original manual gun");
  f=ReloadBones.Find("crossbow",new string?[]{null,"","x"});Check(f.Root<0&&f.Ammo<0&&f.Bolt<0,"empty rig");
  // 0.1.117 profile rules.
  Check(EquipmentProfile.Manual("m16")&&EquipmentProfile.Manual("crossbow")&&EquipmentProfile.RifleMagazine("m16")&&!EquipmentProfile.RifleMagazine("crossbow"),"M16/crossbow not reloaded by hand");
  Check(EquipmentProfile.Arrow("crossbow")&&EquipmentProfile.SingleRound("crossbow")&&EquipmentProfile.SingleRound("shotgun")&&!EquipmentProfile.SingleRound("m16"),"single-round rules");
  Check(EquipmentProfile.ManualFallback("m16")&&EquipmentProfile.ManualFallback("crossbow")&&!EquipmentProfile.ManualFallback("ak47")&&!EquipmentProfile.ManualFallback("shotgun"),"fallback only for the new manual guns");
  Check(EquipmentProfile.TightContact("crossbow")&&EquipmentProfile.TightContact("m16")&&EquipmentProfile.TightContact("uzi")&&!EquipmentProfile.TightContact("ak47")&&!EquipmentProfile.TightContact("revolver")&&!EquipmentProfile.TightContact("grenade")&&!EquipmentProfile.TightContact("prop"),"mesh-cell collision profiles");
  Check(EquipmentProfile.Scoped("crossbow")&&EquipmentProfile.Scoped("sniper")&&!EquipmentProfile.Scoped("m16"),"scoped profiles");
  // 0.1.194: the game's Uzi rig (log of 0.1.192): the magazine (not the spare one), the top cocking knob.
  var uzi=L("wpn_uzi_wrist_SH_JNT,wpn_uzi_grip_adjust_JNT,wpn_uzi_BND_JNT,wpn_uzi_trigger_BND_JNT,wpn_uzi_magazineRelease_BND_JNT,wpn_uzi_strapHolder_BND_JNT,wpn_uzi_shoulderSupport_BND_JNT,wpn_uzi_aim_zero_JNT,wpn_uzi_aim_BND_JNT,wpn_uzi_cover_BND_JNT,wpn_uzi_magazine_zero_JNT,wpn_uzi_magazine_BND_JNT,wpn_uzi_bullet_BND_JNT,wpn_uzi_magazine_buttom_BND_JNT,wpn_uzi_selecter_zero_JNT,wpn_uzi_selecter_BND_JNT,wpn_uzi_safety_zero_JNT,wpn_uzi_safety_BND_JNT,wpn_uzi_rearAim_BND_JNT,wpn_uzi_extraMagazine_zero_JNT,wpn_uzi_extraMagazine_BND_JNT");
  f=ReloadBones.Find("uzi",uzi);Check(uzi[f.Root]=="wpn_uzi_BND_JNT"&&uzi[f.Ammo]=="wpn_uzi_magazine_BND_JNT"&&uzi[f.Bolt]=="wpn_uzi_aim_BND_JNT"&&f.Cover<0,"Uzi rig: "+f);
  Check(ReloadBones.AmmunitionPart("uzi","wpn_uzi_magazine_buttom_bnd_jnt")&&ReloadBones.AmmunitionPart("uzi","wpn_uzi_bullet_bnd_jnt")&&!ReloadBones.AmmunitionPart("uzi","wpn_uzi_extramagazine_bnd_jnt")&&!ReloadBones.AmmunitionPart("pistol","wpn_pistol_bullet_a_bnd_jnt"),"Uzi magazine parts");
  Check(EquipmentProfile.Manual("uzi")&&EquipmentProfile.ManualFallback("uzi")&&!EquipmentProfile.RifleMagazine("uzi")&&!EquipmentProfile.SingleRound("uzi"),"Uzi reload rules");
  Console.WriteLine("PASS: 0.1.194 the Uzi's magazine and top cocking knob; reload bones: AK/SVD/shotgun/pistol rigs as before; crossbow bolt (arrow) and root by common prefix; M16/M4 magazine and charging handle by pattern.");
 }
}
