using System;
namespace TizenAmbilight {
 public static class CaptureRetry {
  // At most three retries at the requested frame cadence after a RECENT good frame.
  // Persistent failures, DRM, native exceptions and out-of-range geometry keep the old backoff.
  public static bool TryDelay(VideoPlaneResult result,long failures,double goodFrameAgeMs,int fps,out int delay,string method=null) {
   delay=0;
   bool fastEmpty=result!=null&&result.Rc==0&&result.RetWidth==0&&result.RetHeight==0&&result.CaptureMs<=5&&result.Error!=null&&
    result.Error.StartsWith("Q74T unsupported returned plane geometry:",StringComparison.Ordinal);
   // v12 live traces: fast empty frames triggered 200/500/1000/2000-ms sleeps.
   // Only screen_video may retry these for up to one second, at <=30 calls/s.
   // No new native ABI, no concurrent calls, and no busy polling during idle/DRM.
   int limit=method=="screen_video"&&fastEmpty?30:3;
   if(result==null||fps<20||failures<1||failures>limit||goodFrameAgeMs<0||goodFrameAgeMs>1000)return false;
   bool transient=(result.Rc==-1&&result.Error==null)||
    (result.Rc==0&&result.RetWidth==0&&result.RetHeight==0&&result.Error!=null&&
     result.Error.StartsWith("Q74T unsupported returned plane geometry:",StringComparison.Ordinal));
   if(!transient)return false;
   delay=Math.Max(33,(int)Math.Ceiling(1000.0/Math.Max(1,fps)));return true;
  }
 }
}
