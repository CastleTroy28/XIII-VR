using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace XiiiXR;
// Full input bindings accompany haptics: installing only output actions can
// disable GetControllerState's legacy buttons in SteamVR.
internal static class SteamInputManifest
{
    internal const string Set="/actions/xiii";
    internal static string Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var actions=new List<object>();
        foreach(string side in new[]{"left","right"})
        {
            foreach(string name in new[]{"trigger","grip","a","b","click"})actions.Add(new{name=Set+"/in/"+side+"_"+name,type="boolean",requirement="optional"});
            actions.Add(new{name=Set+"/in/"+side+"_stick",type="vector2",requirement="optional"});
            actions.Add(new{name=Set+"/out/"+side,type="vibration",requirement="optional"});
        }
        var sources=new List<object>();var haptics=new List<object>();
        foreach(string side in new[]{"left","right"})
        {
            string hand="/user/hand/"+side;
            foreach(string name in new[]{"trigger","grip","a","b"})
            {
                string key=name=="a"?(side=="left"?"x":"a"):name=="b"?(side=="left"?"y":"b"):name;
                sources.Add(new{path=hand+"/input/"+key,mode="button",inputs=new{click=new{output=Set+"/in/"+side+"_"+name}}});
            }
            sources.Add(new{path=hand+"/input/joystick",mode="joystick",inputs=new{position=new{output=Set+"/in/"+side+"_stick"},click=new{output=Set+"/in/"+side+"_click"}}});
            haptics.Add(new{path=hand+"/output/haptic",output=Set+"/out/"+side});
        }
        // A Pimax headset reports Oculus Touch to SteamVR. Unknown
        // profiles keep legacy input; do not guess their button paths.
        string binding="touch-029.json";
        File.WriteAllText(Path.Combine(directory,binding),JsonSerializer.Serialize(new{controller_type="oculus_touch",name="XIII Touch controls",description="XIII VR movement, menus and haptics",bindings=new Dictionary<string,object>{{Set,new{sources,haptics}}}}));
        string path=Path.Combine(directory,"actions-029.json");
        File.WriteAllText(path,JsonSerializer.Serialize(new{action_sets=new[]{new{name=Set,usage="leftright"}},actions,default_bindings=new[]{new{controller_type="oculus_touch",binding_url=binding}}}));
        return path;
    }
}
