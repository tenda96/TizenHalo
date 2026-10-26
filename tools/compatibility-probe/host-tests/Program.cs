using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using TizenHaloProbe;

class Tests
{
    static void Put(byte[] b,int at,ulong n,int width){for(int i=0;i<width;i++)b[at+i]=(byte)(n>>(8*i));}
    static byte[] Fixture(bool wide)
    {
        var b=new byte[1024];b[0]=0x7f;b[1]=69;b[2]=76;b[3]=70;b[4]=(byte)(wide?2:1);b[5]=1;b[6]=1;
        Put(b,18,(ulong)(wide?183:40),2);Put(b,wide?40:32,128,wide?8:4);
        int es=wide?64:40;Put(b,wide?58:46,(ulong)es,2);Put(b,wide?60:48,3,2);
        int sy=128+es,st=128+2*es;Put(b,sy+4,11,4);Put(b,st+4,3,4);
        Put(b,sy+(wide?24:16),512,wide?8:4);Put(b,sy+(wide?32:20),(ulong)(wide?72:48),wide?8:4);
        Put(b,sy+(wide?40:24),2,4);Put(b,sy+(wide?56:36),(ulong)(wide?24:16),wide?8:4);
        Put(b,st+(wide?24:16),768,wide?8:4);Put(b,st+(wide?32:20),128,wide?8:4);
        byte[] names=Encoding.ASCII.GetBytes("\0secvideo_api_capture_video_main\0ppi_ve_get_rgb_measure_pixel\0");Array.Copy(names,0,b,768,names.Length);
        int sym=512+(wide?24:16);Put(b,sym,1,4);b[sym+(wide?4:12)]=0x12;Put(b,sym+(wide?6:14),1,2);
        // A matching imported name must not be reported as an available export.
        sym+=wide?24:16;Put(b,sym,32,4);b[sym+(wide?4:12)]=0x12;
        return b;
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Invalid(byte[] b){try{Inventory.Elf(b);}catch(InvalidDataException){return;}throw new Exception("Malformed ELF accepted");}
    static void Main(string[] args)
    {
        foreach(bool wide in new[]{false,true}) {
            var b=Fixture(wide);var d=Inventory.Elf(b);var names=(List<object>)d["exports"];
            Check(names.Count==1 && (string)names[0]=="secvideo_api_capture_video_main","Imports or wrong exports included");
            Check((int)d["elf_bits"]==(wide?64:32),"Wrong ELF class");
            Invalid(new byte[3]);var bad=(byte[])b.Clone();bad[5]=2;Invalid(bad);
            bad=(byte[])b.Clone();Put(bad,wide?40:32,0xffffff00,wide?8:4);Invalid(bad);
            bad=(byte[])b.Clone();Put(bad,128+(wide?64:40)+(wide?40:24),4,4);Invalid(bad);
        }
        if(args.Length>0) {
            var d=Inventory.Elf(File.ReadAllBytes(args[0]));var names=(List<object>)d["exports"];
            Check(names.Contains("secvideo_api_capture_video_main"),"Real Q74T main export missing");
            Console.WriteLine("Real firmware library: "+names.Count+" relevant exports");
        }
        Console.WriteLine("PASS: ELF32/ELF64, defined vs imported symbols, truncated files and invalid bounds");
    }
}
