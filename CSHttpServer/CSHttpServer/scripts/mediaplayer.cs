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

        public override bool Run()
        {
            string path;
            if (_OTHER_ARGS.Length == 0) return false;
            if ((path = _OTHER_ARGS[0] as string) == null) return false;

            var files = Directory.GetFiles(path);

            StringBuilder sb = new StringBuilder();
            foreach (var file in files)
            {
                var type = MIMEDetector.GetMIMEType(file);
                if (type.StartsWith("audio") || type.StartsWith("video"))
                {
                    sb.Append("<div class=\"media-item\"><a href=\"javascript:;\" onclick=\"item_onclick(this)\">");
                    sb.Append(new FileInfo(file).Name);
                    sb.Append("</a></div>");
                }
            }
            if (sb.Length == 0) return false;

            var html = File.ReadAllText(_SERVER["file-path"] + "mediaplayer.html");
            var htmlb = new StringBuilder(html);
            htmlb.Replace("<!--MediaItems-->", sb.ToString());
            htmlb.Replace("<!--CSS-->", "/" + _SERVER["file-path"] + "mediaplayer.css");
            htmlb.Replace("<!--JS-->", "/" + _SERVER["file-path"] + "mediaplayer.js");
            ExtraText = htmlb.ToString();

            return true;
        }
    }
}
