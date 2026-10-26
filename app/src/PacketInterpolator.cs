using System;
namespace TizenAmbilight {
 public sealed class PacketInterpolator {
  byte[] from,target,output; double start,duration,last;
  public void Clear(){from=target=output=null;last=0;}
  public void Push(byte[] packet,double now){
   if(packet==null||packet.Length<5||(packet.Length-2)%3!=0)throw new ArgumentException("Invalid DRGB frame");
   if(target==null||target.Length!=packet.Length){target=(byte[])packet.Clone();from=(byte[])packet.Clone();output=new byte[packet.Length];duration=0;}
   else {from=(byte[])Render(now).Clone();Array.Copy(packet,target,packet.Length);duration=Math.Max(16,Math.Min(120,now-last));}
   start=last=now;
  }
  public byte[] Render(double now){
   if(target==null)return null;
   double t=duration<=0?1:Math.Max(0,Math.Min(1,(now-start)/duration));
   output[0]=target[0];output[1]=target[1];
   for(int i=2;i<target.Length;i++)output[i]=(byte)Math.Round(from[i]+(target[i]-from[i])*t);
   return output;
  }
 }
}
