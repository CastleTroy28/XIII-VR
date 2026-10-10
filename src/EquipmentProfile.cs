namespace XiiiXR;
// Native inventory slot IDs; naming is diagnostic, not the binding criterion.
internal static class EquipmentProfile
{
 internal static string ForSlot(int slot)=>slot switch {
  20=>"",21=>"pistol",22=>"revolver",23=>"uzi",24=>"shotgun",25=>"m16",26=>"ak47",
  27=>"sniper",28=>"crossbow",29=>"m60",30=>"bazooka",31=>"grenade",32=>"knife",34=>"prop",
  9 or 10=>"",11 or 12 or 13=>"key",14 or 15 or 16 or 33=>"gadget",17=>"mounted",40=>"rifle",41=>"heavy",_=>"equipment"};
 // 0.1.117: the M16 (magazine, like the AK) and the crossbow (one bolt at a
 // time along its rail, no rack) are reloaded by hand too.
 // 0.1.194: the Uzi: its magazine into the grip, then its top cocking knob back.
 internal static bool Manual(string profile)=>profile is "pistol" or "ak47" or "shotgun" or "sniper" or "m16" or "crossbow" or "m60" or "uzi";
 // 0.1.194: guns whose magazine goes in the grip: struck against the chest
 // with the other hand full, a full magazine goes in (the pistol, the Uzi).
 internal static bool ChestMagazine(string profile)=>profile is "pistol" or "uzi";
 // 0.1.119: M60 — top cover opened with B, ammunition box + belt by hand,
 // cover closed by hand, then the charging handle.
 internal static bool Lidded(string profile)=>profile=="m60";
 internal static bool Arrow(string profile)=>profile=="crossbow";
 // Guns with a VR telescopic sight (picture-in-picture lens, R3 zoom).
 internal static bool Scoped(string profile)=>profile is "sniper" or "crossbow";
 // Single rounds pushed in one at a time: shotgun shells, crossbow bolts.
 internal static bool SingleRound(string profile)=>profile is "shotgun" or "crossbow";
 // The newer manual guns fall back to the game's own reload if their
 // ammunition cannot be separated from the gun's mesh.
 internal static bool ManualFallback(string profile)=>profile is "m16" or "crossbow" or "m60" or "uzi";
 // SVD: detachable box magazine and side charging handle, handled like the AK.
 internal static bool RifleMagazine(string profile)=>profile is "ak47" or "sniper" or "m16";
 // 0.1.117: guns whose collision comes from their mesh cells (the four
 // original manual guns and the revolver keep their hand-made shapes).
 internal static bool TightContact(string p)=>RequiresMuzzle(p)&&p is not ("pistol" or "ak47" or "shotgun" or "sniper" or "revolver");
 internal static bool RequiresMuzzle(string p)=>p is "pistol" or "revolver" or "uzi" or "shotgun" or "m16" or "ak47" or "sniper" or "crossbow" or "m60" or "bazooka" or "rifle" or "heavy";
 // 0.1.243: the revolver 37 cm (was 28): the game's own revolver against its
 // hand, so the hand holding it is drawn at the free hand's size (it was 0.74
 // of it; the pistol's 0.98, the rifles' about 1).
 // 0.1.244: the Uzi 35 cm (was 46): the hand holding it was 1.29 of the free
 // hand's size, big beside the pistol in the other hand.
 // 0.1.249: the shotguns 1.08 m (were 0.95): the hand on the pump gun was 0.87
 // of the free hand's size, on the double-barrel 0.85; now about 0.98 and 0.97.
 // 0.1.250: the game's three crossbows share its crossbow slot, each drawn at
 // its own length (CrossbowLength); the plain crossbow 89 cm (was 72).
 // 0.1.257: the AK, the M16 and the crossbows times Growth (below).
 internal static float Length(string p)=>(p switch {"pistol"=>.22f,"revolver"=>.37f,"uzi"=>.35f,"shotgun"=>1.08f,"m16"=>.99f,"ak47"=>.85f,"sniper"=>1.1f,"crossbow"=>CrossbowLength(null),"m60"=>1.1f,"bazooka"=>1.1f,"knife"=>.16f,"grenade"=>.12f,"key"=>.12f,"gadget"=>.22f,_=>.65f})*Growth(p);
 // The length of the model the game holds (its mesh or its name): the
 // crossbows' only differs.
 internal static float Length(string p,string? model)=>p=="crossbow"?CrossbowLength(model)*Growth(p):Length(p);
 // 0.1.257: the AK (85 cm before), the M16 (99 cm) and the three crossbows
 // looked small: they are drawn 10% larger than the game draws them against
 // its own hand (the lengths above and CrossbowLength, times this). The hand
 // holding them stays the size it was, its fist where it was on the grip
 // (GrownGrip): the hand does not grow as one is taken.
 internal const float RifleGrowth=1.10f;
 internal static float Growth(string p)=>p is "ak47" or "m16" or "crossbow"?RifleGrowth:1;
 // 0.1.250: all 72 cm before, so the hand holding the harpoon gun was drawn at
 // 0.69 of the free hand's size, on the crossbow 0.79 and on the tactical one
 // 0.77 (the pistol's 0.98, the rifles' about 1). Now the harpoon gun 1.03 m,
 // the tactical crossbow 92 cm, the crossbow 89 cm: the hand about 0.98 on each.
 // 0.1.257: drawn 10% longer still (Growth), the hand kept its size.
 internal const float HarpoonLength=1.03f,TacticalCrossbowLength=.92f,PlainCrossbowLength=.89f;
 internal static float CrossbowLength(string? model)=>CrossbowModel(model) switch{"harpoon_gun"=>HarpoonLength,"crossbow_tactical"=>TacticalCrossbowLength,_=>PlainCrossbowLength};
 // 0.1.251: which of the three crossbows a model is (its name): its own saved
 // bolt and string, and its size.
 internal static string CrossbowModel(string? model)
 {
  if(model!=null&&model.Contains("harpoon",System.StringComparison.OrdinalIgnoreCase))return "harpoon_gun";
  if(model!=null&&model.Contains("tactical",System.StringComparison.OrdinalIgnoreCase))return "crossbow_tactical";
  return "crossbow";
 }
 // A model's own name for the files the mod keeps for it (the profile's, but a crossbow's own).
 internal static string ModelKey(string profile,string? model)=>profile=="crossbow"?CrossbowModel(model):profile;
 // 0.1.251: the crossbows' telescopic sights were found in their meshes
 // (ScopeGeometry) at this length; drawn longer (0.1.250), the search is made
 // at this size and its result grown back, so it finds what it found.
 internal const float ScopeTunedLength=.72f;
}
