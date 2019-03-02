using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSHttpServer;
using System.Collections.Specialized;

namespace UserScript
{
    public struct Result
    {
        //表示Server接受Result後應該做的動作
        public readonly Actions Action;

        //表示Server應回傳Client何種狀態碼
        public readonly int? StatusCode;

        //Action所需的資源路徑，或Http Status Code 3XX 重新導向路徑
        public readonly string Location;

        public readonly Stream InputStream;

        public readonly long StreamLength;

        public readonly NameValueCollection Headers;

        public Result(Actions action, NameValueCollection headers, int? statusCode, string location, Stream inputStream, long streamLength)
        {
            Action = action;
            Headers = headers;
            StatusCode = statusCode;
            Location = location;
            InputStream = inputStream;
            StreamLength = streamLength;
        }
    }

    public enum Actions : byte { HTML, SendFile, Text, Redirect, None }

    public abstract class BaseScript : IDisposable
    {
        protected class InnerNameValueCollection
        {
            public string this[string key]
            {
                get
                {
                    return _innerCollection[key] ?? string.Empty;
                }
            }

            private readonly NameValueCollection _innerCollection;

            public InnerNameValueCollection(NameValueCollection innerCollection)
            {
                _innerCollection = innerCollection;
            }
        }

        protected InnerNameValueCollection _GET;
        protected InnerNameValueCollection _SERVER;
        protected InnerNameValueCollection _REQUEST_HEADER;

        private readonly MemoryStream __EchoStream;
        private readonly StreamWriter __EchoWriter;

        private Actions __Action;
        private int? __StateCode;
        private string __Location;
        protected readonly NameValueCollection __Headers;

        private HttpContextHandler __ContextHandler;

        public Result Result
        {
            get
            {                
                __EchoWriter.Flush();
                return new Result(__Action, __Headers, __StateCode, __Location, __EchoStream, __EchoStream.Length);
            }
        }

        public BaseScript(object[] args)
        {
            __ContextHandler =  args[0] as HttpContextHandler;
            _SERVER = new InnerNameValueCollection(args[1] as NameValueCollection);
            _REQUEST_HEADER = new InnerNameValueCollection(args[2] as NameValueCollection);
            if (args.Length > 3)
            {
                _GET = new InnerNameValueCollection(args[3] as NameValueCollection);
            }

            __Headers = new NameValueCollection();
            __EchoStream = new MemoryStream(1024 * 256);
            __EchoWriter = new StreamWriter(__EchoStream);
        }

        protected void echo(string str)
        {
            __EchoWriter.Write(str);
        }

        protected void echo(dynamic obj)
        {
            __EchoWriter.Write(obj);
        }

        #region Log Methods
        protected void Log(string str, bool console = true)
        {
            __ContextHandler.Log(str, console);
        }

        protected void Log(object obj, bool console = true)
        {
            __ContextHandler.Log(obj, console);
        }

        protected void LogLine(string str, bool console = true)
        {
            __ContextHandler.Log(str, console);
        }

        protected void LogLine(object obj, bool console = true)
        {
            __ContextHandler.Log(obj, console);
        } 
        #endregion

        protected void Header(string name, string value, bool replace = true)
        {
            if (replace) __Headers.Set(name, value);
            else __Headers.Add(name, value);
        }

        protected void Action(Actions action)
        {
            __Action = action;
        }

        protected void StatusCode(int code)
        {
            __StateCode = code;
        }

        protected void Location(string location)
        {
            __Location = location;
        }

        public StreamReader GetInputStream()
        {
            return new StreamReader(__EchoStream);
        }

        public void Dispose()
        {
            __EchoWriter.Flush();
            __EchoWriter.Close();
            __EchoStream.Close();
        }

        public abstract bool Run();
    }
}
