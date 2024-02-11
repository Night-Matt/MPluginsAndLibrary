using MPlugin.Untruned.MPlugin.API;
using MPlugin.Untruned.MPlugin.Core;
using Newtonsoft.Json;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MP.HttpListenner.HttpListenerHelper;
using static System.Net.WebRequestMethods;

namespace MP.HttpListenner
{
    [MPluginInfo(AuthorName = "Matt", ContactInformation = "QQ: 2472285384", PluginName = "HttpListener")]
    public class Beta : MPlugin<Config>
    {
        HttpListenerHelper http;
        internal static Queue<httpRequestDelegateQueue> tasks = new Queue<httpRequestDelegateQueue>();
        [MPlugin(Type = EMPluginStateType.Load)]
        public void Load()
        {
            http = new HttpListenerHelper();
            http.AddPrefix(Configuration.listenerUrlAddress);
            http.Start();
        }

        void FixedUpdate()
        {
            if (tasks.Count != 0)
            {
                lock (tasks)
                {
                    httpRequestDelegateQueue queue = tasks.Dequeue();
                    HttpResponArgs responArgs = queue.responseArgs;
                    queue.Delegate?.Invoke(queue.requestArgs, ref responArgs);
                    // 设置响应的内容和状态码
                    string responseString = JsonConvert.SerializeObject(responArgs, Formatting.Indented);
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    queue.Response.ContentType = "application/json";
                    queue.Response.ContentLength64 = buffer.Length;
                    queue.Response.StatusCode = 200;
                    // 写入响应的正文
                    System.IO.Stream output = queue.Response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    // 关闭响应
                    output.Close();
                }
            }
        }

        [MPlugin(Type = EMPluginStateType.Unload)]
        public void Unload()
        {
            http.Stop();
        }
    }

    public class Config : IMPluginConfig
    {
        public string listenerUrlAddress;
        public void LoadDefault()
        {
            listenerUrlAddress = "http://localhost/command/";
        }
    }
}
