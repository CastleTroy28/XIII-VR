using System;
using Il2CppInterop.Runtime;
using PlayMagic.AI;
using PlayMagic.Weapons;
namespace XiiiXR;
// 0.1.168: an enemy the player
// disarmed may be taken hostage from any side (GripCarry.Disarmed); when it
// is, its fist fight and stun end first, so the game's hostage hold alone
// moves it (GripCarry.TakingHostage).
internal sealed partial class NpcHitReactions
{
    internal bool IsDisarmed(NPC npc)
    {
        if(npc==null)return false;
        if(brawlers.ContainsKey(npc.Pointer))return true;
        try
        {
            var inv=AnimOf(npc)?.Inventory;
            if(inv==null)inv=npc.GetComponent(Il2CppType.Of<EnemyInventory>())?.TryCast<EnemyInventory>();
            return inv!=null&&disarmed.ContainsKey(inv.Pointer);
        }
        catch(Exception){return false;}
    }
    internal void TakenHostage(NPC npc)
    {
        if(npc==null)return;var id=npc.Pointer;string how="";
        if(brawlers.ContainsKey(id)){EndBrawl(id,false);how+=" stops fighting";}
        if(stuns.TryGetValue(id,out var s)){try{End(s);}catch(Exception){}stuns.Remove(id);how+=" stun ended";}
        if(how.Length>0&&reports++<80)Bootstrap.Write("NPC HIT "+npc.name+" taken hostage:"+how);
    }
    private void LinkHostages(){GripCarry.Disarmed=IsDisarmed;GripCarry.TakingHostage=TakenHostage;}
    private void UnlinkHostages(){GripCarry.Disarmed=null;GripCarry.TakingHostage=null;}
}
