public static class SveltoProfiler
{
//    static Dictionary<string, long> times = new Dictionary<string, long>();
//    static Stopwatch watch = new Stopwatch();
//    static string _name;
//    static long _current;

    public static void BeginSample(string name)
    {
        PixWrapper.PIXBeginEventEx(0x11000000, name);
        /*_name = name;
        if (times.ContainsKey(name) == false)
            times[name] = 0;
        
        Profiler.BeginSample(name);
        _current = watch.ElapsedMilliseconds;*/
    }

    public static void EndSample()
    {
        PixWrapper.PIXEndEventEx();
        /*var passed = watch.ElapsedMilliseconds;
        
        Profiler.EndSample();
        
        var l = passed - _current;
        times[_name] = times[_name] + l;*/
    }
}