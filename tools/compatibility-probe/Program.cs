using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Tizen.Applications;
using Tizen.System;
using ElmSharp;

namespace TizenHaloProbe
{
    class App : CoreUIApplication
    {
        const int Port=8794;
        TcpListener server; volatile bool stopped;
        Window window; string report;
        static string Feature(string name) { try {string value;return Information.TryGetValue(name,out value)?value:"unknown";}catch{return "unknown";} }
        static string Address() {
            try {using(var udp=new UdpClient()){udp.Connect("192.0.2.1",9);return ((IPEndPoint)udp.Client.LocalEndPoint).Address.ToString();}}
            catch{return "<TV-IP>";}
        }
        void Text(string message,int y,int size) {
            var label=new Label(window){Text=message,TextStyle="DEFAULT='font_size="+size+" color=#edf5fa wrap=word'"};
            var screen=window.ScreenSize;label.Move(60,y);label.Resize(Math.Max(600,screen.Width-120),150);label.Show();
        }
        protected override void OnCreate() {
            base.OnCreate();Elementary.Initialize();window=new Window("TizenHalo compatibility");
            var screen=window.ScreenSize;window.Resize(screen.Width,screen.Height);
            var bg=new Background(window){Color=new Color(9,14,19)};window.AddResizeObject(bg);bg.Show();
            window.BackButtonPressed+=(s,e)=>Exit();
            string platform=Feature("http://tizen.org/feature/platform.version");
            report=TizenAmbilight.Json.ToJson(Inventory.Collect(platform,Feature("http://tizen.org/feature/model_name"),"Enter firmware version when sharing this report"));
            try {
                server=new TcpListener(IPAddress.Any,Port);server.Start();
                new Thread(Serve){IsBackground=true}.Start();
                Text("TizenHalo · Compatibility Probe",55,40);
                Text("Tizen "+platform+" · Library report ready",180,30);
                Text("Open on your phone or computer:",290,28);
                Text("http://"+Address()+":"+Port+"/",400,38);
                Text("Keep this screen open. Download the report first. Library downloads are optional for private firmware analysis.",540,25);
                Text("Back: close diagnostics",720,25);
            }catch(Exception e){Text("Diagnostics server failed: "+e.Message,200,28);}
            window.Show();
        }
        void Serve() {
            while(!stopped) {
                try {using(var client=server.AcceptTcpClient()){client.ReceiveTimeout=3000;client.SendTimeout=10000;Handle(client);}}
                catch{if(stopped)return;}
            }
        }
        static string Line(Stream stream,ref int budget) {
            var line=new StringBuilder();
            while(budget++<8192) {
                int c=stream.ReadByte();if(c<0)throw new IOException("Incomplete request");
                if(c==10)return line.ToString().TrimEnd('\r');
                if(c!=13 && (c<32 || c>126))throw new IOException("Invalid request header");
                line.Append((char)c);
            }
            throw new IOException("Request header too large");
        }
        void Handle(TcpClient client) {
            var stream=client.GetStream();int budget=0;
            string first=Line(stream,ref budget);if(first.Length>2048)return;
            string[] request=first.Split(' ');if(request.Length!=3||request[0]!="GET"){Reply(stream,405,"text/plain",Encoding.UTF8.GetBytes("GET only"));return;}
            string host=null,origin=null,line;
            while((line=Line(stream,ref budget)).Length>0) {
                int colon=line.IndexOf(':');if(colon<0)continue;
                string key=line.Substring(0,colon).Trim(),value=line.Substring(colon+1).Trim();
                if(key.Equals("Host",StringComparison.OrdinalIgnoreCase))host=value;
                if(key.Equals("Origin",StringComparison.OrdinalIgnoreCase))origin=value;
            }
            // Restrict browser access to the literal local endpoint, including optional origin.
            string local=((IPEndPoint)client.Client.LocalEndPoint).Address.ToString()+":"+Port;
            if(host!=local && host!="127.0.0.1:"+Port && host!="localhost:"+Port){Reply(stream,403,"text/plain",Encoding.UTF8.GetBytes("Local address required"));return;}
            if(origin!=null && origin!="http://"+host){Reply(stream,403,"text/plain",Encoding.UTF8.GetBytes("Cross-origin request refused"));return;}
            if(request[1]=="/api/report") {Reply(stream,200,"application/json",Encoding.UTF8.GetBytes(report),"tizenhalo-compatibility.json");return;}
            const string prefix="/api/library/";
            if(request[1].StartsWith(prefix,StringComparison.Ordinal)) {
                int id; if(!int.TryParse(request[1].Substring(prefix.Length),out id)||id<0||id>=Inventory.LibraryPaths.Length){Reply(stream,404,"text/plain",new byte[0]);return;}
                string path=Inventory.LibraryPaths[id];
                if(!File.Exists(path)||new FileInfo(path).Length>Inventory.MaximumLibraryBytes){Reply(stream,404,"text/plain",new byte[0]);return;}
                byte[] data=File.ReadAllBytes(path);if(data.Length>Inventory.MaximumLibraryBytes)return;
                Reply(stream,200,"application/octet-stream",data,Path.GetFileName(path));return;
            }
            if(request[1]!="/"){Reply(stream,404,"text/plain",new byte[0]);return;}
            var html=new StringBuilder("<!doctype html><meta name=viewport content='width=device-width'><title>TizenHalo diagnostics</title><style>body{font:18px system-ui;max-width:800px;margin:40px auto;padding:20px;background:#09121b;color:#edf5fa}a{color:#52e2bb}li{margin:18px 0}</style><h1>TizenHalo compatibility</h1><p>This diagnostic reads library files and symbols. It does not invoke capture or change TV settings.</p><p><a href='/api/report'>Download compatibility report</a></p><h2>Libraries for private analysis</h2><p>Download only when requested by the maintainer. Keep these files out of public GitHub issues.</p><ul>");
            for(int i=0;i<Inventory.LibraryPaths.Length;i++)if(File.Exists(Inventory.LibraryPaths[i]))html.Append("<li><a href='/api/library/").Append(i).Append("'>").Append(Inventory.LibraryPaths[i]).Append("</a></li>");
            html.Append("</ul><p>Close the TV app with Back to stop this server.</p>");
            Reply(stream,200,"text/html; charset=utf-8",Encoding.UTF8.GetBytes(html.ToString()));
        }
        static void Reply(Stream stream,int status,string type,byte[] body,string name=null) {
            string h="HTTP/1.1 "+status+" Response\r\nContent-Type: "+type+"\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n";
            if(name!=null)h+="Content-Disposition: attachment; filename=\""+name+"\"\r\n";
            byte[] head=Encoding.ASCII.GetBytes(h+"\r\n");stream.Write(head,0,head.Length);stream.Write(body,0,body.Length);
        }
        protected override void OnTerminate(){stopped=true;try{server.Stop();}catch{}base.OnTerminate();}
        static void Main(string[] args){new App().Run(args);}
    }
}
