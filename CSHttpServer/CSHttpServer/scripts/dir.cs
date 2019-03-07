using CSHttpServer;
using System;
using System.IO;
using System.Text;
//using System.Collections.Generic;

namespace UserScript
{
    public class Script : BaseScript
    {
        private static string[] Sizes = { "B", "KB", "MB", "GB", "TB" };
        private static char[] PathTrimChars = { '/' };
        private static char[] SimpleSpiltChar = { ',' };
        private const int MAX_FILENAME_LENGTH = 48;

        private string MediaMode = "none";

        public Script(params object[] args) : base(args)
        {

        }

        public override bool Run()
        {
            Header("content-type", "text/html;charset=UTF-8");

            var url = _SERVER["url"];
            var rawUrl = _SERVER["local_url"];
            var path = "";
            var csi = rawUrl.IndexOf(".cs");
            if (rawUrl.Length > csi + 3)
                path = rawUrl.Substring(csi + 3);
            var cleanPath = path.Trim(PathTrimChars);
            //Log("Dir: " + path);

            if (_GET.Contains("gt"))
            {
                var types = _GET["gt"].Split(SimpleSpiltChar, StringSplitOptions.RemoveEmptyEntries);
                if (!GenerateFileListJson(cleanPath, types.Length > 0 ? types : null))
                {
                    StatusCode(204);
                    Action(Actions.None);
                    return true;
                }
                Action(Actions.Text);
                return true;
            }

            if (_GET.Contains("media"))
            {
                var mode = _GET["media"];
                if (mode == string.Empty) MediaMode = "page";
                else MediaMode = mode;
            }

            if (MediaMode == "cmp")
            {
                Action(Actions.SendFile);
                Location(new FileInfo(_SERVER["file-path"] + "cmpanel.html").FullName);
                return true;
            }

            if (File.Exists(cleanPath))
            {
                Action(Actions.SendFile);
                Location(new FileInfo(cleanPath).FullName);
                return true;
            }
            else
            {
                if (!path.EndsWith("/"))
                {
                    if (path == string.Empty || Directory.Exists(cleanPath))
                    {
                        Action(Actions.Redirect);
                        Location(url + '/');
                    }
                    else
                    {
                        StatusCode(400);
                    }
                    return true;
                }
                GetHtmlResult(cleanPath + '/');
                return true;
            }
        }

        private bool GetHtmlResult(string path)
        {
            if (path == "/")
            {
                MediaMode = "none";
                var ds = DriveInfo.GetDrives();
                string[] drives = new string[ds.Length];
                for (int i = 0; i < ds.Length; i++)
                {
                    drives[i] = ds[i].Name;
                }

                GetDirectoryHtml("Index of root", false, path, drives);
            }
            else if (Directory.Exists(path))
            {
                if (MediaMode == "page")
                {
                    var filePath = _SERVER["file-path"];
                    var mediaplayer = GetScript(filePath + "mediaplayer.cs", path);
                    if (mediaplayer.Run())
                    {
                        SetResult(mediaplayer.Result);
                        if (mediaplayer.ExtraText != null && mediaplayer.ExtraText.Length > 0)
                        {
                            echo(mediaplayer.ExtraText);
                        }
                    }
                    else StatusCode(404);
                    return true;
                }

                string modifiedTime = new DirectoryInfo(path).LastWriteTime.ToString("r");
                Header("Last-Modified", modifiedTime);
                Header("Cache-Control", "no-cache");

                if (_REQUEST_HEADER["if-modified-since"] == modifiedTime)
                {
                    StatusCode(304); //回應Client不須重新取得資源
                    Action(Actions.None);
                    return true;
                }

                GetDirectoryHtml("Index of " + path, true, path, Directory.GetFileSystemEntries(path));
            }
            else return false;

            Action(Actions.HTML);
            Header("content-type", "text/html;charset=UTF-8");
            return true;
        }

        private void GetDirectoryHtml(string title, bool back, string path, string[] files)
        {
            var mPanel = MediaMode == "panel";

            echo("<head>");
            echo("<title>" + title + "</title>");
            echo("<meta name='viewport' content='width=device-width,initial-scale=1'>");
            if (back)
            {
                echo("<script type='text/javascript'>function back(){window.location = \"../\" + window.location.search;}</script>");
            }
            echo("<script type='text/javascript'>function go(a){window.location = window.location.pathname + a.dataset.loc + \"/\" + window.location.search;}</script>");
            echo("<style type='text/css'>table{border-collapse: collapse;}table * {margin:3px;}tr:nth-child(even){background-color:#e2edf7;border-width:1 0 1 0;border-style:solid;}</style>");
            echo("</head><body>");
            echo("<h1>" + title + "</h1>");
            echo("<table><tbody>");
            if (back)
            {
                echo("<tr><td valign='top'><img src='/icons/back.gif' alt='[PARENTDIR]'></td><td><a href='javascript:;'");
                echo("onclick='back()'");
                echo(">Parent Directory</a></td><td></td><td align='right'>  - </td><td></td></tr>");
            }
            echo("<tr><th valign='top'></th><th>Name</th><th>Last modified</th><th>Size</th></tr>");
            echo("<tr><th colspan='4'><hr></th></tr>");
            foreach (var file in files)
            {
                if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    var di = new DirectoryInfo(file);
                    var dname = di.Name;
                    if (dname.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        dname = dname.Remove(dname.Length - 1);
                    var shortDname = dname.Length > MAX_FILENAME_LENGTH ?
                        dname.Substring(0, MAX_FILENAME_LENGTH - 3) + "..." :
                        dname;

                    echo("<tr><td valign='top'><img src='/icons/folder.gif' alt='[DIR]'></td><td><a href=\"javascript:;\" onclick='go(this)'");
                    echo("data-loc=\"" + dname + "\">");
                    echo(shortDname);
                    echo("/</a></td><td align='right'>");
                    echo(di.LastWriteTime.ToLocalTime());
                    echo("  </td><td align='right'>  - </td>");
                    if (mPanel)
                    {
                        echo("<td><input type='button' onclick=\"addMediaItemsFromButton(this,'audio,video')\" value='🞤'></input></td>");
                        echo("<td><input type='button' onclick=\"addMediaItemsFromButton(this,'audio')\" value='🞤Audio'></input></td>");
                        echo("<td><input type='button' onclick=\"addMediaItemsFromButton(this,'video')\" value='🞤Video'></input></td>");
                    }
                    echo("</tr>");
                }
                else
                {
                    var fi = new FileInfo(file);
                    var size = FormatFileSize(fi.Length);
                    var ext = fi.Extension;
                    var shortName = fi.Name;
                    if (shortName.Length > MAX_FILENAME_LENGTH)
                        shortName = shortName.Substring(0, MAX_FILENAME_LENGTH - 3 - ext.Length) + "..." + ext;

                    echo("<tr><td valign='top'><img src='/icons/file.gif' alt='[");
                    if (ext.Length > 0) echo(ext.Remove(0, 1).ToUpper());
                    else echo("???");
                    echo("]'></td><td><a href=\"");
                    echo(fi.Name);
                    echo("\"");
                    if (mPanel)
                    {
                        echo("target=\"_blank\"");
                        echo("name=\"" + fi.Name + "\"");
                    }
                    echo(">");
                    echo(shortName);
                    echo("</a></td><td align='right'>");
                    echo(fi.LastWriteTime.ToLocalTime());
                    echo("  </td><td align='right'>");
                    echo(size);
                    echo("</td>");
                    if (mPanel)
                    {
                        var mime = MIMEDetector.GetMIMEType(fi);
                        string type = null;
                        if (mime.StartsWith("audio/")) type = "Audio";
                        else if (mime.StartsWith("video/")) type = "Video";
                        if (type != null) echo("<td><input type='button' onclick=\"addMediaItemFromButton(this)\" value='🞤" + type + "'></input></td><td></td><td></td>");
                        else echo("<td></td><td></td><td></td>");
                    }
                    echo("</tr>");
                }
            }
            echo("</tbody></table></body>");
            if (mPanel)
            {
                echo("<script type='text/javascript'>");
                echo("var currentPath=\"" + path + "\";");
                echo(JS_MPANEL);
                echo("</script>");
            }
        }

        const string JS_MPANEL =
            "function addMediaItemsFromButton(button,type){" +
            "var a = button.parentElement.parentElement.querySelector(\"a\");" +
            "var path = a.dataset.loc;" +
            "addMediaItems(path,type);}"
            +
            "function addMediaItemFromButton(button){" +
            "var a = button.parentElement.parentElement.querySelector(\"a\");" +
            "var path = currentPath;" +
            "var name = a.name;" +
            "parent.addMedia(path,name);}"
            +
            "function addMediaItems(path,type){" +
            "var xhr=new XMLHttpRequest();" +
            "xhr.open('GET',path+\"?gt=\"+type,true);" +
            "xhr.onload=function(){if(xhr.status == 200){parent.batchAddMedia(JSON.parse(xhr.response));}};" +
            "xhr.send();}"
            ;

        private bool GenerateFileListJson(string path, string[] mime)
        {
            var files = Directory.GetFiles(path);

            StringBuilder sb = new StringBuilder();
            sb.Append("{\"path\":\"" + path + "/\"");
            sb.Append(",\"items\":[");
            int count = 0;
            foreach (var file in files)
            {
                count++;
                var fileInfo = new FileInfo(file);
                var type = MIMEDetector.GetMIMEType(fileInfo);
                if (mime == null || Array.Exists(mime, m => type.StartsWith(m)))
                {
                    sb.Append('"');
                    sb.Append(fileInfo.Name);
                    sb.Append("\",");
                }
            }
            if (count == 0) return false;
            sb.Remove(sb.Length - 1, 1);
            sb.Append("]}");
            echo(sb.ToString());
            return true;
        }

        private static string FormatFileSize(long size)
        {
            double len = size;
            int order = 0;
            while (len >= 1024 && order < Sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return Math.Round(len, 1) + Sizes[order];
        }

        private const string JS_ADD_MEDIA =
            "";
    }
}
