using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace TizenAmbilight
{
    public static class FramePacer
    {
        [StructLayout(LayoutKind.Sequential)] struct Timespec32 { public int Seconds,Nanoseconds; }
        [DllImport("libc.so.6",EntryPoint="nanosleep",CallingConvention=CallingConvention.Cdecl,SetLastError=true)]
        static extern int nanosleep(ref Timespec32 request,out Timespec32 remaining);
        public static string Backend="not measured";
        public static double ManagedSleep1Ms=-1,PreciseSleep1Ms=-1;
        public static void Sleep(int milliseconds,bool precise)
        {
            if(milliseconds<=0) return;
            if(precise&&IntPtr.Size==4)
            {
                try
                {
                    var request=new Timespec32{Seconds=milliseconds/1000,Nanoseconds=(milliseconds%1000)*1000000};
                    Timespec32 remaining;
                    if(nanosleep(ref request,out remaining)==0) { Backend="libc nanosleep"; return; }
                }
                catch { }
            }
            Backend="Thread.Sleep";
            Thread.Sleep(milliseconds);
        }
        public static void Measure()
        {
            var sw=Stopwatch.StartNew();
            for(int i=0;i<5;i++) Thread.Sleep(1);
            ManagedSleep1Ms=sw.Elapsed.TotalMilliseconds/5;
            sw.Restart();
            for(int i=0;i<5;i++) Sleep(1,true);
            PreciseSleep1Ms=sw.Elapsed.TotalMilliseconds/5;
        }
    }
}
