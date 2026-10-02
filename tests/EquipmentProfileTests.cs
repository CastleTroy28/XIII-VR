using System;using XiiiXR;
class EquipmentProfileTests {
 static void Main(){
 foreach(int slot in new[]{21,22,23,24,25,26,27,28,29,30,31,32,34,11,12,13,14,15,16,33,40,41}){
 var p=EquipmentProfile.ForSlot(slot);if(p.Length==0||EquipmentProfile.Length(p)<=0)throw new Exception("missing slot "+slot);
 if(EquipmentProfile.Manual(p)!=(slot==21||slot==23||slot==24||slot==25||slot==26||slot==27||slot==28||slot==29))throw new Exception("manual reload applied to unconfigured slot "+slot);// 0.1.194: the Uzi (23) too
 if(EquipmentProfile.ChestMagazine(p)!=(slot==21||slot==23))throw new Exception("chest reload for the wrong slot "+slot);// 0.1.117: M16 (25) and crossbow (28) added
 }
 if(EquipmentProfile.ForSlot(20)!=""||EquipmentProfile.ForSlot(9)!="")throw new Exception("fists/medkit special route intercepted");
 if(EquipmentProfile.RequiresMuzzle("prop")||EquipmentProfile.RequiresMuzzle("key")||!EquipmentProfile.RequiresMuzzle("uzi"))throw new Exception("non-fire item needs muzzle");
 Console.WriteLine("PASS: full native inventory slot catalog, generic non-fire route and manual-reload isolation.");
 }
}
