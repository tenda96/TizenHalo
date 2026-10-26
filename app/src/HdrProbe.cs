using System;
using System.Runtime.InteropServices;

namespace TizenAmbilight
{
    public static class HdrProbe
    {
        // Signature and enum values recovered from THIS firmware's Tizen.TV.System.dll.
        // Read-only libavoc getter; no picture settings are changed.
        [DllImport("/usr/lib/libavoc.so",CallingConvention=CallingConvention.Cdecl)]
        static extern int avoc_get_hdr_type(out int type);
        public static volatile int RawType=-1;
        public static volatile string Error="Not queried yet";
        static long _updatedTicks;
        static bool _missing;
        public static void Poll()
        {
            if(_missing||IntPtr.Size!=4) return;
            try
            {
                int type,rc=avoc_get_hdr_type(out type);
                if(rc==0&&type>=0&&type<=3)
                { RawType=type; System.Threading.Interlocked.Exchange(ref _updatedTicks,DateTime.UtcNow.Ticks); Error=""; }
                else Error="avoc_get_hdr_type rc="+rc+" type="+type;
            }
            catch(Exception e) { Error=e.GetType().Name+": "+e.Message; _missing=true; }
        }
        public static double AgeMs {get {long t=System.Threading.Interlocked.Read(ref _updatedTicks);return t==0?-1:(DateTime.UtcNow.Ticks-t)/(double)TimeSpan.TicksPerMillisecond;}}
        public static bool Available
        { get { return RawType>=0&&(DateTime.UtcNow.Ticks-System.Threading.Interlocked.Read(ref _updatedTicks))<TimeSpan.TicksPerSecond*10; } }
        public static string Mode
        { get { return !Available?"unknown":RawType==0?"sdr":RawType==2?"hlg":"hdr"; } }
        public static string Description
        { get { return !Available?"Detection unavailable":RawType==0?"SDR":RawType==1?"HDR10":RawType==2?"HLG":"HDR10+"; } }
    }
}
