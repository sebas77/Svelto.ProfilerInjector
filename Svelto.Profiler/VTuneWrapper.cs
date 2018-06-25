using System;
using System.Runtime.InteropServices;

public static class VTuneWrapper  
{
   [DllImport("VTuneWrapperUnity")]
    public static extern int VTuneCreateEventEx(string name);
    [DllImport("VTuneWrapperUnity")]
    public static extern void VTuneBeginEventEx(int handle);
    [DllImport("VTuneWrapperUnity")]
    public static extern void VTuneEndEventEx(int handle);
}  