using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Web;
using System.Reflection;
using UserScript;
using System.Collections.Specialized;

namespace CSHttpServer
{
    public class HttpServer
    {
        private HttpListener Server;

        public static readonly Encoding REQUEST_HEADER_ENCODING = Encoding.GetEncoding("ISO-8859-1");
        public const int BUFFER_SIZE = 2 * 1024 * 1024; // 1MB

        private static RuntimeCompiler Compiler;
        private Dictionary<string, string> UserList;

        static readonly string[] NEWLINE_SPLIT_STRING = { Environment.NewLine };

        struct UserScript
        {
            public DateTime LastModified;
            public Type Type;
        }
        private static Dictionary<string, UserScript> UserScriptList;

        //主要監聽Port
        public readonly int Port;

        //以此Port監聽本地連線，若為-1則使用主要Port
        public int LocalPort = -1;

        //是否啟用身分驗證
        public bool UseAuthentication
        {
            get
            {
                return Server.AuthenticationSchemes != AuthenticationSchemes.Anonymous;
            }
            set
            {
                Server.AuthenticationSchemes =
                    value ? AuthenticationSchemes.Basic : AuthenticationSchemes.Anonymous;
            }
        }

        //設定、讀取使用者帳戶清單
        public string AuthenticationUserFile
        {
            set
            {
                string text = File.ReadAllText(value);
                UserList = new Dictionary<string, string>();
                foreach (string u in text.Split(NEWLINE_SPLIT_STRING, StringSplitOptions.None))
                {
                    var tmp = u.Split(' ');
                    UserList.Add(tmp[0], tmp[1]);
                }
            }
        }

        private static bool __UseLog = true;
        public bool UseLog
        {
            get { return __UseLog; }
            set { __UseLog = value; }
        }

        private const string LogPath = "logs/";
        private const string LogFileName = "L-";

        private static FileInfo __LogFile;
        private static StreamWriter __LogWriter;

        /// <summary>
        /// 取得執行中的HttpContextHandler數量
        /// </summary>
        public int RunningHandler { get; private set; }

        //建構方法
        //傳入監聽Port
        public HttpServer(int port)
        {
            Port = port;
            Server = new HttpListener();
            Server.Prefixes.Add("http://*:" + Port + '/');

#if !DEBUG
            Server.IgnoreWriteExceptions = true;
#endif

            UserScriptList = new Dictionary<string, UserScript>();
        }

        /// <summary>
        /// 從指定位置載入.cs源碼，編譯後置入Dictionary並回傳
        /// <para>若該檔案在Dictionary有編譯完成的成品則直接回傳</para>
        /// <para>檔案最後修改時間有異動時重新編譯</para>
        /// </summary>
        /// <param name="fileName">檔案路徑</param>
        /// <param name="args">建構方法參數，用以生成腳本實體</param>
        /// <returns>腳本實體</returns>
        public static BaseScript GetUserScript(string fileName, object[] args)
        {
            var fileInfo = new FileInfo(fileName);
            Type type;
            if (UserScriptList.Keys.Contains(fileInfo.FullName) &&
                UserScriptList[fileInfo.FullName].LastModified == fileInfo.LastWriteTime)
            {
                type = UserScriptList[fileInfo.FullName].Type;
            }
            else
            {
                type = CompilingScript(fileInfo);
            }
            return Activator.CreateInstance(type, new object[] { args }) as BaseScript;
        }

        private static Type CompilingScript(FileInfo fileInfo)
        {
            if (Compiler == null) Compiler = new RuntimeCompiler();

            Assembly asb = Compiler.Compiling(fileInfo);
            var type = asb.GetType("UserScript.Script");

            var uscript = new UserScript()
            {
                LastModified = fileInfo.LastWriteTime,
                Type = type
            };
            UserScriptList[fileInfo.FullName] = uscript;
            return type;
        }

        //啟動Server
        public void Start()
        {
            LocalPort = LocalPort < 0 ? Port : LocalPort;
            Server.Prefixes.Add("http://127.0.0.1:" + LocalPort + '/');
            Server.Prefixes.Add("http://localhost:" + LocalPort + '/');

            if (__UseLog)
            {
                if (!Directory.Exists(LogPath)) Directory.CreateDirectory(LogPath);
                __LogFile = new FileInfo(
                    LogPath + LogFileName + DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
            }

            Server.Start();
            Server.BeginGetContext(new AsyncCallback(OnGetContext), Server);
        }

        public void Stop()
        {
            Server.Stop();
        }

        #region Log Methods
        public static void Log(string str, bool show = true)
        {
            if (show) Console.Write(str);
            if (__UseLog)
            {
                __LogWriter = __LogFile.AppendText();
                __LogWriter.Write(str);
                __LogWriter.Close();
            }
        }

        public static void Log(object obj, bool show = true)
        {
            if (show) Console.Write(obj);
            if (__UseLog)
            {
                __LogWriter = __LogFile.AppendText();
                __LogWriter.Write(obj);
                __LogWriter.Close();
            }
        }

        public static void LogLine(string str, bool show = true)
        {
            if (show) Console.WriteLine(str);
            if (__UseLog)
            {
                __LogWriter = __LogFile.AppendText();
                __LogWriter.WriteLine(str);
                __LogWriter.Close();
            }
        }

        public static void LogLine(object obj, bool show = true)
        {
            if (show) Console.WriteLine(obj);
            if (__UseLog)
            {
                __LogWriter = __LogFile.AppendText();
                __LogWriter.WriteLine(obj);
                __LogWriter.Close();
            }
        }
        #endregion

        //接收Request並驗證身分
        private void OnGetContext(IAsyncResult ar)
        {
            HttpListener server = ar.AsyncState as HttpListener;

            try
            {
                HttpListenerContext context = server.EndGetContext(ar);
                server.BeginGetContext(new AsyncCallback(OnGetContext), server);

                if (UseAuthentication)
                {
                    var identity = context.User.Identity as HttpListenerBasicIdentity;
                    if (identity == null ||
                        !UserList.ContainsKey(identity.Name) ||
                        UserList[identity.Name] != identity.Password)
                    {
                        LogLine(
                            "\nRefuse request from: " + context.Request.RemoteEndPoint.Address +
                            "\n　　Authentication Fail");
                        context.Response.StatusCode = 401;
                        context.Response.Close();
                        return;
                    }
                }
                var handler = new HttpContextHandler(context, __UseLog);
            }
            catch { }
        }
    }


    /// <summary>
    /// 獨立處理HttpListenerContext
    /// </summary>
    class HttpContextHandler
    {
        static readonly char[] QUERY_SPLIT_CHAR = { '=' };

        HttpListenerContext Context;
        HttpListenerRequest Request;
        HttpListenerResponse Response;

        string UrlWithoutQuery;
        string LocalUrlWithoutQuery;
        string RemoteAddress;
        NameValueCollection QueryList;

        bool __UseLog;
        StringBuilder __Log;
        string __LogHeader;

#if DEBUG
        bool __ShowError = true;
#else
        bool __ShowError = false;
#endif

        public HttpContextHandler(HttpListenerContext context, bool log)
        {
            Context = context;
            Request = context.Request;
            RemoteAddress = Request.RemoteEndPoint.ToString();

            if (__UseLog = log)
            {
                __Log = new StringBuilder();
            }

            __LogHeader = RemoteAddress + " - ";
            if (context.User?.Identity != null)
            {
                __LogHeader += "[" + context.User.Identity.Name + "] ";
            }

            try
            {
                Log(__LogHeader, false);
                LogLine("New request - " + DateTime.Now.ToLocalTime());

                using (Response = context.Response)
                {
                    var url = Request.Url.ToString();
                    var localUrl = Request.Url.LocalPath.ToString();

                    var qmi = url.IndexOf('?');
                    if (qmi > 0) url = url.Substring(0, qmi);
                    UrlWithoutQuery = url;

                    qmi = localUrl.IndexOf('?');
                    if (qmi > 0) localUrl = localUrl.Substring(0, qmi);
                    LocalUrlWithoutQuery = localUrl;

                    QueryList = GetQueries();

                    DoResponse();
                }
            }
            catch (Exception e)
            {
                LogLine(e.ToString(), __ShowError);
            }
            finally
            {
                var r = "R-" + Response.StatusCode;
                if (Response.StatusCode % 400 < 100) r += "：\"" + Request.Url.ToString() + '\"';
                r += " - " + DateTime.Now.ToLocalTime();
                LogLine(r + '\n');
                if (__UseLog) HttpServer.Log(__Log.ToString(), false);
            }
        }

        //處理Response
        private void DoResponse()
        {
            try
            {
                var path = Environment.CurrentDirectory + LocalUrlWithoutQuery;
                var csi = path.IndexOf(".cs");
                var csp = csi > 0 ? path.Substring(0, csi + 3) : null;

                if (csp != null && File.Exists(csp))
                {
                    RunUserScripts(csp);
                }
                else if (File.Exists(path))
                {
                    if (path.EndsWith(".cs"))
                    {
                        RunUserScripts(path);
                    }
                    else
                    {
                        FileTransport(path);
                    }
                }
                else
                {
                    ProgressQueries();
                }
            }
            catch (FileNotFoundException)
            {
                ResponsePageNotFound();
            }
            catch (HttpListenerException e)
            {
                if (e.NativeErrorCode != 64 && e.NativeErrorCode != 995 &&
                    e.NativeErrorCode != 6)
                {
                    LogLine(e.ToString(), __ShowError);
                    LogLine("ErrorCode: " + e.NativeErrorCode, __ShowError);
                }
            }
            catch (Exception e)
            {
                LogLine(e.ToString(), __ShowError);
                Response.StatusCode = 500;
            }
        }

        public object[] GetScriptParams(string filePath = null)
        {
            var param = new NameValueCollection
            {
                { "url", UrlWithoutQuery },
                { "local_url", LocalUrlWithoutQuery },
                { "remote-address", RemoteAddress }
            };
            if (filePath != null)
                param.Add("file-path", new FileInfo(filePath).Name);

            return QueryList == null ?
                new object[] { this, param, Request.Headers } :
                new object[] { this, param, Request.Headers, QueryList };
        }

        /// <summary>
        /// 執行腳本
        /// </summary>
        /// <param name="fileName">檔案路徑</param>
        public void RunUserScripts(string fileName)
        {
            var objs = GetScriptParams(fileName);
            RunUserScripts(HttpServer.GetUserScript(fileName, objs));
        }

        public void RunUserScripts(BaseScript script)
        {
            using (script)
            {
                if (!script.Run()) return;
                ProgressScriptResult(script.Result);
            }
        }

        /// <summary>
        /// 處理腳本返回結果
        /// </summary>
        /// <param name="result">腳本返回結果</param>
        private void ProgressScriptResult(Result result)
        {
            foreach (var par in result.Headers.AllKeys)
            {
                Response.AddHeader(par, result.Headers[par]);
            }

            if (result.StatusCode != null) Response.StatusCode = result.StatusCode.Value;

            switch (result.Action)
            {
                case Actions.SendFile:
                    FileTransport(result.Location);
                    break;
                case Actions.Redirect:
                    Response.StatusCode = 307;
                    Response.RedirectLocation = result.Location;
                    break;
                case Actions.None:
                    break;
                default:
                    ResponseNormal(result.InputStream, result.StreamLength);
                    break;
            }
        }

        /// <summary>
        /// 針對各個Query做處理
        /// </summary>
        private void ProgressQueries()
        {
            if (QueryList == null)
            {
                ResponsePageNotFound();
                return;
            }

            foreach (var k in QueryList.AllKeys)
            {
                switch (k)
                {
                    case "fp":
                        string fileName = QueryList[k] ?? throw new FileNotFoundException();
                        if (Directory.Exists(fileName)) throw new FileNotFoundException();
                        FileTransport(fileName);
                        break;
                }
            }
        }

        /// <summary>
        /// 將Query整理成Dictionary回傳
        /// </summary>
        private NameValueCollection GetQueries()
        {
            if (Request.Url.Query.Length == 0) return null;
            var qstr = Request.Url.Query.Substring(1);
            if (qstr.Length > 0)
            {
                NameValueCollection queries = new NameValueCollection();
                var qs = qstr.Split('&');
                foreach (string q in qs)
                {
                    var tmp = q.Split(QUERY_SPLIT_CHAR, 2);
                    queries.Add(HttpUtility.UrlDecode(tmp[0]), HttpUtility.UrlDecode(tmp[1]));
                }
                return queries;
            }
            return null;
        }

        /// <summary>
        /// 一般Response。設定內容長度，並將內容寫入Response Stream，無其他動作。
        /// </summary>
        /// <param name="input">輸入串流</param>
        /// <param name="length">內容長度</param>
        private void ResponseNormal(Stream input, long length)
        {
            Response.ContentLength64 = length;
            WriteStream(input, Response.OutputStream, 0, length);
        }

        /// <summary>
        /// 傳輸檔案
        /// </summary>
        /// <param name="fileName">檔案路徑</param>
        /// <param name="range">以"xxx-xxx"為格式，表示檔案範圍，單位為byte</param>
        private void FileTransport(string fileName, string range = null)
        {
            Response.AddHeader("Accept-Ranges", "bytes");
            long offset = 0;
            long end = -1;

            var reqRange = Request.Headers["Range"];
            if (reqRange != null)
            {
                range = reqRange;
                Response.StatusCode = 206;
            }

            try
            {
                if (range != null)
                {
                    var tmp = range.Split('=')[1].Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    offset = long.Parse(tmp[0]);
                    end = tmp.Length > 1 ? long.Parse(tmp[1]) : -1;
                }
            }
            catch
            {
                Response.StatusCode = 416;
                return;
            }

            ResponseMIME(fileName);
            ResponseFile(fileName, offset, end);
        }

        /// <summary>
        /// 將檔案MIME type寫入Response Header
        /// </summary>
        /// <param name="fileName">檔案路徑</param>
        private void ResponseMIME(string fileName)
        {
            string mimeType = MIMEDetector.GetMIMEType(fileName);
            Response.ContentType = mimeType;
            if (!mimeType.ToLower().Contains("text/html"))
            {
                string tmp = "inline; filename=\"" + new FileInfo(fileName).Name + "\"";
                tmp = HttpServer.REQUEST_HEADER_ENCODING.GetString(Encoding.UTF8.GetBytes(tmp));
                Response.AddHeader("Content-Disposition", tmp);
            }
        }

        /// <summary>
        /// 將檔案寫入Response Stream，可設定起點與終點
        /// </summary>
        /// <param name="fileName">檔案路徑</param>
        /// <param name="start">起始位置</param>
        /// <param name="end">結束位置</param>
        private void ResponseFile(string fileName, long start, long end)
        {
            //Response.SendChunked = false;

            using (FileStream fs = File.OpenRead(fileName))
            {
                Stream os = Response.OutputStream;
                long fileLength = fs.Length;
                end = end < 0 ? fileLength - 1 : end;
                long contentLength = end - start + 1;

                Response.ContentLength64 = contentLength;
                Response.AddHeader("Content-Range", "bytes " + start + "-" + end + "/" + fs.Length);
                Response.AddHeader("Last-Modified", new FileInfo(fileName).LastWriteTime.ToString("r"));
                WriteStream(fs, Response.OutputStream, start, contentLength);
            }
        }

        /// <summary>
        /// 將輸入串流寫至輸出串流，可自訂範圍
        /// </summary>
        /// <param name="input">輸入串流</param>
        /// <param name="output">輸出串流</param>
        /// <param name="start">起始位置</param>
        /// <param name="length">內容長度</param>        
        private void WriteStream(Stream input, Stream output, long start, long length)
        {
            if(length > input.Length - start)
            {
                Response.StatusCode = 416;
                return;
            }

            long bytesWrote = 0;
            long buffSize = length < HttpServer.BUFFER_SIZE ? length : HttpServer.BUFFER_SIZE;
            byte[] buff = new byte[buffSize];

            LogLine("Transport, Start at " + start + ", length= " + length);

            try
            {
                input.Seek(start, SeekOrigin.Begin);
                while (bytesWrote < length && input.Position < input.Length)
                {
                    int count = input.Read(buff, 0, buff.Length);
                    output.Write(buff, 0, count);
                    bytesWrote += count;
                }
                LogLine("Transport End: Wrote " + bytesWrote + " bytes");
            }
            catch (Exception e)
            {
#if DEBUG
                LogLine("Transport Fail: " + e.Message);
#else
                LogLine("Transport Fail");
#endif
            }
        }

        #region Log Methods
        public void Log(string str, bool console = true)
        {
            if (console) Console.Write(__LogHeader + str);
            if (__UseLog) __Log.Append(str);
        }

        public void Log(object obj, bool console = true)
        {
            var s = obj.ToString();
            if (console) Console.Write(__LogHeader + obj.ToString());
            if (__UseLog) __Log.Append(s);
        }

        public void LogLine(string str, bool console = true)
        {
            if (console) Console.WriteLine(__LogHeader + str);
            if (__UseLog) __Log.AppendLine(str);
        }

        public void LogLine(object obj, bool console = true)
        {
            var s = obj.ToString();
            if (console) Console.WriteLine(__LogHeader + s);
            if (__UseLog) __Log.AppendLine(s);
        }
        #endregion

        private void ResponsePageNotFound()
        {
            Response.StatusCode = 404;
        }
    }
}
