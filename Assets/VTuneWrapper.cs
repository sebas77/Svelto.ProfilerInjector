using System;
using System.Runtime.InteropServices;

public static class VTuneWrapper  
{
    [DllImport("VTuneWrapperUnity")]
    public static extern void VTuneBeginEventEx(string name);
    [DllImport("VTuneWrapperUnity")]
    public static extern void VTuneEndEventEx();
}  