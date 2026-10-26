using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace TizenHaloProbe
{
    // This inventories ELF files as data. It never dlopens a Samsung library or calls capture.
    public static class Inventory
    {
        public const int MaximumLibraryBytes = 64 * 1024 * 1024;
        public static readonly string[] LibraryPaths = {
            "/usr/lib/libvideo-capture.so.0.1.0",
            "/usr/lib/libsec-video-capture.so.0.1.0",
            "/usr/lib/libvideoenhance.so",
            "/usr/lib/libavoc.so",
            "/usr/lib/libdisplay-capture-api.so.0.0",
            "/usr/lib/libvideo-capture.so",
            "/usr/lib/libdisplay-capture-api.so",
            "/usr/lib/libvideo-capture.so.0",
            "/usr/lib/libsec-video-capture.so.0",
            "/usr/lib/libsec-video-capture.so",
            "/usr/lib/libvideoenhance.so.0",
            "/usr/lib/libavoc.so.0",
            "/usr/lib/libdisplay-capture-api.so.0"
        };

        static void Range(byte[] b, ulong offset, ulong length)
        {
            if (offset > (ulong)b.Length || length > (ulong)b.Length - offset)
                throw new InvalidDataException("ELF field outside file");
        }
        static ushort U16(byte[] b, ulong p) { Range(b,p,2); return (ushort)(b[(int)p] | b[(int)p+1]<<8); }
        static uint U32(byte[] b, ulong p) { Range(b,p,4); return (uint)(b[(int)p] | b[(int)p+1]<<8 | b[(int)p+2]<<16 | b[(int)p+3]<<24); }
        static ulong U64(byte[] b, ulong p) { return U32(b,p) | (ulong)U32(b,p+4)<<32; }
        static bool Relevant(string name)
        {
            return name.StartsWith("secvideo_api_capture_",StringComparison.Ordinal) ||
                name.IndexOf("rgb_measure",StringComparison.Ordinal)>=0 ||
                name.IndexOf("capture",StringComparison.OrdinalIgnoreCase)>=0 ||
                name=="avoc_get_hdr_type";
        }
        public static Dictionary<string,object> Elf(byte[] b)
        {
            Range(b,0,16);
            if (b[0]!=0x7f || b[1]!='E' || b[2]!='L' || b[3]!='F') throw new InvalidDataException("Not ELF");
            if (b[5]!=1 || b[6]!=1 || (b[4]!=1 && b[4]!=2)) throw new InvalidDataException("Unsupported ELF encoding");
            bool wide=b[4]==2; Range(b,0,wide?64UL:52UL);
            ulong sh=wide?U64(b,40):U32(b,32);
            int entry=U16(b,wide?58UL:46UL), count=U16(b,wide?60UL:48UL);
            if(count==0 || entry<(wide?64:40)) throw new InvalidDataException("No usable section table");
            Range(b,sh,(ulong)entry*(ulong)count);
            var names=new SortedSet<string>(StringComparer.Ordinal);
            bool found=false;
            for(int i=0;i<count;i++) {
                ulong s=sh+(ulong)(i*entry); if(U32(b,s+4)!=11)continue; found=true;
                ulong offset=wide?U64(b,s+24):U32(b,s+16), size=wide?U64(b,s+32):U32(b,s+20);
                uint link=U32(b,s+(wide?40UL:24UL));
                ulong stride=wide?U64(b,s+56):U32(b,s+36);
                if(link>=count || stride<(wide?24UL:16UL) || size%stride!=0)throw new InvalidDataException("Invalid dynamic symbols");
                Range(b,offset,size); ulong str=sh+(ulong)link*(ulong)entry;
                if(U32(b,str+4)!=3)throw new InvalidDataException("Invalid symbol string table");
                ulong so=wide?U64(b,str+24):U32(b,str+16), ss=wide?U64(b,str+32):U32(b,str+20);Range(b,so,ss);
                for(ulong at=offset;at<offset+size;at+=stride) {
                    uint n=U32(b,at); if(n>=ss)throw new InvalidDataException("Invalid symbol name offset");
                    byte info=b[(int)(at+(wide?4UL:12UL))];
                    ushort section=U16(b,at+(wide?6UL:14UL));
                    if(section==0 || (info>>4)!=1 && (info>>4)!=2)continue;
                    ulong end=so+n;while(end<so+ss && b[(int)end]!=0)end++;
                    if(end==so+ss)throw new InvalidDataException("Unterminated symbol name");
                    string name=Encoding.UTF8.GetString(b,(int)(so+n),(int)(end-so-n));
                    if(Relevant(name))names.Add(name);
                }
            }
            var symbols=new List<object>();foreach(string name in names)symbols.Add(name);
            return new Dictionary<string,object>{{"elf_bits",wide?64:32},{"machine",(int)U16(b,18)},
                {"dynamic_symbol_table",found},{"exports",symbols}};
        }
        public static Dictionary<string,object> Library(string path,int id)
        {
            var item=new Dictionary<string,object>{{"id",id},{"path",path}};
            try {
                // File.Exists can also return false when the app sandbox hides a file.
                if(!File.Exists(path)){item["status"]="missing_or_inaccessible";return item;}
                long size=new FileInfo(path).Length;
                if(size<16 || size>MaximumLibraryBytes)throw new InvalidDataException("Library size outside limit");
                byte[] bytes=File.ReadAllBytes(path);
                if(bytes.Length>MaximumLibraryBytes)throw new InvalidDataException("Library exceeds limit");
                using(var sha=SHA256.Create())item["sha256"]=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                item["bytes"]=(long)bytes.Length;
                foreach(var pair in Elf(bytes))item[pair.Key]=pair.Value;
                item["status"]="read";
            } catch(Exception e){item["status"]="error";item["error"]=e.GetType().Name+": "+e.Message;}
            return item;
        }
        public static Dictionary<string,object> Collect(string platform,string model,string firmware)
        {
            var libs=new List<object>();for(int i=0;i<LibraryPaths.Length;i++)libs.Add(Library(LibraryPaths[i],i));
            return new Dictionary<string,object>{{"schema",1},{"probe_version","0.1.0"},{"platform",platform},
                {"model",model},{"firmware",firmware},{"process_bits",IntPtr.Size*8},
                {"native_capture_invoked",false},{"libraries",libs}};
        }
    }
}
