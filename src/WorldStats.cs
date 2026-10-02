namespace XiiiXR;
// 0.1.165: how much of
// the world the mod knows about, in the PERF line (world=), to see what grows.
internal static class WorldStats
{
    internal static int Enemies=-1,Pickups=-1,Holsters=-1;
    internal static string Report()=>"world=enemies:"+Enemies+",pickups:"+Pickups+",bodyWeapons:"+Holsters;
}
