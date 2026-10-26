using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace TizenHaloLauncher
{
    // Small grayscale PNG writer; no System.Drawing/native image dependency.
    public static class QrPng
    {
        static void Int32(Stream stream,uint n)
        { stream.WriteByte((byte)(n>>24));stream.WriteByte((byte)(n>>16));stream.WriteByte((byte)(n>>8));stream.WriteByte((byte)n); }
        static uint Crc(byte[] data)
        { uint crc=0xffffffff;foreach(byte b in data){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}return ~crc; }
        static void Chunk(Stream stream,string name,byte[] body)
        {
            Int32(stream,(uint)body.Length);byte[] type=Encoding.ASCII.GetBytes(name);
            stream.Write(type,0,4);stream.Write(body,0,body.Length);
            byte[] check=new byte[body.Length+4];Array.Copy(type,check,4);Array.Copy(body,0,check,4,body.Length);Int32(stream,Crc(check));
        }
        public static void Save(string text,string path)
        {
            var qr=QrCode.EncodeText(text,QrCode.Ecc.Medium);const int scale=8,border=4;
            int size=(qr.Size+border*2)*scale;
            byte[] raw=new byte[(size+1)*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int mx=x/scale-border,my=y/scale-border;
                raw[y*(size+1)+x+1]=(byte)(mx>=0&&my>=0&&mx<qr.Size&&my<qr.Size&&qr.GetModule(mx,my)?0:255);
            }
            byte[] compressed;
            using(var buffer=new MemoryStream())
            {
                buffer.WriteByte(0x78);buffer.WriteByte(0x9c);
                using(var deflate=new DeflateStream(buffer,CompressionLevel.Optimal,true)) deflate.Write(raw,0,raw.Length);
                uint a=1,b=0;foreach(byte value in raw){a=(a+value)%65521;b=(b+a)%65521;}Int32(buffer,(b<<16)|a);
                compressed=buffer.ToArray();
            }
            using(var output=File.Create(path))
            {
                byte[] signature={137,80,78,71,13,10,26,10};output.Write(signature,0,signature.Length);
                using(var header=new MemoryStream())
                { Int32(header,(uint)size);Int32(header,(uint)size);header.WriteByte(8);header.WriteByte(0);header.WriteByte(0);header.WriteByte(0);header.WriteByte(0);Chunk(output,"IHDR",header.ToArray()); }
                Chunk(output,"IDAT",compressed);Chunk(output,"IEND",new byte[0]);
            }
        }
    }
}
