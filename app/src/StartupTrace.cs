using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
namespace TizenAmbilight { public static class StartupTrace {
        public static string ApplicationId="unknown";
        public static string Endpoint=""; // Diagnostic receiver is opt-in; never a hardcoded developer PC.
        public static void Send(string phase,Exception error=null)
        {
            string endpoint=Endpoint;if(string.IsNullOrEmpty(endpoint))return;
            ThreadPool.QueueUserWorkItem(_=>{
                try {
                    var body=Json.ToJson(new Dictionary<string,object>{{"application_id",ApplicationId},{"app","io.github.tenda96.TizenHalo"},{"version",ServiceHeartbeat.Version},{"phase",phase},{"error",error==null?"":error.ToString()}});
                    var bytes=Encoding.UTF8.GetBytes(body);
                    var req=(HttpWebRequest)WebRequest.Create(endpoint);
                    req.Method="POST";req.ContentType="application/json";req.Timeout=1500;req.ReadWriteTimeout=1500;req.ContentLength=bytes.Length;
                    using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);
                    using(var response=req.GetResponse()){}
                }catch{}
            });
        }
}}
