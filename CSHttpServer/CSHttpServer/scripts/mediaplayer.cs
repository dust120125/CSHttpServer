using CSHttpServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UserScript
{
    public class Script : BaseScript
    {
        public Script(params object[] args) : base(args)
        {

        }

        const string GetFile_ScriptName = "dir.cs";

        public override bool Run()
        {
            string path = null;
            var noitem = _GET.Contains("noitem");
            if (!noitem)
            {
                if (_OTHER_ARGS.Length < 1) return false;
                if ((path = _OTHER_ARGS[0] as string) == null) return false;
            }

            var htmlPath = _SERVER["file-path"] + "mediaplayer.html";
            string htmlModifiedTime = new FileInfo(htmlPath).LastWriteTime.ToString("r");
            Header("Last-Modified", htmlModifiedTime);

            if (noitem)
            {
                if (_REQUEST_HEADER["if-modified-since"] == htmlModifiedTime)
                {
                    StatusCode(304); //回應Client不須重新取得資源
                    Action(Actions.None);
                    return true;
                }
                echo(GetHtml(htmlPath));
                Action(Actions.HTML);
                Header("content-type", "text/html;charset:utf8");
                return true;
            }

            string modifiedTime = new DirectoryInfo(path).LastWriteTime.ToString("r");
            Header("Last-Modified", modifiedTime, false);

            if (_REQUEST_HEADER["if-modified-since"].Contains(modifiedTime) &&
                _REQUEST_HEADER["if-modified-since"].Contains(htmlModifiedTime))
            {
                StatusCode(304); //回應Client不須重新取得資源
                Action(Actions.None);
                return true;
            }

            //var files = Directory.GetFiles(path);

            //StringBuilder sb = new StringBuilder();
            //foreach (var file in files)
            //{
            //    var type = MIMEDetector.GetMIMEType(file);
            //    if (type.StartsWith("audio") || type.StartsWith("video"))
            //    {
            //        sb.Append("<div class=\"media-item\"><span onclick=\"item_onclick(this)\">");
            //        sb.Append(new FileInfo(file).Name);
            //        sb.Append("</span></div>");
            //    }
            //}
            //if (sb.Length == 0) return false;

            var js = GetItemJS(path);
            if (js == string.Empty) return false;
            ExtraText = GetHtml(htmlPath, string.Empty, js);
            return true;
        }

        private string GetHtml(string htmlPath, string items = "", string extra = "")
        {
            var html = File.ReadAllText(htmlPath);
            var htmlb = new StringBuilder();
            var cssp = "/" + _SERVER["file-path"] + "mediaplayer.css";
            var jsp = "/" + _SERVER["file-path"] + "mediaplayer.js";
            extra += "<script type='text/javascript'>var dirScriptPath=\"" + _SERVER["file-path"] + GetFile_ScriptName + "\";</script>";

            htmlb.AppendFormat(html, cssp, items, jsp, extra);
            return htmlb.ToString();
        }

        private string GetItemJS(string path)
        {
            var files = Directory.GetFiles(path);

            StringBuilder sb = new StringBuilder();
            int count = 0;
            foreach (var file in files)
            {
                count++;
                var fileInfo = new FileInfo(file);
                var type = MIMEDetector.GetMIMEType(fileInfo);
                if (type.StartsWith("audio/") || type.StartsWith("video/"))
                {
                    sb.Append('"');
                    sb.Append(fileInfo.Name);
                    if (count < files.Length) sb.Append("\",");
                    else sb.Append('"');
                }
            }
            if (count == 0) return string.Empty;
            return string.Format(ITEM_JS, path, sb.ToString());
        }

        const string ITEM_JS =
            "<script type='text/javascript'>" +
            "var __items={{'path':\"{0}\",'items':[{1}]}};" +
            "batchAddMedia(__items);</script>";

    }
}
