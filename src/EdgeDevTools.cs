// A small client for Edge's DevTools protocol (the video player's Edge is started with a local debugging port):
// it lets the launcher style the YouTube page so only the video shows, and see when the video goes fullscreen.
// Only 127.0.0.1 is used; the port is a free one picked at start.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AzerothLauncher
{
    class EdgeDevTools : IDisposable
    {
        ClientWebSocket ws;
        int nextId = 1;

        // a free local TCP port
        public static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        // connects to the first page of the Edge listening on the port; false if there is none (yet)
        public bool Connect(int port)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/json/list");
                req.Proxy = null;
                req.Timeout = 2000;
                string json;
                using (var r = req.GetResponse())
                using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
                var m = Regex.Match(json, "\"type\"\\s*:\\s*\"page\"[^{}]*?\"webSocketDebuggerUrl\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) return false;
                ws = new ClientWebSocket();
                ws.Options.Proxy = null;
                if (!ws.ConnectAsync(new Uri(m.Groups[1].Value), CancellationToken.None).Wait(4000)) return false;
                return ws.State == WebSocketState.Open;
            }
            catch (Exception) { return false; }
        }

        public bool Open { get { return ws != null && ws.State == WebSocketState.Open; } }

        // sends a command and waits for its answer (events in between are skipped); null on failure
        public string Call(string method, string paramsJson)
        {
            if (!Open) return null;
            int id = nextId++;
            string msg = "{\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + (paramsJson ?? "{}") + "}";
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(msg);
                if (!ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) return null;
                var buf = new byte[65536];
                DateTime until = DateTime.Now.AddSeconds(5);
                while (DateTime.Now < until)
                {
                    var ms = new MemoryStream();
                    WebSocketReceiveResult res;
                    do
                    {
                        var t = ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                        if (!t.Wait(5000)) return null;
                        res = t.Result;
                        if (res.MessageType == WebSocketMessageType.Close) return null;
                        ms.Write(buf, 0, res.Count);
                    } while (!res.EndOfMessage);
                    string text = Encoding.UTF8.GetString(ms.ToArray());
                    if (Regex.IsMatch(text, "^\\{\"id\":" + id + "[,}]")) return text;
                }
            }
            catch (Exception) { }
            return null;
        }

        // runs a script in the page; returns the answer's JSON (or null)
        public string Eval(string script)
        {
            return Call("Runtime.evaluate", "{\"expression\":" + Quote(script) + ",\"returnByValue\":true}");
        }

        // runs a script in every page the tab loads from now on
        public void OnEveryPage(string script)
        {
            Call("Page.addScriptToEvaluateOnNewDocument", "{\"source\":" + Quote(script) + "}");
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        public void Dispose()
        {
            try { if (ws != null) ws.Abort(); } catch (Exception) { }
            try { if (ws != null) ws.Dispose(); } catch (Exception) { }
            ws = null;
        }
    }
}
