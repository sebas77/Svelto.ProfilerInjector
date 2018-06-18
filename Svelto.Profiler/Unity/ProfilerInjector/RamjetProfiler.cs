using System;

/// <summary>
/// Used to mark modules in assemblies as already patched.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class RamjetProfilerPostProcessedAssemblyAttribute : Attribute
{
}
