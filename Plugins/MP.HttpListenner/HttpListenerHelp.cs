using MPlugin.Untruned.MPlugin.Core;
using Newtonsoft.Json;
using SDG.Framework.IO.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static MP.HttpListenner.HttpListenerHelper;

namespace MP.HttpListenner
{
    // 定义一个事件参数类，用来传递请求的信息
    public class HttpRequestArgs
    {
        /// <summary>
        /// 绝对URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 请求方法类型
        /// </summary>
        public string MethodType { get; set; }

        /// <summary>
        /// 请求参数
        /// </summary>
        public NameValueCollection Parameters { get; set; }
    }
    public class HttpResponArgs
    {
        public string Instructions {  get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }
    public class httpRequestDelegateQueue
    {
        public httpRequestDelegate Delegate {  get; set; }
        public HttpRequestArgs requestArgs {  get; set; }
        public HttpResponArgs responseArgs {  get; set; }
        public HttpListenerRequest Request { get; set; }
        public HttpListenerResponse Response { get; set; }
    }

    // 定义一个类，用来封装http监听和处理的功能
    public class HttpListenerHelper
    {
        // 声明一个事件，用来在收到请求时通知订阅者
        public delegate void httpRequestDelegate(HttpRequestArgs request,ref HttpResponArgs response);
        public static event httpRequestDelegate OnReceiveHttpRequest;

        // 声明一个HttpListener对象，用来监听http请求
        private HttpListener listener;

        // 定义一个构造函数，用来初始化HttpListener对象和事件
        public HttpListenerHelper()
        {
            listener = new HttpListener();
        }

        // 定义一个方法，用来添加需要监听的URL前缀
        public void AddPrefix(string prefix)
        {
            listener.Prefixes.Add(prefix);
        }

        // 定义一个方法，用来启动监听
        public void Start()
        {
            listener.Start();
            MLog.LogWarning("HttpListenner Start Listening...");
            new Thread(ListenAsync).Start();
        }

        // 定义一个方法，用来停止监听
        public void Stop()
        {
            listener.Stop();
            MLog.LogWarning("HttpListenner Already Stopped.");
        }

        // 定义一个异步方法，用来循环处理请求
        private async void ListenAsync()
        {
            while (true)
            {
                // 异步获取一个HttpListenerContext对象，表示一个客户端请求
                HttpListenerContext context = await listener.GetContextAsync();
                // 获取请求和响应的对象
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                // 获取请求的方法
                string method = request.HttpMethod;

                // 获取请求的 URL
                string url = request.Url.AbsolutePath;

                NameValueCollection par = null;
                Dictionary<string, string> parameter = new Dictionary<string, string> { };

                if (method == "GET")
                {
                    // 如果是 GET 方法，从查询字符串中获取参数
                    par = request.QueryString;
                }
                else if (method == "POST")
                {
                    // 如果是 POST 方法，从请求体中获取参数
                    // 首先，读取请求体中的数据
                    string content = await new StreamReader(request.InputStream, request.ContentEncoding).ReadToEndAsync();

                    // 然后，根据内容类型解析参数
                    string contentType = request.ContentType;

                    if (contentType != "application/x-www-form-urlencoded")
                    {
                        // 如果内容类型是其他类型，您需要自己实现解析逻辑
                        par.Add("content", content);
                    }
                }

                HttpRequestArgs req = new HttpRequestArgs
                {
                    Url = request.Url.ToString(),
                    MethodType = method,
                    Parameters = par
                };


                HttpResponArgs rep = new HttpResponArgs
                {
                    Instructions = "欢迎使用MPlugin的HttpListennerHelp服务,这个属性是说明属性，可以写上你的说明，以下为响应的键值~",
                    Parameters = parameter
                };

                Beta.tasks.Enqueue(new httpRequestDelegateQueue
                {
                    Delegate = OnReceiveHttpRequest,
                    requestArgs = req,
                    responseArgs = rep,
                    Request = request,
                    Response = response
                });
            }
        }

    }
}

