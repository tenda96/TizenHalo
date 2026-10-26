using System;
using System.Runtime.InteropServices;
using TizenAmbilight;

static class VideoPlaneAbiTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        IntPtr p=Marshal.AllocHGlobal(VideoPlaneAbi.HeaderBytes+8);
        try {
            foreach(var abi in new[]{VideoPlaneAbi.Q74T,VideoPlaneAbi.S84F}) {
                for(int i=0;i<VideoPlaneAbi.HeaderBytes+8;i++)Marshal.WriteByte(p,i,0xee);
                abi.InitializeHeader(p,0x12340000,unchecked((int)0x92340000));
                Check(Marshal.ReadInt32(p,0)==abi.YBytes&&Marshal.ReadInt32(p,4)==abi.CBytes,"Wrong advertised capacity");
                Check(abi.CallerPointersUnchanged(p,0x12340000,unchecked((int)0x92340000)),"ARM32 pointer bits changed");
                for(int i=24;i<VideoPlaneAbi.HeaderBytes;i++)Check(Marshal.ReadByte(p,i)==0,"Header tail must start zero");
                for(int i=VideoPlaneAbi.HeaderBytes;i<VideoPlaneAbi.HeaderBytes+8;i++)Check(Marshal.ReadByte(p,i)==0xee,"Header write exceeded allocation");
                Marshal.WriteInt32(p,8,720);Marshal.WriteInt32(p,12,540);
                Marshal.WriteInt32(p,abi.ProtectedOffset,1);
                Marshal.WriteInt32(p,abi.YPitchOffset,960);Marshal.WriteInt32(p,abi.CPitchOffset,960);
                if(abi==VideoPlaneAbi.Q74T)Marshal.WriteInt64(p,0x30,123456789012L);
                else {Marshal.WriteInt32(p,0x18,71);Marshal.WriteInt64(p,0x38,987654321987L);}
                var result=new VideoPlaneResult{Rc=0};abi.ReadHeader(p,false,result);
                Check(result.ColorFormat==0&&result.ProtectedOn==1&&result.Interlaced==0,"Shifted flags misread");
                Check(result.YPitch==960&&result.CPitch==960&&abi.SupportedGeometry(result),"Stride readback inconsistent with ABI");
                Check(result.NativeTimestamp==(abi==VideoPlaneAbi.Q74T?123456789012L:0),"Unproven S84F timestamp must not be read");
                Marshal.WriteInt32(p,0x10,123);Check(!abi.CallerPointersUnchanged(p,0x12340000,unchecked((int)0x92340000)),"Replacement pointers must be rejected");
                Marshal.WriteInt32(p,abi.InterlaceOffset,1);abi.ReadHeader(p,false,result);
                Check(!abi.SupportedGeometry(result),"Interlaced readback must be rejected");
                abi.ClearOutputs(p,false);Check(Marshal.ReadInt32(p,8)==0&&Marshal.ReadInt32(p,abi.YPitchOffset)==0,"Stale geometry not cleared");
                Marshal.WriteInt32(p,8,720);Marshal.WriteInt32(p,12,540);Marshal.WriteInt32(p,abi.ColorOffset,3);
                result=new VideoPlaneResult{Rc=0};abi.ReadHeader(p,false,result);Check(result.Error!=null,"Unknown image format accepted");
                Check(!abi.SupportedGeometry(new VideoPlaneResult{RetWidth=719,RetHeight=540}),"Odd YUV width accepted");
                Check(!abi.SupportedGeometry(new VideoPlaneResult{RetWidth=720,RetHeight=540,YPitch=abi.MaxWidth+1}),"Oversized stride accepted");
            }
            Check(VideoPlaneAbi.Q74T.FitsRequest(1280,720)&&!VideoPlaneAbi.S84F.FitsRequest(1280,720),"Profile allocation limits mixed");
            Check(!VideoPlaneAbi.S84F.AllowsMethod(CaptureMethods.Find("screen_full"))&&!VideoPlaneAbi.S84F.AllowsMethod(CaptureMethods.Find("legacy_main")),"Q74T-only symbols must not escape into S84F");
        }finally{Marshal.FreeHGlobal(p);}
        Console.WriteLine("PASS: separate ARM32 headers, flags/stride offsets, unknown tail, capacity bounds, caller pointers and method scope");
    }
}
