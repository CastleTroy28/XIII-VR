using BepInEx.Configuration;
namespace XiiiXR;
internal static class EnemyOptions
{
    // 0.1.116: the feature was rebuilt after 0.1.114/0.1.115 closed the game;
    // a crash guard of 0.1.115 may have switched it off, so it is switched
    // back on once for the rebuilt version.
    internal const int CurrentRevision=116;
    internal static ConfigEntry<bool> Smarter=null!;
    internal static ConfigEntry<int> Revision=null!;
    internal static bool Reenabled;
    internal static void Load(ConfigFile c)
    {
        Smarter=c.Bind("VR","SmarterEnemies",true,"Searching enemies head roughly towards the player, idle less and search longer; armed enemies may pursue. Config file only (0.1.224: no longer in VR SETTINGS).");
        Revision=c.Bind("VR","SmarterEnemiesRevision",0,"Internal: version of the smarter-enemies feature (do not edit).");
        Reenabled=false;
        if(Revision.Value<CurrentRevision)
        {
            if(!Smarter.Value){Smarter.Value=true;Reenabled=true;}
            Revision.Value=CurrentRevision;
        }
    }
}
