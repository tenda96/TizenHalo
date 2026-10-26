using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TizenAmbilight
{
    public static class SettingsServer
    {
        public const int Port=8793;
        public static string Url="";
        public static string Error="";
        static TcpListener _listener;
        static string _html;
        static string _layoutHtml;
        static readonly HashSet<string> Hosts=new HashSet<string>{"127.0.0.1","localhost"};
        public static void Start(string resourceDirectory)
        {
            try
            {
                string address="127.0.0.1";
                try
                {
                    foreach(var ni in NetworkInterface.GetAllNetworkInterfaces())
                        foreach(var u in ni.GetIPProperties().UnicastAddresses)
                            if(u.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(u.Address))
                            { address=u.Address.ToString(); Hosts.Add(address); }
                }
                catch { }
                Hosts.Add(address);
                try
                {
                    using(var route=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp))
                    {
                        // Ask the OS which local address routes towards the WLED controller (no packet is
                        // sent). Without a controller yet, the interface scan above already chose one.
                        string wled; if(!LocalSettings.TryString("wledIp",out wled)||wled.Length==0) throw new InvalidOperationException();
                        route.Connect(new IPEndPoint(IPAddress.Parse(wled),9));
                        address=((IPEndPoint)route.LocalEndPoint).Address.ToString(); Hosts.Add(address);
                    }
                }
                catch { }
                Url="http://"+address+":"+Port;
                _html=File.ReadAllText(Path.Combine(resourceDirectory,"Settings.html"));
                _layoutHtml=File.ReadAllText(Path.Combine(resourceDirectory,"Layout.html"));
                _listener=new TcpListener(IPAddress.Any,Port); _listener.Start(8);
                new Thread(AcceptLoop){IsBackground=true}.Start();
            }
            catch(Exception e) { Error=e.GetType().Name+": "+e.Message; }
        }
        static void AcceptLoop()
        {
            while(true)
            {
                try { var c=_listener.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(_=>Handle(c)); }
                catch { return; }
            }
        }
        static void Reply(NetworkStream stream,int code,string content,string type="application/json; charset=utf-8")
        {
            byte[] data=Encoding.UTF8.GetBytes(content);
            byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+(code==200?"OK":"Error")+"\r\nContent-Type: "+type+
                "\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nContent-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'\r\n\r\n");
            stream.Write(head,0,head.Length); stream.Write(data,0,data.Length);
        }
        static string ReadLine(Stream stream,ref int bytes)
        {
            var s=new StringBuilder();
            while(true)
            {
                int b=stream.ReadByte(); if(b<0) throw new IOException("Incomplete request");
                if(++bytes>8192) throw new IOException("Intestazioni troppo grandi");
                if(b==10) return s.ToString().TrimEnd('\r');
                s.Append((char)b);
            }
        }
        static void Handle(TcpClient client)
        {
            using(client)
            {
                client.ReceiveTimeout=3000; client.SendTimeout=3000;
                try
                {
                    var stream=client.GetStream(); int bytes=0;
                    string[] request=ReadLine(stream,ref bytes).Split(' ');
                    if(request.Length!=3) return;
                    var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    string line;
                    while((line=ReadLine(stream,ref bytes)).Length>0)
                    { int i=line.IndexOf(':'); if(i>0) headers[line.Substring(0,i)]=line.Substring(i+1).Trim(); }
                    string host;
                    if(!headers.TryGetValue("Host",out host)||!Hosts.Contains(host.Split(':')[0]))
                    { Reply(stream,403,"{\"error\":\"Host not allowed\"}"); return; }
                    string origin;
                    if(headers.TryGetValue("Origin",out origin)&&origin!="http://"+host)
                    { Reply(stream,403,"{\"error\":\"Origin not allowed\"}"); return; }
                    string path=request[1].Split('?')[0];
                    var query=new Dictionary<string,string>();
                    int question=request[1].IndexOf('?');
                    if(question>=0)foreach(string pair in request[1].Substring(question+1).Split('&')) {
                        int equals=pair.IndexOf('=');if(equals>0)query[pair.Substring(0,equals)]=Uri.UnescapeDataString(pair.Substring(equals+1));
                    }
                    if(request[0]=="GET"&&path=="/api/trace/summary") {Reply(stream,200,Json.ToJson(CaptureTrace.Summary()));return;}
                    if(request[0]=="GET"&&path=="/api/trace") {
                        int offset=0,limit=1000;string value;
                        if(query.TryGetValue("offset",out value)&&!int.TryParse(value,out offset))throw new ArgumentException("Invalid offset");
                        if(query.TryGetValue("limit",out value)&&!int.TryParse(value,out limit))throw new ArgumentException("Invalid limit");
                        Reply(stream,200,Json.ToJson(CaptureTrace.Page(offset,limit)));return;
                    }
                    if(request[0]=="POST"&&path=="/api/trace/reset") {
                        object enabled;
                        if(!LocalSettings.LiveStatus.TryGetValue("output_enabled",out enabled)||!(enabled is bool)||((bool)enabled)||CaptureTrace.NativeInFlight) {
                            Reply(stream,409,"{\"error\":\"Disable capture and wait for output_enabled=false before resetting trace\"}");return;
                        }
                        string label,marker;query.TryGetValue("source",out label);query.TryGetValue("markers",out marker);
                        CaptureTrace.Reset(label??"unspecified",marker=="1");Reply(stream,200,"{\"ok\":true}");return;
                    }
                    if(request[0]=="GET"&&path=="/") { Reply(stream,200,_html,"text/html; charset=utf-8"); return; }
                    if(request[0]=="GET"&&path=="/layout") { Reply(stream,200,_layoutHtml,"text/html; charset=utf-8");return; }
                    if(request[0]=="GET"&&path=="/api/layout") { Reply(stream,200,Json.ToJson(CustomLayout.Current()));return; }
                    if(request[0]=="GET"&&path=="/api/capture-methods") { Reply(stream,200,Json.ToJson(CaptureMethods.Describe()));return; }
                    if(request[0]=="GET"&&path=="/api/settings")
                    { Reply(stream,200,Json.ToJson(LocalSettings.Snapshot())); return; }
                    if(request[0]=="GET"&&path=="/api/status")
                    {
                        var status=new Dictionary<string,object>(LocalSettings.LiveStatus);
                        status["version"]=ServiceHeartbeat.Version; status["url"]=Url;
                        status["settings_error"]=LocalSettings.Error;
                        Reply(stream,200,Json.ToJson(status)); return;
                    }
                    if(request[0]=="POST"&&(path=="/api/settings"||path=="/api/layout"))
                    {
                        string length,type;
                        int n;
                        if(!headers.TryGetValue("Content-Type",out type)||!type.StartsWith("application/json",StringComparison.OrdinalIgnoreCase)||
                           !headers.TryGetValue("Content-Length",out length)||!int.TryParse(length,out n)||n<2||n>131072||headers.ContainsKey("Transfer-Encoding"))
                        { Reply(stream,400,"{\"error\":\"Invalid JSON request\"}"); return; }
                        byte[] body=new byte[n]; int got=0;
                        while(got<n) { int take=stream.Read(body,got,n-got); if(take==0) throw new IOException("Incomplete body"); got+=take; }
                        string json=Encoding.UTF8.GetString(body);
                        if(path=="/api/layout") {
                            var doc=CustomLayout.Parse(json);
                            LocalSettings.Save(new Dictionary<string,object>{{"layoutMode","custom"},{"ledLayout",Json.ToJson(doc)},
                                {"ledCount",CustomLayout.Count(doc)},{"manualSides",false},{"mapOffset",0},{"mapReverse",false}});
                        } else LocalSettings.Save(ConfigFile.Parse(json));
                        Reply(stream,200,"{\"ok\":true}"); return;
                    }
                    Reply(stream,404,"{\"error\":\"Page not found\"}");
                }
                catch(ArgumentException e) { try{Reply(client.GetStream(),400,Json.ToJson(new Dictionary<string,object>{{"error",e.Message}}));}catch{} }
                catch(FormatException e) { try{Reply(client.GetStream(),400,Json.ToJson(new Dictionary<string,object>{{"error",e.Message}}));}catch{} }
                catch(Exception e) { try{Reply(client.GetStream(),500,Json.ToJson(new Dictionary<string,object>{{"error",e.Message}}));}catch{} }
            }
        }
    }
}
