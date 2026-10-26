using System;
namespace TizenAmbilight {
 public static class TraceFrameMarker {
  // Synthetic source: 20 cells across the image at y=7%; magic 0xB4 + frame mod 4096.
  public static int Read(VideoPlaneResult r) {
   if(r==null||!r.HasFrame||r.W<64||r.H<64||r.YStride<r.W)return -1;
   int row=(int)(r.H*.07),lo=255,hi=0;
   for(int i=0;i<8;i++){int v=r.Y[row*r.YStride+(int)((i+.5)*r.W/20)];lo=Math.Min(lo,v);hi=Math.Max(hi,v);}
   if(hi-lo<70)return -1;int threshold=(hi+lo)/2,magic=0,id=0;
   for(int i=0;i<20;i++){int bit=r.Y[row*r.YStride+(int)((i+.5)*r.W/20)]>threshold?1:0;if(i<8)magic=(magic<<1)|bit;else id=(id<<1)|bit;}
   return magic==0xb4?id:-1;
  }
  public static uint Fingerprint(VideoPlaneResult r) {
   if(r==null||!r.HasFrame)return 0;
   uint h=2166136261;int length=Math.Min(r.Y.Length,r.H*r.YStride);
   unchecked {for(int i=0;i<256;i++)h=(h^r.Y[i*length/256])*16777619;
    int count=Math.Min(r.C.Length,length/2);for(int i=0;i<64;i++)h=(h^r.C[i*count/64])*16777619;}
   return h;
  }
 }
}
