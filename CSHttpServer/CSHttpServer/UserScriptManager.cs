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

        public Result(Actions action, NameValueCollection headers, int? statusCode, string location, Stream inputStream, long? streamLength)
        {
            Action = action;
            Headers = headers;
            StatusCode = statusCode;
            Location = location;
            InputStream = inputStream;
            StreamLength = streamLength ?? 0;
        }
    }

    public enum Actions : byte { HTML, SendFile, Text, Redirect, None }

    public enum SetResultOption : byte { Replace = 4, Append = 2, Ignore = 1 }

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

            public bool Contains(string value)
            {
                return _innerCollection.AllKeys.Contains(value);
            }
        }

        #region CONST

        private const int BUFFER_SIZE = 1024 * 256; //256KB

        #endregion

        protected readonly InnerNameValueCollection _GET;
        protected readonly InnerNameValueCollection _SERVER;
        protected readonly InnerNameValueCollection _REQUEST_HEADER;

        private MemoryStream __EchoStream;
        private StreamWriter __EchoWriter;

        private Actions __Action;
        private int? __StateCode;
        private string __Location;
        protected readonly NameValueCollection __Headers;

        public string ExtraText;

        private HttpContextHandler __ContextHandler;

        public Result Result
        {
            get
            {
                __EchoWriter?.Flush();
                return new Result(__Action, __Headers, __StateCode, __Location, __EchoStream, __EchoStream?.Length);
            }
        }

        private object[] __args;
        protected object[] _OTHER_ARGS;

        /// <summary>
        /// 可以被HttpServer動態編譯及執行
        /// </summary>
        /// <param name="args"></param>
        public BaseScript(params object[] args)
        {
            __args = args[0] as object[];
            _OTHER_ARGS = args[1] as object[];
            __ContextHandler = __args[0] as HttpContextHandler;
            _SERVER = new InnerNameValueCollection(__args[1] as NameValueCollection);
            _REQUEST_HEADER = new InnerNameValueCollection(__args[2] as NameValueCollection);
            if (__args.Length > 3)
            {
                _GET = new InnerNameValueCollection(__args[3] as NameValueCollection);
            }
            else
            {
                _GET = new InnerNameValueCollection(new NameValueCollection());
            }

            __Headers = new NameValueCollection();
        }

        private void InitStream()
        {
            if (__EchoStream != null) return;
            __EchoStream = new MemoryStream(BUFFER_SIZE);
            __EchoWriter = new StreamWriter(__EchoStream);
        }

        protected void echo(string str)
        {
            if (__EchoStream == null) InitStream();
            __EchoWriter.Write(str);
        }

        protected void echo(object obj)
        {
            if (__EchoStream == null) InitStream();
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

        protected BaseScript GetScript(string fileName, params object[] others)
        {
            return HttpServer.GetUserScript(fileName, __args, others);
        }

        public StreamReader GetInputStream()
        {
            return new StreamReader(__EchoStream);
        }

        protected void SetResult(Result result,
            SetResultOption outStreamOption = SetResultOption.Replace)
        {
            __Action = result.Action;
            __StateCode = result.StatusCode;
            __Location = result.Location;

            if (result.InputStream != null &&
                outStreamOption != SetResultOption.Ignore)
            {
                if (__EchoStream == null)
                {
                    __EchoStream = result.InputStream as MemoryStream;
                    __EchoWriter = new StreamWriter(__EchoStream);
                }
                else
                {
                    if (outStreamOption == SetResultOption.Replace)
                    {
                        __EchoWriter.Close();
                        __EchoStream = result.InputStream as MemoryStream;
                        __EchoWriter = new StreamWriter(__EchoStream);
                    }
                    else
                    {
                        __EchoWriter.Flush();
                        (result.InputStream as MemoryStream).WriteTo((__EchoStream));
                    }
                }
            }

            __Headers.Clear();
            foreach (var k in result.Headers.AllKeys)
            {
                __Headers.Set(k, result.Headers[k]);
            }
        }

        public void Dispose()
        {
            __EchoWriter?.Flush();
            __EchoWriter?.Close();
            __EchoStream?.Close();
        }

        public abstract bool Run();
    }
}
