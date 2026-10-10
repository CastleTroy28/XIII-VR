using System;using System.Linq;using System.Numerics;using XiiiXR;
class WeaponAttachmentTests
{
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Main()
 {
  // 0.1.194: the silenced pistol is fitted as the plain one (the silencer left out).
  Check(WeaponGeometry.Attachment("wpn_pistol_silencer_BND_JNT")&&WeaponGeometry.Attachment("Suppressor")&&!WeaponGeometry.Attachment("wpn_pistol_slide_BND_JNT")&&!WeaponGeometry.Attachment(null),"silencer bones by name");
  var rnd=new Random(5);
  // A pistol: slide/frame box (x .049, y .040, z .079), the silencer a tube ahead of it.
  Vector3[] Gun(bool shown,out bool[] attached)
  {
   var p=new System.Collections.Generic.List<Vector3>();var a=new System.Collections.Generic.List<bool>();
   for(int i=0;i<600;i++){p.Add(new Vector3((float)rnd.NextDouble()*.049f-.0245f,(float)rnd.NextDouble()*.040f-.030f,(float)rnd.NextDouble()*.079f-.079f));a.Add(false);}
   for(int i=0;i<300;i++){var q=shown?new Vector3((float)rnd.NextDouble()*.012f-.006f+.004f,(float)rnd.NextDouble()*.012f-.006f,(float)rnd.NextDouble()*.022f):new Vector3(0,0,-.002f);p.Add(q);a.Add(true);}
   attached=a.ToArray();return p.ToArray();
  }
  var plain=Gun(false,out var pa);var silenced=Gun(true,out var sa);
  Check(!WeaponGeometry.WithoutAttachment(plain,pa,.10f,out _,out _,out _,out _),"the plain pistol (silencer hidden, a point) trimmed");
  Check(WeaponGeometry.WithoutAttachment(silenced,sa,.10f,out var min,out var max,out var amin,out var amax),"the silenced pistol not fitted without its silencer");
  var size=max-min;
  Check(Math.Abs(size.X-.049f)<.002f&&Math.Abs(size.Y-.040f)<.002f&&Math.Abs(size.Z-.079f)<.002f,"the silenced pistol's fitted size is not the plain one's: "+size);
  var a=WeaponGeometry.Fit(min,max,"pistol");var b=WeaponGeometry.Fit(new Vector3(-.0245f,-.030f,-.079f),new Vector3(.0245f,.010f,0),"pistol");
  Check(Math.Abs(a.Scale-b.Scale)/b.Scale<.03f&&Vector3.Distance(a.Translation,b.Translation)<.004f,"the silenced pistol lies other than the plain one in the hand");
  Check(amax.Z>max.Z+.015f,"the silencer is not ahead of the muzzle");
  Check(!WeaponGeometry.WithoutAttachment(new Vector3[0],new bool[0],.1f,out _,out _,out _,out _)&&!WeaponGeometry.WithoutAttachment(plain,new bool[3],.1f,out _,out _,out _,out _),"bad input trimmed");
  Console.WriteLine("PASS: 0.1.194 the silenced pistol fitted as the plain one (same size and handle), the silencer ahead; the plain pistol's hidden silencer not taken as shown.");
  // 0.1.228: the bazooka fitted without its rocket at its usual scale: the same
  // size and handle wherever the game holds the rocket (the tube 0.263 long in
  // the barrel frame; its rocket's head 13.9 cm ahead of it, or 4.4 cm).
  {
   Vector3[] Bazooka(float ahead,out bool[] rocket)
   {
    var p=new System.Collections.Generic.List<Vector3>();var a=new System.Collections.Generic.List<bool>();
    for(int i=0;i<800;i++){p.Add(new Vector3((float)rnd.NextDouble()*.08f-.04f,(float)rnd.NextDouble()*.135f-.115f,(float)rnd.NextDouble()*.263f-.263f));a.Add(false);}
    for(int i=0;i<300;i++){p.Add(new Vector3((float)rnd.NextDouble()*.03f-.015f,(float)rnd.NextDouble()*.03f-.015f,ahead-(float)rnd.NextDouble()*.27f));a.Add(true);}
    rocket=a.ToArray();return p.ToArray();
   }
   var far=Bazooka(.139f,out var fr);var near=Bazooka(.044f,out var nr);
   bool trimmedFar=WeaponGeometry.WithoutAttachment(far,fr,0,out var fmin,out var fmax,out _,out _);bool trimmedNear=WeaponGeometry.WithoutAttachment(near,nr,0,out var nmin,out var nmax,out _,out _);
   Check(trimmedFar&&trimmedNear,"the bazooka not fitted without its rocket");
   var f1=WeaponGeometry.Fit(fmin,fmax,"bazooka",WeaponGeometry.BazookaScale);var f2=WeaponGeometry.Fit(nmin,nmax,"bazooka",WeaponGeometry.BazookaScale);
   var grip=new Vector3(0,-.06f,-.06f);
   Check(f1.Scale==f2.Scale&&MathF.Abs(f1.Scale-1.1f/.402614f)<1e-4f&&Vector3.Distance(f1.Point(grip),f2.Point(grip))<.006f,"the bazooka's handle not at one place and size whatever its rocket's place");
   // Fitted by its length with the rocket in, the second came out a third bigger.
   static Vector3 Min(Vector3[] p)=>new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z));static Vector3 Max(Vector3[] p)=>new(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z));
   var old1=WeaponGeometry.Fit(Min(far),Max(far),"bazooka");var old2=WeaponGeometry.Fit(Min(near),Max(near),"bazooka");
   Check(old2.Scale/old1.Scale>1.25f,"the old fit was not rocket-dependent: "+old1.Scale+" "+old2.Scale);
   Check(MathF.Abs(WeaponGeometry.Fit(fmin,fmax,"bazooka").Scale-1.1f/(fmax.Z-fmin.Z))<1e-4f,"the fit by length changed");
  }
  Console.WriteLine("PASS: 0.1.228 the bazooka fitted without its rocket at its usual scale: one size and handle place wherever the game holds its rocket (fitted by its length, a second one came out a third bigger).");
  // 0.1.243: the plain pistol's hidden silencer is shrunk by the game to a
  // point outside the gun (ahead of it and to its side): left in the fit, the
  // gun's bounds came out 29% longer (0.079 for 0.0614 in the game's size)
  // and the gun was drawn 17 cm long instead of 22, the hand on it at 0.76.
  {
   Vector3[] Pistol(bool shown,out bool[] attached)
   {
    var p=new System.Collections.Generic.List<Vector3>();var a=new System.Collections.Generic.List<bool>();
    for(int i=0;i<600;i++){p.Add(new Vector3((float)rnd.NextDouble()*.0395f-.0295f,(float)rnd.NextDouble()*.0384f-.0284f,(float)rnd.NextDouble()*.0614f-.0614f));a.Add(false);}
    for(int i=0;i<300;i++){var q=shown?new Vector3((float)rnd.NextDouble()*.01f-.005f,(float)rnd.NextDouble()*.01f-.005f,.001f+(float)rnd.NextDouble()*.035f):new Vector3(.0195f,.0118f,.0176f);p.Add(q);a.Add(true);}
    attached=a.ToArray();return p.ToArray();
   }
   var plainP=Pistol(false,out var ppa);var silencedP=Pistol(true,out var spa);
   static Vector3 Lo(Vector3[] p)=>new(p.Min(v=>v.X),p.Min(v=>v.Y),p.Min(v=>v.Z));static Vector3 Hi(Vector3[] p)=>new(p.Max(v=>v.X),p.Max(v=>v.Y),p.Max(v=>v.Z));
   var whole=Hi(plainP)-Lo(plainP);
   Check(Math.Abs(whole.Z-.0790f)<.0015f&&Math.Abs(whole.X-.049f)<.0015f,"the plain pistol's whole bounds not as the game's (0.049 x 0.079): "+whole);
   Check(WeaponGeometry.Split(plainP,ppa,out var pmin,out var pmax,out var pamin,out var pamax)&&!WeaponGeometry.Shown(pmin,pmax,pamin,pamax,.10f),"the plain pistol's hidden silencer not found, or taken as shown");
   Check(WeaponGeometry.Split(silencedP,spa,out var smin,out var smax,out var samin,out var samax)&&WeaponGeometry.Shown(smin,smax,samin,samax,.10f),"the silenced pistol's silencer not taken as shown");
   var plainFit=WeaponGeometry.Fit(pmin,pmax,"pistol");var silencedFit=WeaponGeometry.Fit(smin,smax,"pistol");var oldFit=WeaponGeometry.Fit(Lo(plainP),Hi(plainP),"pistol");
   Check(Math.Abs(plainFit.Scale-silencedFit.Scale)/silencedFit.Scale<.02f&&Vector3.Distance(plainFit.Translation,silencedFit.Translation)<.004f,"the plain pistol drawn other than the silenced one");
   Check(oldFit.Scale/plainFit.Scale<.8f,"the old fit (its hidden silencer in it) was not the smaller one: "+oldFit.Scale+" "+plainFit.Scale);
   Check(Math.Abs((plainFit.Point(pmax)-plainFit.Point(pmin)).Z-.22f)<.002f,"the plain pistol not drawn 22 cm long");
   // The game draws its hand at one size against all its guns (the hand
   // measured on them: 0.2729 of a real hand per unit of the game's size, the
   // same on each), so the hand holding a gun is drawn at the length fitted
   // over the gun's size times that. The rawest sizes logged by the game:
   const float hand=.272901f;
   // 0.1.257: a gun drawn larger than that (EquipmentProfile.Growth): the hand on it kept at the size it had.
   float Held(string profile,float rawLength)=>EquipmentProfile.Length(profile)/EquipmentProfile.Growth(profile)/rawLength*hand;
   float pistol=Held("pistol",.061423f),revolver=Held("revolver",.103633f),ak=Held("ak47",.239861f);
   Check(pistol>.95f&&pistol<1.03f&&revolver>.95f&&revolver<1.03f,"a hand holding a handgun not at the free hand's size: pistol "+pistol+", revolver "+revolver);
   Check(Math.Abs(pistol-ak)<.03f&&Math.Abs(revolver-ak)<.03f,"the handguns' hand not as the AK's: "+pistol+" "+revolver+" "+ak);
   Check(Held("pistol",.079002f)<.78f&&WeaponGeometry.RevolverTunedLength/.103633f*hand<.75f,"the old sizes (the hand at 0.76 and 0.74) not reproduced");
   Check(MathF.Abs(WeaponGeometry.RevolverGrowth-.37f/.28f)<1e-4f,"the revolver's hand-made offsets do not grow with it");
   // 0.1.244: the Uzi (0.097 long in the game's size) was fitted 46 cm long:
   // the hand on it at 1.29 of the free hand's size, big beside the pistol. 35 cm.
   float uzi=Held("uzi",.097155f);
   Check(uzi>.95f&&uzi<1.03f&&Math.Abs(uzi-pistol)<.03f,"the hand on the Uzi not as the pistol's: "+uzi+" "+pistol);
   Check(.46f/.097155f*hand>1.28f,"the old Uzi size (the hand at 1.29) not reproduced");
   // 0.1.249: the shotguns (the pump gun 0.2993 long in the game's size, the
   // double-barrel laid along its barrels 0.3044) were fitted 0.95 m: the hand
   // on them at 0.87 and 0.85. 1.08 m.
   float pump=Held("shotgun",.299275f),both=Held("shotgun",.304414f);
   Check(pump>.95f&&pump<1.03f&&both>.95f&&both<1.03f&&Math.Abs(pump-ak)<.03f&&Math.Abs(both-pistol)<.03f,"the hand on a shotgun not as on the other guns: pump "+pump+", double-barrel "+both);
   Check(.95f/.304414f*hand<.86f&&.95f/.299275f*hand<.87f,"the old shotgun sizes (the hand at 0.85 and 0.87) not reproduced");
   // 0.1.250: the three crossbows of the game's crossbow slot, all fitted 72
   // cm: the hand on the harpoon gun (0.2857 long) at 0.69, on the crossbow
   // (0.2489) at 0.79, on the tactical one (0.2562) at 0.77. Each its own length.
   float Model(string model,float rawLength)=>EquipmentProfile.Length("crossbow",model)/EquipmentProfile.Growth("crossbow")/rawLength*hand;
   float harpoon=Model("wpn_harpoon_gun wpn_harpoon_gun(Clone) wpn_harpoon_gun_LOD0",.285673f),crossbow=Model("wpn_crossbow wpn_crossbow(Clone) wpn_crossbow_LOD0",.248898f),tactical=Model("wpn_crossbow_tactical wpn_crossbow_tactical_LOD0",.256173f);
   foreach(var (name,heldSize) in new[]{("harpoon gun",harpoon),("crossbow",crossbow),("tactical crossbow",tactical)})
    Check(heldSize>.95f&&heldSize<1.03f&&Math.Abs(heldSize-pistol)<.03f&&Math.Abs(heldSize-ak)<.03f,"the hand on the "+name+" not as on the other guns: "+heldSize);
   Check(.72f/.285673f*hand<.70f&&.72f/.248898f*hand<.80f&&.72f/.256173f*hand<.78f,"the old crossbow sizes (the hand at 0.69, 0.79, 0.77) not reproduced");
   Check(EquipmentProfile.Length("crossbow")==EquipmentProfile.Length("crossbow",null)&&EquipmentProfile.Length("crossbow","WPN_HARPOON_GUN")==EquipmentProfile.HarpoonLength*EquipmentProfile.Growth("crossbow"),"an unnamed crossbow or a harpoon gun named otherwise not sized");
   // 0.1.251: each crossbow's own key (its saved bolt and string files; the crossbow's as before).
   Check(EquipmentProfile.ModelKey("crossbow","wpn_harpoon_gun wpn_harpoon_gun(Clone) wpn_harpoon_gun_LOD0")=="harpoon_gun"&&EquipmentProfile.ModelKey("crossbow","wpn_crossbow_tactical")=="crossbow_tactical"
    &&EquipmentProfile.ModelKey("crossbow","wpn_crossbow wpn_crossbow_LOD0")=="crossbow"&&EquipmentProfile.ModelKey("crossbow",null)=="crossbow"&&EquipmentProfile.ModelKey("uzi","wpn_uzi")=="uzi","a crossbow's own key wrong");
   Check(EquipmentProfile.ScopeTunedLength==.72f&&EquipmentProfile.TacticalCrossbowLength/EquipmentProfile.ScopeTunedLength>1.2f,"the sights' search not at the size it was measured at");
   Check(EquipmentProfile.Length("shotgun","wpn_harpoon_gun")==EquipmentProfile.Length("shotgun")&&EquipmentProfile.Length("pistol","tactical")==EquipmentProfile.Length("pistol"),"another gun sized by its model's name");
   var harpoonFit=WeaponGeometry.Fit(new Vector3(-.01f,-.02f,-.2857f),new Vector3(.01f,.02f,0),"crossbow",float.NaN,"wpn_harpoon_gun_LOD0");
   Check(MathF.Abs(harpoonFit.Scale*.2857f-EquipmentProfile.HarpoonLength*EquipmentProfile.RifleGrowth)<1e-4f,"the harpoon gun not fitted at its own length");
   // 0.1.257: the AK, the M16 and the three crossbows drawn 10% larger than the game draws them against its hand
   // (they looked small); the other guns as they were; the hand holding a grown gun kept at the size it had, its
   // fist where it was on the grip.
   Check(EquipmentProfile.RifleGrowth==1.10f&&MathF.Abs(EquipmentProfile.Length("ak47")-.935f)<1e-4f&&MathF.Abs(EquipmentProfile.Length("m16")-1.089f)<1e-4f
    &&MathF.Abs(EquipmentProfile.Length("crossbow","wpn_harpoon_gun")-1.133f)<1e-4f&&MathF.Abs(EquipmentProfile.Length("crossbow","wpn_crossbow_tactical")-1.012f)<1e-4f&&MathF.Abs(EquipmentProfile.Length("crossbow","wpn_crossbow")-.979f)<1e-4f,
    "the AK, the M16 or a crossbow not drawn 10% larger: "+EquipmentProfile.Length("ak47")+" "+EquipmentProfile.Length("m16")+" "+EquipmentProfile.Length("crossbow","wpn_harpoon_gun"));
   foreach(var (gun,was) in new[]{("pistol",.22f),("revolver",.37f),("uzi",.35f),("shotgun",1.08f),("sniper",1.1f),("m60",1.1f),("bazooka",1.1f)})
    Check(EquipmentProfile.Growth(gun)==1&&EquipmentProfile.Length(gun)==was,gun+" no longer drawn at its size");
   {
    var fist=new Vector3(0,-.025f,.073f);var turn=Quaternion.CreateFromYawPitchRoll(.4f,-.3f,.2f);var wrist=new Vector3(.01f,-.03f,-.12f);
    float raw=.967f*EquipmentProfile.RifleGrowth;var grown=GrownGrip.Hand(wrist,turn,raw,fist,EquipmentProfile.RifleGrowth);
    Check(MathF.Abs(grown.size-.967f)<1e-5f,"the hand on a grown gun not kept at its size: "+grown.size);
    Check(Vector3.Distance(grown.wrist+Vector3.Transform(fist*grown.size,turn),wrist+Vector3.Transform(fist*raw,turn))<1e-6f,"the hand's fist moved off the grown gun's grip");
    var same=GrownGrip.Hand(wrist,turn,.98f,fist,1);Check(same.wrist==wrist&&same.size==.98f,"a gun at the game's size changed the hand");
    var bad=GrownGrip.Hand(wrist,turn,float.NaN,fist,1.1f);Check(bad.wrist==wrist,"a broken size moved the hand");
   }
  }
  Console.WriteLine("PASS: 0.1.243 the plain pistol fitted without its hidden silencer (a point outside the gun): drawn 22 cm long as the silenced one, not 17 cm; the revolver drawn 37 cm: the hand holding either at the free hand's size, as on the AK (it was 0.76 and 0.74).");
  Console.WriteLine("PASS: 0.1.244 the Uzi drawn 35 cm long (was 46): the hand holding it as on the pistol (it was 1.29 of the free hand).");
  Console.WriteLine("PASS: 0.1.250 the crossbows by their model: the harpoon gun 1.03 m, the tactical crossbow 92 cm, the crossbow 89 cm (all 72 before): the hand on each as on the pistol and the AK (it was 0.69, 0.77 and 0.79); other guns not sized by name.");
  Console.WriteLine("PASS: 0.1.249 the shotguns drawn 1.08 m long (were 0.95): the hand on the pump gun and on the double-barrel as on the pistol and the AK (it was 0.87 and 0.85).");
 }
}
