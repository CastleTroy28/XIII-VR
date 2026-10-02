using System;
namespace XiiiXR;
internal enum EquipmentCommand { None, NextWeapon }
// Left B is the physical Y button. Grip+Y deliberately has no action.
// Releasing grip while Y is held must not accidentally cycle weapons.
internal sealed class EquipmentShortcuts
{
    private bool bArmed;
    internal EquipmentCommand Sample(bool valid,ulong held)
    {
        if(!valid){Reset();return EquipmentCommand.None;}
        bool b=(held&HandControls.B)!=0,grip=(held&HandControls.Grip)!=0;
        if(!b)bArmed=true;
        var command=EquipmentCommand.None;
        if(b&&bArmed){bArmed=false;if(!grip)command=EquipmentCommand.NextWeapon;}
        return command;
    }
    internal void Reset(){bArmed=false;}
}
