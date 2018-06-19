using System;
using System.Runtime.InteropServices;

public static class PixWrapper  
{
    [DllImport("PixWrapperUnity")]
    public static extern void PIXBeginEventEx(UInt64 color, string text);
    [DllImport("PixWrapperUnity")]
    public static extern void PIXEndEventEx();
}  