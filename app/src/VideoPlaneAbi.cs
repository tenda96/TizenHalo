using System;
using System.Runtime.InteropServices;

namespace TizenAmbilight
{
    // Caller-owned ARM32 headers only. The S84F timestamp/flip tail is not proven,
    // so it is deliberately neither read nor written. All padding stays zero.
    public sealed class VideoPlaneAbi
    {
        public readonly string Id;
        public readonly int MaxWidth,MaxHeight,ColorOffset,ProtectedOffset,InterlaceOffset,YPitchOffset,CPitchOffset,TimestampOffset,FlipOffset;
        public int YBytes {get{return MaxWidth*MaxHeight;}}
        public int CBytes {get{return YBytes/2;}}
        public const int HeaderBytes=256;
        VideoPlaneAbi(string id,int width,int height,int color,int protection,int interlace,int yp,int cp,int timestamp,int flip)
        {Id=id;MaxWidth=width;MaxHeight=height;ColorOffset=color;ProtectedOffset=protection;InterlaceOffset=interlace;YPitchOffset=yp;CPitchOffset=cp;TimestampOffset=timestamp;FlipOffset=flip;}
        public static readonly VideoPlaneAbi Q74T=new VideoPlaneAbi("q74t-55-native-v1",1280,720,0x18,0x1c,0x24,0x28,0x2c,0x30,0x3c);
        public static readonly VideoPlaneAbi S84F=new VideoPlaneAbi("s84f-90-native-v1",960,540,0x1c,0x20,0x28,0x2c,0x30,-1,-1);
        public static VideoPlaneAbi Find(string id)
        {if(id==Q74T.Id)return Q74T;if(id==S84F.Id)return S84F;throw new ArgumentException("No full-frame ABI for "+id);}
        public bool AllowsMethod(CaptureMethod method)
        {return Id==Q74T.Id || (!method.Legacy && method.Id=="main");}
        public bool FitsRequest(int width,int height)
        {return width>=64&&height>=64&&width<=MaxWidth&&height<=MaxHeight&&(width&1)==0&&(height&1)==0;}
        public void InitializeHeader(IntPtr p,int yAddress,int cAddress)
        {
            for(int i=0;i<HeaderBytes;i++)Marshal.WriteByte(p,i,0);
            Marshal.WriteInt32(p,0,YBytes);Marshal.WriteInt32(p,4,CBytes);
            Marshal.WriteInt32(p,0x10,yAddress);Marshal.WriteInt32(p,0x14,cAddress);
        }
        public void ClearOutputs(IntPtr p,bool legacy)
        {
            Marshal.WriteInt32(p,8,0);Marshal.WriteInt32(p,12,0);
            if(!legacy){Marshal.WriteInt32(p,InterlaceOffset,0);Marshal.WriteInt32(p,YPitchOffset,0);Marshal.WriteInt32(p,CPitchOffset,0);}
        }
        public bool CallerPointersUnchanged(IntPtr p,int yAddress,int cAddress)
        {return Marshal.ReadInt32(p,0x10)==yAddress&&Marshal.ReadInt32(p,0x14)==cAddress;}
        public void ReadHeader(IntPtr p,bool legacy,VideoPlaneResult r)
        {
            if(legacy&&Id!=Q74T.Id)throw new ArgumentException("Legacy ABI not validated for "+Id);
            r.RetWidth=Marshal.ReadInt32(p,8);r.RetHeight=Marshal.ReadInt32(p,12);
            r.ColorFormat=Marshal.ReadInt32(p,ColorOffset);r.ProtectedOn=Marshal.ReadInt32(p,ProtectedOffset);
            r.YCapacity=Marshal.ReadInt32(p,0);r.CCapacity=Marshal.ReadInt32(p,4);
            r.NativeTimestamp=TimestampOffset<0?0:Marshal.ReadInt64(p,TimestampOffset);
            r.FlipType=FlipOffset<0?0:Marshal.ReadInt32(p,FlipOffset);
            r.Interlaced=r.YPitch=r.CPitch=0;
            if(!legacy){r.Interlaced=Marshal.ReadInt32(p,InterlaceOffset);r.YPitch=Marshal.ReadInt32(p,YPitchOffset);r.CPitch=Marshal.ReadInt32(p,CPitchOffset);}
            if(r.Rc==0&&r.RetWidth>0&&r.RetHeight>0&&r.ColorFormat!=0)r.Error=Id+": returned format is not YUV420";
        }
        public bool SupportedGeometry(VideoPlaneResult r)
        {
            return r!=null&&FitsRequest(r.RetWidth,r.RetHeight)&&r.Interlaced==0&&
                   (r.YPitch==0||(r.YPitch>=r.RetWidth&&r.YPitch<=MaxWidth))&&
                   (r.CPitch==0||(r.CPitch>=r.RetWidth&&r.CPitch<=MaxWidth))&&
                   (long)(r.YPitch==0?r.RetWidth:r.YPitch)*r.RetHeight<=YBytes&&
                   (long)(r.CPitch==0?r.RetWidth:r.CPitch)*(r.RetHeight/2)<=CBytes;
        }
    }
}
