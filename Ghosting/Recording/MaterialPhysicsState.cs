using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[Flags]
public enum MaterialPhysicsState : ushort
{
    None = 0,
    Tarmac = 1 << 0,
    Grass = 1 << 1,
    Sand = 1 << 2,
    Soap = 1 << 3,
    Wood = 1 << 4,
    Mud = 1 << 5,
    Ice1 = 1 << 6,
    Ice2 = 1 << 7,
    Ice3 = 1 << 8,
}
